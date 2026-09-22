// Keeps the ground from being cut off by the camera's near clip plane in the
// orthographic (isometric) view.
//
// WHY IT GOT CUT. The isometric camera stands as close to the build as it
// safely can, because URP measures shadow distance from the camera and a
// camera parked far back loses every shadow. Zoom out, and the near clip plane
// — the flat plane just in front of the lens — tilts down with the view and
// passes BELOW the ground at the bottom of the screen. Everything behind it is
// clipped, so the grid and the shadow catcher ended in a hard line across the
// view, however far the grid itself extended.
//
// WHY THIS FIX IS EXACT. In an orthographic projection a point's position on
// screen does not depend on its depth at all. So a ground vertex that has
// fallen behind the near plane can have its DEPTH pulled forward to the plane
// without moving a single pixel. Nothing is clipped, and nothing looks
// different. In perspective, depth and screen position are entangled, so both
// helpers do nothing there — Walkthrough never needs them anyway.
//
// WHY DEPTH IS THEN RE-WRITTEN PER PIXEL. Pulling some vertices forward bends
// the depth interpolated across the triangle between them, and the ground quad
// is one very large triangle pair: the bent depth would reach right under the
// build and let the grid draw on top of a part standing on it. Each fragment
// therefore writes its TRUE depth, clamped only where it genuinely lies behind
// the plane — where, by construction, there is nothing else to draw.
//
// Include after Core.hlsl.

#ifndef NEOSPACE_GROUND_CLIP_INCLUDED
#define NEOSPACE_GROUND_CLIP_INCLUDED

bool NeospaceIsOrthographic()
{
    return unity_OrthoParams.w > 0.5;
}

/// Vertex stage: pull a ground vertex that is behind the near plane up to it.
float4 NeospaceKeepGroundInFront(float4 positionCS)
{
    if (NeospaceIsOrthographic())
    {
    #if UNITY_REVERSED_Z
        // Near is at z = w, far at 0: behind the near plane means z > w.
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        // Near is at z = -w: behind it means z < -w.
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    }
    return positionCS;
}

/// Fragment stage: the true depth of this ground point, as SV_Depth expects
/// it (0..1 device depth), clamped to the near plane only in orthographic.
float NeospaceGroundDepth(float3 positionWS)
{
    float4 cs = TransformWorldToHClip(positionWS);
    float z = cs.z / max(cs.w, 1e-6);

#if UNITY_REVERSED_Z
    // Clip z is already 0..1, near at 1.
    if (NeospaceIsOrthographic())
        z = min(z, 1.0);
    return z;
#else
    // Clip z is -1..1 on these platforms (WebGL among them); device depth is
    // 0..1, near at 0.
    z = z * 0.5 + 0.5;
    if (NeospaceIsOrthographic())
        z = max(z, 0.0);
    return z;
#endif
}

#endif
