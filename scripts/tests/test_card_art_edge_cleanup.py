from __future__ import annotations

import argparse
import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from pathlib import Path

import numpy as np
from PIL import Image

from scripts import card_art_edge_cleanup as cleanup


def empty_rgba(width: int = 32, height: int = 32) -> np.ndarray:
    return np.zeros((height, width, 4), dtype=np.uint8)


def dark_subject(width: int = 32, height: int = 32) -> np.ndarray:
    rgba = empty_rgba(width, height)
    rgba[7:25, 8:25, :3] = (24, 48, 72)
    rgba[7:25, 8:25, 3] = 255
    return rgba


def with_white_sliver() -> np.ndarray:
    rgba = dark_subject()
    rgba[8:24, 7, :3] = 255
    rgba[8:24, 7, 3] = 255
    return rgba


def analyze(rgba: np.ndarray, **kwargs: object) -> cleanup.Analysis:
    return cleanup.analyze_image(
        rgba,
        Path("fixture.png"),
        cleanup.sha256_bytes(rgba.tobytes()),
        **kwargs,
    )


def save_fixture(path: Path, rgba: np.ndarray) -> None:
    Image.fromarray(rgba, "RGBA").save(path)


class DetectionTests(unittest.TestCase):
    def test_opaque_white_sliver_is_cleaned(self) -> None:
        result = analyze(with_white_sliver())
        self.assertTrue(any(region.decision == "clean" for region in result.regions))
        self.assertTrue(np.all(result.output_rgba[9:23, 7, 3] < 16))

    def test_antialiased_white_composite_is_decontaminated(self) -> None:
        rgba = dark_subject()
        foreground = np.asarray([20, 50, 80], dtype=np.float64) / 255
        observed = cleanup.linear_to_srgb(
            0.5 * cleanup.srgb_to_linear(foreground) + 0.5
        )
        rgba[8:24, 7, :3] = np.rint(observed * 255).astype(np.uint8)
        rgba[8:24, 7, 3] = 255
        result = analyze(rgba)
        self.assertTrue(any(region.decision == "clean" for region in result.regions))
        self.assertLess(abs(int(np.median(result.output_rgba[9:23, 7, 3])) - 128), 8)
        self.assertLess(np.linalg.norm(np.median(result.output_rgba[9:23, 7, :3], axis=0) - np.asarray([20, 50, 80])), 14)

    def test_intentional_white_object_is_preserved_and_flagged(self) -> None:
        rgba = empty_rgba()
        rgba[7:25, 7:25] = (255, 255, 255, 255)
        result = analyze(rgba)
        self.assertFalse(any(region.decision == "clean" for region in result.regions))
        self.assertTrue(any(region.decision == "flag" for region in result.regions))
        np.testing.assert_array_equal(result.output_rgba, rgba)

    def test_white_highlight_with_white_interior_is_preserved(self) -> None:
        rgba = dark_subject()
        rgba[7:25, 8:14, :3] = 255
        result = analyze(rgba)
        self.assertFalse(any(region.decision == "clean" and region.bbox[0] < 14 for region in result.regions))
        np.testing.assert_array_equal(result.output_rgba[8:24, 8:13], rgba[8:24, 8:13])

    def test_thin_intentional_white_ring_is_not_deleted(self) -> None:
        rgba = empty_rgba(40, 40)
        yy, xx = np.indices((40, 40))
        ring = (np.hypot(xx - 20, yy - 20) >= 11) & (np.hypot(xx - 20, yy - 20) <= 12)
        rgba[ring] = (255, 255, 255, 255)
        result = analyze(rgba)
        self.assertFalse(any(region.decision == "clean" for region in result.regions))
        np.testing.assert_array_equal(result.output_rgba, rgba)

    def test_disconnected_white_particle_is_not_removed(self) -> None:
        rgba = dark_subject()
        rgba[2:4, 2:4] = (255, 255, 255, 255)
        result = analyze(rgba)
        np.testing.assert_array_equal(result.output_rgba[2:4, 2:4], rgba[2:4, 2:4])

    def test_low_alpha_isolated_white_residue_is_removed(self) -> None:
        rgba = dark_subject()
        rgba[2, 2] = (255, 255, 255, 96)
        result = analyze(rgba)
        self.assertEqual(int(result.output_rgba[2, 2, 3]), 0)
        self.assertTrue(any("isolated_low_alpha_residue" in region.reasons for region in result.regions))

    def test_adaptive_support_cleans_fringe_on_thin_component(self) -> None:
        rgba = empty_rgba()
        rgba[7:25, 14] = (24, 48, 72, 255)
        rgba[7:25, 13] = (255, 255, 255, 255)
        result = analyze(rgba)
        self.assertTrue(any(region.decision == "clean" for region in result.regions))
        self.assertTrue(any(region.features["adaptive_support_fraction"] > 0 for region in result.regions))
        self.assertTrue(np.all(result.output_rgba[9:23, 13, 3] < 16))
        self.assertTrue(np.all(result.output_rgba[9:23, 14, 3] == 255))

    def test_transparent_hole_remains_transparent(self) -> None:
        rgba = dark_subject(40, 40)
        rgba[13:19, 14:20] = 0
        result = analyze(rgba)
        self.assertTrue(np.all(result.output_rgba[13:19, 14:20] == 0))

    def test_clean_dark_edge_is_unchanged(self) -> None:
        rgba = dark_subject()
        result = analyze(rgba)
        np.testing.assert_array_equal(result.output_rgba, rgba)
        self.assertEqual(result.changed_pixels, 0)

    def test_colored_edge_is_unchanged(self) -> None:
        rgba = dark_subject()
        rgba[8:24, 7] = (220, 35, 40, 255)
        result = analyze(rgba)
        np.testing.assert_array_equal(result.output_rgba, rgba)


