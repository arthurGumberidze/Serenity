Shader "Serenity/SnowProps"
{
 Properties {
  _BaseColor("Base",Color)=(.24,.3,.32,1)
  _SnowColor("Snow",Color)=(.88,.95,1,1)
  _Tree("Tree",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor, _SnowColor;
   float _Tree;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float height:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
   Varyings Vert(Attributes v) {
    Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o);
    VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS=p.positionCS; o.positionWS=p.positionWS;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.height=v.positionOS.y;
    o.fog=ComputeFogFactor(p.positionCS.z); return o;
   }
   half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    half3 normal=normalize(i.normalWS);
    float grain=sin(i.positionWS.x*5.3)*sin(i.positionWS.z*4.7)*.05;
    float snow=smoothstep(.15,.65,normal.y+grain);
    half3 baseColor=_BaseColor.rgb;
    if(_Tree>.5) {baseColor=lerp(half3(.22,.13,.075),half3(.10,.24,.20),saturate(i.height*.8));snow*=saturate(i.height*.6);}
    half3 albedo=lerp(baseColor,_SnowColor.rgb,snow);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 lighting=SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.shadowAttenuation;
    return half4(MixFog(albedo*lighting,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
