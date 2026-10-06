# Stone Age Technology Tree

Status: canonical U16 content design. Era content is limited to `StoneAge`; later eras belong to U38.

Research cost is deterministic settlement work expressed in game-time ticks. The table presents the configured equivalent in game days at rate 1.0.

| Technology ID | Name | Prerequisites | Cost | Unlocks | Start |
|---|---|---|---:|---|:---:|
| `basic_survival` | Basic Survival | — | 1 day | capability `basic_survival` | yes |
| `gathering` | Gathering | `basic_survival` | 1 day | recipes `gather_berries`, `gather_fiber` | yes |
| `controlled_fire` | Controlled Fire | `basic_survival` | 2 days | capability `controlled_fire` | no |
| `shelter_construction` | Shelter Construction | `basic_survival`, `gathering` | 2 days | building `primitive_shelter` | no |
| `stone_tools` | Stone Tools | `gathering` | 3 days | recipes `gather_stone`, `chop_wood` | no |
| `fiber_working` | Fiber Working | `gathering` | 2 days | capability `fiber_working` | no |
| `woodworking` | Woodworking | `stone_tools` | 3 days | building `storage_basket` | no |
| `hunting` | Organized Hunting | `stone_tools` | 3 days | capability `hunting` | no |
| `food_preservation` | Food Preservation | `controlled_fire`, `hunting` | 4 days | capability `food_preservation` | no |
| `organized_labor` | Organized Labor | `shelter_construction`, `woodworking` | 4 days | capability `organized_labor` | no |
| `primitive_agriculture` | Primitive Agriculture | `controlled_fire`, `fiber_working`, `stone_tools` | 5 days | building `primitive_crop_plot`; recipe `cultivate_berries` | no |
| `weaving` | Weaving | `fiber_working`, `organized_labor` | 4 days | capability `weaving` | no |
| `advanced_stonecraft` | Advanced Stonecraft | `stone_tools`, `woodworking`, `hunting` | 5 days | capability `advanced_stonecraft` | no |
| `stone_age_mastery` | Stone Age Mastery | `food_preservation`, `primitive_agriculture`, `weaving`, `advanced_stonecraft` | 6 days | capability `stone_age_completed`; `StoneAgeCompleted = true` | no |

## Dependency view

```text
basic_survival [start]
├─ controlled_fire ───────────────┬─ food_preservation ───────────────┐
└─ gathering [start]              │                                   │
   ├─ shelter_construction ───────┐│                                   │
   ├─ fiber_working ──────────────┼┼─ primitive_agriculture ──────────┤
   │                              └┼─ organized_labor ── weaving ─────┤
   └─ stone_tools ─┬─ woodworking ┘│                                   │
                   ├─ hunting ─────┘                                   │
                   └─ advanced_stonecraft ─────────────────────────────┤
                                                                       └─ stone_age_mastery
```

The catalog validates unique stable IDs, existing prerequisites, no self-dependency, an acyclic graph, and every typed building/recipe/capability target. Definitions always remain in their U08/U15 catalogs; research controls availability and never clones or removes them.
