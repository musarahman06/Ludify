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
| Minimap / full map / fast travel | BenJPanackal (built on request; gameplay teammate may take over) | `Assets/Ludify/Map/` |
| Outdoor art gallery (image → 3D exhibits) | BenJPanackal | `Assets/Ludify/Gallery/` |
| Esc menu, UI theme, subject library | BenJPanackal | `Assets/Ludify/Menu/`, `Assets/Ludify/Import/UI/Theme/`, `Assets/Ludify/Import/Library/` |

- Import code uses the `Ludify.Import` namespace and is tested in its own scene
  (`Assets/Ludify/Import/Scenes/ImportTest.unity`). **Do not edit gameplay scenes to test import.**
- The contract between the two areas is a small public API in `Ludify.Import`
  (see `docs/import-pipeline.md`, "Hand-off to gameplay"). Gameplay only calls that API.

## Import pipeline (summary)

Full design: `docs/import-pipeline.md`. Teachers pick files at **runtime** (in the built game, not the Editor).

**Built: lecture text → practice questions** (`Assets/Ludify/Import/`)
- Reads PDF (sent to Gemini as-is), PPTX (slides + speaker notes), DOCX, TXT/MD.
- Gemini (free tier) call 1 researches the topic (Google Search only works on billing-enabled keys;
  free keys automatically fall back to the model's own knowledge); call 2 writes multiple-choice
  questions as JSON. Results are cached by file hash in `persistentDataPath/QuestionBanks/`.
- Gameplay uses `QuestionBankStore` + `QuestionDeck` only (see docs, "Questions API").
- In-game: **Menu (top-left, or Esc) → Import files** (`ImportButtonOverlay.RequestImport()`; there's no
  on-screen import button any more). Progress shows in the tip bar. Gameplay can subscribe to
  `ImportButtonOverlay.LessonImported`.
- **Tip bar** (`Assets/Ludify/Menu/TipBar.cs`) at the top of the screen tells the player what to do where they are
  (racetrack, farm, suburbs, city, gallery, river, driving, bike). Add places/tips in `TipBar.ResolvePlaces`.
- Test: menu **Ludify > Import**, or play `Assets/Ludify/Import/Scenes/ImportTest.unity`.

**Built: minimap + question-gated fast travel** (`Assets/Ludify/Map/`, compiles into Assembly-CSharp so it
can use `PlayerController`/`OrbitCamera`). Runtime-installed, map image is a live top-down render of the scene.
Fast-travel points live in `FastTravelPoints.Resolve`.

**Built: outdoor art gallery** (`Assets/Ludify/Gallery/`). At runtime it replaces the `InnerOutskirtsBuildings`
blocks (NE, x 290–500, z 297–500) with lawns and 18 pedestals. Anyone presses **I** at a pedestal to add an image, or
**Ctrl+V** to paste one. Gemini turns it into a `SceneModel` (parts and links), and `ModelBuilder` builds it from
primitives. Circuits, molecules and diagrams become 3D; anything else becomes a framed picture. Saved in
`persistentDataPath/Gallery`. The image → 3D code lives in `Assets/Ludify/Import/Building/` and is reusable.

**Paste support:** `ImportPanel` (file / paste image / paste text) is used by the lecture import and the gallery.
`ImagePaste` reads clipboard pictures via PowerShell (Windows) or osascript (macOS).
`ModalGuard` pauses the game while a panel is open, and `LessonFilePicker.IsOpen` is true then too.

**Planned: photo → 3D mesh** (panel → depth relief → AI mesh, loaded with glTFast).

Editor-only APIs (`AssetDatabase`, `ScriptedImporter`, Unity AI generators) are fine for dev
tooling but must never be on the runtime path. Teachers use the built game.

**API keys never go in the repo.** Each developer uses their own free Gemini key via the
`GEMINI_API_KEY` env var or a gitignored `ludify_secrets.json` in the project root
(copy `ludify_secrets.example.json`). Don't read, print, or log key values.

## UI theme (everyone)
Build UI with `UiKit` and its colours (`UiKit.PanelColor`, `AccentColor`, `ButtonColor`, `TextColor`,
`MutedTextColor`), not hard-coded colours. Then screens follow the reference look (cream panels, chunky
buttons, Fredoka font) and the **current subject's colours**. Leave text colour unset to get automatic contrast.
`UiKit.Button(..., icon: ThemeArt.Icon("gear"))` adds the reference's icon block.

## Collaboration rules

- Pull before starting. Commit and push before handing off. Update `HANDOFF.md` every time.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` (already in .gitignore).
- Always commit `.meta` files with their assets.
- Only one person edits a given scene at a time. Prefer prefabs over scene edits.
- `.mcp.json` is shared. Don't put personal paths in it; use a local-scope MCP override instead.
- Git LFS is not enabled yet (decide as a team first). Keep large binaries out of the repo where possible.
- Optional: Unity Smart Merge for scenes/prefabs:
  `git config merge.unityyamlmerge.driver "'<UnityEditorPath>/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A"`
