using System;
using System.Collections.Generic;

namespace Crusaders30XX.Diagnostics.Snapshots
{
    public enum DisplaySnapshotBaselineMode
    {
        None,
        Verify,
        Accept
    }

    public sealed class DisplaySnapshotLaunchOptions
    {
        public const string VerifyFlag = "--verify";
        public const string AcceptFlag = "--accept";
        public const string RenderScaleFlag = "--render-scale";

        public string FixtureId { get; init; }
        public string[] Args { get; init; } = Array.Empty<string>();
        public DisplaySnapshotBaselineMode BaselineMode { get; init; }
        public float? RenderScaleOverride { get; init; }

        public static bool TryParse(string[] args, out DisplaySnapshotLaunchOptions options)
        {
            options = null;
            if (args == null || args.Length < 2 || !string.Equals(args[0], "snapshot", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fixtureArgs = new List<string>();
            bool verify = false;
            bool accept = false;
            float? renderScale = null;

            for (int i = 2; i < args.Length; i++)
            {
                if (string.Equals(args[i], VerifyFlag, StringComparison.OrdinalIgnoreCase))
                {
                    verify = true;
                }
                else if (string.Equals(args[i], AcceptFlag, StringComparison.OrdinalIgnoreCase))
                {
                    accept = true;
                }
                else if (string.Equals(args[i], RenderScaleFlag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length ||
                        !float.TryParse(
                            args[++i],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float parsedScale) ||
                        parsedScale <= 0f ||
                        parsedScale > 2f)
                    {
                        throw new DisplaySnapshotSetupException(
                            $"{RenderScaleFlag} requires a value greater than 0 and no greater than 2");
                    }

                    renderScale = parsedScale;
                }
                else
                {
                    fixtureArgs.Add(args[i]);
                }
            }

            if (verify && accept)
            {
                throw new DisplaySnapshotSetupException(
                    $"{VerifyFlag} and {AcceptFlag} are mutually exclusive");
            }

            if (renderScale.HasValue && renderScale.Value != 1f && (verify || accept))
            {
                throw new DisplaySnapshotSetupException(
                    $"{RenderScaleFlag} can only be combined with {VerifyFlag} or {AcceptFlag} at scale 1");
            }

            options = new DisplaySnapshotLaunchOptions
            {
                FixtureId = args[1],
                Args = fixtureArgs.ToArray(),
                RenderScaleOverride = renderScale,
                BaselineMode = verify
                    ? DisplaySnapshotBaselineMode.Verify
                    : accept
                        ? DisplaySnapshotBaselineMode.Accept
                        : DisplaySnapshotBaselineMode.None
            };
            return true;
        }
    }
}
