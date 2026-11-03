Shader "Serenity/World Map Vegetation"
{
    Properties
    {
        _LeafTex("Leaves", 2D) = "white" {}
        _TunkTex("Bark", 2D) = "white" {}
        _LeafBaseColour("Leaf tint", Color) = (0,0,0,0)
        _TrunkBaseColour("Bark tint", Color) = (0,0,0,0)
        _LeafNoiseColour("Leaf variation", Color) = (0,0,0,0)
        _TrunkNoiseColour("Bark variation", Color) = (0,0,0,0)
        _TotalWindAmount("Wind", Range(0,1)) = 0.2
        _AlphaCutoff("Cutoff", Range(0,1)) = 0.5
        _AtlasMode("Atlas material", Float) = 0
        _Flowering("Deciduous seasonal crown", Float) = 1
        _BiomeStrength("Regional foliage", Range(0,1)) = 0.68
        _ForceFoliage("Ground foliage without tree vertex mask", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "WorldMapBiomePalette.hlsl"
        TEXTURE2D(_LeafTex); SAMPLER(sampler_LeafTex);
        TEXTURE2D(_TunkTex); SAMPLER(sampler_TunkTex);
        CBUFFER_START(UnityPerMaterial)
        float4 _LeafTex_ST, _TunkTex_ST;
        half4 _LeafBaseColour, _TrunkBaseColour, _LeafNoiseColour, _TrunkNoiseColour;
        float _TotalWindAmount, _AlphaCutoff, _AtlasMode, _Flowering, _BiomeStrength, _ForceFoliage;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 world : TEXCOORD1;
            half3 normal : TEXCOORD2;
            half2 leafFog : TEXCOORD3;
            half4 biome : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings BuildVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            float3 world = TransformObjectToWorld(input.positionOS.xyz);
            // One coherent vertex sway replaces the vendor's repeated fragment noise/texture wind.
            world.xz += sin(_Time.y * 1.3 + world.x * .17 + world.z * .12) *
                input.color.g * _TotalWindAmount * .08;
            output.world = world;
            output.normal = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = TransformWorldToHClip(world);
            output.uv = input.uv;
            output.leafFog = half2(max(step(.5h, input.color.b), _ForceFoliage), ComputeFogFactor(output.positionCS.z));
            output.biome = WorldMapFoliage(world.xz, _Flowering);
            return output;
        }
        half4 Surface(Varyings input)
        {
            half4 tex = input.leafFog.x > .5h || _AtlasMode > .5
                ? SAMPLE_TEXTURE2D(_LeafTex, sampler_LeafTex, input.uv * _LeafTex_ST.xy + _LeafTex_ST.zw)
                : SAMPLE_TEXTURE2D(_TunkTex, sampler_TunkTex, input.uv * _TunkTex_ST.xy + _TunkTex_ST.zw);
            clip(tex.a - _AlphaCutoff);
            half3 tint = input.leafFog.x > .5h
                ? lerp(_LeafBaseColour.rgb, _LeafNoiseColour.rgb, .35h)
                : lerp(_TrunkBaseColour.rgb, _TrunkNoiseColour.rgb, .35h);
            half3 original = max(tex.rgb, tint);
            half foliage = _AtlasMode > .5 ? step(tex.r * 1.08, tex.g) * step(tex.b * 1.1, tex.g) : input.leafFog.x;
            half luminance = dot(original, half3(.22,.7,.08));
            half3 regional = input.biome.rgb * lerp(.65, 1.3, saturate(luminance * 1.8));
            half strength = max(_BiomeStrength, input.biome.a * .92) * foliage * _WorldMapArtEnabled;
            return half4(lerp(original, regional, strength), 1);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BuildVertex
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = Surface(input).rgb;
                half3 normal = normalize(input.normal);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.world));
                half diffuse = saturate(dot(normal, light.direction)) * .7h + .3h;
                half3 lighting = max(SampleSH(normal), .12h) + light.color * diffuse * light.shadowAttenuation;
                return half4(MixFog(albedo * lighting, input.leafFog.y), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = BuildVertex(input);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.world, output.normal, _LightDirection));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); Surface(input); return 0; }
            ENDHLSL
        }
    }
}
