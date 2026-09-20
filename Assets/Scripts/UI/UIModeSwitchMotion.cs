using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single sliding capsule underneath both mode labels.</summary>
public sealed class UIModeSwitchMotion : MonoBehaviour
{
    public float duration = .22f;
    Button _pro, _lite;
    RectTransform _thumb;
    Image _fill;
    Vector3 _from, _to;
    float _elapsed;
    bool _last;

    public void Initialize(Button pro, Button lite)
    {
        _pro=pro; _lite=lite;
        var existing=transform.Find("SlidingSelection");
        if(existing == null)
        {
            existing=new GameObject("SlidingSelection",typeof(RectTransform),typeof(Image)).transform;
            existing.SetParent(transform,false);
        }
        _thumb=(RectTransform)existing;
        var layout=existing.GetComponent<LayoutElement>();
        if(layout == null)layout=existing.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout=true;
        _thumb.SetAsFirstSibling();
        _fill=_thumb.GetComponent<Image>();
        var source=pro.GetComponent<Image>();
        _fill.sprite=source.sprite; _fill.type=source.type;
        _fill.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
        _fill.raycastTarget=false;
        _thumb.anchorMin=_thumb.anchorMax=new Vector2(.5f,.5f);
        _last=SpaceModeController.Active;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        Snap();
        pro.transition=lite.transition=Selectable.Transition.None;
    }

    Vector3 Target
    {
        get
        {
            var rt=(RectTransform)(SpaceModeController.Active ? _lite : _pro).transform;
            return transform.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
        }
    }
    void Snap() { if(_thumb == null)return; _thumb.localPosition=Target; _from=_to=Target; _elapsed=duration; }
    void OnEnable() => Snap();

    void LateUpdate()
    {
        if(_pro == null || _lite == null || _thumb == null)return;
        bool active=SpaceModeController.Active;
        if(active != _last)
        {
            _from=_thumb.localPosition; _to=Target; _elapsed=0; _last=active;
        }
        _elapsed += Time.unscaledDeltaTime;
        if(_elapsed >= duration)_to=Target;
        float t=Mathf.Clamp01(_elapsed/Mathf.Max(.01f,duration));
        t=t*t*(3f-2f*t);
        _thumb.localPosition=Vector3.Lerp(_from,_to,t);
        _thumb.sizeDelta=((RectTransform)_pro.transform).rect.size;
        Paint(_pro,!active); Paint(_lite,active);
    }

    void Paint(Button button, bool selected)
    {
        var canvas=GetComponentInParent<Canvas>();
        Camera cam=canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool hover=RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform,Input.mousePosition,cam);
        button.GetComponent<Image>().color=!selected && hover ? new Color(0,0,0,.07f) : Color.clear;
        var label=button.GetComponentInChildren<TMP_Text>(true);
        if(label != null)label.color=selected ? Color.white : hover ? new Color32(65,65,65,255) : new Color32(135,135,135,255);
        if(selected)_fill.color=hover ? new Color32(55,55,55,255) : new Color32(33,33,33,255);
    }
}
