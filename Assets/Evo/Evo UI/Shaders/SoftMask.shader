Shader "Hidden/Evo/UI/Soft Mask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

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
                float4 vertex    : SV_POSITION;
                float4 color     : COLOR; 
                float2 texcoord  : TEXCOORD0;
                float4 canvasPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _Color;
            float4 _TextureSampleAdd;
            float4 _ClipRect;

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
                
                OUT.canvasPos = v.vertex; 
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                
                OUT.texcoord = v.texcoord;
                OUT.color = UI_ColorSpaceSync(v.color) * _Color;
                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // Soft Mask Rendering Pass
                color.a *= SoftMask_GetAlpha(IN.canvasPos);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.canvasPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}