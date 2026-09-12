#if UNITY_EDITOR
using UnityEditor;

namespace Zombera.Editor
{
    public static partial class RoadGameplayTooling
    {
        [MenuItem(MenuRoot + "Build Automated Road Gameplay Stack (Active Scene)", priority = -500)]
        private static void BuildAutomatedRoadGameplayStackInActiveScene()
        {
            ExecuteBuildAutomatedRoadGameplayStackInActiveScene();
        }

        [MenuItem(MenuRoot + "Create Or Select Gameplay Road Graph", priority = -500)]
        private static void CreateOrSelectGraph()
        {
            ExecuteCreateOrSelectGraph();
        }

        [MenuItem(MenuRoot + "Create Or Select Derived Data Asset", priority = -500)]
        private static void CreateOrSelectDerivedData()
        {
            ExecuteCreateOrSelectDerivedData();
        }

        [MenuItem(MenuRoot + "Create Or Select Mask Bake Settings", priority = -500)]
        private static void CreateOrSelectMaskSettings()
        {
            ExecuteCreateOrSelectMaskSettings();
        }

        [MenuItem(MenuRoot + "Build Graph From Selected Authoring Root", priority = -500)]
        private static void BuildGraphFromSelectedAuthoringRoot()
        {
            ExecuteBuildGraphFromSelectedAuthoringRoot();
        }

        [MenuItem(MenuRoot + "Bake Selected Graph -> Derived Data", priority = -500)]
        private static void BakeSelectedGraphDerivedData()
        {
            ExecuteBakeSelectedGraphDerivedData();
        }

        [MenuItem(MenuRoot + "Bake Selected Graph -> Gameplay Masks", priority = -500)]
        private static void BakeSelectedGraphMasks()
        {
            ExecuteBakeSelectedGraphMasks();
        }

        [MenuItem(MenuRoot + "Setup Road Gameplay Service In Active Scene", priority = -500)]
        private static void SetupRoadGameplayServiceInActiveScene()
        {
            ExecuteSetupRoadGameplayServiceInActiveScene();
        }
    }
}
#endif
