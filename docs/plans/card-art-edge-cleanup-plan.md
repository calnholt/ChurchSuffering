# Precision Card-Art Edge Cleanup Implementation Plan

## Document Status

- **Status:** Ready for implementation.
- **Repository:** `Crusaders30XX`.
- **Target assets:** PNG files under `Content/CardArt`.
- **Implementation runtime:** Python 3.14 with pinned image-processing dependencies.
- **Default generated output:** Ignored files under `debug/card-art-cleanup/`.
- **Source-asset policy:** Never overwrite or otherwise modify an input image.
- **Content-pipeline policy:** Do not update `Content/Content.mgcb` or point the game at cleaned images automatically.
- **Required final verification after implementation:** Run the Python tests and `dotnet build` from the repository root.

---

## 1. Objective

Create a purpose-built, non-destructive command-line tool that detects and cleans white-background remnants around already-cropped card-art PNGs. The artifacts may include:

- Opaque white slivers left outside the intended silhouette.
- Semi-transparent white pixels introduced by antialiasing against a white source background.
- Pale halos visible when the art is composited over dark or saturated backgrounds.
- Irregular, locally contaminated edge segments rather than a complete white background.

The central difficulty is that the art also contains intentional white and ivory material: armor, cloth, weapons, feathers, holy effects, stars, rings, and highlights. The tool must therefore use alpha topology, edge direction, local color continuity, and deeper interior pixels rather than globally erasing near-white colors.

The primary optimization target is precision. If a region cannot be classified safely, preserve it and flag it for review. A small amount of residual fringe is preferable to damaging intentional artwork.

All 67 current CardArt assets may be converted in a batch run. Manual visual acceptance is limited to a representative sample of 10 assets; the other outputs still receive automated analysis, hashes, metrics, and report entries.

---

## 2. Product Decisions and Safety Guarantees

### 2.1 Non-destructive operation

- Resolve source and output paths before processing begins.
- Reject any configuration in which an output file could resolve to an input file.
- Never rename, rewrite, optimize, or touch timestamps on source PNGs.
- Record SHA-256 hashes for every source before processing and confirm that the hashes remain unchanged when the run completes.
- Create a unique timestamped run directory so a new run cannot silently replace an earlier result.
- Copy unchanged images into the run's `cleaned/` directory so every source has a corresponding candidate version.

### 2.2 Conservative classification

Each candidate contour region ends in exactly one of these decisions:

- **Clean:** Evidence is strong enough to modify the edge automatically.
- **Preserve:** Evidence indicates intentional artwork or no relevant contamination.
- **Flag:** Evidence is relevant but ambiguous; leave pixels unchanged and add the region to review.

Automatic cleaning requires both the deterministic safety guards and a calibrated fringe confidence of at least `0.995`. A learned score cannot bypass the safety guards.

### 2.3 Crisp output

- Preserve the source width and height exactly.
- Do not resize, sharpen, globally denoise, blur, or feather the art.
- Perform color calculations in linear light and write lossless RGBA PNGs.
- Modify only the narrow candidate band around existing transparency.
- Reconstruct surviving foreground RGB rather than leaving white-contaminated RGB under partial alpha.
- Write `(0, 0, 0, 0)` for fully transparent output pixels to prevent hidden RGB from producing later filtering artifacts.
- Preserve relevant ICC profile and DPI metadata when Pillow exposes it safely; omit unrelated authoring metadata rather than introducing nondeterministic output.

### 2.4 Scope boundaries

- This tool processes cropped RGBA PNGs; it is not a general-purpose subject-segmentation or background-removal application.
- It does not use generative AI, replace artwork, invent missing details, or redraw silhouettes.
- It does not change game code, card definitions, content references, XNB files, or existing source assets.
- Existing GIMP cleanup scripts remain untouched and are not part of this implementation.

---

## 3. Deliverables

Add the following implementation artifacts:

- `scripts/card_art_edge_cleanup.py`: command-line entry point and processing implementation.
- `scripts/requirements-card-art-cleanup.txt`: exact dependency pins.
- `scripts/tests/test_card_art_edge_cleanup.py`: synthetic and workflow tests using `unittest`.
- `docs/card-art-edge-cleanup.md`: setup, command examples, report interpretation, and recovery guidance.
- A root `.gitignore` entry for `/.venv-card-art-cleanup/`.

Runtime results and collected labels live under the already-ignored `debug/` root and are not committed by default.

---

## 4. Command-Line Interface

