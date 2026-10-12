using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace ForestGammeltroll
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(StarLevelSystemCompat.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class ForestGammeltroll : BaseUnityPlugin
    {
        public const string PluginGUID = "com.voxxsidus.forestgammeltroll";
        public const string PluginName = "ForestGammeltroll";
        public const string PluginVersion = "1.0.0";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public static ConfigEntry<float> SpawnChance;
        public static ConfigEntry<bool> AllowStars;
        public static ConfigEntry<float> Health;
        public static ConfigEntry<float> DamageMultiplier;
        public static ConfigEntry<float> ModelScale;
        public static ConfigEntry<float> ViewRange;
        public static ConfigEntry<float> AlertRange;
        public static ConfigEntry<float> MaxChaseDistance;
        public static ConfigEntry<float> CopperOreWeight;
        public static ConfigEntry<Color> MossTint;
        public static ConfigEntry<float> SnowToMossThreshold;

        private ConfigEntry<bool> _dumpPrefabs;

        private void Awake()
        {
            BindConfig();

            Localization.AddTranslation("English", ForestTrollCreature.NameToken, "Forest Gammeltroll");
            Localization.AddTranslation("English", PetrifiedRemains.NameToken, "Petrified Forest Gammeltroll");

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            CreatureManager.OnVanillaCreaturesAvailable += OnVanillaCreaturesAvailable;

            var harmony = new Harmony(PluginGUID);
            harmony.PatchAll();
            StarLevelSystemCompat.Apply(harmony);
            CommandManager.Instance.AddConsoleCommand(new TestSpawnCommand());

            Jotunn.Logger.LogInfo("ForestGammeltroll has landed");
        }

        private void BindConfig()
        {
            // Gameplay values are admin-only so Jötunn syncs the server's values to every client;
            // whichever machine owns the troll computes its damage and AI, so they must agree
            ConfigDescription Synced(string description, AcceptableValueBase range) =>
                new ConfigDescription(description, range, new ConfigurationManagerAttributes { IsAdminOnly = true });

            SpawnChance = Config.Bind("Spawning", "SpawnChance", 25f,
                Synced("Percent chance that an open-world Troll spawning in the Black Forest becomes a Forest Gammeltroll instead. " +
                       "Troll caves and raids are never affected.",
                    new AcceptableValueRange<float>(0f, 100f)));
            AllowStars = Config.Bind("Spawning", "AllowStars", true,
                Synced("Whether swapped-in Forest Gammeltrolls can spawn with stars, using the Troll's level-up chances (up to 2 stars).", null));

            Health = Config.Bind("Stats", "Health", 1500f,
                Synced("Base health (1 star). Regular Troll: 600, vanilla Gammeltroll: 3000. Applies to newly spawned trolls.",
                    new AcceptableValueRange<float>(100f, 10000f)));
            DamageMultiplier = Config.Bind("Stats", "DamageMultiplier", 0.75f,
                Synced("Multiplier on the vanilla Gammeltroll's damage, applied to every hit (punches, ground slam shockwave, thrown rocks). " +
                       "Vanilla Gammeltroll hits ~3x as hard as a regular Troll, so 0.75 is ~2.2-2.75x a Troll.",
                    new AcceptableValueRange<float>(0.05f, 2f)));
            ModelScale = Config.Bind("Stats", "ModelScale", 1f,
                Synced("Size relative to the vanilla Gammeltroll (1 = vanilla, ~4x a regular Troll; 0.5 = ~2x a Troll).",
                    new AcceptableValueRange<float>(0.1f, 1.5f)));
            ViewRange = Config.Bind("AI", "ViewRange", 50f,
                Synced("How far it can see targets. Regular Troll: 30, vanilla Gammeltroll: 300.",
                    new AcceptableValueRange<float>(5f, 300f)));
            AlertRange = Config.Bind("AI", "AlertRange", 30f,
                Synced("Radius in which it notices nearby targets. Regular Troll: 20, vanilla Gammeltroll: 90.",
                    new AcceptableValueRange<float>(5f, 300f)));
            MaxChaseDistance = Config.Bind("AI", "MaxChaseDistance", 200f,
                Synced("How far from its spawn point it will chase before giving up. Regular Troll: 200, vanilla Gammeltroll: 500.",
                    new AcceptableValueRange<float>(20f, 1000f)));

            CopperOreWeight = Config.Bind("Petrify", "CopperOreWeight", 0.5f,
                Synced("Relative chance of Copper Ore vs Stone (weight 1) for each drop roll when mining the petrified remains. " +
                       "Each of the rock's chunks rolls 2-3 times. 0.5 matches the vanilla Gammeltroll's Bloodgold ratio (~1/3 of drops). " +
                       "Applies to remains created after the change.",
                    new AcceptableValueRange<float>(0f, 10f)));

            // Visual-only settings are per-client, so not synced
            MossTint = Config.Bind("Visual", "MossTint", new Color(0.7f, 1f, 0.55f),
                "Tint applied to the moss that replaces the snow on its skin, hair, statue and rock. Updates live.");
            SnowToMossThreshold = Config.Bind("Visual", "SnowToMossThreshold", 0.48f,
                new ConfigDescription("Brightness above which the skin texture counts as snow and becomes moss. Lower = more moss. Updates live.",
                    new AcceptableValueRange<float>(0.2f, 0.9f)));
            MossTint.SettingChanged += (_, _) => ForestVisuals.RegenerateSkin();
            SnowToMossThreshold.SettingChanged += (_, _) => ForestVisuals.RegenerateSkin();

            _dumpPrefabs = Config.Bind("Debug", "DumpPrefabs", false,
                "Write ForestGammeltroll_PrefabDump.txt to the BepInEx folder when vanilla prefabs load (main menu).");

            foreach (var entry in new[] { Health, DamageMultiplier, ModelScale, ViewRange, AlertRange, MaxChaseDistance, CopperOreWeight })
            {
                entry.SettingChanged += (_, _) => ForestTrollCreature.ApplySettings();
            }
            SynchronizationManager.OnConfigurationSynchronized += (_, _) => ForestTrollCreature.ApplySettings();
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

        private void OnVanillaCreaturesAvailable()
        {
            // The clone persists across menu reloads; register it only once
            CreatureManager.OnVanillaCreaturesAvailable -= OnVanillaCreaturesAvailable;

            ForestTrollCreature.Register();
        }
    }
}
