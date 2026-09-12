using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Zombera.BuildingSystem
{
    internal static class EasyBuildBridgeControlsHintHelper
    {
        internal static string ComposeBuildControlsHelpText(object buildingInput, float placementHeightOffset)
        {
            if (buildingInput == null) return null;

            var place = ResolveControlLabel(buildingInput, "m_validateActionRef", "m_validateKey", "LMB");
            var cancel = ResolveControlLabel(buildingInput, "m_cancelActionRef", "m_cancelKey", "RMB");
            var rotate = ResolveControlLabel(buildingInput, "m_rotateActionRef", null, "Mouse Wheel");
            var select = ResolveControlLabel(buildingInput, "m_selectActionRef", null, "Mouse Wheel");

            var sb = new StringBuilder(128);
            sb.Append("Build Controls");
            sb.Append("\nPlace: ").Append(place);
            sb.Append("\nCancel: ").Append(cancel);

            if (!string.Equals(rotate, select, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("\nRotate: ").Append(rotate);
                sb.Append("\nSelect: ").Append(select);
            }
            else
            {
                sb.Append("\nRotate/Select: ").Append(rotate);
            }

            sb.Append("\nHeight Offset: ")
                .Append(placementHeightOffset.ToString("+0.00;-0.00;0.00"))
                .Append("m");
            sb.Append("\nHold Rotate: V");
            sb.Append("\nHeight +/-: +/- or Up/Down");

            return sb.ToString();
        }

        private static string ResolveControlLabel(object buildingInput, string actionRefFieldName, string keyFieldName,
            string fallback)
        {
            var actionRef = !string.IsNullOrWhiteSpace(actionRefFieldName)
                ? EasyBuildBridgeReflectionHelper.GetMemberValue(buildingInput, actionRefFieldName)
                : null;

            var action = actionRef != null
                ? EasyBuildBridgeReflectionHelper.GetMemberValue(actionRef, "action")
                : null;
            var fromAction = ResolveBindingDisplayFromAction(action);
            if (!string.IsNullOrWhiteSpace(fromAction)) return fromAction;

            if (!string.IsNullOrWhiteSpace(keyFieldName))
            {
                var keyObj = EasyBuildBridgeReflectionHelper.GetMemberValue(buildingInput, keyFieldName);
                if (keyObj != null)
                {
                    var keyText = keyObj.ToString();
                    if (!string.IsNullOrWhiteSpace(keyText))
                        return EasyBuildBridgeTextHelper.NormalizeKeyText(keyText);
                }
            }

            return fallback;
        }

        private static string ResolveBindingDisplayFromAction(object action)
        {
            if (action == null) return null;

            var bindingsObj = EasyBuildBridgeReflectionHelper.GetMemberValue(action, "bindings");
            if (bindingsObj is not IEnumerable bindings) return null;

            var labels = new List<string>(4);
            foreach (var binding in bindings)
            {
                if (!ShouldIncludeBindingForKeyboardMouse(binding)) continue;

                var normalized = ExtractBindingLabel(binding);
                if (string.IsNullOrWhiteSpace(normalized)) continue;
                if (labels.Contains(normalized)) continue;

                labels.Add(normalized);
                if (labels.Count >= 3) break;
            }

            if (labels.Count == 0) return null;
            return string.Join(" / ", labels);
        }

        private static bool ShouldIncludeBindingForKeyboardMouse(object binding)
        {
            if (binding == null) return false;

            var isPart = EasyBuildBridgeReflectionHelper.GetMemberValue(binding, "isPartOfComposite") as bool?;
            if (isPart == true) return false;

            var groups = EasyBuildBridgeReflectionHelper.GetMemberValue(binding, "groups") as string;
            if (string.IsNullOrWhiteSpace(groups)) return true;

            return groups.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0
                   || groups.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractBindingLabel(object binding)
        {
            var path = EasyBuildBridgeReflectionHelper.GetMemberValue(binding, "effectivePath") as string
                       ?? EasyBuildBridgeReflectionHelper.GetMemberValue(binding, "path") as string;
            return EasyBuildBridgeTextHelper.NormalizeBindingPath(path);
        }
    }
}
