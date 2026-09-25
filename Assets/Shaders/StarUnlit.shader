Shader "CornMaze/StarUnlit"
{
    Properties
    {
        _MainTex ("Tex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Twinkle ("Twinkle", Float) = 1
        _Visibility ("Visibility", Float) = 1
        // M25b: blend state used to be hard-coded to SrcAlpha/One (additive). The defaults below ARE that
        // pair, so every existing user — stars, the band, the torn clouds — is pixel-identical; the moon
        // (a photograph, which has to composite rather than add) is the only material that overrides them.
        _SrcBlend ("Src Blend", Float) = 5     // SrcAlpha
        _DstBlend ("Dst Blend", Float) = 1     // One
    }

    // URP path (project uses Universal Render Pipeline)
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "StarUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Twinkle;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float phase = v.positionOS.x * 0.37 + v.positionOS.z * 0.29 + v.positionOS.y * 0.13;
                float tw = 0.86 + 0.14 * sin(_Time.y * (1.7 + frac(phase) * 1.4) + phase);
                tw = lerp(1.0, tw, saturate(_Twinkle));
                o.color = v.color * _Color;
                o.color.a *= tw * saturate(_Visibility);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 c = t * i.color;
                // Soft core boost so small on-screen stars still read on retina.
                c.rgb *= 1.15 + 0.55 * t.a;
                return c;
            }
            ENDHLSL
        }
    }

    // Built-in fallback if URP package path is unavailable in some tooling
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest LEqual
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _Twinkle;
            float _Visibility;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float phase = v.vertex.x * 0.37 + v.vertex.z * 0.29 + v.vertex.y * 0.13;
                float tw = 0.86 + 0.14 * sin(_Time.y * (1.7 + frac(phase) * 1.4) + phase);
                tw = lerp(1.0, tw, saturate(_Twinkle));
                o.color = v.color * _Color;
                o.color.a *= tw * saturate(_Visibility);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                fixed4 c = t * i.color;
                c.rgb *= 1.15 + 0.55 * t.a;
                return c;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
