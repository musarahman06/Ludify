# Handoff log

Newest entry on top. Update this before you push and pass the project on.
Keep each entry short: what changed, what's half-done, what's next, anything that's broken.

---

## 2026-09-27 (4): BenJPanackal (themed UI, Esc menu, subject bundles)

**Done** (no teammate files edited)
- **New UI look** from the reference: cream rounded panels with shadows, chunky pill buttons with icon blocks, and the
  Fredoka font (SIL OFL, `Assets/Ludify/Import/Resources/Fonts/`). All icons are drawn in code.
  - Anything built with `UiKit` restyles automatically, including your HUD, shop, dialogue, job HUD and loading screen.
  - Light text written for the old dark panels is auto-darkened so it still reads.
  - IMGUI screens (speedometer, time-trial HUD, car hint) are unchanged.
- **Subject themes:** Science green (DNA/beakers/flasks), Math blue (operators/numbers), History amber
  (books/scrolls/columns), Language coral, Geography teal, CS purple, Art pink, Music indigo.
  Floating symbols appear behind menus.
- **Esc menu:** Resume · Import files · Organize subjects · Settings · Help.
  - It opens only when nothing else (map, viewer, dialogue, shop, prompts, loading, race) is using Esc.
  - It pauses the game.
- **Organize subjects:**
  - every import (file, pasted text, pasted image) is kept in a library and auto-filed by subject;
  - drag items between subject bundles;
  - "Generate questions" makes one question set from a whole bundle;
  - "Make current" sets the theme and which questions games use.

**For teammates:** use `UiKit` colours instead of literals to follow the subject theme. See CLAUDE.md "UI theme".

---

## 2026-09-27 (3): BenJPanackal (Concept vs Traced exhibits)

**Done** (gallery/import only)
- **The add-image panel has three build modes:**
  - **Concept (default):** Gemini recognises what the image is about and builds the best 3D teaching model of it.
    A black-and-white schematic becomes a coloured, working circuit board; a PCB photo becomes a representative
    circuit; an animal photo becomes a stylised model.
  - **Traced:** copies the drawing.
  - **Compare:** builds both on neighbouring pedestals.
- **Wikipedia facts** (free, no key) appear in the viewer, with a "Read more" link.
- **Clicking inside things:** hovering picks the innermost part (a nucleus inside a cell membrane), and Tab cycles overlapping parts.
- **Circuit fixes:** LEDs orient themselves so they light up; unlabelled chips are drawn as chips; concept circuits are always closed loops.

---

## 2026-09-27 (2): BenJPanackal (hands-on 3D exhibits)

**Done** (gallery only; no teammate files or scenes touched)
- **Hands-on viewer:** press **I** at a filled pedestal. The exhibit floats up with its own camera:
  - drag to spin it any way, scroll to zoom, right-drag to pan;
  - F flips it, X explodes it, R resets, double-click zooms to a part, Esc goes back;
  - hover any part for what it is and does.
- **Circuits** become a two-sided circuit board (parts on top, copper and solder underneath) running a real simulation:
  bulbs light, current dots flow, clicking the switch opens or closes the circuit, and tooltips show voltage and current.
- **New 3D kits:**
  - solar systems with animated orbits;
  - cells with see-through membranes;
  - meshing gears that turn at the right ratios;
  - 3D bar charts;
  - geometry solids with measurement lines;
  - flow arrows with moving particles (cycles, food chains).
- Circuit boards hover tilted toward you on their pedestal; other exhibits slowly turn.
- Still 1 Gemini call per image. Old exhibits: use **Add details** in the viewer (1 call) to get per-part explanations.

---

## 2026-09-26 (5): musarahman (City Life: NPCs, little quests, coins & stars, colorful buildings)

Everything installs itself at runtime in CityMap (`CityLifeBootstrap`). **No scene or teammate files edited.**
Code is in `Assets/Scripts/City/`.

**Done**
- `CityColorizer`: every city building (Downtown/Inner/City Outskirts) gets one of 10 hue-shifted copies of the Kenney
  atlas. The choice is seeded by position so it's the same every run, and no two neighbours share a color. Suburbs are unchanged.
- The runtime NavMesh (`NavMeshSurface`, physics colliders, east bank only) builds in about 40 ms.
  16 residents are cloned from the player's look with random shirt, pants, skin, hair and shoes, and wander the city (`Npc`, `NpcFactory`).
