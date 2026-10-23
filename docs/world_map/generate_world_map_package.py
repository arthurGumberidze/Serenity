#!/usr/bin/env python3
"""
Generate the Serenity 1 km world-map design package from one deterministic data source.

This tool writes documentation, JSON layers and technical PNG maps only. It does not
modify Unity scenes, assets, saves or runtime configuration.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parent
IMAGES = ROOT / "images"
MAP_VERSION = "serenity-world-map-v1"
GENERATION_ID = "p109-world-map-v2-proposed"
DATE = "2026-10-23"
CANVAS = 1800
MAP_LEFT = 120
MAP_TOP = 120
MAP_SIZE = 1420
MAP_BOTTOM = MAP_TOP + MAP_SIZE
RUNTIME_ORIGIN = (-570.0, -120.0)


REGIONS = [
    {
        "id": "region_a_start_valley",
        "code": "A",
        "name": "Стартовая долина",
        "nameEn": "Start Valley",
        "polygon": [[0, 620], [0, 1000], [360, 1000], [330, 830], [420, 700], [300, 610]],
        "elevationMeters": [12, 28],
        "biomes": ["mixed_open_forest", "meadow_clearings", "riparian"],
        "soil": ["loam", "sandy_loam"],
        "gameplay": ["player_start", "early_building", "basic_resources", "river_access"],
        "color": "#a9cf7f",
    },
    {
        "id": "region_b_north_forest",
        "code": "B",
        "name": "Северный лес",
        "nameEn": "North Forest",
        "polygon": [[360, 1000], [720, 1000], [690, 720], [600, 650], [420, 700], [330, 830]],
        "elevationMeters": [20, 48],
        "biomes": ["dense_conifer_forest", "mixed_forest", "ravines"],
        "soil": ["podzol", "forest_loam"],
        "gameplay": ["timber", "mushrooms", "berries", "northern_hunt"],
        "color": "#3f7f51",
    },
    {
        "id": "region_c_stone_ridge",
        "code": "C",
        "name": "Каменный хребет",
        "nameEn": "Stone Ridge",
        "polygon": [[720, 1000], [1000, 1000], [1000, 650], [830, 620], [690, 720]],
        "elevationMeters": [38, 82],
        "biomes": ["rocky_highland", "sparse_conifer", "alpine_scrub"],
        "soil": ["rock", "thin_gravel"],
        "gameplay": ["stone", "hidden_minerals", "cave", "mountain_lake"],
        "color": "#9da293",
    },
    {
        "id": "region_d_west_grove",
        "code": "D",
        "name": "Западная роща",
        "nameEn": "West Grove",
        "polygon": [[0, 620], [300, 610], [420, 700], [600, 650], [560, 500], [420, 400], [300, 330], [0, 360]],
        "elevationMeters": [14, 34],
        "biomes": ["deciduous_grove", "small_clearings", "lake_shore"],
        "soil": ["brown_forest_soil", "loam"],
        "gameplay": ["hardwood", "western_lake", "stone_circle", "foraging"],
        "color": "#86a96e",
    },
    {
        "id": "region_e_south_meadows",
        "code": "E",
        "name": "Южные луга",
        "nameEn": "South Meadows",
        "polygon": [[0, 360], [300, 330], [420, 400], [500, 300], [480, 0], [0, 0]],
        "elevationMeters": [11, 30],
        "biomes": ["open_meadow", "wooded_islets", "shrubland"],
        "soil": ["fertile_loam", "alluvial_loam"],
        "gameplay": ["high_game_density", "future_farming", "settlement_expansion"],
        "color": "#c6d989",
    },
    {
        "id": "region_f_lake_district",
        "code": "F",
        "name": "Озёрный край",
        "nameEn": "Lake District",
        "polygon": [[420, 400], [560, 500], [690, 480], [800, 360], [760, 180], [690, 0], [480, 0], [500, 300]],
        "elevationMeters": [8, 34],
        "biomes": ["lake_forest", "reed_shore", "wet_meadow"],
        "soil": ["alluvium", "peaty_loam"],
        "gameplay": ["fishing", "clay", "reed_fiber", "southern_lake"],
        "color": "#7fa886",
    },
    {
        "id": "region_g_east_highlands",
        "code": "G",
        "name": "Восточные возвышенности",
        "nameEn": "East Highlands",
        "polygon": [[690, 720], [830, 620], [1000, 650], [1000, 300], [800, 360], [690, 480], [560, 500], [600, 650]],
        "elevationMeters": [26, 62],
        "biomes": ["rolling_mixed_forest", "clay_banks", "rock_passes"],
        "soil": ["clay", "stony_loam"],
        "gameplay": ["clay", "stone", "central_east_lake", "chokepoints"],
        "color": "#8fa177",
    },
    {
        "id": "region_h_south_marsh",
        "code": "H",
        "name": "Южное болото",
        "nameEn": "South Marsh",
        "polygon": [[760, 180], [800, 360], [1000, 300], [1000, 0], [690, 0]],
        "elevationMeters": [6, 18],
        "biomes": ["marsh", "wet_scrub", "reed_channels"],
        "soil": ["peat", "waterlogged_clay"],
        "gameplay": ["limited_stone_age_value", "medicinal_plants", "future_oil"],
        "color": "#6f9184",
    },
]


RIVER_POINTS = [
    [0, 770, 18], [80, 700, 20], [155, 625, 18], [250, 590, 22],
    [360, 545, 24], [450, 475, 22], [510, 390, 24], [600, 340, 26],
    [700, 305, 24], [790, 235, 26], [875, 190, 24], [1000, 140, 28],
]

TRIBUTARIES = [
    {
        "id": "stream_west_lake_outlet",
        "name": "Ручей Западного озера",
        "points": [[205, 465], [230, 510], [265, 555], [300, 570]],
        "widthMeters": [3, 5],
        "connects": ["lake_west_grove", "river_main"],
    },
    {
        "id": "stream_mountain",
        "name": "Горный ручей",
        "points": [[850, 765], [820, 710], [775, 650], [735, 575], [705, 530]],
        "widthMeters": [3, 6],
        "connects": ["lake_mountain", "lake_central_east"],
    },
    {
        "id": "stream_central_outlet",
        "name": "Восточный сток",
        "points": [[680, 475], [655, 430], [625, 385], [600, 340]],
        "widthMeters": [4, 7],
        "connects": ["lake_central_east", "river_main"],
    },
    {
        "id": "stream_south_lake",
        "name": "Южный проток",
        "points": [[620, 240], [650, 270], [675, 290], [700, 305]],
        "widthMeters": [4, 7],
        "connects": ["lake_south_forest", "river_main"],
    },
]

LAKES = [
    {
        "id": "lake_south_forest",
        "name": "Большое Южное лесное озеро",
        "center": [600, 175],
        "polygon": [[510, 160], [525, 205], [565, 238], [620, 240], [675, 218], [695, 180], [680, 135], [635, 110], [575, 108], [530, 125]],
        "surfaceY": 8.0,
        "maxDepthMeters": 7.5,
        "shore": "rock_reed_mixed",
        "fishCapacityIndex": 1.0,
        "fishingLandings": [[514, 209], [686, 224], [691, 129], [520, 118]],
    },
    {
        "id": "lake_west_grove",
        "name": "Западное озеро",
        "center": [165, 455],
        "polygon": [[105, 450], [115, 482], [145, 505], [185, 500], [218, 475], [225, 442], [200, 418], [158, 410], [120, 425]],
        "surfaceY": 8.0,
        "maxDepthMeters": 4.5,
        "shore": "deciduous_soft_bank",
        "fishCapacityIndex": 0.7,
        "fishingLandings": [[104, 488], [230, 479], [210, 409], [110, 416]],
    },
    {
        "id": "lake_mountain",
        "name": "Горное озеро",
        "center": [855, 805],
        "polygon": [[802, 798], [818, 830], [852, 845], [892, 835], [910, 808], [895, 780], [860, 768], [825, 775]],
        "surfaceY": 36.0,
        "maxDepthMeters": 6.0,
        "shore": "rocky_steep_north",
        "fishCapacityIndex": 0.55,
        "fishingLandings": [[808, 837], [905, 772], [815, 767]],
    },
    {
        "id": "lake_central_east",
        "name": "Центрально-восточное лесное озеро",
        "center": [705, 505],
        "polygon": [[635, 505], [650, 540], [690, 557], [738, 548], [770, 520], [760, 485], [728, 462], [680, 465], [650, 480]],
        "surfaceY": 17.0,
        "maxDepthMeters": 5.5,
        "shore": "forest_reed_mixed",
        "fishCapacityIndex": 0.85,
        "fishingLandings": [[640, 547], [782, 523], [734, 451], [638, 476]],
    },
]


ROUTES = [
    {
        "id": "route_main_west_southeast",
        "name": "Главная грунтовая тропа",
        "kind": "main_primitive_trail",
        "widthMeters": [5, 8],
        "points": [[0, 585], [110, 575], [225, 555], [345, 540], [465, 520], [575, 470], [680, 410], [790, 330], [900, 240], [1000, 170]],
    },
    {
        "id": "path_start_north_forest",
        "name": "Северная лесная тропа",
        "kind": "footpath",
        "widthMeters": [1.5, 3],
        "points": [[185, 840], [275, 820], [365, 790], [455, 760], [530, 755]],
    },
    {
        "id": "path_start_west_lake",
        "name": "Тропа к Западному озеру",
        "kind": "footpath",
        "widthMeters": [1.5, 3],
        "points": [[185, 840], [145, 745], [125, 640], [115, 545], [104, 488]],
    },
    {
        "id": "path_ridge",
        "name": "Подъём на хребет",
        "kind": "mountain_path",
        "widthMeters": [1.5, 2.5],
        "points": [[575, 470], [665, 560], [735, 650], [790, 735], [790, 810], [808, 837], [850, 875], [905, 860], [930, 815]],
    },
    {
        "id": "path_south_meadow",
        "name": "Южная охотничья тропа",
        "kind": "footpath",
        "widthMeters": [1.5, 3],
        "points": [[345, 540], [300, 430], [250, 320], [210, 225], [260, 120]],
    },
    {
        "id": "path_lake_district",
        "name": "Озёрная тропа",
        "kind": "footpath",
        "widthMeters": [1.5, 3],
        "points": [[575, 470], [640, 547], [705, 575], [770, 550], [782, 523], [735, 460], [680, 410], [650, 300], [686, 224]],
    },
    {
        "id": "path_marsh_edge",
        "name": "Кромка болота",
        "kind": "raised_footpath",
        "widthMeters": [1.5, 2.5],
        "points": [[790, 330], [850, 285], [900, 240], [890, 175], [860, 105]],
    },
]

CROSSINGS = [
    {"id": "crossing_start_ford", "name": "Северо-западный брод", "position": [145, 635], "type": "ford", "routeIds": ["path_start_west_lake"]},
    {"id": "crossing_main_bridge", "name": "Центральная переправа", "position": [350, 542], "type": "simple_wood_bridge", "routeIds": ["route_main_west_southeast"]},
    {"id": "crossing_lake_footbridge", "name": "Озёрный мосток", "position": [632, 385], "type": "footbridge", "routeIds": ["path_lake_district"]},
]

CHOKEPOINTS = [
    {"id": "choke_ridge_pass", "name": "Проход Хребта", "position": [770, 655], "radiusMeters": 28},
    {"id": "choke_marsh_causeway", "name": "Сухая кромка болота", "position": [820, 315], "radiusMeters": 24},
    {"id": "choke_central_bridge", "name": "Центральная переправа", "position": [350, 542], "radiusMeters": 22},
]

POIS = [
    {"id": "poi_sacred_grove", "name": "Священная роща", "position": [530, 755], "regionId": "region_b_north_forest", "purpose": ["culture", "event", "medicinal_plants"], "protectionRadiusMeters": 32},
    {"id": "poi_stone_circle", "name": "Древний круг камней", "position": [155, 365], "regionId": "region_d_west_grove", "purpose": ["discovery", "culture"], "protectionRadiusMeters": 24},
    {"id": "poi_abandoned_camp", "name": "Заброшенная стоянка", "position": [300, 730], "regionId": "region_a_start_valley", "purpose": ["discovery", "small_loot"], "protectionRadiusMeters": 18},
    {"id": "poi_ancient_ruins", "name": "Древние каменные остатки", "position": [635, 410], "regionId": "region_f_lake_district", "purpose": ["discovery", "future_archaeology"], "protectionRadiusMeters": 28},
    {"id": "poi_overlook", "name": "Обзорная высота", "position": [895, 650], "regionId": "region_c_stone_ridge", "purpose": ["scouting", "strategic_advantage"], "protectionRadiusMeters": 22},
    {"id": "poi_rock_spire", "name": "Каменный клык", "position": [780, 870], "regionId": "region_c_stone_ridge", "purpose": ["landmark", "navigation"], "protectionRadiusMeters": 20},
    {"id": "poi_cave", "name": "Малая пещера", "position": [930, 815], "regionId": "region_c_stone_ridge", "purpose": ["exploration", "shelter", "minerals"], "protectionRadiusMeters": 26},
    {"id": "poi_giant_tree", "name": "Старое великое дерево", "position": [330, 480], "regionId": "region_d_west_grove", "purpose": ["landmark", "culture"], "protectionRadiusMeters": 22},
    {"id": "poi_secluded_glade", "name": "Уединённая поляна", "position": [520, 680], "regionId": "region_b_north_forest", "purpose": ["foraging", "temporary_camp"], "protectionRadiusMeters": 30},
]

HEIGHT_ANCHORS = [
    {"id": "height_start_plateau", "position": [185, 840], "heightMeters": 18, "radiusMeters": 95, "mode": "flatten_blend"},
    {"id": "height_north_hills", "position": [520, 850], "heightMeters": 42, "radiusMeters": 180, "mode": "raise"},
    {"id": "height_ridge_peak", "position": [860, 880], "heightMeters": 82, "radiusMeters": 170, "mode": "raise"},
    {"id": "height_ridge_pass", "position": [770, 655], "heightMeters": 38, "radiusMeters": 60, "mode": "lower_pass"},
    {"id": "height_west_grove", "position": [190, 470], "heightMeters": 22, "radiusMeters": 150, "mode": "rolling"},
    {"id": "height_south_meadow", "position": [230, 180], "heightMeters": 17, "radiusMeters": 220, "mode": "gentle"},
    {"id": "height_east_highland", "position": [880, 500], "heightMeters": 52, "radiusMeters": 160, "mode": "raise"},
    {"id": "height_marsh_basin", "position": [885, 125], "heightMeters": 9, "radiusMeters": 150, "mode": "lower"},
]

VEGETATION_CLASSES = [
    {"id": "dense_conifer", "name": "Густой хвойный лес", "interactiveTreesPerHa": [70, 110], "decorativeInstancesPerHa": [140, 240], "canopy": [0.7, 0.9]},
    {"id": "mixed_forest", "name": "Смешанный лес", "interactiveTreesPerHa": [45, 80], "decorativeInstancesPerHa": [100, 180], "canopy": [0.5, 0.75]},
    {"id": "deciduous_grove", "name": "Лиственная роща", "interactiveTreesPerHa": [35, 65], "decorativeInstancesPerHa": [80, 150], "canopy": [0.45, 0.7]},
    {"id": "open_woodland", "name": "Редколесье", "interactiveTreesPerHa": [12, 32], "decorativeInstancesPerHa": [35, 80], "canopy": [0.15, 0.4]},
    {"id": "meadow", "name": "Луг", "interactiveTreesPerHa": [0, 8], "decorativeInstancesPerHa": [30, 90], "canopy": [0, 0.12]},
    {"id": "marsh_vegetation", "name": "Болотная растительность", "interactiveTreesPerHa": [2, 12], "decorativeInstancesPerHa": [120, 260], "canopy": [0.05, 0.2]},
]

VEGETATION_ZONES = [
    {"id": "veg_start_open", "classId": "open_woodland", "shape": {"type": "ellipse", "center": [185, 840], "radius": [120, 95]}, "densityMultiplier": 0.55, "clearings": [[185, 840, 72]]},
    {"id": "veg_north_dense", "classId": "dense_conifer", "shape": {"type": "polygon", "points": [[350, 980], [710, 980], [675, 735], [560, 665], [410, 720], [330, 840]]}, "densityMultiplier": 1.0},
    {"id": "veg_west_deciduous", "classId": "deciduous_grove", "shape": {"type": "polygon", "points": [[20, 600], [280, 590], [405, 675], [545, 625], [510, 510], [390, 420], [260, 350], [20, 380]]}, "densityMultiplier": 0.9},
    {"id": "veg_south_meadow", "classId": "meadow", "shape": {"type": "polygon", "points": [[20, 340], [290, 315], [455, 375], [480, 285], [455, 20], [20, 20]]}, "densityMultiplier": 0.85},
    {"id": "veg_lake_forest", "classId": "mixed_forest", "shape": {"type": "polygon", "points": [[450, 390], [555, 480], [680, 460], [780, 350], [735, 200], [680, 30], [500, 30], [520, 290]]}, "densityMultiplier": 0.78},
    {"id": "veg_east_open", "classId": "open_woodland", "shape": {"type": "polygon", "points": [[700, 700], [825, 610], [980, 635], [980, 320], [815, 375], [700, 490], [590, 510], [620, 635]]}, "densityMultiplier": 0.72},
    {"id": "veg_marsh", "classId": "marsh_vegetation", "shape": {"type": "polygon", "points": [[770, 175], [810, 340], [980, 285], [980, 20], [705, 20]]}, "densityMultiplier": 1.0},
]

RESOURCE_TYPES = [
    {"id": "wood", "era": "stone_age", "visible": True, "depletion": "node_disappears", "renewal": "sapling_regrowth"},
    {"id": "stone", "era": "stone_age", "visible": True, "depletion": "node_disappears", "renewal": "none"},
    {"id": "berries", "era": "stone_age", "visible": True, "depletion": "seasonal_empty", "renewal": "seasonal_regrowth"},
    {"id": "mushrooms", "era": "stone_age", "visible": True, "depletion": "seasonal_empty", "renewal": "seasonal_regrowth"},
    {"id": "medicinal_plants", "era": "stone_age", "visible": True, "depletion": "picked", "renewal": "seasonal_regrowth"},
    {"id": "branches", "era": "stone_age", "visible": True, "depletion": "picked", "renewal": "forest_litter_tick"},
    {"id": "plant_fiber", "era": "stone_age", "visible": True, "depletion": "picked", "renewal": "seasonal_regrowth"},
    {"id": "clay", "era": "stone_age", "visible": True, "depletion": "pit_state", "renewal": "none"},
    {"id": "fish", "era": "stone_age", "visible": True, "depletion": "population", "renewal": "carrying_capacity_growth"},
    {"id": "flint", "era": "stone_age", "visible": True, "depletion": "node_disappears", "renewal": "none"},
    {"id": "copper_ore", "era": "future", "visible": False, "unlockTechnology": "prospecting_copper"},
    {"id": "iron_ore", "era": "future", "visible": False, "unlockTechnology": "prospecting_iron"},
    {"id": "gold_ore", "era": "future", "visible": False, "unlockTechnology": "prospecting_gold"},
    {"id": "oil", "era": "future", "visible": False, "unlockTechnology": "oil_extraction"},
]

RESOURCE_ZONES = [
    {"id": "res_start_basics", "resources": ["wood", "branches", "berries", "stone", "medicinal_plants"], "shape": {"type": "ring", "center": [185, 840], "innerRadius": 35, "outerRadius": 125}, "density": "low_to_medium", "access": "immediate"},
    {"id": "res_north_forest", "resources": ["wood", "branches", "mushrooms", "berries", "medicinal_plants"], "shape": {"type": "polygon", "points": [[360, 980], [700, 980], [680, 740], [575, 670], [430, 715], [345, 835]]}, "density": "high", "access": "forest_paths"},
    {"id": "res_west_grove", "resources": ["wood", "branches", "berries", "mushrooms"], "shape": {"type": "ellipse", "center": [230, 500], "radius": [180, 125]}, "density": "medium_high", "access": "footpaths"},
    {"id": "res_ridge_stone", "resources": ["stone", "flint"], "shape": {"type": "ellipse", "center": [835, 820], "radius": [150, 135]}, "density": "high", "access": "ridge_path"},
    {"id": "res_east_clay", "resources": ["clay", "stone"], "shape": {"type": "ellipse", "center": [880, 455], "radius": [95, 150]}, "density": "medium_high", "access": "main_route"},
    {"id": "res_south_fiber", "resources": ["plant_fiber", "medicinal_plants", "berries"], "shape": {"type": "ellipse", "center": [300, 190], "radius": [210, 135]}, "density": "medium", "access": "open"},
    {"id": "res_marsh_plants", "resources": ["plant_fiber", "medicinal_plants"], "shape": {"type": "polygon", "points": [[780, 165], [820, 325], [980, 280], [980, 25], [710, 25]]}, "density": "medium", "access": "restricted"},
    {"id": "res_hidden_ridge_minerals", "resources": ["copper_ore", "iron_ore", "gold_ore"], "shape": {"type": "ellipse", "center": [865, 835], "radius": [120, 105]}, "density": "hidden_gradient", "access": "technology_locked"},
    {"id": "res_hidden_marsh_oil", "resources": ["oil"], "shape": {"type": "ellipse", "center": [885, 125], "radius": [88, 72]}, "density": "hidden_reservoir", "access": "technology_locked"},
]

SPECIES = [
    {"id": "deer", "name": "Олень", "suitableBiomes": ["open_meadow", "open_woodland", "mixed_forest_edge"], "targetPopulation": [28, 42], "groupSize": [2, 7], "activity": "dawn_dusk"},
    {"id": "boar", "name": "Кабан", "suitableBiomes": ["mixed_forest", "deciduous_grove", "shrubland"], "targetPopulation": [16, 26], "groupSize": [1, 4], "activity": "dusk_night"},
    {"id": "hare", "name": "Заяц", "suitableBiomes": ["open_meadow", "open_woodland"], "targetPopulation": [18, 32], "groupSize": [1, 3], "activity": "dawn_dusk"},
    {"id": "fox", "name": "Лиса", "suitableBiomes": ["mixed_forest_edge", "open_meadow"], "targetPopulation": [5, 9], "groupSize": [1, 2], "activity": "dusk_night"},
    {"id": "wolf", "name": "Волк", "suitableBiomes": ["dense_conifer_forest", "rocky_highland"], "targetPopulation": [5, 9], "groupSize": [2, 5], "activity": "night"},
    {"id": "bear", "name": "Медведь", "suitableBiomes": ["remote_mixed_forest", "rocky_highland"], "targetPopulation": [2, 4], "groupSize": [1, 1], "activity": "day_dusk"},
    {"id": "waterfowl", "name": "Водоплавающие птицы", "suitableBiomes": ["lake_shore", "riparian", "marsh"], "targetPopulation": [14, 26], "groupSize": [2, 8], "activity": "day"},
]

WILDLIFE_CONCENTRATIONS = [
    {"id": "hunt_south_meadows", "name": "Южные луга", "species": ["deer", "hare", "fox"], "shape": {"type": "ellipse", "center": [250, 185], "radius": [220, 145]}, "multiplier": 1.8},
    {"id": "hunt_north_forest", "name": "Северный лес", "species": ["deer", "boar", "wolf", "bear"], "shape": {"type": "ellipse", "center": [520, 835], "radius": [185, 135]}, "multiplier": 1.55},
    {"id": "hunt_west_clearings", "name": "Поляны Западной рощи", "species": ["deer", "boar", "hare"], "shape": {"type": "ellipse", "center": [285, 445], "radius": [110, 80]}, "multiplier": 1.45},
    {"id": "hunt_east_passes", "name": "Восточные проходы", "species": ["deer", "wolf", "fox"], "shape": {"type": "ellipse", "center": [860, 475], "radius": [95, 135]}, "multiplier": 1.35},
]

BUILD_ZONES = [
    {"id": "build_start_core", "category": "ideal", "shape": {"type": "ellipse", "center": [185, 840], "radius": [78, 68]}, "slopeMaxDegrees": 8, "requiresClearing": False, "purpose": ["first_settlement"]},
    {"id": "build_start_west_expansion", "category": "ideal", "shape": {"type": "polygon", "points": [[55, 760], [145, 745], [190, 690], [120, 650], [35, 675]]}, "slopeMaxDegrees": 12, "requiresClearing": True, "purpose": ["housing", "storage"]},
    {"id": "build_start_east_expansion", "category": "ideal", "shape": {"type": "polygon", "points": [[245, 855], [350, 825], [385, 760], [305, 715], [245, 750]]}, "slopeMaxDegrees": 12, "requiresClearing": True, "purpose": ["workshops", "housing"]},
    {"id": "build_south_meadow_settlement", "category": "ideal", "shape": {"type": "ellipse", "center": [285, 175], "radius": [150, 105]}, "slopeMaxDegrees": 10, "requiresClearing": False, "purpose": ["future_settlement", "farming"]},
    {"id": "build_lake_district_clearings", "category": "conditional", "shape": {"type": "ellipse", "center": [525, 310], "radius": [115, 75]}, "slopeMaxDegrees": 16, "requiresClearing": True, "purpose": ["fishing_support", "small_outpost"]},
    {"id": "build_east_highland_terraces", "category": "conditional", "shape": {"type": "ellipse", "center": [880, 500], "radius": [75, 105]}, "slopeMaxDegrees": 18, "requiresClearing": True, "purpose": ["clay_outpost", "watch_position"]},
    {"id": "build_ridge_restricted", "category": "restricted", "shape": {"type": "ellipse", "center": [850, 830], "radius": [155, 150]}, "slopeMaxDegrees": 24, "requiresClearing": True, "purpose": ["mining_only"]},
    {"id": "build_marsh_restricted", "category": "restricted", "shape": {"type": "polygon", "points": [[770, 175], [810, 350], [1000, 300], [1000, 0], [690, 0]]}, "slopeMaxDegrees": 6, "requiresClearing": False, "purpose": ["future_drainage_technology"]},
]

PLAYER_START = {
    "id": "player_start_northwest",
    "designPosition": [185, 840],
    "runtimePosition": [185 + RUNTIME_ORIGIN[0], 0, 840 + RUNTIME_ORIGIN[1]],
    "clearingDiameterMeters": 145,
    "initialRevealRadiusMeters": 135,
    "regionId": "region_a_start_valley",
}


def meta(layer: str) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "mapVersion": MAP_VERSION,
        "implementationGenerationId": GENERATION_ID,
        "status": "design_only_not_activated",
        "date": DATE,
        "layer": layer,
        "units": "meters",
        "north": "+Z",
    }


def runtime_point(point: Iterable[float]) -> list[float]:
    p = list(point)
    return [round(p[0] + RUNTIME_ORIGIN[0], 3), round(p[1] + RUNTIME_ORIGIN[1], 3)]


def world_tiles() -> list[dict[str, Any]]:
    return [
        {
            "id": f"tile_{x}_{z}",
            "index": [x, z],
            "designBounds": {"xMin": x * 250, "xMaxExclusive": (x + 1) * 250, "zMin": z * 250, "zMaxExclusive": (z + 1) * 250},
            "runtimeOrigin": [RUNTIME_ORIGIN[0] + x * 250, 0, RUNTIME_ORIGIN[1] + z * 250],
        }
        for z in range(4)
        for x in range(4)
    ]


def write_json(name: str, value: Any) -> None:
    (ROOT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def point_in_polygon(point: list[float], polygon: list[list[float]]) -> bool:
    x, y = point
    inside = False
    j = len(polygon) - 1
    for i in range(len(polygon)):
        xi, yi = polygon[i]
        xj, yj = polygon[j]
        if ((yi > y) != (yj > y)) and x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-9) + xi:
            inside = not inside
        j = i
    return inside


def write_data() -> None:
    write_json(
        "WORLD_MAP_COORDINATES.json",
        {
            **meta("coordinate_index"),
            "designCoordinateSystem": {
                "x": "west_to_east_0_to_1000",
                "z": "south_to_north_0_to_1000",
                "y": "surface_height_meters",
                "origin": [0, 0, 0],
                "bounds": {"xMin": 0, "xMax": 1000, "zMin": 0, "zMax": 1000, "areaSquareMeters": 1000000},
            },
            "unityProjection": {
                "currentCompactRuntimeOrigin": [RUNTIME_ORIGIN[0], 0, RUNTIME_ORIGIN[1]],
                "formula": {"unityX": "designX - 570", "unityZ": "designZ - 120", "designX": "unityX + 570", "designZ": "unityZ + 120"},
                "note": "Проектная сетка 0..1000 удобна для документации; будущий импорт сохраняет текущий runtime origin.",
            },
            "tiling": {"columns": 4, "rows": 4, "tileSizeMeters": 250, "tiles": world_tiles()},
            "playerStart": PLAYER_START,
            "regionPolygons": [{"id": r["id"], "code": r["code"], "polygon": r["polygon"]} for r in REGIONS],
            "waterIndex": {
                "riverId": "river_main",
                "riverCenterline": [[p[0], p[1]] for p in RIVER_POINTS],
                "lakeIds": [lake["id"] for lake in LAKES],
            },
            "routeIndex": [{"id": route["id"], "points": route["points"]} for route in ROUTES],
            "poiIndex": [{"id": poi["id"], "position": poi["position"]} for poi in POIS],
            "canonicalFiles": [
                "WORLD_MAP_BIOMES.json", "WORLD_MAP_TERRAIN.json", "WORLD_MAP_WATER.json",
                "WORLD_MAP_VEGETATION.json", "WORLD_MAP_RESOURCES.json", "WORLD_MAP_WILDLIFE.json",
                "WORLD_MAP_ROADS.json", "WORLD_MAP_POI.json", "WORLD_MAP_BUILDABILITY.json",
            ],
        },
    )
    write_json(
        "WORLD_MAP_BIOMES.json",
        {
            **meta("biomes_and_regions"),
            "regions": [{k: v for k, v in region.items() if k != "color"} for region in REGIONS],
            "transitionRules": {
                "minimumBlendWidthMeters": 24,
                "preferredBlendWidthMeters": [35, 70],
                "noRectangularVisualEdges": True,
                "ecotoneLayers": ["ground_material", "canopy_density", "understory", "small_props"],
            },
        },
    )
    write_json(
        "WORLD_MAP_TERRAIN.json",
        {
            **meta("terrain"),
            "heightRangeMeters": [4, 96],
            "waterLevelReferenceMeters": 8,
            "walkableLandTypicalMeters": [11, 62],
            "ridgePeakMeters": 82,
            "heightmap": {"worldResolution": 1025, "tileResolution": 257, "tiles": [4, 4], "sharedBorderSamples": True},
            "heightAnchors": HEIGHT_ANCHORS,
            "noise": [
                {"id": "macro", "wavelengthMeters": [220, 360], "amplitudeMeters": [5, 12], "weight": 0.52},
                {"id": "meso", "wavelengthMeters": [70, 140], "amplitudeMeters": [1.5, 4.5], "weight": 0.32},
                {"id": "detail", "wavelengthMeters": [18, 45], "amplitudeMeters": [0.2, 1.2], "weight": 0.16},
            ],
            "manualMasks": [
                {"id": "mask_start_plateau", "operation": "flatten_blend", "center": [185, 840], "radiusMeters": 90, "targetHeightMeters": 18, "maximumSlopeDegrees": 8},
                {"id": "mask_river_valley", "operation": "carve_spline", "source": "WORLD_MAP_WATER.json#river_main", "bankBlendMeters": [14, 28]},
                {"id": "mask_lake_basins", "operation": "carve_polygons", "source": "WORLD_MAP_WATER.json#lakes", "bankBlendMeters": [10, 22]},
                {"id": "mask_ridge", "operation": "ridge_spline_plus_erosion", "points": [[735, 670], [790, 770], [860, 880], [970, 920]], "talusAngleDegrees": [26, 36]},
                {"id": "mask_marsh_basin", "operation": "flatten_low", "center": [885, 125], "radiusMeters": 155, "targetHeightMeters": [8.5, 13]},
            ],
            "slopeTargets": {"buildablePercentMinimum": 38, "slopeUnder12DegreesPercent": [45, 58], "slopeOver32DegreesPercentMaximum": 9},
        },
    )
    write_json(
        "WORLD_MAP_WATER.json",
        {
            **meta("water"),
            "river": {
                "id": "river_main",
                "name": "Главная река",
                "flow": "northwest_to_southeast",
                "centerline": [{"x": p[0], "z": p[1], "widthMeters": p[2]} for p in RIVER_POINTS],
                "depthMeters": [0.4, 3.2],
                "bankBlendMeters": [14, 28],
                "navigability": "not_boat_authorized_in_stone_age_design",
            },
            "tributaries": TRIBUTARIES,
            "lakes": LAKES,
            "fishPopulation": {
                "schoolCountPerLake": 4,
                "riverSchoolCount": 8,
                "totalInitialSchoolCount": 24,
                "model": "logistic_growth_with_harvest_pressure",
                "minimumBankWorkPointOffsetMeters": 12,
            },
            "hydrologyValidation": {"allLakesConnectedBySurfaceDrainage": True, "riverTouchesNorthwestAndSoutheastBorders": True},
        },
    )
    write_json(
        "WORLD_MAP_ROADS.json",
        {
            **meta("roads_and_navigation"),
            "routes": ROUTES,
            "crossings": CROSSINGS,
            "chokepoints": CHOKEPOINTS,
            "rules": {
                "mainTrailSurface": "compacted_earth_and_gravel",
                "avoidSlopeAboveDegrees": 22,
                "footpathAvoidSlopeAboveDegrees": 30,
                "riverCrossingRequiresAuthoredCrossing": True,
                "futureBridgeSocketsUseStableIds": True,
            },
        },
    )
    write_json(
        "WORLD_MAP_VEGETATION.json",
        {
            **meta("vegetation"),
            "classes": VEGETATION_CLASSES,
            "zones": VEGETATION_ZONES,
            "distribution": {
                "method": "seeded_poisson_disc_plus_density_masks",
                "minimumInteractiveTreeSpacingMeters": [4.5, 8],
                "startClearingExclusionRadiusMeters": 58,
                "routeClearanceMeters": {"mainTrail": 5, "footpath": 2},
                "shoreReedBandMeters": [2, 12],
            },
            "performanceBudget": {
                "canonicalInteractiveTrees": [3500, 6000],
                "decorativeTreeAndShrubInstances": [9000, 17000],
                "activeTreeCollidersWithinCameraBudget": [300, 650],
                "interactiveBushesAndHerbs": [800, 1500],
                "decorativeGroundInstances": [18000, 35000],
                "verification": ["GPU frame time", "CPU culling time", "draw calls", "visible triangles", "memory", "NavMesh rebuild cost"],
            },
        },
    )
    write_json(
        "WORLD_MAP_RESOURCES.json",
        {
            **meta("resources"),
            "resourceTypes": RESOURCE_TYPES,
            "zones": RESOURCE_ZONES,
            "fishSources": [{"id": f"fish_{lake['id']}_{n}", "lakeId": lake["id"], "schoolOrdinal": n, "initialCapacityUnits": 48} for lake in LAKES for n in range(4)],
            "riverFishSources": [{"id": f"fish_river_{n}", "riverId": "river_main", "schoolOrdinal": n, "initialCapacityUnits": 42} for n in range(8)],
            "mutationAndSaveRules": {
                "stableEntityIdRequired": True,
                "depletedVisibleNodeMustDisappear": True,
                "remainingCapacitySaved": True,
                "regrowthUsesCanonicalState": True,
                "buildingClearanceUsesNormalWorkAndHauling": True,
                "noAutomaticDeletionForUniqueOrUnclearableObjects": True,
            },
        },
    )
    write_json(
        "WORLD_MAP_WILDLIFE.json",
        {
            **meta("wildlife"),
            "species": SPECIES,
            "concentrationZones": WILDLIFE_CONCENTRATIONS,
            "globalRule": "Животные могут появляться во всех подходящих биомах; зоны лишь повышают вероятность и carrying capacity.",
            "simulation": {
                "movement": "habitat_graph_between_feeding_rest_water_nodes",
                "migration": "season_and_pressure_weighted",
                "reproduction": "population_tick_with_carrying_capacity",
                "huntingPressure": "reduces_local_population_and_increases_avoidance",
                "predatorPrey": "future_aggregate_tick",
                "timeOfDay": True,
                "canonicalPopulationTarget": [88, 148],
                "tier1VisibleCap": [20, 35],
                "tier2ActiveCap": [40, 80],
            },
            "currentProjectGap": "Текущая версия представляет охоту ресурсными узлами hunt_animal; видовая мобильная экология ещё не является канонической системой.",
        },
    )
    write_json(
        "WORLD_MAP_POI.json",
        {
            **meta("points_of_interest"),
            "points": POIS,
            "rules": {"uniqueObjectsPersistByStableId": True, "noAutomaticConstructionClearance": True, "discoveryStateSaved": True},
        },
    )
    write_json(
        "WORLD_MAP_BUILDABILITY.json",
        {
            **meta("buildability"),
            "zones": BUILD_ZONES,
            "hardNoBuild": [
                {"id": "no_build_water", "sources": ["WORLD_MAP_WATER.json#river", "WORLD_MAP_WATER.json#lakes"], "reason": "water"},
                {"id": "no_build_unique_poi", "sources": ["WORLD_MAP_POI.json#points"], "reason": "protected_unique_object"},
                {"id": "no_build_cliffs", "source": "WORLD_MAP_TERRAIN.json", "predicate": "slope > 32 degrees"},
            ],
            "clearable": ["tree", "bush", "berry_bush", "loose_stone", "branches", "plant_fiber"],
            "unclearableWithoutSpecificTechnology": ["water", "cliff", "unique_poi", "cave_entrance", "deep_marsh", "ridge_monolith"],
            "expansionDirections": [
                {"id": "expand_west", "from": [185, 840], "to": [95, 705], "priority": 2},
                {"id": "expand_east", "from": [185, 840], "to": [330, 765], "priority": 1},
                {"id": "expand_south", "from": [185, 840], "to": [245, 660], "priority": 3},
            ],
            "grid": {"cellSizeMeters": 2, "visualization": "editor_and_placement_only", "natureMustNotFollowGrid": True},
        },
    )


def write_docs() -> None:
    audit_rows = """
