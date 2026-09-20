using UnityEngine;

/// <summary>
/// Marks a preview that should NOT light the ground beneath it.
///
/// The ground grid outlines the cells whatever is about to be placed will
/// occupy, which it finds from the preview on the ghost layer. That is right
/// for anything arriving on the ground, and wrong for a panel: a panel fills a
/// bay between frames that are already standing, so the ground under it is
/// already spoken for and an outline there marks nothing new. It is one more
/// rectangle drawn over a place the answer already is.
///
/// Put this on the root of such a preview and the grid passes over it. Every
/// other preview is included by default, which is the right way round — a new
/// tool that puts something on the ground gets the highlight without anyone
/// having to remember to ask for it.
/// </summary>
[DisallowMultipleComponent]
public class NoGroundHighlight : MonoBehaviour
{
}
