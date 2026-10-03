# U02 package and configuration baseline

Source: FRS_Unity.docx 42.1–42.4 and D-008. Editor remains Unity 6000.6.4f1 (12bfff696524).
Exact direct versions live in Packages/manifest.json; Unity generates Packages/packages-lock.json for the complete dependency graph. Commit both. Do not replace pins with `latest`, Git branches, local paths or floating ranges.

| Capability | Package | Pin |
|---|---|---|
| Tier2 ECS | com.unity.entities | 6.6.0 |
| Burst | com.unity.burst | 2.0.0 |
| Collections | com.unity.collections | 6.6.0 |
| Mathematics | com.unity.mathematics | 1.4.0 |
| Camera tools | com.unity.cinemachine | 6.6.0 |
| Content loading | com.unity.addressables | 2.11.2 |
| Input | com.unity.inputsystem | 1.20.0 |
| GameObject navigation | com.unity.ai.navigation | 2.0.12 |
| Rendering | com.unity.render-pipelines.universal | 17.6.0 |
| Tests | com.unity.test-framework | 1.8.0 |

Versions were selected from the installed Editor's Resources/PackageManager/Editor/manifest.json and BuiltInPackages/*/package.json, rather than assuming older Unity 6 package versions. In this Editor, Entities, Collections and Cinemachine are built-in packages. Burst 2.0.0 and Mathematics 1.4.0 are compatibility packages for APIs shipped in engine modules. Jobs ships with the engine; no obsolete standalone com.unity.jobs dependency is needed. The Editor pin is therefore part of the reproducible dependency contract. Existing template tooling packages are retained.

Baseline settings already serialized in ProjectSettings are retained: Windows x64, Mono for development, Linear color space, URP assets for default/quality rendering, Input System only (activeInputHandler=1), Force Text serialization and Visible Meta Files. The existing Enter Play Mode configuration disables both domain and scene reload (options enabled, flags=3). This is retained; future bootstrap tasks must reset static state and handle session lifetime explicitly. Entities generated its empty project-level client build filter in ProjectSettings/EntitiesClientSettings.asset. Burst uses Editor defaults; no machine-specific preferences are saved as project configuration.

No runtime asmdef needs a new package reference while it contains only layer metadata. Explicit references belong to the task introducing package API usage. Tests check actual package registration, compiled runtime package assemblies and engine-module APIs without introducing gameplay. Addressables is installed and compiled; content groups/catalogs/profiles are created when content is introduced, not as empty U02 artifacts. Entities Graphics is not required by the FRS package profile; a renderer can be selected when U12 needs one.

Run `./Tools/Verify-U02.ps1` with the pinned Editor and Windows build support installed. It checks all EditMode tests, builds the Windows Development player and rejects manifest/lock drift. A fresh checkout must contain only tracked source before its first launch (no Library/Temp); shared Unity package/download caches are allowed. See U02_HANDOFF.md for the recorded clean-checkout verification and exact commands.
