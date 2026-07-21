using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.Diagnostics
{
    public static class ShaderRuntimeOptions
    {
        public const string NoShadersLaunchFlag = "no-shaders";

        public static bool ShadersEnabled { get; private set; } = true;

        public static void ConfigureFromArgs(string[] args)
        {
            ShadersEnabled = args == null ||
                !args.Any(a => string.Equals(a, NoShadersLaunchFlag, StringComparison.OrdinalIgnoreCase));
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
                if (!string.Equals(arg, NoShadersLaunchFlag, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(arg);
                }
            }

            return filtered.ToArray();
        }
    }
}
