using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
    static GameObject MakePanel(Transform parent, string name)
    {
        var p = MakeImage(name, parent, C_Panel);
        FillParent(RT(p));
        p.AddComponent<CanvasGroup>();
        return p;
    }

    static void BuildPanelHeader(Transform parent, string title)
    {
        var hdr = MakeImage("PanelHeader", parent, new Color(0.05f, 0.05f, 0.07f, 1f));
        var rt = RT(hdr);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 42f);

        var txt = MakeRect("Title", hdr.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(txt.gameObject));
        RT(txt.gameObject).offsetMin = new Vector2(14f, 0f);
        txt.text = title;
        txt.fontSize = 20f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = C_Text;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.raycastTarget = false;
    }

    // ── Button factories ──────────────────────────────────────────────────

    static Button MakeTab(Transform parent, string name, string label, float width)
    {
        var go = MakeImage(name, parent, C_Tab);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        le.minHeight = 100f;
        le.preferredHeight = 100f;
        le.flexibleWidth = 0f;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();

        var txtGO = MakeRect("Label", go.transform);
        FillParent(RT(txtGO));
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = C_Text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    static Button MakeTimeBtn(Transform parent, string name, string label)
    {
        var go = MakeImage(name, parent, C_Btn);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = TBTN;
        le.preferredWidth = TBTN;
        le.minHeight = 100f;
        le.preferredHeight = 100f;
        le.flexibleWidth = 0f;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();

        var txtGO = MakeRect("Label", go.transform);
        FillParent(RT(txtGO));
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 40f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = C_Text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return btn;
    }

    // ── Primitive helpers ─────────────────────────────────────────────────

    static GameObject MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        return go;
    }

    static GameObject MakeImage(string name, Transform parent, Color color)
    {
        var go = MakeRect(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static RectTransform RT(GameObject go)
    {
        return go.GetComponent<RectTransform>();
    }

    static void FillParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AnchorTop(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    static void AnchorBottom(RectTransform rt, float height)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }
    }
}
