# P10.3 validation evidence

- `p103-editmode.xml`: focused P10.3 EditMode, 17/17 passed.
- `p103-playmode.xml`: focused P10.3 PlayMode, 2/2 passed.
- `affected-editmode.xml`: queues/AI/tiers/persistence/P10.2 compatibility, 92/92 passed.
- `affected-playmode.xml`: P10.3/P10.2/P9.4/TPS compatibility, 19/19 passed.
- `full-editmode-diagnostic.xml`: broad diagnostic run, 536/538 before correcting the stale P10.2 version assertion; the other failure is the independently reproducible P8.2 geography test.
- `full-playmode-diagnostic.xml`: broad headless diagnostic run, 141/159; see `docs/P103_HANDOFF.md` for conflict categories.

The Windows Development build succeeded with 0 errors and 44 warnings. Visible real-mouse screenshots are intentionally absent: a Windows firewall/security dialog blocked the Player and automation is not permitted to operate that dialog. P10.3 therefore remains PARTIAL.