class ReconstructionTests(unittest.TestCase):
    def test_recovered_edge_composites_match_expected_backgrounds(self) -> None:
        rgba = dark_subject()
        foreground = np.asarray([24, 48, 72], dtype=np.float64) / 255
        alpha = 0.42
        observed = cleanup.linear_to_srgb(alpha * cleanup.srgb_to_linear(foreground) + (1 - alpha))
        rgba[9:23, 7, :3] = np.rint(observed * 255).astype(np.uint8)
        rgba[9:23, 7, 3] = 255
        result = analyze(rgba)
        recovered = result.output_rgba[12, 7]
        self.assertAlmostEqual(recovered[3] / 255, alpha, delta=0.04)
        self.assertLess(np.linalg.norm(recovered[:3].astype(float) - np.asarray([24, 48, 72])), 12)
        for background in ([0, 0, 0], [128, 128, 128], [255, 255, 255], [255, 0, 180]):
            actual = cleanup._composite_rgba(result.output_rgba[12:13, 7:8], np.asarray(background))[0, 0]
            expected_linear = alpha * cleanup.srgb_to_linear(foreground) + (1 - alpha) * cleanup.srgb_to_linear(np.asarray(background) / 255)
            expected = np.rint(cleanup.linear_to_srgb(expected_linear) * 255)
            self.assertLess(np.max(np.abs(actual.astype(float) - expected)), 7)

    def test_transparent_rgb_is_zero_and_dimensions_preserved(self) -> None:
        rgba = dark_subject(37, 19)
        rgba[0, 0] = (240, 240, 240, 0)
        result = analyze(rgba)
        self.assertEqual(result.output_rgba.shape, rgba.shape)
        np.testing.assert_array_equal(result.output_rgba[0, 0], (0, 0, 0, 0))

    def test_processing_is_deterministic(self) -> None:
        rgba = with_white_sliver()
        first = analyze(rgba)
        second = analyze(rgba)
        np.testing.assert_array_equal(first.output_rgba, second.output_rgba)
        self.assertEqual([r.stable_id for r in first.regions], [r.stable_id for r in second.regions])

    def test_reprocessing_clean_output_is_idempotent(self) -> None:
        first = analyze(with_white_sliver())
        second = analyze(first.output_rgba)
        np.testing.assert_array_equal(second.output_rgba, first.output_rgba)


