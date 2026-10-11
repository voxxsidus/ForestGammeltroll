using BepInEx;
using BepInEx.Configuration;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace ForestGammeltroll
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ForestGammeltroll : BaseUnityPlugin
    {
        public const string PluginGUID = "com.voxxsidus.forestgammeltroll";
        public const string PluginName = "ForestGammeltroll";
        public const string PluginVersion = "0.0.1";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private ConfigEntry<bool> _dumpPrefabs;

        private void Awake()
        {
            _dumpPrefabs = Config.Bind("Debug", "DumpPrefabs", true,
                "Write ForestGammeltroll_PrefabDump.txt to the BepInEx folder when vanilla prefabs load (main menu).");

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;

            Jotunn.Logger.LogInfo("ForestGammeltroll has landed");
        }

        private void OnVanillaPrefabsAvailable()
        {
            // Fires on every main menu load; dump only once per session
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;

            if (_dumpPrefabs.Value)
            {
                PrefabDumper.DumpTrolls();
            }
        }
    }
}
