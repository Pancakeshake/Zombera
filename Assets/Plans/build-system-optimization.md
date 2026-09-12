# Build System Cleanup & Efficiency Optimization

## Project Overview
- **Game Title**: Zombera
- **High-Level Concept**: Survivor-RTS with building.
- **Goal**: Clean up the build system architecture, move from polling to event-driven state changes, and improve efficiency.

## Implementation Steps

### Phase 1: Event System Foundation
1. **Create Build Events**: Define `BuildModeChangedEvent` in a new file.
   - **File**: `Assets/01_Game/00_Framework/Events/BuildEvents.cs`
   - **Role**: developer
   - **Dependencies**: None
2. **Update EasyBuild Bridge**: Implement event publishing in the bridge.
   - **File**: `Assets/01_Game/07_Building/Integrations/EasyBuildRadialMenuInputBridge.cs`
   - **Logic**: Detect `IsBuildUiActive` state changes in `Update` and publish `BuildModeChangedEvent`.
   - **Role**: developer
   - **Dependencies**: Step 1
3. **Update Legacy Placement Controller**: Implement event publishing in the legacy controller.
   - **File**: `Assets/01_Game/07_Building/Placement/BuildPlacementController.cs`
   - **Logic**: Publish `BuildModeChangedEvent` when toggled.
   - **Role**: developer
   - **Dependencies**: Step 1

### Phase 2: HUD Refactor (Event-Driven)
1. **Subscribe in WorldHUDController**: Replace polling with event subscription.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.Lifecycle.cs`
   - **Logic**: Subscribe in `OnEnable`, unsubscribe in `OnDisable`. Remove `TickBottomBuildModeState()` from `Update`.
   - **Role**: developer
   - **Dependencies**: Step 1, 2, 3
2. **Update Build HUD Logic**: Handle the event and transition UI state.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.cs`
   - **Logic**: `OnBuildModeChanged` handler to drive visibility transitions.
   - **Role**: developer
   - **Dependencies**: Step 1
3. **Clean up Dependencies**: Optimize reference resolution.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.Dependencies.cs`
   - **Logic**: Remove per-frame dependency refresh timer.
   - **Role**: developer
   - **Dependencies**: None

### Phase 3: Efficiency & Catalog
1. **Optimize Catalog Updates**: Ensure catalog rebuilds only happen on actual data changes.
   - **File**: `Assets/01_Game/08_UI/Scripts/HUD/WorldHUDController.BuildHud.CatalogState.cs`
   - **Role**: developer
   - **Dependencies**: None

## Verification & Testing
- **Manual Test**: Enter build mode via 'B' key. Verify HUD bottom strip appears immediately.
- **Manual Test**: Cancel build mode via 'Esc'. Verify HUD returns to squad strip.
- **Manual Test**: Perform building tasks. Verify no performance hit or lag during transitions.
- **Console Logs**: Check for "Build mode visibility changed" logs to ensure events are firing once per transition.
