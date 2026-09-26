# Handoff log

Newest entry on top. Update this before you push and pass the project on.
Keep each entry short: what changed, what's half-done, what's next, anything that's broken.

---

## 2026-09-26 (2): BenJPanackal (lecture → practice questions)

**Done**
- `Assets/Ludify/Import/`: import lecture files (PDF, PPTX, DOCX, TXT/MD) → Gemini researches the
  topic → 20 multiple-choice questions saved as a question bank. See `docs/import-pipeline.md`.
- In-game: small **Import lecture** button at the top of the screen (every scene, no scene edits).
  Gameplay can listen to `ImportButtonOverlay.LessonImported`.
- Free Gemini keys can't use Google Search, so research uses the model's own knowledge (automatic fallback).
- Test it with menu **Ludify > Import**, or play `Assets/Ludify/Import/Scenes/ImportTest.unity`.
- Gameplay API: `QuestionBankStore.LoadAll()` + `QuestionDeck.Next()` / `RecordAnswer()`.

**Packages added to `Packages/manifest.json` (heads-up, shared file)**
- `com.yasirkula.simplefilebrowser` (runtime file picker that works in Mac/Windows builds)
- `com.unity.nuget.newtonsoft-json` (was already installed indirectly, now explicit)

**Every teammate: set up your own free Gemini key (2 min)**
1. Go to https://aistudio.google.com → "Get API key" → create a key (no credit card).
2. Mac: copy `ludify_secrets.example.json` to `ludify_secrets.json` in the project folder and paste
   your key into `geminiApiKey`. The file is gitignored. **Never commit it.**
   Windows: same, or set a user environment variable `GEMINI_API_KEY`.
3. Restart Unity → **Ludify > Import > Check Gemini Setup** should say "Key works".

**Team decision: target platforms are macOS + Windows only** (no Chromebook/WebGL, since we have no
ChromeOS device to test on). The runtime file picker relies on desktop file access.

**Next (import)**
- Teacher-facing import UI (the test panel is developer-only).
- Images → 3D (see docs).

---

## 2026-09-26: therishonsingh (CityMap performance + camera controls)

Touches the gameplay area (scene, player camera, render settings). Gameplay teammate, please review.

**Done**
- Performance for low-end machines/Chromebooks, same look:
  - Quality: `Mobile` level renamed **Low** (now allowed on desktop too); vsync on for both levels (was uncapped).
  - `PC_RPAsset`: 4→2 shadow cascades, soft shadows High→Medium, HDR off (camera has no post-processing),
    depth/opaque textures off, additional-light shadows off.
  - `Mobile_RPAsset` (Low): HDR off, render scale 0.85, shadow distance 40.
  - CityMap: camera far clip 1000→560 (fog ends at 520, so no visible change); terrain no longer casts shadows,
    draw-instanced on, pixel error 5→8, basemap 1000→250, tree distance 5000→500.
- `OrbitCamera`: no more shake when sprinting (smoothed follow + smoothed collision distance).
- Trackpad/no-mouse look: left or right click-drag, two-finger swipe to look, Ctrl/Cmd+scroll or +/- to zoom,
  Q/E turn, R/F tilt. Mouse-wheel zoom now needs Ctrl/Cmd.
- Driveable grid cars: walk up and press **X** to get in/out. W/S throttle/brake-reverse, A/D steer, Space handbrake.
  `CarController` (WheelColliders, RWD, aero drag + downforce, anti-roll, speed-sensitive steering, ~270 km/h),
  `VehicleInteraction` (enter/exit + hint), camera chases behind the car.
  Automatic 6-speed gearbox + rpm model, small analogue `Speedometer` (bottom right, IMGUI, no assets),
  and `CarEngineAudio`: procedurally synthesised supercharged V8 (no audio files, WebGL-safe).
- World colliders (buildings, barriers, stands, pit lane, trees, road props, track surface) are added automatically
  to the in-memory scene on Play/build by `Editor/WorldCollidersSceneProcessor.cs` → `RuntimeWorldColliders`.
  No scene edits. Cars are set up at runtime on scene load.

**Next**
- Decide how Chromebooks will run the game (WebGL, Android or Linux build); Low tier is safe for all three.

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
