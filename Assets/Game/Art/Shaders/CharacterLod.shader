Shader "Serenity/CharacterLod"
{
    Properties { _SkinColor("Skin", Color) = (0.6,0.4,0.25,1) _HairColor("Hair", Color) = (0.1,0.05,0.02,1) _ClothingColor("Clothing", Color) = (0.4,0.22,0.08,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float4 color:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SkinColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _HairColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ClothingColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 skin = UNITY_ACCESS_INSTANCED_PROP(Props, _SkinColor).rgb;
                half3 hair = UNITY_ACCESS_INSTANCED_PROP(Props, _HairColor).rgb;
                half3 clothing = UNITY_ACCESS_INSTANCED_PROP(Props, _ClothingColor).rgb;
                half3 baseColor = skin * input.color.r + hair * input.color.g + clothing * input.color.b + half3(0.18,0.12,0.07) * input.color.a;
                Light light = GetMainLight();
                half ndl = saturate(dot(normalize(input.normalWS), light.direction));
                half3 lit = baseColor * (SampleSH(input.normalWS) + light.color * (0.35h + 0.65h * ndl));
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
