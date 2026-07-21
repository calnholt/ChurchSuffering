using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots
{
    public sealed class DisplaySnapshotPaths
    {
        public string CapturePath { get; init; }
        public string BaselinePath { get; init; }
        public string FailureActualPath { get; init; }
        public string FailureDiffPath { get; init; }
    }

    public sealed class DisplaySnapshotComparisonResult
    {
        public bool Passed { get; init; }
        public string FailureMessage { get; init; }
        public int DifferingPixelCount { get; init; }
        public int TotalPixelCount { get; init; }
        public Color[] DiffPixels { get; init; } = Array.Empty<Color>();

        public double DifferingPixelRatio =>
            TotalPixelCount == 0 ? 0d : (double)DifferingPixelCount / TotalPixelCount;
    }

    public static class DisplaySnapshotBaselineComparer
    {
        public const int CaptureWidth = 1920;
        public const int CaptureHeight = 1080;
        public const int PerChannelTolerance = 8;
        public const double MaximumDifferingPixelRatio = 0.0025d;

        private static readonly Color DifferenceHighlight = new(255, 0, 255, 255);

        public static DisplaySnapshotPaths BuildPaths(
            string repositoryRoot,
            string fixtureId,
            string outputFileName)
        {
            string slug = Path.GetFileNameWithoutExtension(outputFileName);
            string captureDirectory = Path.Combine(repositoryRoot, "debug", "snapshots", fixtureId);

            return new DisplaySnapshotPaths
            {
                CapturePath = Path.Combine(captureDirectory, $"{slug}.png"),
                BaselinePath = Path.Combine(
                    repositoryRoot,
                    "tests",
                    "VisualBaselines",
                    fixtureId,
                    $"{slug}.png"),
                FailureActualPath = Path.Combine(captureDirectory, $"{slug}-actual.png"),
                FailureDiffPath = Path.Combine(captureDirectory, $"{slug}-diff.png")
            };
        }

        public static string FindRepositoryRoot()
        {
            string root = FindRepositoryRootFrom(Directory.GetCurrentDirectory())
                ?? FindRepositoryRootFrom(AppContext.BaseDirectory);

            if (root == null)
            {
                throw new DirectoryNotFoundException(
                    "Could not locate the repository root containing ChurchSuffering.csproj");
            }

            return root;
        }

        public static DisplaySnapshotComparisonResult Compare(
            GraphicsDevice graphicsDevice,
            Texture2D actual,
            string baselinePath)
        {
            var actualPixels = ReadPixels(actual);
            if (!BaselineExists(baselinePath))
            {
                return ComparePixelData(
                    actualPixels,
                    actual.Width,
                    actual.Height,
                    null,
                    0,
                    0,
                    baselinePath);
            }

            using var stream = File.OpenRead(baselinePath);
            using var baseline = Texture2D.FromStream(graphicsDevice, stream);
            var baselinePixels = ReadPixels(baseline);

            return ComparePixelData(
                actualPixels,
                actual.Width,
                actual.Height,
                baselinePixels,
                baseline.Width,
                baseline.Height,
                baselinePath);
        }

        internal static DisplaySnapshotComparisonResult ComparePixelData(
            Color[] actual,
            int actualWidth,
            int actualHeight,
            Color[] baseline,
            int baselineWidth,
            int baselineHeight,
            string baselinePath = null)
        {
            int actualPixelCount = actual?.Length ?? 0;

            if (actualWidth != CaptureWidth || actualHeight != CaptureHeight)
            {
                return Failure(
                    $"Actual snapshot dimensions were {actualWidth}x{actualHeight}; expected {CaptureWidth}x{CaptureHeight}",
                    actual,
                    actualPixelCount);
            }

            if (baseline == null)
            {
                return Failure(
                    $"Baseline is missing: {Path.GetFullPath(baselinePath ?? string.Empty)}",
                    actual,
                    actualPixelCount);
            }

            if (baselineWidth != CaptureWidth || baselineHeight != CaptureHeight)
            {
                return Failure(
                    $"Baseline dimensions were {baselineWidth}x{baselineHeight}; expected {CaptureWidth}x{CaptureHeight}",
                    actual,
                    actualPixelCount);
            }

            int expectedPixelCount = CaptureWidth * CaptureHeight;
            if (actualPixelCount != expectedPixelCount || baseline.Length != expectedPixelCount)
            {
                return Failure(
                    $"Snapshot pixel data must contain exactly {expectedPixelCount} pixels",
                    actual,
                    actualPixelCount);
            }

            int differingPixels = 0;
            var diffPixels = new Color[expectedPixelCount];

            for (int i = 0; i < expectedPixelCount; i++)
            {
                bool differs = PixelDiffers(actual[i], baseline[i]);
                if (differs)
                {
                    differingPixels++;
                    diffPixels[i] = DifferenceHighlight;
                }
                else
                {
                    diffPixels[i] = Dim(actual[i]);
                }
            }

            bool passed = IsDifferingPixelRatioAcceptable(differingPixels, expectedPixelCount);
            return new DisplaySnapshotComparisonResult
            {
                Passed = passed,
                FailureMessage = passed
                    ? null
                    : $"Snapshot differed at {differingPixels} of {expectedPixelCount} pixels " +
                      $"({(double)differingPixels / expectedPixelCount:P4}); maximum is " +
                      $"{MaximumDifferingPixelRatio:P2}",
                DifferingPixelCount = differingPixels,
                TotalPixelCount = expectedPixelCount,
                DiffPixels = diffPixels
            };
        }

        internal static bool PixelDiffers(Color actual, Color baseline) =>
            Math.Abs(actual.R - baseline.R) > PerChannelTolerance
            || Math.Abs(actual.G - baseline.G) > PerChannelTolerance
            || Math.Abs(actual.B - baseline.B) > PerChannelTolerance
            || Math.Abs(actual.A - baseline.A) > PerChannelTolerance;

        internal static bool IsDifferingPixelRatioAcceptable(int differingPixels, int totalPixels) =>
            totalPixels > 0
            && (double)differingPixels / totalPixels <= MaximumDifferingPixelRatio;

        internal static bool BaselineExists(string path) => File.Exists(path);

        public static void SavePng(Texture2D texture, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            texture.SaveAsPng(stream, texture.Width, texture.Height);
        }

        public static void SaveDiffPng(
            GraphicsDevice graphicsDevice,
            DisplaySnapshotComparisonResult result,
            string path)
        {
            using var diff = new Texture2D(graphicsDevice, CaptureWidth, CaptureHeight);
            diff.SetData(result.DiffPixels);
            SavePng(diff, path);
        }

        private static Color[] ReadPixels(Texture2D texture)
        {
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            return pixels;
        }

        private static DisplaySnapshotComparisonResult Failure(
            string message,
            Color[] actual,
            int totalPixelCount)
        {
            var diffPixels = new Color[CaptureWidth * CaptureHeight];
            int copiedPixelCount = Math.Min(actual?.Length ?? 0, diffPixels.Length);
            for (int i = 0; i < copiedPixelCount; i++)
            {
                diffPixels[i] = DifferenceHighlight;
            }

            return new DisplaySnapshotComparisonResult
            {
                Passed = false,
                FailureMessage = message,
                DifferingPixelCount = totalPixelCount,
                TotalPixelCount = totalPixelCount,
                DiffPixels = diffPixels
            };
        }

        private static Color Dim(Color color) =>
            new(color.R / 4, color.G / 4, color.B / 4, 255);

        private static string FindRepositoryRootFrom(string startPath)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startPath));
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ChurchSuffering.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
