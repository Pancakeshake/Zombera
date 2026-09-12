#region

using UnityEngine;

#endregion

namespace Zombera.Editor
{
    public static partial class BuildPrefabSpritePipelineTool
    {
        internal const string IconRenderScenePath = "Assets/Scenes/Tools/BuildingIconRender.unity";
        internal const string RenderTextureAssetPath = "Assets/Art/BuildingIcons/BuildIconCaptureRT.renderTexture";
        internal const string IconOutputFolderPath = "Assets/Art/BuildingIcons/Generated";
        internal const string SpriteLibraryAssetPath = "Assets/Data/Building/BuildPrefabSpriteLibrary.asset";

        internal static readonly string[] DefaultPrefabFolders =
        {
            "Assets/02_Shared/Prefabs/Building/Building_Modular_Parts",
            "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Packages/Extensions/Survival Blueprint",
            "Assets/03_ThirdParty/Mind Code Interactive/Easy Build System/Packages/Extensions/Survival Upgradable"
        };

        internal const string RigRootName = "BuildingIconRenderRig";
        internal const string PreviewRootName = "PreviewRoot";
        internal const string CameraNodeName = "IconRenderCamera";
        internal const string KeyLightNodeName = "KeyLight";
        internal const string FillLightNodeName = "FillLight";

        internal const int RenderResolution = 1024;
        internal const float CameraPitch = 30f;
        internal const float CameraYaw = 35f;
        internal const float FramingPadding = 1.12f;

        internal static readonly Color TransparentBackground = new(0f, 0f, 0f, 0f);
    }
}
