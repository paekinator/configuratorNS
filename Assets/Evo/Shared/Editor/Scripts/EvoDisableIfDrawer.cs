using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    [CustomPropertyDrawer(typeof(EvoDisableIfAttribute))]
    public class EvoDisableIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EvoDisableIfAttribute disableIf = (EvoDisableIfAttribute)attribute;

            // 1. Check Settings
            if (!EvoEditorSettings.IsCustomEditorEnabled(disableIf.packageName))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 2. Check Logic
            bool shouldDisable = ShouldDisable(property, disableIf);
            using (new EditorGUI.DisabledScope(shouldDisable))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        bool ShouldDisable(SerializedProperty property, EvoDisableIfAttribute disableIf)
        {
            // Find property relative to current property
            string path = property.propertyPath;
            string conditionPath = path.Replace(property.name, disableIf.referenceName);
            SerializedProperty referenceProperty = property.serializedObject.FindProperty(conditionPath);

            // Fallback: search top level
            referenceProperty ??= property.serializedObject.FindProperty(disableIf.referenceName);
            if (referenceProperty == null) { return false; } // Default to enabled if ref not found

            bool result = false;

            switch (referenceProperty.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    result = referenceProperty.boolValue.Equals(disableIf.expectedValue);
                    break;
                case SerializedPropertyType.Enum:
                    int currentEnumVal = referenceProperty.intValue;
                    int expectedEnumVal = (int)disableIf.expectedValue;
                    result = currentEnumVal == expectedEnumVal;
                    break;
                case SerializedPropertyType.Integer:
                    result = referenceProperty.intValue.Equals(disableIf.expectedValue);
                    break;
                case SerializedPropertyType.Float:
                    result = Mathf.Approximately(referenceProperty.floatValue, (float)disableIf.expectedValue);
                    break;
            }

            // If Comparison is NotEquals, invert result
            if (disableIf.comparison == EvoComparison.NotEquals) { result = !result; }

            // If the condition is MET, we DISABLE. 
            // Wait - usually "DisableIf(true)" means "Disable if this is true".
            // So if result is true, we return true (to disable).
            return result;
        }
    }
}