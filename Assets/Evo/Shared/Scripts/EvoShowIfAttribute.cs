using System;
using UnityEngine;

namespace Evo
{
    /// <summary>
    /// Hides a single field based on one or multiple conditions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EvoShowIfAttribute : PropertyAttribute
    {
        public string referenceName;
        public object[] expectedValues;
        
        // Exposing these as public fields allows them to be set via named arguments
        public EvoComparison comparison = EvoComparison.Equals;
        public string packageName = "";

        /// <summary>
        /// Default constructor (assumes a boolean condition evaluating to true)
        /// </summary>
        public EvoShowIfAttribute(string referenceName)
        {
            this.referenceName = referenceName;
            this.expectedValues = new object[] { true };
        }

        /// <summary>
        /// Accepts any number of expected values (OR logic).
        /// Example: [EvoShowIf("itemType", ItemType.Button, ItemType.Section)]
        /// </summary>
        public EvoShowIfAttribute(string referenceName, params object[] expectedValues)
        {
            this.referenceName = referenceName;
            this.expectedValues = expectedValues;
        }
    }
}