using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zombera.UI;

namespace Zombera.Editor
{
    public static partial class WorldHudSetupTool
    {
        private enum EquipmentSlotVisual
        {
            Head,
            Face,
            Chest,
            Back,
            LeftHand,
            RightHand,
            Belt,
            Legs,
            Feet
        }

    // ── Inventory Panel ───────────────────────────────────────────────────

    static GameObject BuildInventoryPanel(Transform parent)
    {
        var panel = MakePanel(parent, "InventoryPanel");
        BuildPanelHeader(panel.transform, "INVENTORY");

        var panelHeader = panel.transform.Find("PanelHeader") as RectTransform;
        if (panelHeader != null) panelHeader.gameObject.SetActive(false);

        var panelImage = panel.GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color(0.06f, 0.06f, 0.08f, 1f);

        var panelTitle = panel.transform.Find("PanelHeader/Title")?.GetComponent<TextMeshProUGUI>();
        if (panelTitle != null) panelTitle.gameObject.SetActive(false);
        SquadPortraitStrip headerStrip;

        // Inventory grid area (left ~62 %)
        var gridArea = MakeImage("InventoryGrid", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
        var gridAreaRT = RT(gridArea);
        gridAreaRT.anchorMin = new Vector2(0f, 0f);
        gridAreaRT.anchorMax = new Vector2(0.62f, 1f);
        gridAreaRT.offsetMin = new Vector2(4f, 4f);
        gridAreaRT.offsetMax = new Vector2(-3f, -8f);

        var gridScroll = gridArea.AddComponent<ScrollRect>();
        gridScroll.horizontal = false;
        gridScroll.vertical = true;
        gridScroll.scrollSensitivity = 24f;
        gridScroll.movementType = ScrollRect.MovementType.Clamped;

        var controls = MakeRect("InventoryControls", gridArea.transform);
        var controlsRT = RT(controls);
        controlsRT.anchorMin = new Vector2(0f, 1f);
        controlsRT.anchorMax = new Vector2(1f, 1f);
        controlsRT.pivot = new Vector2(0.5f, 1f);
        controlsRT.offsetMin = new Vector2(6f, -208f);
        controlsRT.offsetMax = new Vector2(-6f, -164f);
        var controlsHLG = controls.AddComponent<HorizontalLayoutGroup>();
        controlsHLG.spacing = 4f;
        controlsHLG.childAlignment = TextAnchor.MiddleLeft;
        controlsHLG.childControlWidth = true;
        controlsHLG.childControlHeight = true;
        controlsHLG.childForceExpandWidth = false;
        controlsHLG.childForceExpandHeight = true;

        var searchGO = MakeImage("SearchBar", controls.transform, new Color(0.10f, 0.11f, 0.14f, 1f));
        var searchLE = searchGO.AddComponent<LayoutElement>();
        searchLE.flexibleWidth = 2f;
        searchLE.minHeight = 42f;
        searchLE.preferredHeight = 42f;
        var searchOutline = searchGO.AddComponent<Outline>();
        searchOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
        searchOutline.effectDistance = new Vector2(1f, -1f);
        var searchInput = searchGO.AddComponent<TMP_InputField>();

        var searchViewport = MakeRect("TextViewport", searchGO.transform);
        var searchViewportRT = RT(searchViewport);
        FillParent(searchViewportRT);
        searchViewportRT.offsetMin = new Vector2(10f, 6f);
        searchViewportRT.offsetMax = new Vector2(-10f, -6f);
        searchViewport.AddComponent<RectMask2D>();

        var searchPlaceholder = MakeRect("Placeholder", searchViewport.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(searchPlaceholder.gameObject));
        searchPlaceholder.text = "Search items...";
        searchPlaceholder.fontSize = 14f;
        searchPlaceholder.color = new Color(0.50f, 0.48f, 0.42f, 0.78f);
        searchPlaceholder.alignment = TextAlignmentOptions.MidlineLeft;
        searchPlaceholder.raycastTarget = false;

        var searchText = MakeRect("Text", searchViewport.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(searchText.gameObject));
        searchText.text = string.Empty;
        searchText.fontSize = 14f;
        searchText.color = C_Text;
        searchText.alignment = TextAlignmentOptions.MidlineLeft;
        searchText.raycastTarget = false;

        searchInput.textViewport = searchViewportRT;
        searchInput.textComponent = searchText;
        searchInput.placeholder = searchPlaceholder;
        searchInput.lineType = TMP_InputField.LineType.SingleLine;

        var filterGO = MakeImage("FilterButton", controls.transform, C_Btn);
        var filterLE = filterGO.AddComponent<LayoutElement>();
        filterLE.minWidth = 132f;
        filterLE.preferredWidth = 132f;
        filterLE.minHeight = 42f;
        filterLE.preferredHeight = 42f;
        var filterBtn = filterGO.AddComponent<Button>();
        filterBtn.targetGraphic = filterGO.GetComponent<Image>();
        var filterLabel = MakeRect("Label", filterGO.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(filterLabel.gameObject));
        filterLabel.text = "FILTER: ALL";
        filterLabel.fontSize = 14f;
        filterLabel.fontStyle = FontStyles.Bold;
        filterLabel.color = C_Text;
        filterLabel.alignment = TextAlignmentOptions.Center;
        filterLabel.raycastTarget = false;

        var selectorRow = MakeRect("HeaderCharacterSelector", gridArea.transform).transform;
        var selectorRowRT = RT(selectorRow.gameObject);
        selectorRowRT.anchorMin = new Vector2(0f, 1f);
        selectorRowRT.anchorMax = new Vector2(1f, 1f);
        selectorRowRT.pivot = new Vector2(0.5f, 1f);
        selectorRowRT.offsetMin = new Vector2(6f, -160f);
        selectorRowRT.offsetMax = new Vector2(-6f, 0f);

        var selectorHLG = selectorRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        selectorHLG.spacing = 4f;
        selectorHLG.childAlignment = TextAnchor.MiddleLeft;
        selectorHLG.childControlWidth = true;
        selectorHLG.childControlHeight = true;
        selectorHLG.childForceExpandWidth = false;
        selectorHLG.childForceExpandHeight = false;
        selectorHLG.padding = new RectOffset(0, 0, 0, 0);

        for (var i = 0; i < 6; i++) BuildInventoryMemberSlot(selectorRow, i, i == 0, 150f, null, true);

        headerStrip = selectorRow.gameObject.AddComponent<SquadPortraitStrip>();

        var squadBtnGO = MakeImage("OpenSquadRosterButton", gridArea.transform, C_Btn);
        var squadBtnRT = RT(squadBtnGO);
        squadBtnRT.anchorMin = new Vector2(0f, 1f);
        squadBtnRT.anchorMax = new Vector2(0f, 1f);
        squadBtnRT.pivot = new Vector2(0f, 1f);
        squadBtnRT.anchoredPosition = new Vector2(6f, -214f);
        squadBtnRT.sizeDelta = new Vector2(220f, 30f);
        var squadBtn = squadBtnGO.AddComponent<Button>();
        squadBtn.targetGraphic = squadBtnGO.GetComponent<Image>();
        var squadBtnLabel = MakeRect("Label", squadBtnGO.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(squadBtnLabel.gameObject));
        squadBtnLabel.text = "SQUAD ROSTER";
        squadBtnLabel.fontSize = 12f;
        squadBtnLabel.fontStyle = FontStyles.Bold;
        squadBtnLabel.color = C_Text;
        squadBtnLabel.alignment = TextAlignmentOptions.Center;
        squadBtnLabel.raycastTarget = false;

        var editingLabel = MakeRect("EditingUnitLabel", gridArea.transform).AddComponent<TextMeshProUGUI>();
        var editingLabelRT = RT(editingLabel.gameObject);
        editingLabelRT.anchorMin = new Vector2(0f, 1f);
        editingLabelRT.anchorMax = new Vector2(1f, 1f);
        editingLabelRT.pivot = new Vector2(0.5f, 1f);
        editingLabelRT.offsetMin = new Vector2(236f, -244f);
        editingLabelRT.offsetMax = new Vector2(-6f, -214f);
        editingLabel.text = "Editing: None";
        editingLabel.fontSize = 12f;
        editingLabel.fontStyle = FontStyles.Bold;
        editingLabel.color = C_TextDim;
        editingLabel.alignment = TextAlignmentOptions.MidlineLeft;
        editingLabel.raycastTarget = false;

        const float invGap = 4f;
        const int visibleRows = 5;
        const float viewportTopInset = 252f;

        var viewport = MakeRect("Viewport", gridArea.transform);
        var viewportRT = RT(viewport);
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(6f, 6f);
        viewportRT.offsetMax = new Vector2(-6f, -viewportTopInset);
        viewport.AddComponent<RectMask2D>();
        gridScroll.viewport = viewportRT;

        var slotGrid = MakeRect("SlotGrid", viewport.transform);
        var slotRT = RT(slotGrid);
        slotRT.anchorMin = new Vector2(0f, 1f);
        slotRT.anchorMax = new Vector2(1f, 1f);
        slotRT.pivot = new Vector2(0.5f, 1f);
        slotRT.offsetMin = Vector2.zero;
        slotRT.offsetMax = Vector2.zero;

        var glg = slotGrid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(40f, 40f);
        glg.spacing = new Vector2(invGap, invGap);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 10;
        glg.childAlignment = TextAnchor.UpperLeft;
        glg.padding = new RectOffset(0, 0, 0, 0);

        gridScroll.content = slotRT;

        for (var i = 0; i < 300; i++)
        {
            var s = MakeImage($"InvSlot_{i}", slotGrid.transform, new Color(0.11f, 0.11f, 0.14f, 1f));
            s.AddComponent<Button>().targetGraphic = s.GetComponent<Image>();
        }

        var gridFitter = slotGrid.AddComponent<InventoryGridViewportFitter>();
        gridFitter.Configure(viewportRT, slotRT, glg, 10, visibleRows);

        var squadModal = MakeImage("SquadRosterModal", panel.transform, new Color(0f, 0f, 0f, 0.72f));
        FillParent(RT(squadModal));
        var modalBackdrop = squadModal.AddComponent<Button>();
        modalBackdrop.targetGraphic = squadModal.GetComponent<Image>();

        var modalCard = MakeImage("Card", squadModal.transform, new Color(0.07f, 0.08f, 0.11f, 0.98f));
        var modalCardRT = RT(modalCard);
        modalCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        modalCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        modalCardRT.pivot = new Vector2(0.5f, 0.5f);
        modalCardRT.sizeDelta = new Vector2(640f, 360f);
        var modalOutline = modalCard.AddComponent<Outline>();
        modalOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
        modalOutline.effectDistance = new Vector2(1f, -1f);

        var modalHdr = MakeRect("Header", modalCard.transform).AddComponent<TextMeshProUGUI>();
        var modalHdrRT = RT(modalHdr.gameObject);
        modalHdrRT.anchorMin = new Vector2(0f, 1f);
        modalHdrRT.anchorMax = Vector2.one;
        modalHdrRT.pivot = new Vector2(0.5f, 1f);
        modalHdrRT.offsetMin = new Vector2(14f, -40f);
        modalHdrRT.offsetMax = new Vector2(-52f, -8f);
        modalHdr.text = "SQUAD CHARACTERS";
        modalHdr.fontSize = 18f;
        modalHdr.fontStyle = FontStyles.Bold;
        modalHdr.color = C_Text;
        modalHdr.alignment = TextAlignmentOptions.MidlineLeft;
        modalHdr.raycastTarget = false;

        var closeBtnGO = MakeImage("CloseButton", modalCard.transform, C_Btn);
        var closeBtnRT = RT(closeBtnGO);
        closeBtnRT.anchorMin = new Vector2(1f, 1f);
        closeBtnRT.anchorMax = new Vector2(1f, 1f);
        closeBtnRT.pivot = new Vector2(1f, 1f);
        closeBtnRT.anchoredPosition = new Vector2(-10f, -10f);
        closeBtnRT.sizeDelta = new Vector2(34f, 30f);
        var closeBtn = closeBtnGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeBtnGO.GetComponent<Image>();
        var closeLbl = MakeRect("Label", closeBtnGO.transform).AddComponent<TextMeshProUGUI>();
        FillParent(RT(closeLbl.gameObject));
        closeLbl.text = "X";
        closeLbl.fontSize = 16f;
        closeLbl.fontStyle = FontStyles.Bold;
        closeLbl.color = C_Text;
        closeLbl.alignment = TextAlignmentOptions.Center;
        closeLbl.raycastTarget = false;

        var modalGrid = MakeRect("RosterGrid", modalCard.transform);
        var modalGridRT = RT(modalGrid);
        modalGridRT.anchorMin = new Vector2(0.5f, 0.5f);
        modalGridRT.anchorMax = new Vector2(0.5f, 0.5f);
        modalGridRT.pivot = new Vector2(0.5f, 0.5f);
        modalGridRT.anchoredPosition = new Vector2(0f, -8f);
        modalGridRT.sizeDelta = new Vector2(560f, 250f);
        var modalGLG = modalGrid.AddComponent<GridLayoutGroup>();
        modalGLG.cellSize = new Vector2(168f, 110f);
        modalGLG.spacing = new Vector2(12f, 12f);
        modalGLG.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        modalGLG.constraintCount = 3;
        modalGLG.childAlignment = TextAnchor.MiddleCenter;

        for (var i = 0; i < 6; i++) BuildInventoryMemberSlot(modalGrid.transform, i, i == 0, 110f, "Loading...", true);

        var modalStrip = modalGrid.AddComponent<SquadPortraitStrip>();

        squadModal.SetActive(false);

        var modalCtrl = panel.AddComponent<SquadRosterModalController>();
        var modalSO = new SerializedObject(modalCtrl);
        modalSO.FindProperty("modalRoot").objectReferenceValue = squadModal;
        modalSO.FindProperty("openButton").objectReferenceValue = squadBtn;
        modalSO.FindProperty("closeButton").objectReferenceValue = closeBtn;
        modalSO.FindProperty("backdropButton").objectReferenceValue = modalBackdrop;
        modalSO.FindProperty("modalPortraitStrip").objectReferenceValue = modalStrip;
        modalSO.ApplyModifiedPropertiesWithoutUndo();

        var editCtrl = panel.AddComponent<InventoryEditTargetController>();
        var editSO = new SerializedObject(editCtrl);
        editSO.FindProperty("headerPortraitStrip").objectReferenceValue = headerStrip;
        editSO.FindProperty("modalPortraitStrip").objectReferenceValue = modalStrip;
        editSO.FindProperty("editingUnitLabel").objectReferenceValue = editingLabel;
        editSO.ApplyModifiedPropertiesWithoutUndo();

        var inventoryCtrl = panel.AddComponent<InventoryPanelController>();
        var inventorySO = new SerializedObject(inventoryCtrl);
        inventorySO.FindProperty("editTargetController").objectReferenceValue = editCtrl;
        inventorySO.FindProperty("searchInputField").objectReferenceValue = searchInput;
        inventorySO.FindProperty("filterButton").objectReferenceValue = filterBtn;
        inventorySO.FindProperty("filterLabel").objectReferenceValue = filterLabel;
        inventorySO.FindProperty("slotGrid").objectReferenceValue = slotGrid.transform;
        inventorySO.ApplyModifiedPropertiesWithoutUndo();

        // Equipment panel (right ~38 %)
        var equipGO = MakeImage("EquipmentPanel", panel.transform, new Color(0.06f, 0.06f, 0.08f, 1f));
        var equipRT = RT(equipGO);
        equipRT.anchorMin = new Vector2(0.62f, 0f);
        equipRT.anchorMax = Vector2.one;
        equipRT.offsetMin = new Vector2(3f, 4f);
        equipRT.offsetMax = new Vector2(-4f, -96f);
        BuildEquipmentSlots(equipGO.transform);

        squadModal.transform.SetAsLastSibling();

        return panel;
    }

    static void BuildInventoryMemberSlot(
        Transform parent,
        int idx,
        bool selected,
        float slotSize = 58f,
        string label = null,
        bool bindToSquadSlot = false,
        float nameFontSizeOverride = 0f)
    {
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var slotGO = MakeImage($"MemberPort_{idx}", parent,
            selected ? new Color(0.15f, 0.30f, 0.20f, 1f) : C_Port);
        var slotRT = RT(slotGO);
        slotRT.sizeDelta = new Vector2(slotSize, slotSize);

        var le = slotGO.AddComponent<LayoutElement>();
        le.minWidth = slotSize;
        le.preferredWidth = slotSize;
        le.minHeight = slotSize;
        le.preferredHeight = slotSize;
        le.flexibleWidth = 0f;

        var outline = slotGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = slotGO.GetComponent<Image>();

        var portGO = MakeImage("PortraitImage", slotGO.transform, new Color(0.20f, 0.20f, 0.25f, 1f));
        var portRT = RT(portGO);
        portRT.anchorMin = new Vector2(0f, 0.24f);
        portRT.anchorMax = new Vector2(1f, 0.76f);
        portRT.offsetMin = new Vector2(2f, 2f);
        portRT.offsetMax = new Vector2(-2f, -2f);

        var hpBgGO = MakeImage("HPBarBG", slotGO.transform, C_HPBg);
        var hpBgRT = RT(hpBgGO);
        hpBgRT.anchorMin = new Vector2(0f, 0.14f);
        hpBgRT.anchorMax = new Vector2(1f, 0.22f);
        hpBgRT.offsetMin = new Vector2(2f, 0f);
        hpBgRT.offsetMax = new Vector2(-2f, 0f);

        var hpFillGO = MakeImage("HPFill", hpBgGO.transform, C_HP);
        var hpImg = hpFillGO.GetComponent<Image>();
        if (uiSprite != null) hpImg.sprite = uiSprite;
        hpImg.type = Image.Type.Filled;
        hpImg.fillMethod = Image.FillMethod.Horizontal;
        hpImg.fillAmount = 1f;
        FillParent(RT(hpFillGO));

        var stBgGO = MakeImage("StaminaBarBG", slotGO.transform, C_StamBg);
        var stBgRT = RT(stBgGO);
        stBgRT.anchorMin = new Vector2(0f, 0.04f);
        stBgRT.anchorMax = new Vector2(1f, 0.12f);
        stBgRT.offsetMin = new Vector2(2f, 0f);
        stBgRT.offsetMax = new Vector2(-2f, 0f);

        var stFillGO = MakeImage("StaminaFill", stBgGO.transform, C_Stam);
        var stImg = stFillGO.GetComponent<Image>();
        if (uiSprite != null) stImg.sprite = uiSprite;
        stImg.type = Image.Type.Filled;
        stImg.fillMethod = Image.FillMethod.Horizontal;
        stImg.fillAmount = 1f;
        FillParent(RT(stFillGO));

        var selGO = MakeImage("SelectOverlay", slotGO.transform, new Color(0.22f, 0.58f, 0.40f, 0.45f));
        FillParent(RT(selGO));
        selGO.GetComponent<Image>().raycastTarget = false;
        selGO.SetActive(selected);

        var namePlateGO = MakeImage("NamePlate", slotGO.transform, new Color(0f, 0f, 0f, 0.65f));
        var nameRT = RT(namePlateGO);
        nameRT.anchorMin = new Vector2(0f, 0.76f);
        nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(1f, 1f);
        nameRT.offsetMax = new Vector2(-1f, -1f);

        var nameGO = MakeRect("NameLabel", namePlateGO.transform);
        FillParent(RT(nameGO));
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text = string.IsNullOrEmpty(label) ? (idx + 1).ToString() : label;
        var defaultNameFontSize = slotSize >= 90f ? 14f : 9f;
        nameTMP.fontSize = nameFontSizeOverride > 0f ? nameFontSizeOverride : defaultNameFontSize;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = C_Text;
        nameTMP.alignment = TextAlignmentOptions.Center;
        nameTMP.textWrappingMode = TextWrappingModes.NoWrap;
        nameTMP.overflowMode = TextOverflowModes.Truncate;
        nameTMP.raycastTarget = false;

        if (bindToSquadSlot)
        {
            var slot = slotGO.AddComponent<SquadPortraitSlot>();
            var sso = new SerializedObject(slot);
            sso.FindProperty("portraitImage").objectReferenceValue = portGO.GetComponent<Image>();
            sso.FindProperty("hpFill").objectReferenceValue = hpFillGO.GetComponent<Image>();
            sso.FindProperty("staminaFill").objectReferenceValue = stFillGO.GetComponent<Image>();
            sso.FindProperty("nameLabel").objectReferenceValue = nameTMP;
            sso.FindProperty("selectOverlay").objectReferenceValue = selGO.GetComponent<Image>();
            sso.FindProperty("slotButton").objectReferenceValue = btn;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void BuildEquipmentSlots(Transform parent)
    {
        var area = MakeImage("EquipmentLayoutArea", parent, new Color(0.04f, 0.05f, 0.07f, 0.98f));
        var areaRT = RT(area);
        areaRT.anchorMin = Vector2.zero;
        areaRT.anchorMax = Vector2.one;
        areaRT.offsetMin = new Vector2(4f, 4f);
        areaRT.offsetMax = new Vector2(-4f, -4f);
        var areaOutline = area.AddComponent<Outline>();
        areaOutline.effectColor = new Color(0.26f, 0.29f, 0.32f, 0.95f);
        areaOutline.effectDistance = new Vector2(1f, -1f);

        BuildEquipmentSilhouette(area.transform);

        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Head, "HEAD", "Helmet", new Vector2(0.46f, 0.94f),
            new Vector2(164f, 60f), false);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Face, "FACE", "Mask / Glasses",
            new Vector2(0.46f, 0.84f), new Vector2(168f, 58f), false);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Chest, "CHEST", "Armor / Jacket",
            new Vector2(0.46f, 0.72f), new Vector2(220f, 76f), true);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Back, "BACK", "Backpack", new Vector2(0.46f, 0.60f),
            new Vector2(210f, 70f), true);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.LeftHand, "LEFT HAND", "Weapon",
            new Vector2(0.18f, 0.56f), new Vector2(178f, 66f), false);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.RightHand, "RIGHT HAND", "Weapon",
            new Vector2(0.72f, 0.56f), new Vector2(178f, 66f), false);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Belt, "BELT", "Utility / Tools",
            new Vector2(0.46f, 0.46f), new Vector2(196f, 62f), false);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Legs, "LEGS", "Pants", new Vector2(0.46f, 0.34f),
            new Vector2(192f, 68f), true);
        BuildEquipmentSlotCard(area.transform, EquipmentSlotVisual.Feet, "FEET", "Boots", new Vector2(0.46f, 0.21f),
            new Vector2(182f, 60f), false);

        var weightTMP = MakeRect("WeightReadout", area.transform).AddComponent<TextMeshProUGUI>();
        var weightRT = RT(weightTMP.gameObject);
        weightRT.anchorMin = new Vector2(0f, 0f);
        weightRT.anchorMax = new Vector2(1f, 0.12f);
        weightRT.offsetMin = new Vector2(8f, 2f);
        weightRT.offsetMax = new Vector2(-8f, -2f);
        weightTMP.text = "WEIGHT 32 / 50 kg";
        weightTMP.fontSize = 14f;
        weightTMP.fontStyle = FontStyles.Bold;
        weightTMP.color = C_Text;
        weightTMP.alignment = TextAlignmentOptions.Center;
        weightTMP.raycastTarget = false;
    }

    static void BuildEquipmentSilhouette(Transform parent)
    {
        var root = MakeRect("BodySilhouette", parent);
        var rootRT = RT(root);
        rootRT.anchorMin = new Vector2(0.46f, 0.56f);
        rootRT.anchorMax = new Vector2(0.46f, 0.56f);
        rootRT.pivot = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta = new Vector2(402f, 705f);

        var silhouette = new Color(0.62f, 0.66f, 0.72f, 0.12f);
        MakeSilhouettePiece(root.transform, "Head", silhouette, new Vector2(0.40f, 0.86f), new Vector2(0.60f, 1.00f));
        MakeSilhouettePiece(root.transform, "Torso", silhouette, new Vector2(0.30f, 0.48f), new Vector2(0.70f, 0.86f));
        MakeSilhouettePiece(root.transform, "Pelvis", silhouette, new Vector2(0.36f, 0.38f), new Vector2(0.64f, 0.48f));
        MakeSilhouettePiece(root.transform, "ArmL", silhouette, new Vector2(0.14f, 0.52f), new Vector2(0.30f, 0.76f));
        MakeSilhouettePiece(root.transform, "ArmR", silhouette, new Vector2(0.70f, 0.52f), new Vector2(0.86f, 0.76f));
        MakeSilhouettePiece(root.transform, "LegL", silhouette, new Vector2(0.36f, 0.10f), new Vector2(0.49f, 0.38f));
        MakeSilhouettePiece(root.transform, "LegR", silhouette, new Vector2(0.51f, 0.10f), new Vector2(0.64f, 0.38f));
    }

    static void MakeSilhouettePiece(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var piece = MakeImage(name, parent, color);
        var rt = RT(piece);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = piece.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    static void BuildEquipmentSlotCard(
        Transform parent,
        EquipmentSlotVisual slot,
        string title,
        string itemHint,
        Vector2 anchor,
        Vector2 size,
        bool emphasize)
    {
        var baseColor = emphasize
            ? new Color(0.12f, 0.13f, 0.16f, 0.98f)
            : new Color(0.10f, 0.11f, 0.14f, 0.96f);

        var slotGO = MakeImage($"Slot_{slot}", parent, baseColor);
        var slotRT = RT(slotGO);
        slotRT.anchorMin = anchor;
        slotRT.anchorMax = anchor;
        slotRT.pivot = new Vector2(0.5f, 0.5f);
        slotRT.sizeDelta = size;

        var slotImg = slotGO.GetComponent<Image>();
        var outline = slotGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.31f, 0.35f, 0.95f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = slotImg;
        var cb = btn.colors;
        cb.normalColor = baseColor;
        cb.highlightedColor = new Color(0.12f, 0.28f, 0.22f, 1f);
        cb.pressedColor = new Color(0.12f, 0.36f, 0.28f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.08f, 0.08f, 0.10f, 0.85f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        var grime = MakeImage("Grime", slotGO.transform, new Color(0f, 0f, 0f, 0.10f));
        FillParent(RT(grime));
        grime.GetComponent<Image>().raycastTarget = false;

        var icon = MakeImage("Icon", slotGO.transform, new Color(0.15f, 0.16f, 0.19f, 1f));
        var iconRT = RT(icon);
        iconRT.anchorMin = new Vector2(0f, 0f);
        iconRT.anchorMax = new Vector2(0f, 1f);
        iconRT.offsetMin = new Vector2(6f, 6f);
        iconRT.offsetMax = new Vector2(40f, -6f);
        icon.GetComponent<Image>().raycastTarget = false;

        var titleTMP = MakeRect("SlotTitle", slotGO.transform).AddComponent<TextMeshProUGUI>();
        var titleRT = RT(titleTMP.gameObject);
        titleRT.anchorMin = new Vector2(0f, 0.48f);
        titleRT.anchorMax = Vector2.one;
        titleRT.offsetMin = new Vector2(48f, 2f);
        titleRT.offsetMax = new Vector2(-8f, -2f);
        titleTMP.text = title;
        titleTMP.fontSize = emphasize ? 16f : 14f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = C_Text;
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.raycastTarget = false;

        var hintTMP = MakeRect("ItemHint", slotGO.transform).AddComponent<TextMeshProUGUI>();
        var hintRT = RT(hintTMP.gameObject);
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0.48f);
        hintRT.offsetMin = new Vector2(48f, 2f);
        hintRT.offsetMax = new Vector2(-8f, -2f);
        hintTMP.text = itemHint;
        hintTMP.fontSize = 12f;
        hintTMP.color = C_TextDim;
        hintTMP.alignment = TextAlignmentOptions.MidlineLeft;
        hintTMP.raycastTarget = false;

        var durBg = MakeImage("DurabilityBG", slotGO.transform, new Color(0.07f, 0.08f, 0.10f, 1f));
        var durBgRT = RT(durBg);
        durBgRT.anchorMin = new Vector2(0f, 0f);
        durBgRT.anchorMax = new Vector2(1f, 0f);
        durBgRT.offsetMin = new Vector2(6f, 2f);
        durBgRT.offsetMax = new Vector2(-6f, 6f);
        durBg.GetComponent<Image>().raycastTarget = false;

        var durFill = MakeImage("DurabilityFill", durBg.transform, new Color(0.18f, 0.56f, 0.44f, 1f));
        var durFillRT = RT(durFill);
        durFillRT.anchorMin = Vector2.zero;
        durFillRT.anchorMax = new Vector2(0.66f, 1f);
        durFillRT.offsetMin = Vector2.zero;
        durFillRT.offsetMax = Vector2.zero;
        durFill.GetComponent<Image>().raycastTarget = false;
    }
    }
}
