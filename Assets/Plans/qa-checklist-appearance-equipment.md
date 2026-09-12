# QA Checklist: Character Appearance & Equipment System

## 1. Character Creation & Selection
- [ ] **Race Selection**: Switching between races in Character Creator updates the preview avatar correctly.
- [ ] **Wardrobe Cycling**: Hair and beard options update based on race/gender tags in the catalog.
- [ ] **DNA Sliders**: Body sliders (Height, Muscle, etc.) affect the preview avatar scale and proportions.
- [ ] **Color Tints**: Skin, hair, and eye color changes are reflected immediately on the preview.
- [ ] **Confirmation**: Clicking "Confirm" correctly serializes the selection to `CharacterSelectionState`.
- [ ] **Persistence**: Returning to the Main Menu and back to Character Creator restores the previous selection.

## 2. World Spawn
- [ ] **Player Spawn**: Player unit spawns with correct appearance profile applied.
- [ ] **Startup Squad**: All 8 initial squad members spawn with randomized appearance variants.
- [ ] **Randomization Variety**: Verified that squad members have different hair, colors, and heights.
- [ ] **Zombie Appearance**: Ambient zombies spawn with randomized variants from the service.

## 3. Equipment Visuals
- [ ] **Weapon Attachment**: Primary and Secondary weapons attach to the correct hand bones.
- [ ] **Socket Priority**: Items with specific socket overrides (e.g., `Socket_ItemName`) attach to the override if present.
- [ ] **Bone Fallback**: Humanoid bone mapping works correctly for units with standard humanoid rigs.
- [ ] **No Wardrobe Dependencies**: Items with `appearanceWardrobeRecipe` assigned but no `equippedVisualPrefab` do NOT show visual artifacts or attempt UMA builds.
- [ ] **Transform Offsets**: localPosition, localRotation, and localScale from `ItemDefinition` are applied correctly to the socketed prefab.

## 4. Save & Load Migration
- [ ] **Legacy Migration**: Old saves using UMA recipe names are correctly migrated to modern `CharacterAppearanceProfile` JSON.
- [ ] **Profile Restore**: Loading a save file restores the exact appearance of the player and all squad members.
- [ ] **Runtime Updates**: Capturing a profile during gameplay (e.g., after a change) and saving/reloading works without data loss.

## 5. Combat & Transitions
- [ ] **Equip/Unequip Performance**: No significant hitches when changing equipment in the inventory.
- [ ] **Animation Integrity**: Equipped items follow the rig correctly during combat animations (melee swings, reloading).
- [ ] **Scene Transitions**: Traveling between world zones maintains equipment and appearance state.
