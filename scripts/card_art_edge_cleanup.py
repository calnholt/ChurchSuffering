#!/usr/bin/env python3
"""Conservatively remove white-background fringe from cropped RGBA card art."""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import math
import mimetypes
import os
import platform
import shutil
import sys
import threading
import time
import uuid
import webbrowser
from dataclasses import dataclass, field
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Iterable, Sequence
from urllib.parse import unquote, urlparse

import numpy as np
import PIL
import scipy
from PIL import Image, ImageDraw, ImageOps, UnidentifiedImageError
from scipy import ndimage, optimize


TOOL_VERSION = "1.0.0"
FEATURE_SCHEMA_VERSION = "card-art-edge-v2"
AUTO_THRESHOLD = 0.995
DEFAULT_INPUT = Path("Content/CardArt")
DEFAULT_OUTPUT_ROOT = Path("debug/card-art-cleanup/runs")
DEFAULT_FEEDBACK = Path("debug/card-art-cleanup/feedback.jsonl")
DEFAULT_MODEL = Path("debug/card-art-cleanup/model.json")
MANUAL_SAMPLE = (
    "blood_price.png",
    "divine_protection.png",
    "graveward.png",
    "deus_vult.png",
    "sword.png",
    "dagger.png",
    "hidden_kunai.png",
    "seize.png",
    "crusade.png",
    "ark_of_the_covenant.png",
)
FEATURE_VECTOR_NAMES = (
    "area_log",
    "median_thickness",
    "background_similarity",
    "foreground_contrast",
    "solution_stability",
    "interior_white_support",
    "alpha_median",
    "luminance_median",
    "ribbon_support",
    "foreground_variance",
)


class CleanupError(RuntimeError):
    """An expected input, safety, or workflow error."""


@dataclass
class LoadedImage:
    rgba: np.ndarray
    metadata: dict[str, Any]
    mode: str
    metadata_keys: list[str]


@dataclass
class Region:
    numeric_id: int
    stable_id: str
    mask: np.ndarray  # Cropped to bbox; full-frame masks are intentionally not retained.
    bbox: tuple[int, int, int, int]
    mask_hash: str
    features: dict[str, float]
    feature_vector: list[float]
    prior_score: float
    learned_score: float | None
    confidence: float
    decision: str
    reasons: list[str]
    guards: dict[str, bool]
    feedback_label: str | None = None
    diagnostic_path: str | None = None

    def to_manifest(self) -> dict[str, Any]:
        return {
            "region_id": self.stable_id,
            "numeric_id": self.numeric_id,
            "bbox": list(self.bbox),
            "mask_hash": self.mask_hash,
            "area": int(self.mask.sum()),
            "features": self.features,
            "feature_vector": dict(zip(FEATURE_VECTOR_NAMES, self.feature_vector)),
            "deterministic_prior": self.prior_score,
            "learned_score": self.learned_score,
            "confidence": self.confidence,
            "decision": self.decision,
            "reason_codes": self.reasons,
            "hard_guards": self.guards,
            "feedback_label": self.feedback_label,
            "diagnostic_path": self.diagnostic_path,
        }


@dataclass
class Analysis:
    output_rgba: np.ndarray
    regions: list[Region]
    candidate_mask: np.ndarray
    cleaned_mask: np.ndarray
    flagged_mask: np.ndarray
    preserved_mask: np.ndarray
    band_width: int
    changed_pixels: int


@dataclass
class AcceptedModel:
    path: Path
    digest: str
    means: np.ndarray
    scales: np.ndarray
    weights: np.ndarray
    bias: float
    accepted: bool

    def predict(self, vector: Sequence[float]) -> float:
        x = (np.asarray(vector, dtype=np.float64) - self.means) / self.scales
        z = float(np.dot(x, self.weights) + self.bias)
        return stable_sigmoid(z)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def stable_sigmoid(value: float) -> float:
    if value >= 0:
        return 1.0 / (1.0 + math.exp(-min(value, 700.0)))
    exp_value = math.exp(max(value, -700.0))
    return exp_value / (1.0 + exp_value)


def srgb_to_linear(rgb: np.ndarray) -> np.ndarray:
    rgb = np.asarray(rgb, dtype=np.float64)
    return np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(rgb: np.ndarray) -> np.ndarray:
    rgb = np.clip(np.asarray(rgb, dtype=np.float64), 0.0, 1.0)
    return np.where(rgb <= 0.0031308, rgb * 12.92, 1.055 * np.power(rgb, 1 / 2.4) - 0.055)


# PNG channels are discrete. Lookup tables preserve the same transfer function
# while avoiding repeated multi-million-element fractional-power operations.
SRGB8_TO_LINEAR = srgb_to_linear(np.arange(256, dtype=np.float64) / 255.0)
LINEAR16_TO_SRGB8 = np.rint(
    linear_to_srgb(np.arange(65536, dtype=np.float64) / 65535.0) * 255.0
).astype(np.uint8)


def linear_to_srgb8(rgb: np.ndarray) -> np.ndarray:
    indices = np.rint(np.clip(rgb, 0.0, 1.0) * 65535.0).astype(np.uint16)
    return LINEAR16_TO_SRGB8[indices]


def linear_rgb_to_lab(rgb: np.ndarray) -> np.ndarray:
    """Convert linear sRGB to CIE Lab (D65), with no extra color dependency."""
    matrix = np.asarray(
        [[0.4124564, 0.3575761, 0.1804375],
         [0.2126729, 0.7151522, 0.0721750],
         [0.0193339, 0.1191920, 0.9503041]],
        dtype=np.float64,
    )
    xyz = np.tensordot(rgb, matrix.T, axes=1)
    xyz = xyz / np.asarray([0.95047, 1.0, 1.08883])
    delta = 6 / 29
    f = np.where(xyz > delta**3, np.cbrt(xyz), xyz / (3 * delta**2) + 4 / 29)
    return np.stack(
        [116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])],
        axis=-1,
    )


def parse_hex_color(value: str) -> tuple[int, int, int]:
    text = value.strip().lstrip("#")
    if len(text) != 6:
        raise argparse.ArgumentTypeError("background color must contain exactly six hexadecimal digits")
    try:
        channels = tuple(int(text[index:index + 2], 16) for index in (0, 2, 4))
    except ValueError as exc:
        raise argparse.ArgumentTypeError("background color must be hexadecimal RRGGBB") from exc
    return channels  # type: ignore[return-value]


def candidate_band_width(width: int, height: int) -> int:
    return max(2, min(8, int(math.floor(min(width, height) / 256 + 0.5))))


def discover_inputs(input_path: Path) -> tuple[Path, list[tuple[Path, Path]]]:
    resolved = input_path.expanduser().resolve(strict=True)
    if resolved.is_file():
        if resolved.suffix.lower() != ".png":
            raise CleanupError(f"input file is not a PNG: {input_path}")
        return resolved.parent, [(resolved, Path(resolved.name))]
    if not resolved.is_dir():
        raise CleanupError(f"input is neither a PNG nor a directory: {input_path}")
    inputs = sorted(
        ((path.resolve(), path.relative_to(resolved)) for path in resolved.rglob("*") if path.is_file() and path.suffix.lower() == ".png"),
        key=lambda item: item[1].as_posix(),
    )
    if not inputs:
        raise CleanupError(f"no PNG files found under {input_path}")
    return resolved, inputs


