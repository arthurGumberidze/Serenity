using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Disposable presentation profiles over the approved Procedural Water Shader asset.
    /// Hydrology, water levels and gameplay remain owned by the tiled-world domain data.
    /// </summary>
    internal enum WorldWaterProfile
    {
        River = 0,
        Lake = 1,
        MountainLake = 2,
        Marsh = 3,
        Ocean = 4
    }

    internal static class WorldWaterProfileMaterials
    {
        public static Material Create(WorldWaterProfile profile)
        {
            var shader = Shader.Find("FX/ProceduralWater");
            if (shader == null)
                throw new System.InvalidOperationException(
                    "The approved Procedural Water Shader URP asset is not available.");

            var material = new Material(shader)
            {
                name = "WM15 Procedural Water - " + profile,
                enableInstancing = true,
                renderQueue = 3000
            };
            SetCommon(material);
            switch (profile)
            {
                case WorldWaterProfile.River: ConfigureRiver(material); break;
                case WorldWaterProfile.Lake: ConfigureLake(material); break;
                case WorldWaterProfile.MountainLake: ConfigureMountainLake(material); break;
                case WorldWaterProfile.Marsh: ConfigureMarsh(material); break;
                case WorldWaterProfile.Ocean: ConfigureOcean(material); break;
            }
            return material;
        }

        private static void SetCommon(Material value)
        {
            Set(value,"_WorldLightDir",new Vector4(-.32f,.88f,-.35f,0f));
            Set(value,"_FoamColor",new Color(.92f,.97f,1f,1f));
            Set(value,"_FresnelScale",.82f); Set(value,"_FresnelPower",4.5f);
            Set(value,"_FresnelBias",.07f); Set(value,"_EdgeFade",.72f);
            Set(value,"_EdgeFoamWidth",1.2f); Set(value,"_EdgeFoamThreshold",.075f);
            Set(value,"_MinAlpha",.12f); Set(value,"_DepthAlphaBoost",.44f);
            Set(value,"_MarshBaseColor",new Color(.10f,.15f,.045f,.95f));
            Set(value,"_MarshDeepColor",new Color(.014f,.026f,.006f,1f));
            Set(value,"_MarshReflectionColor",new Color(.22f,.27f,.11f,.07f));
            Set(value,"_MarshSpecularColor",new Color(.48f,.52f,.28f,.07f));
            Set(value,"_MarshRefractionStrength",.96f); Set(value,"_MarshAbsorption",3.15f);
            Set(value,"_MarshMinAlpha",.88f); Set(value,"_MarshDepthAlphaBoost",.9f);
        }

        private static void ConfigureRiver(Material value)
        {
            Set(value,"_BaseColor",new Color(.08f,.38f,.42f,.68f));
            Set(value,"_DeepColor",new Color(.018f,.12f,.19f,.94f));
            Set(value,"_ReflectionColor",new Color(.62f,.79f,.86f,.34f));
            Set(value,"_SpecularColor",new Color(.92f,.97f,1f,.48f));
            Set(value,"_Shininess",210f); Set(value,"_RefractionStrength",.82f);
            Set(value,"_DepthMax",4.8f); Set(value,"_Absorption",.9f); Set(value,"_Opacity",.96f);
            Set(value,"_MinAlpha",.32f);
            Set(value,"_RippleScale",2.2f); Set(value,"_RippleSpeed",1.52f); Set(value,"_RippleStrength",.47f);
            Set(value,"_FoamIntensity",.34f); Set(value,"_ShoreDepth",.42f);
            Set(value,"_FoamNoiseScale",2.9f); Set(value,"_FoamNoiseSpeed",1.42f);
            Set(value,"_FoamCrestThreshold",.62f); Set(value,"_FoamCrestIntensity",.28f);
            Set(value,"_EdgeFoamIntensity",.48f); Set(value,"_GerstnerIntensity",.32f);
            Set(value,"_GAmplitude",new Vector4(.045f,.032f,.022f,.015f));
            Set(value,"_GFrequency",new Vector4(1.7f,2.1f,1.3f,.9f));
            Set(value,"_GSteepness",new Vector4(.55f,.48f,.38f,.3f));
            Set(value,"_GSpeed",new Vector4(1.5f,1.25f,.95f,.7f));
            Set(value,"_GDirectionAB",new Vector4(.18f,.98f,.35f,.94f));
            Set(value,"_GDirectionCD",new Vector4(.1f,.99f,.28f,.96f));
        }

        private static void ConfigureLake(Material value)
        {
            Set(value,"_BaseColor",new Color(.055f,.26f,.33f,.92f));
            Set(value,"_DeepColor",new Color(.009f,.06f,.12f,1f));
            Set(value,"_ReflectionColor",new Color(.58f,.72f,.80f,.32f));
            Set(value,"_SpecularColor",new Color(.96f,.98f,1f,.42f));
            // In this shader a higher refraction blend shows more water colour and less scene colour.
            // The previous .43 value therefore made ordinary lakes look more transparent, not less.
            Set(value,"_Shininess",260f); Set(value,"_RefractionStrength",.91f);
            Set(value,"_DepthMax",5.8f); Set(value,"_Absorption",1.58f); Set(value,"_Opacity",1f);
            Set(value,"_MinAlpha",.72f); Set(value,"_DepthAlphaBoost",.95f);
            Set(value,"_RippleScale",1.35f); Set(value,"_RippleSpeed",.48f); Set(value,"_RippleStrength",.39f);
            Set(value,"_FoamIntensity",.17f); Set(value,"_ShoreDepth",.62f);
            Set(value,"_FoamNoiseScale",1.8f); Set(value,"_FoamNoiseSpeed",.38f);
            Set(value,"_FoamCrestThreshold",.76f); Set(value,"_FoamCrestIntensity",.12f);
            Set(value,"_EdgeFoamIntensity",.24f); Set(value,"_GerstnerIntensity",.2f);
            Set(value,"_GAmplitude",new Vector4(.035f,.024f,.018f,.012f));
            Set(value,"_GFrequency",new Vector4(1.1f,1.45f,.82f,.65f));
            Set(value,"_GSteepness",new Vector4(.38f,.32f,.26f,.22f));
            Set(value,"_GSpeed",new Vector4(.52f,.43f,.34f,.28f));
            Set(value,"_GDirectionAB",new Vector4(.72f,.32f,.22f,.92f));
            Set(value,"_GDirectionCD",new Vector4(.46f,.74f,.94f,.16f));
        }

        private static void ConfigureMountainLake(Material value)
        {
            Set(value,"_BaseColor",new Color(.13f,.40f,.51f,.80f));
            Set(value,"_DeepColor",new Color(.018f,.105f,.20f,.98f));
            Set(value,"_ReflectionColor",new Color(.78f,.88f,.96f,.52f));
            Set(value,"_SpecularColor",new Color(1f,1f,1f,.56f));
            Set(value,"_Shininess",340f); Set(value,"_RefractionStrength",.48f);
            Set(value,"_DepthMax",8.2f); Set(value,"_Absorption",.88f); Set(value,"_Opacity",1f);
            Set(value,"_MinAlpha",.52f); Set(value,"_DepthAlphaBoost",.68f);
            Set(value,"_RippleScale",1.65f); Set(value,"_RippleSpeed",.34f); Set(value,"_RippleStrength",.34f);
            Set(value,"_FoamIntensity",.22f); Set(value,"_ShoreDepth",.76f);
            Set(value,"_FoamNoiseScale",2.1f); Set(value,"_FoamNoiseSpeed",.28f);
            Set(value,"_FoamCrestThreshold",.82f); Set(value,"_FoamCrestIntensity",.08f);
            Set(value,"_EdgeFoamIntensity",.32f); Set(value,"_GerstnerIntensity",.14f);
            Set(value,"_GAmplitude",new Vector4(.022f,.016f,.011f,.008f));
            Set(value,"_GFrequency",new Vector4(1.35f,1.8f,1.0f,.72f));
            Set(value,"_GSteepness",new Vector4(.28f,.24f,.2f,.16f));
            Set(value,"_GSpeed",new Vector4(.38f,.31f,.25f,.2f));
            Set(value,"_GDirectionAB",new Vector4(.82f,.18f,.34f,.88f));
            Set(value,"_GDirectionCD",new Vector4(.56f,.62f,.94f,.08f));
        }

        private static void ConfigureMarsh(Material value)
        {
            Set(value,"_BaseColor",new Color(.10f,.15f,.045f,.95f));
            Set(value,"_DeepColor",new Color(.014f,.026f,.006f,1f));
            Set(value,"_ReflectionColor",new Color(.22f,.27f,.11f,.07f));
            Set(value,"_SpecularColor",new Color(.48f,.52f,.28f,.07f));
            Set(value,"_Shininess",95f); Set(value,"_RefractionStrength",.96f);
            Set(value,"_DepthMax",2.7f); Set(value,"_Absorption",3.15f); Set(value,"_Opacity",1f);
            Set(value,"_MinAlpha",.88f); Set(value,"_DepthAlphaBoost",.9f);
            Set(value,"_RippleScale",3.3f); Set(value,"_RippleSpeed",.13f); Set(value,"_RippleStrength",.16f);
            Set(value,"_FoamIntensity",0f); Set(value,"_ShoreDepth",.28f);
            Set(value,"_FoamCrestIntensity",0f); Set(value,"_EdgeFoamIntensity",.05f);
            Set(value,"_GerstnerIntensity",.045f);
            Set(value,"_GAmplitude",new Vector4(.008f,.006f,.004f,.003f));
            Set(value,"_GSpeed",new Vector4(.16f,.12f,.09f,.07f));
        }

        private static void ConfigureOcean(Material value)
        {
            Set(value,"_BaseColor",new Color(.075f,.29f,.37f,.55f));
            Set(value,"_DeepColor",new Color(.012f,.07f,.13f,.94f));
            Set(value,"_ReflectionColor",new Color(.63f,.78f,.88f,.5f));
            Set(value,"_SpecularColor",new Color(.96f,.98f,1f,.52f));
            Set(value,"_Shininess",230f); Set(value,"_RefractionStrength",.48f);
            Set(value,"_DepthMax",14f); Set(value,"_Absorption",1.05f); Set(value,"_Opacity",.9f);
            Set(value,"_RippleScale",1.05f); Set(value,"_RippleSpeed",.75f); Set(value,"_RippleStrength",.48f);
            Set(value,"_FoamIntensity",.55f); Set(value,"_ShoreDepth",1.15f);
            Set(value,"_FoamNoiseScale",1.6f); Set(value,"_FoamNoiseSpeed",.72f);
            Set(value,"_FoamCrestThreshold",.36f); Set(value,"_FoamCrestIntensity",.68f);
            Set(value,"_EdgeFoamIntensity",.48f); Set(value,"_GerstnerIntensity",.62f);
            Set(value,"_GAmplitude",new Vector4(.16f,.11f,.075f,.05f));
            Set(value,"_GFrequency",new Vector4(.78f,1.15f,.58f,.42f));
            Set(value,"_GSteepness",new Vector4(.62f,.54f,.44f,.36f));
            Set(value,"_GSpeed",new Vector4(.82f,1.02f,.66f,.5f));
            Set(value,"_GDirectionAB",new Vector4(.3f,.88f,.94f,.15f));
            Set(value,"_GDirectionCD",new Vector4(.12f,.96f,.64f,.48f));
        }

        private static void Set(Material material,string property,float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property,value);
        }

        private static void Set(Material material,string property,Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property,value);
        }

        private static void Set(Material material,string property,Vector4 value)
        {
            if (material.HasProperty(property)) material.SetVector(property,value);
        }
    }
}
