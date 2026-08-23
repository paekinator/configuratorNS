using System.Collections;
using UnityEngine;

namespace Evo.UI.Demo
{
    public class DemoManager : MonoBehaviour
    {
        [Header("Splash Screen")]
        [SerializeField] private Animator splashAnimator;

        [Header("Content Arrow")]
        [SerializeField] private Transform listParent;
        [SerializeField] private Button prevArrow;
        [SerializeField] private Button nextArrow;

        IEnumerator Start()
        {
            if (splashAnimator == null)
                yield break;

            float waitTime = Utilities.GetAnimationClipLength(splashAnimator, "SplashScreen_In") + 0.1f;
            splashAnimator.gameObject.SetActive(true);

            yield return Time.timeScale == 1 ? new WaitForSeconds(waitTime) : new WaitForSecondsRealtime(waitTime);
            Destroy(splashAnimator.gameObject);
        }

        public void ManageContentArrows(int index)
        {
            if (prevArrow != null) { prevArrow.SetInteractable(index > 0); }
            if (nextArrow != null) { nextArrow.SetInteractable(index < listParent.childCount - 1); }
        }
    }
}