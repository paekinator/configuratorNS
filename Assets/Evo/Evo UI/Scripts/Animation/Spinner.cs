using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Handles animated loading spinners using Image fill amounts and rotations.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "animation/spinner")]
    [AddComponentMenu("Evo/UI/Animation/Spinner")]
    public class Spinner : MonoBehaviour
    {
        [EvoHeader("Settings", Constants.CustomEditorID)]
        [SerializeField] private SpinnerStyle spinnerStyle = SpinnerStyle.Radial;
        [Range(0.1f, 10f)] public float rotationSpeed = 1f;
        [Range(0f, 1f)] public float minFillAmount = 0.1f;
        [Range(0f, 1f)] public float maxFillAmount = 0.8f;

        [EvoHeader("References", Constants.CustomEditorID)]
        public Image spinnerImage;

        public enum SpinnerStyle
        {
            Radial,
            Horizontal,
            Vertical
        }

        // Cache
        Coroutine spinnerCoroutine;
        RectTransform rectTransform;

        // Animation state
        float currentRotation;
        float fillPhase;
        bool pingPongReverse; // For horizontal/vertical ping-pong direction
        bool fillForward = true; // Whether we're filling 0 -> 1 or 1 -> 0

        // Cache Constants
        const float TwoPI = Mathf.PI * 2f;

        void Awake()
        {
            if (spinnerImage == null)
                TryGetComponent(out spinnerImage);

            if (spinnerImage == null)
                return;

            if (rectTransform == null)
                rectTransform = spinnerImage.GetComponent<RectTransform>();

            ConfigureImageForStyle();
            spinnerImage.fillAmount = minFillAmount;
        }

        void OnEnable() => StartSpinning();

        void OnDisable() => StopSpinning();

        void ConfigureImageForStyle()
        {
            switch (spinnerStyle)
            {
                case SpinnerStyle.Radial:
                    spinnerImage.type = Image.Type.Filled;
                    spinnerImage.fillMethod = Image.FillMethod.Radial360;
                    spinnerImage.fillOrigin = (int)Image.Origin360.Top;
                    break;

                case SpinnerStyle.Horizontal:
                    spinnerImage.type = Image.Type.Filled;
                    spinnerImage.fillMethod = Image.FillMethod.Horizontal;
                    spinnerImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                    break;

                case SpinnerStyle.Vertical:
                    spinnerImage.type = Image.Type.Filled;
                    spinnerImage.fillMethod = Image.FillMethod.Vertical;
                    spinnerImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                    break;
            }
        }

        /// <summary>
        /// Begins the spinner animation sequence. Automatically called OnEnable.
        /// </summary>
        public void StartSpinning()
        {
            if (spinnerImage == null || rectTransform == null)
                return;

            if (spinnerCoroutine != null) { StopCoroutine(spinnerCoroutine); }
            spinnerCoroutine = StartCoroutine(SpinnerAnimation());
        }

        /// <summary>
        /// Stops the spinner animation sequence. Automatically called OnDisable.
        /// </summary>
        public void StopSpinning()
        {
            if (spinnerCoroutine != null)
            {
                StopCoroutine(spinnerCoroutine);
                spinnerCoroutine = null;
            }
        }

        float EaseInOutCubic(float t)
        {
            if (t < 0.5f) { return 4f * t * t * t; }
            else
            {
                float p = 2f * t - 2f;
                return 1f + p * p * p / 2f;
            }
        }

        IEnumerator SpinnerAnimation()
        {
            while (true)
            {
                float deltaTime = Time.unscaledDeltaTime;

                if (spinnerStyle == SpinnerStyle.Radial)
                {
                    // Update fill amount with sine wave for smooth expansion/contraction
                    fillPhase += (rotationSpeed / 2f) * deltaTime * TwoPI;
                    float fillNormalized = (Mathf.Sin(fillPhase) + 1f) * 0.5f; // 0 to 1
                    float currentFill = Mathf.Lerp(minFillAmount, maxFillAmount, fillNormalized);

                    // Adjust rotation speed based on fill amount for Android-like behavior
                    // When fill is high, rotate faster to maintain visible motion
                    float fillBasedSpeedMultiplier = Mathf.Lerp(1f, 2f, currentFill);
                    float adjustedRotationSpeed = rotationSpeed * fillBasedSpeedMultiplier;

                    // Respect the fillClockwise setting for rotation direction
                    float rotationDirection = spinnerImage.fillClockwise ? -1f : 1f;

                    // Update rotation with adjusted speed and direction
                    currentRotation += adjustedRotationSpeed * 360f * deltaTime * rotationDirection;

                    // Keep rotation wrapped nicely to prevent float precision issues over extremely long play times
                    if (currentRotation >= 360f) { currentRotation -= 360f; }
                    else if (currentRotation <= -360f) { currentRotation += 360f; }

                    rectTransform.rotation = Quaternion.Euler(0, 0, currentRotation);
                    spinnerImage.fillAmount = currentFill;
                }
                else // Horizontal or Vertical ping-pong
                {
                    if (fillForward)
                    {
                        // Filling 0 -> 1
                        fillPhase += rotationSpeed * deltaTime;
                        if (fillPhase >= 1f)
                        {
                            fillPhase = 1f;
                            fillForward = false; // Next will be 1 -> 0

                            // Switch origin for next cycle
                            if (spinnerStyle == SpinnerStyle.Horizontal)
                            {
                                spinnerImage.fillOrigin = pingPongReverse ?
                                    (int)Image.OriginHorizontal.Left :
                                    (int)Image.OriginHorizontal.Right;
                            }
                            else if (spinnerStyle == SpinnerStyle.Vertical)
                            {
                                spinnerImage.fillOrigin = pingPongReverse ?
                                    (int)Image.OriginVertical.Bottom :
                                    (int)Image.OriginVertical.Top;
                            }
                        }
                    }
                    else
                    {
                        // Filling 1 -> 0
                        fillPhase -= rotationSpeed * deltaTime;
                        if (fillPhase <= 0f)
                        {
                            fillPhase = 0f;
                            fillForward = true; // Next will be 0 -> 1
                            pingPongReverse = !pingPongReverse; // Toggle direction after full cycle

                            // Set origin for next cycle
                            if (spinnerStyle == SpinnerStyle.Horizontal)
                            {
                                spinnerImage.fillOrigin = pingPongReverse ?
                                    (int)Image.OriginHorizontal.Right :
                                    (int)Image.OriginHorizontal.Left;
                            }
                            else if (spinnerStyle == SpinnerStyle.Vertical)
                            {
                                spinnerImage.fillOrigin = pingPongReverse ?
                                    (int)Image.OriginVertical.Top :
                                    (int)Image.OriginVertical.Bottom;
                            }
                        }
                    }

                    // Apply smooth easing curve to the fill amount
                    float easedFill = EaseInOutCubic(fillPhase);
                    spinnerImage.fillAmount = easedFill;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Updates the visual style of the spinner dynamically at runtime.
        /// </summary>
        public void SetSpinnerStyle(SpinnerStyle style)
        {
            spinnerStyle = style;
            ConfigureImageForStyle();
        }
    }
}