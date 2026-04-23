using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zombera.Debugging.DebugVisuals
{
    /// <summary>
    /// Visualizes AI state text above world units.
    /// Responsibilities:
    /// - Hold state labels per tracked target
    /// - Update state label position
    /// - Respect debug visibility toggles
    /// </summary>
    public sealed class AIDebugVisualizer : MonoBehaviour, IDebugTool
    {
        [Header("Label Settings")]
        [SerializeField] private TextMeshPro stateLabelPrefab;
        [SerializeField] private Vector3 labelOffset = new Vector3(0f, 2f, 0f);

        private readonly Dictionary<Transform, TextMeshPro> labelsByTarget = new Dictionary<Transform, TextMeshPro>();
        private readonly Dictionary<Transform, string> stateTextByTarget = new Dictionary<Transform, string>();

        public string ToolName => nameof(AIDebugVisualizer);
        public bool IsToolEnabled { get; private set; } = true;

        private void OnEnable()
        {
            DebugManager.Instance?.RegisterDebugTool(this);
        }

        private void OnDisable()
        {
            DebugManager.Instance?.UnregisterDebugTool(this);
            ClearAll();
        }

        public void SetToolEnabled(bool enabled)
        {
            IsToolEnabled = enabled;
            SetLabelsVisible(enabled);
        }

        public void SetAIState(Transform target, string stateText)
        {
            if (target == null)
            {
                return;
            }

            stateTextByTarget[target] = stateText;

            if (!labelsByTarget.ContainsKey(target))
            {
                labelsByTarget[target] = CreateLabelInstance();
            }
        }

        public void RemoveTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            stateTextByTarget.Remove(target);

            if (labelsByTarget.TryGetValue(target, out TextMeshPro label))
            {
                if (label != null)
                {
                    Destroy(label.gameObject);
                }

                labelsByTarget.Remove(target);
            }
        }

        public void ClearAll()
        {
            foreach (KeyValuePair<Transform, TextMeshPro> entry in labelsByTarget)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            labelsByTarget.Clear();
            stateTextByTarget.Clear();
        }

        private void LateUpdate()
        {
            if (!IsToolEnabled)
            {
                return;
            }

            bool showAIStates = DebugManager.Instance == null || DebugManager.Instance.Settings == null || DebugManager.Instance.Settings.showAIStates;

            foreach (KeyValuePair<Transform, TextMeshPro> entry in labelsByTarget)
            {
                Transform target = entry.Key;
                TextMeshPro label = entry.Value;

                if (target == null || label == null)
                {
                    continue;
                }

                label.gameObject.SetActive(showAIStates);
                label.transform.position = target.position + labelOffset;
                label.text = stateTextByTarget.TryGetValue(target, out string stateText) ? stateText : "[UNKNOWN]";
            }
        }

        private TextMeshPro CreateLabelInstance()
        {
            if (stateLabelPrefab != null)
            {
                return Instantiate(stateLabelPrefab, transform);
            }

            GameObject root = new GameObject("AIStateLabel");
            root.transform.SetParent(transform, false);
            TextMeshPro label = root.AddComponent<TextMeshPro>();
            label.fontSize = 3f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "[STATE]";
            return label;
        }

        private void SetLabelsVisible(bool visible)
        {
            foreach (KeyValuePair<Transform, TextMeshPro> entry in labelsByTarget)
            {
                if (entry.Value != null)
                {
                    entry.Value.gameObject.SetActive(visible);
                }
            }
        }

        // Label pool — inactive labels are returned here and reused to avoid Instantiate spikes.
        private readonly System.Collections.Generic.Stack<TextMeshPro> labelPool
            = new System.Collections.Generic.Stack<TextMeshPro>();

        private TextMeshPro AcquireLabel(Transform target)
        {
            TextMeshPro label = labelPool.Count > 0 ? labelPool.Pop() : null;

            if (label == null)
            {
                label = CreateLabelInstance();
            }
            else
            {
                label.gameObject.SetActive(true);
                label.transform.SetParent(target, false);
                label.transform.localPosition = Vector3.up * 2.2f;
            }

            return label;
        }

        private void ReturnLabel(Transform target)
        {
            if (labelsByTarget.TryGetValue(target, out TextMeshPro label) && label != null)
            {
                label.gameObject.SetActive(false);
                labelPool.Push(label);
                labelsByTarget.Remove(target);
            }
        }

        [SerializeField] private Color zombieColor = Color.red;
        [SerializeField] private Color squadColor = Color.cyan;
        [SerializeField] private Color playerColor = Color.green;
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float cullingDistance = 40f;
#pragma warning restore CS0414

        private Color GetFactionColor(Zombera.Characters.UnitRole role)
        {
            switch (role)
            {
                case Zombera.Characters.UnitRole.Zombie:
                    return zombieColor;
                case Zombera.Characters.UnitRole.SquadMember:
                    return squadColor;
                case Zombera.Characters.UnitRole.Player:
                    return playerColor;
                default:
                    return Color.white;
            }
        }
    }
}