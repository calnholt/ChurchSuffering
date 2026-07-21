using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.Diagnostics
{
    public static class TutorialLaunchOptions
    {
        public const string SkipTutorialsLaunchFlag = "skip-tutorials";

        public static bool SkipTutorials { get; private set; }

        public static void ConfigureFromArgs(string[] args)
        {
            SkipTutorials = args != null &&
                args.Any(a => string.Equals(a, SkipTutorialsLaunchFlag, StringComparison.OrdinalIgnoreCase));
        }

        public static void ForceSkip()
        {
            SkipTutorials = true;
        }

        public static string[] StripLaunchFlag(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return Array.Empty<string>();
            }

            var filtered = new List<string>(args.Length);
            foreach (var arg in args)
            {
                if (!string.Equals(arg, SkipTutorialsLaunchFlag, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(arg);
                }
            }

            return filtered.ToArray();
        }
    }
}
