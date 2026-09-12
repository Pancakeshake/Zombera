# Project Overview 
- **Game Title**: Zombera
- **Goal**: Create a camera controller for the `ClothingTesting` scene to allow easy inspection of the character and equipment.
- **Features**:
  - WASD: Move/Pan.
  - QE: Rotate left/right.
  - Mouse Scroll: Zoom in/out.
  - Mouse Right-Click + Drag: Rotate/Orbit view.

# Key Asset & Context
- `Assets/Scripts/Testing/ClothingTestCameraController.cs`: New script.
- `ClothingTesting` scene: Target location for the controller.

# Implementation Steps

1. **Create ClothingTestCameraController.cs**:
   - Implement movement using `Keyboard.current` and `Mouse.current` for direct access.
   - Use `transform.Translate` for local movement (WASD) and zoom.
   - Use `transform.Rotate` for rotation (QE and mouse drag).
   - Add sensitivity and smoothing variables for a better feel.
   - **Dependency**: None.

2. **Attach and Configure**:
   - Locate the **Main Camera** in the `ClothingTesting` scene.
   - Add the `ClothingTestCameraController` component.
   - Set default speed and sensitivity values in the Inspector.
   - **Dependency**: Step 1.

# Verification & Testing
- **Input Check**: Confirm WASD moves the camera locally.
- **Rotation Check**: Confirm QE and Right-Click Drag rotate the camera correctly.
- **Zoom Check**: Confirm mouse wheel zooms in/out relative to the view direction.
