using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drag a block card onto a collection row to move it there.
///
/// Dragging is the gesture because the question is "which of these folders
/// does this belong in", and both the card and the folders are already on
/// screen together. A menu would make you name the destination you are
/// looking straight at.
///
/// The card itself never moves. A translucent copy follows the cursor and the
/// original stays where it is: if the drop is refused or abandoned, there is
/// nothing to put back, and the row you are dragging out of does not reflow
/// under your hand while you aim.
/// </summary>
public class BlockCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public BlocksPanelUI panel;
    public string blockId;

    RectTransform _ghost;
    CanvasGroup _ghostGroup;
    Canvas _canvas;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (panel == null || string.IsNullOrEmpty(blockId))
            return;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
            return;

        // A copy, not the original. Cloned without its own drag handler, or
        // the ghost would try to start a drag of its own.
        GameObject clone = Instantiate(gameObject, _canvas.transform);
        clone.name = "DragGhost";
        foreach (BlockCardDrag drag in clone.GetComponentsInChildren<BlockCardDrag>(true))
            Destroy(drag);
        foreach (Button button in clone.GetComponentsInChildren<Button>(true))
            Destroy(button);

        _ghost = (RectTransform)clone.transform;
        _ghost.sizeDelta = ((RectTransform)transform).rect.size;
        _ghost.SetAsLastSibling();

        _ghostGroup = clone.AddComponent<CanvasGroup>();
        _ghostGroup.alpha = 0.7f;
        _ghostGroup.blocksRaycasts = false;   // or it would be its own drop target

        Move(eventData);
        panel.BeginBlockDrag(blockId);
    }

    public void OnDrag(PointerEventData eventData) => Move(eventData);

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_ghost != null)
            Destroy(_ghost.gameObject);
        _ghost = null;

        if (panel != null)
            panel.EndBlockDrag(eventData);
    }

    void Move(PointerEventData eventData)
    {
        if (_ghost == null || _canvas == null)
            return;

        // Screen point to canvas point: the canvas scales with the window, so
        // screen pixels are not canvas units and assigning the raw position
        // would put the ghost a long way from the cursor.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, eventData.position,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out Vector2 local))
        {
            _ghost.localPosition = local;
        }
    }
}
