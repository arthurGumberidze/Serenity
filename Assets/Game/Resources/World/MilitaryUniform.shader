Shader "Serenity/MilitaryUniform"
{
 Properties
 {
  _Texture0("Face masks",2D)="white"{} _Texture1("Face details",2D)="white"{}
  _Texture2("Authored shading",2D)="white"{} _Texture3("Leather masks",2D)="white"{}
  _Texture4("Feather masks",2D)="white"{} _Texture5("Cloth masks",2D)="white"{}
  _Texture6("Metal masks",2D)="white"{} _Texture7("Gem masks",2D)="white"{}
  _SKINCOLOR("Skin",Color)=(.64,.40,.27,1) _HAIRCOLOR("Hair",Color)=(.12,.065,.035,1)
  _EYESCOLOR("Eyes",Color)=(.08,.12,.16,1) _SCLERACOLOR("Sclera",Color)=(.9,.85,.8,1)
  _LIPSCOLOR("Lips",Color)=(.45,.15,.12,1) _SCARSCOLOR("Scars",Color)=(.5,.25,.2,1)
  _METAL1COLOR("Primary metal",Color)=(.4,.4,.4,1) _METAL2COLOR("Metal trim",Color)=(.6,.6,.6,1) _METAL3COLOR("Dark metal",Color)=(.2,.2,.2,1)
  _LEATHER1COLOR("Leather 1",Color)=(.25,.12,.045,1) _LEATHER2COLOR("Leather 2",Color)=(.42,.24,.1,1) _LEATHER3COLOR("Leather 3",Color)=(.13,.07,.025,1)
  _CLOTH1COLOR("Cloth 1",Color)=(.1,.2,.3,1) _CLOTH2COLOR("Cloth 2",Color)=(.2,.3,.4,1) _CLOTH3COLOR("Cloth 3",Color)=(.1,.1,.1,1)
  _BaseMap("Shadow opacity",2D)="white"{} _BaseColor("Shadow tint",Color)=(1,1,1,1) _Cutoff("Cutoff",Float)=.5
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_Texture0);TEXTURE2D(_Texture1);TEXTURE2D(_Texture2);TEXTURE2D(_Texture3);
   TEXTURE2D(_Texture4);TEXTURE2D(_Texture5);TEXTURE2D(_Texture6);TEXTURE2D(_Texture7);SAMPLER(sampler_Texture0);
   CBUFFER_START(UnityPerMaterial)
   half4 _SKINCOLOR,_HAIRCOLOR,_EYESCOLOR,_SCLERACOLOR,_LIPSCOLOR,_SCARSCOLOR;
   half4 _METAL1COLOR,_METAL2COLOR,_METAL3COLOR,_LEATHER1COLOR,_LEATHER2COLOR,_LEATHER3COLOR;
   half4 _CLOTH1COLOR,_CLOTH2COLOR,_CLOTH3COLOR;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
   Varyings Vert(Attributes v){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half3 ApplyMask(half3 base,half3 mask,half3 a,half3 b,half3 c)
   {
    // The supplied Polytope RGB masks encode each region as black in its own channel.
    half3 w=1-smoothstep(.04,.075,mask);
    base=lerp(base,c,w.b);base=lerp(base,b,w.g);return lerp(base,a,w.r);
   }
   half4 Frag(Varyings i):SV_Target
   {
    half3 colour=half3(.24,.22,.20);
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture7,sampler_Texture0,i.uv).rgb,half3(.6,.06,.1),half3(.2,.1,.5),half3(.05,.35,.15));
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture4,sampler_Texture0,i.uv).rgb,_CLOTH1COLOR.rgb,_CLOTH2COLOR.rgb,_CLOTH3COLOR.rgb);
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture5,sampler_Texture0,i.uv).rgb,_CLOTH1COLOR.rgb,_CLOTH2COLOR.rgb,_CLOTH3COLOR.rgb);
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture3,sampler_Texture0,i.uv).rgb,_LEATHER1COLOR.rgb,_LEATHER2COLOR.rgb,_LEATHER3COLOR.rgb);
    half3 metalMask=SAMPLE_TEXTURE2D(_Texture6,sampler_Texture0,i.uv).rgb;
    colour=ApplyMask(colour,metalMask,_METAL1COLOR.rgb,_METAL2COLOR.rgb,_METAL3COLOR.rgb);
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture1,sampler_Texture0,i.uv).rgb,_SCLERACOLOR.rgb,_LIPSCOLOR.rgb,_SCARSCOLOR.rgb);
    colour=ApplyMask(colour,SAMPLE_TEXTURE2D(_Texture0,sampler_Texture0,i.uv).rgb,_SKINCOLOR.rgb,_HAIRCOLOR.rgb,_EYESCOLOR.rgb);
    colour*=SAMPLE_TEXTURE2D(_Texture2,sampler_Texture0,i.uv).rgb;
    half3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 illumination=max(SampleSH(n),half3(.14,.14,.14))+light.color*saturate(dot(n,light.direction))*light.shadowAttenuation;
    half metal=1-smoothstep(.04,.075,min(metalMask.r,min(metalMask.g,metalMask.b)));
    half spec=pow(saturate(dot(n,normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS)))),40)*metal*.35*light.shadowAttenuation;
    return half4(MixFog(colour*illumination+light.color*spec,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
