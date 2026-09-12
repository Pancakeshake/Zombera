#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Zombera.World.Roads;

namespace Zombera.Editor
{
    [CustomEditor(typeof(ProceduralRoadSystem))]
    internal sealed class ProceduralRoadSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var roadSystem = (ProceduralRoadSystem)target;
            if (!Application.isPlaying)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    "Editor road preview is controlled from MapMagic Pinned Tile Road Authoring on the MapMagicObject.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("Play mode uses tile-stream events.", MessageType.Info);
            }
        }
    }
}
#endif
