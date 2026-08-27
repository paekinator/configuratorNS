using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Evo/UI/Effects/Blur Overlay")]
    public class BlurOverlay : MonoBehaviour
    {
        [Header("Blur Settings")]
        [Range(0, 20)] public float blurRadius = 6;
        [Range(1, 6)] public int blurSmoothness = 4;
        [Range(1, 4)] public int baseDownsample = 2;

        [Header("Output Adjustments")]
        [Range(0, 5)] public float exposure = 1;
        [Range(0, 5)] public float saturation = 1;
        [Range(0f, 0.1f)] public float noiseStrength = 0;

        [Header("Performance")]
        [Tooltip("0 = Only update on enable (zero ongoing cost - best for pause menus)\n1+ = Update every N frames (higher = better performance)")]
        [Range(0, 10)] public int updateInterval = 1;

        [Header("References")]
        [SerializeField] private Camera targetCamera;

        // Properties
        public Camera TargetCamera
        {
            get
            {
                if (targetCamera != null)
                    return targetCamera;

                if (Camera.main != null)
                {
                    targetCamera = Camera.main;
                    return targetCamera;
                }

                return null;
            }
            set => targetCamera = value;
        }

        // Cache
        Image imageObj;
        Material blurMaterial;
        Material instancedMaterial;
        RenderTexture finalBlurredTexture;
        [SerializeField, HideInInspector] private Shader embeddedBlurShader;
        [SerializeField, HideInInspector] private Shader embeddedUIShader;

        // Shader IDs
        static readonly string DualKawaseBlurShader = "Hidden/Evo/UI/DualKawaseBlur";
        static readonly string BlurOverlayShader = "Evo/UI/Blur Overlay";
        static readonly int BackgroundTexID = Shader.PropertyToID("_BackgroundTex");
        static readonly int ExposureID = Shader.PropertyToID("_Exposure");
        static readonly int SaturationID = Shader.PropertyToID("_Saturation");
        static readonly int NoiseStrengthID = Shader.PropertyToID("_NoiseStrength");
        static readonly int OffsetID = Shader.PropertyToID("_Offset");

        void Awake()
        {
            imageObj = GetComponent<Image>();
            imageObj.enabled = false;
        }

        void Start()
        {
            if (ValidateResources())
            {
                RegisterWithManager();
                Render();
            }
        }

        void OnEnable()
        {
            if (ValidateResources())
            {
                RegisterWithManager();
                Render();
            }
        }

        void OnDisable() => UnregisterFromManager();

        void OnDestroy()
        {
            UnregisterFromManager();
            CleanupTextures();
            DestroySafe(instancedMaterial);
            DestroySafe(blurMaterial);

            if (imageObj != null) { imageObj.material = null; }
            instancedMaterial = null;
            blurMaterial = null;
        }

        void RegisterWithManager()
        {
            if (TargetCamera != null)
            {
                BlurOverlayManager.Instance.RegisterOverlay(this, TargetCamera, updateInterval);
            }
        }

        void UnregisterFromManager()
        {
            if (TargetCamera != null)
            {
                BlurOverlayManager.Instance.UnregisterOverlay(this, TargetCamera);
            }
        }

        void CleanupTextures()
        {
            if (finalBlurredTexture != null)
            {
                finalBlurredTexture.Release();
                DestroySafe(finalBlurredTexture);
                finalBlurredTexture = null;
            }
        }

        void DestroySafe(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying) { Destroy(obj); }
            else { DestroyImmediate(obj); }
        }

        bool ValidateResources()
        {
            if (TargetCamera == null || imageObj == null)
                return false;

            // UI material
            if (instancedMaterial == null)
            {
                // Try embedded reference first, fall back to Find()
                Shader uiShader = embeddedUIShader;
                if (uiShader == null) { uiShader = Shader.Find(BlurOverlayShader); }
                if (uiShader == null)
                {
                    Debug.LogWarning($"[Blur Overlay] Shader '{BlurOverlayShader}' not found. Ensure the shader is in your project.");
                    return false;
                }

                instancedMaterial = new Material(uiShader) { hideFlags = HideFlags.HideAndDontSave };
                imageObj.material = instancedMaterial;
            }
            else if (imageObj.material != instancedMaterial)
            {
                imageObj.material = instancedMaterial;
            }

            // Blur material
            if (blurMaterial == null)
            {
                // Try embedded reference first, fall back to Find()
                Shader blurShader = embeddedBlurShader;

                if (blurShader == null)
                    blurShader = Shader.Find(DualKawaseBlurShader);

                if (blurShader == null)
                {
                    Debug.LogWarning($"[Blur Overlay] Shader '{DualKawaseBlurShader}' not found. Ensure the shader is in your project.");
                    return false;
                }

                blurMaterial = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        void PerformBlur(RenderTexture capturedTexture)
        {
            if (capturedTexture == null || blurMaterial == null)
                return;

            Texture sourceTexture = capturedTexture;
            int w = sourceTexture.width;
            int h = sourceTexture.height;

            // Determine max downsample iterations based on blur radius
            int allowedDownsamples = 0;
            if (blurRadius > 1.0f) { allowedDownsamples = 1; }
            if (blurRadius > 3.0f) { allowedDownsamples = 2; }
            if (blurRadius > 6.0f) { allowedDownsamples = 3; }
            if (blurRadius > 10.0f) { allowedDownsamples = 4; }

            float variableSpread = Mathf.Max(0.5f, blurRadius / blurSmoothness);

            RenderTexture[] rtChain = new RenderTexture[blurSmoothness];
            RenderTexture currentSource = null;

            // Downsample pass
            for (int i = 0; i < blurSmoothness; i++)
            {
                bool dropRes = i < allowedDownsamples;
                if (dropRes)
                {
                    w = Mathf.Max(2, w / 2);
                    h = Mathf.Max(2, h / 2);
                }

                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
                rt.filterMode = FilterMode.Bilinear;
                rt.wrapMode = TextureWrapMode.Clamp;

                float offset = 0.5f + (variableSpread * i);
                blurMaterial.SetFloat(OffsetID, offset);

                Graphics.Blit(currentSource == null ? sourceTexture : currentSource, rt, blurMaterial, 0);

                currentSource = rt;
                rtChain[i] = rt;
            }

            // Upsample pass
            for (int i = blurSmoothness - 2; i >= 0; i--)
            {
                RenderTexture baseRT = rtChain[i];
                RenderTexture inputRT = rtChain[i + 1];

                RenderTexture upsampledRT = RenderTexture.GetTemporary(baseRT.width, baseRT.height, 0, RenderTextureFormat.Default);
                upsampledRT.filterMode = FilterMode.Bilinear;
                upsampledRT.wrapMode = TextureWrapMode.Clamp;

                float offset = 0.5f + (variableSpread * i);
                blurMaterial.SetFloat(OffsetID, offset);

                Graphics.Blit(inputRT, upsampledRT, blurMaterial, 1);

                RenderTexture.ReleaseTemporary(inputRT);
                RenderTexture.ReleaseTemporary(baseRT);

                rtChain[i] = upsampledRT;
            }

            // Final texture - only recreate if dimensions changed
            if (finalBlurredTexture == null || finalBlurredTexture.width != rtChain[0].width || finalBlurredTexture.height != rtChain[0].height)
            {
                if (finalBlurredTexture != null)
                {
                    finalBlurredTexture.Release();
                    DestroySafe(finalBlurredTexture);
                }

                finalBlurredTexture = new RenderTexture(rtChain[0].width, rtChain[0].height, 0, RenderTextureFormat.Default)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false
                };
            }

            Graphics.Blit(rtChain[0], finalBlurredTexture);
            RenderTexture.ReleaseTemporary(rtChain[0]);

            // Set material tex
            if (instancedMaterial != null) { instancedMaterial.SetTexture(BackgroundTexID, finalBlurredTexture); }
        }

        void UpdateMaterialProperties()
        {
            if (instancedMaterial == null)
                return;

            instancedMaterial.SetFloat(ExposureID, exposure);
            instancedMaterial.SetFloat(SaturationID, saturation);
            instancedMaterial.SetFloat(NoiseStrengthID, noiseStrength);
        }

        void UpdateVisibility()
        {
            if (imageObj == null)
                return;

            bool isValid = finalBlurredTexture != null && finalBlurredTexture.IsCreated();
            if (imageObj.enabled != isValid) { imageObj.enabled = isValid; }
        }

        /// <summary>
        /// Called by BlurOverlayManager when shared capture is updated.
        /// </summary>
        public void OnSharedCaptureUpdated() => Render();

        /// <summary>
        /// Manually trigger a blur render. Useful when updateInterval is 0 and you need to refresh.
        /// </summary>
        public void Render()
        {
            if (!ValidateResources())
            {
                if (imageObj != null && imageObj.enabled)
                    imageObj.enabled = false;

                return;
            }

            // Get shared capture from manager
            RenderTexture capturedTexture = BlurOverlayManager.Instance.GetCaptureTexture(TargetCamera, baseDownsample);
            if (capturedTexture == null)
            {
                if (imageObj != null && imageObj.enabled)
                    imageObj.enabled = false;

                return;
            }

            PerformBlur(capturedTexture);
            UpdateMaterialProperties();
            UpdateVisibility();
            imageObj.SetMaterialDirty();
        }

#if UNITY_EDITOR
        void Reset()
        {
            // Auto-assign shaders when component is added
            embeddedBlurShader = Shader.Find(DualKawaseBlurShader);
            embeddedUIShader = Shader.Find(BlurOverlayShader);
        }

        void OnValidate()
        {
            // Ensure shaders are assigned even if Reset() wasn't called (e.g. existing prefabs)
            if (embeddedBlurShader == null) embeddedBlurShader = Shader.Find(DualKawaseBlurShader);
            if (embeddedUIShader == null) embeddedUIShader = Shader.Find(BlurOverlayShader);

            if (!gameObject.activeInHierarchy)
                return;

            if (Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= OnValidateDelayCall;
                UnityEditor.EditorApplication.delayCall += OnValidateDelayCall;
            }
            else
            {
                // Editor preview
                UnityEditor.EditorApplication.delayCall -= EditorPreviewDelayCall;
                UnityEditor.EditorApplication.delayCall += EditorPreviewDelayCall;
            }
        }

        void OnValidateDelayCall()
        {
            if (this != null)
                Render();
        }

        void EditorPreviewDelayCall()
        {
            if (this != null)
                EditorPreview();
        }

        void EditorPreview()
        {
            if (imageObj == null)
                TryGetComponent(out imageObj);

            if (TargetCamera == null || !ValidateResources())
                return;

            // Manual capture in editor (no manager in edit mode)
            int w = Mathf.Max(2, TargetCamera.pixelWidth / Mathf.Max(1, baseDownsample));
            int h = Mathf.Max(2, TargetCamera.pixelHeight / Mathf.Max(1, baseDownsample));

            RenderTexture tempCapture = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default);
            tempCapture.filterMode = FilterMode.Bilinear;
            tempCapture.wrapMode = TextureWrapMode.Clamp;

            // Capture
            RenderTexture prevRT = TargetCamera.targetTexture;
            TargetCamera.targetTexture = tempCapture;
            TargetCamera.Render();
            TargetCamera.targetTexture = prevRT;

            // Blur
            PerformBlur(tempCapture);
            UpdateMaterialProperties();
            UpdateVisibility();

            RenderTexture.ReleaseTemporary(tempCapture);
        }
#endif
    }
}