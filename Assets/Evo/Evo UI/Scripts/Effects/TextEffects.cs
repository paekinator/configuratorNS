using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [HelpURL(Constants.HelpUrl)]
    [AddComponentMenu("Evo/UI/Effects/Text Effects (Preview)")]
    [RequireComponent(typeof(TMP_Text))]
    public class TextEffects : MonoBehaviour
    {
        [Header("References")]
        public TMP_Text textComponent;

        [Header("Character")]
        public TypewriterFX typewriter;
        public GlitchFX glitch;

        [Header("Color & Overlay")]
        public AlphaPulseFX alphaPulse;
        public PaletteFX palette;
        public RainbowFX rainbow;
        public ShimmerFX shimmer;

        [Header("Motion")]
        public BounceFX bounce;
        public ShakeFX shake;
        public WaveFX wave;

        [Header("Text Emphasis")]
        public PulseFX pulse;
        public SwingFX swing;
        public WiggleFX wiggle;

        // Internal State
        bool isTyping;
        TMP_TextInfo textInfo;
        Coroutine typewriterCoroutine;
        Coroutine glitchCoroutine;

        // Glitch & Tag State
        string cleanText;
        char[] bufferText;

        // Memory Cache
        bool hasActiveTags = false;
        bool needsMeshUpdate = false;
        EffectType[] characterEffectMap;
        Vector3[][] cachedVertices;
        Color32[][] cachedColors;

        // Parsing Caches
        readonly StringBuilder stringBuilderCache = new();
        readonly List<EffectType> activeStackCache = new();
        readonly List<EffectType> mapListCache = new();

        static readonly Regex TagRegex = new(@"</?(wave|bounce|shake|wiggle|swing|pulse|rainbow|palette|alphapulse|shimmer|glitch)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Enums
        [Flags] enum EffectType
        {
            None = 0,
            Wave = 1 << 0,
            Bounce = 1 << 1,
            Shake = 1 << 2,
            Wiggle = 1 << 3,
            Swing = 1 << 4,
            Pulse = 1 << 5,
            Rainbow = 1 << 6,
            Palette = 1 << 7,
            AlphaPulse = 1 << 8,
            Shimmer = 1 << 9,
            Glitch = 1 << 10
        }

        // Properties
        public bool IsTyping => isTyping;

        void Awake()
        {
            if (textComponent == null)
                textComponent = GetComponent<TMP_Text>();

            if (typewriter.audioClip != null && typewriter.audioSource == null)
                TryGetComponent(out typewriter.audioSource);

            if (!string.IsNullOrEmpty(textComponent.text))
            {
                ParseText(textComponent.text);
                textComponent.text = cleanText;
            }
        }

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMProTextChanged);

            if (typewriter.playOnEnable)
                PlayTypewriter();

            CheckGlitchState();
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMProTextChanged);
        }

        void Update()
        {
            if (needsMeshUpdate)
            {
                needsMeshUpdate = false;
                UpdateMeshCache();
            }

            CheckGlitchState();

            if (textComponent.textInfo == null || textComponent.textInfo.characterCount == 0)
                return;

            if (ShouldAnimate())
                AnimateVertices();
        }

        #region Internal Logic
        void OnTMProTextChanged(UnityEngine.Object obj)
        {
            if (obj == textComponent)
                needsMeshUpdate = true;
        }

        void UpdateMeshCache()
        {
            textComponent.ForceMeshUpdate();
            textInfo = textComponent.textInfo;

            int materialCount = textInfo.meshInfo.Length;

            // Array Pooling
            if (cachedVertices == null || cachedVertices.Length < materialCount)
            {
                cachedVertices = new Vector3[materialCount][];
                cachedColors = new Color32[materialCount][];
            }

            for (int i = 0; i < materialCount; i++)
            {
                var sourceMesh = textInfo.meshInfo[i];
                int vertexCount = sourceMesh.vertices.Length;

                if (cachedVertices[i] == null || cachedVertices[i].Length < vertexCount)
                {
                    // Allocate powers of 2 to minimize future resizing
                    int newSize = Mathf.NextPowerOfTwo(Mathf.Max(vertexCount, 256));
                    cachedVertices[i] = new Vector3[newSize];
                    cachedColors[i] = new Color32[newSize];
                }

                Array.Copy(sourceMesh.vertices, cachedVertices[i], vertexCount);
                Array.Copy(sourceMesh.colors32, cachedColors[i], vertexCount);
            }
        }

        void CheckGlitchState()
        {
            bool shouldRun = glitch.enabled || hasActiveTags;

            if (shouldRun && glitchCoroutine == null) { glitchCoroutine = StartCoroutine(GlitchRoutine()); }
            else if (!shouldRun && glitchCoroutine != null)
            {
                StopCoroutine(glitchCoroutine);
                glitchCoroutine = null;

                if (!string.IsNullOrEmpty(cleanText))
                    textComponent.text = cleanText;
            }
        }

        void ParseText(string input)
        {
            stringBuilderCache.Clear();
            activeStackCache.Clear();
            mapListCache.Clear();
            hasActiveTags = false;
            int lastIndex = 0;

            foreach (Match match in TagRegex.Matches(input))
            {
                // Process text before the tag without substring allocation
                for (int i = lastIndex; i < match.Index; i++)
                {
                    stringBuilderCache.Append(input[i]);
                    EffectType mask = GetCurrentEffectMask();
                    mapListCache.Add(mask);
                    if (mask != EffectType.None) hasActiveTags = true;
                }

                string tagName = match.Groups[1].Value.ToLower();
                bool isClose = input[match.Index + 1] == '/';
                EffectType effect = TagToEffect(tagName);

                if (effect != EffectType.None)
                {
                    if (isClose) { activeStackCache.Remove(effect); }
                    else { activeStackCache.Add(effect); }
                }

                lastIndex = match.Index + match.Length;
            }

            // Process remaining text
            for (int i = lastIndex; i < input.Length; i++)
            {
                stringBuilderCache.Append(input[i]);
                EffectType mask = GetCurrentEffectMask();
                mapListCache.Add(mask);

                if (mask != EffectType.None)
                    hasActiveTags = true;
            }

            cleanText = stringBuilderCache.ToString();

            // Resize map array
            if (characterEffectMap == null || characterEffectMap.Length < mapListCache.Count)
                characterEffectMap = new EffectType[Mathf.NextPowerOfTwo(Mathf.Max(mapListCache.Count, 64))];

            for (int i = 0; i < mapListCache.Count; i++)
                characterEffectMap[i] = mapListCache[i];
        }

        EffectType GetCurrentEffectMask()
        {
            EffectType mask = EffectType.None;

            for (int i = 0; i < activeStackCache.Count; i++)
                mask |= activeStackCache[i];
           
            return mask;
        }

        EffectType TagToEffect(string tag)
        {
            return tag switch
            {
                "wave" => EffectType.Wave,
                "bounce" => EffectType.Bounce,
                "shake" => EffectType.Shake,
                "wiggle" => EffectType.Wiggle,
                "swing" => EffectType.Swing,
                "pulse" => EffectType.Pulse,
                "rainbow" => EffectType.Rainbow,
                "palette" => EffectType.Palette,
                "alphapulse" => EffectType.AlphaPulse,
                "shimmer" => EffectType.Shimmer,
                "glitch" => EffectType.Glitch,
                _ => EffectType.None,
            };
        }

        bool ShouldAnimate()
        {
            if (hasActiveTags)
                return true;
            
            if (wave.enabled || bounce.enabled || shake.enabled)
                return true;
           
            if (wiggle.enabled || swing.enabled || pulse.enabled)
                return true;
            
            if (rainbow.enabled || palette.enabled || alphaPulse.enabled || shimmer.enabled)
                return true;

            return false;
        }

        IEnumerator GlitchRoutine()
        {
            if (string.IsNullOrEmpty(cleanText))
                yield break;

            WaitForSeconds wait = new(0.05f);

            while (true)
            {
                // Only allocate new array if the buffer is uninitialized or wrong size
                if (bufferText == null || bufferText.Length != cleanText.Length)
                    bufferText = cleanText.ToCharArray();

                bool changed = false;
                float time = Time.time;
                int len = bufferText.Length;

                for (int i = 0; i < len; i++)
                {
                    bool active = glitch.enabled;

                    if (!active && characterEffectMap != null && i < characterEffectMap.Length)
                        active = (characterEffectMap[i] & EffectType.Glitch) != 0;

                    if (!active)
                    {
                        if (bufferText[i] != cleanText[i])
                        {
                            bufferText[i] = cleanText[i];
                            changed = true;
                        }
                        continue;
                    }

                    char original = cleanText[i];

                    if (original == '\n' || original == ' ' || original == '<' || original == '>')
                        continue;

                    float noise = Mathf.PerlinNoise(i * 0.3f, time * glitch.speed * 0.1f);
                    if (noise > (1.0f - glitch.intensity))
                    {
                        char randomChar = glitch.glitchAlphabet[UnityEngine.Random.Range(0, glitch.glitchAlphabet.Length)];
                        if (bufferText[i] != randomChar)
                        {
                            bufferText[i] = randomChar;
                            changed = true;
                        }
                    }
                    else if (bufferText[i] != original)
                    {
                        bufferText[i] = original;
                        changed = true;
                    }
                }

                if (changed)
                    textComponent.SetCharArray(bufferText, 0, bufferText.Length);

                yield return wait;
            }
        }

        IEnumerator TypewriterRoutine()
        {
            isTyping = true;
            textComponent.maxVisibleCharacters = 0;
            yield return null;

            int totalCharacters = textInfo.characterCount;
            float timer = 0f;
            int currentVisible = 0;
            float delayPerChar = 1.0f / Mathf.Max(0.1f, typewriter.speed);

            while (currentVisible < totalCharacters)
            {
                if (textComponent.textInfo.characterCount != totalCharacters)
                    totalCharacters = textComponent.textInfo.characterCount;

                timer += Time.deltaTime;
                int charsProcessed = 0;

                while (timer >= delayPerChar && charsProcessed < 100)
                {
                    timer -= delayPerChar;
                    currentVisible++;
                    charsProcessed++;

                    if (typewriter.audioClip != null && typewriter.audioSource != null)
                    {
                        float originalPitch = 1.0f;
                        typewriter.audioSource.pitch = originalPitch + UnityEngine.Random.Range(-typewriter.pitchVariation, typewriter.pitchVariation);
                        typewriter.audioSource.PlayOneShot(typewriter.audioClip, typewriter.volume);
                        typewriter.audioSource.pitch = originalPitch;
                    }
                }

                textComponent.maxVisibleCharacters = currentVisible;
                yield return null;
            }

            textComponent.maxVisibleCharacters = totalCharacters;
            isTyping = false;
        }

        void AnimateVertices()
        {
            if (cachedVertices == null || cachedVertices.Length != textInfo.meshInfo.Length)
            {
                UpdateMeshCache();

                if (cachedVertices == null)
                    return;
            }

            int characterCount = textInfo.characterCount;
            float time = Time.time;
            float shimmerCycleTime = 0f;

            if (shimmer.enabled || hasActiveTags)
            {
                float activeDuration = 1f / Mathf.Max(0.01f, shimmer.speed);
                float shimmerLoopDuration = activeDuration + shimmer.restDuration;
                shimmerCycleTime = Mathf.Repeat(time, shimmerLoopDuration);
            }

            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible)
                    continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                if (materialIndex >= cachedVertices.Length || materialIndex >= textInfo.meshInfo.Length)
                    continue;

                Vector3[] sourceVertices = cachedVertices[materialIndex];
                Vector3[] destinationVertices = textInfo.meshInfo[materialIndex].vertices;

                if (vertexIndex + 3 >= sourceVertices.Length)
                    continue;

                EffectType activeMask = EffectType.None;
                if (characterEffectMap != null && i < characterEffectMap.Length)
                    activeMask = characterEffectMap[i];

                // Movement
                Vector3 offset = Vector3.zero;

                if (wave.enabled || (activeMask & EffectType.Wave) != 0)
                    offset.y += Mathf.Sin(time * wave.speed + i * wave.density) * wave.height;
               
                if (bounce.enabled || (activeMask & EffectType.Bounce) != 0)
                    offset.y += Mathf.Abs(Mathf.Sin(time * bounce.speed + i * bounce.density)) * bounce.height;
              
                if (shake.enabled || (activeMask & EffectType.Shake) != 0)
                {
                    float seed = time * shake.speed + (i * 10f);
                    offset.x += (Mathf.PerlinNoise(seed, 0) - 0.5f) * shake.strength;
                    offset.y += (Mathf.PerlinNoise(0, seed) - 0.5f) * shake.strength;
                }

                Vector3 v0 = sourceVertices[vertexIndex + 0] + offset;
                Vector3 v1 = sourceVertices[vertexIndex + 1] + offset;
                Vector3 v2 = sourceVertices[vertexIndex + 2] + offset;
                Vector3 v3 = sourceVertices[vertexIndex + 3] + offset;

                // Emphasis
                if (wiggle.enabled || swing.enabled || pulse.enabled || (activeMask & (EffectType.Wiggle | EffectType.Swing | EffectType.Pulse)) != 0)
                {
                    Vector3 center = (v0 + v2) / 2f;
                    Matrix4x4 matrix = Matrix4x4.identity;

                    if (wiggle.enabled || (activeMask & EffectType.Wiggle) != 0)
                    {
                        float angle = Mathf.Sin(time * wiggle.speed + i) * wiggle.angle;
                        matrix = Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one) * Matrix4x4.Translate(-center);
                    }

                    if (swing.enabled || (activeMask & EffectType.Swing) != 0)
                    {
                        Vector3 topCenter = (v1 + v2) / 2f;
                        float angle = Mathf.Sin(time * swing.speed + i) * swing.angle;
                        matrix = (Matrix4x4.TRS(topCenter, Quaternion.Euler(0, 0, angle), Vector3.one) * Matrix4x4.Translate(-topCenter)) * matrix;
                    }

                    if (pulse.enabled || (activeMask & EffectType.Pulse) != 0)
                    {
                        float s = 1f + (Mathf.Sin(time * pulse.speed + i) * (pulse.scaleMultiplier - 1f));
                        matrix = (Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * s) * Matrix4x4.Translate(-center)) * matrix;
                    }

                    v0 = matrix.MultiplyPoint3x4(v0);
                    v1 = matrix.MultiplyPoint3x4(v1);
                    v2 = matrix.MultiplyPoint3x4(v2);
                    v3 = matrix.MultiplyPoint3x4(v3);
                }

                destinationVertices[vertexIndex + 0] = v0;
                destinationVertices[vertexIndex + 1] = v1;
                destinationVertices[vertexIndex + 2] = v2;
                destinationVertices[vertexIndex + 3] = v3;

                // Color
                Color32[] sourceColors = cachedColors[materialIndex];
                Color32[] destinationColors = textInfo.meshInfo[materialIndex].colors32;

                if (vertexIndex + 3 < sourceColors.Length)
                {
                    Color target = sourceColors[vertexIndex];

                    if (rainbow.enabled || (activeMask & EffectType.Rainbow) != 0)
                    {
                        float hue = Mathf.Repeat(time * rainbow.speed + (i * rainbow.density * 0.1f), 1f);
                        target = Color.HSVToRGB(hue, rainbow.saturation, rainbow.brightness);
                    }
                    else if ((palette.enabled || (activeMask & EffectType.Palette) != 0) && palette.gradient != null)
                    {
                        float t = Mathf.Repeat(time * palette.speed + (i * palette.density * 0.1f), 1f);
                        target = palette.gradient.Evaluate(t);
                    }

                    if (alphaPulse.enabled || (activeMask & EffectType.AlphaPulse) != 0)
                    {
                        float a = Mathf.Lerp(alphaPulse.minAlpha, 1f, (Mathf.Sin(time * alphaPulse.speed - i) + 1f) / 2f);
                        target.a *= a;
                    }

                    if (shimmer.enabled || (activeMask & EffectType.Shimmer) != 0)
                    {
                        float activeDuration = 1f / Mathf.Max(0.01f, shimmer.speed);
                        float intensity = 0f;

                        if (shimmerCycleTime < activeDuration)
                        {
                            float normalizedIndex = (float)i / (float)(characterCount > 1 ? characterCount - 1 : 1);
                            float progress = shimmerCycleTime / activeDuration;

                            float beamCenter = Mathf.Lerp(-shimmer.highlightWidth, 1f + shimmer.highlightWidth, progress);
                            float dist = Mathf.Abs(normalizedIndex - beamCenter);
                            if (dist < shimmer.highlightWidth) { intensity = Mathf.Cos((dist / shimmer.highlightWidth) * (Mathf.PI / 2f)); }
                        }

                        target.a = Mathf.Lerp(shimmer.baseOpacity, 1f, intensity);
                    }

                    Color32 c32 = target;
                    destinationColors[vertexIndex + 0] = c32;
                    destinationColors[vertexIndex + 1] = c32;
                    destinationColors[vertexIndex + 2] = c32;
                    destinationColors[vertexIndex + 3] = c32;
                }
            }

            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Call this method to ensure tags are parsed correctly at runtime.
        /// </summary>
        public void SetText(string newText) => SetText(newText, false);

        /// <summary>
        /// Call this method to ensure tags are parsed correctly at runtime.
        /// </summary>
        public void SetText(string newText, bool startTypewriter = false)
        {
            ParseText(newText);
            textComponent.text = cleanText;
            needsMeshUpdate = true;

            if (startTypewriter)
                PlayTypewriter();
        }

        public void PlayTypewriter()
        {
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);

            if (string.IsNullOrEmpty(cleanText))
            {
                ParseText(textComponent.text);
                textComponent.text = cleanText;
            }

            textComponent.ForceMeshUpdate();
            UpdateMeshCache();
            typewriterCoroutine = StartCoroutine(TypewriterRoutine());
        }

        public void SkipTypewriter()
        {
            if (typewriterCoroutine != null)
                StopCoroutine(typewriterCoroutine);

            textComponent.maxVisibleCharacters = 99999;
            isTyping = false;
        }
        #endregion

        #region FX Classses
        [Serializable]
        public class TypewriterFX
        {
            public bool playOnEnable = false;
            [Tooltip("Speed in characters per second.")]
            [Min(0.1f)] public float speed = 30f;

            [Header("Audio")]
            [Tooltip("Optional: Plays this sound on every character.")]
            public AudioClip audioClip;
            [Tooltip("Optional: If null, will try to find one on this object.")]
            public AudioSource audioSource;
            [Range(0f, 2f)] public float volume = 0.5f;
            [Tooltip("Randomizes pitch slightly for a more natural feel.")]
            [Range(0f, 0.5f)] public float pitchVariation = 0.05f;
        }

        [Serializable]
        public class GlitchFX
        {
            public bool enabled = false;

            [Tooltip("How fast the glitch pattern moves.")]
            [Min(0f)] public float speed = 20f;
            [Tooltip("How much of the text is glitched at any moment (0.0 to 1.0).")]
            [Range(0f, 1f)] public float intensity = 0.1f;
            [Tooltip("The characters used for the glitch effect.")]
            public string glitchAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        }

        [Serializable]
        public class WaveFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 4f;
            [Min(0f)] public float height = 5f;
            [Tooltip("How many waves fit in the text (Spatial Density).")]
            [Min(0f)] public float density = 2f;
        }

        [Serializable]
        public class BounceFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float height = 5f;
            [Tooltip("How crowded the bounces are.")]
            [Min(0f)] public float density = 1f;
        }

        [Serializable]
        public class ShakeFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 10f;
            [Tooltip("Maximum distance the text can jitter.")]
            [Min(0f)] public float strength = 2f;
        }

        [Serializable]
        public class WiggleFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float angle = 10f;
        }

        [Serializable]
        public class SwingFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float angle = 15f;
        }

        [Serializable]
        public class PulseFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 3f;
            [Min(0f)] public float scaleMultiplier = 1.2f;
        }

        [Serializable]
        public class RainbowFX
        {
            public bool enabled = false;

            public float speed = 1f;
            [Tooltip("How spread out the rainbow is.")]
            [Min(0f)] public float density = 0.5f;
            [Range(0f, 1f)] public float saturation = 1f;
            [Range(0f, 1f)] public float brightness = 1f;
        }

        [Serializable]
        public class PaletteFX
        {
            public bool enabled = false;

            public Gradient gradient;
            public float speed = 1f;
            [Min(0f)] public float density = 0.5f;
        }

        [Serializable]
        public class AlphaPulseFX
        {
            public bool enabled = false;

            [Min(0f)] public float speed = 2f;
            [Tooltip("The lowest opacity the text reaches during the pulse.")]
            [Range(0f, 1f)] public float minAlpha = 0.2f;
        }

        [Serializable]
        public class ShimmerFX
        {
            public bool enabled = false;

            [Tooltip("How fast the shimmer beam moves across the text.")]
            public float speed = 1f;
            [Tooltip("Time in seconds to wait after the shimmer finishes before starting again.")]
            [Min(0f)] public float restDuration = 0.3f;
            [Tooltip("The width of the beam (0.0 to 1.0 relative to text length).")]
            [Range(0.1f, 1f)] public float highlightWidth = 0.3f;
            [Tooltip("The resting opacity of the text (0 = invisible, 1 = fully visible). The beam brings it up to 1.")]
            [Range(0f, 1f)] public float baseOpacity = 0.1f;
        }
        #endregion

#if UNITY_EDITOR
        void Reset() => textComponent = GetComponent<TMP_Text>();
#endif
    }
}