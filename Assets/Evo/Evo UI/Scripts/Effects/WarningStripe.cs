using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Evo/UI/Effects/Warning Lines")]
    public class WarningStripe : MonoBehaviour, IMaterialModifier
    {
        [Header("Settings")]
        [SerializeField, Range(0.01f, 0.5f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.01f, 1f)] private float lineSpacing = 0.2f;
        [SerializeField, Range(-180f, 180f)] private float lineAngle = 25;
        [SerializeField, Range(-5f, 5f)] private float scrollSpeed = 0.25f;

        // Property IDs
        [SerializeField, HideInInspector] private Shader embeddedShader;
        const string SHADER_NAME = "Evo/UI/Warning Stripe";

        static readonly int LINE_WIDTH_ID = Shader.PropertyToID("_LineWidth");
        static readonly int LINE_SPACING_ID = Shader.PropertyToID("_LineSpacing");
        static readonly int LINE_ANGLE_ID = Shader.PropertyToID("_LineAngle");
        static readonly int SCROLL_SPEED_ID = Shader.PropertyToID("_ScrollSpeed");

        // Cached Stencil IDs
        static readonly int STENCIL_ID = Shader.PropertyToID("_Stencil");
        static readonly int STENCIL_COMP_ID = Shader.PropertyToID("_StencilComp");
        static readonly int STENCIL_OP_ID = Shader.PropertyToID("_StencilOp");
        static readonly int STENCIL_READ_MASK_ID = Shader.PropertyToID("_StencilReadMask");
        static readonly int STENCIL_WRITE_MASK_ID = Shader.PropertyToID("_StencilWriteMask");
        static readonly int COLOR_MASK_ID = Shader.PropertyToID("_ColorMask");

        // Cache
        Material uiMaterial;
        Shader cachedShader;
        Graphic targetGraphic;

        Graphic TargetGraphic
        {
            get
            {
                if (targetGraphic == null)
                    TryGetComponent(out targetGraphic);

                return targetGraphic;
            }
        }

        void OnEnable()
        {
            if (TargetGraphic != null)
                TargetGraphic.SetMaterialDirty();
        }

        void OnDisable()
        {
            if (TargetGraphic != null)
                TargetGraphic.SetMaterialDirty();
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
#endif
        }

        void OnDestroy()
        {
            if (uiMaterial != null)
            {
                if (Application.isPlaying) { Destroy(uiMaterial); }
                else { DestroyImmediate(uiMaterial); }
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // Cache the shader reference to prevent repeated string lookups
            if (cachedShader == null)
            {
                cachedShader = embeddedShader != null ? embeddedShader : Shader.Find(SHADER_NAME);
                if (cachedShader == null)
                {
                    Debug.LogError($"Shader '{SHADER_NAME}' not found.", this);
                    return baseMaterial;
                }
            }

            // Create the material instance if needed
            if (uiMaterial == null || uiMaterial.shader != cachedShader)
            {
                uiMaterial = new Material(cachedShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            // Update our custom shader properties using highly optimized int IDs
            uiMaterial.SetFloat(LINE_WIDTH_ID, lineWidth);
            uiMaterial.SetFloat(LINE_SPACING_ID, lineSpacing);
            uiMaterial.SetFloat(LINE_ANGLE_ID, lineAngle);
            uiMaterial.SetFloat(SCROLL_SPEED_ID, scrollSpeed);

            // Copy Masking & Stencil properties from the base UI material
            // This makes standard Mask and RectMask2D components work smoothly
            if (baseMaterial != null)
            {
                if (baseMaterial.HasProperty(STENCIL_ID))
                {
                    uiMaterial.SetFloat(STENCIL_ID, baseMaterial.GetFloat(STENCIL_ID));
                    uiMaterial.SetFloat(STENCIL_COMP_ID, baseMaterial.GetFloat(STENCIL_COMP_ID));
                    uiMaterial.SetFloat(STENCIL_OP_ID, baseMaterial.GetFloat(STENCIL_OP_ID));
                    uiMaterial.SetFloat(STENCIL_READ_MASK_ID, baseMaterial.GetFloat(STENCIL_READ_MASK_ID));
                    uiMaterial.SetFloat(STENCIL_WRITE_MASK_ID, baseMaterial.GetFloat(STENCIL_WRITE_MASK_ID));
                    uiMaterial.SetFloat(COLOR_MASK_ID, baseMaterial.GetFloat(COLOR_MASK_ID));
                }

                // Enable keywords for RectMask2D clipping
                if (baseMaterial.IsKeywordEnabled("UNITY_UI_CLIP_RECT")) { uiMaterial.EnableKeyword("UNITY_UI_CLIP_RECT"); }
                else { uiMaterial.DisableKeyword("UNITY_UI_CLIP_RECT"); }

                if (baseMaterial.IsKeywordEnabled("UNITY_UI_ALPHACLIP")) { uiMaterial.EnableKeyword("UNITY_UI_ALPHACLIP"); }
                else { uiMaterial.DisableKeyword("UNITY_UI_ALPHACLIP"); }
            }

            return uiMaterial;
        }

#if UNITY_EDITOR
        void Reset() => embeddedShader = Shader.Find(SHADER_NAME);

        void OnValidate()
        {
            if (embeddedShader == null)
                embeddedShader = Shader.Find(SHADER_NAME);

            cachedShader = embeddedShader;

            if (TargetGraphic != null)
                TargetGraphic.SetMaterialDirty();
        }
#endif
    }
}