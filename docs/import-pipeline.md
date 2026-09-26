# Import pipeline: teacher files → 3D objects in Ludify

Owner: import team (BenJPanackal). Namespace: `Ludify.Import`. Folders: `Assets/Ludify/Import/`, `Server/`.

## Lecture text → practice questions (built)

```
 PDF / PPTX / DOCX / TXT / MD
        │ LessonReaderFactory       (PPTX: slides in order + speaker notes; DOCX: paragraphs + tables;
        ▼                             PDF: raw bytes, Gemini reads it natively incl. scans)
 LessonContent ── sha256(file + settings) ──► cache hit? → QuestionBankStore (0 API calls)
        │
        ▼ Gemini call 1: LessonResearcher   (Google Search grounding if the key allows it, else model knowledge)
        ▼ Gemini call 2: QuestionGenerator  (responseSchema JSON → N multiple-choice questions)
        ▼ QuestionValidator                 (exactly 4 choices, no dupes/"all of the above", shuffle answers)
 QuestionBank ──► persistentDataPath/QuestionBanks/<id>.json
```

- **LLM:** Google Gemini API free tier. Default model is `gemini-flash-lite-latest` (largest free
  daily quota; verified working 2026-09-26). Override with `geminiModel` in `ludify_secrets.json` or the `GEMINI_MODEL` env var.
  **Ludify > Import > Check Gemini Setup** validates the key and lists models.
- **Web search:** Google Search grounding returns 429 (quota 0) on free keys. The researcher then
  falls back to the model's own knowledge for the session. Enabling billing on the key turns web search on
  automatically, and source URLs appear in `QuestionBank.Sources`.
- **In-game button:** `ImportButtonOverlay` adds "Import lecture" at the top of every scene and raises
  `ImportButtonOverlay.LessonImported(QuestionBank)`.
- **Cost:** 2 requests per new file. Re-importing the same file is free (cache).
  Changing prompts: bump `PipelineVersion` in `LessonImporter` to invalidate old banks.
- **Keys:** `GEMINI_API_KEY` env var → `<project>/ludify_secrets.json` → `persistentDataPath/ludify_secrets.json`.
- **Free-tier privacy:** Google may use free-tier prompts to improve its products. Don't import
  files containing private student data.

### Questions API (for gameplay)

```csharp
using Ludify.Import;

List<QuestionBank> banks = QuestionBankStore.LoadAll();   // newest first
var deck = new QuestionDeck(banks[0]);
McQuestion q = deck.Next();            // or deck.Next(difficulty: 1)
// show q.Prompt and q.Choices[0..3]
bool correct = deck.RecordAnswer(q, chosenIndex);   // e.g. speed up / slow down
// q.Explanation: one sentence to show after answering
```
Missed questions come back after a few others. Questions are ≤120 chars, choices ≤40 chars,
so they fit a racing HUD.

To import from gameplay code: `await new LessonImporter().ImportAsync(path, progress)` (main thread).

---

## Key constraint: this is a *runtime* import

