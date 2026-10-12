using HarmonyLib;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Scales every hit the Forest Gammeltroll deals by the DamageMultiplier config.
    ///     Runs on the machine that owns the troll (where its attacks are simulated).
    /// </summary>
    internal static class DamagePatches
    {
        // Melee, area and projectile attacks all build their HitData through Attack.ModifyDamage
        [HarmonyPatch(typeof(Attack), "ModifyDamage")]
        private static class Attack_ModifyDamage
        {
            private static void Postfix(HitData hitData, Humanoid ___m_character)
            {
                if (ForestTrollCreature.IsForestTroll(___m_character))
                {
                    hitData.m_damage.Modify(ForestGammeltroll.DamageMultiplier.Value);
                }
            }
        }

        // The ground slam shockwave (trollsnow_groundslam_aoe) is spawned without HitData and
        // uses its own m_damage, so it never passes through Attack.ModifyDamage
        [HarmonyPatch(typeof(Aoe), nameof(Aoe.Setup))]
        private static class Aoe_Setup
        {
            private static void Postfix(Aoe __instance, Character owner, HitData hitData)
            {
                bool usesAttackDamage = hitData != null && __instance.m_useAttackSettings;
                if (!usesAttackDamage && ForestTrollCreature.IsForestTroll(owner))
                {
                    __instance.m_damage.Modify(ForestGammeltroll.DamageMultiplier.Value);
                }
            }
        }

        // Same for projectiles spawned without HitData (e.g. secondary projectiles)
        [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
        private static class Projectile_Setup
        {
            private static void Postfix(Projectile __instance, Character owner, HitData hitData)
            {
                if (hitData == null && ForestTrollCreature.IsForestTroll(owner))
                {
                    __instance.m_damage.Modify(ForestGammeltroll.DamageMultiplier.Value);
                }
            }
        }
    }
}
