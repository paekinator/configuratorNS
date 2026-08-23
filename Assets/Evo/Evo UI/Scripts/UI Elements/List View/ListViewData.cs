using System.Collections.Generic;
using UnityEngine;

namespace Evo.UI
{
    [System.Serializable]
    public class ListViewColumn
    {
        public string columnName = "Column";
        public Sprite columnIcon;
        public float width = 100;
        public bool useFlexibleWidth = true;
        public TextAnchor alignment = TextAnchor.MiddleCenter;
    }

    [System.Serializable]
    public class ListViewRow
    {
        public List<string> values = new();
        public List<Sprite> icons = new();
        public List<GameObject> customObjects = new();

        public ListViewRow() { }
        public ListViewRow(params string[] rowValues)
        {
            values = new List<string>(rowValues);
            icons = new List<Sprite>(new Sprite[rowValues.Length]);
            customObjects = new List<GameObject>(new GameObject[rowValues.Length]);
        }

        public void SetCell(int index, string value, Sprite icon = null, GameObject obj = null)
        {
            EnsureCapacity(index + 1);
            values[index] = value;
            icons[index] = icon;
            customObjects[index] = obj;
        }

        void EnsureCapacity(int cap)
        {
            while (values.Count < cap) { values.Add(""); }
            while (icons.Count < cap) { icons.Add(null); }
            while (customObjects.Count < cap) { customObjects.Add(null); }
        }

        public Sprite GetIcon(int index) => index < icons.Count ? icons[index] : null;
        public GameObject GetCustomObject(int index) => index < customObjects.Count ? customObjects[index] : null;
    }
}