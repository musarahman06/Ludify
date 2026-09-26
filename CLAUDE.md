# Ludify

An educational Unity game. Teachers import their own lesson files (slides, images) and the game
turns them into 3D objects that students can explore and interact with.

- Unity **6000.6.3f1**, URP. Everyone must use this exact editor version.
- Team is on mixed platforms (macOS + Windows). Keep everything cross-platform: use
  `Path.Combine` / `Application.persistentDataPath`, never hard-coded `C:\` or `/Users/...` paths.
- Target builds: Windows and macOS.

## Who owns what

Stay inside your own area. If you need a change in someone else's area, note it in HANDOFF.md
rather than editing their files.

| Area | Owner | Folders |
|---|---|---|
| Gameplay: scenes, map, player, mechanics | Gameplay teammate | `Assets/Scenes/`, `Assets/Terrain/`, `Assets/Kenney/`, `Assets/Ludify/Gameplay/` |
| File import → 3D pipeline | BenJPanackal (import team) | `Assets/Ludify/Import/`, `Server/` |

- Import code uses the `Ludify.Import` namespace and is tested in its own scene
  (`Assets/Ludify/Import/Scenes/ImportTest.unity`). **Do not edit gameplay scenes to test import.**
- The contract between the two areas is a small public API in `Ludify.Import`
  (see `docs/import-pipeline.md`, "Hand-off to gameplay"). Gameplay only calls that API.

## Import pipeline (summary)

Full design: `docs/import-pipeline.md`. In short:

1. Teacher picks a file at **runtime** (in the built game, not the Editor).
2. A backend service in `Server/` normalizes it to images (PDF/PPTX → one PNG per page).
3. Each image becomes 3D through one of three tiers: textured panel → depth relief → AI mesh (GLB).
4. Unity loads the result at runtime (glTFast for GLB) and caches it by file hash.

Editor-only APIs (`AssetDatabase`, `ScriptedImporter`, Unity AI generators) are fine for dev
tooling but must never be on the runtime path. Teachers use the built game.
Never put API keys in the Unity project. They belong on the backend.

## Collaboration rules

- Pull before starting. Commit and push before handing off. Update `HANDOFF.md` every time.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already in .gitignore).
- Always commit `.meta` files with their assets.
- Only one person edits a given scene at a time. Prefer prefabs over scene edits.
- `.mcp.json` is shared. Don't put personal paths in it; use a local-scope MCP override instead.
- Git LFS is not enabled yet (decide as a team first). Keep large binaries out of the repo where possible.
- Optional: Unity Smart Merge for scenes/prefabs:
  `git config merge.unityyamlmerge.driver "'<UnityEditorPath>/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A"`
