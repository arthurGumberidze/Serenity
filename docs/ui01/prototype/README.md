# UI01 interactive prototype

Open `index.html` in a desktop browser. Use the left rail or the numbered keys shown by the prototype to move
between the HUD, resident, building, settlement, construction, research, dynasty, events, army, map and future
diplomacy states.

This is a dependency-free review artifact. It contains no game state, network calls or Unity integration.
The reference viewport is 1920×1080 and the layout also supports 1280×720.

To regenerate and verify the reference renders with the bundled runtime:

```powershell
$env:NODE_PATH='C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules'
& 'C:\Users\Lenovo\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' verify.cjs
```
