Shader "Serenity/StarterWater"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float depth : TEXCOORD1; float fog : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.depth = v.color.r;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float wave = sin(i.world.x * 1.7 + i.world.z * 0.8 + t * 0.7)
                    * sin(i.world.z * 2.1 - i.world.x * 0.4 - t * 0.5);
                float3 normal = normalize(float3(wave * 0.07, 1, cos(i.world.x + i.world.z - t * 0.6) * 0.06));
                float3 view = GetWorldSpaceNormalizeViewDir(i.world);
                Light light = GetMainLight();
                half3 base = lerp(half3(0.24, 0.37, 0.30), half3(0.055, 0.20, 0.22), saturate(i.depth));
                float fresnel = pow(1 - saturate(dot(normal, view)), 4);
                float specular = pow(saturate(dot(normal, normalize(light.direction + view))), 96);
                half3 color = base * (0.65 + light.color * 0.35) + wave * 0.009;
                color = lerp(color, half3(0.38, 0.49, 0.51), fresnel * 0.45);
                color += light.color * specular * 0.3;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
