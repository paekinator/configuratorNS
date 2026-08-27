using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    [CustomPropertyDrawer(typeof(EvoHideIfAttribute))]
    public class EvoHideIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the standard height if visible (condition NOT met)
            if (ShouldShow(property)) { return EditorGUI.GetPropertyHeight(property, label); }

            // Return negative vertical spacing to collapse the empty space completely
            return -EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        /// <summary>
        /// Logic to determine if the property should be drawn.
        /// Returns true when condition is NOT met (inverse of EvoShowIf).
        /// </summary>
        bool ShouldShow(SerializedProperty property)
        {
            EvoHideIfAttribute hideIf = (EvoHideIfAttribute)attribute;

            // Try to find the property relative to the current property (handles nested classes/structs)
            string path = property.propertyPath;
            string conditionPath = path.Replace(property.name, hideIf.referenceName);
            SerializedProperty referenceProperty = property.serializedObject.FindProperty(conditionPath);

            // Fallback: search on top level if relative search failed
            referenceProperty ??= property.serializedObject.FindProperty(hideIf.referenceName);
            if (referenceProperty == null)
            {
                // If we can't find the reference, show it to avoid hidden errors
                return true;
            }

            bool result;

            switch (referenceProperty.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    result = referenceProperty.boolValue.Equals(hideIf.expectedValue);
                    break;
                case SerializedPropertyType.Enum:
                    // Compare the enum index
                    int currentEnumVal = referenceProperty.intValue;
                    int expectedEnumVal = (int)hideIf.expectedValue;
                    result = currentEnumVal == expectedEnumVal;
                    break;
                case SerializedPropertyType.Integer:
                    result = referenceProperty.intValue.Equals(hideIf.expectedValue);
                    break;
                case SerializedPropertyType.Float:
                    // strict float comparison
                    result = Mathf.Approximately(referenceProperty.floatValue, (float)hideIf.expectedValue);
                    break;
                default:
                    Debug.LogError($"[EvoHideIf] Property type {referenceProperty.propertyType} not supported.");
                    return true;
            }

            // Apply comparison operator
            if (hideIf.comparison == EvoComparison.NotEquals) { result = !result; }

            // Return the inverse - hide when condition is met, show when it's not
            return !result;
        }
    }
}