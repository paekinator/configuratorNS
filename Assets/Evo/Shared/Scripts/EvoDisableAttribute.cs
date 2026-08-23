using System;
using UnityEngine;

namespace Evo
{
    /// <summary>
    /// Unconditionally disables (ReadOnly) a SINGLE field in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EvoDisableAttribute : PropertyAttribute
    {
        public string packageName;

        public EvoDisableAttribute(string packageName = "")
        {
            this.packageName = packageName;
        }
    }
}