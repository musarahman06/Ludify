Shader "Custom/VertexColorLit"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                float3 n = normalize(IN.normalWS);
                float ndotl = saturate(dot(n, mainLight.direction)) * 0.6 + 0.4;
                half3 lit = IN.color.rgb * ndotl * mainLight.color;
                half3 ambient = IN.color.rgb * 0.35;
                return half4(lit + ambient, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
