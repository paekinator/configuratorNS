using System;
using UnityEngine;
using TMPro;

namespace Evo.UI
{
    public class CalendarDay : MonoBehaviour
    {
        [Header("References")]
        public Button button;
        [SerializeField] private GameObject disabledState;
        [SerializeField] private GameObject normalState;
        [SerializeField] private GameObject currentState;
        [SerializeField] private GameObject selectedState;
        [SerializeField] private TMP_Text[] labels;

        /// <summary>
        /// Stores the date this button represents.
        /// </summary>
        public DateTime Date { get; set; }

        public void SetLabel(string text)
        {
            foreach (TMP_Text lbl in labels)
            {
                if (lbl == null)
                    continue;

                lbl.text = text;
            }
        }

        public void SetState(int index)
        {
            disabledState.SetActive(index == 0);
            normalState.SetActive(index == 1);
            currentState.SetActive(index == 2);
            selectedState.SetActive(index == 3);
        }
    }
}