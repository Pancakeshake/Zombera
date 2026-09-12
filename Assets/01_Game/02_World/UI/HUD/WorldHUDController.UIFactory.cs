#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zombera.BuildingSystem;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Random = UnityEngine.Random;

#endregion

namespace Zombera.UI
{
    public sealed partial class WorldHUDController
    {

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }


        private static TextMeshProUGUI MakeText(string name, RectTransform parent, string text, float size)
        {
            var rt = MakeRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.raycastTarget = false;
            return tmp;
        }


        private static Button CreatePopupButton(string name, RectTransform parent, string text)
        {
            var rt = MakeRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.22f, 0.30f, 0.96f);

            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.44f, 0.56f, 0.96f);
            outline.effectDistance = new Vector2(1f, -1f);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = MakeText("Label", rt, text, 13f);
            label.alignment = TextAlignmentOptions.Midline;
            label.color = new Color(0.93f, 0.96f, 0.98f, 0.98f);
            label.fontStyle = FontStyles.Bold;

            return button;
        }


        private sealed class DamagePopupView
        {
            public float Age;
            public Color BaseColor;
            public Transform FollowTarget;
            public float Height;
            public Vector2 Jitter;
            public TextMeshProUGUI Label;
            public float Lifetime;
            public float RisePixels;
            public RectTransform Root;
            public Vector3 WorldAnchor;
        }


        private sealed class BuildItemBoxView
        {
            public int ItemIndex;
            public int SourceItemIndex;
            public Image Background;
            public Button Button;
            public Color DefaultBackgroundColor;
            public Color DefaultLabelColor;
            public string DefaultLabelText;
            public Image Icon;
            public TextMeshProUGUI Label;
        }


        private sealed class BuildCommandButtonView
        {
            public int Index;
            public Button Button;
            public Image Background;
            public TextMeshProUGUI Label;
            public Color DefaultBackgroundColor;
        }
    }
}
