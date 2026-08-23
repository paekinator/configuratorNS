using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Applies a soft mask to child Graphic elements.
    /// </summary>
    [ExecuteAlways]
    [HelpURL(Constants.HelpUrl)]
    [AddComponentMenu("Evo/UI/Effects/Soft Mask (Preview)")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic), typeof(RectTransform))]
    public class SoftMask : UIBehaviour, IMeshModifier
    {
        [Tooltip("Should the graphic serving as the mask be drawn?")]
        [SerializeField] private bool showMaskGraphic = false;

        // Cache
        Graphic maskGraphic;
        RectTransform rectTransform;
        readonly Dictionary<Material, Material> materialCache = new();
        static readonly List<Graphic> graphics = new();
        static readonly List<SoftMaskable> maskables = new();

        // Shader Serialization
        [SerializeField, HideInInspector] Shader embeddedShader;
        [SerializeField, HideInInspector] Shader embeddedTMPShader;
        const string ShaderName = "Hidden/Evo/UI/Soft Mask";
        const string TMPShaderName = "Hidden/Evo/UI/Soft Mask TMP";

        // Shader Property IDs
        static readonly int PropsTex = Shader.PropertyToID("_SoftMaskTex");
        static readonly int PropsCanvasToLocal = Shader.PropertyToID("_SoftMask_CanvasToLocal");
        static readonly int PropsRect = Shader.PropertyToID("_SoftMask_Rect");

        // Procedural Rect Property IDs
        static readonly int PropsPRCenter = Shader.PropertyToID("_SoftMask_PR_Center");
        static readonly int PropsPRHalfSize = Shader.PropertyToID("_SoftMask_PR_HalfSize");
        static readonly int PropsPRRadii = Shader.PropertyToID("_SoftMask_PR_Radii");
        static readonly int PropsPRSoftness = Shader.PropertyToID("_SoftMask_PR_Softness");
        static readonly int PropsPRFillData = Shader.PropertyToID("_SoftMask_PR_FillData");

        // Sliced Atlas Property IDs
        static readonly int PropsBorderData = Shader.PropertyToID("_SoftMask_BorderData");
        static readonly int PropsUVOuter = Shader.PropertyToID("_SoftMask_UVOuter");
        static readonly int PropsUVInner = Shader.PropertyToID("_SoftMask_UVInner");

        // General Properties
        static readonly int PropsMainTex = Shader.PropertyToID("_MainTex");

#if UNITY_EDITOR
        // Cache the delegate to prevent Editor memory leaks from repeated OnValidate calls
        UnityEditor.EditorApplication.CallbackFunction onValidateDelayCall;
#endif

        public bool ShowMaskGraphic
        {
            get => showMaskGraphic;
            set
            {
                if (showMaskGraphic == value)
                    return;

                showMaskGraphic = value;
                if (maskGraphic != null)
                {
                    maskGraphic.SetVerticesDirty();
                    maskGraphic.canvasRenderer.SetAlpha(showMaskGraphic ? 1f : 0f);
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            rectTransform = GetComponent<RectTransform>();
            maskGraphic = GetComponent<Graphic>();

            if (embeddedShader == null) { embeddedShader = Shader.Find(ShaderName); }
            if (embeddedTMPShader == null) { embeddedTMPShader = Shader.Find(TMPShaderName); }
            if (maskGraphic != null) { maskGraphic.canvasRenderer.SetAlpha(showMaskGraphic ? 1f : 0f); }
            if (Application.isPlaying) { SpawnMaskables(); }

            Canvas.willRenderCanvases += UpdateMaskProperties;
            NotifyChildren();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Canvas.willRenderCanvases -= UpdateMaskProperties;

            if (Application.isPlaying)
            {
                GetComponentsInChildren<SoftMaskable>(true, maskables);
                for (int i = 0; i < maskables.Count; i++)
                {
                    var m = maskables[i];
                    
                    if (m != null)
                        m.enabled = false;
                }
            }

            // Force children to revert to their original materials natively
            GetComponentsInChildren(true, graphics);
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];

                if (g == null || g.gameObject == this.gameObject)
                    continue;

                g.SetMaterialDirty();
            }

            ClearCache();

            // Update mask graphic
            if (maskGraphic != null)
            {
                maskGraphic.SetVerticesDirty();
                maskGraphic.SetMaterialDirty();
                maskGraphic.canvasRenderer.SetAlpha(1f);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (Application.isPlaying)
            {
                GetComponentsInChildren<SoftMaskable>(true, maskables);
                for (int i = 0; i < maskables.Count; i++)
                {
                    var m = maskables[i];

                    if (m == null || m.gameObject == this.gameObject || m.AssignedMask != this)
                        continue;

                    if (m.TryGetComponent<Graphic>(out var graphic))
                        graphic.SetMaterialDirty();

                    Destroy(m);
                }
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            UpdateMaskProperties();
        }

        void NotifyChildren()
        {
            GetComponentsInChildren<Graphic>(true, graphics);
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];

                if (g == null || g.gameObject == this.gameObject)
                    continue;

                g.SetMaterialDirty();
            }
        }

        void SpawnMaskables()
        {
            if (this == null || !Application.isPlaying)
                return;

            GetComponentsInChildren<Graphic>(false, graphics);
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];

                if (g.gameObject == this.gameObject)
                    continue;

                if (!g.TryGetComponent<SoftMaskable>(out var maskable))
                {
                    maskable = g.gameObject.AddComponent<SoftMaskable>();
                    maskable.hideFlags = HideFlags.HideInInspector;
                    g.SetMaterialDirty();
                }
                else if (!maskable.enabled)
                {
                    // If the maskable was disabled by toggling the parent Soft Mask off,
                    // we must re-enable it here so it resumes intercepting the TMP materials.
                    maskable.enabled = true;
                    g.SetMaterialDirty();
                }
            }
        }

        void SetMaterialProperties(Material mat)
        {
            if (maskGraphic == null || rectTransform == null || mat == null)
                return;

            Rect r = rectTransform.rect;

            ProceduralRect prMask = maskGraphic as ProceduralRect;
            if (prMask != null)
            {
                mat.DisableKeyword("SOFTMASK_SLICED");
                mat.EnableKeyword("SOFTMASK_PROCEDURAL");

                mat.SetVector(PropsPRCenter, r.center);
                mat.SetVector(PropsPRHalfSize, new Vector2(r.width * 0.5f, r.height * 0.5f));

                float maxR = Mathf.Min(r.width, r.height) * 0.5f;
                float P(float val) => prMask.radiusMode == ProceduralRect.RadiusMode.Percentage ? val * 0.01f * maxR : val;
                mat.SetVector(PropsPRRadii, new Vector4(P(prMask.cornerRadius.y), P(prMask.cornerRadius.z), P(prMask.cornerRadius.w), P(prMask.cornerRadius.x)));

                mat.SetFloat(PropsPRSoftness, prMask.softness);

                float fillPacked = (int)prMask.clipMethod + Mathf.Clamp(prMask.clipOrigin, 0, 3) * 8 + (prMask.clipClockwise ? 1 : 0) * 64;
                mat.SetVector(PropsPRFillData, new Vector4(prMask.clipAmount, fillPacked, 0, 0));
            }
            else
            {
                mat.DisableKeyword("SOFTMASK_PROCEDURAL");
                Texture maskTexture = maskGraphic.mainTexture != null ? maskGraphic.mainTexture : Texture2D.whiteTexture;
                mat.SetTexture(PropsTex, maskTexture);

                Image img = maskGraphic as Image;
                if (img != null && img.sprite != null)
                {
                    Vector4 outerUV = UnityEngine.Sprites.DataUtility.GetOuterUV(img.sprite);
                    mat.SetVector(PropsUVOuter, outerUV);

                    if (img.type == Image.Type.Simple && img.preserveAspect)
                    {
                        mat.DisableKeyword("SOFTMASK_SLICED");
                        Vector2 size = new(img.sprite.rect.width, img.sprite.rect.height);
                        if (size.sqrMagnitude > 0)
                        {
                            float spriteRatio = size.x / size.y;
                            float rectRatio = r.width / r.height;
                            if (spriteRatio > rectRatio)
                            {
                                float oldHeight = r.height;
                                r.height = r.width * (1.0f / spriteRatio);
                                r.y += (oldHeight - r.height) * img.rectTransform.pivot.y;
                            }
                            else
                            {
                                float oldWidth = r.width;
                                r.width = r.height * spriteRatio;
                                r.x += (oldWidth - r.width) * img.rectTransform.pivot.x;
                            }
                        }
                    }
                    else if (img.type == Image.Type.Sliced && img.hasBorder)
                    {
                        mat.EnableKeyword("SOFTMASK_SLICED");

                        float ppu = img.pixelsPerUnit * img.pixelsPerUnitMultiplier;
                        if (ppu <= 0f) { ppu = 1f; }

                        Vector4 border = img.sprite.border / ppu;

                        for (int axis = 0; axis <= 1; axis++)
                        {
                            float combinedBorders = border[axis] + border[axis + 2];
                            if (r.size[axis] < combinedBorders && combinedBorders != 0)
                            {
                                float borderScaleRatio = r.size[axis] / combinedBorders;
                                border[axis] *= borderScaleRatio;
                                border[axis + 2] *= borderScaleRatio;
                            }
                        }

                        mat.SetVector(PropsBorderData, new Vector4(border.x, border.y, r.width - border.z, r.height - border.w));
                        Vector4 innerUV = UnityEngine.Sprites.DataUtility.GetInnerUV(img.sprite);
                        mat.SetVector(PropsUVInner, innerUV);
                    }
                    else
                    {
                        mat.DisableKeyword("SOFTMASK_SLICED");
                    }
                }
                else
                {
                    mat.SetVector(PropsUVOuter, new Vector4(0, 0, 1, 1));
                    mat.DisableKeyword("SOFTMASK_SLICED");
                }
            }

            Canvas canvas = maskGraphic.canvas;
            if (canvas == null) { mat.SetMatrix(PropsCanvasToLocal, rectTransform.worldToLocalMatrix); }
            else
            {
                Matrix4x4 canvasToWorld = canvas.transform.localToWorldMatrix;
                Matrix4x4 worldToMaskLocal = rectTransform.worldToLocalMatrix;
                mat.SetMatrix(PropsCanvasToLocal, worldToMaskLocal * canvasToWorld);
            }

            mat.SetVector(PropsRect, new Vector4(r.xMin, r.yMin, Mathf.Max(r.width, 0.001f), Mathf.Max(r.height, 0.001f)));
        }

        void UpdateMaskProperties()
        {
            if (!isActiveAndEnabled || maskGraphic == null)
                return;

#if UNITY_EDITOR
            if (Application.isPlaying) { SpawnMaskables(); }
            else
            {
                // Edit Mode: Brute-force the preview to avoid dirtying the Scene/Prefab
                GetComponentsInChildren<Graphic>(false, graphics);
                foreach (var g in graphics)
                {
                    if (g == null || g.gameObject == this.gameObject)
                        continue;

                    Material baseMat = g.materialForRendering;
                    Texture texToUse = g.mainTexture;

                    // Honor the standard Maskable flag
                    if (g is MaskableGraphic mg && !mg.maskable)
                    {
                        g.canvasRenderer.SetMaterial(baseMat, texToUse);
                        continue;
                    }

                    Material modified = GetModifiedMaterialForChild(baseMat);
                    bool isDefaultTex = texToUse == null || texToUse.name == "White Texture" || texToUse.name == "UnityWhite";

                    // TMP Editor Bug Fix
                    if (isDefaultTex && baseMat != null && baseMat.HasProperty(PropsMainTex))
                    {
                        var matTex = baseMat.GetTexture(PropsMainTex);
                        if (matTex != null)
                        {
                            texToUse = matTex;
                            isDefaultTex = false;
                        }
                    }

                    if (isDefaultTex && baseMat != null && baseMat.shader.name.Contains("TextMeshPro"))
                        continue;

                    if (modified != null && texToUse != null) { modified.SetTexture(PropsMainTex, texToUse); }
                    g.canvasRenderer.SetMaterial(modified, texToUse);
                }
            }
#else
            SpawnMaskables();
#endif

            if (materialCache.Count == 0)
                return;

            foreach (var mat in materialCache.Values)
                SetMaterialProperties(mat);
        }

        public void ModifyMesh(VertexHelper vh)
        {
            if (maskGraphic != null)
                maskGraphic.canvasRenderer.SetAlpha(showMaskGraphic ? 1f : 0f);
          
            if (!showMaskGraphic)
                vh.Clear();
        }

        void ClearCache()
        {
            foreach (var mat in materialCache.Values)
            {
                if (mat != null)
                {
                    if (Application.isPlaying) { Destroy(mat); }
                    else { DestroyImmediate(mat); }
                }
            }
            materialCache.Clear();
        }

        void SyncMaterial(Material baseMaterial, Material modifiedMaterial)
        {
            // Always sync properties to support dynamic material updates
            modifiedMaterial.CopyPropertiesFromMaterial(baseMaterial);

            // When masks are dynamically enabled/disabled at runtime, TMP can desync and CanvasRenderer 
            // briefly pushes a default white texture. Forcing the Font Atlas directly onto the material 
            // instance ensures the SDF math never draws a solid square.
            if (baseMaterial.HasProperty(PropsMainTex))
            {
                var tex = baseMaterial.GetTexture(PropsMainTex);
                if (tex != null) { modifiedMaterial.SetTexture(PropsMainTex, tex); }
            }

            SetMaterialProperties(modifiedMaterial);
        }

        public Material GetModifiedMaterialForChild(Material baseMaterial)
        {
            if (!isActiveAndEnabled || maskGraphic == null || baseMaterial == null)
                return baseMaterial;

            // Check the cache first.
            // Accessing baseMaterial.shader.name allocates a new string in C# memory. 
            // Doing it after the cache check prevents string allocations during continuous UI rebuilds.
            if (materialCache.TryGetValue(baseMaterial, out Material modifiedMaterial) && modifiedMaterial != null)
            {
                SyncMaterial(baseMaterial, modifiedMaterial);
                return modifiedMaterial;
            }

            // Cache miss - we will only allocate the string name once per new material
            string shaderName = baseMaterial.shader.name;
            Shader shaderToUse = null;

            if (shaderName == "UI/Default" || shaderName == ShaderName)
            {
                // Intercept standard UI Shaders
                shaderToUse = embeddedShader != null ? embeddedShader : Shader.Find(ShaderName);
            }
            else if (shaderName.Contains("TextMeshPro") || shaderName == TMPShaderName)
            {
                // Intercept TextMeshPro Shaders
                shaderToUse = embeddedTMPShader != null ? embeddedTMPShader : Shader.Find(TMPShaderName);
            }
            else if (baseMaterial.HasProperty(PropsRect))
            {
                // Intercept custom shaders that natively support Soft Masking
                shaderToUse = baseMaterial.shader;
            }

            if (shaderToUse == null)
                return baseMaterial;

            modifiedMaterial = new Material(shaderToUse)
            {
                name = "SoftMask_Mat",
                hideFlags = HideFlags.HideAndDontSave
            };
            materialCache[baseMaterial] = modifiedMaterial;

            SyncMaterial(baseMaterial, modifiedMaterial);
            return modifiedMaterial;
        }

        public void ModifyMesh(Mesh mesh) { }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();

            embeddedShader = Shader.Find(ShaderName);
            embeddedTMPShader = Shader.Find(TMPShaderName);

            if (maskGraphic == null)
                maskGraphic = GetComponent<Graphic>();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (embeddedShader == null) { embeddedShader = Shader.Find(ShaderName); }
            if (embeddedTMPShader == null) { embeddedTMPShader = Shader.Find(TMPShaderName); }
            if (maskGraphic == null) { maskGraphic = GetComponent<Graphic>(); }
            if (maskGraphic != null) { maskGraphic.canvasRenderer.SetAlpha(showMaskGraphic ? 1f : 0f); }

            // Ensure we don't cause Editor delegate leaks by subscribing multiple anonymous lambdas
            onValidateDelayCall ??= () =>
                {
                    if (this == null)
                        return;

                    if (Application.isPlaying)
                        SpawnMaskables();

                    UpdateMaskProperties();

                    if (maskGraphic != null)
                        maskGraphic.SetVerticesDirty();
                };

            UnityEditor.EditorApplication.delayCall -= onValidateDelayCall;
            UnityEditor.EditorApplication.delayCall += onValidateDelayCall;
        }
#endif
    }
}