def is_relative_to(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def validate_output_root(input_root: Path, inputs: Sequence[tuple[Path, Path]], output_root: Path) -> Path:
    output = output_root.expanduser().resolve()
    resolved_inputs = {path.resolve() for path, _ in inputs}
    if output in resolved_inputs:
        raise CleanupError("output root resolves to an input file")
    if input_root.is_dir() and is_relative_to(output, input_root):
        raise CleanupError("output root cannot be inside the input directory")
    for source, relative in inputs:
        candidate = (output / "placeholder" / "cleaned" / relative).resolve()
        if candidate == source.resolve():
            raise CleanupError(f"output would overwrite input: {source}")
    return output


def create_run_directory(output_root: Path) -> tuple[str, Path]:
    output_root.mkdir(parents=True, exist_ok=True)
    for _ in range(10):
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        run_id = f"{stamp}-{uuid.uuid4().hex[:8]}"
        run_dir = output_root / run_id
        try:
            run_dir.mkdir()
            (run_dir / "cleaned").mkdir()
            (run_dir / "diagnostics").mkdir()
            return run_id, run_dir
        except FileExistsError:
            continue
    raise CleanupError("could not allocate a unique run directory")


def load_png(path: Path) -> LoadedImage:
    try:
        with Image.open(path) as image:
            image.verify()
        with Image.open(path) as image:
            image.load()
            if image.width <= 0 or image.height <= 0:
                raise CleanupError("image has zero dimensions")
            original_mode = image.mode
            if "A" not in image.getbands() and "transparency" not in image.info:
                raise CleanupError("PNG has no alpha channel")
            rgba_image = image.convert("RGBA")
            rgba = np.asarray(rgba_image, dtype=np.uint8).copy()
            if rgba.shape[:2] != (image.height, image.width):
                raise CleanupError("decoded dimensions do not match the PNG header")
            if np.all(rgba[..., 3] == 255):
                raise CleanupError("PNG is entirely opaque; cropped RGBA input is required")
            metadata: dict[str, Any] = {}
            if isinstance(image.info.get("icc_profile"), bytes):
                metadata["icc_profile"] = image.info["icc_profile"]
            dpi = image.info.get("dpi")
            if isinstance(dpi, tuple) and len(dpi) == 2:
                metadata["dpi"] = tuple(float(value) for value in dpi)
            return LoadedImage(rgba, metadata, original_mode, sorted(str(key) for key in image.info))
    except CleanupError:
        raise
    except (UnidentifiedImageError, OSError, SyntaxError, ValueError) as exc:
        raise CleanupError(f"could not decode PNG: {exc}") from exc


def save_rgba(path: Path, rgba: np.ndarray, metadata: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image = Image.fromarray(np.asarray(rgba, dtype=np.uint8), "RGBA")
    kwargs: dict[str, Any] = {"format": "PNG", "compress_level": 6, "optimize": False}
    for key in ("icc_profile", "dpi"):
        if key in metadata:
            kwargs[key] = metadata[key]
    image.save(path, **kwargs)


def _sample_map(values: np.ndarray, ys: np.ndarray, xs: np.ndarray, order: int = 1) -> np.ndarray:
    coords = np.vstack([ys.ravel(), xs.ravel()])
    if values.ndim == 2:
        return ndimage.map_coordinates(values, coords, order=order, mode="nearest").reshape(ys.shape)
    sampled = [
        ndimage.map_coordinates(values[..., channel], coords, order=order, mode="nearest").reshape(ys.shape)
        for channel in range(values.shape[-1])
    ]
    return np.stack(sampled, axis=-1)


def _split_regions(
    mask: np.ndarray,
    normal_y: np.ndarray,
    normal_x: np.ndarray,
    evidence: np.ndarray,
    component_owners: np.ndarray | None = None,
    merge_radius: int = 0,
) -> list[np.ndarray]:
    """8-connected grouping that avoids joining sharply different contour segments."""
    if component_owners is not None:
        # Real painted contours contain many one-pixel gaps in near-background
        # evidence. Bridge those gaps for review/classification, but never bridge
        # disconnected foreground components. Mixed segments become conservative
        # through their region summaries and hard guards.
        pieces: list[np.ndarray] = []
        structure = np.ones((3, 3), dtype=np.uint8)
        for owner in np.unique(component_owners[mask]):
            if owner <= 0:
                continue
            owned = mask & (component_owners == owner)
            grouped = ndimage.binary_dilation(owned, structure=structure, iterations=max(1, merge_radius))
            group_labels, group_count = ndimage.label(grouped, structure=structure)
            for group_id in range(1, group_count + 1):
                coords = np.argwhere(owned & (group_labels == group_id))
                if len(coords):
                    pieces.append(coords.astype(np.int32, copy=False))
        pieces.sort(key=lambda coords: (int(coords[:, 0].min()), int(coords[:, 1].min())))
        return pieces

    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    pieces: list[np.ndarray] = []
    for start_y, start_x in np.argwhere(mask):
        if visited[start_y, start_x]:
            continue
        queue = [(int(start_y), int(start_x))]
        visited[start_y, start_x] = True
        coords: list[tuple[int, int]] = []
        while queue:
            y, x = queue.pop()
            coords.append((y, x))
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    if not (dx or dy):
                        continue
                    ny, nx = y + dy, x + dx
                    if ny < 0 or ny >= height or nx < 0 or nx >= width or visited[ny, nx] or not mask[ny, nx]:
                        continue
                    dot = normal_y[y, x] * normal_y[ny, nx] + normal_x[y, x] * normal_x[ny, nx]
                    if dot < 0.35 or abs(float(evidence[y, x] - evidence[ny, nx])) > 0.42:
                        continue
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        pieces.append(np.asarray(coords, dtype=np.int32))
    return pieces


def _stable_region_id(relative_path: Path, source_hash: str, bbox: tuple[int, int, int, int], local_mask: np.ndarray) -> tuple[str, str]:
    packed = np.packbits(local_mask.astype(np.uint8), bitorder="little").tobytes()
    mask_hash = sha256_bytes(packed)
    identity = "\n".join(
        [relative_path.as_posix(), source_hash, FEATURE_SCHEMA_VERSION, ",".join(map(str, bbox)), mask_hash]
    ).encode("utf-8")
    return sha256_bytes(identity), mask_hash


def _feature_vector(features: dict[str, float]) -> list[float]:
    return [
        math.log1p(features["area"]),
        features["median_thickness"],
        features["background_similarity"],
        features["foreground_contrast"],
        features["solution_stability"],
        features["interior_white_support"],
        features["alpha_median"],
        features["luminance_median"],
        features["ribbon_support"],
        features["foreground_variance"],
    ]


def _regularize_alpha(region_mask: np.ndarray, raw_alpha: np.ndarray, normal_y: np.ndarray, normal_x: np.ndarray) -> np.ndarray:
    result = raw_alpha.copy()
    height, width = region_mask.shape
    for y, x in np.argwhere(region_mask):
        samples = [float(raw_alpha[y, x])]
        weights = [2.0]
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not (dx or dy):
                    continue
                ny, nx = int(y + dy), int(x + dx)
                if ny < 0 or ny >= height or nx < 0 or nx >= width or not region_mask[ny, nx]:
                    continue
                along_normal = abs(dx * normal_x[y, x] + dy * normal_y[y, x])
                if along_normal > 0.75:
                    continue
                difference = abs(float(raw_alpha[ny, nx] - raw_alpha[y, x]))
                weight = math.exp(-difference * 8.0)
                samples.append(float(raw_alpha[ny, nx]))
                weights.append(weight)
        result[y, x] = float(np.average(samples, weights=weights))
    return result


def _is_spatially_inconsistent(area: int, band_width: int, alpha_spread: float) -> bool:
    """Only long enough contour runs can fail spatial regularity on their own."""
    return area >= max(16, band_width * 4) and alpha_spread > 0.90


def analyze_image(
    rgba: np.ndarray,
    relative_path: Path,
    source_hash: str,
    background_rgb: tuple[int, int, int] = (255, 255, 255),
    band_override: int | None = None,
    feedback: dict[str, dict[str, Any]] | None = None,
    model: AcceptedModel | None = None,
) -> Analysis:
    height, width = rgba.shape[:2]
    band_width = band_override or candidate_band_width(width, height)
    if not 1 <= band_width <= 64:
        raise CleanupError("candidate band must be between 1 and 64 pixels")

    alpha = rgba[..., 3].astype(np.float64) / 255.0
    rgb = SRGB8_TO_LINEAR[rgba[..., :3]]
    foreground = alpha > 0.0
    distance = ndimage.distance_transform_edt(foreground)
    candidate = foreground & (distance <= band_width)
    if not np.any(candidate):
        empty = np.zeros_like(foreground)
        return Analysis(rgba.copy(), [], candidate, empty.copy(), empty.copy(), empty.copy(), band_width, 0)

    gradient_y, gradient_x = np.gradient(distance)
    norm = np.hypot(gradient_y, gradient_x)
    normal_y = np.divide(gradient_y, norm, out=np.zeros_like(gradient_y), where=norm > 1e-8)
    normal_x = np.divide(gradient_x, norm, out=np.zeros_like(gradient_x), where=norm > 1e-8)

    luminance = rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722
    local_mean = ndimage.uniform_filter(luminance, size=3, mode="nearest")
    local_variance = np.maximum(0.0, ndimage.uniform_filter(luminance**2, size=3, mode="nearest") - local_mean**2)
    luminance_gradient = np.hypot(ndimage.sobel(luminance, axis=0), ndimage.sobel(luminance, axis=1)) / 8.0
    background = SRGB8_TO_LINEAR[np.asarray(background_rgb, dtype=np.uint8)]
    lab = linear_rgb_to_lab(rgb)
    background_lab = linear_rgb_to_lab(background)
    background_delta = np.linalg.norm(lab - background_lab, axis=-1)
    chroma = np.linalg.norm(lab[..., 1:], axis=-1)
    # A 50% antialiased dark edge is perceptually well away from white even though
    # white is still the correct compositing source. Keep the relevance cutoff
    # narrow, but grade similarity over a wider range for the matting evidence.
    background_similarity = np.exp(-np.square(background_delta / 30.0))

    candidate_ys, candidate_xs = np.nonzero(candidate)
    candidate_normal_y = normal_y[candidate_ys, candidate_xs]
    candidate_normal_x = normal_x[candidate_ys, candidate_xs]
    inward_depths = np.asarray([max(2, band_width), band_width + 2, band_width * 2 + 1], dtype=np.float64)
    inward_rgb_samples = []
    inward_alpha_samples = []
    inward_bg_samples = []
    for depth in inward_depths:
        sample_y = np.clip(candidate_ys + candidate_normal_y * depth, 0, height - 1)
        sample_x = np.clip(candidate_xs + candidate_normal_x * depth, 0, width - 1)
        inward_rgb_samples.append(_sample_map(rgb, sample_y, sample_x))
        inward_alpha_samples.append(_sample_map(alpha, sample_y, sample_x))
        inward_bg_samples.append(_sample_map(background_delta, sample_y, sample_x))
    inward_stack = np.stack(inward_rgb_samples, axis=0)
    inward_rgb = np.zeros_like(rgb)
    inward_variance = np.zeros_like(alpha)
    inward_alpha = np.zeros_like(alpha)
    inward_background_delta = np.zeros_like(alpha)
    inward_rgb[candidate_ys, candidate_xs] = np.median(inward_stack, axis=0)
    inward_variance[candidate_ys, candidate_xs] = np.mean(np.var(inward_stack, axis=0), axis=-1)
    inward_alpha[candidate_ys, candidate_xs] = np.median(np.stack(inward_alpha_samples, axis=0), axis=0)
    inward_background_delta[candidate_ys, candidate_xs] = np.median(np.stack(inward_bg_samples, axis=0), axis=0)
    del lab, inward_stack, inward_rgb_samples, inward_alpha_samples, inward_bg_samples

    # Fixed normal samples can jump across a narrow stroke or detached shard and
    # land in transparency. In that case, use the nearest demonstrably non-white
    # pixel from the same connected foreground component. This stays topology-
    # safe while supporting geometry thinner than the normal production band.
    component_labels, component_count = ndimage.label(
        foreground, structure=np.ones((3, 3), dtype=np.uint8)
    )
    stable_support = foreground & (alpha >= 0.50) & (background_delta >= 30.0)
    support_distance, support_indices = ndimage.distance_transform_edt(
        ~stable_support, return_distances=True, return_indices=True
    )
    support_y = support_indices[0, candidate_ys, candidate_xs]
    support_x = support_indices[1, candidate_ys, candidate_xs]
    same_component_support = (
        stable_support[support_y, support_x]
        & (component_labels[support_y, support_x] == component_labels[candidate_ys, candidate_xs])
        & (support_distance[candidate_ys, candidate_xs] <= max(8, band_width * 6))
    )
    needs_adaptive_support = (
        (inward_alpha[candidate_ys, candidate_xs] < 0.65)
        | (inward_variance[candidate_ys, candidate_xs] >= 0.012)
        | (inward_background_delta[candidate_ys, candidate_xs] < 10.0)
    )
    adaptive = needs_adaptive_support & same_component_support
    adaptive_positions = np.nonzero(adaptive)[0]
    adaptive_support_used = np.zeros_like(candidate)
    adaptive_support_distance = np.zeros_like(alpha)
    if len(adaptive_positions):
        target_y = candidate_ys[adaptive_positions]
        target_x = candidate_xs[adaptive_positions]
        source_y = support_y[adaptive_positions]
        source_x = support_x[adaptive_positions]
        inward_rgb[target_y, target_x] = rgb[source_y, source_x]
        inward_alpha[target_y, target_x] = alpha[source_y, source_x]
        inward_background_delta[target_y, target_x] = background_delta[source_y, source_x]
        inward_variance[target_y, target_x] = np.minimum(
            inward_variance[target_y, target_x], local_variance[source_y, source_x]
        )
        adaptive_support_used[target_y, target_x] = True
        adaptive_support_distance[target_y, target_x] = support_distance[target_y, target_x]
    del support_indices, support_distance

    denominator = np.zeros_like(alpha)
    solved_alpha = np.zeros_like(alpha)
    residual = np.zeros_like(alpha)
    contradictory = np.zeros_like(candidate)
    solution_stability = np.zeros_like(alpha)
    foreground_contrast = np.zeros_like(alpha)
    thin_ribbon = np.zeros_like(alpha)
    candidate_inward = inward_rgb[candidate_ys, candidate_xs]
    candidate_observed = rgb[candidate_ys, candidate_xs]
    candidate_delta = candidate_inward - background
    denominator_values = np.sum(np.square(candidate_delta), axis=-1)
    solved_values = np.divide(
        np.sum((candidate_observed - background) * candidate_delta, axis=-1),
        denominator_values,
        out=np.zeros_like(denominator_values),
        where=denominator_values > 1e-6,
    )
    reconstructed = background + solved_values[..., None] * candidate_delta
    residual_values = np.linalg.norm(reconstructed - candidate_observed, axis=-1) / math.sqrt(3)
    contradictory_values = (solved_values < -0.08) | (solved_values > 1.08)
    denominator[candidate_ys, candidate_xs] = denominator_values
    solved_alpha[candidate_ys, candidate_xs] = solved_values
    residual[candidate_ys, candidate_xs] = residual_values
    contradictory[candidate_ys, candidate_xs] = contradictory_values
    solution_stability[candidate_ys, candidate_xs] = (
        np.exp(-residual_values * 24.0)
        * np.exp(-inward_variance[candidate_ys, candidate_xs] * 30.0)
        * (~contradictory_values)
    )
    foreground_contrast[candidate_ys, candidate_xs] = np.clip(
        (inward_background_delta[candidate_ys, candidate_xs] - 8.0) / 42.0, 0.0, 1.0
    )
    thin_ribbon[candidate_ys, candidate_xs] = np.clip(
        (inward_background_delta[candidate_ys, candidate_xs] - background_delta[candidate_ys, candidate_xs] - 5.0) / 30.0,
        0.0, 1.0,
    )

    relevance = candidate & (background_delta < 28.0) & (
        (foreground_contrast > 0.08) | (background_similarity > 0.60)
    )
    pixel_evidence = np.clip(
        0.33 * background_similarity
        + 0.31 * foreground_contrast
        + 0.20 * solution_stability
        + 0.16 * thin_ribbon,
        0.0,
        1.0,
    )
    region_masks = _split_regions(
        relevance, normal_y, normal_x, pixel_evidence,
        component_owners=component_labels, merge_radius=max(24, band_width * 12),
    )

    output_linear = rgb.copy()
    output_alpha = alpha.copy()
    cleaned_mask = np.zeros_like(candidate)
    flagged_mask = np.zeros_like(candidate)
    regions: list[Region] = []
    feedback = feedback or {}
    component_sizes = np.bincount(component_labels.ravel())
    component_alpha_max = np.asarray(
        ndimage.maximum(alpha, labels=component_labels, index=np.arange(component_count + 1)),
        dtype=np.float64,
    )

    for numeric_id, region_coords in enumerate(region_masks, start=1):
        if not len(region_coords):
            continue
        region_ys = region_coords[:, 0]
        region_xs = region_coords[:, 1]
        bbox = (
            int(region_xs.min()), int(region_ys.min()), int(region_xs.max()) + 1, int(region_ys.max()) + 1
        )
        left, top, right, bottom = bbox
        region_local = np.zeros((bottom - top, right - left), dtype=bool)
        region_local[region_ys - top, region_xs - left] = True
        stable_id, mask_hash = _stable_region_id(relative_path, source_hash, bbox, region_local)
        expansion = max(2, band_width * 2)
        expanded_left, expanded_top = max(0, left - expansion), max(0, top - expansion)
        expanded_right, expanded_bottom = min(width, right + expansion), min(height, bottom + expansion)
        seed = np.zeros((expanded_bottom - expanded_top, expanded_right - expanded_left), dtype=bool)
        seed[region_ys - expanded_top, region_xs - expanded_left] = True
        local_dilation = ndimage.binary_dilation(seed, iterations=expansion)
        distance_crop = distance[expanded_top:expanded_bottom, expanded_left:expanded_right]
        background_delta_crop = background_delta[expanded_top:expanded_bottom, expanded_left:expanded_right]
        deep = local_dilation & (distance_crop > band_width + 1)
        interior_white = deep & (background_delta_crop < 22.0)
        interior_white_support = float(interior_white.sum() / max(1, deep.sum()))
        region_component_ids = np.unique(component_labels[region_ys, region_xs])
        component_area = int(component_sizes[region_component_ids[region_component_ids > 0]].sum())
        whole_component = len(region_coords) >= max(1, component_area * 0.80)
        region_component_alpha_max = float(component_alpha_max[region_component_ids].max())

        values = lambda array: np.asarray(array[region_ys, region_xs], dtype=np.float64)
        area = len(region_coords)
        foreground_variance = float(np.median(values(inward_variance)))
        stable_foreground = foreground_variance < 0.012 and float(np.median(values(inward_alpha))) > 0.65
        ill_conditioned = float(np.median(values(denominator))) < 0.006
        continued_white = interior_white_support > 0.34
        filled_bright_shape = continued_white and float(np.median(values(background_similarity))) > 0.72
        isolated_residue = bool(
            whole_component
            and component_area <= max(4, band_width * 2)
            and region_component_alpha_max <= 0.65
            and float(np.median(values(background_similarity))) >= 0.94
            and float(np.median(values(background_delta))) <= 12.0
        )
        would_remove_component = bool(
            whole_component
            and float(np.median(values(foreground_contrast))) < 0.55
            and not isolated_residue
        )
        alpha_spread = float(
            np.quantile(values(solved_alpha), 0.90) - np.quantile(values(solved_alpha), 0.10)
        )
        spatially_inconsistent = _is_spatially_inconsistent(area, band_width, alpha_spread)

        boundary = region_local & ~ndimage.binary_erosion(region_local, structure=np.ones((3, 3), dtype=bool))
        features = {
            "area": float(area),
            "contour_length": float(boundary.sum()),
            "median_thickness": float(np.median(values(distance))),
            "background_similarity": float(np.median(values(background_similarity))),
            "background_delta_e_median": float(np.median(values(background_delta))),
            "foreground_contrast": float(np.median(values(foreground_contrast))),
            "solution_stability": float(np.median(values(solution_stability))),
            "solution_residual_median": float(np.median(values(residual))),
            "interior_white_support": interior_white_support,
            "alpha_median": float(np.median(values(alpha))),
            "alpha_q10": float(np.quantile(values(alpha), 0.10)),
            "alpha_q90": float(np.quantile(values(alpha), 0.90)),
            "luminance_median": float(np.median(values(luminance))),
            "chroma_median": float(np.median(values(chroma))),
            "local_variance_median": float(np.median(values(local_variance))),
            "gradient_median": float(np.median(values(luminance_gradient))),
            "ribbon_support": float(np.median(values(thin_ribbon))),
            "foreground_variance": foreground_variance,
            "solved_alpha_median": float(np.median(values(solved_alpha))),
            "solved_alpha_spread": alpha_spread,
            "adaptive_support_fraction": float(np.mean(values(adaptive_support_used))),
            "adaptive_support_distance_median": float(np.median(values(adaptive_support_distance))),
            "component_area": float(component_area),
            "component_alpha_max": region_component_alpha_max,
            "isolated_residue": float(isolated_residue),
        }
        vector = _feature_vector(features)

        raw_score = (
            -12.0
            + 7.0 * features["background_similarity"]
            + 6.0 * features["foreground_contrast"]
            + 4.0 * features["solution_stability"]
            + 4.0 * features["ribbon_support"]
            + 1.5 * min(1.0, area / 4.0)
        )
        prior_score = stable_sigmoid(raw_score)
        learned_score = model.predict(vector) if model is not None else None
        confidence = prior_score
        if model is not None and model.accepted:
            confidence = 0.5 * prior_score + 0.5 * learned_score
        if isolated_residue:
            residue_logit = (
                5.5
                + 2.0 * (features["background_similarity"] - 0.94) / 0.06
                + 2.0 * (0.65 - region_component_alpha_max) / 0.65
                + max(0.0, 1.0 - component_area / max(4.0, band_width * 2.0))
            )
            confidence = max(confidence, stable_sigmoid(residue_logit))

        feedback_record = feedback.get(stable_id)
        feedback_label = str(feedback_record.get("label")) if feedback_record else None
        guards = {
            "interior_white_continuation": continued_white,
            "filled_bright_shape": filled_bright_shape,
            "unstable_foreground": not stable_foreground and not isolated_residue,
            "ill_conditioned_composite": ill_conditioned and not isolated_residue,
            "whole_component_removal": would_remove_component,
            "intentional_or_unsure_feedback": feedback_label in {"intentional", "unsure"},
            "spatial_inconsistency": spatially_inconsistent,
        }
        reasons = [name for name, failed in guards.items() if failed]
        if isolated_residue:
            reasons.append("isolated_low_alpha_residue")
        strong_evidence = (
            features["background_similarity"] > 0.54
            and features["foreground_contrast"] > 0.32
            and features["ribbon_support"] > 0.20
        )
        can_clean = not any(guards.values())
        feedback_authorized = feedback_label == "fringe"
        if can_clean and (feedback_authorized or confidence >= AUTO_THRESHOLD):
            decision = "clean"
            reasons.append("exact_fringe_feedback" if feedback_authorized else "automatic_threshold_met")
        elif strong_evidence or any(guards.values()):
            decision = "flag"
            reasons.append("ambiguous_relevant_contamination")
        else:
            decision = "preserve"
            reasons.append("insufficient_fringe_evidence")

        if decision == "clean":
            solved_crop = np.clip(solved_alpha[top:bottom, left:right], 0.0, 1.0)
            proposed = _regularize_alpha(
                region_local, solved_crop,
                normal_y[top:bottom, left:right], normal_x[top:bottom, left:right],
            )
            excessive_clamping = float(np.mean(contradictory[region_ys, region_xs])) > 0.03
            if excessive_clamping:
                decision = "flag"
                reasons = [reason for reason in reasons if reason not in {"automatic_threshold_met", "exact_fringe_feedback"}]
                reasons.extend(["excessive_solution_clamping", "ambiguous_relevant_contamination"])
            else:
                proposed_values = proposed[region_local]
                output_alpha[region_ys, region_xs] = proposed_values
                surviving = proposed_values > 1 / 255
                output_linear[region_ys[surviving], region_xs[surviving]] = inward_rgb[region_ys[surviving], region_xs[surviving]]
                output_linear[region_ys[~surviving], region_xs[~surviving]] = 0.0
                output_alpha[region_ys[~surviving], region_xs[~surviving]] = 0.0
                cleaned_mask[region_ys, region_xs] = True

        if decision == "flag":
            flagged_mask[region_ys, region_xs] = True

        regions.append(
            Region(
                numeric_id, stable_id, region_local, bbox, mask_hash, features, vector,
                prior_score, learned_score, confidence, decision, reasons, guards, feedback_label,
            )
        )

    output_alpha = np.clip(output_alpha, 0.0, 1.0)
    output_linear = np.clip(output_linear, 0.0, 1.0)
    output_linear[output_alpha <= 0.0] = 0.0
    output_rgba = np.empty_like(rgba)
    output_rgba[..., :3] = linear_to_srgb8(output_linear)
    output_rgba[..., 3] = np.rint(output_alpha * 255.0).astype(np.uint8)
    output_rgba[output_rgba[..., 3] == 0, :3] = 0
    changed_pixels = int(np.any(output_rgba != rgba, axis=-1).sum())
    preserved_mask = candidate & ~cleaned_mask & ~flagged_mask
    return Analysis(output_rgba, regions, candidate, cleaned_mask, flagged_mask, preserved_mask, band_width, changed_pixels)


def _composite_rgba(rgba: np.ndarray, background: np.ndarray) -> np.ndarray:
    alpha = rgba[..., 3:4].astype(np.float64) / 255.0
    foreground = SRGB8_TO_LINEAR[rgba[..., :3]]
    bg_linear = SRGB8_TO_LINEAR[background]
    composite = foreground * alpha + bg_linear * (1.0 - alpha)
    return linear_to_srgb8(composite)


def _prepared_rgba(rgba: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    return (
        SRGB8_TO_LINEAR[rgba[..., :3]],
        rgba[..., 3:4].astype(np.float64) / 255.0,
    )


def _composite_prepared(foreground: np.ndarray, alpha: np.ndarray, background: np.ndarray) -> np.ndarray:
    bg_linear = SRGB8_TO_LINEAR[background]
    composite = foreground * alpha + bg_linear * (1.0 - alpha)
    return linear_to_srgb8(composite)


def _checkerboard(height: int, width: int, cell: int = 12) -> np.ndarray:
    yy, xx = np.indices((height, width))
    selector = ((yy // cell) + (xx // cell)) % 2
    gray = np.where(selector[..., None] == 0, 84, 148).astype(np.uint8)
    return np.repeat(gray, 3, axis=2)


def _decision_overlay(rgba: np.ndarray, analysis: Analysis) -> np.ndarray:
    base = _composite_rgba(rgba, np.asarray([38, 38, 38], dtype=np.uint8))
    overlay = base.astype(np.float64)
    for mask, color in (
        (analysis.preserved_mask, np.asarray([64, 128, 255])),
        (analysis.flagged_mask, np.asarray([255, 180, 0])),
        (analysis.cleaned_mask, np.asarray([0, 220, 120])),
    ):
        overlay[mask] = overlay[mask] * 0.30 + color * 0.70
    return np.rint(overlay).astype(np.uint8)


def _diagnostic_sheet(original: np.ndarray, cleaned: np.ndarray) -> np.ndarray:
    height, width = original.shape[:2]
    checker = _checkerboard(height, width)
    backgrounds = (
        np.asarray([0, 0, 0], dtype=np.uint8),
        checker,
        np.asarray([255, 0, 180], dtype=np.uint8),
    )
    original_linear, original_alpha = _prepared_rgba(original)
    cleaned_linear, cleaned_alpha = _prepared_rgba(cleaned)
    rows = []
    separator = np.full((height, 2, 3), 32, dtype=np.uint8)
    for background in backgrounds:
        rows.append(np.concatenate([
            _composite_prepared(original_linear, original_alpha, background), separator,
            _composite_prepared(cleaned_linear, cleaned_alpha, background),
        ], axis=1))
    horizontal = np.full((2, rows[0].shape[1], 3), 32, dtype=np.uint8)
    return np.concatenate([rows[0], horizontal, rows[1], horizontal, rows[2]], axis=0)


def write_diagnostics(
    run_dir: Path,
    relative_path: Path,
    original: np.ndarray,
    analysis: Analysis,
) -> dict[str, str]:
    safe_parts = list(relative_path.with_suffix("").parts)
    diagnostic_dir = run_dir / "diagnostics" / Path(*safe_parts)
    region_dir = diagnostic_dir / "regions"
    region_dir.mkdir(parents=True, exist_ok=True)
    comparison_path = diagnostic_dir / "comparison.png"
    decisions_path = diagnostic_dir / "decisions.png"
    Image.fromarray(_diagnostic_sheet(original, analysis.output_rgba), "RGB").save(comparison_path, format="PNG", compress_level=6)
    decision_overlay = _decision_overlay(original, analysis)
    Image.fromarray(decision_overlay, "RGB").save(decisions_path, format="PNG", compress_level=6)

    for region in analysis.regions:
        if region.decision not in {"clean", "flag"}:
            continue
        left, top, right, bottom = region.bbox
        padding = max(4, analysis.band_width * 3)
        left = max(0, left - padding)
        top = max(0, top - padding)
        right = min(original.shape[1], right + padding)
        bottom = min(original.shape[0], bottom + padding)
        source_crop = original[top:bottom, left:right]
        cleaned_crop = analysis.output_rgba[top:bottom, left:right]
        overlay_crop = decision_overlay[top:bottom, left:right]
        black = np.asarray([0, 0, 0], dtype=np.uint8)
        magenta = np.asarray([255, 0, 180], dtype=np.uint8)
        crop_checker = _checkerboard(bottom - top, right - left, cell=4)
        panels = [
            _composite_rgba(source_crop, black),
            _composite_rgba(cleaned_crop, black),
            _composite_rgba(source_crop, crop_checker),
            _composite_rgba(cleaned_crop, crop_checker),
            _composite_rgba(source_crop, magenta),
            _composite_rgba(cleaned_crop, magenta),
            overlay_crop,
        ]
        spacer = np.full((panels[0].shape[0], 1, 3), 32, dtype=np.uint8)
        sheet = panels[0]
        for panel in panels[1:]:
            sheet = np.concatenate([sheet, spacer, panel], axis=1)
        scale = max(1, min(8, 768 // max(sheet.shape[0], sheet.shape[1])))
        crop_image = Image.fromarray(sheet, "RGB")
        if scale > 1:
            crop_image = crop_image.resize((crop_image.width * scale, crop_image.height * scale), Image.Resampling.NEAREST)
        crop_path = region_dir / f"{region.stable_id}.png"
        crop_image.save(crop_path, format="PNG", compress_level=6)
        region.diagnostic_path = crop_path.relative_to(run_dir).as_posix()

    return {
        "comparison": comparison_path.relative_to(run_dir).as_posix(),
        "decisions": decisions_path.relative_to(run_dir).as_posix(),
    }


def load_feedback(path: Path) -> dict[str, dict[str, Any]]:
    if not path.exists():
        return {}
    latest: dict[str, dict[str, Any]] = {}
    with path.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, start=1):
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError as exc:
                raise CleanupError(f"invalid feedback JSON on line {line_number}: {exc}") from exc
            region_id = record.get("region_id")
            label = record.get("label")
            if not isinstance(region_id, str) or label not in {"fringe", "intentional", "unsure"}:
                raise CleanupError(f"invalid feedback record on line {line_number}")
            latest[region_id] = record
    return latest


def load_model(path: Path, required: bool = False) -> AcceptedModel | None:
    if not path.exists():
        if required:
            raise CleanupError(f"model does not exist: {path}")
        return None
    try:
        data = path.read_bytes()
        payload = json.loads(data)
        if payload.get("feature_schema_version") != FEATURE_SCHEMA_VERSION:
            raise CleanupError(f"model feature schema does not match {FEATURE_SCHEMA_VERSION}")
        names = payload.get("feature_names")
        if names != list(FEATURE_VECTOR_NAMES):
            raise CleanupError("model feature vector does not match this tool")
        normalization = payload["normalization"]
        coefficients = payload["coefficients"]
        means = np.asarray(normalization["means"], dtype=np.float64)
        scales = np.asarray(normalization["scales"], dtype=np.float64)
        weights = np.asarray(coefficients["weights"], dtype=np.float64)
        if means.shape != (len(FEATURE_VECTOR_NAMES),) or scales.shape != means.shape or weights.shape != means.shape:
            raise CleanupError("model coefficient dimensions are invalid")
        if np.any(scales <= 0) or not np.all(np.isfinite(np.concatenate([means, scales, weights]))):
            raise CleanupError("model contains invalid numeric values")
        return AcceptedModel(
            path.resolve(), sha256_bytes(data), means, scales, weights, float(coefficients["bias"]),
            payload.get("acceptance_status") == "accepted",
        )
    except CleanupError:
        if required:
            raise
        return None
    except (KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
        if required:
            raise CleanupError(f"invalid model file {path}: {exc}") from exc
        return None


def _manifest_json(manifest: dict[str, Any]) -> str:
    return json.dumps(manifest, indent=2, sort_keys=True, ensure_ascii=True) + "\n"


def _write_manifest(run_dir: Path, manifest: dict[str, Any]) -> None:
    (run_dir / "manifest.json").write_text(_manifest_json(manifest), encoding="utf-8")


def _image_status(analysis: Analysis) -> str:
    if any(region.decision == "clean" for region in analysis.regions) or analysis.changed_pixels:
        return "cleaned"
    if any(region.decision == "flag" for region in analysis.regions):
        return "flagged"
    return "unchanged"


def _count_decisions(regions: Sequence[Region]) -> dict[str, int]:
    return {decision: sum(region.decision == decision for region in regions) for decision in ("clean", "flag", "preserve")}


def _report_region_card(image_record: dict[str, Any], region: dict[str, Any]) -> str:
    region_id = html.escape(region["region_id"])
    diagnostic = region.get("diagnostic_path")
    diagnostic_html = ""
    if diagnostic:
        diagnostic_html = f'<a href="{html.escape(diagnostic)}"><img loading="lazy" src="{html.escape(diagnostic)}" alt="Region diagnostic"></a>'
    reasons = ", ".join(region.get("reason_codes", []))
    features = region.get("features", {})
    summary = (
        f"background similarity {features.get('background_similarity', 0):.3f}, "
        f"foreground contrast {features.get('foreground_contrast', 0):.3f}, "
        f"stability {features.get('solution_stability', 0):.3f}"
    )
    return f"""
    <article class="region" data-decision="{html.escape(region['decision'])}" data-confidence="{region['confidence']:.8f}" data-reasons="{html.escape(reasons)}">
      <h4>{html.escape(region['decision'].upper())} region {region['numeric_id']} <code>{region_id[:12]}</code></h4>
      {diagnostic_html}
      <p>Confidence {region['confidence']:.6f}. {html.escape(summary)}.</p>
      <p class="reasons">{html.escape(reasons)}</p>
      <div class="labels" data-region-id="{region_id}">
        <button data-label="fringe">Fringe</button><button data-label="intentional">Intentional</button><button data-label="unsure">Unsure</button>
        <input class="note" aria-label="Optional note" placeholder="Optional note">
        <span class="save-state">Review server required to save feedback.</span>
      </div>
    </article>"""


def _report_image_card(record: dict[str, Any]) -> str:
    relative = html.escape(record["relative_path"])
    links = [f'<a href="{html.escape(record["output_path"])}">candidate PNG</a>'] if record.get("output_path") else []
    for label, path in record.get("diagnostics", {}).items():
        links.append(f'<a href="{html.escape(path)}">{html.escape(label)}</a>')
    regions = "".join(_report_region_card(record, region) for region in record.get("regions", []) if region["decision"] in {"flag", "clean"})
    reasons = " ".join(reason for region in record.get("regions", []) for reason in region.get("reason_codes", []))
    return f"""
    <section class="image-result" data-status="{html.escape(record['status'])}" data-filename="{relative.lower()}" data-reasons="{html.escape(reasons.lower())}">
      <h3>{relative} <span class="status {html.escape(record['status'])}">{html.escape(record['status'])}</span></h3>
      <p>{' | '.join(links)} | {record.get('changed_pixels', 0)} changed pixels | band {record.get('candidate_band', '-')} px</p>
      {regions or '<p>No cleaned or flagged contour regions.</p>'}
    </section>"""


def write_report(run_dir: Path, manifest: dict[str, Any]) -> None:
    images = manifest.get("images", [])
    completed = [record for record in images if record.get("status") != "failed"]
    totals = {status: sum(record.get("status") == status for record in images) for status in ("cleaned", "flagged", "unchanged", "failed")}
    flagged_cards = []
    other_cards = []
    for record in completed:
        card = _report_image_card(record)
        if any(region.get("decision") == "flag" for region in record.get("regions", [])):
            flagged_cards.append(card)
        else:
            other_cards.append(card)
    failed_cards = "".join(
        f'<section class="image-result" data-status="failed" data-filename="{html.escape(record["relative_path"].lower())}"><h3>{html.escape(record["relative_path"])} <span class="status failed">failed</span></h3><pre>{html.escape(record.get("error", "unknown error"))}</pre></section>'
        for record in images if record.get("status") == "failed"
    )
    by_name = {Path(record["relative_path"]).name: record for record in completed}
    sample_cards = "".join(_report_image_card(by_name[name]) for name in MANUAL_SAMPLE if name in by_name)
    totals_text = ", ".join(f"{status}: {count}" for status, count in totals.items())
    document = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Card-art edge cleanup report</title>
<style>
:root {{ color-scheme: dark; font-family: system-ui,sans-serif; background:#15171a; color:#e8e8e8 }}
body {{ max-width:1200px; margin:auto; padding:24px }} a {{ color:#86c8ff }}
.controls {{ position:sticky; top:0; background:#20242a; padding:12px; z-index:2; display:flex; gap:10px; flex-wrap:wrap }}
.image-result {{ border:1px solid #414750; border-radius:8px; padding:14px; margin:14px 0; background:#1c2025 }}
.region {{ border-left:5px solid #6585a5; padding:8px 12px; margin:12px 0; background:#242a31 }}
.region[data-decision=flag] {{ border-color:#ffb400 }} .region[data-decision=clean] {{ border-color:#00dc78 }}
.region img {{ max-width:100%; image-rendering:pixelated; border:1px solid #555 }}
.status {{ border-radius:4px; padding:2px 7px; font-size:.75em }} .cleaned {{ background:#176b45 }} .flagged {{ background:#805b00 }} .unchanged {{ background:#424850 }} .failed {{ background:#842b2b }}
button,input,select {{ padding:7px; background:#30363d; color:#fff; border:1px solid #616975; border-radius:4px }}
.save-state {{ font-size:.85em; color:#b9bec6 }} code {{ font-size:.85em }} .hidden {{ display:none }}
</style></head><body>
<h1>Card-art edge cleanup report</h1>
<p>Run <code>{html.escape(manifest['run_id'])}</code>. {html.escape(totals_text)}. Sources unchanged: {str(manifest.get('source_verification', {}).get('all_unchanged', False)).lower()}.</p>
<div class="controls"><label>Status <select id="status"><option value="">all</option><option>cleaned</option><option>flagged</option><option>unchanged</option><option>failed</option></select></label><label>Filename <input id="filename"></label><label>Reason <input id="reason"></label><label>Minimum confidence <input id="confidence" type="number" min="0" max="1" step=".001" value="0"></label></div>
<h2>Flagged regions first</h2>{''.join(flagged_cards) or '<p>No flagged images.</p>'}
<h2>Ten-image manual acceptance sample</h2><p>Inspect these on black, checkerboard, and magenta at normal and magnified size, then label every flagged region through <code>review</code>.</p>{sample_cards or '<p>No named sample images completed.</p>'}
<h2>Other automated results</h2>{''.join(other_cards)}{failed_cards}
<script>
const filters = ['status','filename','reason','confidence'].map(id=>document.getElementById(id));
function applyFilters() {{
 const status=document.getElementById('status').value, filename=document.getElementById('filename').value.toLowerCase(), reason=document.getElementById('reason').value.toLowerCase(), minimum=Number(document.getElementById('confidence').value||0);
 document.querySelectorAll('.image-result').forEach(card=>{{
   const regionMatch=[...card.querySelectorAll('.region')].some(r=>Number(r.dataset.confidence)>=minimum && r.dataset.reasons.toLowerCase().includes(reason));
   const noRegions=card.querySelectorAll('.region').length===0;
   card.classList.toggle('hidden', !!status&&card.dataset.status!==status || !card.dataset.filename.includes(filename) || (!regionMatch && !(noRegions&&minimum===0&&!reason)));
 }});
}}
filters.forEach(control=>control.addEventListener('input',applyFilters));
document.querySelectorAll('.labels button').forEach(button=>button.addEventListener('click',async()=>{{
 const box=button.closest('.labels'), state=box.querySelector('.save-state'); state.textContent='Saving...';
 try {{ const response=await fetch('/label',{{method:'POST',headers:{{'Content-Type':'application/json'}},body:JSON.stringify({{region_id:box.dataset.regionId,label:button.dataset.label,note:box.querySelector('.note').value}})}}); if(!response.ok) throw new Error(await response.text()); state.textContent='Saved '+button.dataset.label+'.'; }}
 catch(error) {{ state.textContent='Could not save: start the review server.'; }}
}}));
</script></body></html>"""
    (run_dir / "report.html").write_text(document, encoding="utf-8")


def _dependency_versions() -> dict[str, str]:
    return {"Pillow": PIL.__version__, "numpy": np.__version__, "scipy": scipy.__version__}


def run_cleanup(args: argparse.Namespace) -> int:
    input_root, inputs = discover_inputs(Path(args.input))
    output_root = validate_output_root(input_root, inputs, Path(args.output_root))
    source_hashes = {path: sha256_file(path) for path, _ in inputs}
    run_id, run_dir = create_run_directory(output_root)
    feedback_path = Path(args.feedback).expanduser().resolve() if args.feedback else DEFAULT_FEEDBACK.resolve()
    feedback = load_feedback(feedback_path) if feedback_path.exists() else {}
    if args.no_model:
        model = None
    elif args.model:
        model = load_model(Path(args.model).expanduser().resolve(), required=True)
    else:
        model = load_model(DEFAULT_MODEL.resolve(), required=False)
    started_clock = time.perf_counter()
    manifest: dict[str, Any] = {
        "tool": "card-art-edge-cleanup",
        "tool_version": TOOL_VERSION,
        "feature_schema_version": FEATURE_SCHEMA_VERSION,
        "python_version": platform.python_version(),
        "dependencies": _dependency_versions(),
        "run_id": run_id,
        "started_at": utc_now(),
        "ended_at": None,
        "input_root": str(input_root),
        "output_root": str(output_root),
        "run_directory": str(run_dir.resolve()),
        "arguments": {
            "input": str(args.input), "output_root": str(args.output_root), "model": args.model,
            "no_model": args.no_model, "feedback": args.feedback, "candidate_band": args.candidate_band,
            "background_color": "".join(f"{channel:02X}" for channel in args.background_color),
            "fail_fast": args.fail_fast, "verbose": args.verbose,
        },
        "model": None if model is None else {
            "path": str(model.path), "sha256": model.digest,
            "acceptance_status": "accepted" if model.accepted else "ranking_only",
        },
        "feedback_path": str(feedback_path),
        "images": [],
        "source_verification": {"all_unchanged": None, "files": []},
        "totals": {},
    }
    _write_manifest(run_dir, manifest)
    failures = 0

    for source, relative in inputs:
        image_started = time.perf_counter()
        output_path = run_dir / "cleaned" / relative
        record: dict[str, Any] = {
            "relative_path": relative.as_posix(),
            "source_path": str(source),
            "source_sha256": source_hashes[source],
            "source_bytes": source.stat().st_size,
            "output_path": output_path.relative_to(run_dir).as_posix(),
        }
        try:
            loaded = load_png(source)
            height, width = loaded.rgba.shape[:2]
            record.update({
                "dimensions": [width, height], "source_mode": loaded.mode,
                "metadata_keys": loaded.metadata_keys,
            })
            analysis = analyze_image(
                loaded.rgba, relative, source_hashes[source], args.background_color,
                args.candidate_band, feedback, model,
            )
            status = _image_status(analysis)
            if analysis.changed_pixels == 0:
                output_path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, output_path)
            else:
                save_rgba(output_path, analysis.output_rgba, loaded.metadata)
            diagnostics: dict[str, str] = {}
            if any(region.decision in {"clean", "flag"} for region in analysis.regions):
                diagnostics = write_diagnostics(run_dir, relative, loaded.rgba, analysis)
            decisions = _count_decisions(analysis.regions)
            record.update({
                "status": status,
                "candidate_band": analysis.band_width,
                "candidate_pixels": int(analysis.candidate_mask.sum()),
                "changed_pixels": analysis.changed_pixels,
                "region_counts": decisions,
                "regions": [region.to_manifest() for region in analysis.regions],
                "diagnostics": diagnostics,
                "output_sha256": sha256_file(output_path),
            })
            if args.verbose:
                print(f"{relative}: {status}, {analysis.changed_pixels} changed pixels, {decisions}")
                for region in analysis.regions:
                    print(f"  {region.stable_id[:12]} {region.decision} {region.confidence:.6f} {','.join(region.reasons)}")
        except Exception as exc:  # Individual images are isolated by product design.
            if isinstance(exc, (KeyboardInterrupt, SystemExit)):
                raise
            failures += 1
            record.update({"status": "failed", "error": f"{type(exc).__name__}: {exc}", "regions": []})
            print(f"error: {relative}: {record['error']}", file=sys.stderr)
        record["elapsed_seconds"] = round(time.perf_counter() - image_started, 6)
        manifest["images"].append(record)
        _write_manifest(run_dir, manifest)
        if failures and args.fail_fast:
            break

    verification = []
    for source, relative in inputs:
        current_hash = sha256_file(source)
        verification.append({
            "relative_path": relative.as_posix(), "before_sha256": source_hashes[source],
            "after_sha256": current_hash, "unchanged": current_hash == source_hashes[source],
        })
    manifest["source_verification"] = {
        "all_unchanged": all(item["unchanged"] for item in verification), "files": verification,
    }
    manifest["totals"] = {
        "discovered": len(inputs),
        "processed": len(manifest["images"]),
        "cleaned": sum(item.get("status") == "cleaned" for item in manifest["images"]),
        "flagged": sum(item.get("status") == "flagged" for item in manifest["images"]),
        "unchanged": sum(item.get("status") == "unchanged" for item in manifest["images"]),
        "failed": failures,
        "regions": {
            decision: sum(item.get("region_counts", {}).get(decision, 0) for item in manifest["images"])
            for decision in ("clean", "flag", "preserve")
        },
    }
    manifest["elapsed_seconds"] = round(time.perf_counter() - started_clock, 6)
    manifest["ended_at"] = utc_now()
    _write_manifest(run_dir, manifest)
    write_report(run_dir, manifest)
    print(run_dir)
    return 1 if failures or not manifest["source_verification"]["all_unchanged"] else 0


def find_latest_run(output_root: Path = DEFAULT_OUTPUT_ROOT) -> Path:
    root = output_root.expanduser().resolve()
    if not root.is_dir():
        raise CleanupError(f"no cleanup runs found under {root}")
    completed: list[tuple[str, Path]] = []
    for directory in root.iterdir():
        manifest_path = directory / "manifest.json"
        report_path = directory / "report.html"
        if not directory.is_dir() or not manifest_path.is_file() or not report_path.is_file():
            continue
        try:
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        ended_at = manifest.get("ended_at")
        if isinstance(ended_at, str) and ended_at:
            completed.append((ended_at, directory))
    if not completed:
        raise CleanupError(f"no completed cleanup runs found under {root}")
    return max(completed, key=lambda item: (item[0], item[1].name))[1]


def _review_region_index(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    index: dict[str, dict[str, Any]] = {}
    for image_record in manifest.get("images", []):
        for region in image_record.get("regions", []):
            region_id = region.get("region_id")
            if not isinstance(region_id, str):
                continue
            index[region_id] = {
                "run_id": manifest.get("run_id"),
                "tool_version": manifest.get("tool_version"),
                "feature_schema_version": manifest.get("feature_schema_version"),
                "source_relative_path": image_record.get("relative_path"),
                "source_sha256": image_record.get("source_sha256"),
                "bbox": region.get("bbox"),
                "mask_hash": region.get("mask_hash"),
                "features": region.get("features"),
                "feature_vector": region.get("feature_vector"),
                "deterministic_prior": region.get("deterministic_prior"),
                "learned_score": region.get("learned_score"),
                "decision": region.get("decision"),
                "reason_codes": region.get("reason_codes"),
                "diagnostic_path": region.get("diagnostic_path"),
                "comparison_path": image_record.get("diagnostics", {}).get("comparison"),
            }
    return index


def create_review_server(run_dir: Path, feedback_path: Path, port: int = 0) -> ThreadingHTTPServer:
    run_root = run_dir.expanduser().resolve(strict=True)
    manifest_path = run_root / "manifest.json"
    if not manifest_path.is_file() or not (run_root / "report.html").is_file():
        raise CleanupError(f"run directory is missing manifest.json or report.html: {run_root}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    region_index = _review_region_index(manifest)
    feedback_target = feedback_path.expanduser().resolve()
    append_lock = threading.Lock()

    class ReviewHandler(BaseHTTPRequestHandler):
        server_version = "CardArtReview/1.0"

        def log_message(self, format: str, *values: Any) -> None:
            if getattr(self.server, "verbose", False):
                super().log_message(format, *values)

        def _send(self, status: HTTPStatus, data: bytes, content_type: str) -> None:
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler API
            parsed = urlparse(self.path)
            request_path = unquote(parsed.path)
            relative = Path("report.html") if request_path in {"", "/"} else Path(request_path.lstrip("/"))
            candidate = (run_root / relative).resolve()
            if not is_relative_to(candidate, run_root) or not candidate.is_file():
                self._send(HTTPStatus.NOT_FOUND, b"Not found\n", "text/plain; charset=utf-8")
                return
            mime_type = mimetypes.guess_type(candidate.name)[0] or "application/octet-stream"
            self._send(HTTPStatus.OK, candidate.read_bytes(), mime_type)

        def do_POST(self) -> None:  # noqa: N802 - BaseHTTPRequestHandler API
            if urlparse(self.path).path != "/label":
                self._send(HTTPStatus.NOT_FOUND, b"Not found\n", "text/plain; charset=utf-8")
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > 64 * 1024:
                    raise CleanupError("invalid request size")
                payload = json.loads(self.rfile.read(length))
                region_id = payload.get("region_id")
                label = payload.get("label")
                note = payload.get("note", "")
                if region_id not in region_index:
                    raise CleanupError("unknown region ID")
                if label not in {"fringe", "intentional", "unsure"}:
                    raise CleanupError("label must be fringe, intentional, or unsure")
                if not isinstance(note, str) or len(note) > 2000:
                    raise CleanupError("note must be a string of at most 2000 characters")
                record = dict(region_index[region_id])
                record.update({"region_id": region_id, "label": label, "note": note, "labeled_at": utc_now()})
                encoded = (json.dumps(record, sort_keys=True, ensure_ascii=True) + "\n").encode("utf-8")
                with append_lock:
                    feedback_target.parent.mkdir(parents=True, exist_ok=True)
                    with feedback_target.open("ab") as stream:
                        stream.write(encoded)
                        stream.flush()
                        os.fsync(stream.fileno())
                self._send(HTTPStatus.OK, b'{"saved":true}\n', "application/json")
            except (CleanupError, json.JSONDecodeError, TypeError, ValueError) as exc:
                self._send(HTTPStatus.BAD_REQUEST, (str(exc) + "\n").encode("utf-8"), "text/plain; charset=utf-8")

    server = ThreadingHTTPServer(("127.0.0.1", port), ReviewHandler)
    server.verbose = False  # type: ignore[attr-defined]
    return server


def review_run(args: argparse.Namespace) -> int:
    run_dir = Path(args.run_dir).expanduser().resolve() if args.run_dir else find_latest_run()
    feedback_path = Path(args.feedback).expanduser().resolve()
    server = create_review_server(run_dir, feedback_path, args.port)
    server.verbose = args.verbose  # type: ignore[attr-defined]
    host, port = server.server_address
    url = f"http://{host}:{port}/"
    print(f"Reviewing {run_dir}")
    print(f"Feedback: {feedback_path}")
    print(url)
    if not args.no_open:
        webbrowser.open(url)
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        print("\nReview server stopped.")
    finally:
        server.server_close()
    return 0


def _training_records(feedback_path: Path) -> list[dict[str, Any]]:
    latest = load_feedback(feedback_path)
    records = [record for record in latest.values() if record.get("label") in {"fringe", "intentional"}]
    valid = []
    for record in records:
        vector = record.get("feature_vector")
        if isinstance(vector, dict) and all(name in vector for name in FEATURE_VECTOR_NAMES):
            values = [float(vector[name]) for name in FEATURE_VECTOR_NAMES]
        elif isinstance(vector, list) and len(vector) == len(FEATURE_VECTOR_NAMES):
            values = [float(value) for value in vector]
        else:
            continue
        if not np.all(np.isfinite(values)) or not isinstance(record.get("source_relative_path"), str):
            continue
        copied = dict(record)
        copied["_vector"] = values
        valid.append(copied)
    return valid


def _fit_logistic(x: np.ndarray, y: np.ndarray, regularization: float) -> tuple[np.ndarray, float]:
    feature_count = x.shape[1]

    def objective(parameters: np.ndarray) -> tuple[float, np.ndarray]:
        weights = parameters[:feature_count]
        bias = parameters[-1]
        logits = x @ weights + bias
        loss = float(np.mean(np.logaddexp(0.0, logits) - y * logits) + 0.5 * regularization * np.dot(weights, weights))
        probabilities = np.asarray([stable_sigmoid(float(value)) for value in logits])
        error = probabilities - y
        gradient = np.concatenate([
            x.T @ error / len(y) + regularization * weights,
            np.asarray([float(np.mean(error))]),
        ])
        return loss, gradient

    result = optimize.minimize(
        objective, np.zeros(feature_count + 1), jac=True, method="L-BFGS-B",
        options={"maxiter": 1000, "ftol": 1e-12},
    )
    if not result.success or not np.all(np.isfinite(result.x)):
        raise CleanupError(f"logistic optimization failed: {result.message}")
    return result.x[:feature_count], float(result.x[-1])


def _normalize(training_x: np.ndarray, other_x: np.ndarray | None = None) -> tuple[np.ndarray, np.ndarray | None, np.ndarray, np.ndarray]:
    means = np.mean(training_x, axis=0)
    scales = np.std(training_x, axis=0)
    scales = np.where(scales < 1e-8, 1.0, scales)
    normalized_training = (training_x - means) / scales
    normalized_other = None if other_x is None else (other_x - means) / scales
    return normalized_training, normalized_other, means, scales


def train_model(feedback_path: Path, output_path: Path) -> dict[str, Any]:
    records = _training_records(feedback_path)
    fringe_count = sum(record["label"] == "fringe" for record in records)
    intentional_count = sum(record["label"] == "intentional" for record in records)
    images = sorted({record["source_relative_path"] for record in records})
    training_digest = sha256_bytes(
        "\n".join(json.dumps({key: value for key, value in record.items() if key != "_vector"}, sort_keys=True) for record in records).encode("utf-8")
    )
    sufficient = fringe_count >= 10 and intentional_count >= 10 and len(images) >= 3
    base_payload: dict[str, Any] = {
        "tool_version": TOOL_VERSION,
        "feature_schema_version": FEATURE_SCHEMA_VERSION,
        "feature_names": list(FEATURE_VECTOR_NAMES),
        "created_at": utc_now(),
        "training_data_digest": training_digest,
        "training_counts": {"fringe": fringe_count, "intentional": intentional_count, "images": len(images)},
        "threshold": AUTO_THRESHOLD,
    }
    if not sufficient:
        payload = {
            **base_payload,
            "acceptance_status": "ranking_only",
            "regularization": None,
            "normalization": {"means": [0.0] * len(FEATURE_VECTOR_NAMES), "scales": [1.0] * len(FEATURE_VECTOR_NAMES)},
            "coefficients": {"weights": [0.0] * len(FEATURE_VECTOR_NAMES), "bias": 0.0},
            "validation_folds": [],
            "metrics": {
                "accepted": False,
                "reason": "requires at least 10 fringe and 10 intentional labels across at least three source images",
                "held_out_log_loss": None,
                "automatic_fringe": 0,
                "automatic_intentional_false_positives": 0,
            },
        }
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        return payload

    x = np.asarray([record["_vector"] for record in records], dtype=np.float64)
    y = np.asarray([1.0 if record["label"] == "fringe" else 0.0 for record in records], dtype=np.float64)
    groups = np.asarray([record["source_relative_path"] for record in records])
    group_names = sorted(set(groups))
    fold_count = min(5, len(group_names))
    fold_groups = [group_names[index::fold_count] for index in range(fold_count)]
    regularization_grid = (0.01, 0.1, 1.0, 10.0)
    candidates = []
    for regularization in regularization_grid:
        probabilities = np.zeros(len(records), dtype=np.float64)
        fold_summaries = []
        for fold_number, held_groups in enumerate(fold_groups):
            held_mask = np.isin(groups, held_groups)
            training_mask = ~held_mask
            if not np.any(held_mask) or len(np.unique(y[training_mask])) < 2:
                probabilities[held_mask] = float(np.mean(y[training_mask])) if np.any(training_mask) else 0.5
                fold_summaries.append({"fold": fold_number, "held_out_images": held_groups, "fallback": True})
                continue
            train_x, held_x, _, _ = _normalize(x[training_mask], x[held_mask])
            weights, bias = _fit_logistic(train_x, y[training_mask], regularization)
            logits = held_x @ weights + bias
            probabilities[held_mask] = [stable_sigmoid(float(value)) for value in logits]
            fold_summaries.append({"fold": fold_number, "held_out_images": held_groups, "fallback": False})
        clipped = np.clip(probabilities, 1e-12, 1 - 1e-12)
        log_loss = float(-np.mean(y * np.log(clipped) + (1 - y) * np.log(1 - clipped)))
        intentional_fp = int(np.sum((y == 0) & (probabilities >= AUTO_THRESHOLD)))
        automatic_fringe = int(np.sum((y == 1) & (probabilities >= AUTO_THRESHOLD)))
        candidates.append({
            "regularization": regularization, "probabilities": probabilities,
            "log_loss": log_loss, "intentional_fp": intentional_fp,
            "automatic_fringe": automatic_fringe, "folds": fold_summaries,
        })
    selected = min(candidates, key=lambda candidate: (candidate["intentional_fp"], candidate["log_loss"]))
    normalized_x, _, means, scales = _normalize(x)
    weights, bias = _fit_logistic(normalized_x, y, selected["regularization"])
    accepted = selected["intentional_fp"] == 0 and selected["automatic_fringe"] >= 1
    payload = {
        **base_payload,
        "acceptance_status": "accepted" if accepted else "ranking_only",
        "regularization": selected["regularization"],
        "normalization": {"means": means.tolist(), "scales": scales.tolist()},
        "coefficients": {"weights": weights.tolist(), "bias": bias},
        "validation_folds": selected["folds"],
        "metrics": {
            "accepted": accepted,
            "reason": "held-out acceptance criteria met" if accepted else "held-out automatic-decision criteria not met",
            "held_out_log_loss": selected["log_loss"],
            "automatic_fringe": selected["automatic_fringe"],
            "automatic_intentional_false_positives": selected["intentional_fp"],
            "regularization_candidates": [
                {
                    "regularization": candidate["regularization"], "held_out_log_loss": candidate["log_loss"],
                    "automatic_fringe": candidate["automatic_fringe"],
                    "automatic_intentional_false_positives": candidate["intentional_fp"],
                }
                for candidate in candidates
            ],
        },
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return payload


def train_command(args: argparse.Namespace) -> int:
    feedback_path = Path(args.feedback).expanduser().resolve()
    output_path = Path(args.output).expanduser().resolve()
    payload = train_model(feedback_path, output_path)
    metrics = payload["metrics"]
    print(f"{output_path}: {payload['acceptance_status']}")
    print(json.dumps(metrics, indent=2, sort_keys=True))
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Conservatively detect and clean white-background fringe around cropped RGBA card art.",
    )
    parser.add_argument("--version", action="version", version=f"%(prog)s {TOOL_VERSION}")
    subparsers = parser.add_subparsers(dest="command", required=True)

    run_parser = subparsers.add_parser("run", help="process one PNG or a directory of PNGs")
    run_parser.add_argument("input", nargs="?", default=str(DEFAULT_INPUT), help=f"PNG or directory (default: {DEFAULT_INPUT})")
    run_parser.add_argument("--output-root", default=str(DEFAULT_OUTPUT_ROOT), help=f"timestamped run parent (default: {DEFAULT_OUTPUT_ROOT})")
    run_parser.add_argument("--model", help="specific model.json; its schema must match")
    run_parser.add_argument("--no-model", action="store_true", help="use deterministic scoring only")
    run_parser.add_argument("--feedback", help=f"exact region overrides (default: {DEFAULT_FEEDBACK})")
    run_parser.add_argument("--candidate-band", type=int, help="diagnostic fixed candidate-band width")
    run_parser.add_argument("--background-color", type=parse_hex_color, default=(255, 255, 255), metavar="RRGGBB", help="assumed source background (default: FFFFFF)")
    run_parser.add_argument("--fail-fast", action="store_true", help="stop after the first image error, after writing a partial manifest")
    run_parser.add_argument("--verbose", action="store_true", help="print per-image and per-region decisions")
    run_parser.set_defaults(handler=run_cleanup)

    review_parser = subparsers.add_parser("review", help="serve a completed report locally and collect region labels")
    review_parser.add_argument("run_dir", nargs="?", help="completed run (default: most recent)")
    review_parser.add_argument("--feedback", default=str(DEFAULT_FEEDBACK), help=f"append-only label file (default: {DEFAULT_FEEDBACK})")
    review_parser.add_argument("--port", type=int, default=0, help="local port (default: automatically selected)")
    review_parser.add_argument("--no-open", action="store_true", help="do not open the default browser")
    review_parser.add_argument("--verbose", action="store_true", help="log local HTTP requests")
    review_parser.set_defaults(handler=review_run)

    train_parser = subparsers.add_parser("train", help="fit and validate a region-ranking classifier from feedback")
    train_parser.add_argument("--feedback", default=str(DEFAULT_FEEDBACK), help=f"feedback JSONL (default: {DEFAULT_FEEDBACK})")
    train_parser.add_argument("--output", default=str(DEFAULT_MODEL), help=f"model output (default: {DEFAULT_MODEL})")
    train_parser.set_defaults(handler=train_command)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.handler(args))
    except CleanupError as exc:
        parser.error(str(exc))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
