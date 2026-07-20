#!/usr/bin/env python3
"""Resize and compress RGBA card-art PNGs for smaller disk, build, and VRAM use."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
import uuid
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

from PIL import Image, UnidentifiedImageError


TOOL_VERSION = "1.0.0"
DEFAULT_INPUT = Path("Content/CardArt")
DEFAULT_OUTPUT_ROOT = Path("debug/card-art-compress/runs")
DEFAULT_BACKUP_ROOT = Path("debug/card-art-compress/backups")

# CardDisplaySystem.cs ArtWidth / ArtHeight
DEFAULT_ART_WIDTH = 204
DEFAULT_ART_HEIGHT = 270
DEFAULT_DISPLAY_SCALE = 2.0
DEFAULT_PNG_COMPRESS_LEVEL = 9


class CompressError(RuntimeError):
    """An expected input, safety, or workflow error."""


@dataclass(frozen=True)
class TargetBounds:
    max_width: int
    max_height: int

    @classmethod
    def from_scale(cls, art_width: int, art_height: int, scale: float) -> TargetBounds:
        if art_width <= 0 or art_height <= 0:
            raise CompressError("art width and height must be positive.")
        if scale <= 0:
            raise CompressError("display scale must be positive.")
        return cls(
            max_width=max(1, round(art_width * scale)),
            max_height=max(1, round(art_height * scale)),
        )


@dataclass
class ImageMetrics:
    path: str
    source_sha256: str
    source_bytes: int
    source_width: int
    source_height: int
    output_bytes: int
    output_width: int
    output_height: int
    resized: bool
    status: str
    error: str | None = None

    @property
    def bytes_saved(self) -> int:
        return self.source_bytes - self.output_bytes

    @property
    def compression_ratio(self) -> float:
        if self.output_bytes <= 0:
            return 0.0
        return self.source_bytes / self.output_bytes


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def discover_pngs(input_path: Path) -> list[Path]:
    if input_path.is_file():
        if input_path.suffix.lower() != ".png":
            raise CompressError(f"expected a PNG file: {input_path}")
        return [input_path.resolve()]
    if not input_path.is_dir():
        raise CompressError(f"input path does not exist: {input_path}")
    files = sorted(path.resolve() for path in input_path.glob("*.png"))
    if not files:
        raise CompressError(f"no PNG files found under {input_path}")
    return files


def ensure_output_is_safe(
    input_path: Path,
    input_paths: Sequence[Path],
    run_dir: Path,
    output_dir: Path,
) -> None:
    run_dir = run_dir.resolve()
    output_dir = output_dir.resolve()
    input_root = input_path.resolve()

    if input_root.is_dir():
        if run_dir == input_root or run_dir in input_root.parents or input_root in run_dir.parents:
            # Allow sibling directories, but not runs inside the source tree.
            if input_root in run_dir.parents or run_dir == input_root:
                raise CompressError(
                    f"refusing to write run directory inside input tree: {run_dir}",
                )

    for source_path in input_paths:
        resolved = source_path.resolve()
        output_path = (output_dir / resolved.name).resolve()
        if output_path == resolved:
            raise CompressError(
                f"refusing to overwrite input file with output: {resolved}",
            )


def fit_within_bounds(width: int, height: int, bounds: TargetBounds) -> tuple[int, int]:
    if width <= bounds.max_width and height <= bounds.max_height:
        return width, height
    scale = min(bounds.max_width / width, bounds.max_height / height)
    return max(1, round(width * scale)), max(1, round(height * scale))


def load_rgba_image(path: Path) -> Image.Image:
    try:
        image = Image.open(path)
    except UnidentifiedImageError as exc:
        raise CompressError(f"not a readable image: {path}") from exc
    if image.mode != "RGBA":
        image = image.convert("RGBA")
    return image


def compress_image(
    source_path: Path,
    output_path: Path,
    bounds: TargetBounds,
    compress_level: int,
) -> ImageMetrics:
    source_sha256 = sha256_file(source_path)
    source_bytes = source_path.stat().st_size

    image = load_rgba_image(source_path)
    source_width, source_height = image.size
    target_width, target_height = fit_within_bounds(source_width, source_height, bounds)
    resized = (target_width, target_height) != (source_width, source_height)
    if resized:
        image = image.resize((target_width, target_height), Image.Resampling.LANCZOS)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(
        output_path,
        format="PNG",
        optimize=True,
        compress_level=compress_level,
    )

    output_bytes = output_path.stat().st_size
    return ImageMetrics(
        path=str(source_path),
        source_sha256=source_sha256,
        source_bytes=source_bytes,
        source_width=source_width,
        source_height=source_height,
        output_bytes=output_bytes,
        output_width=target_width,
        output_height=target_height,
        resized=resized,
        status="ok",
    )


def make_run_dir(output_root: Path) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_id = uuid.uuid4().hex[:8]
    run_dir = output_root / f"{stamp}-{run_id}"
    run_dir.mkdir(parents=True, exist_ok=False)
    return run_dir


def summarize_metrics(metrics: Sequence[ImageMetrics]) -> dict[str, Any]:
    ok = [item for item in metrics if item.status == "ok"]
    failed = [item for item in metrics if item.status != "ok"]
    source_total = sum(item.source_bytes for item in ok)
    output_total = sum(item.output_bytes for item in ok)
    return {
        "images_total": len(metrics),
        "images_ok": len(ok),
        "images_failed": len(failed),
        "source_bytes_total": source_total,
        "output_bytes_total": output_total,
        "bytes_saved_total": source_total - output_total,
        "average_compression_ratio": (
            round(source_total / output_total, 2) if output_total > 0 else 0.0
        ),
        "resized_count": sum(1 for item in ok if item.resized),
    }


def verify_sources_unchanged(metrics: Sequence[ImageMetrics]) -> None:
    for item in metrics:
        if item.status != "ok":
            continue
        path = Path(item.path)
        current = sha256_file(path)
        if current != item.source_sha256:
            raise CompressError(f"source file changed during run: {path}")


def run_compress(args: argparse.Namespace) -> int:
    input_path = Path(args.input).expanduser().resolve()
    output_root = Path(args.output_root).expanduser().resolve()
    bounds = TargetBounds.from_scale(args.art_width, args.art_height, args.display_scale)
    input_files = discover_pngs(input_path)

    if args.output_dir:
        run_dir = Path(args.output_dir).expanduser().resolve()
        run_dir.mkdir(parents=True, exist_ok=True)
    else:
        output_root.mkdir(parents=True, exist_ok=True)
        run_dir = make_run_dir(output_root)

    output_dir = run_dir / "compressed"
    ensure_output_is_safe(input_path, input_files, run_dir, output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    source_hashes = {path: sha256_file(path) for path in input_files}
    metrics: list[ImageMetrics] = []
    failures = 0

    for source_path in input_files:
        output_path = output_dir / source_path.name
        try:
            result = compress_image(
                source_path,
                output_path,
                bounds,
                args.compress_level,
            )
            metrics.append(result)
            if args.verbose:
                print(
                    f"{source_path.name}: "
                    f"{result.source_width}x{result.source_height} "
                    f"({result.source_bytes // 1024} KB) -> "
                    f"{result.output_width}x{result.output_height} "
                    f"({result.output_bytes // 1024} KB)"
                )
        except (CompressError, OSError, ValueError) as exc:
            failures += 1
            metrics.append(
                ImageMetrics(
                    path=str(source_path),
                    source_sha256=source_hashes[source_path],
                    source_bytes=source_path.stat().st_size,
                    source_width=0,
                    source_height=0,
                    output_bytes=0,
                    output_width=0,
                    output_height=0,
                    resized=False,
                    status="error",
                    error=str(exc),
                ),
            )
            print(f"error: {source_path.name}: {exc}", file=sys.stderr)
            if args.fail_fast:
                break

    for source_path, expected_hash in source_hashes.items():
        if sha256_file(source_path) != expected_hash:
            raise CompressError(f"source file changed during run: {source_path}")

    verify_sources_unchanged(metrics)

    report = {
        "tool_version": TOOL_VERSION,
        "created_at": utc_now(),
        "input": str(input_path),
        "run_dir": str(run_dir),
        "output_dir": str(output_dir),
        "target_bounds": asdict(bounds),
        "display_scale": args.display_scale,
        "png_compress_level": args.compress_level,
        "summary": summarize_metrics(metrics),
        "images": [asdict(item) for item in metrics],
    }
    report_path = run_dir / "report.json"
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    summary = report["summary"]
    print(f"run_dir: {run_dir}")
    print(f"compressed: {output_dir}")
    print(
        "saved: "
        f"{summary['bytes_saved_total'] // 1024 // 1024} MB "
        f"({summary['source_bytes_total'] // 1024 // 1024} MB -> "
        f"{summary['output_bytes_total'] // 1024 // 1024} MB, "
        f"{summary['average_compression_ratio']}x)"
    )
    print(f"report: {report_path}")
    return 1 if failures else 0


def resolve_latest_run(output_root: Path) -> Path:
    if not output_root.is_dir():
        raise CompressError(f"output root does not exist: {output_root}")
    runs = sorted(path for path in output_root.iterdir() if path.is_dir())
    if not runs:
        raise CompressError(f"no completed runs under {output_root}")
    return runs[-1]


def apply_compressed(args: argparse.Namespace) -> int:
    run_dir = (
        Path(args.run_dir).expanduser().resolve()
        if args.run_dir
        else resolve_latest_run(Path(args.output_root).expanduser().resolve())
    )
    compressed_dir = run_dir / "compressed"
    report_path = run_dir / "report.json"
    if not compressed_dir.is_dir():
        raise CompressError(f"missing compressed output directory: {compressed_dir}")
    if not report_path.is_file():
        raise CompressError(f"missing report.json in run directory: {run_dir}")

    report = json.loads(report_path.read_text(encoding="utf-8"))
    target_root = Path(args.target).expanduser().resolve()
    target_root.mkdir(parents=True, exist_ok=True)

    backup_dir = Path(args.backup_dir).expanduser().resolve()
    if not args.dry_run:
        backup_dir.mkdir(parents=True, exist_ok=True)

    applied = 0
    for entry in report["images"]:
        if entry["status"] != "ok":
            continue
        source_path = Path(entry["path"]).resolve()
        compressed_path = compressed_dir / source_path.name
        target_path = target_root / source_path.name

        if not compressed_path.is_file():
            raise CompressError(f"missing compressed file: {compressed_path}")
        if sha256_file(source_path) != entry["source_sha256"]:
            raise CompressError(
                f"source hash mismatch; refusing to apply over modified file: {source_path}",
            )

        if args.dry_run:
            print(f"would apply {compressed_path.name} -> {target_path}")
            applied += 1
            continue

        backup_path = backup_dir / source_path.name
        shutil.copy2(source_path, backup_path)
        shutil.copy2(compressed_path, target_path)
        print(f"applied {target_path.name} (backup: {backup_path})")
        applied += 1

    if args.dry_run:
        print(f"dry run: would apply {applied} file(s) to {target_root}")
    else:
        print(f"applied {applied} file(s) to {target_root}")
        print(f"backups: {backup_dir}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Resize and compress RGBA card-art PNGs. "
            "Default output is non-destructive under debug/card-art-compress/."
        ),
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {TOOL_VERSION}")
    subparsers = parser.add_subparsers(dest="command", required=True)

    run_parser = subparsers.add_parser("run", help="compress one PNG or a directory of PNGs")
    run_parser.add_argument(
        "input",
        nargs="?",
        default=str(DEFAULT_INPUT),
        help=f"PNG file or directory (default: {DEFAULT_INPUT})",
    )
    run_parser.add_argument(
        "--output-root",
        default=str(DEFAULT_OUTPUT_ROOT),
        help=f"parent for timestamped runs (default: {DEFAULT_OUTPUT_ROOT})",
    )
    run_parser.add_argument(
        "--output-dir",
        help="write directly to this run directory instead of creating a timestamped run",
    )
    run_parser.add_argument(
        "--art-width",
        type=int,
        default=DEFAULT_ART_WIDTH,
        help=f"in-game card-art width in pixels (default: {DEFAULT_ART_WIDTH})",
    )
    run_parser.add_argument(
        "--art-height",
        type=int,
        default=DEFAULT_ART_HEIGHT,
        help=f"in-game card-art height in pixels (default: {DEFAULT_ART_HEIGHT})",
    )
    run_parser.add_argument(
        "--display-scale",
        type=float,
        default=DEFAULT_DISPLAY_SCALE,
        help=(
            "multiply art width/height to get the maximum output bounds "
            f"(default: {DEFAULT_DISPLAY_SCALE})"
        ),
    )
    run_parser.add_argument(
        "--compress-level",
        type=int,
        default=DEFAULT_PNG_COMPRESS_LEVEL,
        choices=range(0, 10),
        help=f"PNG deflate level 0-9 (default: {DEFAULT_PNG_COMPRESS_LEVEL})",
    )
    run_parser.add_argument(
        "--fail-fast",
        action="store_true",
        help="stop after the first image error",
    )
    run_parser.add_argument("--verbose", action="store_true", help="print per-image metrics")
    run_parser.set_defaults(handler=run_compress)

    apply_parser = subparsers.add_parser(
        "apply",
        help="copy compressed files from a completed run into Content/CardArt",
    )
    apply_parser.add_argument(
        "run_dir",
        nargs="?",
        help="completed run directory (default: most recent under --output-root)",
    )
    apply_parser.add_argument(
        "--output-root",
        default=str(DEFAULT_OUTPUT_ROOT),
        help=f"find the latest run here when run_dir is omitted (default: {DEFAULT_OUTPUT_ROOT})",
    )
    apply_parser.add_argument(
        "--target",
        default=str(DEFAULT_INPUT),
        help=f"destination directory for compressed PNGs (default: {DEFAULT_INPUT})",
    )
    apply_parser.add_argument(
        "--backup-dir",
        default=str(DEFAULT_BACKUP_ROOT),
        help=f"backup originals here before applying (default: {DEFAULT_BACKUP_ROOT})",
    )
    apply_parser.add_argument(
        "--dry-run",
        action="store_true",
        help="show what would be copied without modifying files",
    )
    apply_parser.set_defaults(handler=apply_compressed)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.handler(args))
    except CompressError as exc:
        parser.error(str(exc))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
