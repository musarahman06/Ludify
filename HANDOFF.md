# Handoff log

Newest entry on top. Update this before you push and pass the project on.
Keep each entry short: what changed, what's half-done, what's next, anything that's broken.

---

## 2026-09-26: musarahman (gameplay, milestone 1: world + foundation)

**Done**
- Gameplay module under `Assets/Ludify/Gameplay/` (namespace `Ludify.Gameplay.*`, asmdefs `Ludify.Gameplay` + `Ludify.Gameplay.Editor`).
  `PlayerController`/`OrbitCamera` moved here from `Assets/Scripts/` (GUIDs kept, scene refs intact).
- `Core/`: `GameModeManager` (singleton), `InputRouter`, `GameEvents` hub, `Singleton<T>`, `ZoneId`/`BridgeId`/`GameMode` enums,
  and `LudifyControls.inputactions` (maps: OnFoot, Driving, Inspect, Menu; keyboard/mouse + gamepad).
- `World/`: `WorldLayout` (river curve, bridges, zone bounds), `ZoneVolume`, `ZoneRegistry`, `FastTravelWaypoint`.
- Editor tools (menu **Ludify → World**): city re-layout (buildings moved into blocks so no building sits on a road),
  collider pass (739 colliders; bridge decks lowered flush with the streets), boundaries (river bank walls with gaps only at
  the 4 bridges, bridge side walls, map edge walls), zones + `_LudifySystems` prefab.
- CityMap: river can only be crossed on a bridge; buildings/barriers/map edge are solid. Verified in Play Mode.

**Next (gameplay)**
- Milestone 2: low-poly UI kit + theme system.

**Notes for the import teammate**
- Architecture blueprint for the gameplay side: Content/ (text → lesson JSON via LLM) and a Gallery Theatre that will
  show your `LessonAsset`s through one adapter (`ImportTeamImageSource`). It will call `ILessonImporter.Import(path)`
  with images we extract from documents. Would a bytes-based overload help? `BackendLLMClient` will expect a
  `/lesson/generate` endpoint on `Server/`.

---

## 2026-09-26: BenJPanackal (import pipeline)

**Done**
- Added `.gitattributes` (LF line endings for code and Unity YAML, binaries marked binary). No Git LFS yet.
- Added `CLAUDE.md` (project rules + ownership) and `docs/import-pipeline.md` (import design).

**In progress**
- Nothing yet.

**Next (import)**
- Milestone 1: runtime file pick → PNG/JPG shown as a textured panel in `ImportTest` scene.

**Notes for the gameplay teammate**
- CLAUDE.md lists your folders (`Scenes/`, `Terrain/`, `Kenney/`). The import team won't touch them.
- `.mcp.json` contains a Mac-specific path. See CLAUDE.md about local-scope overrides.
