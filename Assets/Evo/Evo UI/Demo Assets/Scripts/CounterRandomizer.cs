using System.Collections;
using UnityEngine;

namespace Evo.UI.Demo
{
    [RequireComponent(typeof(Counter))]
    public class CounterRandomizer : MonoBehaviour
    {
        Counter counter;
        Coroutine updateCoroutine;

        readonly int minValue = 0;
        readonly int maxValue = 1000;

        void Awake()
        {
            counter = GetComponent<Counter>();
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
            if (counter == null)
                return;

            float randomValue = Random.Range(minValue, maxValue);
            randomValue = Mathf.Round(randomValue * 100f) / 100f;
            counter.SetValue(randomValue);
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
                SetRandomValue();
                yield return new WaitForSeconds(3.8f);
            }
        }
    }
}