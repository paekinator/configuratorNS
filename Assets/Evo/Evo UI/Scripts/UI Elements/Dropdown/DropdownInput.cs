using UnityEngine;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    public class DropdownInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private TMP_InputField inputField;

        [Header("Settings")]
        [SerializeField] private bool resetIndexOnAwake = true;

        void Awake()
        {
            if (dropdown == null || inputField == null)
                return;

            if (resetIndexOnAwake)
                dropdown.selectedIndex = -1;

            dropdown.onItemSelected.AddListener(UpdateInputField);

            inputField.onValueChanged.AddListener(FilterDropdown);
            inputField.onSelect.AddListener((str) => dropdown.Toggle());
        }

        void Start()
        {
            if (!resetIndexOnAwake)
            {
                FilterDropdown(dropdown.items[dropdown.selectedIndex].label);
                SetValue(dropdown.items[dropdown.selectedIndex].label);
            }
        }

        void FilterDropdown(string input)
        {
            if (dropdown.selectedIndex != -1 && input != dropdown.items[dropdown.selectedIndex].generatedButton.text) 
            {
                dropdown.selectedIndex = -1;
                dropdown.SelectedItem?.generatedButton.SetState(InteractionState.Normal);
            }

            foreach (Dropdown.Item item in dropdown.items)
                item.generatedButton.gameObject.SetActive(item.generatedButton.text.ToLower().Contains(input.ToLower()));
        }

        void UpdateInputField(int index)
        {
            inputField.text = dropdown.items[index].generatedButton.text;
            inputField.onEndEdit?.Invoke(inputField.text);
        }

        public void SetValue(string value) => inputField.text = value;
    }
}