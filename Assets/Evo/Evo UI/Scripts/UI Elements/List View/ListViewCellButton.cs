using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Evo.UI
{
    public class ListViewCellButton : MonoBehaviour, IPointerClickHandler
    {
        public UnityEvent onClick = new();

        public virtual void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();
    }
}