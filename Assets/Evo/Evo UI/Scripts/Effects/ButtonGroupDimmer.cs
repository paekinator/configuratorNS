using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    /// <summary>
    /// Manages a group of buttons and smoothly dims the non-hovered ones.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl)]
    [AddComponentMenu("Evo/UI/Effects/Button Group Dimmer")]
    public class ButtonGroupDimmer : MonoBehaviour
    {
        Button currentButton;
        readonly List<Button> elements = new();

        // Cache
        Coroutine normalCoroutine;
        static readonly WaitForEndOfFrame waitForEndOfFrame = new();

        void Start()
        {
            // Standard for loop to avoid Transform enumerator allocation
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                if (transform.GetChild(i).TryGetComponent<Button>(out var btn))
                {
                    elements.Add(btn);

                    // Capture local variable for the lambda
                    var targetBtn = btn;
                    targetBtn.onPointerEnter.AddListener(() => SetToDim(targetBtn));

                    // Direct method reference prevents closure allocation
                    targetBtn.onPointerExit.AddListener(SetToNormal);
                }
            }
        }

        void SetToDim(Button sourceButton)
        {
            currentButton = sourceButton;

            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].InteractionState == InteractionState.Selected)
                    continue;

                elements[i].SetInteractable(elements[i] == sourceButton);
            }
        }

        void SetToNormal()
        {
            currentButton = null;

            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].InteractionState == InteractionState.Selected)
                    continue;

                elements[i].interactable = true;
            }

            if (normalCoroutine != null)
                StopCoroutine(normalCoroutine);

            normalCoroutine = StartCoroutine(SetToNormalHelper());
        }

        IEnumerator SetToNormalHelper()
        {
            yield return waitForEndOfFrame;

            if (currentButton != null)
                yield break;

            for (int i = 0; i < elements.Count; ++i)
            {
                if (elements[i].InteractionState == InteractionState.Selected)
                    continue;

                elements[i].SetState(InteractionState.Normal);
            }
        }
    }
}