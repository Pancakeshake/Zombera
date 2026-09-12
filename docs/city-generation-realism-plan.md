# City Generation Realism Roadmap

> Zombera procedural city — what's built, what's missing, and what to build next.

---

## Pipeline Flow

```
CityPrefabRoadNetworkBuilder
  └─ CityMathRoadLayoutGenerator (arterial + local grid)
       └─ RoadMeshBuilder (road meshes + optional sidewalks)
            └─ CityPrefabDistrictBuilder (blocks, lots, fences)
                 └─ CityPrefabDistrictBuildingPlacer (buildings, door paths)
                      └─ CityDoorPathGenerator (door-to-road strip meshes)
```

---

## Already Built

| Feature | Location | Status |
|---------|----------|--------|
| Road network layout (arterial ring + local grid) | `CityMathRoadLayoutGenerator` | ✅ |
| Road mesh generation | `RoadMeshBuilder.cs` | ✅ |
| Road junctions (cross, T, city hub) | `ProceduralRoadSystem.Junctions.*` | ✅ |
| Sidewalk strips along roads | `RoadMeshBuilder.BuildSidewalks()` | ✅ coded — toggle `spawnSidewalkMeshes` in `RoadNetworkSettings` |
| Residential blocks + lots + fences | `CityPrefabDistrictBuilder` | ✅ |
| 1 building per lot, road-facing | `CityPrefabDistrictBuildingPlacer` | ✅ |
| Door-to-road strip-mesh paths | `CityDoorPathGenerator` | ✅ |
| EasyRoads3D ↔ gameplay bridge | `EasyRoadsRoadGameplayBridge` | ✅ |
| Runtime streaming city (MapMagic tiles) | `StreamedMapMagicCityBuilder` | ✅ |
| Building catalog + proxy system | `CityAssembledBuildingCatalogLoader` | ✅ |
| Industrial lots → concrete terrain | `CityLotTerrainPainter` | ✅ |

---

## Missing — Priority Tiers




### Tier 2: Medium Effort, Big Visual Upgrade

| # | Feature | What it does | Approach | New/Changed Files |
|---|---------|-------------|----------|-------------------|
| 4 | **Traffic Lights** | Signal poles at 4-way / 3-way intersections | Read junction type from `GameplayRoadGraph` node connections; place signal prefab at each approach | New `CityTrafficLightPlacer.cs`, signal prefab |
| 6 | **Street Signs** | Corner signs with road names | Pull names from `RoadGameplayAuthoring` segments; place sign prefab at intersection corners | New `CityStreetSignPlacer.cs`, sign prefab |
| 7 | **Footpath Network** | Pedestrian paths connecting blocks | Extend `CityDoorPathGenerator` to chain paths between adjacent blocks, or use EasyRoads side-object markers to generate a continuous footpath strip along all road edges | `CityDoorPathGenerator.cs` extension, or `EasyRoadsRoadGameplayBridge.cs` |

### Tier 3: Higher Effort, Full Realism

| # | Feature | What it does | Approach | New/Changed Files |
|---|---------|-------------|----------|-------------------|
| 8 | **Parks / Green Spaces** | Reserve blocks as park zone — grass, trees, benches, footpath loops | Add `Park` to `CityDistrictType`; build `CityParkBuilder` with scatter placement + looping paths | `CityDistrictType.cs`, new `CityParkBuilder.cs` |
| 9 | **Power Lines** | Utility poles along roads | Place pole prefab every N meters; connect adjacent poles with line renderer or thin cylinder | New `CityPowerLinePlacer.cs`, pole prefab |
| 10 | **Street Furniture** | Benches, mailboxes, fire hydrants, trash cans | Poisson-disc scatter on sidewalk zones; configurable density per furniture type | New `CityStreetFurniturePlacer.cs`, furniture prefabs |
| 11 | **Parked Cars** | Car prefabs on road shoulders | Curb-offset placement with random rotation facing road direction; density per road class | New `CityParkedCarPlacer.cs`, car prefabs |
| 12 | **EasyRoads Footpath Area** | Use EasyRoads3D side-object system for footpath generation zones | Configure EasyRoads road types with a "Sidewalk" / "Footpath" side object; bake into gameplay graph via `EasyRoadsRoadGameplayBridge` | EasyRoads road type config, `EasyRoadsRoadGameplayBridge.cs` |

---

## Recommended Build Order

```
1. Turn on sidewalks           ← 5 min, already coded (config only)
2. Street lamps                ← 1 new script, reuses road polyline data
3. Intersection markings       ← 1 new script, reads junction graph
4. Traffic lights              ← builds on #3 (same junction graph)
5. Footpath connectivity       ← extends existing DoorPathGenerator
6+ Depends on art pipeline     ← needs prefabs (signs, poles, cars, furniture)
```

---

## Dependencies

| Feature | Depends on |
|---------|-----------|
| Street lamps | `RoadNetworkSettings` road width, lamp prefab asset |
| Intersection markings | `ProceduralRoadSystem.Junctions` (already built) |
| Traffic lights | `GameplayRoadGraph` junction connectivity |
| Lane markings | `RoadNetworkSettings` road class (arterial vs local) |
| Street signs | `RoadGameplayAuthoring` segment names |
| All Tier 2-3 | Prefab assets (signals, signs, poles, benches, cars) — need art/modeling |
