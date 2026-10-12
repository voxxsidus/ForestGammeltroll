using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace ForestGammeltroll
{
    /// <summary>
    ///     Turns the snowy Gammeltroll look into a Black Forest one. Textures are generated at runtime
    ///     from vanilla textures (no game art is shipped with the mod):
    ///     - skin: the white snow patches baked into frosttroll_d become Black Forest rock moss; hair becomes lichen
    ///     - back trees: the snowy pine card in the tree atlas is replaced by the green small fir
    ///     - petrified rock: the Bloodgold vein material becomes the vanilla copper deposit interior
    ///     - snowy death/hit effects become their Black Forest equivalents
    /// </summary>
    internal static class ForestVisuals
    {
        // Vanilla asset names, verified against the game's asset bundles
        private const string SkinMaterialName = "FrostTrollmat";
        private const string BackTreesMaterialName = "Fir_Snow_Troll_mat";
        private const string BloodgoldVeinMaterialName = "FrostTrollBloodGold_mat";
        private const string GreenFirPrefabName = "FirTree_small";
        private const string GreenFirMaterialName = "Pine_tree_small";
        private const string CopperRockPrefabName = "rock4_copper";
        private const string CopperRockMaterialName = "rock1_copper";
        private const string CopperRockMossProperty = "_MossTex";
        private const string CopperFracPrefabName = "rock4_copper_frac";
        private const string CopperVeinMaterialName = "rock_copper_internal";

        private static readonly Dictionary<string, string> EffectReplacements = new Dictionary<string, string>
        {
            { "vfx_firetreecut_snow_FrostTrollDeath", "vfx_firetreecut" },
            { "vfx_frostarrow_hit", "vfx_foresttroll_hit" },
        };

        // UV rectangles (Unity UV space, origin bottom-left), measured from the meshes and atlases.
        // The back-tree cards (Body submesh 1) sample this snowy fir in Pine_tree_snow_small_d...
        private static readonly Rect SnowyFirCard = new Rect(23f / 64f, 0f, 21f / 64f, 44f / 64f);
        // ...which is replaced by the middle green fir of Pine_tree_texture_small
        private static readonly Rect GreenFirCard = new Rect(47f / 128f, 0f, 39f / 128f, 85f / 128f);
        // The Hair mesh only samples this block of frosttroll_d
        private static readonly Rect HairBlock = new Rect(0.58f, 0f, 0.42f, 0.36f);

        private const int BackTreesUpscale = 4;
        private const float MossTiling = 2f;
        private const float MossGain = 1.35f;
        private const float SnowFeather = 0.27f;

        private static Texture2D s_snowySkin;
        private static Texture2D s_moss;
        private static Material s_skinMaterial;
        private static Texture2D s_generatedSkin;

        public static void Apply(GameObject creature, GameObject statue, GameObject rock)
        {
            var renderers = new[] { creature, statue, rock }
                .Where(go => go)
                .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                .ToList();

            var replacements = new Dictionary<string, Material>();

            var skin = FindMaterial(renderers, SkinMaterialName);
            var moss = GetMossTexture();
            if (skin && moss)
            {
                s_snowySkin = ReadPixels(skin.mainTexture);
                s_moss = ReadPixels(moss);
                s_skinMaterial = new Material(skin) { name = "ForestGammeltroll_mat" };
                RegenerateSkin();
                replacements[SkinMaterialName] = s_skinMaterial;
            }

            var backTrees = FindMaterial(renderers, BackTreesMaterialName);
            var greenFir = GetMaterialFromPrefab(GreenFirPrefabName, GreenFirMaterialName);
            if (backTrees && greenFir)
            {
                var material = new Material(backTrees) { name = "ForestGammeltroll_Fir_mat" };
                material.mainTexture = BuildBackTreesTexture(backTrees.mainTexture, greenFir.mainTexture);
                replacements[BackTreesMaterialName] = material;
            }

            var copperVein = GetMaterialFromPrefab(CopperFracPrefabName, CopperVeinMaterialName);
            if (copperVein)
            {
                replacements[BloodgoldVeinMaterialName] = copperVein;
            }

            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(m => m && replacements.TryGetValue(m.name, out var r) ? r : m)
                    .ToArray();
            }

            ReplaceEffects(creature.GetComponent<Character>());

            Jotunn.Logger.LogInfo($"Forest visuals applied: {string.Join(", ", replacements.Keys)}");
        }

        /// <summary>
        ///     Rebuilds the mossy skin from the current config. Every renderer shares the material,
        ///     so live trolls and statues update immediately.
        /// </summary>
        public static void RegenerateSkin()
        {
            if (!s_skinMaterial)
            {
                return;
            }

            // Only ever destroy our own texture, never the vanilla one the material was cloned with
            var previous = s_generatedSkin;
            s_generatedSkin = BuildMossySkin(s_snowySkin, s_moss,
                ForestGammeltroll.MossTint.Value, ForestGammeltroll.SnowToMossThreshold.Value);
            s_skinMaterial.mainTexture = s_generatedSkin;

            // Starred trolls use hue-shifted copies of the skin material that LevelEffects caches per
            // prefab+level ("ForestGammeltroll2", ...); point those at the new texture too
            if (AccessTools.Field(typeof(LevelEffects), "m_materials")?.GetValue(null) is Dictionary<string, Material> levelMaterials)
            {
                foreach (var entry in levelMaterials.Where(e => e.Key.StartsWith(ForestTrollCreature.PrefabName) && e.Value))
                {
                    entry.Value.mainTexture = s_generatedSkin;
                }
            }

            if (previous)
            {
                Object.Destroy(previous);
            }
        }

        private static Texture2D BuildMossySkin(Texture2D snowy, Texture2D moss, Color tint, float threshold)
        {
            int w = snowy.width, h = snowy.height;
            var src = snowy.GetPixels();
            var dst = new Color[src.Length];
            var mossTint = new Color(tint.r * MossGain, tint.g * MossGain, tint.b * MossGain);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = src[i];
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;

                    float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                    var mossColor = moss.GetPixelBilinear(u * MossTiling, v * MossTiling) * mossTint;

                    Color result;
                    if (HairBlock.Contains(new Vector2(u, v)))
                    {
                        // Pale hair strands become hanging lichen
                        result = mossColor * (0.3f + 0.9f * lum);
                    }
                    else
                    {
                        // Bright, unsaturated pixels are the baked-in snow; saturated ones (eyes, mouth) are left alone
                        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                        float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        float saturation = max > 0f ? (max - min) / max : 0f;
                        float snow = SmoothStep(threshold, threshold + SnowFeather, lum) * (1f - SmoothStep(0.18f, 0.35f, saturation));
                        float shade = 0.55f + 0.9f * (lum - 0.5f);
                        result = Color.Lerp(c, mossColor * shade, snow);
                    }
                    result.a = c.a;
                    dst[i] = result;
                }
            }

            return CreateTexture(snowy, w, h, dst, "ForestGammeltroll_d");
        }

        private static Texture2D BuildBackTreesTexture(Texture snowySource, Texture greenSource)
        {
            var snowy = ReadPixels(snowySource);
            var green = ReadPixels(greenSource);

            // Upscale so the higher-resolution green fir keeps its detail
            int w = snowy.width * BackTreesUpscale, h = snowy.height * BackTreesUpscale;
            var dst = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);
                    if (SnowyFirCard.Contains(uv))
                    {
                        var local = Rect.PointToNormalized(SnowyFirCard, uv);
                        var g = Rect.NormalizedToPoint(GreenFirCard, local);
                        dst[y * w + x] = green.GetPixelBilinear(g.x, g.y);
                    }
                    else
                    {
                        dst[y * w + x] = snowy.GetPixelBilinear(uv.x, uv.y);
                    }
                }
            }

            var result = CreateTexture(snowySource, w, h, dst, "ForestGammeltroll_Fir_d");
            Object.Destroy(snowy);
            Object.Destroy(green);
            return result;
        }

        private static void ReplaceEffects(Character character)
        {
            foreach (var effects in new[] { character.m_deathEffects, character.m_hitEffects, character.m_critHitEffects })
            {
                foreach (var effect in effects.m_effectPrefabs)
                {
                    if (!effect.m_prefab || !EffectReplacements.TryGetValue(effect.m_prefab.name, out var replacementName))
                    {
                        continue;
                    }
                    var replacement = PrefabManager.Cache.GetPrefab<GameObject>(replacementName);
                    if (replacement)
                    {
                        effect.m_prefab = replacement;
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning($"Effect {replacementName} not found; keeping {effect.m_prefab.name}");
                    }
                }
            }
        }

        private static Texture GetMossTexture()
        {
            var material = GetMaterialFromPrefab(CopperRockPrefabName, CopperRockMaterialName);
            var moss = material && material.HasProperty(CopperRockMossProperty) ? material.GetTexture(CopperRockMossProperty) : null;
            if (!moss)
            {
                Jotunn.Logger.LogWarning($"No {CopperRockMossProperty} on {CopperRockMaterialName}; skin stays snowy");
            }
            return moss;
        }

        private static Material GetMaterialFromPrefab(string prefabName, string materialName)
        {
            var prefab = PrefabManager.Cache.GetPrefab<GameObject>(prefabName);
            var material = prefab ? FindMaterial(prefab.GetComponentsInChildren<Renderer>(true), materialName) : null;
            if (!material)
            {
                Jotunn.Logger.LogWarning($"Material {materialName} not found on {prefabName}");
            }
            return material;
        }

        private static Material FindMaterial(IEnumerable<Renderer> renderers, string name) =>
            renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name == name);

        /// <summary>
        ///     Copies a (usually non-readable) GPU texture into a readable one. Uses an sRGB render target
        ///     so colour values come back exactly as stored, in both linear and gamma colour space.
        /// </summary>
        private static Texture2D ReadPixels(Texture source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            copy.wrapMode = TextureWrapMode.Repeat;
            return copy;
        }

        private static Texture2D CreateTexture(Texture template, int w, int h, Color[] pixels, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = template.filterMode,
                wrapMode = template.wrapMode,
                anisoLevel = template.anisoLevel,
            };
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
