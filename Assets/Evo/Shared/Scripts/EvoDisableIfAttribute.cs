using System;
using UnityEngine;

namespace Evo
{
    /// <summary>
    /// Disables (ReadOnly) a SINGLE field based on a condition.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EvoDisableIfAttribute : PropertyAttribute
    {
        public string referenceName;
        public object expectedValue;
        public EvoComparison comparison;
        public string packageName;

        public EvoDisableIfAttribute(string referenceName, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = true;
            this.comparison = EvoComparison.Equals;
            this.packageName = packageName;
        }

        public EvoDisableIfAttribute(string referenceName, object expectedValue, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = expectedValue;
            this.comparison = EvoComparison.Equals;
            this.packageName = packageName;
        }

        public EvoDisableIfAttribute(string referenceName, EvoComparison comparison, object expectedValue, string packageName = "")
        {
            this.referenceName = referenceName;
            this.expectedValue = expectedValue;
            this.comparison = comparison;
            this.packageName = packageName;
        }
    }
}