The script exposes three subcommands.

### 4.1 `run`

```bash
python scripts/card_art_edge_cleanup.py run [INPUT] [options]
```

Behavior:

- `INPUT` may be one PNG or a directory.
- Default input is `Content/CardArt`.
- Directory input discovers `.png` files in deterministic lexical order.
- Default output root is `debug/card-art-cleanup/runs/`.
- Each run creates `<UTC timestamp>-<short run id>/`.
- If `debug/card-art-cleanup/model.json` contains an accepted model with the current feature schema, use it in addition to deterministic guards.
- If no accepted model exists, use the deterministic prior score only.
- Continue after an individual image failure, record the failure, and exit nonzero after the report is written.

Supported options:

- `--output-root PATH`: override the run root without changing non-destructive checks.
- `--model PATH`: use a specific accepted model.
- `--no-model`: use deterministic scoring only.
- `--feedback PATH`: use exact region overrides from a specific feedback file.
- `--candidate-band N`: diagnostic override for the scale-derived band width; normal production runs omit it.
- `--background-color RRGGBB`: override the assumed source background; default is `FFFFFF`.
- `--fail-fast`: stop after the first image error, while still writing the partial manifest.
- `--verbose`: print per-image and per-region decisions.

The run directory contains:

```text
<run>/
  cleaned/
    <relative input paths>.png
  diagnostics/
    <image stem>/
      comparison.png
      decisions.png
      regions/
        <region id>.png
  manifest.json
  report.html
```

### 4.2 `review`

```bash
python scripts/card_art_edge_cleanup.py review [RUN_DIR] [options]
```

Behavior:

- Default to the most recent completed run in `debug/card-art-cleanup/runs/`.
- Bind a local-only HTTP server to `127.0.0.1` on an automatically selected port unless `--port` is supplied.
- Open the report in the default browser unless `--no-open` is supplied.
- Allow each flagged contour region to be labeled `fringe`, `intentional`, or `unsure`.
- Append labels to `debug/card-art-cleanup/feedback.jsonl` by default.
- Never require or initiate an external network connection.
- Keep the static `report.html` usable without the server for read-only inspection; labeling controls explain that the review server is required to save feedback.

### 4.3 `train`

```bash
python scripts/card_art_edge_cleanup.py train [options]
```

Behavior:

- Read `debug/card-art-cleanup/feedback.jsonl` by default.
- Deduplicate labels by stable region ID, keeping the latest non-`unsure` label.
- Refuse to train an automatic-edit model until there are at least 10 `fringe` labels and 10 `intentional` labels spanning at least three source images.
- Fit and validate the model, then write `debug/card-art-cleanup/model.json`.
- Always write validation metrics and acceptance status. A rejected model may rank review regions but may not authorize automatic edits.

---

## 5. Input Validation and Image Representation

### 5.1 Validation

For every input:

- Confirm that the file has a `.png` extension and can be decoded fully.
- Convert supported PNG encodings to an in-memory RGBA representation without changing source files.
- Treat a missing alpha channel or an entirely opaque image as unsupported for this edge-cleanup workflow; record a clear failure rather than attempting full background removal.
- Reject zero-sized images and images whose decoded dimensions do not match their header.
- Record dimensions, mode, metadata keys, byte size, and SHA-256 in the manifest.

### 5.2 Color space

- Decode straight-alpha RGBA with Pillow.
- Convert sRGB color channels to floating-point linear-light values for compositing and reconstruction.
- Retain alpha as a separate `[0, 1]` array.
- Implement the required perceptual color conversion directly with NumPy so the dependency set remains Pillow, NumPy, and SciPy only.
- Convert reconstructed linear RGB back to sRGB only at PNG export.

### 5.3 Candidate band

Define the production band width as:

```text
clamp(round(min(width, height) / 256), 2, 8)
```

Use SciPy's Euclidean distance transform on the foreground mask to identify pixels inside the silhouette and within that many pixels of existing full transparency. The tool must not modify pixels deeper than this band.

The foreground mask includes every pixel whose alpha is greater than zero. Preserve disconnected foreground components; do not assume that the largest component is the only subject.

---

## 6. Feature Extraction and Region Construction

### 6.1 Per-pixel features

Calculate these features for pixels in the candidate band:

