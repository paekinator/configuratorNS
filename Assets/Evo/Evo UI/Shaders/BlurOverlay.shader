Shader "Evo/UI/Blur Overlay"
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
        _Saturation ("Saturation", Range(0, 5)) = 1.0
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.02
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

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.screenPosition = ComputeScreenPos(OUT.vertex);
                
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float nrand(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Use screen-space UV directly - the blur texture is already the full camera view
                float2 screenUV = IN.screenPosition.xy / IN.screenPosition.w;
                
                // Fetch Blur using screen UV
                half3 blurRGB = tex2D(_BackgroundTex, screenUV).rgb;
                
                // Adjustments
                blurRGB *= _Exposure;
                half luminance = dot(blurRGB, float3(0.2126, 0.7152, 0.0722));
                blurRGB = lerp(float3(luminance, luminance, luminance), blurRGB, _Saturation);
                
                // Add subtle noise
                blurRGB += (nrand(screenUV * 100.0) - 0.5) * _NoiseStrength;

                // Final RGB - multiply blur by Image component's RGB color
                half3 finalRGB = blurRGB * IN.color.rgb;

                // Final Alpha - multiply texture alpha by Image component's Alpha
                half maskAlpha = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd).a;
                half finalAlpha = maskAlpha * IN.color.a;

                // Output
                half4 finalCol = half4(finalRGB, finalAlpha);

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