class ScaleAndTopologyTests(unittest.TestCase):
    def test_candidate_band_sizing(self) -> None:
        self.assertEqual(cleanup.candidate_band_width(64, 64), 2)
        self.assertEqual(cleanup.candidate_band_width(1024, 1536), 4)
        self.assertEqual(cleanup.candidate_band_width(4096, 4096), 8)
        self.assertEqual(cleanup.candidate_band_width(128, 2048), 2)
        self.assertEqual(cleanup.candidate_band_width(2048, 128), 2)

    def test_multiple_components_are_independent(self) -> None:
        rgba = empty_rgba(48, 32)
        rgba[7:25, 8:20] = (24, 48, 72, 255)
        rgba[7:25, 7] = (255, 255, 255, 255)
        rgba[8:24, 30:42] = (255, 255, 255, 255)
        result = analyze(rgba)
        self.assertTrue(np.all(result.output_rgba[9:23, 7, 3] < 16))
        self.assertTrue(np.all(result.output_rgba[9:23, 31:41, 3] == 255))

    def test_region_split_respects_distinct_normals(self) -> None:
        mask = np.zeros((5, 5), dtype=bool)
        mask[2, 1:4] = True
        normal_y = np.zeros((5, 5), dtype=float)
        normal_x = np.ones((5, 5), dtype=float)
        normal_x[2, 3] = -1
        evidence = np.ones((5, 5), dtype=float) * 0.8
        pieces = cleanup._split_regions(mask, normal_y, normal_x, evidence)
        self.assertEqual(len(pieces), 2)

    def test_short_segments_do_not_fail_spatial_consistency_alone(self) -> None:
        self.assertFalse(cleanup._is_spatially_inconsistent(8, 4, 1.0))
        self.assertTrue(cleanup._is_spatially_inconsistent(32, 4, 1.0))


