using System.Collections;
using UnityEngine;

namespace Evo.UI.Demo
{
    [RequireComponent(typeof(ProgressBar))]
    public class ProgressBarRandomizer : MonoBehaviour
    {
        ProgressBar progressBar;
        Coroutine updateCoroutine;

        void Awake()
        {
            progressBar = GetComponent<ProgressBar>();
        }

        void OnEnable()
        {
            StopAutoUpdate();
            updateCoroutine = StartCoroutine(AutoUpdateCoroutine());
        }

        void OnDisable()
        {
            StopAutoUpdate();
        }

        void SetRandomValue()
        {
            if (progressBar == null)
                return;

            float randomValue = Random.Range(progressBar.MinValue, progressBar.MaxValue);
            randomValue = Mathf.Round(randomValue * 100f) / 100f;
            progressBar.Value = randomValue;
        }

        void StopAutoUpdate()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = null;
            }
        }

        IEnumerator AutoUpdateCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.6f);
                SetRandomValue();
            }
        }
    }
}