- Quests (`QuestManager`, `QuestTemplates`, `QuestTargets`): lost pet (cat/dog with a paw-print trail and a
  "Meow!"/"Woof!" bubble), lost item (sparkles), 5 scattered pages, and parcel delivery. Head icons: yellow **!** = quest,
  blue **?** = hint (3 per quest, getting more precise: area → building color → direction), green **!** = turn in.
  Walk up and press **E** to talk or pick up (blocked in a car, during a time trial, or when the map or a prompt is open).
  Talk to the quest giver again to give up.
- Rewards: coins (pet 40, item 30, pages 50, delivery 25) + 1 star. Titles are Newcomer / Neighbor (1) / Helper (3) /
  Local Hero (6) / City Legend (10). The coin and star wallet with the quest tracker is top-right (`CityHud`). Progress is saved to
  `<persistentDataPath>/Progress/player_progress.json`.

- Paw prints follow the street route (NavMesh path) the pet took from its owner, over the last 70 m. They're
  bigger, with dark pads and a light halo so they show on both roads and pavements.
- A picked-up pet follows you on its own NavMeshAgent, so it stays on the ground and walks around buildings. It runs to keep up,
  hides while you're in a car, and pops back beside you after fast travel or when you get out.
- NPCs with a quest are marked on the minimap and full map with a yellow **!**; the person to return to gets a green **!**.
  Click a marker on the full map to fast travel there (same practice-question gate). Offers stay marked while
  you're on another quest; talking to them then says to come back later.
- `CityNav`: each building's volume is marked not walkable on the NavMesh. Recast only sees a box collider's
  faces, so before this there was walkable floor inside every building and on every roof. NPCs, wander targets,
  lost pets, items and pages only use ground-level points connected to the main street network (checked from a hub point).
- Pets can be heard: a synthesised 3D meow/woof (`AudioClip.Create`, no audio files) is audible from about 35 m
  and gets louder as you get closer. The text bubble still only shows within 15 m.
- Minimap has N/E/S/W on its rim and the full map has a north arrow, matching the compass words in the hints.
- Easier quests (playtest: "near the sand building" meant nothing):
  - Hint NPCs ask a practice question first (`QuestionPrompt.AskAsync`; free if no lecture is imported; Cancel =
    no hint, come back later). They're marked with a blue "?" on the minimap and full map.
  - Each hint draws a search circle on both maps (50 m → 30 m → 12 m, target inside, off-centre). The tracker shows a
    live "Search area: north-east of you, about 70 m" line and the latest hint.
  - Hint 2 names the building color in that color plus plain words ("lavender (light purple)"). A tall cyan
    light pillar with "?" marks that building on the street side facing the target.
  - Targets now spawn 40–120 m from the quest giver (was 60–180 m).

- Clothing store (`Wardrobe`, `StoreView`, `ClothingShop`): about 35 shirts, hats, glasses, pants and shoes (15–150 coins),
  with stripes, caps, beanie, cowboy/top/party hat, crown, round/sun/3D/star glasses, shorts and sneakers. They're
  low-poly pieces (`Accessory_<slot>_*`) added to the player's VisualRoot. Open it from Stella the shopkeeper's stall downtown
  (E; pink "$" on the maps) or anywhere on foot with **B**. Click to try on, buy with coins, wear what you own. It has a
  live preview (the character is drawn alone on layer 31 while the store is open). Owned and equipped items are saved in
  `player_progress.json`. The outfit is applied after residents are cloned, so NPCs don't copy it.
- Time trial: +5 coins per correct lap question (`TimeTrialManager.CoinsPerCorrect`); "Coins earned" shows on the
  finish panel.

