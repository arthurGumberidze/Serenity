#ifndef SERENITY_BIOME_PALETTE
#define SERENITY_BIOME_PALETTE
float4 _WorldMapToDesign;
float4 _WorldMapBiomeShapes[8], _WorldMapBiomeColors[8], _WorldMapBlossomGroves[3];
float _WorldMapArtEnabled, _WorldMapSeason;
half4 _WorldMapShallowWater, _WorldMapDeepWater, _WorldMapMarshWater;
float2 WorldMapDesign(float2 world) { return world * _WorldMapToDesign.zw + _WorldMapToDesign.xy; }
float WorldMapEllipse(float2 samplePosition, float4 ellipse)
{
    float2 d = (samplePosition - ellipse.xy) / max(ellipse.zw, 1);
    return 1 - smoothstep(.45, 1, length(d));
}
half4 WorldMapFoliage(float2 world, float flowering)
{
    float2 p = WorldMapDesign(world);
    half3 color = 0; float total = 0;
    [unroll] for (int n = 0; n < 8; n++)
    {
        float2 d = (p - _WorldMapBiomeShapes[n].xy) / max(_WorldMapBiomeShapes[n].zw, 1);
        float w = exp(-dot(d,d) * 2);
        color += _WorldMapBiomeColors[n].rgb * w; total += w;
    }
    color /= max(total, .0001);
    // 0 spring, 1 summer, 2 autumn, 3 winter. Only the landmark grove keeps its blossoms.
    float spring = 1 - smoothstep(.05, .9, _WorldMapSeason);
    float autumn = saturate(1 - abs(_WorldMapSeason - 2));
    float winter = saturate(_WorldMapSeason - 2);
    color = lerp(color, half3(.66,.37,.13), autumn * .62 * flowering);
    color = lerp(color, half3(.40,.46,.43), winter * .55);
    float bloom = WorldMapEllipse(p, _WorldMapBlossomGroves[0]);
    bloom = max(bloom, max(WorldMapEllipse(p, _WorldMapBlossomGroves[1]),
        WorldMapEllipse(p, _WorldMapBlossomGroves[2])) * spring);
    // Coherent groups have soft edges; bark is excluded by the caller.
    color = lerp(color, half3(.96,.46,.65), bloom * flowering);
    return half4(color, bloom * flowering);
}
float WorldMapMurk(float2 world)
{
    return WorldMapEllipse(WorldMapDesign(world), _WorldMapBiomeShapes[7]) * _WorldMapArtEnabled;
}
#endif
