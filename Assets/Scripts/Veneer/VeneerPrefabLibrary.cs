using System;
using UnityEngine;

public class VeneerPrefabLibrary : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Strip Prefabs (Index 0..14 = H1..H15)")]
    public GameObject[] interiorStrips = new GameObject[15];
    public GameObject[] exteriorStrips = new GameObject[15];

    [Header("Caps / Foot")]
    public GameObject topCap;
    public GameObject sideCap;
    public GameObject foot;

    [Header("Prefab Orientation Assumptions")]
    [Tooltip("Which local axis of the strip prefab represents its LENGTH direction.")]
    public Axis stripLengthAxis = Axis.Z;

    [Tooltip("Which local axis of the strip prefab points OUTWARD from the beam face.")]
    public Axis stripOutwardAxis = Axis.Y;

    [Header("Debug")]
    public bool logMeasuredLengths = false;

    private float[] _interiorLengths;
    private float[] _exteriorLengths;

    public int MaxIndex { get { return 15; } }

    void Awake()
    {
        _interiorLengths = new float[15];
        _exteriorLengths = new float[15];

        for (int i = 0; i < 15; i++)
        {
            _interiorLengths[i] = MeasurePrefabLengthSafe(interiorStrips[i], stripLengthAxis);
            _exteriorLengths[i] = MeasurePrefabLengthSafe(exteriorStrips[i], stripLengthAxis);

            if (logMeasuredLengths)
            {
                Debug.Log(string.Format(
                    "[VeneerLibrary] H{0} interior length={1:F4}, exterior length={2:F4} (prefabInt={3}, prefabExt={4})",
                    i + 1, _interiorLengths[i], _exteriorLengths[i],
                    interiorStrips[i] ? interiorStrips[i].name : "NULL",
                    exteriorStrips[i] ? exteriorStrips[i].name : "NULL"
                ));
            }
        }
    }

    public GameObject GetStrip(bool exterior, int hIndex1to15)
    {
        int idx = Mathf.Clamp(hIndex1to15, 1, 15) - 1;
        return exterior ? exteriorStrips[idx] : interiorStrips[idx];
    }

    public float GetStripLength(bool exterior, int hIndex1to15)
    {
        int idx = Mathf.Clamp(hIndex1to15, 1, 15) - 1;
        return exterior ? _exteriorLengths[idx] : _interiorLengths[idx];
    }

    public int FindBestIndex(bool exterior, float targetLength)
    {
        float best = float.MaxValue;
        int bestIdx = 1;

        for (int i = 0; i < 15; i++)
        {
            float len = exterior ? _exteriorLengths[i] : _interiorLengths[i];
            if (len <= 0f) continue;

            float d = Mathf.Abs(len - targetLength);
            if (d < best)
            {
                best = d;
                bestIdx = i + 1;
            }
        }

        return bestIdx;
    }

    public static Vector3 AxisToVector(Axis a)
    {
        if (a == Axis.X) return Vector3.right;
        if (a == Axis.Y) return Vector3.up;
        return Vector3.forward;
    }

    /// <summary>
    /// Safer than measuring prefab-asset matrices: instantiate hidden, measure, destroy.
    /// </summary>
    private static float MeasurePrefabLengthSafe(GameObject prefab, Axis axis)
    {
        if (prefab == null) return 0f;

        GameObject temp = null;
        try
        {
            temp = Instantiate(prefab);
            temp.name = "__TEMP_VENEER_MEASURE__";
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.SetActive(true);

            // measure in temp-root local space
            Renderer[] rends = temp.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return 0f;

            Bounds rootLocalBounds;
            if (!ComputeRootLocalBounds(temp.transform, rends, out rootLocalBounds))
                return 0f;

            Vector3 size = rootLocalBounds.size;
            if (axis == Axis.X) return Mathf.Abs(size.x);
            if (axis == Axis.Y) return Mathf.Abs(size.y);
            return Mathf.Abs(size.z);
        }
        finally
        {
            if (temp != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(temp);
                else Destroy(temp);
#else
                Destroy(temp);
#endif
            }
        }
    }

    private static bool ComputeRootLocalBounds(Transform root, Renderer[] rends, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool inited = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        Matrix4x4 rootWorldToLocal = root.worldToLocalMatrix;

        for (int r = 0; r < rends.Length; r++)
        {
            Renderer ren = rends[r];
            if (ren == null) continue;

            Bounds lb = ren.localBounds;
            Transform t = ren.transform;

            Matrix4x4 m = rootWorldToLocal * t.localToWorldMatrix;

            Vector3 c = lb.center;
            Vector3 e = lb.extents;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = c + Vector3.Scale(e, new Vector3(sx, sy, sz));
                Vector3 p = m.MultiplyPoint3x4(corner);

                if (!inited) { inited = true; min = max = p; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
        }

        if (!inited) return false;

        bounds = new Bounds((min + max) * 0.5f, (max - min));
        return true;
    }
}