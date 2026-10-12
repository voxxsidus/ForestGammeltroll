using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Debug command: pushes Troll spawns through the real open-world SpawnSystem.Spawn path near the player,
    ///     so the spawn swap can be tested without waiting for the Troll entry's 4000 s interval.
    /// </summary>
    internal class TestSpawnCommand : ConsoleCommand
    {
        private const int MaxCount = 50;

        public override string Name => "fgt_testspawn";

        public override string Help => $"[count] Spawn Trolls through the Black Forest open-world spawn path (max {MaxCount}); each rolls the swap chance";

        public override bool IsCheat => true;

        public override void Run(string[] args, Terminal context)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                return;
            }

            int count = args.Length > 0 && int.TryParse(args[0], out int n) ? Mathf.Clamp(n, 1, MaxCount) : 1;

            var instances = AccessTools.Field(typeof(SpawnSystem), "m_instances")?.GetValue(null) as List<SpawnSystem>;
            var spawnSystem = instances?
                .Where(s => s)
                .OrderBy(s => Vector3.Distance(s.transform.position, player.transform.position))
                .FirstOrDefault();
            var troll = spawnSystem?.m_spawnLists
                .SelectMany(l => l.m_spawners)
                .FirstOrDefault(s => s.m_prefab && s.m_prefab.name == "Troll");
            if (troll == null)
            {
                context?.AddString("No SpawnSystem with a Troll entry found near you");
                return;
            }

            var spawn = AccessTools.Method(typeof(SpawnSystem), "Spawn");
            for (int i = 0; i < count; i++)
            {
                var offset = Random.insideUnitCircle * 10f;
                var point = player.transform.position + player.transform.forward * 30f + new Vector3(offset.x, 0f, offset.y);
                point.y = ZoneSystem.instance.GetGroundHeight(point) + 0.5f;
                spawn.Invoke(spawnSystem, new object[] { troll, point, false });
            }

            context?.AddString($"Ran {count} Troll spawn(s) through SpawnSystem.Spawn; see the log for swaps " +
                               $"(SpawnChance {ForestGammeltroll.SpawnChance.Value}%)");
        }
    }
}
