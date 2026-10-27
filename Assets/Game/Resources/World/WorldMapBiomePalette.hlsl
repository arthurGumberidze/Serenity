#ifndef SERENITY_BIOME_PALETTE
#define SERENITY_BIOME_PALETTE
float4 _WorldMapToDesign;
float4 _WorldMapBiomeShapes[8], _WorldMapBiomeColors[8], _WorldMapBlossomGroves[3];
float _WorldMapArtEnabled, _WorldMapSeason;
TEXTURE2D(_WorldMapSnowAlbedo); SAMPLER(sampler_WorldMapSnowAlbedo);
TEXTURE2D(_WorldMapSnowNormal); SAMPLER(sampler_WorldMapSnowNormal);
TEXTURE2D(_WorldMapSnowHeight); SAMPLER(sampler_WorldMapSnowHeight);
float _WorldMapSnowTextures;
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
half3 WorldMapGround(half3 albedo, float3 world, float3 normal)
{
    float2 p = WorldMapDesign(world.xz);
    // Snow blankets the ridge's shelves; only near-vertical faces retain exposed stone.
    float2 ridge = (p - _WorldMapBiomeShapes[2].xy) / (_WorldMapBiomeShapes[2].zw * 1.3);
    float ridgeMask = 1 - smoothstep(.72, 1.0, length(ridge));
    float snow = ridgeMask * smoothstep(.18, .65, normal.y);
    float grove = WorldMapEllipse(p, _WorldMapBlossomGroves[0]);
    float marsh = WorldMapMurk(world.xz);
    float meadow = WorldMapEllipse(p, _WorldMapBiomeShapes[0]);
    float luminance = dot(albedo, half3(.2126,.7152,.0722));
    half3 result = lerp(albedo, albedo * half3(.82,1.24,.64), meadow * .7);
    result = lerp(result, half3(.75,.38,.49) * (.55 + luminance), grove * .68);
    result = lerp(result, half3(.13,.18,.10) * (.6 + luminance), marsh * .72);
    float fineSnow = .97 + .03 * sin(world.x * 4.1) * sin(world.z * 3.7);
    half3 snowColor = half3(.91,.96,1.0) * fineSnow;
    if (_WorldMapSnowTextures > .5)
    {
        float2 snowUv = world.xz / 7.0;
        half3 detail = SAMPLE_TEXTURE2D(_WorldMapSnowAlbedo,sampler_WorldMapSnowAlbedo,snowUv).rgb;
        half relief = SAMPLE_TEXTURE2D(_WorldMapSnowHeight,sampler_WorldMapSnowHeight,snowUv).r;
        snowColor = detail * half3(.94,.98,1.0) * (.88 + relief * .2);
    }
    result = lerp(result, snowColor, snow);
    return lerp(albedo, result, _WorldMapArtEnabled);
}
half3 WorldMapSnowSurfaceNormal(float3 world, half3 normal)
{
    if (_WorldMapSnowTextures < .5) return normal;
    float2 ridge = (WorldMapDesign(world.xz) - _WorldMapBiomeShapes[2].xy) / (_WorldMapBiomeShapes[2].zw * 1.3);
    half mask = (1 - smoothstep(.72,1.0,length(ridge))) * smoothstep(.18,.65,normal.y) * _WorldMapArtEnabled;
    half3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_WorldMapSnowNormal,sampler_WorldMapSnowNormal,world.xz / 7.0));
    return normalize(normal + half3(detail.x,0,detail.y) * mask * .28);
}
#endif
