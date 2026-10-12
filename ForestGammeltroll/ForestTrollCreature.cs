using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     The Black Forest Gammeltroll: a clone of the vanilla Gammeltroll (TrollFrost) that
    ///     petrifies into its own statue (see <see cref="PetrifiedRemains"/>) instead of the Bloodgold one.
    /// </summary>
    internal static class ForestTrollCreature
    {
        public const string PrefabName = "ForestGammeltroll";
        public const string NameToken = "$enemy_forestgammeltroll";

        // Vanilla prefab names, from the Phase 1 prefab dump
        private const string BasePrefabName = "TrollFrost";

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

            // Like the vanilla Gammeltroll, its CharacterDrop is empty: all loot comes from mining its petrified remains
            SetPetrifiedStatue(prefab.GetComponent<Character>(), PetrifiedRemains.Register());

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
            PetrifiedRemains.ApplySettings();

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

        private static void SetPetrifiedStatue(Character character, GameObject statue)
        {
            // The vanilla death effects spawn TrollFrost_Dead, a stone statue that breaks into a Bloodgold rock.
            // Swap in our own statue; if it couldn't be created, drop the statue entirely so the body
            // vanishes in the remaining smoke/pebble effects rather than leaving Bloodgold behind.
            bool found = false;
            var effects = character.m_deathEffects.m_effectPrefabs.ToList();
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                if (!effects[i].m_prefab || effects[i].m_prefab.name != PetrifiedRemains.VanillaStatuePrefabName)
                {
                    continue;
                }
                found = true;
                if (statue)
                {
                    effects[i].m_prefab = statue;
                }
                else
                {
                    effects.RemoveAt(i);
                }
            }
            character.m_deathEffects.m_effectPrefabs = effects.ToArray();

            if (!found)
            {
                Jotunn.Logger.LogWarning($"{PetrifiedRemains.VanillaStatuePrefabName} not found in {BasePrefabName} death effects; it will not turn to stone");
            }
        }
    }
}
