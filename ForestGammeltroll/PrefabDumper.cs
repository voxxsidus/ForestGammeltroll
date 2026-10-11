using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Phase 1 diagnostics: writes the Gammeltroll and Troll prefabs (hierarchy, components,
    ///     serialized fields, materials/shaders, drops and death chain) to a text file.
    /// </summary>
    internal static class PrefabDumper
    {
        public const string DumpFileName = "ForestGammeltroll_PrefabDump.txt";

        private const int MaxFieldDepth = 7;
        private const int MaxListElements = 64;
        private const int MaxLinkedPrefabDepth = 3;

        // Fields whose names match this are treated as "runs on death / destruction":
        // every root prefab referenced beneath them gets a full dump of its own.
        private static readonly string[] DeathChainFieldHints = { "death", "ragdoll", "destroy", "remove", "petrif" };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private class DumpContext
        {
            public readonly StringBuilder Sb = new StringBuilder();
            public readonly Queue<(GameObject prefab, string via, int depth)> LinkedQueue = new Queue<(GameObject, string, int)>();
            public readonly HashSet<GameObject> Dumped = new HashSet<GameObject>();
            public GameObject CurrentRoot;
            public int CurrentLinkDepth;
        }

        public static void DumpTrolls()
        {
            try
            {
                var ctx = new DumpContext();
                var sb = ctx.Sb;

                sb.AppendLine($"ForestGammeltroll prefab dump - Valheim {GameVersionString()} - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                var characters = PrefabManager.Cache.GetPrefabs(typeof(Character))
                    .Values.OfType<Character>()
                    .Select(c => c.gameObject)
                    .Where(go => go.transform.parent == null)
                    .Distinct()
                    .OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var candidates = characters
                    .Where(go => ContainsCI(go.name, "gammel") || ContainsCI(go.name, "troll"))
                    .ToList();

                Section(sb, "CANDIDATE PREFABS (Character prefabs with 'gammel' or 'troll' in the name)");
                foreach (var go in candidates)
                {
                    sb.AppendLine($"  {go.name}  [{string.Join(", ", go.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name))}]");
                }
                sb.AppendLine();

                DumpSpawnReferences(sb);

                var gammel = candidates.Where(go => ContainsCI(go.name, "gammel")).ToList();
                var troll = candidates.FirstOrDefault(go => go.name == "Troll");

                if (gammel.Count == 0)
                {
                    sb.AppendLine("!!! No prefab containing 'gammel' was found.");
                }

                foreach (var go in gammel)
                {
                    Section(sb, $"FULL DUMP: {go.name}");
                    DumpPrefab(ctx, go, includeRenderers: true);
                    DumpHumanoidItems(ctx, go);
                }

                if (ctx.LinkedQueue.Count > 0)
                {
                    Section(sb, "DEATH CHAIN: prefabs referenced from death/ragdoll/destroy/remove fields");
                    while (ctx.LinkedQueue.Count > 0)
                    {
                        var (prefab, via, depth) = ctx.LinkedQueue.Dequeue();
                        if (ctx.Dumped.Contains(prefab))
                        {
                            continue;
                        }
                        sb.AppendLine();
                        sb.AppendLine($"----- Linked prefab: {prefab.name}  (via {via}) -----");
                        ctx.CurrentLinkDepth = depth;
                        DumpPrefab(ctx, prefab, includeRenderers: true);
                    }
                }

                if (troll)
                {
                    Section(sb, "COMPARISON: regular Troll (components and stats, no materials)");
                    ctx.CurrentLinkDepth = MaxLinkedPrefabDepth; // don't follow its death chain
                    DumpPrefab(ctx, troll, includeRenderers: false);
                    DumpHumanoidItems(ctx, troll);
                }
                else
                {
                    sb.AppendLine("!!! No prefab named exactly 'Troll' was found.");
                }

                string path = Path.Combine(Paths.BepInExRootPath, DumpFileName);
                File.WriteAllText(path, sb.ToString());
                Jotunn.Logger.LogInfo($"Prefab dump written to {path} ({gammel.Count} gammel prefab(s), troll found: {(bool)troll})");
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogError($"Prefab dump failed: {e}");
            }
        }

        private static void DumpSpawnReferences(StringBuilder sb)
        {
            Section(sb, "SPAWN REFERENCES (loaded SpawnSystemList / CreatureSpawner entries for trolls)");

            foreach (var list in Resources.FindObjectsOfTypeAll<SpawnSystemList>())
            {
                foreach (var s in list.m_spawners)
                {
                    if (!s.m_prefab || !(ContainsCI(s.m_prefab.name, "troll") || ContainsCI(s.m_prefab.name, "gammel")))
                    {
                        continue;
                    }
                    sb.AppendLine($"  SpawnSystemList '{list.name}': name='{s.m_name}' prefab={s.m_prefab.name} biome={s.m_biome} area={s.m_biomeArea} " +
                                  $"enabled={s.m_enabled} chance={F(s.m_spawnChance)} interval={F(s.m_spawnInterval)} maxSpawned={s.m_maxSpawned} " +
                                  $"group={s.m_groupSizeMin}-{s.m_groupSizeMax} night={s.m_spawnAtNight} day={s.m_spawnAtDay} " +
                                  $"levels={s.m_minLevel}-{s.m_maxLevel} globalKey='{s.m_requiredGlobalKey}' huntPlayer={s.m_huntPlayer}");
                }
            }

            foreach (var cs in Resources.FindObjectsOfTypeAll<CreatureSpawner>())
            {
                if (!cs.m_creaturePrefab || !(ContainsCI(cs.m_creaturePrefab.name, "troll") || ContainsCI(cs.m_creaturePrefab.name, "gammel")))
                {
                    continue;
                }
                sb.AppendLine($"  CreatureSpawner on '{GetPath(cs.transform)}': prefab={cs.m_creaturePrefab.name} levels={cs.m_minLevel}-{cs.m_maxLevel}");
            }

            sb.AppendLine("  (Location prefabs are soft-referenced and usually not loaded at the main menu, so CreatureSpawners in locations may be missing here.)");
            sb.AppendLine();
        }

        private static void DumpPrefab(DumpContext ctx, GameObject prefab, bool includeRenderers)
        {
            ctx.Dumped.Add(prefab);
            var previousRoot = ctx.CurrentRoot;
            ctx.CurrentRoot = prefab;
            DumpGameObject(ctx, prefab.transform, 0, includeRenderers);
            ctx.CurrentRoot = previousRoot;
        }

        private static void DumpGameObject(DumpContext ctx, Transform t, int indent, bool includeRenderers)
        {
            var sb = ctx.Sb;
            string pad = new string(' ', indent * 2);
            var go = t.gameObject;

            sb.AppendLine($"{pad}+ {go.name}  (activeSelf={go.activeSelf}, layer={LayerMask.LayerToName(go.layer)}({go.layer}), tag={go.tag})");
            sb.AppendLine($"{pad}    localPos={V(t.localPosition)} localRot={V(t.localEulerAngles)} localScale={V(t.localScale)}");

            foreach (var c in go.GetComponents<Component>())
            {
                if (!c)
                {
                    sb.AppendLine($"{pad}  * <missing script>");
                    continue;
                }
                if (c is Transform)
                {
                    continue;
                }

                sb.AppendLine($"{pad}  * {c.GetType().FullName}");
                string fpad = pad + "      ";
                DumpBuiltinComponent(ctx, c, fpad, includeRenderers);
                if (c is MonoBehaviour)
                {
                    DumpFields(ctx, c, c.GetType(), fpad, 0, follow: false, "");
                }
            }

            foreach (Transform child in t)
            {
                DumpGameObject(ctx, child, indent + 1, includeRenderers);
            }
        }

        private static void DumpBuiltinComponent(DumpContext ctx, Component c, string pad, bool includeRenderers)
        {
            var sb = ctx.Sb;
            switch (c)
            {
                case SkinnedMeshRenderer smr:
                    sb.AppendLine($"{pad}enabled={smr.enabled} mesh={Name(smr.sharedMesh)} verts={(smr.sharedMesh ? smr.sharedMesh.vertexCount : 0)} " +
                                  $"subMeshes={(smr.sharedMesh ? smr.sharedMesh.subMeshCount : 0)} rootBone={Name(smr.rootBone)} bones={smr.bones?.Length ?? 0}");
                    if (includeRenderers) DumpMaterials(sb, smr, pad);
                    break;
                case MeshRenderer mr:
                    sb.AppendLine($"{pad}enabled={mr.enabled} shadowCasting={mr.shadowCastingMode}");
                    if (includeRenderers) DumpMaterials(sb, mr, pad);
                    break;
                case Renderer r:
                    sb.AppendLine($"{pad}enabled={r.enabled}");
                    if (includeRenderers) DumpMaterials(sb, r, pad);
                    break;
                case MeshFilter mf:
                    sb.AppendLine($"{pad}mesh={Name(mf.sharedMesh)} verts={(mf.sharedMesh ? mf.sharedMesh.vertexCount : 0)}");
                    break;
                case CapsuleCollider cc:
                    sb.AppendLine($"{pad}trigger={cc.isTrigger} center={V(cc.center)} radius={F(cc.radius)} height={F(cc.height)} dir={cc.direction}");
                    break;
                case SphereCollider sc:
                    sb.AppendLine($"{pad}trigger={sc.isTrigger} center={V(sc.center)} radius={F(sc.radius)}");
                    break;
                case BoxCollider bc:
                    sb.AppendLine($"{pad}trigger={bc.isTrigger} center={V(bc.center)} size={V(bc.size)}");
                    break;
                case MeshCollider mc:
                    sb.AppendLine($"{pad}trigger={mc.isTrigger} convex={mc.convex} mesh={Name(mc.sharedMesh)}");
                    break;
                case Rigidbody rb:
                    sb.AppendLine($"{pad}mass={F(rb.mass)} kinematic={rb.isKinematic} gravity={rb.useGravity}");
                    break;
                case Animator an:
                    sb.AppendLine($"{pad}controller={Name(an.runtimeAnimatorController)} avatar={Name(an.avatar)} applyRootMotion={an.applyRootMotion}");
                    break;
                case LODGroup lod:
                    var lods = lod.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        sb.AppendLine($"{pad}LOD{i}: screenHeight={F(lods[i].screenRelativeTransitionHeight)} renderers=[{string.Join(", ", lods[i].renderers.Select(Name))}]");
                    }
                    break;
                case ParticleSystem ps:
                    sb.AppendLine($"{pad}playOnAwake={ps.main.playOnAwake} loop={ps.main.loop}");
                    break;
            }
        }

        private static void DumpMaterials(StringBuilder sb, Renderer r, string pad)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m)
                {
                    sb.AppendLine($"{pad}material[{i}] = null");
                    continue;
                }

                var shader = m.shader;
                sb.AppendLine($"{pad}material[{i}] = '{m.name}' shader='{(shader ? shader.name : "null")}' renderQueue={m.renderQueue}");
                sb.AppendLine($"{pad}  keywords=[{string.Join(", ", m.shaderKeywords)}]");
                if (!shader)
                {
                    continue;
                }

                int count = shader.GetPropertyCount();
                for (int p = 0; p < count; p++)
                {
                    string propName = shader.GetPropertyName(p);
                    int id = shader.GetPropertyNameId(p);
                    var type = shader.GetPropertyType(p);
                    string value;
                    switch (type)
                    {
                        case ShaderPropertyType.Color:
                            value = m.GetColor(id).ToString();
                            break;
                        case ShaderPropertyType.Vector:
                            value = m.GetVector(id).ToString();
                            break;
                        case ShaderPropertyType.Texture:
                            var tex = m.GetTexture(id);
                            value = tex ? $"'{tex.name}' ({tex.width}x{tex.height}) scale={m.GetTextureScale(id)} offset={m.GetTextureOffset(id)}" : "null";
                            break;
                        case ShaderPropertyType.Int:
                            value = m.GetInteger(id).ToString(Inv);
                            break;
                        default:
                            value = F(m.GetFloat(id));
                            break;
                    }
                    sb.AppendLine($"{pad}  {propName} ({type}, \"{shader.GetPropertyDescription(p)}\") = {value}");
                }
            }
        }

        private static void DumpHumanoidItems(DumpContext ctx, GameObject go)
        {
            var humanoid = go.GetComponent<Humanoid>();
            if (!humanoid)
            {
                return;
            }

            var items = new List<(string field, GameObject item)>();
            void Add(string field, IEnumerable<GameObject> list)
            {
                if (list == null) return;
                items.AddRange(list.Where(i => i).Select(i => (field, i)));
            }

            Add("m_defaultItems", humanoid.m_defaultItems);
            Add("m_randomWeapon", humanoid.m_randomWeapon);
            Add("m_randomArmor", humanoid.m_randomArmor);
            Add("m_randomShield", humanoid.m_randomShield);
            if (humanoid.m_randomSets != null)
            {
                for (int i = 0; i < humanoid.m_randomSets.Length; i++)
                {
                    Add($"m_randomSets[{i}]", humanoid.m_randomSets[i]?.m_items);
                }
            }

            ctx.Sb.AppendLine();
            ctx.Sb.AppendLine($"----- Humanoid items of {go.name} (attacks/damage live here) -----");
            foreach (var (field, item) in items)
            {
                ctx.Sb.AppendLine($"  [{field}] {item.name}");
                var itemDrop = item.GetComponent<ItemDrop>();
                if (itemDrop)
                {
                    DumpFields(ctx, itemDrop, itemDrop.GetType(), "      ", 0, follow: false, "");
                }
            }
        }

        private static void DumpFields(DumpContext ctx, object obj, Type type, string pad, int depth, bool follow, string path)
        {
            foreach (var field in GetSerializedFields(type))
            {
                object value;
                try
                {
                    value = field.GetValue(obj);
                }
                catch (Exception e)
                {
                    ctx.Sb.AppendLine($"{pad}{field.Name} = <error {e.GetType().Name}>");
                    continue;
                }

                bool childFollow = follow || DeathChainFieldHints.Any(h => ContainsCI(field.Name, h));
                DumpValue(ctx, field.Name, value, field.FieldType, pad, depth, childFollow, path + "." + field.Name);
            }
        }

        private static void DumpValue(DumpContext ctx, string label, object value, Type declaredType, string pad, int depth, bool follow, string path)
        {
            var sb = ctx.Sb;

            if (value == null)
            {
                sb.AppendLine($"{pad}{label} = null");
                return;
            }

            if (value is Object uo)
            {
                if (!uo)
                {
                    sb.AppendLine($"{pad}{label} = null");
                    return;
                }
                sb.AppendLine($"{pad}{label} = {Name(uo)} ({uo.GetType().Name})");
                if (follow)
                {
                    EnqueueLinked(ctx, uo, path);
                }
                return;
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string || value is decimal)
            {
                sb.AppendLine($"{pad}{label} = {FormatScalar(value)}");
                return;
            }

            if (type.Namespace == "UnityEngine")
            {
                // Vector3, Color, Quaternion, LayerMask, AnimationCurve, ...
                sb.AppendLine($"{pad}{label} = {value}");
                return;
            }

            if (value is IList list)
            {
                sb.AppendLine($"{pad}{label} = [{list.Count}]");
                if (depth >= MaxFieldDepth) return;
                for (int i = 0; i < list.Count && i < MaxListElements; i++)
                {
                    DumpValue(ctx, $"[{i}]", list[i], null, pad + "  ", depth + 1, follow, $"{path}[{i}]");
                }
                if (list.Count > MaxListElements)
                {
                    sb.AppendLine($"{pad}  ... {list.Count - MaxListElements} more");
                }
                return;
            }

            if (depth >= MaxFieldDepth)
            {
                sb.AppendLine($"{pad}{label} = {type.Name} {{...}}");
                return;
            }

            sb.AppendLine($"{pad}{label} = {type.Name}");
            DumpFields(ctx, value, type, pad + "  ", depth + 1, follow, path);
        }

        private static void EnqueueLinked(DumpContext ctx, Object uo, string path)
        {
            GameObject go = uo as GameObject ?? (uo as Component)?.gameObject;
            if (!go || go.transform.parent != null || go == ctx.CurrentRoot || ctx.Dumped.Contains(go))
            {
                return;
            }
            if (ctx.CurrentLinkDepth >= MaxLinkedPrefabDepth)
            {
                return;
            }
            ctx.LinkedQueue.Enqueue((go, $"{ctx.CurrentRoot.name}{path}", ctx.CurrentLinkDepth + 1));
        }

        private static IEnumerable<FieldInfo> GetSerializedFields(Type type)
        {
            var chain = new List<Type>();
            for (var t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(object); t = t.BaseType)
            {
                chain.Add(t);
            }
            chain.Reverse();

            foreach (var t in chain)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsDefined(typeof(NonSerializedAttribute), false) || f.Name.Contains("<"))
                    {
                        continue;
                    }
                    if (f.IsPublic || f.IsDefined(typeof(SerializeField), false))
                    {
                        yield return f;
                    }
                }
            }
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.AppendLine(new string('=', 100));
            sb.AppendLine(title);
            sb.AppendLine(new string('=', 100));
        }

        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            for (; t; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string GameVersionString() => global::Version.GetVersionString();

        private static bool ContainsCI(string s, string sub) => s != null && s.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Name(Object o) => o ? $"'{o.name}'" : "null";

        private static string F(float f) => f.ToString("0.####", Inv);

        private static string V(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

        private static string FormatScalar(object value) =>
            value is float f ? F(f) :
            value is string s ? $"\"{s}\"" :
            Convert.ToString(value, Inv);
    }
}
