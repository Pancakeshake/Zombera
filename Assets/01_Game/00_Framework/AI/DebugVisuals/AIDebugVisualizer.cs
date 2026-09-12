#region

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zombera.Debugging;

#endregion

namespace Zombera.Debugging.DebugVisuals
{
    // ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
    /// <summary>
    ///     Visualizes AI state text above world units.
    ///     Responsibilities:
    ///     - Hold state labels per tracked target
    ///     - Update state label position
    ///     - Respect debug visibility toggles
    /// </summary>
    public sealed class AIDebugVisualizer : MonoBehaviour, IDebugTool
    {
        [Header("Label Settings")] [SerializeField]
        private TextMeshPro stateLabelPrefab;

        [SerializeField] private Vector3 labelOffset = new(0f, 2f, 0f);

        private readonly Dictionary<Transform, TextMeshPro> _labelsByTarget = new();
        private readonly Dictionary<Transform, string> _stateTextByTarget = new();

        private void LateUpdate()
        {
            if (!IsToolEnabled) return;

            var accessor = DebugManagerAccessor.Instance;
            var showAIStates = accessor == null || accessor.Settings == null || accessor.Settings.showAIStates;

            foreach (var (target, label) in _labelsByTarget)
            {
                if (target == null || label == null) continue;

                label.gameObject.SetActive(showAIStates);
                label.transform.position = target.position + labelOffset;
                label.text = _stateTextByTarget.GetValueOrDefault(target, "[UNKNOWN]");
            }
        }

        private void OnEnable()
        {
            DebugManagerAccessor.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManagerAccessor.Instance?.UnregisterDebugTool(this);
            ClearAll();
        }

        public string ToolName => nameof(AIDebugVisualizer);
        public bool IsToolEnabled { get; private set; } = true;

        public void SetToolEnabled(bool isEnabled)
        {
            IsToolEnabled = isEnabled;
            SetLabelsVisible(isEnabled);
        }

        public void SetAIState(Transform target, string stateText)
        {
            if (target == null) return;

            _stateTextByTarget[target] = stateText;

            if (!_labelsByTarget.ContainsKey(target)) _labelsByTarget[target] = CreateLabelInstance();
        }

        public void RemoveTarget(Transform target)
        {
            if (target == null) return;

            _stateTextByTarget.Remove(target);

            if (!_labelsByTarget.TryGetValue(target, out var label)) return;

            if (label != null) Destroy(label.gameObject);

            _labelsByTarget.Remove(target);
        }

        public void ClearAll()
        {
            foreach (var (_, label) in _labelsByTarget)
                if (label != null)
                    Destroy(label.gameObject);

            _labelsByTarget.Clear();
            _stateTextByTarget.Clear();
        }

        private TextMeshPro CreateLabelInstance()
        {
            if (stateLabelPrefab != null) return Instantiate(stateLabelPrefab, transform);

            var root = new GameObject("AIStateLabel");
            root.transform.SetParent(transform, false);
            var label = root.AddComponent<TextMeshPro>();
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "[STATE]";
            return label;
        }

        private void SetLabelsVisible(bool visible)
        {
            foreach (var (_, label) in _labelsByTarget)
                if (label != null)
                    label.gameObject.SetActive(visible);
        }
    }
}