Shader "Evo/UI/Warning Stripe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        
        _LineWidth ("Line Width", Range(0.01, 0.5)) = 0.1
        _LineSpacing ("Line Spacing", Range(0.05, 1)) = 0.2
        _LineAngle ("Line Angle", Range(-180, 180)) = 45
        _ScrollSpeed ("Scroll Speed", Range(-5, 5)) = 1
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma target 2.0
            
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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                // Pass the ALREADY rotated position to the fragment shader
                float2 rotatedPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float _LineWidth;
            float _LineSpacing;
            float _LineAngle;
            float _ScrollSpeed;

            static const float DEG_TO_RAD = 0.01745329251;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;

                // Calculate rotation math here (Per Vertex) instead of in the Fragment Shader (Per Pixel)
                float2 objectPos = v.vertex.xy * 0.01; // Scale to manageable numbers
                float angleRad = _LineAngle * DEG_TO_RAD;
                float c = cos(angleRad);
                float s = sin(angleRad);
                
                // Rotate the position
                OUT.rotatedPos.x = objectPos.x * c - objectPos.y * s;
                OUT.rotatedPos.y = objectPos.x * s + objectPos.y * c;

                return OUT;
            }
            
            fixed4 frag(v2f IN) : SV_Target
            {
                // Standard UI Color (Texture * Vertex Color)
                half4 texColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd);
                fixed4 color = texColor * IN.color;

                // Add scrolling to the pre-calculated rotated position
                float scrolledPos = IN.rotatedPos.x + _Time.y * _ScrollSpeed;
                
                float repeatPeriod = _LineWidth + _LineSpacing;
                
                // Centering logic for the stripes
                float lineCenter = floor(scrolledPos / repeatPeriod) * repeatPeriod + repeatPeriod * 0.5;
                float distanceFromCenter = abs(scrolledPos - lineCenter);
                
                // Anti-aliasing
                // fwidth gives us the rate of change of the position across pixels, perfect for AA edge width
                float edgeWidth = fwidth(scrolledPos) * 2.0; 
                float halfWidth = _LineWidth * 0.5;
                
                // 1.0 = opaque line, 0.0 = transparent gap
                float lineMask = smoothstep(halfWidth + edgeWidth, halfWidth, distanceFromCenter);

                // Apply mask to alpha
                color.a *= lineMask;
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                
                return color;
            }
            ENDCG
        }
    }
}