| Область | Найдено в проекте | Разрыв до этой спецификации |
|---|---|---|
| Мир | p109-compact-world-v1, 4×4 Terrain по 250 м, runtime origin (-570,-120) | Нужна новая версия географии, а не правка действующей |
| Рельеф | TiledWorldHeightSampler, 257 samples/tile, общий seed | Нужны авторские маски хребта, долин, болот и новой гидрологии |
| Вода | Одна река и 11 fishing-зон, WaterLevel=8 м | Проект фиксирует ровно четыре разных озера и связанную систему ручьёв |
| Ресурсы | StableEntityId, конечная ёмкость, depletion/regrowth, физические piles | Не хватает природных масок для лекарств, ветвей, волокон, глины и скрытого oil |
| Животные | hunt_animal как ресурсный узел; Deer — presentation | Нет видовой мобильной экологии, миграции и популяционного восстановления |
| Строительство | Общая сетка/occupancy, slope/water checks, normal work clearance | Требуются маски buildability и защищённые уникальные объекты |
| Навигация | NavMeshSurface на каждом Terrain и межтайловые links | Нужны авторские проходы, броды и тест всех маршрутов |
| Сохранения | Versioned snapshot, StableEntityId, отдельные поколения | Новую карту активировать только новым generation/save path |
| Арт | 88 tree, 47 rock, 42 plant, 23 bush, 43 grass, 8 water prefab совпадений; каталог StarterNature | serenity_games_assets как отдельный каталог не найден; использовать только уже допущенные assets |
"""
    (ROOT / "WORLD_MAP_OVERVIEW.md").write_text(
        f"""# Serenity World Map — обзор