Teachers use the **built game**, not the Unity Editor. Unity's normal import system
(`AssetDatabase`, `ScriptedImporter`, the Unity AI Assistant's generators) only exists in the Editor.
So the whole pipeline has to work from a built player on Windows and macOS.

## Architecture

```
 Teacher's file (PNG/JPG/PDF/PPTX)
        │  runtime file picker / drag-drop
        ▼
 ┌──────────────── Unity client (Ludify.Import) ────────────────┐
 │ 1. Ingest   → hash file, check local cache                   │
 │ 2. Upload   → UnityWebRequest to backend (job-based)         │
 │ 5. Load     → Texture2D.LoadImage / glTFast for .glb         │
 │ 6. Cache    → Application.persistentDataPath/imports/<hash>/ │
 └──────────────────────────────┬───────────────────────────────┘
                                │ HTTP (submit job → poll → download)
 ┌──────────────── Backend (Server/, Python FastAPI) ───────────┐
 │ 3. Normalize → PPTX→PDF (LibreOffice headless) → PNG/page    │
 │ 4. 2D→3D     → per-image: tier A / B / C (below)             │
 │    returns   → manifest.json + PNG/GLB files                 │
 └──────────────────────────────────────────────────────────────┘
```

Why a backend instead of doing everything in Unity:
- Converting PPTX/PDF is easy in Python (LibreOffice, PyMuPDF) and hard inside a Unity player.
- AI 3D generation needs a GPU and/or paid API keys. Keys must never ship inside the game build.
- One backend works for both the Mac and the Windows builds.
- 3D generation takes seconds to minutes, so the API is job-based: `POST /jobs` → `GET /jobs/{id}`
  → download results.

## The three 2D→3D tiers

Not every image should become a full 3D mesh. A slide full of text is best shown as a panel.
A photo of a volcano or a heart can become a real object. Choose the tier per image:

| Tier | What it makes | How | Good for | Cost/speed |
|---|---|---|---|---|
| **A. Panel** | Textured 3D board/card in the world | Quad + image texture, no AI | Text slides, charts, anything | Free, instant |
| **B. Depth relief** | Image pushed out into a 2.5D surface | Monocular depth model (e.g. Depth Anything family) → displaced mesh | Photos, diagrams, maps | Cheap. Can even run in-Unity via Inference Engine (`com.unity.ai.inference`, already installed) |
| **C. Full object** | Real 3D model (GLB) you can walk around | Background removal → image-to-3D model (open: TRELLIS, Hunyuan3D, Stable Fast 3D; hosted: Meshy, Tripo, etc.) | Single clear object: organs, animals, artifacts, molecules | GPU or paid API, ~10s–minutes |

Always produce tier A as a fallback so the game shows *something* immediately, then upgrade to
B/C when ready. Image-to-3D models change fast. Benchmark 2–3 current options on real teacher
images before committing to one, and keep the model behind an interface on the backend so it can be swapped.

Later: let the teacher (or an LLM classifying the image) pick the tier, and crop individual objects
out of a slide (segmentation) before sending them to tier C.

## Unity-side packages

- **glTFast** (`com.unity.cloud.gltfast`): runtime GLB/glTF loading. Works in built players on Mac and Windows.
- **Runtime file picker**: Unity players have no built-in cross-platform file dialog. Use a
  standalone file browser plugin or drag-and-drop.
- **Inference Engine** (`com.unity.ai.inference`, already in manifest): optional on-device depth for tier B.

## Hand-off to gameplay (the contract)

Gameplay code should only depend on this API, not on how import works internally:

```csharp
namespace Ludify.Import
{
    public enum LessonAssetKind { Panel, Relief, Model }

    public sealed class LessonAsset
    {
        public string Id;              // content hash
        public string SourceFileName;
        public int PageIndex;
        public LessonAssetKind Kind;
        public GameObject Root;        // ready to place; has a collider, scaled to ~1m
        public Texture2D Thumbnail;
    }

    public interface ILessonImporter
    {
        // Emits tier A immediately, then upgrades as better tiers finish.
        event System.Action<LessonAsset> AssetReady;
        void Import(string filePath);
    }
}
```

The gameplay teammate decides where and how `LessonAsset.Root` gets placed and interacted with.
The import team guarantees every `Root` is sensibly scaled, centered, and has a collider.

## Milestones

1. **Local images → panels**: runtime pick PNG/JPG → tier A panel in `ImportTest` scene. No backend.
2. **GLB loading**: load a hand-made/downloaded `.glb` at runtime with glTFast, normalize scale + collider.
3. **Backend skeleton**: FastAPI job API; PDF/PPTX → page PNGs; Unity uploads and shows pages as panels.
4. **Tier C**: backend calls an image-to-3D model, returns GLB, Unity swaps panel → model.
5. **Tier B**: depth relief (backend or on-device).
6. **Caching + persistence**: re-opening a lesson doesn't re-run AI.
