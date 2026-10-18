using Game.Domain.Characters;
using UnityEngine;

namespace Game.Presentation.Characters
{
    public readonly struct CharacterLodStyle
    {
        public CharacterLodStyle(Color skin, Color hair, Color clothing, Vector3 scale)
        {
            Skin = skin;
            Hair = hair;
            Clothing = clothing;
            Scale = scale;
        }

        public Color Skin { get; }
        public Color Hair { get; }
        public Color Clothing { get; }
        public Vector3 Scale { get; }

        public static CharacterLodStyle From(AppearanceState appearance)
        {
            var skin = appearance.SkinId.Value == "skin_light" ? new Color(0.82f, 0.63f, 0.49f) :
                appearance.SkinId.Value == "skin_deep" ? new Color(0.30f, 0.17f, 0.105f) :
                new Color(0.61f, 0.39f, 0.26f);
            var hair = appearance.HairColorId.Value == "hair_black" ? new Color(0.035f, 0.027f, 0.022f) :
                appearance.HairColorId.Value == "hair_auburn" ? new Color(0.35f, 0.10f, 0.035f) :
                appearance.HairColorId.Value == "hair_blond" ? new Color(0.68f, 0.52f, 0.25f) :
                appearance.HairColorId.Value == "hair_gray" ? new Color(0.46f, 0.45f, 0.42f) :
                new Color(0.17f, 0.075f, 0.03f);
            var clothing = appearance.ClothingId == StoneAgeAppearanceProfiles.MilitaryClothing
                ? new Color(0.24f, 0.25f, 0.16f)
                : appearance.ClothingId == StoneAgeAppearanceProfiles.WorkerClothing
                    ? new Color(0.32f, 0.19f, 0.075f)
                    : new Color(0.39f, 0.22f, 0.09f);
            var height = Mathf.Lerp(0.91f, 1.09f,
                appearance.Parameter(StoneAgeAppearanceProfiles.Height) / 1000f);
            var build = Mathf.Lerp(0.88f, 1.14f,
                appearance.Parameter(StoneAgeAppearanceProfiles.Build) / 1000f);
            return new CharacterLodStyle(skin, hair, clothing, new Vector3(build, height, build));
        }
    }
}
