Shader "Serenity/World Map Horizon"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; half3 normalOS:NORMAL; half4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; half3 normal:TEXCOORD0; half4 color:COLOR; float fog:TEXCOORD1; };
            V Vert(A input)
            {
                V o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.normal=TransformObjectToWorldNormal(input.normalOS); o.color=input.color;
                o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(V i):SV_Target
            {
                Light light=GetMainLight(); half3 normal=normalize(i.normal);
                half3 color=i.color.rgb*(max(SampleSH(normal),.06)+light.color*(saturate(dot(normal,light.direction))*.7+.15));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
