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

**Next (import)**
- Teacher-facing import UI (the test panel is developer-only).
- Images → 3D (see docs).

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