class WorkflowTests(unittest.TestCase):
    def test_output_collision_checks_cover_aliases_and_symlinks(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source_dir = root / "input"
            source_dir.mkdir()
            source = source_dir / "art.png"
            save_fixture(source, dark_subject())
            input_root, inputs = cleanup.discover_inputs(source_dir / ".")
            with self.assertRaises(cleanup.CleanupError):
                cleanup.validate_output_root(input_root, inputs, source_dir / "generated")
            alias = root / "alias"
            alias.symlink_to(source_dir, target_is_directory=True)
            with self.assertRaises(cleanup.CleanupError):
                cleanup.validate_output_root(input_root, inputs, alias / "generated")
            with self.assertRaises(cleanup.CleanupError):
                cleanup.validate_output_root(source.parent, [(source, Path(source.name))], source)

    def test_corrupt_png_is_recorded_and_other_input_continues(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            input_dir = root / "input"
            input_dir.mkdir()
            valid = input_dir / "a-valid.png"
            corrupt = input_dir / "b-corrupt.png"
            save_fixture(valid, dark_subject())
            corrupt.write_bytes(b"not a PNG")
            before = {path.name: cleanup.sha256_file(path) for path in (valid, corrupt)}
            args = argparse.Namespace(
                input=str(input_dir), output_root=str(root / "runs"), model=None, no_model=True,
                feedback=None, candidate_band=None, background_color=(255, 255, 255),
                fail_fast=False, verbose=False,
            )
            self.assertEqual(cleanup.run_cleanup(args), 1)
            run = cleanup.find_latest_run(root / "runs")
            manifest = json.loads((run / "manifest.json").read_text())
            self.assertEqual(manifest["totals"]["failed"], 1)
            self.assertTrue((run / "cleaned" / valid.name).is_file())
            self.assertTrue(manifest["source_verification"]["all_unchanged"])
            self.assertEqual(before, {path.name: cleanup.sha256_file(path) for path in (valid, corrupt)})

    def test_stable_region_id_changes_with_source_or_schema_input(self) -> None:
        rgba = with_white_sliver()
        first = cleanup.analyze_image(rgba, Path("same.png"), "a" * 64)
        repeat = cleanup.analyze_image(rgba, Path("same.png"), "a" * 64)
        changed = cleanup.analyze_image(rgba, Path("same.png"), "b" * 64)
        self.assertEqual(first.regions[0].stable_id, repeat.regions[0].stable_id)
        self.assertNotEqual(first.regions[0].stable_id, changed.regions[0].stable_id)

    def test_exact_feedback_override_only_matches_complete_id(self) -> None:
        rgba = with_white_sliver()
        baseline = analyze(rgba)
        region_id = baseline.regions[0].stable_id
        exact = analyze(rgba, feedback={region_id: {"label": "intentional"}})
        mismatch = analyze(rgba, feedback={"not-the-region": {"label": "intentional"}})
        self.assertFalse(any(region.decision == "clean" for region in exact.regions))
        self.assertTrue(any(region.decision == "clean" for region in mismatch.regions))

    def test_missing_alpha_and_opaque_inputs_fail_validation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            rgb_path = root / "rgb.png"
            Image.new("RGB", (10, 10), "red").save(rgb_path)
            with self.assertRaises(cleanup.CleanupError):
                cleanup.load_png(rgb_path)
            opaque_path = root / "opaque.png"
            Image.new("RGBA", (10, 10), (255, 0, 0, 255)).save(opaque_path)
            with self.assertRaises(cleanup.CleanupError):
                cleanup.load_png(opaque_path)

    def test_insufficient_feedback_writes_ranking_only_model(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            feedback = root / "feedback.jsonl"
            feedback.write_text(json.dumps({
                "region_id": "one", "label": "fringe", "source_relative_path": "a.png",
                "feature_vector": {name: 0.0 for name in cleanup.FEATURE_VECTOR_NAMES},
            }) + "\n")
            model = cleanup.train_model(feedback, root / "model.json")
            self.assertEqual(model["acceptance_status"], "ranking_only")
            self.assertIn("requires at least", model["metrics"]["reason"])

    def test_model_with_no_automatic_held_out_fringe_is_ranking_only(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            feedback = root / "feedback.jsonl"
            records = []
            for index in range(24):
                records.append({
                    "region_id": f"r{index}",
                    "label": "fringe" if index % 2 == 0 else "intentional",
                    "source_relative_path": f"image-{index % 3}.png",
                    "feature_vector": {name: 0.0 for name in cleanup.FEATURE_VECTOR_NAMES},
                })
            feedback.write_text("".join(json.dumps(record) + "\n" for record in records))
            model = cleanup.train_model(feedback, root / "model.json")
            self.assertEqual(model["acceptance_status"], "ranking_only")
            self.assertEqual(model["metrics"]["automatic_fringe"], 0)

    def test_static_report_has_no_remote_resources(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = {
                "run_id": "test", "images": [], "source_verification": {"all_unchanged": True}
            }
            cleanup.write_report(root, manifest)
            report = (root / "report.html").read_text()
            self.assertNotIn("https://", report)
            self.assertNotIn("http://", report)
            self.assertIn("<style>", report)
            self.assertIn("<script>", report)

    def test_review_server_appends_valid_jsonl(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            run = root / "run"
            run.mkdir()
            region = {
                "region_id": "stable", "bbox": [0, 0, 2, 2], "mask_hash": "mask",
                "features": {"area": 2}, "feature_vector": {name: 0.0 for name in cleanup.FEATURE_VECTOR_NAMES},
                "deterministic_prior": 0.5, "learned_score": None, "decision": "flag",
                "reason_codes": ["ambiguous"], "diagnostic_path": None,
            }
            manifest = {
                "run_id": "run", "tool_version": cleanup.TOOL_VERSION,
                "feature_schema_version": cleanup.FEATURE_SCHEMA_VERSION,
                "images": [{"relative_path": "a.png", "source_sha256": "hash", "regions": [region], "diagnostics": {}}],
            }
            (run / "manifest.json").write_text(json.dumps(manifest))
            (run / "report.html").write_text("<!doctype html><title>test</title>")
            feedback = root / "feedback.jsonl"
            server = cleanup.create_review_server(run, feedback)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                body = json.dumps({"region_id": "stable", "label": "intentional", "note": "keep"}).encode()
                request = urllib.request.Request(
                    f"http://127.0.0.1:{server.server_address[1]}/label",
                    data=body, headers={"Content-Type": "application/json"}, method="POST",
                )
                with urllib.request.urlopen(request, timeout=5) as response:
                    self.assertEqual(response.status, 200)
                record = json.loads(feedback.read_text())
                self.assertEqual(record["label"], "intentional")
                self.assertEqual(record["region_id"], "stable")
                self.assertEqual(record["source_relative_path"], "a.png")
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=5)


if __name__ == "__main__":
    unittest.main()
