# Save System and UI Improvements

## Threading Fix (SaveSystem.cs)
- Cache `Application.persistentDataPath` in `Initialize()` to allow background thread access.
- Replace all calls to `Application.persistentDataPath` with the cached version.

## UI Real-time Update (SaveGameMenuController.cs)
- Subscribe to `SaveSystem.SaveListChanged` in `Awake()`.
- Unsubscribe in `OnDestroy()`.
- Implement `HandleSaveListChanged()` to trigger `RefreshList()` when a save completes.

## UI Layout Adjustment (LoadSavePanel_Modern.prefab)
- Increase `DetailScreenshot` RectTransform height from 200 to 400.

# Implementation Steps

## 1. Fix SaveSystem Threading and UI Refresh
- **Description**: Modify `SaveSystem.cs` and `SaveGameMenuController.cs`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Adjust Screenshot Height
- **Description**: Modify the `DetailScreenshot` object in `LoadSavePanel_Modern.prefab`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes
