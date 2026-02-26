using System.Collections.Generic;
using UnityEngine;

public class SelectableBeam : MonoBehaviour
{
    [Header("Visuals")]
    public Material selectionMaterial;

    private bool _selected;
    private Renderer[] _renderers;
    private readonly Dictionary<Renderer, Material[]> _originalMats = new Dictionary<Renderer, Material[]>();

    public List<GameObject> veneerStrips = new List<GameObject>(); // populated by PanelSlotManager pairing rebuild (runtime)
    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);

        _originalMats.Clear();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            _originalMats[r] = r.sharedMaterials;
        }
    }

    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected;

        if (_renderers == null) return;

        foreach (var r in _renderers)
        {
            if (r == null) continue;

            if (_selected)
            {
                if (selectionMaterial == null) continue;

                // Replace ALL sub-materials with selection material
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = selectionMaterial;

                r.sharedMaterials = mats;
            }
            else
            {
                if (_originalMats.TryGetValue(r, out var orig))
                    r.sharedMaterials = orig;
            }
        }
    }

    public bool IsSelected() => _selected;
}