Версия карты: {MAP_VERSION}. Статус: завершённый проектный пакет, не активирован в Unity.

## Концепция

Карта площадью 1 км² строится как компактный, но визуально разнообразный Stone Age frontier. Северо-западная стартовая долина открыта в три направления; север удерживает массив леса, северо-восток — высокий каменный хребет, запад — лиственная роща, юг — луга и большой лесной водоём, восток — глинистые возвышенности, юго-восток — болото со скрытым нефтяным резервуаром будущих эпох.

Главная река входит с северо-запада и покидает карту на юго-востоке. Четыре озера связаны с ней ручьями. Главная широкая грунтовая тропа идёт с запада через центр к юго-востоку, а вторичные тропы соединяют старт, водоёмы, хребет, места добычи и POI.

## Координаты

- Проектные координаты: X=0…1000 на восток, Z=0…1000 на север, север сверху.
- Unity-проекция сохраняет текущий origin: unityX = designX - 570, unityZ = designZ - 120.
- Старт: design (185, 840), Unity (-385, 720), поляна диаметром 145 м.
- Разбиение: 4×4 тайла по 250 м; границы heightmap используют общие samples.

## Аудит проекта

{audit_rows}

## Восемь регионов

| ID | Регион | Роль |
|---|---|---|
| A | Стартовая долина | безопасный старт, базовые ресурсы, первые стройки |
| B | Северный лес | древесина, собирательство, северная охота |
| C | Каменный хребет | камень, кремень, будущие руды, пещера |
| D | Западная роща | лиственная древесина, озеро, культурные POI |
| E | Южные луга | охота, земледелие, второе поселение |
| F | Озёрный край | рыбалка, тростник, глина, лесной водоём |
| G | Восточные возвышенности | глина, камень, проходы и обзорные позиции |
| H | Южное болото | ограниченный Stone Age доступ, лекарства, будущая нефть |

