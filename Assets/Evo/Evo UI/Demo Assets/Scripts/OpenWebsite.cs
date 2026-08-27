using UnityEngine;

namespace Evo.UI.Demo
{
    public class OpenWebsite : MonoBehaviour
    {
        public void Open(string url)
        {
            Application.OpenURL(url);
        }
    }
}