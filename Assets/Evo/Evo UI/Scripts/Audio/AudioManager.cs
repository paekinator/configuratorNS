using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Manages audio-related methods for runtime usage.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl)]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Evo/UI/Audio/Audio Manager")]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public AudioSource AudioSource { get; private set; }

        [Header("Settings")]
        [Tooltip("Adds the attached object to DontDestroyOnLoad when enabled.")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            AudioSource = GetComponent<AudioSource>();

            if (dontDestroyOnLoad)
            {
                Instance.transform.SetParent(null);
                DontDestroyOnLoad(Instance.gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        static void CreateInstance()
        {
            var go = new GameObject("[Evo UI - Audio]");
            go.AddComponent<AudioSource>();
            go.AddComponent<AudioManager>();
        }

        /// <summary>
        /// Plays an audio clip via AudioSource.
        /// </summary>
        public static void PlayClip(AudioClip clip, float clipVolume = 1)
        {
            if (clip == null)
                return;

            if (Instance == null)
                CreateInstance();

            Instance.AudioSource.PlayOneShot(clip, Mathf.Clamp01(clipVolume));
        }
    }
}