using System;

namespace ChurchSuffering.Diagnostics
{
    public sealed class BattleRenderProfileLaunchOptions
    {
        public const string Command = "battle-render-profile";
        public const float RenderScale = 1f;
        public const int WarmupFrames = 180;
        public const int MeasuredFrames = 300;
        public const int MaxQuerySettleFrames = 120;

        public static bool TryParse(string[] args, out BattleRenderProfileLaunchOptions options)
        {
            options = null;
            if (args == null || args.Length == 0 ||
                !string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

#if !DEBUG
            throw new BattleRenderProfileSetupException("battle-render-profile is available only in DEBUG builds");
#else
            if (args.Length != 1)
            {
                throw new BattleRenderProfileSetupException(
                    $"Usage: dotnet run -- {Command} profile-gpu");
            }

            if (!GpuProfilingRuntimeOptions.Enabled)
            {
                throw new BattleRenderProfileSetupException(
                    "battle-render-profile requires profile-gpu so GPU timing and workload counters are captured");
            }

            options = new BattleRenderProfileLaunchOptions();
            return true;
#endif
        }
    }

    public sealed class BattleRenderProfileSetupException : Exception
    {
        public BattleRenderProfileSetupException(string message) : base(message) { }
    }
}
