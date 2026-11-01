# Research balance — UI08-R4 proposal, not measured gameplay

The existing `Assets/Game/Simulation/Time/GameClock.cs` advances calendar ticks at 60x normal real time:
one calendar day takes 24 real minutes. Work is still measured in equivalent researcher calendar days.
The prototype scales authored difficulty budgets by 0.8 and rounds to half-days: introductory 1.5–2.5,
developed 3–4, key 5–5.5, epoch 6.5 person-days. Starting knowledge is free.

Proposed capacity: 4 effective researchers, raised to 5 by record keeping and 6 by knowledge transfer.
Surplus workers must not yield unbounded acceleration. Existing worker skill/health/fatigue still applies.
Observation/calendar/systematization propose cumulative research modifiers capped at +20%; these are not
currently implemented. Exact contribution sharing belongs to the subsequent approved implementation.

At 4 fully productive workers and mean skill factor 1.1, real minutes = work-days × 24 / 4.4.
The following sums include the 6.5-day epoch project, exclude free starting knowledge, and ignore proposed
science bonuses. They do not include sleep, travel, material acquisition or waiting for practical gates.

| Demonstration route | Learned technologies | Work-days including epoch | Ideal real hours at ×1 |
|---|---:|---:|---:|
| Peaceful | 17 | 54.5 | 5.0 |
| Knowledge | 17 | 56 | 5.1 |
| Military | 16 | 57 | 5.2 |
| Mixed / shortest supplied | 15 | 45.5 | 4.1 |

The requested 3–6-hour target is a design target, not a validated playthrough. Science can shorten these
estimates; interrupted work and settlement development can lengthen them beyond six hours. After approval,
use a focused gameplay check and adjust data instead of adding artificial time gates. ×2/×4 simulation speed
approximately halves/quarters real duration when worker activity and other constraints are unchanged.

Only selected technologies consume existing wood/stone, once at start. Other gates use current stocks
(hides/herbs/food), built shelter and military profession counts. No new resource or fake production-history
counter is introduced in this prototype. All demo facts can be edited through «Условия поселения».

R4's branch-local prerequisites add animal observation to the supplied mixed preset. Costs are unchanged;
only the required dependency closure changes. The shorter theoretical 14-technology route is not timed here.

Focused verification command: `node docs/prototypes/research-review-v4/verify.cjs`.
