#!/usr/bin/env python3
"""Bake hourglass frame/sand PNG masks from webapp SVG paths (48x64, white on transparent)."""

from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
CONTENT = ROOT / "Content"
SCALE = 4
W, H = 12 * SCALE, 16 * SCALE


def scale_point(x: float, y: float) -> tuple[float, float]:
    return x * SCALE, y * SCALE


def bake_sand() -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    points = [
        scale_point(5.15, 8.85),
        scale_point(6.85, 8.85),
        scale_point(9.55, 13.85),
        scale_point(2.45, 13.85),
    ]
    draw.polygon(points, fill=(255, 255, 255, 255))
    return img


def bake_frame() -> Image.Image:
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    stroke = max(1, round(1.45 * SCALE))
    outline = [
        scale_point(2, 1.35),
        scale_point(10, 1.35),
        scale_point(6.75, 8),
        scale_point(10, 14.65),
        scale_point(2, 14.65),
        scale_point(5.25, 8),
        scale_point(2, 1.35),
    ]
    draw.line(outline, fill=(255, 255, 255, 255), width=stroke, joint="curve")
    return img


def main() -> None:
    sand_path = CONTENT / "time_icon_hourglass_sand.png"
    frame_path = CONTENT / "time_icon_hourglass_frame.png"
    bake_sand().save(sand_path)
    bake_frame().save(frame_path)
    print(f"Wrote {sand_path}")
    print(f"Wrote {frame_path}")


if __name__ == "__main__":
    main()
