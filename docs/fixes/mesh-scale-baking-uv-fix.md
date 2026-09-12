# Fix: Non-Uniform Mesh Scale → Material Stretching (Brick/Gable/Roof)

## Problem
When a ProBuilder mesh is scaled non-uniformly via `Transform.localScale` (e.g., a gable stretched to `(w, peakH/1.5, 1)`), any material applied to it stretches because Unity applies materials post-transform — UV coordinates get scaled by the Transform.

## Solution: Bake scale into mesh + scale UVs
Instead of keeping `localScale`, bake the scale into the mesh geometry **and** the UVs, then reset `localScale` to `(1,1,1)`.

### Step-by-step (see `BakeMeshScale` in `ModularSingleLevelHouseGeneratorTool.Building.cs`)

1. **Clone the source mesh** with `Object.Instantiate(sourceMesh)` — never modify the source asset
2. **Scale vertices** by `localScale` using `Vector3.Scale`
3. **Scale UV0** by `(localScale.x, localScale.y)` so texture density stays consistent
4. **Assign the clone** to `MeshFilter.sharedMesh`
5. **Save clone as sub-asset** via `AssetDatabase.AddObjectToAsset(mesh, prefabPath)` so it survives prefab serialization
6. **Reset `localScale` to `(1,1,1)`**

### Why UV scaling matters
Without UV scaling: a 4m-wide mesh with UV 0-1 stretches the texture 4×.
With UV scaling: UV 0-4 tiles the texture 4 times — same brick density as the original 1m mesh.

### Key gotchas
- `Object.Instantiate(sourceMesh)` creates a runtime clone. It MUST be saved as a sub-asset of the generated prefab before `SaveAsPrefabAsset`, or it becomes a dangling reference.
- The source asset (`Roof_Gable.prefab`, etc.) is never modified.
- Use `GetComponentInChildren<MeshFilter>()` (not `GetComponent`) — ProBuilder puts MeshFilter on a child when ProBuilderMesh is on the root.

## Files modified
- `Assets/Editor/ModularSingleLevelHouseGeneratorTool.Building.cs`
  - `BakeMeshScale(GameObject go)` — the bake method (line ~577)
  - `_bakedMeshes` static list — collects clones for sub-asset registration
  - Called after each roof piece scale assignment (ridge, L panel, R panel, front gable, back gable)