- Distance to full transparency.
- Existing alpha.
- Linear RGB luminance.
- Perceptual chroma and distance from the configured background color.
- Local mean, variance, and gradient magnitude.
- Direction from the transparent boundary into the foreground, derived from the distance-field gradient.
- Color and alpha samples at multiple inward distances along that normal.
- Difference between the boundary pixel and robust inward foreground estimates.
- Whether near-white values continue deeper than the candidate band.
- Whether the pixel belongs to a thin bright ribbon between transparency and stable non-white foreground.
- Confidence and stability of the foreground/background compositing solution.

### 6.2 Contour regions

- Group relevant candidate pixels using 8-connectivity constrained to neighboring contour directions.
- Split a region when there is a large change in color evidence, edge normal, or foreground support so unrelated edge segments do not share one decision.
- Merge tiny adjacent fragments when their feature distributions and contour directions agree.
- Calculate region summaries: bounding box, area, contour length, median thickness, feature quantiles, alpha distribution, interior-white support, and solution stability.

### 6.3 Stable identity

Create a stable region ID from:

- Source path relative to the input root.
- SHA-256 of the source bytes.
- Feature-schema version.
- Region bounding box.
- Hash of the region mask within that bounding box.

Feedback applies only when the complete ID matches. Any source-image or feature-schema change naturally invalidates stale labels.

---

## 7. Classification Rules

### 7.1 Hard preservation guards

A region cannot be cleaned automatically when any of these conditions holds:

- Its near-white color continues into a substantial interior area beyond the candidate band.
- It is part of a stable intentional bright component such as a filled white shape rather than a ribbon over a different foreground color.
- Inward color samples do not provide a stable foreground estimate.
- The compositing equation is ill-conditioned because both foreground and source background are white or nearly identical.
- The proposed edit would remove an entire disconnected component without non-white inward support.
- The region is covered by an exact `intentional` or `unsure` feedback label.
- The proposed alpha change is spatially inconsistent with neighboring contour regions and cannot be regularized without crossing a preserved boundary.

These regions are either preserved or flagged depending on whether meaningful fringe evidence remains.

### 7.2 Positive fringe evidence

Strong fringe evidence includes:

- A thin, continuous near-white band hugging transparency.
- Stable darker or more chromatic foreground immediately inward from the band.
- Boundary colors consistent with compositing that foreground over the configured white background.
- Alpha and luminance transitions that agree with a contaminated antialiased edge.
- Similar evidence across adjacent contour pixels.

### 7.3 Confidence decisions

- Exact `fringe` feedback for the current region authorizes cleaning while all geometric safety limits remain enforced.
- Without an exact override, automatic cleaning requires all hard guards to pass and final confidence `>= 0.995`.
- Relevant regions below that threshold are flagged and left byte-equivalent at the pixel level.
- Regions without relevant contamination evidence are preserved without appearing in the priority review queue.

The report must expose the reason codes and material feature values behind every clean or flag decision.

---

## 8. Alpha Matting and Edge Reconstruction

For a high-confidence region, use the standard compositing relationship in linear light:

```text
observed = alpha * foreground + (1 - alpha) * background
```

Implementation behavior:

- Estimate the local uncontaminated foreground from multiple robust inward samples, excluding other suspect pixels.
- Solve for corrected alpha using all usable RGB channels and reject unstable or contradictory solutions.
- Reconstruct foreground RGB from the observed color, corrected alpha, and configured background.
- Clamp alpha and RGB to valid ranges only after solving; count excessive clamping as instability and flag the region instead.
- Regularize corrected alpha along the contour with edge-aware one-dimensional smoothing that never crosses region or preservation boundaries.
- Do not use a two-dimensional Gaussian blur or generic feather.
- Preserve fully opaque stable foreground pixels whenever the contamination can be resolved by adjusting only the outer edge.
- For a confirmed opaque white sliver over clearly non-white inward foreground, erode only the supported sliver and reconstruct the newly exposed partial edge from the inward foreground model.
- Zero RGB wherever the final alpha becomes zero.

The output should composite cleanly on black, middle gray, white, and saturated backgrounds without a light outline or a visibly over-eroded silhouette.

---

## 9. Feedback Collection and Learning

### 9.1 Review data

Each review record includes:

- Stable region ID and source-image hash.
- Run ID and tool/feature-schema version.
- Region bounding box and mask hash.
- Raw and summarized feature values.
- Deterministic prior score, learned score if available, and final decision.
- Reason codes and failed/passed hard guards.
- Paths to the region crop and full-image diagnostics.
- Human label, timestamp, and optional note.

