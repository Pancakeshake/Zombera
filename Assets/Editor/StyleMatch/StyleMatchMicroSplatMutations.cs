#if UNITY_EDITOR
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor.StyleMatch
{
    internal static class StyleMatchMicroSplatMutations
    {
        private const string PropDataPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat_propdata.asset";

        private const string WorldConfigPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/Microsplat_World.asset";

        private const string TemplateMatPath =
            "Assets/01_Game/02_World/Terrain/Materials/Microsplat/MicroSplat.mat";

        public static bool BumpFloat(MicroSplatPropData.PerTexFloat channel, float delta, float min, float max)
        {
            var prop = LoadPropData();
            if (prop == null)
                return false;

            var count = prop.maxTextures;
            for (var i = 0; i < count; i++)
            {
                var v = GetPerTexFloat(prop, i, channel);
                prop.SetValue(i, channel, Mathf.Clamp(v + delta, min, max));
            }

            Save(prop);
            return true;
        }

        public static bool BumpFloatOnLayers(
            MicroSplatPropData.PerTexFloat channel,
            float delta,
            int fromInclusive,
            int toInclusive,
            float min,
            float max)
        {
            var prop = LoadPropData();
            if (prop == null)
                return false;

            var count = prop.maxTextures;
            for (var i = fromInclusive; i <= toInclusive && i < count; i++)
            {
                var v = GetPerTexFloat(prop, i, channel);
                prop.SetValue(i, channel, Mathf.Clamp(v + delta, min, max));
            }

            Save(prop);
            return true;
        }

        public static bool CompileWorldConfig(bool force = false) =>
            StyleMatchMicroSplatCompileGuard.TryRun(
                StyleMatchMicroSplatCompileGuard.Operation.CompileConfig,
                CompileWorldConfigInternal,
                force);

        public static bool CompileTemplateMaterial(bool force = false) =>
            StyleMatchMicroSplatCompileGuard.TryRun(
                StyleMatchMicroSplatCompileGuard.Operation.CompileTemplate,
                CompileTemplateMaterialInternal,
                force);

        private static bool CompileWorldConfigInternal()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<TextureArrayConfig>(WorldConfigPath);
            if (cfg == null)
                return false;

            TextureArrayConfigEditor.CompileConfig(cfg);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool CompileTemplateMaterialInternal()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TemplateMatPath);
            if (mat == null)
                return false;

            var compiler = new MicroSplatShaderGUI.MicroSplatCompiler();
            compiler.Compile(mat);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static float GetPerTexFloat(
            MicroSplatPropData prop,
            int textureIndex,
            MicroSplatPropData.PerTexFloat channel)
        {
            var encoded = (float)channel / 4f;
            var attrRow = (int)encoded;
            var colorChannel = Mathf.RoundToInt((encoded - attrRow) * 4f);
            return prop.GetValue(textureIndex, attrRow)[colorChannel];
        }

        private static MicroSplatPropData LoadPropData() =>
            AssetDatabase.LoadAssetAtPath<MicroSplatPropData>(PropDataPath);

        private static void Save(MicroSplatPropData prop)
        {
            prop.RevisionData();
            prop.GetTexture();
            EditorUtility.SetDirty(prop);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
