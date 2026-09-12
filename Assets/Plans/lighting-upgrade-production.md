# Project Overview
- Game Title: Zombera
- High-Level Concept: Open-world zombie survival and squad-based RTS-lite experience.
- Players: Single player (squad management).
- Inspiration / Reference Games: State of Decay, Kenshi, RTS survival games.
- Tone / Art Direction: Gritty, realistic, post-apocalyptic.
- Target Platform: PC (StandaloneWindows64).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: URP (Unity 6).

# Game Mechanics
## Core Gameplay Loop
Scavenge resources, manage a squad, build bases, and defend against dynamic zombie hordes in a procedurally influenced world.

## Controls and Input Methods
Standard mouse and keyboard for RTS/Survival controls (Input System Package).

# UI
- Uses a mix of UGUI and UI Toolkit.
- Current task focuses on improving the visual quality of the UI environment (Main Menu lighting) and global post-processing.

# Key Asset & Context
- **URP Asset**: `Assets/Settings/Build Profiles/New Universal Render Pipeline Asset.asset` (Current)
- **Renderer**: `Assets/Settings/Build Profiles/New Universal Render Pipeline Asset_Renderer.asset`
- **World Scene**: `Assets/00_Scenes/World.unity`
- **Menu Scene**: `Assets/00_Scenes/MainMenu.unity`
- **New Assets to create**:
  - `Assets/Settings/Zombera_HighQuality_URPAsset.asset`
  - `Assets/Settings/Zombera_HighQuality_Renderer.asset`
  - `Assets/Settings/Zombera_Production_VolumeProfile.asset`
  - `Assets/Settings/Zombera_Production_LightingSettings.lighting`

# Implementation Steps
## Step 1: Create Production Visual Assets
1. **Create High-Quality URP Asset**:
   - Duplicate `Assets/Settings/Build Profiles/New Universal Render Pipeline Asset.asset` to `Assets/Settings/Zombera_HighQuality_URPAsset.asset`.
   - Update settings:
     - Shadow Distance: 150.
     - Main Light Shadowmap Resolution: 4096.
     - Supports Soft Shadows: Enabled.
     - Supports Additional Light Shadows: Enabled.
     - Reflection Probe Blending: Enabled.
     - Reflection Probe Box Projection: Enabled.
2. **Create High-Quality Renderer**:
   - Create a new Forward Renderer asset at `Assets/Settings/Zombera_HighQuality_Renderer.asset`.
   - Assign this renderer to `Zombera_HighQuality_URPAsset`.
3. **Create Production Volume Profile**:
   - Create `Assets/Settings/Zombera_Production_VolumeProfile.asset`.
   - Add Overrides:
     - **Tonemapping**: Mode = ACES.
     - **Bloom**: Intensity = 0.5, Threshold = 0.9, Scatter = 0.7.
     - **Color Adjustments**: Saturation = 10, Contrast = 5.
     - **Vignette**: Intensity = 0.25, Smoothness = 0.4.
4. **Create Production LightingSettings**:
   - Create `Assets/Settings/Zombera_Production_LightingSettings.lighting`.
   - Set Default Reflection Resolution to 512.

**Assigned Role**: developer
**Dependencies**: None
**Parallelizable**: No

## Step 2: Configure Global Project Settings
1. **Update Graphics Settings**:
   - Set `Zombera_HighQuality_URPAsset` as the default render pipeline in `ProjectSettings/GraphicsSettings.asset`.
2. **Update Quality Settings**:
   - Ensure the PC quality level uses the new High-Quality URP Asset.

**Assigned Role**: developer
**Dependencies**: Step 1
**Parallelizable**: No

## Step 3: Upgrade World Scene Lighting
1. **Apply Lighting Settings**:
   - Open `Assets/00_Scenes/World.unity`.
   - Assign `Zombera_Production_LightingSettings`.
2. **Configure Global Volume**:
   - Find "Global Volume" GameObject.
   - Assign `Zombera_Production_VolumeProfile`.
3. **Upgrade Reflection Probes**:
   - Find "[Global Reflection Probe]".
   - Set Resolution to 512.
   - Enable Box Projection.
4. **Verify Sun Binding**:
   - Ensure `Sun/Moon Directional Light` is assigned to `RenderSettings.sun`.

**Assigned Role**: developer
**Dependencies**: Step 1
**Parallelizable**: Yes (with Step 4)

## Step 4: Upgrade Main Menu Lighting
1. **Apply Lighting Settings**:
   - Open `Assets/00_Scenes/MainMenu.unity`.
   - Assign `Zombera_Production_LightingSettings`.
2. **Configure Global Volume**:
   - Find "Global Volume" GameObject.
   - Assign `Zombera_Production_VolumeProfile`.
3. **Implement 3-Light Rig**:
   - Replace `Preview_Light` with a more professional setup:
     - **Key Light**: Directional or Spot, warm tone, soft shadows.
     - **Fill Light**: Directional or Point, cool tone, lower intensity, no shadows.
     - **Rim Light**: Spot, positioned behind character, high intensity, highlights edges.
4. **Verify Sun Binding**:
   - Assign the Key Light to `RenderSettings.sun`.

**Assigned Role**: developer
**Dependencies**: Step 1
**Parallelizable**: Yes (with Step 3)

# Verification & Testing
1. **Visual Inspection**:
   - Run the game and check the Main Menu. Characters should look more three-dimensional and better lit.
   - Enter the World scene. Check shadows at distance. They should be sharper and more stable.
   - Verify post-processing (Bloom, ACES tonemapping) is visible and improving the overall image quality.
2. **Performance Check**:
   - Ensure frame rate remains acceptable on target hardware with higher shadow resolutions.
3. **Console Check**:
   - Ensure no URP-related warnings or errors in the console.
