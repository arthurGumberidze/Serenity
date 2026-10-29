Shader "Serenity/DeformableSnow"
{
    Properties { _BaseMap("Imported snow",2D)="white"{} _NormalMap("Imported snow normal",2D)="bump"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+8" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
            struct A { float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR; };
            struct V { float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float packed:TEXCOORD2;float fog:TEXCOORD3; };
            V vert(A a) { V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.packed=a.c.r;o.fog=ComputeFogFactor(o.p.z);return o; }
            half4 frag(V i):SV_Target
            {
                half3 detail=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.w.xz/5).rgb;
                half3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.w.xz/3));
                half3 n=normalize(i.n+float3(normal.x,0,normal.y)*.18);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.w));
                half3 base=lerp(half3(.80,.85,.91),half3(.32,.45,.57),i.packed*.68)*lerp(.88,1.02,detail.r);
                half diffuse=saturate(dot(n,light.direction));
                half3 col=base*(half3(.23,.25,.29)+saturate(light.color)*diffuse*.72*lerp(.45,1,light.shadowAttenuation));
                half sparkle=pow(saturate(dot(n,normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.w)))),64)*.04;
                return half4(MixFog(col+sparkle,i.fog),1);
            }
            ENDHLSL
        }
    }
}