## Главное игровое чтение

Игрок начинает не в стене леса, а на мягком плато. Река даёт ориентир и воду, дорожная дуга создаёт основной маршрут, а хребет, озёра и болото делят пространство на узнаваемые цели. Плотность деталей меняется каждые 80–160 м, но свободные зоны сохраняются под расширение.

## Границы текущего этапа

Unity-сцена, P109TiledWorld.json, Terrain, сохранения и runtime-код не изменены. Художественный concept art в images/world_map_concept_art.png служит только направлением: канонические координаты находятся в JSON и технических PNG.
""",
        encoding="utf-8",
    )

    (ROOT / "WORLD_MAP_MASTER_SPEC.md").write_text(
        f"""# WORLD MAP MASTER SPEC

Версия: {MAP_VERSION}
Предлагаемое runtime generation ID: {GENERATION_ID}
Статус: DESIGN COMPLETE / NOT IMPLEMENTED

## 1. Назначение и authority

Этот каталог — единый технический проект карты Serenity Game 1000×1000 м. Каноническими считаются JSON-файлы с одинаковым mapVersion. Изображения являются проверяемой проекцией тех же данных. При расхождении JSON имеет приоритет; расхождение должно блокировать импорт.

Runtime authority после внедрения остаётся в Domain/Simulation: StableEntityId, TiledWorldDefinition, ProductionService, ResourceEcologyService, BuildingPlacementService, WorkManager и SaveCoordinator. Terrain, деревья, вода, животные и маркеры на сцене — disposable presentation.

## 2. Игровые цели

1. Стартовая поляна диаметром 145 м в северо-западной долине, без кольца густого леса.
2. Три естественных направления расширения и доступ к реке в первые минуты.
3. Ровно четыре различимых озера и единая дренажная сеть.
4. Ресурсы представлены природными градиентами, а не прямоугольными монополиями.
5. Животные встречаются во всех подходящих биомах; hunt-зоны только повышают концентрацию.
6. Каменный век не показывает нефть и поздние руды, но их stable deposits существуют.
7. Большинство земли проходимо; крутые склоны и вода формируют осмысленные, а не случайные барьеры.
8. Истощение, расчистка и строительство сохраняются как изменения канонического мира.

## 3. Масштаб и рельеф

- Площадь: 1 000 000 м².
- Terrain: 16 тайлов 250×250 м, heightmap 257×257 на тайл.
- Диапазон высот: 4–96 м; рабочая земля обычно 11–62 м; вода 8 м; пик хребта 82 м.
- Цель: не менее 38% территории пригодно для построек после допустимой расчистки.
- Не более 9% площади имеет склон выше 32°.
- Стартовое плато: 18 м, целевой склон не более 8°.

## 4. Гидрология

Главная река имеет 12 control points и ширину 18–28 м. Западное, горное, центрально-восточное и большое южное озёра имеют разные формы, высоты, глубины и берега. Каждое имеет 3–4 авторские точки рыбалки; итоговый runtime должен выбирать только сухие и NavMesh-доступные landing points. 16 озёрных и 8 речных школ образуют начальные 24 популяции.

## 5. Биомы и растительность

Региональные полигоны задают макронаправление, а визуальная граница смешивается в полосе 35–70 м. Размещение использует seeded Poisson-disc и density masks. Интерактивные деревья отделены от декоративных instanced props. Поляна старта получает exclusion radius 58 м и редкие группы деревьев по краю.

## 6. Ресурсы

Ранние: древесина, ветви, камень, кремень, ягоды, грибы, лекарства, волокно, глина, рыба и животные. Скрытые: медь, железо, золото и нефтяной резервуар под южным болотом. У каждого узла StableEntityId, ёмкость, depletion policy и save-state. Вырубленный объект исчезает; восстановление создаётся только каноническим ecology tick.

## 7. Животные

Видовая цель — 88–148 канонических особей, 20–35 видимых Tier1 и 40–80 активных Tier2. Deer, boar, hare, fox, wolf, bear и waterfowl получают habitat graph, суточную активность, carrying capacity и migration pressure. До реализации этой системы существующий hunt_animal остаётся совместимым упрощением, но не выдаётся за мобильную экологию.