Use newline-delimited JSON so appending a label does not require rewriting the entire dataset.

### 9.2 Review experience

The report presents flagged regions first, ordered by expected information value: closest to the decision threshold, underrepresented feature patterns, and image diversity.

For each region, show:

- Original and candidate-cleaned crops over black, checkerboard, and saturated magenta.
- A magnified nearest-neighbor view so individual alpha-edge pixels remain visible.
- A decision-mask overlay.
- The full source image with the region highlighted.
- Confidence, reason codes, and concise feature summaries.
- `Fringe`, `Intentional`, and `Unsure` buttons plus an optional note.

### 9.3 Model fitting

- Standardize the fixed region-level feature vector using training-set means and scales.
- Fit an L2-regularized logistic classifier through SciPy optimization.
- Keep deterministic hard guards separate from the model.
- Validate by holding out complete source images rather than randomly splitting regions from the same image.
- Select regularization strength from a small fixed grid using held-out log loss, with intentional-region false positives as the primary rejection criterion.
- Accept a model for automatic decisions only if it automatically identifies at least one held-out fringe region and produces zero automatic removals among held-out intentional regions at the `0.995` threshold.
- When validation cannot meet those conditions, save the model as `ranking_only` and keep deterministic automatic behavior unchanged.

The model file includes the feature schema/version, training-data digest, normalization constants, weights, regularization value, validation folds, metrics, threshold, creation time, and acceptance status.

---

## 10. Diagnostics and Run Manifest

### 10.1 Image diagnostics

Generate lossless diagnostic PNGs for every changed or flagged image:

- Original and cleaned composites on black.
- Original and cleaned composites on a gray checkerboard.
- Original and cleaned composites on saturated magenta.
- A decision overlay using distinct colors for cleaned, flagged, and preserved candidate regions.
- Zoomed crops for every cleaned or flagged region.

Images with no relevant candidate regions need only a concise report entry; avoid generating redundant large diagnostic sheets for them.

### 10.2 `manifest.json`

The manifest contains:

- Tool, feature-schema, dependency, and Python versions.
- Run ID, UTC timestamps, resolved paths, and all command arguments.
- Source/output hashes and dimensions.
- Per-image status: `cleaned`, `unchanged`, `flagged`, or `failed`.
- Pixel and region counts for each decision.
- Region features, confidence, reasons, and diagnostic paths.
- Model identity and whether it was accepted or ranking-only.
- Source-hash verification results at the end of the run.
- Aggregate timings and failure details.

### 10.3 `report.html`

- Provide batch totals and filters by status, confidence, reason, and filename.
- Link every image to its cleaned PNG and diagnostics.
- Make the 10-image manual acceptance sample a named section.
- Include all other images in automated results without requiring manual approval.
- Embed no remote resources; CSS and JavaScript must work offline.

---

## 11. Dependency and Environment Setup

Pin these Python 3.14-compatible packages exactly:

```text
Pillow==12.3.0
numpy==2.5.1
scipy==1.18.0
```

Document this setup:

```bash
python3 -m venv .venv-card-art-cleanup
source .venv-card-art-cleanup/bin/activate
python -m pip install --upgrade pip
python -m pip install -r scripts/requirements-card-art-cleanup.txt
```

Do not add OpenCV, scikit-image, scikit-learn, GIMP bindings, web frameworks, or model-download dependencies. Use Python's standard-library HTTP server for local review and NumPy/SciPy for morphology, distance transforms, optimization, and numeric operations.

---

## 12. Automated Test Plan

Use generated RGBA fixtures in temporary directories so tests do not modify or depend on production assets.

### 12.1 Detection and preservation

- A dark solid subject with a one-pixel opaque white sliver is cleaned.
- A dark subject antialiased against white has its alpha and RGB decontaminated.
- A pure-white intentional object touching transparency is preserved and flagged when indistinguishable from background.
- A white highlight connected to a large intentional interior white region is preserved.
- A thin intentional white ring or magical effect is not automatically deleted.
- A disconnected white particle is not removed merely because the component is small.
- Transparent holes inside a subject remain transparent and receive the same boundary analysis as the outer contour.
- A clean dark edge with no white contamination remains unchanged.
- A colored edge outside the white-background model remains unchanged.

### 12.2 Reconstruction quality

- Expected alpha and foreground RGB are recovered within a documented numeric tolerance from a synthetically composited edge.
- Cleaned output matches expected composites on black, white, gray, and magenta backgrounds.
- Fully transparent pixels have zero RGB.
- Dimensions never change.
- The processing path is deterministic for identical input, arguments, feedback, and model.
- Reprocessing an already-clean synthetic image makes no further pixel changes.

