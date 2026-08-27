Shader "Evo/UI/Procedural Rect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        // Fill
        _FillColor           ("Fill Color",           Vector) = (1,1,1,1)
        _FillColorMode       ("Fill Color Mode",      Float) = 0
        _FillGradientTex     ("Fill Gradient Tex",    2D)    = "white" {}
        _FillGradientAngle   ("Fill Gradient Angle",  Float) = 0
        _FillGradientZoom    ("Fill Gradient Zoom",   Float) = 1
        _FillGradientReverse ("Fill Gradient Reverse",Float) = 0

        // Outline
        _OutlineColor           ("Outline Color",          Vector) = (0,0,0,1)
        _OutlineColorMode       ("Outline Color Mode",     Float) = 0
        _OutlineGradientTex     ("Outline Gradient Tex",   2D)    = "white" {}
        _OutlineGradientAngle   ("Outline Gradient Angle", Float) = 0
        _OutlineGradientZoom    ("Outline Gradient Zoom",  Float) = 1
        _OutlineGradientReverse ("Outline Gradient Rev",   Float) = 0

        // Inner Shadow
        _InnerShadowColor           ("Inner Shadow Color",          Vector) = (0,0,0,0.4)
        _InnerShadowColorMode       ("Inner Shadow Color Mode",     Float) = 0
        _InnerShadowGradientTex     ("Inner Shadow Gradient Tex",   2D)    = "white" {}
        _InnerShadowGradientAngle   ("Inner Shadow Gradient Angle", Float) = 0
        _InnerShadowGradientZoom    ("Inner Shadow Gradient Zoom",  Float) = 1
        _InnerShadowGradientReverse ("Inner Shadow Gradient Rev",   Float) = 0

        // Outer Shadow
        _OuterShadowColor           ("Outer Shadow Color",          Vector) = (0,0,0,0.4)
        _OuterShadowColorMode       ("Outer Shadow Color Mode",     Float) = 0
        _OuterShadowGradientTex     ("Outer Shadow Gradient Tex",   2D)    = "white" {}
        _OuterShadowGradientAngle   ("Outer Shadow Gradient Angle", Float) = 0
        _OuterShadowGradientZoom    ("Outer Shadow Gradient Zoom",  Float) = 1
        _OuterShadowGradientReverse ("Outer Shadow Gradient Rev",   Float) = 0

        // Mask/stencil
        _StencilComp      ("Stencil Comparison",  Float) = 8
        _Stencil          ("Stencil ID",          Float) = 0
        _StencilOp        ("Stencil Operation",   Float) = 0
        _StencilWriteMask ("Stencil Write Mask",  Float) = 255
        _StencilReadMask  ("Stencil Read Mask",   Float) = 255
        _ColorMask        ("Color Mask",          Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Soft Mask Parameters
        [HideInInspector] _SoftMaskTex ("Soft Mask Texture", 2D) = "white" {}
        [HideInInspector] _SoftMask_Mode ("Mask Mode", Float) = 0
        [HideInInspector] _SoftMask_Rect ("Soft Mask Rect", Vector) = (0,0,0,0)
        
        [HideInInspector] _SoftMask_PR_Center ("PR Center", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_HalfSize ("PR Half Size", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_Radii ("PR Radii", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_PR_Softness ("PR Softness", Float) = 0
        [HideInInspector] _SoftMask_PR_FillData ("PR Fill Data", Vector) = (0,0,0,0)

        [HideInInspector] _SoftMask_BorderData ("Border Data", Vector) = (0,0,0,0)
        [HideInInspector] _SoftMask_UVOuter ("UV Outer", Vector) = (0,0,1,1)
        [HideInInspector] _SoftMask_UVInner ("UV Inner", Vector) = (0,0,1,1)
    }

    CGINCLUDE

    #include "UnityCG.cginc"
    #include "UnityUI.cginc"
    #include "SoftMask.cginc"

    sampler2D _MainTex;
    float4    _Color;
    float4    _ClipRect;

    float     _UIVertexColorAlwaysGammaSpace;

    float4    _FillColor;
    float     _FillColorMode;
    sampler2D _FillGradientTex;
    float     _FillGradientAngle;
    float     _FillGradientZoom;
    float     _FillGradientReverse;

    float4    _OutlineColor;
    float     _OutlineColorMode;
    sampler2D _OutlineGradientTex;
    float     _OutlineGradientAngle;
    float     _OutlineGradientZoom;
    float     _OutlineGradientReverse;

    float4    _InnerShadowColor;
    float     _InnerShadowColorMode;
    sampler2D _InnerShadowGradientTex;
    float     _InnerShadowGradientAngle;
    float     _InnerShadowGradientZoom;
    float     _InnerShadowGradientReverse;

    float4    _OuterShadowColor;
    float     _OuterShadowColorMode;
    sampler2D _OuterShadowGradientTex;
    float     _OuterShadowGradientAngle;
    float     _OuterShadowGradientZoom;
    float     _OuterShadowGradientReverse;

    struct appdata
    {
        float4 vertex  : POSITION;
        float4 color   : COLOR;
        float4 uv0     : TEXCOORD0;
        float4 uv1     : TEXCOORD1;
        float4 uv2     : TEXCOORD2;
        float4 uv3     : TEXCOORD3;
        float4 tangent : TANGENT; 
        float3 normal  : NORMAL;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float4 vertex   : SV_POSITION;
        float4 color    : COLOR;
        float2 sdfCoord : TEXCOORD0;
        float4 rectData : TEXCOORD1;
        float4 fxData   : TEXCOORD2;
        float4 radii    : TEXCOORD3;
        float4 outerShd : TEXCOORD4;
        float4 worldPos : TEXCOORD5;
        float3 innerShd : TEXCOORD6;
        float2 spriteUV : TEXCOORD7;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    // Engine Polynomial Sync
    inline float3 UI_GammaToLinear(float3 c)
    {
        float3 linearPath = c * 0.084971 - 0.000163;
        float3 gammaPath  = c * (c * (c * 0.265885 + 0.736584) - 0.009802) + 0.003197;
        float3 cmp = step(0.072549, c);
        return lerp(linearPath, gammaPath, cmp);
    }

    inline float4 UI_ColorSpaceSync(float4 c)
    {
    #if !defined(UNITY_COLORSPACE_GAMMA)
        if (_UIVertexColorAlwaysGammaSpace > 0.5) {
            c.rgb = UI_GammaToLinear(c.rgb);
        }
    #endif
        return c;
    }

    // Straight Alpha Blending Helper
    float4 AlphaBlend(float4 top, float4 bottom)
    {
        float outA = top.a + bottom.a * (1.0 - top.a);
        if (outA < 0.0001) return float4(0.0, 0.0, 0.0, 0.0);
        float3 outRGB = (top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a)) / outA;
        return float4(outRGB, outA);
    }

    // Gradient helper
    float4 SampleGradient(sampler2D tex, float2 sdfCoord, float2 halfSize, float angleDeg, float zoom, float reverse)
    {
        float2 normUV   = sdfCoord / max(halfSize, 0.0001); // [-1, 1]
        float  angleRad = angleDeg * (3.14159265 / 180.0);
        float  t        = dot(normUV, float2(cos(angleRad), sin(angleRad)));
        t = t * 0.5 / max(zoom, 0.01) + 0.5;
        t = saturate(t);
        if (reverse > 0.5) t = 1.0 - t;
        return tex2D(tex, float2(t, 0.5));
    }

    // Mathematical Squircle / Rounded Box SDF mapping
    float sdRoundedBoxEx(float2 p, float2 b, float4 r, float isSq) {
        r.xy = (p.x>0.0)?r.xy : r.zw;
        r.x  = (p.y>0.0)?r.x  : r.y;
        float2 q = abs(p)-b+r.x;
        
        float2 mq = max(q, 0.0);
        
        // Standard Euclidean Length
        float lNormal = length(mq);
        
        // Fast mathematical squircle length approximation
        float sqX = mq.x * mq.x;
        float sqY = mq.y * mq.y;
        float lSq = sqrt(sqrt(sqX * sqX + sqY * sqY));
        
        // Lerp based on squircle activation flag
        float l = lerp(lNormal, lSq, isSq);
        
        return min(max(q.x,q.y),0.0) + l - r.x;
    }

    // Vertex
    v2f vert(appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.vertex    = UnityObjectToClipPos(v.vertex);
        o.worldPos  = v.vertex;
        
        float4 vcolor = UI_ColorSpaceSync(v.color);
        o.color     = vcolor * _Color; 
        
        o.sdfCoord  = v.uv0.xy;
        o.spriteUV  = v.uv0.zw;
        
        o.rectData  = v.uv1;
        o.fxData    = v.uv2;
        o.radii     = v.uv3;
        o.outerShd  = v.tangent; 
        o.innerShd  = v.normal;
        return o;
    }

    // Fragment
    float4 frag(v2f i) : SV_Target
    {
        float2 rectSize       = i.rectData.xy;
        float  softness       = i.rectData.z;
        
        // Unpack properties (Floor ensures floating point consistency across rendering pipeline limits)
        float  centerAndSq    = floor(i.rectData.w + 0.5);
        float  doFill         = fmod(centerAndSq, 2.0);
        float  isSquircle     = floor(centerAndSq / 2.0);
        
        float  outlineWidth   = i.fxData.x;
        float  innerSoftness  = i.fxData.y;
        float  fillAmount     = i.fxData.z;
        float  fillPacked     = i.fxData.w;

        float2 outerOffset    = i.outerShd.xy;
        float  outerSize      = i.outerShd.z;
        float  outerSoftness  = i.outerShd.w;

        float2 innerOffset    = i.innerShd.xy;
        float  innerSize      = i.innerShd.z;

        float2 halfSize = rectSize * 0.5;
        float4 r        = min(i.radii, min(halfSize.x, halfSize.y));
        
        float dist = sdRoundedBoxEx(i.sdfCoord, halfSize, r, isSquircle);
        float aa = fwidth(dist) * 0.5 + softness;
        float shapeMask = 1.0 - smoothstep(-aa, aa, dist);

        // Fill Base color mapping
        float4 fillCol = float4(1.0, 1.0, 1.0, 1.0);
        if (_FillColorMode < 0.5) {
            fillCol.rgb = i.color.rgb;
        } else if (_FillColorMode < 1.5) {
            float4 customCol = UI_ColorSpaceSync(_FillColor);
            fillCol.rgb = customCol.rgb;
            fillCol.a = customCol.a;
        } else {
            float4 grad = SampleGradient(_FillGradientTex, i.sdfCoord, halfSize, _FillGradientAngle, _FillGradientZoom, _FillGradientReverse);
            grad = UI_ColorSpaceSync(grad);
            fillCol.rgb = grad.rgb;
            fillCol.a = grad.a;
        }
        fillCol.a *= doFill;

        // Overlay with MainTex (Sprite Map). Default to a 1x1 white texture if unassigned.
        float4 spriteCol = tex2D(_MainTex, i.spriteUV);
        fillCol *= spriteCol;

        // Inner Shadow
        float4 inShdCol = float4(1.0, 1.0, 1.0, 1.0);
        float innerShadowMask = 0.0;

        if (innerSize != 0.0 || innerSoftness > 0.001 || dot(innerOffset, innerOffset) > 0.001)
        {
            float inDist = sdRoundedBoxEx(i.sdfCoord - innerOffset, halfSize, r, isSquircle);
            float inAA = max(fwidth(inDist) * 0.5, innerSoftness);
            innerShadowMask = smoothstep(-inAA, inAA, inDist + innerSize);

            if (_InnerShadowColorMode < 0.5) {
                inShdCol.rgb = i.color.rgb;
            } else if (_InnerShadowColorMode < 1.5) {
                float4 customCol = UI_ColorSpaceSync(_InnerShadowColor);
                inShdCol.rgb = customCol.rgb;
                inShdCol.a = customCol.a;
            } else {
                float4 grad = SampleGradient(_InnerShadowGradientTex, i.sdfCoord, halfSize, _InnerShadowGradientAngle, _InnerShadowGradientZoom, _InnerShadowGradientReverse);
                grad = UI_ColorSpaceSync(grad);
                inShdCol.rgb = grad.rgb;
                inShdCol.a = grad.a;
            }
            
            inShdCol.a *= innerShadowMask;
            fillCol = AlphaBlend(inShdCol, fillCol);
        }

        // Outline
        float4 shapeCol = fillCol;
        if (outlineWidth > 0.001)
        {
            float4 outCol = float4(1.0, 1.0, 1.0, 1.0);

            if (_OutlineColorMode < 0.5) {
                outCol.rgb = i.color.rgb;
            } else if (_OutlineColorMode < 1.5) {
                float4 customCol = UI_ColorSpaceSync(_OutlineColor);
                outCol.rgb = customCol.rgb;
                outCol.a = customCol.a;
            } else {
                float4 grad = SampleGradient(_OutlineGradientTex, i.sdfCoord, halfSize, _OutlineGradientAngle, _OutlineGradientZoom, _OutlineGradientReverse);
                grad = UI_ColorSpaceSync(grad);
                outCol.rgb = grad.rgb;
                outCol.a = grad.a;
            }
            
            float2 halfSizeInner = max(halfSize - outlineWidth, 0.0);
            float4 rInner        = max(r - outlineWidth, 0.0);
            float  innerDist     = sdRoundedBoxEx(i.sdfCoord, halfSizeInner, rInner, isSquircle);
            
            float innerAA = fwidth(innerDist) * 0.5 + softness;
            float innerMask = smoothstep(-innerAA, innerAA, innerDist);
            
            outCol.a *= innerMask;

            shapeCol = AlphaBlend(outCol, shapeCol);
        }

        shapeCol.a *= shapeMask;

        // Outer Shadow
        float4 outShdCol = float4(1.0, 1.0, 1.0, 1.0);
        float outerShadowMask = 0.0;

        if (outerSize != 0.0 || outerSoftness > 0.001 || dot(outerOffset, outerOffset) > 0.001)
        {
            float2 shadowCoord = i.sdfCoord - outerOffset;
            float2 shdHalfSize = max(halfSize + outerSize, 0.0);
            float4 shdRadii    = max(r + outerSize, 0.0);
            float  shdDist     = sdRoundedBoxEx(shadowCoord, shdHalfSize, shdRadii, isSquircle);
            
            float shdAA = max(fwidth(shdDist) * 0.5, outerSoftness);
            outerShadowMask = 1.0 - smoothstep(-shdAA, shdAA, shdDist);

            if (_OuterShadowColorMode < 0.5) {
                outShdCol.rgb = i.color.rgb;
            } else if (_OuterShadowColorMode < 1.5) {
                float4 customCol = UI_ColorSpaceSync(_OuterShadowColor);
                outShdCol.rgb = customCol.rgb;
                outShdCol.a = customCol.a;
            } else {
                float4 grad = SampleGradient(_OuterShadowGradientTex, i.sdfCoord, halfSize, _OuterShadowGradientAngle, _OuterShadowGradientZoom, _OuterShadowGradientReverse);
                grad = UI_ColorSpaceSync(grad);
                outShdCol.rgb = grad.rgb;
                outShdCol.a = grad.a;
            }
            
            outShdCol.a *= outerShadowMask;
        }
        else
        {
            outShdCol = float4(0, 0, 0, 0);
        }

        // Final Composite
        float4 col = AlphaBlend(shapeCol, outShdCol);

        float clipMask = computeFillMask(i.sdfCoord, halfSize, fillAmount, fillPacked);
        col.a *= clipMask;

        // Apply Master Tint Alpha
        col.a *= i.color.a;
        
        // Apply Soft Mask Alpha
        col.a *= SoftMask_GetAlpha(i.worldPos);

        #ifdef UNITY_UI_CLIP_RECT
        col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
        #endif

        col.rgb *= col.a;

        #ifdef UNITY_UI_ALPHACLIP
        clip(col.a - 0.001);
        #endif

        return col;
    }

    ENDCG

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #pragma multi_compile_local _ SOFTMASK_SLICED SOFTMASK_PROCEDURAL
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            ENDCG
        }
    }
}