using System;
using UnityEngine;
using UnityEngine.Rendering;
using Zombera.World.CityPipeline.WorldBuilder;

namespace Zombera.World.Enviro
{
    /// <summary>
    /// Editor-only URP provisioning: Depth Texture + Enviro URP Render Feature on active renderers.
    /// </summary>
    public sealed partial class EnviroWorldEnvironmentBackend
    {
        private void EnsureUrpSupportForProfile(WorldEnvironmentProfile profile)
        {
            if (profile == null || !profile.RequireUrpRenderFeature) return;
#if UNITY_EDITOR
            TryEnsureUrpDepthAndFeature();
#endif
        }

#if UNITY_EDITOR
        private static bool TryEnsureUrpDepthAndFeature()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return false;

            var pipelineTypeName = pipeline.GetType().Name;
            if (pipelineTypeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) < 0)
                return true;

            EnableDepthTexture(pipeline);

            var so = new UnityEditor.SerializedObject(pipeline);
            var listProp = so.FindProperty("m_RendererDataList");
            if (listProp == null || !listProp.isArray) return false;

            var featureType = FindType("Enviro.EnviroURPRenderFeature");
            if (featureType == null)
            {
                Debug.LogWarning(
                    "[EnviroWorldEnvironmentBackend] EnviroURPRenderFeature type not found. " +
                    "Is ENVIRO_URP defined?");
                return false;
            }

            var changed = false;
            for (var i = 0; i < listProp.arraySize; i++)
            {
                var renderer = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (renderer == null) continue;
                if (RendererDataHasEnviroFeature(renderer)) continue;
                if (TryAddEnviroFeatureToRenderer(renderer, featureType))
                    changed = true;
            }

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(pipeline);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[EnviroWorldEnvironmentBackend] Added Enviro URP Render Feature to active URP renderer(s).");
            }

            return UrpRendererHasEnviroFeature(pipeline);
        }

        private static void EnableDepthTexture(UnityEngine.Object pipelineAsset)
        {
            var so = new UnityEditor.SerializedObject(pipelineAsset);
            var depth = so.FindProperty("m_RequireDepthTexture");
            if (depth == null || depth.boolValue) return;

            depth.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(pipelineAsset);
            Debug.Log("[EnviroWorldEnvironmentBackend] Enabled URP Depth Texture for Enviro fog/clouds.");
        }

        private static bool TryAddEnviroFeatureToRenderer(UnityEngine.Object rendererData, Type featureType)
        {
            if (rendererData == null || featureType == null) return false;

            var feature = ScriptableObject.CreateInstance(featureType);
            feature.name = "Enviro URP Render Feature";
            UnityEditor.AssetDatabase.AddObjectToAsset(feature, rendererData);

            var so = new UnityEditor.SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (features == null)
            {
                UnityEngine.Object.DestroyImmediate(feature, true);
                return false;
            }

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;

            if (map != null)
            {
                map.arraySize = features.arraySize;
                if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature, out _, out long localId))
                {
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(rendererData);
            return true;
        }
#endif
    }
}
