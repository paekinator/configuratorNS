using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "navigation/ui-navigation")]
    [AddComponentMenu("Evo/UI/Navigation/UI Navigation")]
    public class UINavigation : Selectable
    {
        protected override void Start()
        {
            if (transition != Transition.None) 
            { 
                transition = Transition.None; 
            }
        }
    }
}