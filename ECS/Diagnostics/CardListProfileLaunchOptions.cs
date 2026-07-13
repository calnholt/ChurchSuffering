using System;
using System.Globalization;

namespace Crusaders30XX.Diagnostics
{
    public sealed class CardListProfileLaunchOptions
    {
        public const string Command = "card-list-profile";
        public const string RenderScaleFlag = "--render-scale";
        public const int WarmupFrames = 180;
        public const int MeasuredFrames = 300;
        public const int MaxQuerySettleFrames = 120;

        public float RenderScale { get; init; } = 2f;

        public static bool TryParse(string[] args, out CardListProfileLaunchOptions options)
        {
            options = null;
            if (args == null || args.Length == 0 || !string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase)) return false;
#if !DEBUG
            throw new CardListProfileSetupException("card-list-profile is available only in DEBUG builds");
#else
            float scale = 2f;
            for (int i = 1; i < args.Length; i++)
            {
                if (!string.Equals(args[i], RenderScaleFlag, StringComparison.OrdinalIgnoreCase) ||
                    i + 1 >= args.Length ||
                    !float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out scale))
                {
                    throw new CardListProfileSetupException($"Usage: dotnet run -- {Command} profile-gpu {RenderScaleFlag} 2");
                }
            }
            if (Math.Abs(scale - 2f) > 0.001f)
            {
                throw new CardListProfileSetupException("card-list-profile requires --render-scale 2 for the 3840x2160 acceptance workload");
            }
            options = new CardListProfileLaunchOptions { RenderScale = scale };
            return true;
#endif
        }
    }

    public sealed class CardListProfileSetupException : Exception
    {
        public CardListProfileSetupException(string message) : base(message) { }
    }
}
