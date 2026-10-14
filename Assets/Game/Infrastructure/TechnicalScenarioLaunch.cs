using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Infrastructure
{
    /// <summary>
    /// Bridges command-line qualification adapters through the normal Main Menu -> New Game scene boundary.
    /// It chooses only the requested world seed; the LocalGameplay composition remains the sole owner of
    /// benchmark, soak and release-smoke setup.
    /// </summary>
    internal static class TechnicalScenarioLaunch
    {
        private const string U29Flag = "-u29Benchmark";
        private const string U30Flag = "-u30Soak";
        private const string U31Flag = "-u31Smoke";

        public static bool TryResolveWorldSeed(string[] arguments, out long worldSeed)
        {
            worldSeed = 0L;
            if (arguments == null || arguments.Length == 0) return false;

            var requested = new List<(string Flag, string SeedArgument)>();
            if (HasArgument(arguments, U29Flag)) requested.Add((U29Flag, "-u29Seed"));
            if (HasArgument(arguments, U30Flag)) requested.Add((U30Flag, "-u30Seed"));
            if (HasArgument(arguments, U31Flag)) requested.Add((U31Flag, "-u31SmokeSeed"));
            if (requested.Count == 0) return false;
            if (requested.Count != 1)
                throw new InvalidOperationException("Only one technical qualification scenario may run at a time.");

            var seedText = ReadRequiredArgument(arguments, requested[0].SeedArgument);
            if (!long.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out worldSeed) ||
                worldSeed <= 0L)
                throw new InvalidOperationException(requested[0].SeedArgument + " must be a positive whole number.");
            return true;
        }

        private static bool HasArgument(IEnumerable<string> arguments, string key) =>
            arguments.Any(value => string.Equals(value, key, StringComparison.OrdinalIgnoreCase));

        private static string ReadRequiredArgument(IReadOnlyList<string> arguments, string key)
        {
            for (var index = 0; index < arguments.Count - 1; index++)
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            throw new InvalidOperationException("Missing technical scenario argument " + key + ".");
        }
    }
}
