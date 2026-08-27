using UnityEngine;
using UnityEditor;

namespace Evo.EditorTools
{
    [CustomPropertyDrawer(typeof(EvoHeaderAttribute))]
    public class EvoHeaderDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            EvoHeaderAttribute headerAttribute = (EvoHeaderAttribute)attribute;

            // Only draw header if custom editor is disabled for this package
            if (!EvoEditorSettings.IsCustomEditorEnabled(headerAttribute.packageName))
            {
                // Add spacing above the header (like Unity's Header)
                position.y += 6f;
                position.height = EditorGUIUtility.singleLineHeight;

                // Draw the header text
                EditorGUI.LabelField(position, headerAttribute.header, EditorStyles.boldLabel);
            }
        }

        public override float GetHeight()
        {
            EvoHeaderAttribute headerAttribute = (EvoHeaderAttribute)attribute;
            if (!EvoEditorSettings.IsCustomEditorEnabled(headerAttribute.packageName))
            {
                // Match Unity's Header spacing
                return 6f + EditorGUIUtility.singleLineHeight;
            }
            return 0f;
        }
    }
}