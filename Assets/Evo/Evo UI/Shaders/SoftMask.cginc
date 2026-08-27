#ifndef SOFTMASK_CGINC
#define SOFTMASK_CGINC

// Shared Variables
sampler2D _SoftMaskTex;
float4x4 _SoftMask_CanvasToLocal;
float4 _SoftMask_Rect;

float4 _SoftMask_PR_Center;
float4 _SoftMask_PR_HalfSize;
float4 _SoftMask_PR_Radii;
float _SoftMask_PR_Softness;
float4 _SoftMask_PR_FillData;

float4 _SoftMask_BorderData;
float4 _SoftMask_UVOuter;
float4 _SoftMask_UVInner;

// Math Functions
float sdRoundedBox(float2 p, float2 b, float4 r)
{
    r.xy = (p.x > 0.0) ? r.xy : r.wz;
    r.x = (p.y > 0.0) ? r.x : r.y;
    float2 q = abs(p) - b + r.x;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r.x;
}

float computeFillMask(float2 p, float2 halfSize, float fillAmount, float fillPacked)
{
    if (fillAmount >= 1.0)
        return 1.0;
    if (fillAmount <= 0.0)
        return 0.0;

    float packed = round(fillPacked);
    float method = fmod(packed, 8.0);
    if (method < 0.5)
        return 1.0;

    float origin = fmod(floor(packed * 0.125), 8.0);
    float cw = floor(packed * 0.015625);
    float2 uv = (p + halfSize) / (halfSize * 2.0);

    if (method < 1.5)
    {
        float coord = (origin < 0.5) ? uv.x : 1.0 - uv.x;
        coord = (cw > 0.5) ? coord : 1.0 - coord;
        float aa = fwidth(coord) * 0.5;
        return 1.0 - smoothstep(fillAmount - aa, fillAmount + aa, coord);
    }

    if (method < 2.5)
    {
        float coord = (origin < 0.5) ? uv.y : 1.0 - uv.y;
        coord = (cw > 0.5) ? coord : 1.0 - coord;
        float aa = fwidth(coord) * 0.5;
        return 1.0 - smoothstep(fillAmount - aa, fillAmount + aa, coord);
    }

#define EVO_PI     3.14159265
#define EVO_TWO_PI 6.28318530

    float startAngle = (origin < 0.5) ? -EVO_PI * 0.5 :
                       (origin < 1.5) ? 0.0 :
                       (origin < 2.5) ? EVO_PI * 0.5 : EVO_PI;

    float angle = atan2(p.y, p.x) - startAngle;
    angle = (cw > 0.5) ? -angle : angle;
    float a = frac(angle / EVO_TWO_PI + 1.0);
    float aa = fwidth(a) * 0.5;
    return 1.0 - smoothstep(fillAmount - aa, fillAmount + aa, a);

#undef EVO_PI
#undef EVO_TWO_PI
}

float map1D(float x, float L, float R, float max_x, float uvMin, float uvL, float uvR, float uvMax)
{
    if (x < L)
    {
        return lerp(uvMin, uvL, (L > 0.001) ? (x / L) : 0.0);
    }
    else if (x > R)
    {
        return lerp(uvR, uvMax, (max_x > R + 0.001) ? ((x - R) / (max_x - R)) : 0.0);
    }
    else
    {
        return lerp(uvL, uvR, (R > L + 0.001) ? ((x - L) / (R - L)) : 0.0);
    }
}

// Master Evaluation Function
inline float SoftMask_GetAlpha(float4 canvasPos)
{
    // If rect width/height is 0, we are not under a mask. Return 100% visible.
    if (_SoftMask_Rect.z <= 0.0)
        return 1.0;

    float4 localPos = mul(_SoftMask_CanvasToLocal, canvasPos);
    float maskAlpha = 1.0;
    float inBounds = 1.0;

#if defined(SOFTMASK_PROCEDURAL)
        float2 sdfCoord = localPos.xy - _SoftMask_PR_Center.xy;
        float2 halfSize = _SoftMask_PR_HalfSize.xy;
        float4 r = min(_SoftMask_PR_Radii, min(halfSize.x, halfSize.y));
        
        float maskDist = sdRoundedBox(sdfCoord, halfSize, r);
        float shapeAa = fwidth(maskDist) * 0.5 + _SoftMask_PR_Softness;
        float shapeMask = 1.0 - smoothstep(-shapeAa, shapeAa, maskDist);
        
        float clipMask = computeFillMask(sdfCoord, halfSize, _SoftMask_PR_FillData.x, _SoftMask_PR_FillData.y);
        maskAlpha = shapeMask * clipMask;
        
#elif defined(SOFTMASK_SLICED)
        float2 p = localPos.xy - _SoftMask_Rect.xy;
        float2 size = _SoftMask_Rect.zw;
        float2 maskUV;

        maskUV.x = map1D(p.x, _SoftMask_BorderData.x, _SoftMask_BorderData.z, size.x, _SoftMask_UVOuter.x, _SoftMask_UVInner.x, _SoftMask_UVInner.z, _SoftMask_UVOuter.z);
        maskUV.y = map1D(p.y, _SoftMask_BorderData.y, _SoftMask_BorderData.w, size.y, _SoftMask_UVOuter.y, _SoftMask_UVInner.y, _SoftMask_UVInner.w, _SoftMask_UVOuter.w);
        
        maskAlpha = tex2D(_SoftMaskTex, maskUV).a;
        
        float2 bounds = step(0.0, p) * step(p, size);
        inBounds = bounds.x * bounds.y;
        
#else
    float2 p = localPos.xy - _SoftMask_Rect.xy;
    float2 size = _SoftMask_Rect.zw;
        
    float2 t = p / size;
    float2 maskUV = lerp(_SoftMask_UVOuter.xy, _SoftMask_UVOuter.zw, t);
        
    maskAlpha = tex2D(_SoftMaskTex, maskUV).a;
        
    float2 bounds = step(0.0, p) * step(p, size);
    inBounds = bounds.x * bounds.y;
        
#endif

    return maskAlpha * inBounds;
}
#endif