using System;
using UnityEngine;

namespace Evo
{
    /// <summary>
    /// Hides a single field based on a condition (inverse of EvoShowIf).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EvoHideIfAttribute : PropertyAttribute
    {
        public string referenceName;
        public object expectedValue;
        public EvoComparison comparison;
        public string packageName;

        public EvoHideIfAttribute(string referenceName, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = true;
            this.comparison = EvoComparison.Equals;
            this.packageName = packageName;
        }

        public EvoHideIfAttribute(string referenceName, object expectedValue, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = expectedValue;
            this.comparison = EvoComparison.Equals;
            this.packageName = packageName;
        }

        public EvoHideIfAttribute(string referenceName, EvoComparison comparison, object expectedValue, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = expectedValue;
            this.comparison = comparison;
            this.packageName = packageName;
        }
    }
}