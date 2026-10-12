using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     The Black Forest Gammeltroll: a clone of the vanilla Gammeltroll (TrollFrost)
    ///     without the petrify-on-death statue and its Bloodgold drops.
    /// </summary>
    internal static class ForestTrollCreature
    {
        public const string PrefabName = "ForestGammeltroll";
        public const string NameToken = "$enemy_forestgammeltroll";

        // Vanilla prefab names, from the Phase 1 prefab dump
        private const string BasePrefabName = "TrollFrost";
        private const string DropsSourcePrefabName = "Troll";
        private const string PetrifiedStatuePrefabName = "TrollFrost_Dead";

        private static GameObject s_prefab;
        private static Vector3 s_baseScale;

        public static void Register()
        {
            var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, BasePrefabName);
            if (!prefab)
            {
                Jotunn.Logger.LogError($"Could not clone {BasePrefabName}; {PrefabName} will not exist");
                return;
            }

            RemovePetrifyOnDeath(prefab.GetComponent<Character>());
            CopyDrops(prefab);

            var config = new CreatureConfig
            {
                Name = NameToken,
                Faction = Character.Faction.ForestMonsters,
            };
            CreatureManager.Instance.AddCreature(new CustomCreature(prefab, false, config));

            s_prefab = prefab;
            s_baseScale = prefab.transform.localScale;
            ApplySettings();

            Jotunn.Logger.LogInfo($"Registered {PrefabName} (cloned from {BasePrefabName})");
        }

        public static bool IsForestTroll(Character character) => character && character.m_name == NameToken;

        /// <summary>
        ///     Pushes config values onto the prefab (affects new spawns) and onto live instances where that is safe.
        /// </summary>
        public static void ApplySettings()
        {
            if (!s_prefab)
            {
                return;
            }

            // Live instances also carry world-modifier scaling from Character.Awake, so rescale them
            // by the change in ModelScale rather than setting an absolute size
            float previousModelScale = s_prefab.transform.localScale.x / s_baseScale.x;
            float scaleRatio = ForestGammeltroll.ModelScale.Value / previousModelScale;

            s_prefab.GetComponent<Character>().m_health = ForestGammeltroll.Health.Value;
            s_prefab.transform.localScale = s_baseScale * ForestGammeltroll.ModelScale.Value;
            ApplyAI(s_prefab.GetComponent<MonsterAI>());

            foreach (var character in Character.GetAllCharacters().Where(IsForestTroll))
            {
                if (!Mathf.Approximately(scaleRatio, 1f))
                {
                    character.transform.localScale *= scaleRatio;
                }
                ApplyAI(character.GetComponent<MonsterAI>());
            }
        }

        private static void ApplyAI(MonsterAI ai)
        {
            if (!ai)
            {
                return;
            }
            ai.m_viewRange = ForestGammeltroll.ViewRange.Value;
            ai.m_alertRange = ForestGammeltroll.AlertRange.Value;
            ai.m_maxChaseDistance = ForestGammeltroll.MaxChaseDistance.Value;
        }

        private static void RemovePetrifyOnDeath(Character character)
        {
            // The vanilla death effects spawn a stone statue that breaks into a Bloodgold rock.
            // Without it the body vanishes in the remaining smoke/pebble effects and CharacterDrop drops loot.
            var effects = character.m_deathEffects.m_effectPrefabs;
            character.m_deathEffects.m_effectPrefabs = effects
                .Where(e => !e.m_prefab || e.m_prefab.name != PetrifiedStatuePrefabName)
                .ToArray();

            if (character.m_deathEffects.m_effectPrefabs.Length == effects.Length)
            {
                Jotunn.Logger.LogWarning($"{PetrifiedStatuePrefabName} not found in {BasePrefabName} death effects; it may still petrify");
            }
        }

        private static void CopyDrops(GameObject prefab)
        {
            // Placeholder loot: the regular Troll's drop table
            var troll = PrefabManager.Cache.GetPrefab<GameObject>(DropsSourcePrefabName);
            var trollDrop = troll ? troll.GetComponent<CharacterDrop>() : null;
            if (!trollDrop)
            {
                Jotunn.Logger.LogWarning($"No CharacterDrop on {DropsSourcePrefabName}; {PrefabName} will drop nothing");
                return;
            }

            var drop = prefab.GetComponent<CharacterDrop>();
            if (!drop)
            {
                drop = prefab.AddComponent<CharacterDrop>();
            }
            drop.m_drops = trollDrop.m_drops.Select(d => new CharacterDrop.Drop
            {
                m_prefab = d.m_prefab,
                m_amountMin = d.m_amountMin,
                m_amountMax = d.m_amountMax,
                m_chance = d.m_chance,
                m_onePerPlayer = d.m_onePerPlayer,
                m_levelMultiplier = d.m_levelMultiplier,
                m_dontScale = d.m_dontScale,
            }).ToList();
        }
    }
}
