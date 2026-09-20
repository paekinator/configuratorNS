using TMPro;
using UnityEngine;

/// <summary>
/// The open project's name, on screen, as the mockup has it: a small dim
/// eyebrow over the name itself.
///
/// It reads "Untitled design" until a project is opened or saved, which is the
/// honest answer rather than a blank — work with no project behind it is the
/// normal state when you have just started, not an error.
/// </summary>
public class ProjectNameDisplay : MonoBehaviour
{
    public TextMeshProUGUI nameText;

    void OnEnable()
    {
        CurrentProject.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => CurrentProject.Changed -= Refresh;

    void Refresh()
    {
        if (nameText != null)
            nameText.text = CurrentProject.DisplayName;
    }
}
