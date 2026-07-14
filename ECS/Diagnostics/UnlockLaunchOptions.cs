using System;
using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.Diagnostics
{
    public static class UnlockLaunchOptions
    {
        public const string UnlockLaunchFlag = "unlock";
        public const string UnlockRunSetupLaunchFlag = "unlock-run-setup";

        public static bool UnlockAllCollectionItems { get; private set; }
        public static bool UnlockAllRunSetupOptions { get; private set; }

        public static void ConfigureFromArgs(string[] args)
        {
            UnlockAllCollectionItems = args != null &&
                args.Any(arg => string.Equals(arg, UnlockLaunchFlag, StringComparison.OrdinalIgnoreCase));
            UnlockAllRunSetupOptions = args != null &&
                args.Any(arg => string.Equals(arg, UnlockRunSetupLaunchFlag, StringComparison.OrdinalIgnoreCase));
        }

        public static string[] StripLaunchFlags(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return Array.Empty<string>();
            }

            var filtered = new List<string>(args.Length);
            foreach (var arg in args)
            {
                if (!string.Equals(arg, UnlockLaunchFlag, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(arg, UnlockRunSetupLaunchFlag, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(arg);
                }
            }

            return filtered.ToArray();
        }
    }
}
