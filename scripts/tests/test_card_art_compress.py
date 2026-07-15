from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

from scripts import card_art_compress as compress


def write_rgba(path: Path, width: int, height: int) -> None:
    rgba = np.zeros((height, width, 4), dtype=np.uint8)
    rgba[:, :, :3] = (40, 80, 120)
    rgba[height // 4 : 3 * height // 4, width // 4 : 3 * width // 4, :3] = (220, 220, 220)
    rgba[height // 4 : 3 * height // 4, width // 4 : 3 * width // 4, 3] = 255
    Image.fromarray(rgba, "RGBA").save(path, compress_level=0)


class FitBoundsTests(unittest.TestCase):
    def test_small_image_is_not_upscaled(self) -> None:
        bounds = compress.TargetBounds(max_width=408, max_height=540)
        self.assertEqual(compress.fit_within_bounds(200, 250, bounds), (200, 250))

    def test_large_image_is_downscaled_to_fit(self) -> None:
        bounds = compress.TargetBounds(max_width=408, max_height=540)
        width, height = compress.fit_within_bounds(1086, 1448, bounds)
        self.assertLessEqual(width, bounds.max_width)
        self.assertLessEqual(height, bounds.max_height)
        self.assertAlmostEqual(width / height, 1086 / 1448, places=3)


class RunWorkflowTests(unittest.TestCase):
    def test_run_writes_compressed_outputs_and_report(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source_dir = root / "input"
            output_root = root / "runs"
            source_dir.mkdir()
            source_path = source_dir / "sample.png"
            write_rgba(source_path, 1086, 1448)

            exit_code = compress.main(
                [
                    "run",
                    str(source_dir),
                    "--output-root",
                    str(output_root),
                    "--verbose",
                ],
            )
            self.assertEqual(exit_code, 0)

            run_dirs = sorted(output_root.iterdir())
            self.assertEqual(len(run_dirs), 1)
            run_dir = run_dirs[0]
            report = json.loads((run_dir / "report.json").read_text(encoding="utf-8"))
            self.assertEqual(report["summary"]["images_ok"], 1)
            self.assertEqual(report["summary"]["resized_count"], 1)

            output_path = run_dir / "compressed" / "sample.png"
            self.assertTrue(output_path.is_file())
            self.assertLess(output_path.stat().st_size, source_path.stat().st_size)

            with Image.open(output_path) as image:
                self.assertEqual(image.size, (405, 540))

            self.assertEqual(compress.sha256_file(source_path), report["images"][0]["source_sha256"])

    def test_run_refuses_run_dir_inside_input_tree(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source_dir = root / "input"
            source_dir.mkdir()
            write_rgba(source_dir / "sample.png", 64, 64)

            with self.assertRaises(SystemExit):
                compress.main(
                    [
                        "run",
                        str(source_dir),
                        "--output-dir",
                        str(source_dir / "compressed"),
                    ],
                )

    def test_run_refuses_output_that_would_overwrite_input(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source_dir = root / "input"
            source_dir.mkdir()
            write_rgba(source_dir / "sample.png", 64, 64)

            with self.assertRaises(SystemExit):
                compress.main(
                    [
                        "run",
                        str(source_dir),
                        "--output-dir",
                        str(source_dir),
                    ],
                )


class ApplyWorkflowTests(unittest.TestCase):
    def test_apply_copies_outputs_and_backups(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            source_dir = root / "input"
            target_dir = root / "target"
            backup_dir = root / "backup"
            output_root = root / "runs"
            source_dir.mkdir()
            target_dir.mkdir()
            source_path = source_dir / "sample.png"
            write_rgba(source_path, 1086, 1448)

            exit_code = compress.main(["run", str(source_dir), "--output-root", str(output_root)])
            self.assertEqual(exit_code, 0)
            run_dir = sorted(output_root.iterdir())[0]

            exit_code = compress.main(
                [
                    "apply",
                    str(run_dir),
                    "--target",
                    str(target_dir),
                    "--backup-dir",
                    str(backup_dir),
                ],
            )
            self.assertEqual(exit_code, 0)

            applied_path = target_dir / "sample.png"
            backup_path = backup_dir / "sample.png"
            self.assertTrue(applied_path.is_file())
            self.assertTrue(backup_path.is_file())
            self.assertEqual(compress.sha256_file(source_path), compress.sha256_file(backup_path))
            self.assertLess(applied_path.stat().st_size, backup_path.stat().st_size)


if __name__ == "__main__":
    unittest.main()
