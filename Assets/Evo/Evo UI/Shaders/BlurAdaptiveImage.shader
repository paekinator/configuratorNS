Shader "Evo/UI/Adaptive Image Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // Custom Properties
        _BackgroundTex ("Background Texture (Generated)", 2D) = "white" {}    
        _Exposure ("Exposure", Range(0, 5)) = 1.0
        _Saturation ("Saturation", Range(0, 5)) = 1.2
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.03
        
        // Viewport Mapping
        _BgViewportRect ("Bg Viewport Rect", Vector) = (0,0,1,1)
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        
        // Mode: 0 = Screen Space, 1 = Local
        _LocalSpace ("Local Space", Float) = 0 
        
        // Soft Mask Parameters
        _SoftMaskTex ("Soft Mask Texture", 2D) = "white" {}
        _SoftMask_Mode ("Mask Mode", Float) = 0
        _SoftMask_Rect ("Soft Mask Rect", Vector) = (0,0,0,0)
        
        _SoftMask_PR_Center ("PR Center", Vector) = (0,0,0,0)
        _SoftMask_PR_HalfSize ("PR Half Size", Vector) = (0,0,0,0)
        _SoftMask_PR_Radii ("PR Radii", Vector) = (0,0,0,0)
        _SoftMask_PR_Softness ("PR Softness", Float) = 0
        _SoftMask_PR_FillData ("PR Fill Data", Vector) = (0,0,0,0)

        _SoftMask_BorderData ("Border Data", Vector) = (0,0,0,0)
        _SoftMask_UVOuter ("UV Outer", Vector) = (0,0,1,1)
        _SoftMask_UVInner ("UV Inner", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "SoftMask.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ SOFTMASK_SLICED SOFTMASK_PROCEDURAL

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float4 canvasPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            // Custom
            sampler2D _BackgroundTex;
            float _Exposure;
            float _Saturation;
            float _NoiseStrength;
            
            float4 _BgViewportRect;
            float4 _SpriteUVRect; 
            float _LocalSpace;

            // Engine Polynomial Sync
            float _UIVertexColorAlwaysGammaSpace;

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

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.screenPosition = ComputeScreenPos(OUT.vertex);
                OUT.canvasPos = v.vertex;
                
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = UI_ColorSpaceSync(v.color) * _Color;
                return OUT;
            }

            float nrand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 viewportUV = IN.screenPosition.xy / IN.screenPosition.w;
                float2 normalizedPos;

                if (_LocalSpace > 0.5) { normalizedPos = IN.texcoord; }
                else { normalizedPos = (viewportUV - _BgViewportRect.xy) / _BgViewportRect.zw; }
                
                float edgeFade = 1.0;
                if (normalizedPos.x < 0 || normalizedPos.x > 1 || normalizedPos.y < 0 || normalizedPos.y > 1) 
                    edgeFade = 0;

                float2 bgUV = float2(
                    _SpriteUVRect.x + (normalizedPos.x * _SpriteUVRect.z),
                    _SpriteUVRect.y + (normalizedPos.y * _SpriteUVRect.w)
                );

                // Fetch Blur
                half3 blurRGB = tex2D(_BackgroundTex, bgUV).rgb;
                
                // Adjustments
                blurRGB *= _Exposure;
                half luminance = dot(blurRGB, float3(0.2126, 0.7152, 0.0722));
                blurRGB = lerp(float3(luminance, luminance, luminance), blurRGB, _Saturation);
                blurRGB += (nrand(viewportUV * 100.0) - 0.5) * _NoiseStrength;

                // Fetch the Soft Mask Alpha in a single line!
                half maskAlpha = SoftMask_GetAlpha(IN.canvasPos);

                // Final Alpha
                half sourceAlpha = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd).a;
                half finalAlpha = sourceAlpha * IN.color.a * edgeFade * maskAlpha;

                // Output
                half4 finalCol = half4(blurRGB * IN.color.rgb, finalAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                finalCol.a *= UnityGet2DClipping(IN.screenPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (finalCol.a - 0.001);
                #endif

                return finalCol;
            }
            ENDCG
        }
    }
}