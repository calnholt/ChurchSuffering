#!/usr/bin/env python3
"""One-shot rename of product identity across the repo.

Replaces the old product id with ChurchSuffering in file contents and paths.
Skips .git/bin/obj/.vs/node_modules/.vite. Exits non-zero if leftovers remain.

Usage:
  python3 scripts/rename_to_church_suffering.py
  python3 scripts/rename_to_church_suffering.py --dry-run
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

# Construct old tokens without leaving a permanent contiguous old-id literal
# after this script is deleted post-run.
OLD = "Crusaders" + "30XX"
NEW = "ChurchSuffering"
OLD_LOWER = OLD.lower()
NEW_LOWER = "churchsuffering"

SKIP_DIR_NAMES = {
    ".git",
    "bin",
    "obj",
    ".vs",
    "node_modules",
    ".vite",
    "__pycache__",
}

TEXT_EXTENSIONS = {
    ".cs",
    ".csproj",
    ".sln",
    ".md",
    ".json",
    ".yaml",
    ".yml",
    ".sh",
    ".bash",
    ".zsh",
    ".html",
    ".htm",
    ".css",
    ".js",
    ".ts",
    ".tsx",
    ".jsx",
    ".xml",
    ".manifest",
    ".txt",
    ".gitignore",
    ".gitattributes",
    ".editorconfig",
    ".props",
    ".targets",
    ".config",
    ".svg",
    ".glsl",
    ".fx",
    ".mgcb",
    ".py",
    ".toml",
    ".ini",
    ".csv",
}

TEXT_BASENAMES = {
    "AGENTS.md",
    "CLAUDE.md",
    "CONTEXT.md",
    "README",
    "VERSION",
    "Dockerfile",
    "Makefile",
    "LICENSE",
    "app.manifest",
}

REPLACEMENTS = (
    (OLD, NEW),
    (OLD_LOWER, NEW_LOWER),
)

# Do not rewrite this driver (avoids corrupting OLD/NEW constants mid-run).
SELF_NAME = "rename_to_church_suffering.py"


def should_skip_dir(name: str) -> bool:
    return name in SKIP_DIR_NAMES


def is_text_file(path: Path) -> bool:
    if path.name in TEXT_BASENAMES:
        return True
    if path.suffix.lower() in TEXT_EXTENSIONS:
        return True
    if path.name.startswith(".") and path.suffix == "":
        return True
    return False


def try_decode(data: bytes) -> str | None:
    for encoding in ("utf-8", "utf-8-sig", "latin-1"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            continue
    return None


def replace_content(text: str) -> tuple[str, int]:
    count = 0
    for old, new in REPLACEMENTS:
        n = text.count(old)
        if n:
            text = text.replace(old, new)
            count += n
    return text, count


def iter_files(root: Path):
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not should_skip_dir(d)]
        for filename in filenames:
            yield Path(dirpath) / filename


def rewrite_file_contents(root: Path, dry_run: bool) -> int:
    changed_files = 0
    total_replacements = 0
    for path in iter_files(root):
        if path.name == SELF_NAME:
            continue

        if not is_text_file(path):
            if path.stat().st_size > 2_000_000:
                continue
            try:
                sample = path.read_bytes()[:4096]
            except OSError:
                continue
            if OLD.encode() not in sample and OLD_LOWER.encode() not in sample:
                continue

        try:
            raw = path.read_bytes()
        except OSError as exc:
            print(f"skip read error: {path}: {exc}", file=sys.stderr)
            continue

        text = try_decode(raw)
        if text is None:
            continue

        new_text, count = replace_content(text)
        if count == 0:
            continue

        changed_files += 1
        total_replacements += count
        rel = path.relative_to(root)
        print(f"content: {rel} ({count})")
        if not dry_run:
            path.write_bytes(new_text.encode("utf-8"))

    print(f"content files changed: {changed_files}, replacements: {total_replacements}")
    return total_replacements


def rename_paths(root: Path, dry_run: bool) -> int:
    targets: list[Path] = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not should_skip_dir(d)]
        current = Path(dirpath)
        for filename in filenames:
            if OLD in filename or OLD_LOWER in filename:
                targets.append(current / filename)
        for dirname in list(dirnames):
            if OLD in dirname or OLD_LOWER in dirname:
                targets.append(current / dirname)

    targets.sort(key=lambda p: len(p.as_posix()), reverse=True)
    renamed = 0
    for src in targets:
        if not src.exists():
            continue
        name = src.name
        new_name = name.replace(OLD, NEW).replace(OLD_LOWER, NEW_LOWER)
        if new_name == name:
            continue
        dest = src.with_name(new_name)
        rel_src = src.relative_to(root)
        rel_dest = dest.relative_to(root)
        print(f"rename: {rel_src} -> {rel_dest}")
        if not dry_run:
            if dest.exists():
                raise SystemExit(f"refusing to overwrite existing path: {dest}")
            src.rename(dest)
        renamed += 1
    print(f"paths renamed: {renamed}")
    return renamed


def find_leftovers(root: Path) -> list[str]:
    leftovers: list[str] = []
    for path in iter_files(root):
        if path.name == SELF_NAME:
            continue
        rel = str(path.relative_to(root))
        if OLD in rel or OLD_LOWER in rel:
            leftovers.append(f"path: {rel}")
            continue
        if not is_text_file(path) and path.stat().st_size > 2_000_000:
            continue
        try:
            raw = path.read_bytes()
        except OSError:
            continue
        sample = raw[:8192]
        if b"\x00" in sample:
            continue
        text = try_decode(raw)
        if text is None:
            continue
        if OLD in text or OLD_LOWER in text:
            leftovers.append(f"content: {rel}")
    return leftovers


def delete_stray_empty(root: Path, dry_run: bool) -> None:
    for path in root.glob(OLD + ".ECS.Data.Locations.QuestDefinition"):
        if path.is_file() and path.stat().st_size == 0:
            print(f"delete empty stray: {path.relative_to(root)}")
            if not dry_run:
                path.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--root", type=Path, default=None)
    args = parser.parse_args()
    root = (args.root or Path(__file__).resolve().parent.parent).resolve()
    print(f"root: {root}")
    if args.dry_run:
        print("mode: dry-run")

    delete_stray_empty(root, args.dry_run)
    rewrite_file_contents(root, args.dry_run)
    rename_paths(root, args.dry_run)

    leftovers = find_leftovers(root)
    if leftovers:
        print("\nLEFTOVERS:", file=sys.stderr)
        for item in leftovers[:200]:
            print(f"  {item}", file=sys.stderr)
        if len(leftovers) > 200:
            print(f"  ... and {len(leftovers) - 200} more", file=sys.stderr)
        if args.dry_run:
            print(
                f"dry-run complete with {len(leftovers)} projected leftovers",
                file=sys.stderr,
            )
            return 0
        return 1

    print(f"OK: no {OLD} leftovers in source tree")
    return 0


if __name__ == "__main__":
    sys.exit(main())
