---
name: _asset-organizer
description: "Open the Asset Auditor & Organizer window — scan, review, and move project assets into their correct category folders under Assets/02_Shared/. The primary asset organization tool for Zombera."
---

# Asset Auditor & Organizer

Opens the **Asset Auditor & Organizer** editor window (Tools → Assets → Asset Auditor & Organizer).

Scan, review, and move project assets into their correct category folders under `Assets/02_Shared/`.

## What It Does

- **Scan** — searches a folder for assets of a given type (Prefab, Material, Texture, Mesh, Audio, etc.)
- **Review** — shows a sortable results table with asset name, current folder, and status
- **Move** — moves selected (or all) assets into the correct `Assets/02_Shared/[Category]/` subfolder
- **Delete Duplicates** — removes assets that share a name with one already in the destination
- **Exclude Folders** — skip folders that should not be scanned

## How to Open

From Unity's top menu: **Tools → Assets → Asset Auditor & Organizer**

Or via script execution:

```csharp
// Open the window programmatically
UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Assets/Asset Auditor & Organizer");
```

## Related Files

- `Assets/Editor/Organizing/AssetAuditorWindow.cs` — the editor window
- `Assets/Editor/Organizing/AssetAuditorScanner.cs` — scan logic
- `Assets/Editor/Organizing/AssetAuditorConfig.cs` — persisted settings
- `Assets/Editor/Organizing/AssetAuditorCategoryRegistry.cs` — category definitions
- `Assets/Editor/Organizing/AssetAuditorData.cs` — data types

## Category Destination

Moves assets into `Assets/02_Shared/` under category-specific folders based on asset type and the selected category.