## 8. Дороги и навигация

Главная тропа имеет ширину 5–8 м и поверхность из утоптанной земли/гравия. Семь маршрутов связаны тремя авторскими переправами. Рridge pass, central bridge и marsh causeway — проверяемые chokepoints. Ни одна тропа не пересекает воду вне crossing ID и не режет склон выше своего лимита.

## 9. Строительство

Проверка идёт в порядке bounds → slope → water/special mask → occupancy → settlement rule → blockers. Обычный природный ресурс даёт условно допустимое amber-состояние: после подтверждения обычные WorkOrder освобождают площадку и переносят добытое. Water, cliff, unique POI, cave entrance и deep marsh не удаляются автоматически.

## 10. Производительность

- Interactive trees: 3500–6000 canonical, но colliders активны только в ограниченном радиусе/чанках.
- Decorative vegetation: 9000–17000 trees/shrubs и 18000–35000 ground instances через instancing/LOD.
- Rocks: ориентир 350–650 harvestable и 1500–3000 decorative.
- Wildlife: центральный scheduler; никаких Update на особь.
- Все 16 Terrain можно удерживать для коллизий на 1 км² только после профилирования; detail/animal presentation обязана дистанционно отключаться.
- Gates: 30 FPS минимум, 60 FPS цель; измерять CPU, GPU, память, NavMesh, culling и save size на целевом среднем PC.

## 11. Сохранения и версии

Реализация создаёт новый generation ID {GENERATION_ID} и отдельный путь сохранений. p109-compact-world-v1 остаётся читаемым только своей логикой. Нельзя загружать старые координаты в новую карту или переписывать старые слоты. Сохраняются depletion, regrowth, population, discovered POI, road/bridge state, altered marsh/drainage и все постройки.

## 12. Acceptance gates

1. Все JSON проходят schema/ID/bounds/cross-reference validation.
2. Четыре lakes в данных совпадают с техническими картами.
3. Старт принадлежит region A, не воде, не no-build и имеет три пути.
4. River касается западной/северо-западной и юго-восточной границ.
5. Height samples совпадают на 16 shared tile edges.
6. Каждый fish landing сухой, доступный и связан с source ID.
7. Every region reachable from start without swimming or clipping.
8. Resource depletion and world changes round-trip через Save/Load.
9. Tier materialization не меняет StableEntityId и координаты.
10. Performance gates выполняются до увеличения плотности.

## 13. Файлы

См. WORLD_MAP_COORDINATES.json как индекс, WORLD_MAP_IMPLEMENTATION_PLAN.md как безопасный порядок внедрения и WORLD_MAP_CODEX_TASKS.md как последующие независимые задачи.
""",
        encoding="utf-8",
    )

    (ROOT / "WORLD_MAP_BUILDABILITY.md").write_text(
        """# Строительство и изменение мира

## Состояния placement

- Green: рельеф, границы, вода, ownership, occupancy и blockers допускают немедленное строительство.
- Amber: геометрия допустима, но есть только собираемые препятствия. После подтверждения создаётся площадка, затем NPC получают обычные задачи рубки/сбора/переноса.
- Red: вода, глубокое болото, склон выше лимита, уникальный POI, пещера, защищённый монолит, чужая территория или непреодолимое препятствие.

## Транзакция площадки

1. BuildingPlacementService вычисляет точные cells и perimeter.
2. World surface возвращает slope/water/special-zone verdict.
3. Occupancy проверяет здания, сегменты и подходы.
4. Clearable blockers перечисляются, но не удаляются.
5. Подтверждение создаёт canonical construction site и exclusive clearance WorkOrder.
6. Рабочий физически подходит, добывает resource и переносит его в разрешённое хранилище/кучу.
7. После освобождения cells начинается существующая доставка материалов и build work.
8. Cancel сохраняет уже добытые ресурсы и корректно снимает reservations.

## Геометрические лимиты

- Обычные здания: slope ≤ 18°, если definition не задаёт меньший предел.
- Дороги/поля: предпочтительно ≤ 12°.
- Горные добывающие площадки: до 24° только в специальных масках.
- Береговые постройки: сухая точка, валидная fishing landing, не менее 2 м от water polygon и доступный подход.
- Уникальный объект защищён своим protectionRadiusMeters.
- Природная среда не следует 2-метровой сетке визуально.

## Expansion

Стартовая зона расширяется сначала на восток к мастерским, затем на запад к жилью/складам и на юг к river access. Южные луга поддерживают второе поселение. Озёрный край допускает малый рыболовный outpost, восточные террасы — специализированный clay/stone outpost.
""",
        encoding="utf-8",
    )

    (ROOT / "WORLD_MAP_GENERATION.md").write_text(
        f"""# Алгоритм генерации {MAP_VERSION}

## Детерминированный pipeline

1. Загрузить все JSON и проверить одинаковый mapVersion.
2. Применить дизайн→Unity transform, не изменяя исходные точки.
3. Создать 4×4 shared-coordinate height field: macro noise, meso noise, detail noise.
4. Применить авторские height anchors и ridge mask.
5. Вырезать river/tributary valleys и lake basins; повторно проверить drainage.
6. Сгладить стартовое плато, agricultural clearings, passes и authored crossings.
7. Рассчитать slope, wetness, distance-to-water, soil и buildability rasters.
8. Смешать biome masks в ecotone 35–70 м.
9. Покрасить Terrain layers из одной маски; shared borders берут одинаковые samples.
10. Разместить interactive resources seeded Poisson-disc с StableEntityId от generation+layer+cell+ordinal.
11. Разместить decorative vegetation отдельно, без canonical mutable state.
12. Создать water presentation, dry fishing landings и navigation exclusions.
13. Спроецировать routes по высоте; пересечения воды разрешить только по crossing IDs.
14. Разместить POI и их protection volumes.
15. Сформировать NavMesh, links и habitat graph.
16. Запустить consistency, reachability, save/load и performance gates.

## Seed contract

World seed влияет на микровариации внутри масок, но не перемещает регионы, озёра, реку, дороги, POI и buildability corridors. StableEntityId должен выводиться из mapVersion, world seed, layer ID, quantized cell и ordinal. Порядок загрузки тайлов не влияет на результат.

## Высота

base = 18 м + macro*0.52 + meso*0.32 + detail*0.16. Затем применяются ручные маски. River/lake carve выполняется после noise, но до slope audit. Стартовое плато и проходы применяются последними с smooth blend, чтобы не появлялись резкие террасы.

## Ресурсы и восстановление

Resource masks задают вероятность и ёмкость, но не прямоугольную границу. Depleted interactive node сохраняет состояние и исчезает из presentation. Regrowth проверяет свободное место, occupancy и сезон. Hidden deposits генерируются сразу, но не имеют surface presenter до технологии.

## Животные

Habitat graph связывает food, rest, water и shelter nodes. Aggregate population tick работает вне камеры; Tier1 создаётся в пределах бюджета, Tier2 — для соседних активных зон, Tier3 хранит population cohorts. Охота уменьшает ту же популяцию, а не создаёт отдельную добычу поверх hunt_animal.

## Повторяемость

Генератор технических материалов: generate_world_map_package.py. Он не является Unity-генератором, но служит эталоном координат, изображений и cross-file validation.
""",
        encoding="utf-8",
    )

    (ROOT / "WORLD_MAP_IMPLEMENTATION_PLAN.md").write_text(
        f"""# План внедрения карты в Unity

## Принцип

Не редактировать текущую сцену или p109-compact-world-v1 на месте. Создать {GENERATION_ID}, отдельный Resources JSON и отдельный save directory. Внедрение начинается только после завершения активного P10.9 F1 либо нового явного решения пользователя.

## Использовать существующие authority

- TiledWorldDefinition / TiledWorldDefinitionLoader: расширить polygon/spline layers без параллельного world manager.
- TiledWorldHeightSampler: заменить упрощённый noise pipeline data-driven masks, сохранив pure deterministic API.
- TiledTerrainWorld: проецировать 16 Terrain, water, paint и navigation.
- P109TiledWorldPopulationPresenter: presentation только; stable nodes создаёт Domain plan.
- ProductionService / ResourceEcologyService: depletion, regrowth и save-state.
- BuildingPlacementService / P102WorldPlacementSurface / ConstructionClearanceCoordinator: green/amber/red placement.
- TierManager и OffCameraSimulationService: wildlife/materialization tiers.
- SaveCoordinator / SaveXmlCodec: новая версия payload только при появлении нового canonical state.

## Этапы

1. Data contracts: новые DTO для polygons, spline widths, height masks, vegetation/resource/wildlife layers.
2. Validation: bounds, unique IDs, cross references, non-self-intersecting polygons, shoreline and route rules.
3. Height/hydrology: shared samples, erosion mask, four basins, dry banks.
4. Terrain paint/vegetation: density masks, LOD, instancing, collider budget.
5. Resources: stable natural nodes, hidden deposits, start guarantee, depletion persistence.
6. Wildlife: сначала species/population Domain, затем Tier1/2/3 presentation; не расширять hunt_animal как вечную заглушку.
7. Roads/navigation: terrain projection, three crossings, complete reachability matrix.
8. POI/discovery: protected StableEntityId, discovery and event hooks.
9. Buildability: data layer → existing placement verdict and clearance workflow.
10. Save boundary: isolated generation/path, wrong-generation rejection before rebind.
11. Tests/build/Player: targeted tests, broad affected regression, Windows build and visible route.

## Риски

- Текущий loader жёстко требует p109-compact-world-v1 и 4×4×250; нужен новый parser/version branch.
- Текущий WorldZoneDefinition поддерживает только ellipse/rectangle, поэтому polygons нельзя свести к большим boxes.
- Height sampler сейчас опускает все fishing ellipses до одного WaterLevel; горное озеро требует per-water surfaceY или дизайнерского компромисса.
- Охота сейчас ресурсная, а не видовая мобильная. Не создавать presentation-only популяцию.
- Все 16 Terrain сейчас удерживаются. Это удобно для 1 км², но density budgets должны пройти profiler.
- Существующие пользовательские dirty-файлы не относятся к этой карте и не должны входить в её commit.

## Test matrix

- EditMode: schema, transforms, polygon containment, shared height edges, water connectivity, route/water crossing, start guarantees, StableEntityId determinism.
- PlayMode: all-region reachability, every fishing landing, building green/amber/red, clearable blocker workflow, depletion/regrowth, POI protection, Tier transitions.
- Save/Load: resource remaining, removed trees, wildlife cohorts, discovery, buildings, bridge state, generation rejection.
- Player: камера до всех четырёх краёв, старт→river→lake→ridge→marsh, strategic overview, construction clearance and reload.
- Performance: 100/300/500 NPC gates плюс vegetation/wildlife density tiers.

## Acceptance result for this design stage

