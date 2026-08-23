using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Modifies UI graphics by overlaying a highly configurable gradient effect.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Evo/UI/Effects/Image Gradient")]
    [HelpURL(Constants.HelpUrl + "effects/image-gradient")]
    public class ImageGradient : BaseMeshEffect
    {
        [Header("Gradient")]
        public Gradient gradientEffect = new()
        {
            colorKeys = new GradientColorKey[]
            {
                new(Color.black, 0),
                new(Color.white, 1)
            }
        };

        [Header("Settings")]
        public Type gradientType = Type.Horizontal;
        public BlendMode blendMode = BlendMode.Multiply;
        [Range(-180f, 180f)] public float angle = 0f;
        [Range(-1f, 1f)] public float effectOffset = 0f;
        [Range(0.1f, 10f)] public float zoom = 1f;
        public bool reverse = false;

        // Cache
        Graphic cachedGraphic;

        // Cached gradient keys to prevent massive GC spikes from accessing Gradient.colorKeys/alphaKeys during mesh generation
        GradientColorKey[] cachedColorKeys = Array.Empty<GradientColorKey>();
        GradientAlphaKey[] cachedAlphaKeys = Array.Empty<GradientAlphaKey>();

        // Memory Pools
        readonly List<float> stops = new();
        readonly List<float> rawKeys = new();
        readonly List<UIVertex> vertexList = new();
        readonly List<UIVertex> currentTriangles = new();
        readonly List<UIVertex> nextTriangles = new();
        readonly List<UIVertex> finalTriangles = new();

        // Enums
        public enum Type { Horizontal, Vertical }
        public enum BlendMode { Multiply = 0, Add = 1, Subtract = 2, Override = 3 }

        protected override void Awake()
        {
            base.Awake();

            cachedGraphic = GetComponent<Graphic>();
            CacheGradientKeys();
        }

        void CacheGradientKeys()
        {
            if (gradientEffect != null)
            {
                cachedColorKeys = gradientEffect.colorKeys;
                cachedAlphaKeys = gradientEffect.alphaKeys;
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0)
                return;

            vertexList.Clear();
            helper.GetUIVertexStream(vertexList);

            // Calculate effective angle
            float effectiveAngle = angle;
            if (gradientType == Type.Vertical) { effectiveAngle += 90f; }

            // Rotate vertices to align with Horizontal axis for simpler math
            Rect bounds = GetBounds(vertexList);
            Vector2 center = bounds.center;
            RotateVertices(vertexList, center, -effectiveAngle);

            // Recalculate bounds in the rotated space
            Rect rotatedBounds = GetBounds(vertexList);
            float min = rotatedBounds.xMin;
            float w = rotatedBounds.width;
            float width = w == 0f ? 0f : 1f / w / zoom;
            float zoomOffset = (1 - (1 / zoom)) * 0.5f;
            float offset = (effectOffset * (1 - zoomOffset)) - zoomOffset;

            // Perform the mesh splitting
            SplitTrianglesAtGradientStops(vertexList, rotatedBounds, zoomOffset, helper, min, width, offset);

            // PS: SplitTrianglesAtGradientStops puts the result into helper directly.
            // Need to fetch them back to rotate them back.
            vertexList.Clear();
            helper.GetUIVertexStream(vertexList);

            // Rotate back
            RotateVertices(vertexList, center, effectiveAngle);

            // Push final vertices back
            helper.Clear();
            helper.AddUIVertexTriangleStream(vertexList);
        }

        void RotateVertices(List<UIVertex> vertices, Vector2 center, float angleVal)
        {
            if (Mathf.Abs(angleVal) < 0.001f)
                return;

            Quaternion q = Quaternion.Euler(0, 0, angleVal);
            Vector3 c = center;

            int count = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                UIVertex v = vertices[i];
                Vector3 pos = v.position;
                pos -= c;
                pos = q * pos;
                pos += c;
                v.position = pos;
                vertices[i] = v;
            }
        }

        Rect GetBounds(List<UIVertex> vertices)
        {
            if (vertices.Count == 0)
                return new Rect();

            float left = vertices[0].position.x;
            float right = left;
            float bottom = vertices[0].position.y;
            float top = bottom;

            int count = vertices.Count;
            for (int i = 1; i < count; i++)
            {
                float x = vertices[i].position.x;
                float y = vertices[i].position.y;

                if (x > right) { right = x; }
                else if (x < left) { left = x; }

                if (y > top) { top = y; }
                else if (y < bottom) { bottom = y; }
            }

            return new Rect(left, bottom, right - left, top - bottom);
        }

        void SplitTrianglesAtGradientStops(List<UIVertex> inputVerts, Rect bounds, float zoomoffset, VertexHelper helper, 
            float min, float width, float offset)
        {
            FindStops(zoomoffset, bounds); // populates stops

            // If no stops needed, just color the original mesh
            if (stops.Count == 0)
            {
                helper.Clear();
                int count = inputVerts.Count;
                for (int i = 0; i < count; i++)
                {
                    UIVertex v = inputVerts[i];
                    ApplyGradientColor(ref v, min, width, offset, 0f);
                    inputVerts[i] = v;
                }
                helper.AddUIVertexTriangleStream(inputVerts);
                return;
            }

            // Reuse pooled lists
            currentTriangles.Clear();
            currentTriangles.AddRange(inputVerts);
            nextTriangles.Clear();
            finalTriangles.Clear();

            // Toggle logic for swapping lists
            List<UIVertex> activeList = currentTriangles;
            List<UIVertex> nextList = nextTriangles;

            int stopCount = stops.Count;
            for (int s = 0; s < stopCount; s++)
            {
                float stopValue = stops[s];
                nextList.Clear();

                int activeCount = activeList.Count;
                for (int i = 0; i < activeCount; i += 3)
                {
                    UIVertex v1 = activeList[i];
                    UIVertex v2 = activeList[i + 1];
                    UIVertex v3 = activeList[i + 2];

                    bool v1Left = v1.position.x <= stopValue;
                    bool v2Left = v2.position.x <= stopValue;
                    bool v3Left = v3.position.x <= stopValue;

                    if (v1Left && v2Left && v3Left)
                        AddTriangleAndColor(finalTriangles, v1, v2, v3, min, width, offset, -0.0001f);
                    else if (!v1Left && !v2Left && !v3Left)
                    {
                        nextList.Add(v1);
                        nextList.Add(v2);
                        nextList.Add(v3);
                    }
                    else
                        PerformTriangleSplit(v1, v2, v3, v1Left, v2Left, v3Left, stopValue, finalTriangles, nextList, min, width, offset);
                }

                // Swap lists (Allocation-free Tuple swap)
                (nextList, activeList) = (activeList, nextList);
            }

            // Remaining triangles are on the far right
            int remainingCount = activeList.Count;
            for (int i = 0; i < remainingCount; i += 3)
                AddTriangleAndColor(finalTriangles, activeList[i], activeList[i + 1], activeList[i + 2], min, width, offset, 0.0001f);

            helper.Clear();
            helper.AddUIVertexTriangleStream(finalTriangles);
        }

        void PerformTriangleSplit(UIVertex v1, UIVertex v2, UIVertex v3, bool v1Left, bool v2Left, bool v3Left, float stopValue, 
            List<UIVertex> leftList, List<UIVertex> rightList, float min, float width, float offset)
        {
            UIVertex lone, other1, other2;
            bool loneIsLeft;

            if (v1Left == v2Left) { lone = v3; other1 = v1; other2 = v2; loneIsLeft = v3Left; }
            else if (v1Left == v3Left) { lone = v2; other1 = v1; other2 = v3; loneIsLeft = v2Left; }
            else { lone = v1; other1 = v2; other2 = v3; loneIsLeft = v1Left; }

            UIVertex split1 = CreateSplitVertex(lone, other1, stopValue);
            UIVertex split2 = CreateSplitVertex(lone, other2, stopValue);

            if (loneIsLeft)
            {
                AddTriangleAndColor(leftList, lone, split1, split2, min, width, offset, -0.0001f);
                rightList.Add(split1); rightList.Add(other1); rightList.Add(other2);
                rightList.Add(split1); rightList.Add(other2); rightList.Add(split2);
            }
            else
            {
                AddTriangleAndColor(leftList, split1, other1, other2, min, width, offset, -0.0001f);
                AddTriangleAndColor(leftList, split1, other2, split2, min, width, offset, -0.0001f);
                rightList.Add(lone); rightList.Add(split1); rightList.Add(split2);
            }
        }

        void AddTriangleAndColor(List<UIVertex> list, UIVertex v1, UIVertex v2, UIVertex v3, float min, float width, float offset, float eps)
        {
            ApplyGradientColor(ref v1, min, width, offset, eps);
            ApplyGradientColor(ref v2, min, width, offset, eps);
            ApplyGradientColor(ref v3, min, width, offset, eps);

            list.Add(v1);
            list.Add(v2);
            list.Add(v3);
        }

        void ApplyGradientColor(ref UIVertex v, float min, float width, float offset, float eps)
        {
            float t = (v.position.x - min) * width - offset;
        
            if (reverse)
                t = 1f - t;

            float sampleT = Mathf.Clamp01(t + eps);
            Color gradientColor = gradientEffect.Evaluate(sampleT);
            v.color = BlendColor(v.color, gradientColor);
        }

        void FindStops(float zoomoffset, Rect bounds)
        {
            stops.Clear();
            var offset = effectOffset * (1 - zoomoffset);
            var startBoundary = zoomoffset - offset;
            var endBoundary = (1 - zoomoffset) - offset;

            // Optim: Use pre-cached keys
            var colorKeys = cachedColorKeys;
            var alphaKeys = cachedAlphaKeys;

            foreach (var color in colorKeys)
            {
                if (color.time >= endBoundary)
                    break;

                if (color.time > startBoundary)
                    stops.Add((color.time - startBoundary) * zoom);
            }

            foreach (var alpha in alphaKeys)
            {
                if (alpha.time >= endBoundary)
                    break;

                if (alpha.time > startBoundary)
                    stops.Add((alpha.time - startBoundary) * zoom);
            }

            // Perceptual / Nonlinear check
            // Only subdivide if the gradient is not linear. 
            // This prevents massive vertex creation for standard gradients.
            rawKeys.Clear();

            foreach (var k in colorKeys)
                rawKeys.Add(k.time);
           
            foreach (var k in alphaKeys)
                rawKeys.Add(k.time);
          
            rawKeys.Sort();

            if (rawKeys.Count == 0 || rawKeys[0] > 0)
                rawKeys.Insert(0, 0);

            if (rawKeys[^1] < 1)
                rawKeys.Add(1);

            int rawKeyCount = rawKeys.Count;
            for (int i = 0; i < rawKeyCount - 1; i++)
            {
                float t1 = rawKeys[i];
                float t2 = rawKeys[i + 1];
                float dist = t2 - t1;

                if (dist > 0.05f) // Check segments larger than 5%
                {
                    // Does the gradient deviate from a linear lerp?
                    Color c1 = gradientEffect.Evaluate(t1);
                    Color c2 = gradientEffect.Evaluate(t2);
                    Color midColor = gradientEffect.Evaluate(t1 + dist * 0.5f);
                    Color linearMid = Color.Lerp(c1, c2, 0.5f);

                    // If deviation is significant, it's a perceptual or complex gradient. Subdivide.
                    // If it matches, the GPU handles it perfectly, no extra verts needed.
                    if (IsNonLinear(midColor, linearMid))
                    {
                        int steps = Mathf.FloorToInt(dist / 0.1f); // 1 subdivision per 10%
                        for (int j = 1; j <= steps; j++)
                        {
                            float subT = t1 + (dist * ((float)j / (steps + 1)));
                            if (subT >= endBoundary) { break; }
                            if (subT > startBoundary) { stops.Add((subT - startBoundary) * zoom); }
                        }
                    }
                }
            }

            float min = bounds.xMin;
            float size = bounds.width;

            stops.Sort();

            // Remove duplicates
            for (int i = 1; i < stops.Count; i++)
            {
                if (Math.Abs(stops[i] - stops[i - 1]) < 0.001f)
                {
                    stops.RemoveAt(i);
                    --i;
                }
            }

            // Map to world space
            int count = stops.Count;
            for (int i = 0; i < count; i++) { stops[i] = (stops[i] * size) + min; }
        }

        bool IsNonLinear(Color a, Color b) => (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a)) > 0.05f;

        UIVertex CreateSplitVertex(UIVertex vertex1, UIVertex vertex2, float stop)
        {
            float sx = vertex1.position.x - stop;
            float dx = vertex1.position.x - vertex2.position.x;
            float dy = vertex1.position.y - vertex2.position.y;

            float ratio = Mathf.Abs(dx) < 0.00001f ? 0 : sx / dx;
            float splitY = vertex1.position.y - (dy * ratio);

            UIVertex splitVertex = new()
            {
                position = new Vector3(stop, splitY, vertex1.position.z),
                normal = Vector3.Lerp(vertex1.normal, vertex2.normal, ratio),
                tangent = Vector4.Lerp(vertex1.tangent, vertex2.tangent, ratio),
                uv0 = Vector2.Lerp(vertex1.uv0, vertex2.uv0, ratio),
                uv1 = Vector2.Lerp(vertex1.uv1, vertex2.uv1, ratio),
                uv2 = Vector2.Lerp(vertex1.uv2, vertex2.uv2, ratio),
                uv3 = Vector2.Lerp(vertex1.uv3, vertex2.uv3, ratio),
                color = Color.Lerp(vertex1.color, vertex2.color, ratio)
            };

            return splitVertex;
        }

        Color BlendColor(Color colorA, Color colorB)
        {
            return blendMode switch
            {
                BlendMode.Add => new Color(
                                        Mathf.Clamp01(colorA.r + colorB.r),
                                        Mathf.Clamp01(colorA.g + colorB.g),
                                        Mathf.Clamp01(colorA.b + colorB.b),
                                        Mathf.Clamp01(colorA.a + colorB.a)
                                    ),
                BlendMode.Subtract => new Color(
                                        Mathf.Clamp01(colorA.r - colorB.r),
                                        Mathf.Clamp01(colorA.g - colorB.g),
                                        Mathf.Clamp01(colorA.b - colorB.b),
                                        colorA.a
                                    ),
                BlendMode.Override => new Color(colorB.r, colorB.g, colorB.b, colorB.a * colorA.a),
                _ => colorA * colorB,
            };
        }

        public void UpdateGradient()
        {
            if (cachedGraphic != null)
                cachedGraphic.SetVerticesDirty();
        }

        public void SetGradient(Gradient newGradient)
        {
            gradientEffect = newGradient;
            CacheGradientKeys();
            UpdateGradient();
        }

        /// <summary>
        /// Sets gradient colors for simple two-color gradients.
        /// </summary>
        public void SetGradientColors(Color startColor, Color endColor)
        {
            gradientEffect = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new(startColor, 0f),
                    new(endColor, 1f)
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new(startColor.a, 0f),
                    new(endColor.a, 1f)
                }
            };

            CacheGradientKeys();
            UpdateGradient();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            CacheGradientKeys();

            if (cachedGraphic == null) { cachedGraphic = GetComponent<Graphic>(); }
            if (cachedGraphic != null) { cachedGraphic.SetVerticesDirty(); }
        }
#endif
    }
}