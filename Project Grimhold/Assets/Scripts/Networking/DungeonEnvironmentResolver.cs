namespace Spawning
{
    /// <summary>
    /// Resolves the canonical dungeon environment key from active scene or session context.
    /// Used by enemy spawners to determine environmental eligibility for creatures like the Specter.
    /// </summary>
    public static class DungeonEnvironmentResolver
    {
        public const string Catacombs = "catacombs";
        public const string Town = "town";
        public const string Sandbox = "sandbox";

        public static string ResolveEnvironment(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return Catacombs;
            }

            string lower = sceneName.Trim().ToLowerInvariant();
            if (lower.Contains("catacomb") || lower.Contains("crypt") || lower.Contains("gameplay") || lower.Contains("dungeon"))
            {
                return Catacombs;
            }
            if (lower.Contains("town") || lower.Contains("lobby"))
            {
                return Town;
            }
            if (lower.Contains("sandbox"))
            {
                return Sandbox;
            }

            return lower;
        }
    }
}
