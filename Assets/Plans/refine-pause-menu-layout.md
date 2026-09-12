# Project Overview
- Game Title: Zombera
- Goal: Move pause menu buttons to the left, keep title at the top center, and remove the selection highlight box while keeping text color changes.
- Target Platform: PC (Windows)

# UI
- Title ("PAUSED"): Should be top-center.
- Menu Buttons: Should be left-aligned in a sidebar-like layout.
- Hover Effect: Remove the rough selection box ("Highlight" object), but keep the red text color change on hover.

# Key Asset & Context
- `Assets/01_Game/08_UI/PauseCard.prefab`: The prefab containing the pause menu layout.
- `PauseMenuButtonHighlight.cs`: The script that handles the highlight object and text color.

# Implementation Steps
1. **Relocate Title in PauseCard.prefab**:
   - Open `Assets/01_Game/08_UI/PauseCard.prefab` in edit mode.
   - Move the **`Title`** GameObject (and potentially the `Separator` if desired as a header) from `Content` to the `PauseCard` root.
   - Set `Title` RectTransform:
     - Anchors: `TopCenter` (0.5, 1).
     - Pivot: `TopCenter` (0.5, 1).
     - Anchored Position: (0, -80).
     - Size: Keep large width (e.g. 1000).

2. **Move Buttons Container to the Left**:
   - Update **`Content`** RectTransform:
     - Anchors: `LeftStretch` (AnchorMin: 0.05, 0; AnchorMax: 0.45, 1).
     - Pivot: `MiddleLeft` (0, 0.5).
     - OffsetMin/Max: (0, 0).
   - Update `VerticalLayoutGroup`:
     - `Child Alignment`: `MiddleLeft`.
     - `Spacing`: Keep around 40.
     - Ensure `Padding` is reasonable (e.g. left 40).

3. **Left-Align Button Internal Layout**:
   - For each button (`ResumeButton`, `SaveButton`, `SettingsButton`, `QuitButton`):
     - Update `HorizontalLayoutGroup`: Set `Child Alignment` to `MiddleLeft`.
     - Update `Label` TMP: Set `Alignment` to `Left`.
     - Adjust `LayoutElement` or `RectTransform` on `Label` if necessary to ensure it doesn't push the button too wide.

4. **Disable Selection Highlight Box**:
   - For each button:
     - In the **`PauseMenuButtonHighlight`** component, set the **`Highlight Object`** field to `None` (null).
     - This ensures the script still changes the text color (via `hoverColor`) but no longer toggles the selection sprite.
     - Optionally: Deactivate the `Highlight` child GameObject in the prefab.

5. **Verify in Scene**:
   - Open `Assets/00_Scenes/SystemDevelopment/3_Ui.unity`.
   - Ensure the `PauseCard` instance overrides are cleared so it uses the new prefab layout.

# Verification & Testing
- Enter Play Mode.
- Confirm "PAUSED" text is at the top-center.
- Confirm buttons are on the left.
- Hover over buttons: Text should turn red, but NO rough box should appear.
