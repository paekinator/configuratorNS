using UnityEngine;

namespace Evo
{
    public class EvoHeaderAttribute : PropertyAttribute
    {
        public string header;
        public string packageName;

        public EvoHeaderAttribute(string header, string packageName = "")
        {
            this.header = header;
            this.packageName = packageName;
        }
    }
}