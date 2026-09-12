# Portrait Studio + Save Slot Screenshot Implementation Plan

## Overview
This plan implements a reliable portrait pipeline for squad management and automatic save-slot visual previews. It focuses on hardening the existing Portrait Studio and introducing a robust screenshot capture service.

## 1. Portrait Studio Hardening (Phase A)
- **Objective**: Stabilize `PortraitStudioManager` and ensure it responds to appearance/equipment changes.
- **Key Changes**:
    - Add `ValidateStudio()` for runtime configuration checks.
    - Implement a robust Singleton pattern that survives scene transitions.
    - Add `RefreshPortrait()` hook for equipment changes and post-load restoration.
    - Implement `CapturePortraitToBase64()` for save system fallbacks.

## 2. Save Slot Screenshot Pipeline (Phase B)
- **Objective**: Implement automatic screenshot capture on save.
- **Key Changes**:
    - Create `SaveScreenshotService` to capture the main game camera (world only).
    - Hook capture into `SaveManager.PopulateMetadata`.
    - Handle resizing and Base64 encoding.

## 3. UI Polish & Fallbacks (Phase C)
- **Objective**: Display visual previews in the save menu with intelligent fallbacks.
- **Key Changes**:
    - Update `SaveGameMenuController` and `SaveSlotItem` to support visual previews.
    - Implementation of Fallback Chain: Screenshot -> Portrait -> Placeholder.
    - Add texture caching to minimize Base64 decoding overhead.

# Implementation Steps

## 1. Harden Portrait Studio
- **Description**: Refactor `PortraitStudioManager.cs` with validation, singleton improvements, and refresh hooks.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 2. Implement Save Screenshot Service
- **Description**: Create `SaveScreenshotService.cs` and integrate it into the `SaveManager.cs` metadata population flow.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Update UI Fallbacks and Caching
- **Description**: Modify `SaveGameMenuController.cs` and `SaveSlotItem.cs` to handle the new preview data and fallbacks.
- **Assigned role**: developer
- **Dependencies**: Steps 1 & 2
- **Parallelizable**: No

## 4. Final Integration & Testing
- **Description**: Wire components in the `[GameManager]` prefab and perform validation tests.
- **Assigned role**: developer
- **Dependencies**: All previous steps
- **Parallelizable**: No
