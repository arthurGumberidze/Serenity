# UI01 interactive prototype

Open `index.html` in a desktop browser. Use the bottom navigation or number keys 1-9 to move between settlement,
residents, worker groups, construction, research, dynasty, military, Realm and attention. The local mini-map
opens Realm, which combines the strategic map and future diplomacy.

Construction opens as a compact lower drawer so the world remains visible. The persistent resident tracker
selects and locates any inhabitant with one click; its list scrolls when the population does not fit.

This is a dependency-free review artifact. It contains no game state, network calls or Unity integration.
The reference viewport is 1920×1080 and the layout also supports 1280×720.

To regenerate and verify the reference renders with the bundled runtime:

```powershell
$env:NODE_PATH='C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' verify.cjs
```