Документация, JSON и изображения проверяются локальным генератором. Unity build не требуется на design-only этапе, потому что Assets, Packages, ProjectSettings и сцены не изменены. Runtime implementation не может считаться завершённой без Unity tests/build.
""",
        encoding="utf-8",
    )

    task_specs = [
        ("WM01", "Контракты данных карты", "нет", "Assets/Game/Domain/World; Resources/World", "Добавить polygon/spline/mask DTO и version parser.", "Все JSON загружаются, invalid data fail closed.", "EditMode schema/ID/bounds.", "средне", "нет"),
        ("WM02", "Генератор рельефа", "WM01", "TiledWorldHeightSampler; Terrain authoring", "Noise octaves, anchors, ridge/pass/plateau masks.", "Shared edges bit-identical; slope budget выполнен.", "EditMode shared samples и slope histogram.", "тяжело", "да, с WM03 после contracts"),
        ("WM03", "Гидрология", "WM01,WM02", "World water domain; TiledTerrainWorld", "River widths, tributaries, four lake basins, per-water level policy.", "Ровно четыре озера, сухие берега, связный drainage.", "EditMode hydrology; PlayMode bank checks.", "тяжело", "нет"),
        ("WM04", "Биомы и Terrain paint", "WM02,WM03", "Terrain paint/material projection", "Blend region/ecotone masks and soil layers.", "Нет прямоугольных переходов и tile seams.", "Golden mask/hash и Player screenshots.", "средне", "да, с WM05"),
        ("WM05", "Растительность и LOD", "WM01,WM02", "StarterNatureCatalog; environment LOD", "Seeded distribution, interactive/decorative split, collider culling.", "Плотность в budgets, старт открыт.", "EditMode determinism; profiler gates.", "тяжело", "да, с WM04"),
        ("WM06", "Природные ресурсы", "WM01,WM04", "TiledWorldPopulation; Production/ResourceEcology", "Resource masks, start guarantee, stable IDs, hidden deposits.", "Все ресурсы доступны логично, depletion persists.", "EditMode determinism; Save/Load PlayMode.", "тяжело", "да, с WM07"),
        ("WM07", "Рыба и восстановление водоёмов", "WM03,WM06", "Production sites; ecology", "24 populations, bank points, carrying capacity recovery.", "Каждая landing dry/reachable; overfishing recovers.", "PlayMode all landings; save/reload.", "средне", "нет"),
        ("WM08", "Видовая экология животных", "WM01,WM04", "Domain wildlife; tiers; presenters", "Species populations, habitat graph, migration, reproduction.", "Animals map-wide; zones only multiply density.", "Pure simulation tests; Tier transition PlayMode.", "тяжело", "да, с WM06"),
        ("WM09", "Дороги и навигация", "WM02,WM03", "NavMesh; route presentation", "Seven routes, crossings, passes, links.", "Все регионы доступны без воды/клиппинга.", "Reachability matrix и Player route.", "тяжело", "да, с WM08"),
        ("WM10", "Стартовая территория", "WM04,WM05,WM06,WM09", "New game bootstrap; population plan", "NW start, clearing, basics, expansion corridors.", "Первые стройки не заблокированы; river reachable.", "PlayMode first-15-min contract.", "средне", "нет"),
        ("WM11", "POI и исследование", "WM01,WM04,WM09", "Discovery/event adapters; presenters", "Nine POI, protection, discovery save-state.", "POI уникальны, доступны, не auto-clear.", "EditMode identity; Save/Load PlayMode.", "средне", "да, с WM10"),
        ("WM12", "Строительство и расчистка", "WM02,WM03,WM05,WM10", "BuildingPlacement; clearance coordinator", "Map masks into green/amber/red verdict.", "Clearable blockers use workers; hard blockers fail.", "Focused EditMode/PlayMode matrix.", "тяжело", "нет"),
        ("WM13", "Версия мира и сохранения", "WM06,WM08,WM11,WM12", "SaveCoordinator; SaveXmlCodec; StartupSession", f"Isolate {GENERATION_ID} and persist new states.", "Old saves untouched; wrong generation rejected.", "Codec compatibility and file-hash checks.", "тяжело", "нет"),
        ("WM14", "Оптимизация и профилирование", "WM05,WM08,WM09", "LOD/culling/tier schedulers", "Measure budgets and tune density, not arbitrary counts.", "30 FPS minimum; no unbounded per-agent Update.", "Profiler, memory, 100/300/500 gates.", "тяжело", "да, после feature stability"),
        ("WM15", "Финальная интеграция и visual acceptance", "WM01-WM14", "LocalGameplay; build pipeline; docs", "Broad regression, Windows build, visible route and coordinate audit.", "JSON, runtime and images correspond; user accepts visuals.", "Broad Edit/PlayMode, build, manual route.", "тяжело", "нет"),
    ]
    lines = [
        "# WORLD MAP CODEX TASKS",
        "",
        "Эти задачи не меняют активный NEXT_TASK.md автоматически. Они становятся исполнимыми только после явной постановки и соблюдения текущего TASK_GRAPH.",
        "",
    ]
    for task in task_specs:
        tid, title, deps, files, change, done, tests, difficulty, parallel = task
        lines += [
            f"## {tid} — {title}",
            "",
            f"- Цель: {title}.",
            f"- Зависимости: {deps}.",
            f"- Файлы и компоненты: {files}.",
            f"- Изменить: {change}",
            f"- Готово, когда: {done}",
            f"- Проверка: {tests}",
            f"- Сложность: {difficulty}.",
            f"- Параллельная работа: {parallel}.",
            "",
        ]
    (ROOT / "WORLD_MAP_CODEX_TASKS.md").write_text("\n".join(lines), encoding="utf-8")

    (ROOT / "README.md").write_text(
        f"""# World map package

Каноническая версия: {MAP_VERSION}. Сгенерировано {DATE}.

Начните с WORLD_MAP_OVERVIEW.md, затем WORLD_MAP_MASTER_SPEC.md и WORLD_MAP_COORDINATES.json. Все layer JSON используют метры и север +Z. PNG в images/ созданы из тех же Python-структур. world_map_concept_art.png — единственный неканонический художественный референс.

Повторная генерация:

    C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe docs/world_map/generate_world_map_package.py

