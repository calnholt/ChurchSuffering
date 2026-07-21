using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.Diagnostics
{
    public static class NewGameLaunchOptions
    {
        public const string NewGameLaunchFlag = "new";

        public static bool DeleteSaveBeforeLaunch { get; private set; }

        public static void ConfigureFromArgs(string[] args)
        {
            DeleteSaveBeforeLaunch = args != null &&
                args.Any(a => string.Equals(a, NewGameLaunchFlag, StringComparison.OrdinalIgnoreCase));
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
                if (!string.Equals(arg, NewGameLaunchFlag, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(arg);
                }
            }

            return filtered.ToArray();
        }
    }
}
