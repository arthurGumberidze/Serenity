# Stone Age Technology Tree

Status: canonical U16/UI08 second-review content. Era content is limited to `StoneAge`; later eras belong to U38.
The graph contains 33 technologies: one common root, five specialization branches and one shared capstone.
Research cost is deterministic settlement work expressed below as game days at rate 1.0.

## Common root

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `basic_survival` | Basic Survival | — | 1 | Common root and starting knowledge |

## Survival branch

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `gathering` | Gathering | `basic_survival` | 1 | Wild gathering, fishing and gathering sites |
| `controlled_fire` | Controlled Fire | `basic_survival` | 2 | Campfire and cooked meat, fish and berries |
| `shelter_construction` | Shelter Construction | `basic_survival`, `gathering` | 2 | Stone-age hut or primitive shelter |
| `hunting` | Organized Hunting | `stone_tools` | 3 | Hunting and carcass processing |
| `food_preservation` | Food Preservation | `controlled_fire`, `hunting` | 4 | Preserved food, hides and related buildings |
| `primitive_agriculture` | Primitive Agriculture | `controlled_fire`, `fiber_working`, `stone_tools` | 5 | Crops, gardens, pasture and cultivation recipes |
| `seasonal_preparation` | Seasonal Preparation | `food_preservation`, `primitive_agriculture` | 5 | Survival-branch endpoint |

## Production branch

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `stone_tools` | Stone Tools | `gathering` | 3 | Stone/wood extraction and stone tool workshop |
| `fiber_working` | Fiber Working | `gathering` | 2 | Fiber-working capability |
| `woodworking` | Woodworking | `stone_tools` | 3 | Storage basket |
| `organized_labor` | Organized Labor | `shelter_construction`, `woodworking` | 4 | Organized work and civic/defensive buildings |
| `weaving` | Weaving | `fiber_working`, `organized_labor` | 4 | Weaving capability |
| `advanced_stonecraft` | Advanced Stonecraft | `stone_tools`, `woodworking`, `hunting` | 5 | Advanced stonecraft and well where available |
| `industrial_extraction` | Industrial Extraction | `advanced_stonecraft`, `organized_labor` | 5 | Industrial extraction capability |
| `restorative_mining` | Restorative Mining | `industrial_extraction` | 5 | Mine restoration capability |
| `standardization` | Standardization | `restorative_mining`, `weaving` | 5 | Production-branch endpoint |

## Military branch

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `militia` | Settlement Militia | `stone_tools`, `hunting` | 3 | Separates trained defenders from civilians |
| `formation_training` | Formation Training | `militia` | 4 | Formation-training capability |
| `military_organization` | Military Organization | `formation_training`, `organized_labor` | 5 | Raises active squad limit from two to four |
| `military_logistics` | Military Logistics | `military_organization`, `food_preservation` | 5 | Away-from-settlement military logistics |
| `fortification` | Fortification | `military_logistics`, `advanced_stonecraft` | 6 | Military-branch endpoint |

Only residents with a recognized military profession may be members of an active squad. The initial active-squad
limit is two; capability `military_organization` raises it to four.

## Knowledge and medicine branch

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `observation` | Systematic Observation | `basic_survival` | 2 | Repeatable observation capability |
| `herbalism` | Herbalism | `observation`, `gathering` | 3 | Plant identification and preparation |
| `practical_anatomy` | Practical Anatomy | `herbalism`, `hunting` | 4 | Practical treatment knowledge |
| `record_keeping` | Record Keeping | `practical_anatomy`, `organized_labor` | 5 | Durable records and obligations |
| `natural_philosophy` | Natural Philosophy | `record_keeping`, `advanced_stonecraft` | 6 | Knowledge-branch endpoint |

## Society branch

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `kinship` | Kinship Customs | `basic_survival` | 2 | Family duties and inheritance customs |
| `settlement_council` | Settlement Council | `kinship`, `shelter_construction` | 3 | Acknowledged settlement council |
| `exchange_and_measures` | Exchange and Measures | `settlement_council`, `organized_labor` | 4 | Common measures and understandable obligations |
| `land_administration` | Land Administration | `exchange_and_measures`, `record_keeping` | 5 | Tracks fields, homes and duties |
| `customary_law` | Customary Law | `land_administration`, `military_organization` | 6 | Society-branch endpoint |

## Shared capstone

| Technology ID | Name | Prerequisites | Cost | Main effect |
|---|---|---|---:|---|
| `stone_age_mastery` | Stone Age Mastery | `seasonal_preparation`, `standardization`, `fortification`, `natural_philosophy`, `customary_law` | 6 | `stone_age_completed`; `StoneAgeCompleted = true` |

## Dependency overview

```text
Basic Survival
├─ Survival ............. Gathering / Fire → food + agriculture → Seasonal Preparation ┐
├─ Production ........... tools + craft + extraction → Standardization ................. ┤
├─ Military ............. Militia → Formation → Organization → Logistics → Fortification ┤
├─ Knowledge & medicine . Observation → Herbalism → Anatomy → Records → Natural Philosophy├→ Stone Age Mastery
└─ Society .............. Kinship → Council → Measures → Land → Customary Law ............ ┘
```

Cross-branch prerequisites are deliberate: specialized development still depends on settlement capacity, food,
records and command. The catalog validates unique stable IDs, existing prerequisites, no self-dependency, an
acyclic graph, and every typed building/recipe/capability target. Definitions remain in their U08/U15 catalogs;
research controls availability and never clones or removes them.

Old research saves are intentionally not merged or migrated into this graph. Obsolete technology IDs are not
silently aliased to new progress.
