Shader "Serenity/StarterWater"
{
    Properties { _Ripples("Synty lake ripples", 2D) = "gray" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WorldMapBiomePalette.hlsl"
            TEXTURE2D(_Ripples); SAMPLER(sampler_Ripples);
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
                // Broad overlapping ripples avoid the regular high-frequency checker at RTS distances.
                float wave = sin(i.world.x * 0.19 + i.world.z * 0.12 + t * 0.45) * 0.6
                    + sin(i.world.z * 0.27 - i.world.x * 0.08 - t * 0.32) * 0.4;
                float2 ripple = SAMPLE_TEXTURE2D(_Ripples, sampler_Ripples, i.world.xz * 0.037 + t * float2(0.003, 0.002)).rg;
                ripple += SAMPLE_TEXTURE2D(_Ripples, sampler_Ripples, i.world.zx * 0.071 - t * float2(0.002, 0.003)).rg;
                float3 normal = normalize(float3((ripple.x - 1) * 0.1, 1, (ripple.y - 1) * 0.1));
                float3 view = GetWorldSpaceNormalizeViewDir(i.world);
                Light light = GetMainLight();
                half3 shallow = lerp(half3(.20,.38,.34), _WorldMapShallowWater.rgb, _WorldMapArtEnabled);
                half3 deep = lerp(half3(.035,.16,.22), _WorldMapDeepWater.rgb, _WorldMapArtEnabled);
                float murk = WorldMapMurk(i.world.xz);
                float opticalDepth = smoothstep(0, .7, i.depth);
                half3 base = lerp(shallow, deep, opticalDepth * .45);
                // A submerged bed pattern remains visible in shallows and fades with optical depth.
                float bed = sin(i.world.x * .64 + sin(i.world.z * .47)) * sin(i.world.z * .71);
                base += bed * .018 * (1 - saturate(i.depth * 2)) * (1 - murk);
                base = lerp(base, _WorldMapMarshWater.rgb * lerp(1.1, .65, i.depth), murk);
                float fresnel = pow(1 - saturate(dot(normal, view)), 4);
                float specular = pow(saturate(dot(normal, normalize(light.direction + view))), 96);
                half3 color = base * (max(SampleSH(normal), .05) + light.color * .6) + wave * 0.009;
                color = lerp(color, half3(0.25, 0.36, 0.39), fresnel * 0.22);
                color += light.color * specular * 0.065;
                float shore = (1 - smoothstep(0.015, 0.12, i.depth)) * (0.04 + 0.04 * wave);
                color += half3(0.65, 0.72, 0.58) * shore;
                float opacity = lerp(.82, .96, opticalDepth);
                opacity = lerp(opacity, .97, murk);
                return half4(MixFog(color, i.fog), opacity);
            }
            ENDHLSL
        }
    }
}
