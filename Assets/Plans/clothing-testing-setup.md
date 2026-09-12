# Project Overview
- Game Title: Zombera
- High-Level Concept: Survival RPG with character customization.
- Players: Single player (testing focus).
- Target Platform: Standalone Windows 64.
- Render Pipeline: URP.

# Game Mechanics
## Core Gameplay Loop
The player explores and scavenges for items. Clothing and equipment provide both visual customization and stat bonuses (armor, etc.).
## Controls and Input Methods
The testing scene will focus on visual evaluation.

# UI
- **Quick Equipment Inspector**: A custom editor for `EquipmentSystem` that provides a dedicated slot-based interface (Head, Chest, Legs, etc.) for easy drag-and-drop of `ItemDefinition` assets.
- **Random Animation Previewer**: A script to cycle through animations.

# Key Asset & Context
- **Scene**: `Assets/Scenes/Testing/ClothingTesting.unity`
- **Scripts**:
    - `EquipmentSystem.cs`: Modify to support editor-time quick-equipping.
    - `RandomAnimationPreviewer.cs` (New): Handles random animation playback.
    - `EquipmentSystemEditor.cs` (New): Custom inspector for better UX.
- **Prefabs**: `Player` prefab.

# Implementation Steps
1. **Modify `EquipmentSystem.cs`**:
    - Add a `[Header("Editor Testing")]` section.
    - Add a `private void OnValidate()` method that calls a new `UpdateEditorEquipment()` method.
    - This allows visual updates in the scene view when fields are changed in the inspector.
2. **Create `RandomAnimationPreviewer.cs`**:
    - Implement a simple loop that picks a random `AnimationClip` from a serialized array and plays it using `animator.CrossFade`.
    - Support a "Randomize Speed" option to see animations at different tempos.
3. **Create `EquipmentSystemEditor.cs`**:
    - Create a custom inspector for `EquipmentSystem`.
    - Layout the slots (Head, Chest, Back, Face, Left Hand, Right Hand, Belt, Legs, Feet) in a clear, vertical or grid-based list.
    - Use `EditorGUILayout.ObjectField` for each slot to allow direct "dropping" of `ItemDefinition` assets.
4. **Setup `ClothingTesting` Scene**:
    - Open `Assets/Scenes/Testing/ClothingTesting.unity`.
    - Place one or more `Player` models.
    - Attach `RandomAnimationPreviewer` to the players.
    - Populate the `RandomAnimationPreviewer` with a selection of common player animations (Idle, Walk, Run, Attack).
    - Ensure a ground plane and a `Directional Light` are present for clear visibility.

# Verification & Testing
1. **Editor Mode**: Drag an `ItemDefinition` (e.g., `T-Shirt`) into the "Chest" slot in the inspector. The player mesh should update immediately in the Scene view.
2. **Play Mode**:
    - Enter Play Mode.
    - The player should start playing random animations.
    - Verify that clothing stays attached and skin-weights (remapping) work correctly during motion.
    - Drag new items into slots at runtime to verify dynamic switching.
