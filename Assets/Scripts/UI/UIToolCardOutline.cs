using UnityEngine;
using UnityEngine.UI;

/// <summary>Inset rounded outline, without tinting the card's text or icons.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIToolCardOutline : MaskableGraphic
{
    public float thickness = 2f;
    public float radius = 12f;

    public static void Apply(Transform card, bool selected)
    {
        var child=card.Find("SelectedOutline");
        if(child == null)
        {
            if(!selected)return;
            child=new GameObject("SelectedOutline",typeof(RectTransform),typeof(UIToolCardOutline)).transform;
            child.SetParent(card,false);
            var rt=(RectTransform)child;
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
            rt.offsetMin=rt.offsetMax=Vector2.zero;
            var graphic=child.GetComponent<UIToolCardOutline>();
            graphic.raycastTarget=false; graphic.color=new Color32(174,174,174,255);
        }
        child.gameObject.SetActive(selected);
        if(selected)
        {
            var canvas=card.GetComponentInParent<Canvas>();
            var body=canvas != null ? canvas.transform.Find("Dock") : null;
            var background=body != null ? body.GetComponent<Image>() : null;
            card.GetComponent<Image>().color=background != null ? background.color : new Color32(33,33,33,255);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r=rectTransform.rect;
        float rad=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
        const int steps=8;
        for(int c=0;c<4;c++)
        {
            Vector2 centre=new Vector2(c==0 || c==3 ? r.xMax-rad : r.xMin+rad,c<2 ? r.yMax-rad : r.yMin+rad);
            for(int s=0;s<=steps;s++)
            {
                float a=(c*90f+s*90f/steps)*Mathf.Deg2Rad;
                Vector2 n=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                vh.AddVert(centre+n*rad,color,Vector2.zero);
                vh.AddVert(centre+n*Mathf.Max(0,rad-thickness),color,Vector2.zero);
            }
        }
        int count=vh.currentVertCount;
        for(int i=0;i<count;i+=2)
        {
            int next=(i+2)%count;
            vh.AddTriangle(i,next,i+1); vh.AddTriangle(next,(next+1)%count,i+1);
        }
    }
}
