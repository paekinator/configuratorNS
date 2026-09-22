using UnityEngine;

/// <summary>
/// Camera questions whose answers depend on the projection, answered once.
///
/// The camera is orthographic in the isometric scheme and perspective in
/// Walkthrough, and several pieces of code had quietly assumed perspective:
/// they sized things by the camera's DISTANCE, which in an orthographic view
/// says nothing about how big anything looks. An orthographic camera sits far
/// back purely so it does not clip the build, and every gizmo sized by that
/// distance would have grown enormous.
///
/// Anything that needs "how close is the viewer, really" asks here.
/// </summary>
public static class CameraMath
{
    /// <summary>
    /// The distance a PERSPECTIVE camera would have to be at to show
    /// <paramref name="point"/> at the scale this camera shows it.
    ///
    /// For a perspective camera that is simply the distance. For an
    /// orthographic one it is the distance at which the same field of view
    /// covers the same height of world — so code written for perspective
    /// ("scale with distance") keeps producing the same on-screen size in both,
    /// and nothing that already worked changes by a pixel.
    /// </summary>
    public static float EffectiveDistance(Camera cam, Vector3 point)
    {
        if (cam == null)
            return 1f;

        if (!cam.orthographic)
            return Vector3.Distance(cam.transform.position, point);

        float halfFov = Mathf.Max(cam.fieldOfView, 1f) * 0.5f * Mathf.Deg2Rad;
        return cam.orthographicSize / Mathf.Tan(halfFov);
    }

    /// <summary>
    /// World units covered by one screen pixel at <paramref name="point"/>.
    /// </summary>
    public static float WorldPerPixel(Camera cam, Vector3 point)
    {
        if (cam == null)
            return 0.01f;

        float height = Mathf.Max(cam.pixelHeight, 1);
        if (cam.orthographic)
            return 2f * cam.orthographicSize / height;

        float halfFov = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        return 2f * Vector3.Distance(cam.transform.position, point) * Mathf.Tan(halfFov) / height;
    }

    /// <summary>
    /// Where the camera's view axis meets the ground plane at
    /// <paramref name="groundY"/>. False when it never does — level with the
    /// ground or looking up — and <paramref name="focus"/> is then the
    /// camera's own position dropped onto the ground.
    ///
    /// Correct for both projections: an orthographic camera looks along its
    /// forward from its whole near plane, so its centre ray is still the one
    /// that says what is in the middle of the screen.
    /// </summary>
    public static bool GroundFocus(Camera cam, float groundY, out Vector3 focus)
    {
        Transform t = cam.transform;
        Vector3 origin = t.position;
        Vector3 direction = t.forward;

        if (direction.y < -0.05f)
        {
            float distance = (origin.y - groundY) / -direction.y;
            if (distance > 0f && distance < 10000f)
            {
                focus = origin + direction * distance;
                return true;
            }
        }

        focus = new Vector3(origin.x, groundY, origin.z);
        return false;
    }
}
