using System;
using System.Collections.Generic;

namespace ChurchSuffering.Diagnostics
{
    public static class GpuProfilingRuntimeOptions
    {
        public const string LaunchFlag = "profile-gpu";

        public static bool Enabled { get; private set; }

        public static void ConfigureFromArgs(string[] args)
        {
#if DEBUG
            Enabled = args != null && Array.Exists(
                args,
                arg => string.Equals(arg, LaunchFlag, StringComparison.OrdinalIgnoreCase));
#else
            Enabled = false;
#endif
        }

        public static string[] StripLaunchFlag(string[] args)
        {
            if (args == null || args.Length == 0) return Array.Empty<string>();
            var filtered = new List<string>(args.Length);
            foreach (string arg in args)
            {
                if (!string.Equals(arg, LaunchFlag, StringComparison.OrdinalIgnoreCase)) filtered.Add(arg);
            }
            return filtered.ToArray();
        }
    }
}
