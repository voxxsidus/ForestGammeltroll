using HarmonyLib;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Replaces a share of open-world Black Forest Troll spawns with the Forest Gammeltroll.
    /// </summary>
    internal static class SpawnPatches
    {
        private const string TrollPrefabName = "Troll";

        /// <summary>Whether the most recent Troll spawn was swapped (read by the test command).</summary>
        public static bool LastSpawnSwapped { get; private set; }

        // SpawnSystem drives open-world biome spawns only. Locations such as troll caves (TrollCave02) place
        // their trolls with CreatureSpawner instead, so they never reach this patch.
        // SpawnSystem.UpdateSpawning only runs for the zone owner, so the roll happens on exactly one machine.
        [HarmonyPatch(typeof(SpawnSystem), "Spawn")]
        private static class SpawnSystem_Spawn
        {
            private static void Prefix(ref SpawnSystem.SpawnData critter, Vector3 spawnPoint, bool eventSpawner)
            {
                LastSpawnSwapped = false;

                // Raid events (eventSpawner) are excluded; they are not regular spawns
                if (eventSpawner || !critter.m_prefab || critter.m_prefab.name != TrollPrefabName)
                {
                    return;
                }
                if (Heightmap.FindBiome(spawnPoint) != Heightmap.Biome.BlackForest)
                {
                    return;
                }

                var variant = ForestTrollCreature.Prefab;
                if (!variant || Random.Range(0f, 100f) >= ForestGammeltroll.SpawnChance.Value)
                {
                    return;
                }

                // Swap in a copy of the Troll's spawn entry so its level-up and hunt rules still apply
                var swapped = critter.Clone();
                swapped.m_prefab = variant;
                if (!ForestGammeltroll.AllowStars.Value)
                {
                    swapped.m_maxLevel = 1;
                }
                critter = swapped;
                LastSpawnSwapped = true;

                Jotunn.Logger.LogInfo($"Spawn swap: Troll -> {variant.name} at {spawnPoint:F0} " +
                                      $"(zone {ZoneSystem.GetZone(spawnPoint)}, chance {ForestGammeltroll.SpawnChance.Value}%)");
            }
        }
    }
}
