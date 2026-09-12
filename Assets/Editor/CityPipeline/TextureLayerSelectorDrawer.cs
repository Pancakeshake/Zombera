#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using JBooth.MicroSplat;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    /// <summary>
    ///     Replaces the int slider for [TextureLayerSelector] fields with a dropdown
    ///     that shows the actual terrain layer name from the parent
    ///     DistrictLotTerrainLayout's TextureArrayConfig.
    /// </summary>
    [CustomPropertyDrawer(typeof(TextureLayerSelectorAttribute))]
    internal sealed class TextureLayerSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var cfg = ResolveConfig(property);
            if (cfg?.sourceTextures == null || cfg.sourceTextures.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var count = cfg.sourceTextures.Count;
            var names = new string[count];
            for (var i = 0; i < count; i++)
            {
                var entry = cfg.sourceTextures[i];
                string layerName;
                if (entry != null && entry.terrainLayer != null)
                    layerName = entry.terrainLayer.name;
                else if (entry != null && entry.diffuse != null)
                    layerName = entry.diffuse.name;
                else
                    layerName = $"Layer {i}";
                names[i] = $"{i}: {layerName}";
            }

            property.intValue = Mathf.Clamp(property.intValue, 0, count - 1);
            property.intValue = EditorGUI.Popup(position, label.text, property.intValue, names);
        }

        private static TextureArrayConfig ResolveConfig(SerializedProperty property)
        {
            // Walk up to find the DistrictLotTerrainLayout SO that owns this property.
            var target = property.serializedObject.targetObject;
            if (target is DistrictLotTerrainLayout layout && layout.textureArrayConfig != null)
                return layout.textureArrayConfig;

            // Fallback: try the one wired in the builder.
            var builder = Object.FindFirstObjectByType<CityPrefabRoadNetworkBuilder>();
            if (builder != null)
            {
                var tac = builder.DistrictLotTerrainConfig?.textureArrayConfig;
                if (tac != null) return tac;
            }

            return null;
        }
    }
}
#endif
