using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJamOcean.Interaction
{
    public static class InteractionDiscoveryStore
    {
        private const string PlayerPrefsKey = "GameJamOcean.InteractionDiscoveries";
        private static readonly HashSet<string> SeenKeys = new(StringComparer.Ordinal);
        private static bool loaded;

        public static bool HasSeen(string key)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(key) && SeenKeys.Contains(key.Trim());
        }

        public static void MarkSeen(string key)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(key) || !SeenKeys.Add(key.Trim()))
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, string.Join("\n", SeenKeys));
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            SeenKeys.Clear();
            loaded = true;
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            string savedKeys = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            foreach (string key in savedKeys.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                SeenKeys.Add(key.Trim());
            }
        }
    }
}
