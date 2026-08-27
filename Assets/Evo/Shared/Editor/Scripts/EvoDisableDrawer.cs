using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    [CustomPropertyDrawer(typeof(EvoDisableAttribute))]
    public class EvoDisableDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EvoDisableAttribute disableAttr = (EvoDisableAttribute)attribute;

            // 1. Check Settings
            // If custom editor features are disabled, we revert to standard Unity behavior (Editable)
            if (!EvoEditorSettings.IsCustomEditorEnabled(disableAttr.packageName))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 2. Unconditionally Disable
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }
}