Скрипт не читает и не изменяет Unity scene/assets/saves.
""",
        encoding="utf-8",
    )


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
        Path("C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


F_TITLE = font(42, True)
F_SUBTITLE = font(22)
F_LABEL = font(24, True)
F_SMALL = font(18)
F_TINY = font(15)


def xy(point: Iterable[float]) -> tuple[int, int]:
    x, z = list(point)[:2]
    return (
        int(MAP_LEFT + x / 1000 * MAP_SIZE),
        int(MAP_TOP + (1000 - z) / 1000 * MAP_SIZE),
    )


def polygon_xy(points: list[list[float]]) -> list[tuple[int, int]]:
    return [xy(p) for p in points]


def draw_header(draw: ImageDraw.ImageDraw, title: str, subtitle: str) -> None:
    draw.text((MAP_LEFT, 28), title, fill="#10241b", font=F_TITLE)
    draw.text((MAP_LEFT, 78), subtitle, fill="#405248", font=F_SUBTITLE)


def draw_frame(draw: ImageDraw.ImageDraw, technical: bool = True) -> None:
    draw.rectangle((MAP_LEFT, MAP_TOP, MAP_LEFT + MAP_SIZE, MAP_TOP + MAP_SIZE), outline="#17231c", width=5)
    if technical:
        for m in range(0, 1001, 100):
            x = xy([m, 0])[0]
            y = xy([0, m])[1]
            draw.line((x, MAP_TOP, x, MAP_BOTTOM), fill="#ffffff55", width=1)
            draw.line((MAP_LEFT, y, MAP_LEFT + MAP_SIZE, y), fill="#ffffff55", width=1)
            draw.text((x - 10, MAP_BOTTOM + 5), str(m), fill="#24382b", font=F_TINY)
            draw.text((MAP_LEFT - 48, y - 8), str(m), fill="#24382b", font=F_TINY)
        draw.text((MAP_LEFT + MAP_SIZE - 15, MAP_BOTTOM + 36), "X →", anchor="ra", fill="#10241b", font=F_LABEL)
        draw.text((MAP_LEFT - 35, MAP_TOP - 10), "Z", anchor="ms", fill="#10241b", font=F_LABEL)
        draw.polygon([(MAP_LEFT - 35, MAP_TOP + 10), (MAP_LEFT - 44, MAP_TOP + 30), (MAP_LEFT - 26, MAP_TOP + 30)], fill="#10241b")


def region_centroid(region: dict[str, Any]) -> tuple[float, float]:
    pts = region["polygon"]
    return sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts)


def draw_regions(draw: ImageDraw.ImageDraw, labels: bool = False, alpha: int = 255) -> None:
    for region in REGIONS:
        fill = region["color"]
        if alpha < 255:
            rgb = tuple(int(fill[i:i+2], 16) for i in (1, 3, 5)) + (alpha,)
            draw.polygon(polygon_xy(region["polygon"]), fill=rgb, outline="#5c6f60")
        else:
            draw.polygon(polygon_xy(region["polygon"]), fill=fill, outline="#5c6f60")
        if labels:
            cx, cz = region_centroid(region)
            draw.ellipse((xy([cx, cz])[0] - 25, xy([cx, cz])[1] - 25, xy([cx, cz])[0] + 25, xy([cx, cz])[1] + 25), fill="#f8f4df", outline="#263c2c", width=3)
            draw.text(xy([cx, cz]), region["code"], anchor="mm", fill="#183020", font=F_LABEL)


def draw_water(draw: ImageDraw.ImageDraw, labels: bool = False) -> None:
    pts = [xy(p) for p in RIVER_POINTS]
    draw.line(pts, fill="#d9f5fb", width=44, joint="curve")
    draw.line(pts, fill="#2792c9", width=34, joint="curve")
    for tributary in TRIBUTARIES:
        p = [xy(v) for v in tributary["points"]]
        draw.line(p, fill="#d9f5fb", width=15, joint="curve")
        draw.line(p, fill="#3ba2cf", width=9, joint="curve")
    for lake in LAKES:
        draw.polygon(polygon_xy(lake["polygon"]), fill="#d9f5fb", outline="#d9f5fb")
        inner = polygon_xy(lake["polygon"])
        draw.line(inner + [inner[0]], fill="#217fba", width=8, joint="curve")
        draw.polygon(inner, fill="#3198ce")
        if labels:
            draw.rounded_rectangle((xy(lake["center"])[0] - 120, xy(lake["center"])[1] - 18, xy(lake["center"])[0] + 120, xy(lake["center"])[1] + 18), 12, fill="#eefbff", outline="#247da9")
            short = lake["name"].replace("Большое ", "").replace("Центрально-восточное ", "Восточное ")
            draw.text(xy(lake["center"]), short, anchor="mm", fill="#123c50", font=F_TINY)


def draw_routes(draw: ImageDraw.ImageDraw, labels: bool = False) -> None:
    for route in ROUTES:
        pts = [xy(p) for p in route["points"]]
        if route["kind"] == "main_primitive_trail":
            draw.line(pts, fill="#7e552d", width=18, joint="curve")
            draw.line(pts, fill="#c5914d", width=12, joint="curve")
        else:
            draw.line(pts, fill="#f2e4b5", width=7, joint="curve")
            draw.line(pts, fill="#9f7441", width=3, joint="curve")
    for crossing in CROSSINGS:
        cx, cy = xy(crossing["position"])
        draw.rectangle((cx - 14, cy - 8, cx + 14, cy + 8), fill="#805128", outline="#f4e3b1", width=2)
        if labels:
            draw.text((cx + 20, cy - 18), crossing["name"], fill="#3c2a15", font=F_TINY)


def marker(draw: ImageDraw.ImageDraw, point: list[float], color: str, text_value: str, radius: int = 13) -> None:
    cx, cy = xy(point)
    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=color, outline="#ffffff", width=3)
    if text_value:
        draw.text((cx, cy), text_value, anchor="mm", fill="#ffffff", font=F_TINY)


def draw_scale(draw: ImageDraw.ImageDraw) -> None:
    x0, y0 = MAP_LEFT + 30, MAP_BOTTOM - 35
    length = int(MAP_SIZE * 0.2)
    draw.rectangle((x0, y0 - 10, x0 + length // 2, y0), fill="#13251b")
    draw.rectangle((x0 + length // 2, y0 - 10, x0 + length, y0), fill="#faf7e9", outline="#13251b")
    draw.text((x0, y0 + 3), "0", fill="#10241b", font=F_TINY)
    draw.text((x0 + length, y0 + 3), "200 м", anchor="ra", fill="#10241b", font=F_TINY)


def legend(draw: ImageDraw.ImageDraw, items: list[tuple[str, str]], y: int = 1585, columns: int = 4) -> None:
    width = 390
    for i, (color, label) in enumerate(items):
        col, row = i % columns, i // columns
        x = MAP_LEFT + col * width
        yy = y + row * 38
        draw.rounded_rectangle((x, yy, x + 28, yy + 22), 4, fill=color, outline="#304437")
        draw.text((x + 38, yy - 1), label, fill="#1b2b20", font=F_SMALL)


def new_canvas() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGB", (CANVAS, CANVAS), "#f5f1df")
    return image, ImageDraw.Draw(image, "RGBA")


def save_map(image: Image.Image, filename: str) -> None:
    image.save(IMAGES / filename, optimize=True)


def render_overview() -> None:
    image, draw = new_canvas()
    draw_header(draw, "SERENITY — КАРТА МИРА 1×1 КМ", f"{MAP_VERSION} · север +Z · каноническая обзорная схема")
    draw_regions(draw)
    draw_water(draw, True)
    draw_routes(draw)
    for poi in POIS:
        marker(draw, poi["position"], "#8e3c7a", "◆", 11)
    marker(draw, PLAYER_START["designPosition"], "#d44a38", "S", 18)
    for zone in WILDLIFE_CONCENTRATIONS:
        cx, cy = xy(zone["shape"]["center"])
        rx = int(zone["shape"]["radius"][0] / 1000 * MAP_SIZE)
        ry = int(zone["shape"]["radius"][1] / 1000 * MAP_SIZE)
        draw.ellipse((cx-rx, cy-ry, cx+rx, cy+ry), outline="#7a4b2c", width=4)
    resource_markers = [
        ([420, 875], "W"), ([815, 900], "S"), ([900, 480], "C"),
        ([300, 200], "H"), ([885, 125], "O"), ([565, 175], "F"),
    ]
    for point, symbol in resource_markers:
        marker(draw, point, "#755c2a" if symbol != "O" else "#5c2b66", symbol, 10)
    wildlife_markers = [
        ([100, 735], "О"), ([420, 910], "К"), ([610, 790], "В"),
        ([905, 560], "Л"), ([320, 255], "З"), ([735, 210], "П"),
    ]
    for point, symbol in wildlife_markers:
        marker(draw, point, "#315f39", symbol, 10)
    for region in REGIONS:
        cx, cz = region_centroid(region)
        draw.text(xy([cx, cz]), f"{region['code']}  {region['name']}", anchor="mm", fill="#14281d", stroke_width=3, stroke_fill="#f5f1df", font=F_LABEL)
    draw_frame(draw, True)
    draw_scale(draw)
    legend(draw, [
        ("#d44a38", "Старт"), ("#2792c9", "Вода"), ("#c5914d", "Главная тропа"), ("#8e3c7a", "POI"),
        ("#3f7f51", "Густой лес"), ("#c6d989", "Луга"), ("#9da293", "Хребет"), ("#6f9184", "Болото"),
        ("#755c2a", "Ресурсные районы"), ("#315f39", "Животные"), ("#7a4b2c", "Концентрация дичи"),
    ])
    save_map(image, "world_map_overview.png")


def render_regions() -> None:
    image, draw = new_canvas()
    draw_header(draw, "ТЕХНИЧЕСКАЯ КАРТА РЕГИОНОВ", "Сетка 100 м · ID регионов · старт, вода и ключевые точки")
    draw_regions(draw, True)
    draw_water(draw)
    marker(draw, PLAYER_START["designPosition"], "#d44a38", "S", 16)
    for poi in POIS:
        marker(draw, poi["position"], "#704b8c", "", 8)
    draw_frame(draw, True)
    legend(draw, [(r["color"], f"{r['code']} — {r['name']}") for r in REGIONS], columns=2)
    save_map(image, "world_map_regions.png")


def distance_to_polyline(point: tuple[float, float], points: list[list[float]]) -> float:
    px, py = point
    best = 1e9
    for a, b in zip(points, points[1:]):
        ax, ay = a[:2]
        bx, by = b[:2]
        dx, dy = bx - ax, by - ay
        denom = dx * dx + dy * dy
        t = 0 if denom == 0 else max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / denom))
        qx, qy = ax + t * dx, ay + t * dy
        best = min(best, math.hypot(px - qx, py - qy))
    return best


def approximate_height(x: float, z: float) -> float:
    value = 18 + 2.0 * math.sin(x / 82) + 1.4 * math.cos(z / 67) + 0.8 * math.sin((x + z) / 31)
    for anchor in HEIGHT_ANCHORS:
        ax, az = anchor["position"]
        radius = anchor["radiusMeters"]
        influence = math.exp(-((x-ax)**2 + (z-az)**2) / (2 * (radius * 0.62) ** 2))
        value += (anchor["heightMeters"] - 18) * influence
    river_distance = distance_to_polyline((x, z), [[p[0], p[1]] for p in RIVER_POINTS])
    if river_distance < 38:
        value = min(value, 8 + (river_distance / 38) * 10)
    for lake in LAKES:
        if point_in_polygon([x, z], lake["polygon"]):
            value = lake["surfaceY"]
    return max(4, min(96, value))


def height_color(h: float) -> tuple[int, int, int]:
    stops = [(4, (39, 130, 170)), (10, (87, 151, 121)), (20, (169, 199, 115)), (35, (213, 194, 119)), (55, (160, 129, 91)), (75, (125, 118, 108)), (96, (236, 234, 221))]
    for (h0, c0), (h1, c1) in zip(stops, stops[1:]):
        if h <= h1:
            t = (h-h0)/(h1-h0)
            return tuple(int(c0[i] + (c1[i]-c0[i])*t) for i in range(3))
    return stops[-1][1]


def render_height() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА ВЫСОТ И РЕЛЬЕФА", "Диапазон 4–96 м · рабочая вода 8 м · цветовые интервалы и контрольные отметки")
    low = Image.new("RGB", (500, 500))
    pix = low.load()
    for py in range(500):
        for px in range(500):
            x = px / 499 * 1000
            z = (499 - py) / 499 * 1000
            h = approximate_height(x, z)
            band = round(h / 5) * 5
            pix[px, py] = height_color(band)
    low = low.resize((MAP_SIZE, MAP_SIZE), Image.Resampling.BICUBIC)
    image.paste(low, (MAP_LEFT, MAP_TOP))
    draw = ImageDraw.Draw(image, "RGBA")
    draw_water(draw)
    for anchor in HEIGHT_ANCHORS:
        marker(draw, anchor["position"], "#4f3c2c", str(anchor["heightMeters"]), 16)
    draw_frame(draw, True)
    legend(draw, [(f"#{height_color(h)[0]:02x}{height_color(h)[1]:02x}{height_color(h)[2]:02x}", f"{h} м") for h in [5, 10, 20, 35, 55, 75, 95]], columns=4)
    save_map(image, "world_map_height.png")


def draw_shape(draw: ImageDraw.ImageDraw, shape: dict[str, Any], fill: str, outline: str, width: int = 4) -> None:
    if shape["type"] == "polygon":
        draw.polygon(polygon_xy(shape["points"]), fill=fill, outline=outline)
    elif shape["type"] in ("ellipse", "ring"):
        center = shape["center"]
        radius = shape.get("radius", [shape.get("outerRadius", 50), shape.get("outerRadius", 50)])
        cx, cy = xy(center)
        rx = int(radius[0] / 1000 * MAP_SIZE)
        ry = int(radius[1] / 1000 * MAP_SIZE)
        draw.ellipse((cx-rx, cy-ry, cx+rx, cy+ry), fill=fill, outline=outline, width=width)


def render_vegetation() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА РАСТИТЕЛЬНОСТИ", "Плотности — маски размещения, а не жёсткие визуальные границы")
    draw_regions(draw)
    colors = {
        "dense_conifer": "#1f5e3b", "mixed_forest": "#3f8050", "deciduous_grove": "#729a45",
        "open_woodland": "#9db36c", "meadow": "#d6df8c", "marsh_vegetation": "#527d70",
    }
    for zone in VEGETATION_ZONES:
        draw_shape(draw, zone["shape"], colors[zone["classId"]] + "cc", "#244b34")
        center = zone["shape"].get("center")
        if center:
            draw.text(xy(center), zone["id"].replace("veg_", ""), anchor="mm", fill="#ffffff", stroke_width=2, stroke_fill="#163321", font=F_SMALL)
    draw_water(draw)
    draw_routes(draw)
    draw_frame(draw, True)
    legend(draw, [(colors[c["id"]], c["name"]) for c in VEGETATION_CLASSES], columns=3)
    save_map(image, "world_map_vegetation.png")


def render_resources() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА РЕСУРСОВ", "Сплошная линия — Stone Age · пунктирная/пурпурная — скрытые ресурсы будущих эпох")
    draw_regions(draw)
    palette = ["#6b4f2b", "#7d6b5d", "#b63155", "#d99b37", "#3e8f66", "#a66b43", "#bc7c28", "#784d87", "#252525"]
    for i, zone in enumerate(RESOURCE_ZONES):
        hidden = zone["access"] == "technology_locked"
        color = "#6b2e72" if hidden else palette[i % len(palette)]
        draw_shape(draw, zone["shape"], color + ("55" if hidden else "88"), color, 5)
        shape = zone["shape"]
        center = shape.get("center")
        if center:
            label = ", ".join(zone["resources"])
            draw.text(xy(center), label, anchor="mm", fill="#ffffff" if not hidden else "#fff2ff", stroke_width=2, stroke_fill="#321b25", font=F_TINY)
    draw_water(draw)
    for lake in LAKES:
        marker(draw, lake["center"], "#1c76a8", "F", 12)
    draw_frame(draw, True)
    legend(draw, [
        ("#6b4f2b", "Древесина/ветви"), ("#7d6b5d", "Камень/кремень"), ("#b63155", "Ягоды/грибы"),
        ("#3e8f66", "Лекарства/волокно"), ("#a66b43", "Глина"), ("#1c76a8", "Рыба"),
        ("#6b2e72", "Скрытые руды/нефть"),
    ])
    save_map(image, "world_map_resources.png")


def render_wildlife() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА ЖИВОТНЫХ И ОХОТЫ", "Животные встречаются по всей подходящей среде; контуры показывают только повышенную концентрацию")
    draw_regions(draw)
    colors = ["#8b5a2b", "#5e3923", "#a56a35", "#6f5539"]
    for idx, zone in enumerate(WILDLIFE_CONCENTRATIONS):
        draw_shape(draw, zone["shape"], colors[idx] + "55", colors[idx], 7)
        draw.text(xy(zone["shape"]["center"]), f"{zone['name']}\n×{zone['multiplier']}", anchor="mm", fill="#fff8e8", stroke_width=2, stroke_fill="#3c2818", font=F_SMALL)
    distribution_points = [[110, 780], [280, 880], [430, 900], [610, 790], [820, 700], [930, 500], [720, 420], [530, 285], [340, 180], [150, 230], [870, 150]]
    symbols = ["О", "К", "В", "М", "О", "Л", "К", "П", "З", "О", "П"]
    for p, symbol in zip(distribution_points, symbols):
        marker(draw, p, "#264c30", symbol, 12)
    draw_water(draw)
    draw_frame(draw, True)
    legend(draw, [
        ("#8b5a2b", "Повышенная концентрация"), ("#264c30", "Примеры встреч"),
        ("#3f7f51", "Подходящая лесная среда"), ("#c6d989", "Подходящая открытая среда"),
    ])
    save_map(image, "world_map_wildlife.png")


def render_buildability() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА СТРОИТЕЛЬСТВА", "Green — подходит · Amber — расчистка/ограничения · Red — запрет или технология")
    draw_regions(draw)
    colors = {"ideal": "#39a45b", "conditional": "#d59832", "restricted": "#b9473f"}
    for zone in BUILD_ZONES:
        draw_shape(draw, zone["shape"], colors[zone["category"]] + "88", colors[zone["category"]], 6)
        center = zone["shape"].get("center")
        if center:
            draw.text(xy(center), zone["id"].replace("build_", ""), anchor="mm", fill="#ffffff", stroke_width=2, stroke_fill="#253228", font=F_TINY)
    draw_water(draw)
    for poi in POIS:
        cx, cy = xy(poi["position"])
        rr = int(poi["protectionRadiusMeters"] / 1000 * MAP_SIZE)
        draw.ellipse((cx-rr, cy-rr, cx+rr, cy+rr), outline="#b9473f", width=4)
    start = PLAYER_START["designPosition"]
    for expansion in [
        {"to": [95, 705]}, {"to": [330, 765]}, {"to": [245, 660]},
    ]:
        draw.line([xy(start), xy(expansion["to"])], fill="#1f6d3b", width=9)
    marker(draw, start, "#d44a38", "S", 16)
    draw_frame(draw, True)
    legend(draw, [("#39a45b", "Подходит"), ("#d59832", "Условно/расчистка"), ("#b9473f", "Запрет/технология"), ("#2792c9", "Вода")])
    save_map(image, "world_map_buildability.png")


def render_roads() -> None:
    image, draw = new_canvas()
    draw_header(draw, "КАРТА ДОРОГ И НАВИГАЦИИ", "Маршруты, переправы, проходы и узкие места")
    draw_regions(draw)
    draw_water(draw)
    draw_routes(draw, True)
    for choke in CHOKEPOINTS:
        marker(draw, choke["position"], "#b33c31", "!", 14)
        draw.text((xy(choke["position"])[0] + 18, xy(choke["position"])[1] + 12), choke["name"], fill="#5b211d", stroke_width=2, stroke_fill="#f5f1df", font=F_TINY)
    marker(draw, PLAYER_START["designPosition"], "#d44a38", "S", 16)
    draw_frame(draw, True)
    legend(draw, [("#c5914d", "Главная тропа"), ("#9f7441", "Вторичные тропы"), ("#805128", "Переправа"), ("#b33c31", "Узкое место")])
    save_map(image, "world_map_roads.png")


def render_contact_sheet() -> None:
    names = [
        "world_map_overview.png", "world_map_regions.png", "world_map_height.png",
        "world_map_vegetation.png", "world_map_resources.png", "world_map_wildlife.png",
        "world_map_buildability.png", "world_map_roads.png", "world_map_concept_art.png",
    ]
    existing = [IMAGES / name for name in names if (IMAGES / name).exists()]
    thumb = 420
    sheet = Image.new("RGB", (thumb * 3, thumb * 3), "#1a211c")
    draw = ImageDraw.Draw(sheet)
    for i, path in enumerate(existing[:9]):
        img = Image.open(path).convert("RGB")
        img.thumbnail((thumb - 16, thumb - 46), Image.Resampling.LANCZOS)
        x = (i % 3) * thumb + (thumb - img.width) // 2
        y = (i // 3) * thumb + 8
        sheet.paste(img, (x, y))
        draw.text(((i % 3) * thumb + thumb // 2, (i // 3) * thumb + thumb - 28), path.stem, anchor="mm", fill="#f4f1df", font=F_TINY)
    sheet.save(IMAGES / "world_map_contact_sheet.png", optimize=True)


def validate() -> list[str]:
    errors: list[str] = []
    if len(LAKES) != 4:
        errors.append(f"Expected exactly four lakes, got {len(LAKES)}")
    id_groups = [REGIONS, LAKES, ROUTES, CROSSINGS, POIS, HEIGHT_ANCHORS, VEGETATION_ZONES, RESOURCE_ZONES, SPECIES, WILDLIFE_CONCENTRATIONS, BUILD_ZONES]
    for group in id_groups:
        ids = [item["id"] for item in group]
        if len(ids) != len(set(ids)):
            errors.append("Duplicate IDs in group: " + ", ".join(ids))
    all_points: list[tuple[str, list[float]]] = []
    for region in REGIONS:
        all_points += [(region["id"], p) for p in region["polygon"]]
    all_points += [("river_main", [p[0], p[1]]) for p in RIVER_POINTS]
    for lake in LAKES:
        all_points += [(lake["id"], p) for p in lake["polygon"]]
    for route in ROUTES:
        all_points += [(route["id"], p) for p in route["points"]]
    all_points += [(poi["id"], poi["position"]) for poi in POIS]
    for label, p in all_points:
        if not (0 <= p[0] <= 1000 and 0 <= p[1] <= 1000):
            errors.append(f"Out of bounds {label}: {p}")
    region_a = next(region for region in REGIONS if region["code"] == "A")
    if not point_in_polygon(PLAYER_START["designPosition"], region_a["polygon"]):
        errors.append("Player start is not inside Region A")
    regions_by_id = {region["id"]: region for region in REGIONS}
    for poi in POIS:
        if poi["regionId"] not in regions_by_id or not point_in_polygon(
            poi["position"], regions_by_id[poi["regionId"]]["polygon"]
        ):
            errors.append(f"POI is outside assigned region: {poi['id']}")
    for lake in LAKES:
        for landing in lake["fishingLandings"]:
            if point_in_polygon(landing, lake["polygon"]):
                errors.append(f"Fishing landing is inside water: {lake['id']} {landing}")
    for route in ROUTES:
        for route_point in route["points"]:
            for lake in LAKES:
                if point_in_polygon(route_point, lake["polygon"]):
                    errors.append(f"Route vertex is inside lake: {route['id']} {lake['id']} {route_point}")
    if RIVER_POINTS[0][0] != 0 or RIVER_POINTS[-1][0] != 1000:
        errors.append("River endpoints do not touch west/east borders")
    fish_lakes = {source["lakeId"] for source in [{"lakeId": lake["id"]} for lake in LAKES]}
    if fish_lakes != {lake["id"] for lake in LAKES}:
        errors.append("Fish/lake cross-reference mismatch")
    resource_ids = {resource["id"] for resource in RESOURCE_TYPES}
    for zone in RESOURCE_ZONES:
        missing_resources = sorted(set(zone["resources"]) - resource_ids)
        if missing_resources:
            errors.append(f"Unknown resources in {zone['id']}: {missing_resources}")
    species_ids = {species["id"] for species in SPECIES}
    for zone in WILDLIFE_CONCENTRATIONS:
        missing_species = sorted(set(zone["species"]) - species_ids)
        if missing_species:
            errors.append(f"Unknown wildlife in {zone['id']}: {missing_species}")
    if runtime_point(PLAYER_START["designPosition"]) != [
        PLAYER_START["runtimePosition"][0], PLAYER_START["runtimePosition"][2]
    ]:
        errors.append("Player start runtime transform mismatch")
    expected_json = [
        "WORLD_MAP_COORDINATES.json", "WORLD_MAP_BIOMES.json", "WORLD_MAP_TERRAIN.json",
        "WORLD_MAP_WATER.json", "WORLD_MAP_ROADS.json", "WORLD_MAP_VEGETATION.json",
        "WORLD_MAP_RESOURCES.json", "WORLD_MAP_WILDLIFE.json", "WORLD_MAP_POI.json",
        "WORLD_MAP_BUILDABILITY.json",
    ]
    for name in expected_json:
        try:
            data = json.loads((ROOT / name).read_text(encoding="utf-8"))
            if data.get("mapVersion") != MAP_VERSION:
                errors.append(f"Version mismatch in {name}")
        except Exception as exc:
            errors.append(f"Invalid JSON {name}: {exc}")
    return errors


def write_validation(errors: list[str]) -> None:
    expected_images = [
        "world_map_overview.png", "world_map_regions.png", "world_map_height.png",
        "world_map_vegetation.png", "world_map_resources.png", "world_map_wildlife.png",
        "world_map_buildability.png", "world_map_roads.png", "world_map_contact_sheet.png",
    ]
    missing = [name for name in expected_images if not (IMAGES / name).exists()]
    if missing:
        errors.extend(["Missing image: " + name for name in missing])
    status = "PASS" if not errors else "FAIL"
    checks = [
        f"JSON version consistency: {'PASS' if not any('Version mismatch' in e or 'Invalid JSON' in e for e in errors) else 'FAIL'}",
        f"Exactly four lakes: {'PASS' if len(LAKES) == 4 else 'FAIL'}",
        f"Coordinate bounds: {'PASS' if not any('Out of bounds' in e for e in errors) else 'FAIL'}",
        f"Unique stable IDs: {'PASS' if not any('Duplicate IDs' in e for e in errors) else 'FAIL'}",
        f"Start in Region A: {'PASS' if point_in_polygon(PLAYER_START['designPosition'], REGIONS[0]['polygon']) else 'FAIL'}",
        f"River border connection: {'PASS' if RIVER_POINTS[0][0] == 0 and RIVER_POINTS[-1][0] == 1000 else 'FAIL'}",
        f"Technical PNG set: {'PASS' if not missing else 'FAIL'}",
    ]
    detail = "\n".join(f"- {item}" for item in errors) if errors else "- Ошибок не обнаружено."
    (ROOT / "WORLD_MAP_VALIDATION_REPORT.md").write_text(
        f"""# World map validation report

