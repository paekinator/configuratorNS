Shader "Hidden/Evo/UI/DualKawaseBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Offset", Float) = 1.0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
    };

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float _Offset;

    v2f vert(appdata v)
    {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    // Downsample Pass (5 Taps)
    half4 frag_downsample(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 halfPixel = _MainTex_TexelSize.xy * 0.5;

        half4 sum = tex2D(_MainTex, uv) * 4.0;
        sum += tex2D(_MainTex, uv - halfPixel.xy * _Offset);
        sum += tex2D(_MainTex, uv + halfPixel.xy * _Offset);
        sum += tex2D(_MainTex, uv + float2(halfPixel.x, -halfPixel.y) * _Offset);
        sum += tex2D(_MainTex, uv - float2(halfPixel.x, -halfPixel.y) * _Offset);

        return sum * 0.125;
    }

    // Upsample Pass (8 Taps)
    half4 frag_upsample(v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 halfPixel = _MainTex_TexelSize.xy * 0.5;
        
        half4 sum = tex2D(_MainTex, uv + float2(-halfPixel.x * 2.0, 0.0) * _Offset);
        sum += tex2D(_MainTex, uv + float2(-halfPixel.x, halfPixel.y) * _Offset) * 2.0;
        sum += tex2D(_MainTex, uv + float2(0.0, halfPixel.y * 2.0) * _Offset);
        sum += tex2D(_MainTex, uv + float2(halfPixel.x, halfPixel.y) * _Offset) * 2.0;
        sum += tex2D(_MainTex, uv + float2(halfPixel.x * 2.0, 0.0) * _Offset);
        sum += tex2D(_MainTex, uv + float2(halfPixel.x, -halfPixel.y) * _Offset) * 2.0;
        sum += tex2D(_MainTex, uv + float2(0.0, -halfPixel.y * 2.0) * _Offset);
        sum += tex2D(_MainTex, uv + float2(-halfPixel.x, -halfPixel.y) * _Offset) * 2.0;

        return sum * 0.0833;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_downsample
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_upsample
            ENDCG
        }
    }
}