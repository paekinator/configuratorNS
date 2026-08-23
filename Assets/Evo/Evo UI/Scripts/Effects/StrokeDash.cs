using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI.Tools
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Evo/UI/Effects/Stroke Dash")]
    public class StrokeDash : MonoBehaviour, IMaterialModifier
    {
        [Header("Stroke Settings")]
        [Tooltip("Thickness of the stroke line.")]
        [Min(0.5f)] public float thickness = 4f;

        [Tooltip("Outer Radius of the corners. Set to 0 for sharp corners.")]
        [Min(0f)] public float borderRadius = 20f;

        [Header("Dash Settings")]
        [Tooltip("Length of the visible dash.")]
        [Min(0.5f)] public float dashLength = 20f;

        [Tooltip("Length of the empty gap.")]
        [Min(0f)] public float gapLength = 5f;

        [Tooltip("Animation speed. Positive for clockwise.")]
        public float speed = 30f;

        // Cache
        Graphic graphicComponent;
        RectTransform rectTransform;
        Material cachedMaterialInstance;
        Vector2 lastRectSize;
        StrokeKey? currentKey = null;
        [SerializeField, HideInInspector] private Shader embeddedShader;

        // Shader IDs
        const string ShaderName = "Evo/UI/Stroke Dash";
        static readonly int PropsRectSize = Shader.PropertyToID("_RectSize");
        static readonly int PropsRadius = Shader.PropertyToID("_Radius");
        static readonly int PropsPathRadius = Shader.PropertyToID("_PathRadius");
        static readonly int PropsThickness = Shader.PropertyToID("_Thickness");
        static readonly int PropsDashSettings = Shader.PropertyToID("_DashSettings");

        // Stencil IDs
        static readonly int PropsStencil = Shader.PropertyToID("_Stencil");
        static readonly int PropsStencilComp = Shader.PropertyToID("_StencilComp");
        static readonly int PropsStencilOp = Shader.PropertyToID("_StencilOp");
        static readonly int PropsStencilReadMask = Shader.PropertyToID("_StencilReadMask");
        static readonly int PropsStencilWriteMask = Shader.PropertyToID("_StencilWriteMask");
        static readonly int PropsColorMask = Shader.PropertyToID("_ColorMask");

        void Awake()
        {
            graphicComponent = GetComponent<Graphic>();
            rectTransform = GetComponent<RectTransform>();

            if (embeddedShader == null)
                embeddedShader = Shader.Find(ShaderName);
        }

        void OnEnable()
        {
            lastRectSize = rectTransform.rect.size;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
            graphicComponent.SetMaterialDirty();
        }

        void OnDisable()
        {
            if (currentKey.HasValue)
            {
                MaterialCache.Release(currentKey.Value);
                currentKey = null;
            }

            cachedMaterialInstance = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
#endif
            graphicComponent.SetMaterialDirty();
        }

        void OnDestroy()
        {
            if (currentKey.HasValue)
            {
                MaterialCache.Release(currentKey.Value);
                currentKey = null;
            }
        }

        void Update()
        {
            if (rectTransform == null || graphicComponent == null)
                return;

            Vector2 currentSize = rectTransform.rect.size;
            if (currentSize != lastRectSize)
            {
                lastRectSize = currentSize;
                graphicComponent.SetMaterialDirty();
            }

            if (Application.isPlaying && speed != 0)
                graphicComponent.SetMaterialDirty();
        }

#if UNITY_EDITOR
        void Reset() => embeddedShader = Shader.Find(ShaderName);

        void OnEditorUpdate()
        {
            if (Application.isPlaying || this == null)
                return;

            if (speed != 0)
            {
                graphicComponent.SetMaterialDirty();
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
        }
        void OnValidate()
        {
            if (embeddedShader == null) { embeddedShader = Shader.Find(ShaderName); }
            if (graphicComponent != null) { graphicComponent.SetMaterialDirty(); }
        }
#endif

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled)
                return baseMaterial;

            Rect rect = rectTransform.rect;
            float width = rect.width;
            float height = rect.height;

            // Calculations
            float maxR = Mathf.Min(width, height) * 0.5f;
            float outerRadius = Mathf.Clamp(borderRadius, 0f, maxR);
            float pathRadius = Mathf.Clamp(Mathf.Max(outerRadius, thickness), 0f, maxR);
            float perimeter = 2f * (width + height) + pathRadius * (6.283185f - 8f);
            float finalDash = dashLength;
            float finalGap = gapLength;
            float cycle = dashLength + gapLength;

            if (perimeter > 0.001f && cycle > 0.001f)
            {
                float ratio = perimeter / cycle;
                float targetCount = Mathf.Round(ratio);
                if (targetCount < 1) { targetCount = 1; }
                float perfectCycle = perimeter / targetCount;
                finalGap = perfectCycle - finalDash;
                if (finalGap < 0)
                {
                    float scale = perfectCycle / cycle;
                    finalDash *= scale;
                    finalGap *= scale;
                }
            }

            // Global Time Calculation
            double time = 0.0;
            if (Application.isPlaying) { time = Time.timeAsDouble; }
            else
            {
#if UNITY_EDITOR
                time = UnityEditor.EditorApplication.timeSinceStartup;
#endif
            }

            double finalCycle = finalDash + finalGap;
            float currentPhase = 0;
            if (finalCycle > 0.001)
            {
                double totalMovement = time * speed;
                double wrapped = totalMovement % finalCycle;
                if (wrapped < 0) { wrapped += finalCycle; }
                currentPhase = (float)wrapped;
            }

            // Stencil Support
            float stencilId = 0;
            float stencilComp = 8;
            float stencilOp = 0;
            float stencilRead = 255;
            float stencilWrite = 255;
            float colorMask = 15;

            if (baseMaterial != null && baseMaterial.HasProperty(PropsStencil))
            {
                stencilId = baseMaterial.GetFloat(PropsStencil);
                stencilComp = baseMaterial.GetFloat(PropsStencilComp);
                stencilOp = baseMaterial.GetFloat(PropsStencilOp);
                stencilRead = baseMaterial.GetFloat(PropsStencilReadMask);
                stencilWrite = baseMaterial.GetFloat(PropsStencilWriteMask);
                colorMask = baseMaterial.GetFloat(PropsColorMask);
            }

            StrokeKey newKey = new(
                width, height, outerRadius, thickness, finalDash, finalGap, speed,
                stencilId, stencilComp, stencilOp, stencilRead, stencilWrite, colorMask
            );

            if (!currentKey.HasValue || !newKey.Equals(currentKey.Value))
            {
                if (currentKey.HasValue) { MaterialCache.Release(currentKey.Value); }

                // Use the serialized shader reference here
                Shader shaderToUse = embeddedShader;
                if (shaderToUse == null) { shaderToUse = Shader.Find(ShaderName); }

                cachedMaterialInstance = MaterialCache.Get(newKey, shaderToUse);
                currentKey = newKey;
            }

            if (cachedMaterialInstance == null) { return baseMaterial; }

            // Update Shader Uniforms
            cachedMaterialInstance.SetVector(PropsRectSize, new Vector4(width, height, 0, 0));
            cachedMaterialInstance.SetFloat(PropsRadius, outerRadius);
            cachedMaterialInstance.SetFloat(PropsPathRadius, pathRadius);
            cachedMaterialInstance.SetFloat(PropsThickness, thickness);
            cachedMaterialInstance.SetVector(PropsDashSettings, new Vector3(finalDash, finalGap, currentPhase));

            // Pass Stencil Uniforms
            cachedMaterialInstance.SetFloat(PropsStencil, stencilId);
            cachedMaterialInstance.SetFloat(PropsStencilComp, stencilComp);
            cachedMaterialInstance.SetFloat(PropsStencilOp, stencilOp);
            cachedMaterialInstance.SetFloat(PropsStencilReadMask, stencilRead);
            cachedMaterialInstance.SetFloat(PropsStencilWriteMask, stencilWrite);
            cachedMaterialInstance.SetFloat(PropsColorMask, colorMask);

            return cachedMaterialInstance;
        }

        // Caching System
        readonly struct StrokeKey : IEquatable<StrokeKey>
        {
            public readonly int width;
            public readonly int height;
            public readonly float radius;
            public readonly float thickness;
            public readonly float dash;
            public readonly float gap;
            public readonly float speed;

            // Stencil keys
            public readonly float stencilId;
            public readonly float stencilComp;
            public readonly float stencilOp;
            public readonly float stencilRead;
            public readonly float stencilWrite;
            public readonly float colorMask;

            public StrokeKey(
                float w, float h, float r, float t, float d, float g, float s,
                float sId, float sComp, float sOp, float sRead, float sWrite, float cMask)
            {
                width = Mathf.RoundToInt(w);
                height = Mathf.RoundToInt(h);
                radius = r;
                thickness = t;
                dash = d;
                gap = g;
                speed = s;

                stencilId = sId;
                stencilComp = sComp;
                stencilOp = sOp;
                stencilRead = sRead;
                stencilWrite = sWrite;
                colorMask = cMask;
            }

            public bool Equals(StrokeKey other)
            {
                return width == other.width && height == other.height &&
                       Mathf.Approximately(radius, other.radius) &&
                       Mathf.Approximately(thickness, other.thickness) &&
                       Mathf.Approximately(dash, other.dash) &&
                       Mathf.Approximately(gap, other.gap) &&
                       Mathf.Approximately(speed, other.speed) &&
                       Mathf.Approximately(stencilId, other.stencilId) &&
                       Mathf.Approximately(stencilComp, other.stencilComp) &&
                       Mathf.Approximately(stencilOp, other.stencilOp) &&
                       Mathf.Approximately(stencilRead, other.stencilRead) &&
                       Mathf.Approximately(stencilWrite, other.stencilWrite) &&
                       Mathf.Approximately(colorMask, other.colorMask);
            }

            public override int GetHashCode()
            {
                var hash1 = HashCode.Combine(width, height, radius, thickness, dash, gap, speed);
                var hash2 = HashCode.Combine(stencilId, stencilComp, stencilOp, stencilRead, stencilWrite, colorMask);
                return HashCode.Combine(hash1, hash2);
            }
        }

        static class MaterialCache
        {
            static readonly Dictionary<StrokeKey, Material> cache = new();
            static readonly Dictionary<StrokeKey, int> refCounts = new();

            public static Material Get(StrokeKey key, Shader shader)
            {
                if (cache.TryGetValue(key, out Material mat))
                {
                    if (mat != null)
                    {
                        refCounts[key]++;
                        return mat;
                    }
                    cache.Remove(key);
                    refCounts.Remove(key);
                }

                if (shader == null)
                    return null;

                Material newMat = new(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    enableInstancing = true
                };

                cache[key] = newMat;
                refCounts[key] = 1;
                return newMat;
            }

            public static void Release(StrokeKey key)
            {
                if (refCounts.ContainsKey(key))
                {
                    refCounts[key]--;
                    if (refCounts[key] <= 0)
                    {
                        if (cache.TryGetValue(key, out Material mat))
                        {
                            if (mat != null)
                            {
                                if (Application.isPlaying) { Destroy(mat); }
                                else { DestroyImmediate(mat); }
                            }
                            cache.Remove(key);
                        }
                        refCounts.Remove(key);
                    }
                }
            }
        }
    }
}