**Map changes (BenJPanackal's folder, `Assets/Ludify/Map/`, please review)**
- New `MapMarkers` (static add/remove list of runtime `FastTravelPoint`s, with a `Changed` event).
  `MinimapView` and `FullMapView` draw these alongside the fixed points and move them every frame.
- `FastTravelPoint` gained `Glyph` (icon text, defaults to the first letter) and `Follow` (the marker tracks a
  transform; you land 2.5 m in front of it, facing it). Existing points behave exactly as before.
- Compass: `MinimapView.AddCompass` (N/E/S/W badges; the "Map [M]" label moved up 14 px to clear the N) and a
  north arrow in the full map's top-left corner.
- `FastTravelPoint.Radius`: if above 0, both maps draw a translucent circle of that radius (metres) under the marker
  (`MinimapView.AreaCircle`). Used for quest search areas.

**Notes**
- The UI font (LiberationSans SDF) has no ★ glyph, so the star is a procedural sprite.
- Coins aren't spent on anything yet.

---

## 2026-09-27: BenJPanackal (outdoor art gallery + paste import)

The gallery touches the city area (the NE blocks), but **no scene or teammate files were edited**. Everything is built at runtime.

**Done**
- **Art gallery** (`Assets/Ludify/Gallery/`). At runtime:
  - the `InnerOutskirtsBuildings` blocks (NE, x 290–500, z 297–500) are switched off;
  - each lot becomes a lawn, and 18 large pedestals stand on every other lawn;
  - an entrance gate at the south marks it. Only the gate's posts are solid, so you can walk through it.

  There's a purple **A** "Art Gallery" marker on the map (via `MapMarkers`).
- At an empty pedestal, press **I** (choose a file or paste) or **Ctrl+V** (paste straight away).
  - **Gemini reads the image** and describes it as parts + links. Unity builds a labeled 3D model: circuits with real
    batteries/resistors/bulbs, animated current, molecules, diagrams. Text slides and photos become a framed picture.
  - At a filled pedestal, press **I** to inspect (orbit camera, explanation, Replace / Remove, Esc to go back).
  - Exhibits are saved and reload with no API calls.
- **Paste everywhere:** "Import lecture" now offers file / **paste text** / **paste image** (Snipping Tool, browser
  "Copy image", image links). Pasted screenshots of slides work as lecture material too.
- The game pauses while an import panel is open, and player/camera/car controls are off, so typing doesn't move you.

**For the City Life code (musarahman)**
- The gallery hides those buildings **before or after** City Life starts (order isn't guaranteed). Afterwards it removes
  them from `CityColorizer.Buildings` and re-bakes the `_CityLife` NavMesh, so NPCs walk through the gallery and hints
  never mention removed buildings. If you rebuild the city in that area, tell me and I'll move the gallery rect.
- `LessonFilePicker.IsOpen` is now also true while an import panel is open (your quests already respect it).

**Mac teammates, please test:** Ctrl+V paste on macOS uses `osascript` + `sips`. It's untested on a Mac so far.

---

## 2026-09-26 (4): BenJPanackal (minimap, full map, question-gated fast travel)

Touches the gameplay area (map/HUD/player position), but **no gameplay files or scenes were edited**.
Everything installs itself at runtime in any scene with a `PlayerController`. Gameplay teammate, please review.

**Done**
- `Assets/Ludify/Map/`: circular **minimap** bottom-left (north-up, player arrow, destination icons on the rim).
  Click it or press **M** for the **full map**; Esc/M/X closes. The game pauses while the full map is open.
- The map image is a **live top-down render of the current scene** (at start and every time the map opens),
  so it updates automatically when the world changes. No map image to maintain.
- **Fast travel points:** Racetrack (= player spawn) and Skyscraper (= tallest building under `DowntownBuildings`).
  Add more in `FastTravelPoints.Resolve` (e.g. `AddAtObject(points, "Farm", "FarmHouse", Color.yellow)`).
- Fast travel asks a **practice question** from the imported lectures. Wrong → shows the answer, then another question.
  If no lecture has been imported, travel is free. Must be on foot ("Get out of the car to fast travel").
- Reusable for the racing game: `await QuestionPrompt.AskAsync(title, buttonLabel)` → Correct / Cancelled / NoQuestions,
  and `QuestionPool.Deck` (one shared deck over all imported lectures). See `docs/import-pipeline.md`.

**Notes**
- The map sets `OrbitCamera.yaw` on arrival and disables `PlayerController`/`OrbitCamera`/`VehicleInteraction`
  while the full map is open (restored on close).
- The full map can't be opened during a time trial (it pauses the game); the minimap stays visible.
- Player settings "Run In Background" is off, so play mode pauses when the Unity window loses focus (unchanged).

---

## 2026-09-26 (3): musarahman (Knowledge Time Trial)

**Done**
- Getting into a car (X) starts a **time trial** on the F1 circuit: choose **3 or 5 laps**, then a 3-2-1 countdown; lap counter and times top-left
  (lap, total, last, best, personal bests, quiz score). Checkpoints at 25/50/75% stop shortcuts; "Wrong way" warning.
- **One question per lap** at a random point: slow motion (10%), 15 real seconds, answer with 1-4 or click.
  Right = speed boost; wrong/timeout = short slowdown + 3 s penalty. Questions come from the newest imported lecture.
- **Questions are never reused**: every asked question is recorded per lecture in
  `<persistentDataPath>/TimeTrials/used_questions.json`. When a lecture runs out, the race prompts for a new import
  (or races without questions). Race records are kept per race length.
- **No lecture imported yet** → getting in shows an import prompt (Import lecture / Race without questions).
- Best lap + best total saved to `<persistentDataPath>/TimeTrials/records.json` (overall and per lecture).
- New files: `Assets/Scripts/TimeTrial/` (`TimeTrialManager`, `TimeTrialHud`, `TimeTrialRecords`, `TrackPath`,
  `Resources/LudifyTrackPath.txt`). Set up automatically next to `_VehicleInteraction`; no scene edits.

**Heads-up: small additive edits to teammates' files**
- `CarController`: `HoldForStart`, `ApplyBoost`, `ApplyPenalty`, `ClearEffects`, `PlaceAt` (normal driving unchanged).
- `VehicleInteraction`: static `CarEntered` / `CarExited` events and `BlockExit` (X disabled mid-question).
- `RuntimeWorldColliders.SetUpCars`: also creates `_TimeTrial`.
- `ImportButtonOverlay` (import team): `RequestImport()`, `IsBusy`, `StatusMessage`, `ImportFailed` so the race prompt
  reuses the import flow. The button is hidden during a race and comes back afterwards.

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

## 2026-09-27: therishonsingh (track extension, catch wall, sponsor boards)

**Musa, heads-up:** `Assets/Scripts/TimeTrial/Resources/LudifyTrackPath.txt` now holds the **extended** centreline
(493 points, 2 m apart, 987 m lap, same start line). Your `TrackPath`/checkpoints use it unchanged; no code of yours was
edited. The original loop is still in `Assets/Terrain/Track/f1_path.txt`.

**Done**
- `TrackExtension` (in memory, via the scene processor; saved scene untouched) rebuilds the circuit from that file. The west half
  of the farm is gone, and the track now turns north up a long straight, through a right-left chicane, into a **banked 180°
  left-hander** (radius 20 m, 10° banking, embankment on the outside), then comes back south to rejoin the old SW corner.
  - The whole asphalt is one new mesh (the old one is hidden). New kerbs go on the new corners; old kerbs and barriers from the removed
    corner are switched off; new red/white barriers line the new section.
  - A 1.5 m-high **catch wall** (with collider) runs round the outside of the banked corner, its entry and its exit,
    so cars can't fly off the banking.
  - **Sponsor boards**, F1 style: fictional brands (VOLTEX, ZEPHYRA, Quantix, NOVA-X FUEL, Kestrel Timing, BRIGHTPEAK,
    Orbitron Energy, Pixel Cola, LUDIFY, HEXA TYRES) on every 6th barrier and as banners along the catch wall (TMP text).
  - Grid cars now face the racing direction (east).
- Time trial: **2 questions per lap** (one in each half of the lap, at random points). This is a small edit in Musa's
  `TimeTrialManager.StartLap`/trigger check, and the "Two per lap" wording in `TimeTrialHud`.
- Getting into a car hides the other grid cars, and they reappear, parked, when you get out (`VehicleInteraction`).
- Old kerbs are now trimmed triangle by triangle to the new track edge. Previously a leftover red/white kerb from the removed corner
  crossed the new asphalt.
- **Loading screen** (`LoadingScreen.cs`, appears before the first frame). The player is frozen until every runtime system reports ready:
  `_WorldDressing`, `_TrackExtension`, `FarmGround.Painted`, `_VehicleInteraction`, `QuestManager`, `JobManager`, `_Gallery`,
  `MapSystem.Points`. It has a 30 s safety timeout, and `LoadingScreen.IsLoading` is there for anyone who wants to hold input.
  **If you add a new big runtime system, add a step for it in `LoadingScreen.Build`.**
  - `FarmDressing` stays clear of the new track; the old west farm and corner are painted as grass.

---

## 2026-09-27: therishonsingh (suburb + farm jobs / minigames)

Built on Musa's City Life quest pattern, but as a separate module in `Assets/Scripts/Jobs/`. **None of Musa's or Ben's
files were edited**; it only uses their public APIs (`NpcFactory`, `Npc`, `DialogueBox`, `CityHud.Toast`,
`PlayerProgress.AddQuestReward`, `MapMarkers`, `QuestionPrompt`/`QuestionPool`, `UiKit`, `CityArea.DescribeDirection`).
It installs itself at runtime once City Life is up (`JobsBootstrap`); there are no scene edits.

**Done**
- Flow, same as city quests: yellow **!** giver (also on the maps) → accept → own tracker (progress bar, timer,
  "Next: north-west of you, about 40 m") → talk to the giver mid-job (blue **?**) for **one help, gated by a practice
  question** (free with no lecture) → green **!** → coins + 1 star. One job at a time, independent of city quests. **E** to talk or act.
- Suburbs:
  - **Paperboy Pete, newspaper route by bike** (35 coins, +10 if done in 150 s). `BikeController` is arcade physics on a
    CharacterController, with a pedalling rider copy of the player; E throws a paper at a glowing mailbox within 10 m.
    Help: exact markers + 30 s.
  - **Mrs. Green, lawn mowing** (30). Push the mower over the tall grass to 95%. Help: turbo blade.
- Farm:
  - **Farmer Joe, harvest** 12 ripe crop rows (basket of 6 → crate), 35 coins. Crops are hidden by renderer and restored on cleanup.
    Help: exact markers.
  - **Rosa, raking leaves** 40 clusters (bag of 12 → compost bin), 30 coins. Help: leaf blower.
  - **Gardener Sam, planting**: 6 plots, plant → fill can at well → water → grows 25 s → harvest, 40 coins. Help: fertiliser.
- More questions in the suburb jobs (`JobManager.AskAsync`, skipped with no lecture): every paper-route household
  asks one before you can deliver (the bonus clock pauses while it's open), and the mower stalls at 25/50/75%
  until you answer one. The tracker counts questions answered.
- **Lusher farm** (`FarmDressing`, built in memory by the scene processor; saved scene untouched):
  - a 40x30 m barnyard west of the farmhouse with Quaternius's red **BigBarn**, two silos, a windmill, a chicken coop,
    hay bales and a fence, plus a free garden corner for the planting job;
  - every sparse crop plot is filled out into rows of its crop (green corn / golden wheat patchwork, ~1.8k plants);
  - hedgerows, flower meadows (mostly yellow), an orchard, trees and rocks;
  - `FarmGround` paints green grass with dirt only under the rows, on an in-memory copy of the TerrainData.
- New assets: `Assets/Quaternius/FarmBuildings/` (CC0 "LowPoly Farm Buildings", OBJ+MTL, ~0.7 MB, see
  `Assets/Quaternius/CREDITS.md`). OBJ materials are swapped for URP Lit in the MTL colours at runtime.
- `OrbitCamera` chase cam now also works for Rigidbody-less vehicles (the bike). `VehicleInteraction` ignores X while on the bike.

**Notes**
- Job givers are made with `NpcFactory` (adds a NavMeshAgent). The west bank has no NavMesh, so Unity may log one
  "not close enough to the NavMesh" warning per giver before `MakeStationary()` turns it off. It's harmless.
- E is shared with Musa's talk key and my Q/E camera turn (existing conflict).

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
  and `CarEngineAudio`: recorded V8 loops (`Assets/Audio/Resources/EngineV8/`, **CC-BY-SA 4.0, credit required**,
  see `Assets/Audio/CREDITS.md`) crossfaded by throttle + pitched to rpm, plus a synthesised supercharger whine.
  Cars are faster: ~650 kW, top speed ~210 mph; speedometer reads to 240 mph.
- World colliders (buildings, barriers, stands, pit lane, trees, road props, track surface) are added automatically
  to the in-memory scene on Play/build by `Editor/WorldCollidersSceneProcessor.cs` → `RuntimeWorldColliders`.
  No scene edits. Cars are set up at runtime on scene load.
- `WorldDressing` (same scene processor, in-memory only): the 4 floating bridge tiles are replaced by **arched,
  walkable bridges** (road level at both banks, ~6 m rise mid-river, side walls, mesh collider); **rolling hills
  with ~260 trees** in a 220 m ring around the map (river valley kept open, water extended); **invisible walls**
  around the playable terrain. `FallGuard` on the player and cars resets them to spawn if they end up >3 m below
  the terrain.

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
