using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Zombera.BuildingSystem
{
    internal readonly struct HeightKeyConfig
    {
        internal readonly Key HeightUpPrimaryKey;
        internal readonly Key HeightUpAlternateKey;
        internal readonly Key HeightUpNumpadKey;
        internal readonly Key HeightDownPrimaryKey;
        internal readonly Key HeightDownAlternateKey;
        internal readonly Key HeightDownNumpadKey;
        internal readonly float RealtimeHeightStep;
        internal readonly float RealtimeHeightRepeatSeconds;

        internal HeightKeyConfig(
            Key heightUpPrimaryKey,
            Key heightUpAlternateKey,
            Key heightUpNumpadKey,
            Key heightDownPrimaryKey,
            Key heightDownAlternateKey,
            Key heightDownNumpadKey,
            float realtimeHeightStep,
            float realtimeHeightRepeatSeconds)
        {
            HeightUpPrimaryKey = heightUpPrimaryKey;
            HeightUpAlternateKey = heightUpAlternateKey;
            HeightUpNumpadKey = heightUpNumpadKey;
            HeightDownPrimaryKey = heightDownPrimaryKey;
            HeightDownAlternateKey = heightDownAlternateKey;
            HeightDownNumpadKey = heightDownNumpadKey;
            RealtimeHeightStep = realtimeHeightStep;
            RealtimeHeightRepeatSeconds = realtimeHeightRepeatSeconds;
        }
    }

    internal static class EasyBuildBridgeShortcutHelper
    {
        internal static float ResolveNextRotateRepeatAt(
            bool enableHoldRotatePreview,
            Keyboard keyboard,
            Key holdRotatePreviewKey,
            float holdRotateRepeatSeconds,
            float currentRepeatAt,
            float now,
            Func<bool> tryRotate)
        {
            if (!enableHoldRotatePreview) return currentRepeatAt;

            if (!IsKeyPressed(keyboard, holdRotatePreviewKey))
                return 0f;

            if (now < currentRepeatAt)
                return currentRepeatAt;

            var rotateRepeat = Mathf.Max(0.02f, holdRotateRepeatSeconds);
            var rotated = tryRotate != null && tryRotate();
            return now + (rotated ? rotateRepeat : 0.12f);
        }

        internal static float ResolveNextHeightRepeatAt(
            bool enableRealtimeHeightHotkeys,
            Keyboard keyboard,
            in HeightKeyConfig keyCfg,
            float currentRepeatAt,
            float now,
            Func<float, bool> tryAdjustHeight)
        {
            if (!enableRealtimeHeightHotkeys) return currentRepeatAt;

            var direction = ResolveHeightAdjustmentDirection(keyboard, keyCfg);

            if (direction == 0)
                return 0f;

            if (now < currentRepeatAt)
                return currentRepeatAt;

            var step = Mathf.Max(0.01f, keyCfg.RealtimeHeightStep) * direction;
            var adjusted = tryAdjustHeight != null && tryAdjustHeight(step);
            var heightRepeat = Mathf.Max(0.02f, keyCfg.RealtimeHeightRepeatSeconds);
            return now + (adjusted ? heightRepeat : 0.12f);
        }

        internal static int ResolveHeightAdjustmentDirection(Keyboard keyboard, in HeightKeyConfig keyCfg)
        {
            var direction = 0;

            if (IsKeyPressed(keyboard, keyCfg.HeightUpPrimaryKey)
                || IsKeyPressed(keyboard, keyCfg.HeightUpAlternateKey)
                || IsKeyPressed(keyboard, keyCfg.HeightUpNumpadKey))
                direction += 1;

            if (IsKeyPressed(keyboard, keyCfg.HeightDownPrimaryKey)
                || IsKeyPressed(keyboard, keyCfg.HeightDownAlternateKey)
                || IsKeyPressed(keyboard, keyCfg.HeightDownNumpadKey))
                direction -= 1;

            return direction;
        }

        internal static bool IsKeyPressed(Keyboard keyboard, Key key)
        {
            if (keyboard == null || key == Key.None) return false;

            return keyboard[key].isPressed;
        }
    }
}
