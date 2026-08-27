using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI.Demo
{
    [RequireComponent(typeof(RectTransform))]
    public class RectRouteMover : MonoBehaviour
    {
        [Header("Route")]
        [SerializeField] private List<RectTransform> routePoints = new();

        [Header("References")]
        [SerializeField] private OffScreenIndicator offScreenIndicator;

        [Header("Settings")]
        [SerializeField] private float duration = 0.35f;
        [SerializeField] private float waitBetween = 0.05f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Helpers
        RectTransform rt;
        Coroutine routeCoroutine;
        int currentIndex = 0;

        void Awake()
        {
            rt = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            if (routeCoroutine != null) { StopCoroutine(routeCoroutine); }
            routeCoroutine = StartCoroutine(RouteRoutine());
        }

        IEnumerator RouteRoutine()
        {
            if (routePoints == null || routePoints.Count == 0) { yield break; }
            while (true)
            {
                var dest = routePoints[currentIndex];
                yield return StartCoroutine(MoveToCoroutine(dest));
                if (waitBetween > 0f) { yield return new WaitForSeconds(waitBetween); }

                currentIndex++;
                if (currentIndex >= routePoints.Count) { currentIndex = 0; }
            }
        }

        IEnumerator MoveToCoroutine(RectTransform target)
        {
            if (target == null)
                yield break;

            var parentRect = rt.parent as RectTransform;
            if (parentRect == null)
            {
                rt.position = target.position;
                yield break;
            }

            Vector2 start = rt.anchoredPosition;
            Vector2 end = ConvertRectTransformToAnchoredPosition(target, parentRect);

            if (duration <= 0f)
            {
                rt.anchoredPosition = end;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                float eased = ease.Evaluate(norm);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, eased);
                yield return null;
            }

            rt.anchoredPosition = end;
        }

        Vector2 ConvertRectTransformToAnchoredPosition(RectTransform source, RectTransform parentRect)
        {
            Vector3 sourceWorldPos = source.TransformPoint(source.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, sourceWorldPos);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out Vector2 localPoint);
            return localPoint;
        }

        public void HideWhenOnScreen(bool value)
        {
            if (offScreenIndicator != null)
            {
                offScreenIndicator.hideWhenOnScreen = value;
            }
        }
    }
}