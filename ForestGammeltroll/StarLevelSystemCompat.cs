using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Optional compatibility with Star Level System (MidnightsFX.StarLevelSystem): keeps starred
    ///     Forest Gammeltrolls in their normal colours instead of SLS's per-star tint.
    /// </summary>
    internal static class StarLevelSystemCompat
    {
        public const string PluginGUID = "MidnightsFX.StarLevelSystem";

        // Every SLS tint (creature setup, config reloads, API calls, ragdolls) goes through this method,
        // which copies the creature's first material and shifts its _Hue/_Saturation/_Value
        private const string ColorizeMethod = "StarLevelSystem.modules.Colorization:ApplyColorizationWithoutLevelEffects";

        public static void Apply(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.ContainsKey(PluginGUID))
            {
                return;
            }

            var method = AccessTools.Method(ColorizeMethod);
            if (method == null)
            {
                Jotunn.Logger.LogWarning($"Star Level System is installed but {ColorizeMethod} was not found; " +
                                         "starred Forest Gammeltrolls may be recoloured");
                return;
            }

            harmony.Patch(method, prefix: new HarmonyMethod(typeof(StarLevelSystemCompat), nameof(SkipForestTroll)));
            Jotunn.Logger.LogInfo("Star Level System detected: starred Forest Gammeltrolls keep their normal colours");
        }

        private static bool SkipForestTroll(GameObject cgo) =>
            !(cgo && ForestTrollCreature.IsForestTroll(cgo.GetComponent<Character>()));
    }
}
