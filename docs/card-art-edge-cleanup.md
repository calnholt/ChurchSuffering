# Card-Art Edge Cleanup

`scripts/card_art_edge_cleanup.py` creates non-destructive candidate PNGs that remove high-confidence white fringe from cropped card art. It is deliberately conservative: ambiguous white or ivory edges are copied unchanged and placed in the review queue.

The tool never changes files under `Content/CardArt`, never updates `Content/Content.mgcb`, and never points the game at generated candidates. Each batch goes into a unique directory under `debug/card-art-cleanup/runs/` by default.

## Setup

Python 3.14 and the pinned dependencies are required:

```bash
python3 -m venv .venv-card-art-cleanup
source .venv-card-art-cleanup/bin/activate
python -m pip install --upgrade pip
python -m pip install -r scripts/requirements-card-art-cleanup.txt
```

The environment and all generated runs, feedback, and models are ignored by Git.

## Generate candidates

Process all current card art:

```bash
source .venv-card-art-cleanup/bin/activate
python scripts/card_art_edge_cleanup.py run Content/CardArt
```

Process one file, use another output root, or diagnose a fixed-width candidate band:

```bash
python scripts/card_art_edge_cleanup.py run Content/CardArt/sword.png
python scripts/card_art_edge_cleanup.py run Content/CardArt --output-root /tmp/card-art-runs
python scripts/card_art_edge_cleanup.py run Content/CardArt/sword.png --candidate-band 3 --verbose
```

The assumed contaminating background is white. For art antialiased against another known color, pass `--background-color RRGGBB`. `--no-model` disables learned ranking and decisions. `--model PATH` requires a readable model with the current feature schema. `--feedback PATH` selects exact region overrides from another feedback file.

Directory inputs are discovered recursively in lexical order. A failure in one PNG is recorded while later images continue; the command exits nonzero after writing the report. `--fail-fast` stops after the first failure but still writes a partial manifest and report.

## Run contents

Every successful source has a corresponding file under `cleaned/`, including unchanged candidates. A run contains:

```text
<run-id>/
  cleaned/
  diagnostics/
  manifest.json
  report.html
```

`manifest.json` is the machine-readable record. It includes input/output hashes, dimensions, exact arguments and dependency versions, timing, candidate-band width, region feature values, confidence, decision reasons, model identity, and final source-hash verification.

The image statuses are:

- `cleaned`: the candidate has pixel changes, including removal of hidden RGB beneath zero alpha.
- `flagged`: relevant edge evidence was ambiguous and pixels were preserved.
- `unchanged`: no output pixels changed and no region needs review.
- `failed`: that source could not be validated, decoded, analyzed, or written.

Region decisions are `clean`, `flag`, or `preserve`. Automatic cleaning requires every hard safety guard to pass and confidence of at least `0.995`. The reason codes and material feature values explain the decision. A flagged region is byte-equivalent at the pixel level to the source region.

For thin strokes and detached fragments, the analyzer may replace an inward normal sample that overshot into transparency with nearby non-white support from the same connected foreground component. It never borrows support across transparent component boundaries. Very small, low-alpha, near-background components can be classified as isolated residue; opaque isolated white particles remain protected. Spatial-consistency guards apply only to contour runs long enough to regularize meaningfully.

`report.html` is completely self-contained and can be opened directly for read-only inspection. Changed or flagged images include comparisons over black, gray checkerboard, and saturated magenta; decision overlays use green for cleaned, amber for flagged, and blue for preserved candidate pixels. Region crops use nearest-neighbor magnification.

## Review and feedback

Start the local review UI for the newest completed run:

```bash
python scripts/card_art_edge_cleanup.py review
```

Or select a run without opening a browser:

```bash
python scripts/card_art_edge_cleanup.py review debug/card-art-cleanup/runs/<run-id> --no-open
```

The server binds only to `127.0.0.1`; it has no external resources or network dependency. Label each flagged contour as `Fringe`, `Intentional`, or `Unsure`, with an optional note. Labels append to `debug/card-art-cleanup/feedback.jsonl`. The latest label for a stable region ID wins. Exact `intentional` and `unsure` feedback prevents edits; exact `fringe` feedback can authorize an edit only when geometric and compositing safety guards still pass.

A region ID includes the relative source path, source SHA-256, feature-schema version, bounding box, and region-mask hash. Changing the source or schema invalidates stale feedback automatically.

For the initial acceptance pass, inspect and label flagged regions in the named ten-image section of the report: `blood_price.png`, `divine_protection.png`, `graveward.png`, `deus_vult.png`, `sword.png`, `dagger.png`, `hidden_kunai.png`, `seize.png`, `crusade.png`, and `ark_of_the_covenant.png`.

## Train a ranking model

```bash
python scripts/card_art_edge_cleanup.py train
```

Training keeps only the latest non-`unsure` label for each exact region. At least 10 `fringe` and 10 `intentional` labels across three source images are required. Validation holds out complete images and considers several fixed L2 regularization strengths.

The generated `debug/card-art-cleanup/model.json` always records validation metrics. It is `accepted` for automatic decisions only when at least one held-out fringe reaches `0.995` and no held-out intentional region does. Otherwise it is `ranking_only`: its score may help order review work, but it cannot authorize an edit or bypass deterministic guards.

## Verification

```bash
source .venv-card-art-cleanup/bin/activate
python -m unittest scripts.tests.test_card_art_edge_cleanup
python scripts/card_art_edge_cleanup.py --help
python scripts/card_art_edge_cleanup.py run Content/CardArt
python scripts/card_art_edge_cleanup.py review --help
python scripts/card_art_edge_cleanup.py train --help
dotnet build
```

## Recovery and safe adoption

Generated results can be deleted at any time because sources are untouched. If a run fails, read its per-image error and source verification in `manifest.json`, correct the input or environment, and start a new run. Never resume by writing into the old timestamped run.

To adopt a candidate, compare it visually and copy it into the intended source location as a separate, explicit asset change. This tool does not perform that step. Keep the run manifest with review notes outside Git if an audit trail is needed. If source verification ever reports a changed hash, stop and restore the source through the repository's normal version-control workflow before evaluating candidates.
