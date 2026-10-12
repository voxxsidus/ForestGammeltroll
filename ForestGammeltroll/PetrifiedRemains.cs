using Jotunn.Managers;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     The stone the Forest Gammeltroll turns into on death, cloned from the vanilla Gammeltroll's:
    ///     a statue (Destructible) that breaks into a mineable rock (MineRock5) dropping Stone and Copper Ore.
    ///     Both are mineable from Antler Pickaxe tier instead of the vanilla tier 6.
    /// </summary>
    internal static class PetrifiedRemains
    {
        public const string StatuePrefabName = "ForestGammeltroll_Dead";
        public const string RockPrefabName = "ForestGammeltroll_Frac";
        public const string NameToken = "$piece_forestgammeltroll_petrified";

        // Vanilla prefab names, from the Phase 1 prefab dump
        public const string VanillaStatuePrefabName = "TrollFrost_Dead";
        private const string VanillaRockPrefabName = "TrollFrost_Frac";
        private const string VanillaRareOreName = "GoldOre";
        private const string CopperOreName = "CopperOre";
        private const string MinimumPickaxeName = "PickaxeAntler";

        private static GameObject s_statue;
        private static GameObject s_rock;
        private static int s_copperDropIndex = -1;

        /// <summary>
        ///     Clones and registers the statue and rock. Returns the statue prefab, or null on failure.
        /// </summary>
        public static GameObject Register()
        {
            int toolTier = GetMinimumToolTier();
            if (toolTier < 0)
            {
                return null;
            }

            var copperOre = PrefabManager.Cache.GetPrefab<GameObject>(CopperOreName);
            if (!copperOre)
            {
                Jotunn.Logger.LogError($"Could not find {CopperOreName}; it will not turn to stone");
                return null;
            }

            var rock = PrefabManager.Instance.CreateClonedPrefab(RockPrefabName, VanillaRockPrefabName);
            var statue = PrefabManager.Instance.CreateClonedPrefab(StatuePrefabName, VanillaStatuePrefabName);
            if (!rock || !statue)
            {
                Jotunn.Logger.LogError($"Could not clone {VanillaRockPrefabName}/{VanillaStatuePrefabName}; it will not turn to stone");
                return null;
            }

            var mineRock = rock.GetComponent<MineRock5>();
            mineRock.m_name = NameToken;
            mineRock.m_minToolTier = toolTier;
            var drops = mineRock.m_dropItems.m_drops;
            for (int i = 0; i < drops.Count; i++)
            {
                if (drops[i].m_item && drops[i].m_item.name == VanillaRareOreName)
                {
                    var drop = drops[i];
                    drop.m_item = copperOre;
                    drops[i] = drop;
                    s_copperDropIndex = i;
                }
            }
            if (s_copperDropIndex < 0)
            {
                Jotunn.Logger.LogWarning($"{VanillaRareOreName} not found in {VanillaRockPrefabName} drops; rock will drop no copper");
            }

            var destructible = statue.GetComponent<Destructible>();
            destructible.m_minToolTier = toolTier;
            destructible.m_spawnWhenDestroyed = rock;
            var hoverText = statue.GetComponent<HoverText>();
            if (hoverText)
            {
                hoverText.m_text = NameToken;
            }

            PrefabManager.Instance.AddPrefab(rock);
            PrefabManager.Instance.AddPrefab(statue);

            s_rock = rock;
            s_statue = statue;
            ApplySettings();

            Jotunn.Logger.LogInfo($"Registered {StatuePrefabName} and {RockPrefabName} (min tool tier {toolTier}, from {MinimumPickaxeName})");
            return statue;
        }

        public static void ApplySettings()
        {
            if (!s_statue)
            {
                return;
            }

            // The statue's mesh is baked at the vanilla Gammeltroll's size; scale it with the troll.
            // The rock inherits the statue's scale when it spawns (Destructible.m_spawnWhenDestroyed).
            s_statue.transform.localScale = Vector3.one * ForestGammeltroll.ModelScale.Value;

            if (s_copperDropIndex >= 0)
            {
                var drops = s_rock.GetComponent<MineRock5>().m_dropItems.m_drops;
                var drop = drops[s_copperDropIndex];
                drop.m_weight = ForestGammeltroll.CopperOreWeight.Value;
                drops[s_copperDropIndex] = drop;
            }
        }

        private static int GetMinimumToolTier()
        {
            var pickaxe = PrefabManager.Cache.GetPrefab<GameObject>(MinimumPickaxeName);
            var itemDrop = pickaxe ? pickaxe.GetComponent<ItemDrop>() : null;
            if (!itemDrop)
            {
                Jotunn.Logger.LogError($"Could not find {MinimumPickaxeName}; it will not turn to stone");
                return -1;
            }
            return itemDrop.m_itemData.m_shared.m_toolTier;
        }
    }
}
