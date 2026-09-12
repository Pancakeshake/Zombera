using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
    static GameObject BuildMissionsPanel(Transform parent)
    {
        var panel = MakePanel(parent, "MissionsPanel");
        BuildPanelHeader(panel.transform, "MISSIONS & OBJECTIVES");

        var scrollGO = MakeRect("MissionScroll", panel.transform);
        var scrollRT = RT(scrollGO);
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(8f, 8f);
        scrollRT.offsetMax = new Vector2(-8f, -44f);

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = MakeRect("Viewport", scrollGO.transform);
        FillParent(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);

        var content = MakeRect("Content", viewport.transform);
        var cRT = RT(content);
        cRT.anchorMin = new Vector2(0f, 1f);
        cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot = new Vector2(0.5f, 1f);
        cRT.offsetMin = Vector2.zero;
        cRT.offsetMax = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(4, 4, 4, 4);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = cRT;

        BuildMissionRow(content.transform, "Secure the Outpost", "Build 5 walls around Outpost Alpha", 0.40f, false);
        BuildMissionRow(content.transform, "Gather Supplies", "Scavenge 20 food items", 0.65f, false);
        BuildMissionRow(content.transform, "Survivor Escort", "Guide Smith family to base camp", 0f, false);
        BuildMissionRow(content.transform, "Clear the Highway", "Eliminate 30 zombies on Route 9", 0.12f, false);

        return panel;
    }

    static void BuildMissionRow(Transform parent, string title, string desc, float prog, bool done)
    {
        var row = MakeImage($"Mission_{parent.childCount}", parent, new Color(0.10f, 0.11f, 0.13f, 1f));
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = 68f;
        rowLE.preferredHeight = 68f;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(8, 8, 6, 6);

        var iconGO = MakeImage("Status", row.transform, done ? C_Accent : C_Btn);
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.minWidth = 36f;
        iconLE.preferredWidth = 36f;
        iconLE.minHeight = 36f;
        iconLE.preferredHeight = 36f;
        iconLE.flexibleWidth = 0f;
        var iconLblGO = MakeRect("Label", iconGO.transform);
        FillParent(RT(iconLblGO));
        var iconTMP = iconLblGO.AddComponent<TextMeshProUGUI>();
        iconTMP.text = done ? "✓" : "!";
        iconTMP.fontSize = 18f;
        iconTMP.color = done ? C_Text : new Color(0.95f, 0.75f, 0.20f, 1f);
        iconTMP.alignment = TextAlignmentOptions.Center;
        iconTMP.raycastTarget = false;

        var info = MakeRect("Info", row.transform);
        info.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var infoVLG = info.AddComponent<VerticalLayoutGroup>();
        infoVLG.spacing = 3f;
        infoVLG.childForceExpandWidth = true;
        infoVLG.childForceExpandHeight = false;

        var titleTMP = MakeRect("Title", info.transform).AddComponent<TextMeshProUGUI>();
        titleTMP.text = title;
        titleTMP.fontSize = 13f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = done ? C_TextDim : C_Text;
        titleTMP.raycastTarget = false;

        var descTMP = MakeRect("Desc", info.transform).AddComponent<TextMeshProUGUI>();
        descTMP.text = desc;
        descTMP.fontSize = 11f;
        descTMP.color = C_TextDim;
        descTMP.raycastTarget = false;

        var progBg = MakeImage("ProgressBG", info.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
        progBg.AddComponent<LayoutElement>().minHeight = 6f;
        var spr = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var progFill = MakeImage("ProgressFill", progBg.transform, done ? C_Accent : C_Stam);
        var progImg = progFill.GetComponent<Image>();
        if (spr != null) progImg.sprite = spr;
        progImg.type = Image.Type.Filled;
        progImg.fillMethod = Image.FillMethod.Horizontal;
        progImg.fillAmount = done ? 1f : prog;
        FillParent(RT(progFill));
    }

    // ── Placeholder Panel ─────────────────────────────────────────────────

    static GameObject MakePlaceholder(Transform parent, string name, string title, string body)
    {
        var panel = MakePanel(parent, name);
        BuildPanelHeader(panel.transform, title);

        var txt = MakeRect("PlaceholderText", panel.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(txt.gameObject));
        txt.text = body;
        txt.fontSize = 22f;
        txt.color = C_TextDim;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        return panel;
    }
    }
}
