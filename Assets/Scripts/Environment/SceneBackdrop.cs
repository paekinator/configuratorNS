using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marks the quad that paints the studio backdrop behind the 3D view, holds
/// the one copy of the gradient's colours, and keeps the quad from painting
/// over anything it does not belong to.
///
/// WHY A MARKER RATHER THAN A NAME. Scene-wide renderer scans have to tell the
/// model from the scenery, and the one that already existed does it by name
/// substring ("GridFloor"). A backdrop that filled the screen and was counted
/// as part of the model would ruin every camera framing in the app, silently
/// and only sometimes. A component cannot be broken by a rename.
///
/// WHY THE COLOURS LIVE HERE. Three things want to know them: the shader (via
/// the material the styler writes), the camera's solid clear colour underneath
/// the quad, and UIThemeController, which repaints that clear colour at
/// runtime. They were already two values disagreeing — the environment styler
/// cleared to #EAE5DD while the theme cleared to #F2F2F2, so the background
/// changed the moment you pressed Play.
///
/// WHY IT GATES ITS OWN RENDERER. The gradient shader writes clip-space
/// positions directly, so it fills whatever viewport it is drawn into,
/// ignoring where the quad actually is. Left ungated it would paint over the
/// editor's Scene view whenever the quad drifted into that view's frustum, and
/// over every thumbnail capture. It draws for its own camera and nothing else.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SceneBackdrop : MonoBehaviour
{
    /// <summary>Name of the backdrop object; it hangs off the camera.</summary>
    public const string ObjectName = "SceneBackdrop";

    // The backdrop is stated as FOUR CORNER BRIGHTNESSES of a neutral grey,
    // not as an angle with colour stops along it. An angled gradient has to
    // decide how to normalise across the screen, and the two reasonable
    // answers disagree badly on a wide screen: CSS divides by (width+height)
    // in pixels, the obvious shader version divides each axis by its own
    // length. The second sent the bottom of the view grey far sooner and read
    // as flat. Corners have no such choice to get wrong.
    //
    // These are brightness percentages as a colour picker shows them —
    // 100 / 97 / 85 / 80 — which is why the shader blends them in display
    // space rather than in the linear space Unity hands it.
    //
    // Neutral, with no hue. The mockup's own greys lead green, and that cast
    // was corrected to zero chroma everywhere else in this UI.
    public static readonly Color TopLeft = Hex("FFFFFF");      // 100%
    public static readonly Color TopRight = Hex("F7F7F7");     //  97%
    public static readonly Color BottomLeft = Hex("D9D9D9");   //  85%
    public static readonly Color BottomRight = Hex("CCCCCC");  //  80%

    /// <summary>
    /// What the camera clears to underneath the quad: the average of the four
    /// corners, which is what the gradient itself shows at the centre of the
    /// screen. Computed rather than written down, so it cannot drift when a
    /// corner is retuned. A view the quad does not draw into — a thumbnail, a
    /// scene that has not been re-styled — then reads as the same backdrop
    /// rather than as a different colour.
    /// </summary>
    public static Color ClearColor =>
        (TopLeft + TopRight + BottomLeft + BottomRight) * 0.25f;

    /// <summary>
    /// True for a renderer that is scenery, not model. Scene-wide scans that
    /// measure what has been BUILT must skip these.
    /// </summary>
    public static bool IsBackdrop(Renderer renderer)
    {
        return renderer != null && renderer.GetComponent<SceneBackdrop>() != null;
    }

    Renderer _renderer;
    Camera _owner;

    void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        _owner = GetComponentInParent<Camera>();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

        // Left off, the quad would be invisible in the editor the moment this
        // component was disabled, with nothing on screen to say why.
        if (_renderer != null)
            _renderer.enabled = true;
    }

    /// <summary>
    /// URP raises this once per camera, before that camera culls — which is
    /// the one moment where "should this camera see the backdrop?" can still
    /// be answered.
    /// </summary>
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (_renderer == null)
            return;

        // A backdrop with no camera above it cannot know which view it belongs
        // to, so it draws into none rather than into all of them.
        _renderer.enabled = _owner != null && camera == _owner;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
}
