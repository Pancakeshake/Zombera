#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Zombera.Editor
{
    /// <summary>
    ///     Custom inspector for <see cref="RoofTypeConfig"/>.
    ///     Hides Gable-specific fields when Style is Flat, and vice versa,
    ///     keeping the inspector clean and relevant.
    /// </summary>
    [CustomEditor(typeof(RoofTypeConfig))]
    public sealed class RoofTypeConfigEditor : UnityEditor.Editor
    {
        // Identity
        private SerializedProperty _displayName;
        private SerializedProperty _style;

        // Gable prefabs
        private SerializedProperty _ridgePrefab;
        private SerializedProperty _panelPrefab;
        private SerializedProperty _gablePrefab;

        // Flat prefabs
        private SerializedProperty _tilePrefab;

        // Gable geometry
        private SerializedProperty _peakHeightMultiplier;
        private SerializedProperty _panelDiagonalNorm;
        private SerializedProperty _panelWidthMultiplier;
        private SerializedProperty _panelZOverhang;
        private SerializedProperty _gableXSnugness;
        private SerializedProperty _gableYBaseHeight;

        // Gable ridge offset
        private SerializedProperty _ridgeYOffset;
        private SerializedProperty _ridgeYScale;
        private SerializedProperty _ridgeZOverhang;

        // Gable panel offset
        private SerializedProperty _panelXOffset;
        private SerializedProperty _panelYOffset;
        private SerializedProperty _panelXPositionMultiplier;
        private SerializedProperty _panelYPositionMultiplier;

        // Gable gable offset
        private SerializedProperty _gableYPositionMultiplier;

        // Flat tile modifiers
        private SerializedProperty _flatRoofYOffset;
        private SerializedProperty _flatRoofYRotation;
        private SerializedProperty _flatRoofZRotation;
        private SerializedProperty _flatRoofXScale;
        private SerializedProperty _flatRoofYScale;
        private SerializedProperty _flatRoofZScale;
        private SerializedProperty _flatRoofUvTiling;
        private SerializedProperty _parapetYScale;
        private SerializedProperty _parapetZScale;
        private SerializedProperty _parapetInset;
        private SerializedProperty _shedLowEdge;
        private SerializedProperty _shedHighScale;
        private SerializedProperty _shedLowScale;
        private SerializedProperty _shedPanelXScale;
        private SerializedProperty _saltboxGablePrefab;
        private SerializedProperty _saltboxRidgeOffset;
        private SerializedProperty _saltboxRidgeTowardPositiveX;
        private SerializedProperty _gambrelGablePrefab;
        private SerializedProperty _gambrelBreakHeight;
        private SerializedProperty _gambrelBreakWidth;
        private SerializedProperty _gambrelRidgeWidth;
        private SerializedProperty _flatRoofGutterXOffset;
        private SerializedProperty _flatRoofGutterYOffset;
        private SerializedProperty _flatRoofGutterZOffset;

        private void OnEnable()
        {
            _displayName = serializedObject.FindProperty("DisplayName");
            _style = serializedObject.FindProperty("Style");

            _ridgePrefab = serializedObject.FindProperty("RidgePrefab");
            _panelPrefab = serializedObject.FindProperty("PanelPrefab");
            _gablePrefab = serializedObject.FindProperty("GablePrefab");

            _tilePrefab = serializedObject.FindProperty("TilePrefab");

            _peakHeightMultiplier = serializedObject.FindProperty("PeakHeightMultiplier");
            _panelDiagonalNorm = serializedObject.FindProperty("PanelDiagonalNorm");
            _panelWidthMultiplier = serializedObject.FindProperty("PanelWidthMultiplier");
            _panelZOverhang = serializedObject.FindProperty("PanelZOverhang");
            _gableXSnugness = serializedObject.FindProperty("GableXSnugness");
            _gableYBaseHeight = serializedObject.FindProperty("GableYBaseHeight");

            _ridgeYOffset = serializedObject.FindProperty("RidgeYOffset");
            _ridgeYScale = serializedObject.FindProperty("RidgeYScale");
            _ridgeZOverhang = serializedObject.FindProperty("RidgeZOverhang");

            _panelXOffset = serializedObject.FindProperty("PanelXOffset");
            _panelYOffset = serializedObject.FindProperty("PanelYOffset");
            _panelXPositionMultiplier = serializedObject.FindProperty("PanelXPositionMultiplier");
            _panelYPositionMultiplier = serializedObject.FindProperty("PanelYPositionMultiplier");

            _gableYPositionMultiplier = serializedObject.FindProperty("GableYPositionMultiplier");

            _flatRoofYOffset = serializedObject.FindProperty("FlatRoofYOffset");
            _flatRoofYRotation = serializedObject.FindProperty("FlatRoofYRotation");
            _flatRoofZRotation = serializedObject.FindProperty("FlatRoofZRotation");
            _flatRoofXScale = serializedObject.FindProperty("FlatRoofXScale");
            _flatRoofYScale = serializedObject.FindProperty("FlatRoofYScale");
            _flatRoofZScale = serializedObject.FindProperty("FlatRoofZScale");
            _flatRoofUvTiling = serializedObject.FindProperty("FlatRoofUvTiling");
            _parapetYScale = serializedObject.FindProperty("ParapetYScale");
            _parapetZScale = serializedObject.FindProperty("ParapetZScale");
            _parapetInset = serializedObject.FindProperty("ParapetInset");
            _shedLowEdge = serializedObject.FindProperty("ShedLowEdge");
            _shedHighScale = serializedObject.FindProperty("ShedHighScale");
            _shedLowScale = serializedObject.FindProperty("ShedLowScale");
            _shedPanelXScale = serializedObject.FindProperty("ShedPanelXScale");
            _saltboxGablePrefab = serializedObject.FindProperty("SaltboxGablePrefab");
            _saltboxRidgeOffset = serializedObject.FindProperty("SaltboxRidgeOffset");
            _saltboxRidgeTowardPositiveX = serializedObject.FindProperty("SaltboxRidgeTowardPositiveX");
            _gambrelGablePrefab = serializedObject.FindProperty("GambrelGablePrefab");
            _gambrelBreakHeight = serializedObject.FindProperty("GambrelBreakHeight");
            _gambrelBreakWidth = serializedObject.FindProperty("GambrelBreakWidth");
            _gambrelRidgeWidth = serializedObject.FindProperty("GambrelRidgeWidth");
            _flatRoofGutterXOffset = serializedObject.FindProperty("FlatRoofGutterXOffset");
            _flatRoofGutterYOffset = serializedObject.FindProperty("FlatRoofGutterYOffset");
            _flatRoofGutterZOffset = serializedObject.FindProperty("FlatRoofGutterZOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ── Identity (always visible) ──
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_style);

            var style = (RoofStyle)_style.enumValueIndex;

            EditorGUILayout.Space(8f);

            switch (style)
            {
                case RoofStyle.Gable:
                    DrawGableFields();
                    break;
                case RoofStyle.Flat:
                    DrawFlatFields();
                    break;
                case RoofStyle.Shed:
                    DrawShedFields();
                    break;
                case RoofStyle.Saltbox:
                    DrawSaltboxFields();
                    break;
                case RoofStyle.Gambrel:
                    DrawGambrelFields();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Gable drawing ──────────────────────────────────────────────

        private void DrawGableFields()
        {
            // Prefabs
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgePrefab);
            EditorGUILayout.PropertyField(_panelPrefab);
            EditorGUILayout.PropertyField(_gablePrefab);

            EditorGUILayout.Space(8f);

            // Geometry
            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_peakHeightMultiplier);
            EditorGUILayout.PropertyField(_panelDiagonalNorm);
            EditorGUILayout.PropertyField(_panelWidthMultiplier);
            EditorGUILayout.PropertyField(_panelZOverhang);

            EditorGUILayout.Space(4f);

            // Gable scaling
            EditorGUILayout.LabelField("Gable Scaling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gableXSnugness);
            EditorGUILayout.PropertyField(_gableYBaseHeight);

            EditorGUILayout.Space(8f);

            // Ridge offset
            EditorGUILayout.LabelField("Ridge Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgeYOffset);
            EditorGUILayout.PropertyField(_ridgeYScale);
            EditorGUILayout.PropertyField(_ridgeZOverhang);

            EditorGUILayout.Space(8f);

            // Panel offset
            EditorGUILayout.LabelField("Panel Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_panelXOffset);
            EditorGUILayout.PropertyField(_panelYOffset);
            EditorGUILayout.PropertyField(_panelXPositionMultiplier);
            EditorGUILayout.PropertyField(_panelYPositionMultiplier);

            EditorGUILayout.Space(8f);

            // Gable offset
            EditorGUILayout.LabelField("Gable Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gableYPositionMultiplier);
        }

        // ── Flat drawing ───────────────────────────────────────────────

        private void DrawFlatFields()
        {
            // Prefabs
            EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tilePrefab);

            EditorGUILayout.Space(8f);

            // Tile modifiers
            EditorGUILayout.LabelField("Tile Modifiers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_flatRoofYOffset);
            EditorGUILayout.PropertyField(_flatRoofYRotation);
            EditorGUILayout.PropertyField(_flatRoofZRotation);
            EditorGUILayout.PropertyField(_flatRoofXScale);
            EditorGUILayout.PropertyField(_flatRoofYScale);
            EditorGUILayout.PropertyField(_flatRoofZScale);
            EditorGUILayout.PropertyField(_flatRoofUvTiling);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Parapet", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_parapetYScale);
            EditorGUILayout.PropertyField(_parapetZScale);
            EditorGUILayout.PropertyField(_parapetInset);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Guttering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_flatRoofGutterXOffset);
            EditorGUILayout.PropertyField(_flatRoofGutterYOffset);
            EditorGUILayout.PropertyField(_flatRoofGutterZOffset);
        }

        // ── Shed drawing ────────────────────────────────────────────────

        private void DrawShedFields()
        {
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_panelPrefab, new GUIContent("Panel Prefab"));
            EditorGUILayout.PropertyField(_gablePrefab, new GUIContent("Gable Prefab"));

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Slope", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_shedLowEdge);
            EditorGUILayout.PropertyField(_shedHighScale);
            EditorGUILayout.PropertyField(_shedLowScale);
            EditorGUILayout.PropertyField(_shedPanelXScale);
        }

        // ── Saltbox drawing ────────────────────────────────────────────

        private void DrawSaltboxFields()
        {
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgePrefab, new GUIContent("Ridge Prefab"));
            EditorGUILayout.PropertyField(_panelPrefab, new GUIContent("Panel Prefab"));
            EditorGUILayout.PropertyField(_saltboxGablePrefab, new GUIContent("Gable Prefab (Right-Angle)"));

            EditorGUILayout.Space(8f);

            // Shared gable geometry
            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_peakHeightMultiplier);
            EditorGUILayout.PropertyField(_panelDiagonalNorm);
            EditorGUILayout.PropertyField(_panelZOverhang);
            EditorGUILayout.PropertyField(_gableYBaseHeight);

            EditorGUILayout.Space(8f);

            // Saltbox-specific
            EditorGUILayout.LabelField("Saltbox Ridge", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_saltboxRidgeOffset);
            EditorGUILayout.PropertyField(_saltboxRidgeTowardPositiveX);

            EditorGUILayout.Space(8f);

            // Ridge offsets
            EditorGUILayout.LabelField("Ridge Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgeYOffset);
            EditorGUILayout.PropertyField(_ridgeYScale);
            EditorGUILayout.PropertyField(_ridgeZOverhang);

            EditorGUILayout.Space(8f);

            // Panel offsets
            EditorGUILayout.LabelField("Panel Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_panelXOffset);
            EditorGUILayout.PropertyField(_panelYOffset);
            EditorGUILayout.PropertyField(_panelXPositionMultiplier);
            EditorGUILayout.PropertyField(_panelYPositionMultiplier);

            EditorGUILayout.Space(8f);

            // Gable offset
            EditorGUILayout.LabelField("Gable Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gableYPositionMultiplier);
        }

        // ── Gambrel drawing ────────────────────────────────────────────

        private void DrawGambrelFields()
        {
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgePrefab, new GUIContent("Ridge Prefab"));
            EditorGUILayout.PropertyField(_panelPrefab, new GUIContent("Panel Prefab (reused for all slopes)"));
            EditorGUILayout.PropertyField(_gambrelGablePrefab, new GUIContent("Gable Prefab (Gambrel)"));

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_peakHeightMultiplier);
            EditorGUILayout.PropertyField(_panelDiagonalNorm);
            EditorGUILayout.PropertyField(_panelZOverhang);
            EditorGUILayout.PropertyField(_gableXSnugness);

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Gambrel Profile", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gambrelBreakHeight, new GUIContent("Break Height",
                "Height of slope change as fraction of peak. 0.45 = break at 45% of ridge."));
            EditorGUILayout.PropertyField(_gambrelBreakWidth, new GUIContent("Break Width",
                "Width at break as fraction of half-building. 0.55 = break at 55% out from ridge."));
            EditorGUILayout.PropertyField(_gambrelRidgeWidth, new GUIContent("Ridge Width",
                "Ridge width as fraction of half-width. 0.10 = narrow ridge."));

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Ridge Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_ridgeYOffset);
            EditorGUILayout.PropertyField(_ridgeYScale);
            EditorGUILayout.PropertyField(_ridgeZOverhang);

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Panel Offset", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_panelXOffset);
            EditorGUILayout.PropertyField(_panelYOffset);
            EditorGUILayout.PropertyField(_panelXPositionMultiplier);
            EditorGUILayout.PropertyField(_panelYPositionMultiplier);
            EditorGUILayout.PropertyField(_panelWidthMultiplier);
        }
    }
}
#endif
