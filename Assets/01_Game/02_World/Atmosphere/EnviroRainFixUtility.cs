#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Zombera.Environment
{
    /// <summary>
    ///     Repairs common "purple rain" causes by fixing invalid particle shaders/materials
    ///     and neutralizing magenta color tints on rain-like particle renderers.
    /// </summary>
    public static class EnviroRainFixUtility
    {
        private static Material _fallbackMaterial;
        private static readonly Dictionary<int, Material> FallbackBySourceMaterialId = new();

        public static int ApplySceneRainFixes(bool includeInactive = true, bool neutralizeMagentaTint = true)
        {
            var shader = ResolveParticleShader();
            if (shader == null)
            {
                Debug.LogWarning("[EnviroRainFixUtility] No compatible particle shader found for rain fallback.");
                return 0;
            }

            EnsureFallbackMaterial(shader);

            var fixedCount = 0;
            var renderers = FindParticleRenderers(includeInactive);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !IsRainLike(renderer)) continue;

                var shared = renderer.sharedMaterial;
                if (NeedsFallbackMaterial(shared))
                {
                    renderer.sharedMaterial = ResolveFallbackMaterial(shared, shader);
                    fixedCount++;
                    continue;
                }

                if (!neutralizeMagentaTint || shared == null) continue;

                if (!TryGetMaterialColor(shared, out var tint)) continue;
                if (!IsMagentaLike(tint)) continue;

                var runtimeMaterial = renderer.material;
                SetMaterialColor(runtimeMaterial, new Color(1f, 1f, 1f, tint.a));
                fixedCount++;
            }

            if (fixedCount > 0)
                Debug.Log($"[EnviroRainFixUtility] Repaired {fixedCount} rain renderer(s) that could appear purple.");

            return fixedCount;
        }

        private static ParticleSystemRenderer[] FindParticleRenderers(bool includeInactive)
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<ParticleSystemRenderer>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<ParticleSystemRenderer>(includeInactive);
#endif
        }

        private static bool IsRainLike(ParticleSystemRenderer renderer)
        {
            var path = BuildHierarchyPath(renderer.transform).ToLowerInvariant();
            return path.Contains("rain")
                   || path.Contains("precip")
                   || path.Contains("storm")
                   || path.Contains("enviro");
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;

            var path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool NeedsFallbackMaterial(Material material)
        {
            if (material == null) return true;

            var shader = material.shader;
            if (shader == null) return true;
            if (!shader.isSupported) return true;
            if (string.Equals(shader.name, "Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase)) return true;
            if (UsesLegacyEnviroWeatherShader(shader)) return true;

            return false;
        }

        private static bool UsesLegacyEnviroWeatherShader(Shader shader)
        {
            if (shader == null || !IsUrpActive()) return false;

            var shaderName = shader.name;
            return shaderName.StartsWith("Enviro3/Particles/", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(shaderName, "Enviro/Standard", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUrpActive()
        {
            return GraphicsSettings.currentRenderPipeline != null;
        }

        private static Shader ResolveParticleShader()
        {
            if (IsUrpActive())
            {
                return Shader.Find("Universal Render Pipeline/Particles/Unlit")
                       ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            }

            return Shader.Find("Particles/Standard Unlit")
                   ?? Shader.Find("Standard");
        }

        private static Material ResolveFallbackMaterial(Material source, Shader shader)
        {
            if (source == null) return _fallbackMaterial;

            var sourceId = source.GetInstanceID();
            if (FallbackBySourceMaterialId.TryGetValue(sourceId, out var cached)
                && cached != null
                && cached.shader == shader)
            {
                return cached;
            }

            var fallback = CreateFallbackMaterialFromSource(source, shader);
            FallbackBySourceMaterialId[sourceId] = fallback;
            return fallback;
        }

        private static Material CreateFallbackMaterialFromSource(Material source, Shader shader)
        {
            var fallback = new Material(shader)
            {
                name = source.name + "_URP_Fallback",
                renderQueue = source.renderQueue > 0 ? source.renderQueue : 3000
            };

            CopyTexture(source, fallback, "_MainTex", "_BaseMap");
            CopyTexture(source, fallback, "_MainTex", "_MainTex");

            if (TryGetMaterialColor(source, out var tint))
                SetMaterialColor(fallback, tint);
            else
                SetMaterialColor(fallback, new Color(1f, 1f, 1f, 0.75f));

            ConfigureTransparentParticle(fallback);
            return fallback;
        }

        private static void CopyTexture(Material source, Material destination, string sourceProperty, string destinationProperty)
        {
            if (source == null || destination == null) return;
            if (!source.HasProperty(sourceProperty) || !destination.HasProperty(destinationProperty)) return;

            var texture = source.GetTexture(sourceProperty);
            if (texture != null)
                destination.SetTexture(destinationProperty, texture);
        }

        private static void ConfigureTransparentParticle(Material material)
        {
            if (material == null) return;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static void EnsureFallbackMaterial(Shader shader)
        {
            if (_fallbackMaterial != null && _fallbackMaterial.shader == shader) return;

            _fallbackMaterial = new Material(shader)
            {
                name = "Runtime_Rain_Fallback_Material",
                renderQueue = 3000
            };
            SetMaterialColor(_fallbackMaterial, new Color(1f, 1f, 1f, 0.75f));
            ConfigureTransparentParticle(_fallbackMaterial);
        }

        private static bool TryGetMaterialColor(Material material, out Color color)
        {
            color = Color.white;
            if (material == null) return false;

            if (material.HasProperty("_TintColor"))
            {
                color = material.GetColor("_TintColor");
                return true;
            }

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }

            return false;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;

            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static bool IsMagentaLike(Color color)
        {
            return color.r >= 0.75f && color.b >= 0.75f && color.g <= 0.35f;
        }
    }
}