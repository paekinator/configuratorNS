using System;
using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    [CustomPropertyDrawer(typeof(EvoShowIfAttribute))]
    public class EvoShowIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Return the standard height if visible
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
        /// Logic to determine if the property should be drawn based on the referenced property.
        /// </summary>
        bool ShouldShow(SerializedProperty property)
        {
            EvoShowIfAttribute showIf = (EvoShowIfAttribute)attribute;

            // Try to find the property relative to the current property (handles nested classes/structs)
            string path = property.propertyPath;
            string conditionPath = path.Replace(property.name, showIf.referenceName);
            SerializedProperty referenceProperty = property.serializedObject.FindProperty(conditionPath);

            // Fallback: search on top level if relative search failed
            referenceProperty ??= property.serializedObject.FindProperty(showIf.referenceName);
            if (referenceProperty == null)
            {
                return true;
            }

            bool result = false;

            // Loop through all expected values. If ANY match, we set result to true (OR logic).
            if (showIf.expectedValues != null && showIf.expectedValues.Length > 0)
            {
                foreach (var expectedValue in showIf.expectedValues)
                {
                    if (Matches(referenceProperty, expectedValue))
                    {
                        result = true;
                        break;
                    }
                }
            }
            else
            {
                // Fallback for empty parameters
                result = Matches(referenceProperty, true);
            }

            // Apply comparison operator
            if (showIf.comparison == EvoComparison.NotEquals) { return !result; }
            return result;
        }

        /// <summary>
        /// Compares a single SerializedProperty against a single expected object.
        /// </summary>
        bool Matches(SerializedProperty referenceProperty, object expectedValue)
        {
            try
            {
                switch (referenceProperty.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        return referenceProperty.boolValue == Convert.ToBoolean(expectedValue);
                    
                    case SerializedPropertyType.Enum:
                    case SerializedPropertyType.Integer:
                        // System.Convert safely unboxes the enum object into an integer representation
                        return referenceProperty.intValue == Convert.ToInt32(expectedValue);
                    
                    case SerializedPropertyType.Float:
                        return Mathf.Approximately(referenceProperty.floatValue, Convert.ToSingle(expectedValue));
                    
                    default:
                        Debug.LogError($"[EvoShowIf] Property type {referenceProperty.propertyType} not supported.");
                        return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[EvoShowIf] Error comparing values. Exception: {e.Message}");
                return true;
            }
        }
    }
}