### 12.3 Scale and topology

- Candidate-band sizing is correct at small, typical, and large CardArt dimensions.
- Narrow and landscape images receive the same normalized behavior.
- Multiple disconnected subject components remain independent.
- Region splitting prevents nearby but visually distinct contours from sharing a decision.

### 12.4 File and workflow safety

- Input/output collision checks cover direct equality, symlinks, and relative-path aliases.
- Source hashes remain unchanged after successful and failed runs.
- A corrupt PNG produces a recorded failure and nonzero exit status without blocking the remaining batch by default.
- Stable region IDs repeat for unchanged sources and change after source or feature-schema changes.
- Exact feedback overrides apply only to matching region IDs.
- Insufficient feedback prevents automatic-model acceptance.
- A model that misclassifies any held-out intentional region at the automatic threshold is marked ranking-only.
- Static reports render without network resources, and review-server label submissions append valid JSONL records.

---

## 13. Production Batch and Manual Acceptance Sample

### 13.1 Full batch

Run the tool across all 67 PNGs currently under `Content/CardArt`. Generate cleaned candidates and automated report data for every file. Do not require a human to inspect all 67 before the implementation is considered verified.

### 13.2 Ten-image manual sample

Manually inspect these 10 outputs because they cover varied dimensions, orientations, edge complexity, intentional white content, and negative controls:

1. `blood_price.png` — extensive intentional white cloth adjacent to transparency.
2. `divine_protection.png` — thin bright rings, stars, and effects.
3. `graveward.png` — detached ivory fragments and complex contours.
4. `deus_vult.png` — small, hard-outlined artwork.
5. `sword.png` — tall, narrow negative control without a broad white edge.
6. `dagger.png` — wide, smaller negative control with separated geometry.
7. `hidden_kunai.png` — small disconnected elements.
8. `seize.png` — landscape orientation.
9. `crusade.png` — large 1024x1536 artwork.
10. `ark_of_the_covenant.png` — irregular dimensions and complex silhouette.

For each sampled image:

- Compare original and cleaned versions on black, checkerboard gray, and saturated magenta.
- Inspect the silhouette at full size and pixel-magnified scale.
- Confirm that intentional white/ivory details are not shaved or made translucent.
- Confirm that cleaned regions have no obvious pale halo or jagged alpha erosion.
- Label all flagged contour regions encountered during this review so the feedback workflow is exercised.

The remaining 57 images need automated success, valid outputs, source-hash verification, and report entries, but not individual manual sign-off for the initial implementation.

---

## 14. Verification Commands

After implementation, run:

```bash
source .venv-card-art-cleanup/bin/activate
python -m unittest scripts.tests.test_card_art_edge_cleanup
python scripts/card_art_edge_cleanup.py --help
python scripts/card_art_edge_cleanup.py run Content/CardArt
python scripts/card_art_edge_cleanup.py review --help
python scripts/card_art_edge_cleanup.py train --help
dotnet build
```

Do not run `review` interactively as part of unattended verification; inspect the generated static report and then exercise the review server during the 10-image manual acceptance pass.

---

## 15. Completion Criteria

Implementation is complete when:

- The CLI, dependencies, tests, documentation, and ignore entry are present.
- A full 67-image batch completes or reports individual failures clearly without altering source hashes.
- Every source has a corresponding candidate output unless that source failed decoding.
- The report contains machine-readable region features, reasons, decisions, and diagnostics.
- Ambiguous regions remain unchanged and can be labeled through the local review workflow.
- Feedback can train a validated model or safely produce a ranking-only model when acceptance criteria are not met.
- The specified 10-image sample passes manual comparison without visible damage to intentional white artwork.
- Python tests and CLI smoke checks pass.
- `dotnet build` succeeds from the repository root.

## 16. Assumptions

- Current CardArt inputs are already cropped RGBA PNGs with transparent pixels around the intended subject.
- The contaminating source background is white by default; alternate background colors require an explicit CLI override.
- Conservative preservation is the desired default, and human feedback is available to improve difficult cases over time.
- Generated candidates and learned local data remain ignored under `debug/` until a separate decision is made to promote selected cleaned assets into `Content/CardArt`.
- No save-file, game-runtime, or content-manifest compatibility work is required for this tooling-only change.