Дата: {DATE}
Версия: {MAP_VERSION}
Результат: {status}

## Checks

{chr(10).join('- ' + item for item in checks)}

## Errors

{detail}

## Команда

    C:/Users/Lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe docs/world_map/generate_world_map_package.py

Проверка является design-package validation. Unity tests/build не запускались, потому что runtime/scene/assets не изменены.
""",
        encoding="utf-8",
    )
    if errors:
        raise SystemExit("\n".join(errors))


def write_manifest() -> None:
    files = []
    for path in sorted(ROOT.rglob("*")):
        if not path.is_file() or path.name == "WORLD_MAP_MANIFEST.json":
            continue
        files.append({
            "path": path.relative_to(ROOT).as_posix(),
            "bytes": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        })
    write_json(
        "WORLD_MAP_MANIFEST.json",
        {
            **meta("manifest"),
            "generator": "generate_world_map_package.py",
            "files": files,
        },
    )


def main() -> None:
    IMAGES.mkdir(parents=True, exist_ok=True)
    write_data()
    write_docs()
    render_overview()
    render_regions()
    render_height()
    render_vegetation()
    render_resources()
    render_wildlife()
    render_buildability()
    render_roads()
    render_contact_sheet()
    errors = validate()
    write_validation(errors)
    write_manifest()
    print(f"Generated {MAP_VERSION}: {len(list(ROOT.glob('*.json')))} JSON, {len(list(ROOT.glob('*.md')))} Markdown, {len(list(IMAGES.glob('*.png')))} PNG")


if __name__ == "__main__":
    main()
