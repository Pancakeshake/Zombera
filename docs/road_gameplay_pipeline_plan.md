# Gameplay Road Pipeline Plan

## Goal
Use one authoritative road graph for both terrain visuals and gameplay systems.

Pipeline:
1. Author road graph metadata (types, lane widths/count, speed/noise, nav/path tags, biome links, town links, spawn anchors, roadside-lot metadata, optional sidewalk/decal flags).
2. Bake derived data for fast runtime queries.
3. Bake masks for terrain flattening, MicroSplat road painting, nav/path hints, spawn density, sidewalk/decal placement, and roadside-lot influence.
4. Feed AI/nav/spawn/city systems from the same graph data.

## Tools Added
Editor menu root:
- Tools/3. Roads

Commands:
- Build Automated Road Gameplay Stack (Active Scene)
- Create Or Select Gameplay Road Graph
- Create Or Select Derived Data Asset
- Create Or Select Mask Bake Settings
- Build Graph From Selected Authoring Root
- Bake Selected Graph -> Derived Data
- Bake Selected Graph -> Gameplay Masks
- Setup Road Gameplay Service In Active Scene

Runtime/data assets:
- GameplayRoadGraph
- GameplayRoadDerivedData
- RoadGameplayMaskBakeSettings
- RoadGameplayMaskSet
- RoadGameplayService
- RoadGameplayAuthoringRoot
- RoadGameplayAuthoringNode
- RoadGameplayAuthoringSegment
- RoadGameplayAuthoringSpawnPoint

## Data You Can Author
In GameplayRoadGraph:
- Road segment metadata:
  - roadType
  - laneCount
  - laneWidthMeters
  - speedModifier
  - noiseLevel
  - navArea
  - contributesPathArea
  - fromBiomeId / toBiomeId / biomeConnectionWeight
  - contributesPatrolRoutes
  - supportsVehicles
  - contributesLootZone
  - lootZoneWeight
  - supportsSidewalks / sidewalkWidthMeters
  - supportsDecals / decalDensity
  - contributesRoadsideLots / roadsideLotWeight / roadsideLotSpacingMeters / roadsideLotDepthMeters
  - widthMeters
- Town connectivity:
  - node town flags
  - explicit townConnections list
- Biome connectivity:
  - explicit biomeConnections list (derived from segment biome ids)
- Spawn anchors:
  - role (zombie/patrol/vehicle/loot/survivor/encounter)
  - normalized distance along segment
  - radius/weight/nav requirement

## What You Need To Do
1. Run Build Automated Road Gameplay Stack (Active Scene).
2. This auto-creates:
  - graph/derived/mask assets
  - scene stack root
  - authoring root
  - child node/segment/spawn objects
  - configured RoadGameplayService
  - built graph + derived data + masks
3. Move and extend the generated nodes/segments/spawn points in-scene as needed.
4. Re-run Build Graph From Selected Authoring Root, then re-bake derived data/masks after major authoring edits.
5. Wire gameplay systems to use RoadGameplayService runtime queries.

## Integration Checklist
MapMagic:
- Keep your road graph as source of truth.
- Use baked terrain flatten mask as input to your terrain deformation path.

MicroSplat:
- Use baked microSplatPaintMask as the road paint influence map.
- Blend asphalt/mud/edge textures by roadType if needed.

NavMesh / AI:
- Use navModifierMask to bias road movement areas.
- Use pathAreaMask for general path preference and sidewalkMask for pedestrian-style routing overlays.
- Use RoadGameplayService.GetSpeedModifierAtPosition for movement cost/speed logic.
- Use RoadGameplayService.GetNoiseLevelAtPosition for stealth/hearing gameplay.
- Use RoadGameplayService.TryGetNearestRoadSampleByNavArea for lane/sidewalk/restricted routing decisions.

Spawning / Loot:
- Use RuntimeSpawnPoints from RoadGameplayService to place patrols/vehicles/loot encounters.
- Use spawn role + tag filters to separate biome/event channels.
- Use roadsideLotMask or RoadGameplayService.CollectRoadsideLotCandidates for road-first lot seeding.

Town and Mission Logic:
- Use townConnections to compute road-connected settlement graph.
- Use biomeConnections to identify corridor transitions for encounter and weather handoff logic.
- Build patrol routes from segments where contributesPatrolRoutes is true.

## Recommended Next Build Steps
1. Create a runtime adapter that writes the baked nav mask into your StreamingNavMeshTileService build source area weights.
2. Add a MicroSplat bridge that injects the baked paint mask per tile into your existing MapMagicMicroSplatTileSync workflow.
3. Add a town-connection graph utility (Dijkstra/BFS) for mission routing and convoy path planning.
4. Add authoring validation (missing node IDs, orphaned segments, spawn anchors with invalid segment IDs).

## Notes
- Existing RoadNetworkSystem is still present as a legacy spline/terrain art path.
- This new graph tooling is designed to be gameplay-first and can coexist while you migrate.

## Zone-Aware Streaming Integration Design (May 2026)

Target dependency chain:
1. Terrain tile applied (MapMagic)
2. EasyRoads runtime roads synced into gameplay graph
3. Derived road samples rebuilt
4. City lots consume nearest road sample zone before building pick

### Current Baseline Already in Place
- Per-tile sync trigger exists through MapMagic tile events in EasyRoadsRoadGameplayBridge.
- Runtime road cache and derived bake path already exist via RoadGameplayService and GameplayRoadBaker.
- City spawn already supports waiting for gameplay-road readiness before fallback behavior.

### Phase 1: Extend Road Schema With City Zone
File targets:
- Assets/Scripts/World/Roads/GameplayRoadGraph.cs
- Assets/Scripts/World/Roads/GameplayRoadDerivedData.cs
- Assets/Scripts/World/Roads/GameplayRoadBaker.cs

Design:
- Add a zone enum used by roads and city selection, for example: Unknown, Residential, Commercial, Industrial, Service, Slum, Mixed.
- Add cityZone to RoadGraphSegment.
- Add cityZone to RoadSamplePoint so baked/runtime sampling preserves zone metadata.
- In GameplayRoadBaker.AppendSegmentSamples, copy segment.cityZone into each baked sample.

Compatibility rules:
- Default cityZone to Mixed so existing assets remain valid.
- Keep all legacy roadType behavior unchanged.

### Phase 2: EasyRoads Name -> Zone Mapping
File target:
- Assets/Scripts/World/Roads/EasyRoadsRoadGameplayBridge.cs

Design:
- Add bridge-level naming rules that map road names to city zones.
- Keep mapping data-driven in serialized fields (token lists per zone), not hardcoded in one method.
- During segment conversion, assign cityZone alongside roadType.

Example token intent:
- Residential: res, suburb, housing, neighborhood
- Commercial: com, market, shop, downtown
- Industrial: ind, factory, plant, warehouse
- Slum: slum, shanty

Fallback order:
1. Explicit token match
2. Optional roadType-based default
3. Mixed

### Phase 3: Zone-Aware Building Catalog + Picker
File targets:
- Assets/Scripts/World/City/StreamedCityCatalog.cs
- Assets/Scripts/World/City/StreamedMapMagicCityBuilder.cs

Design:
- Add zone eligibility on each StreamedCityBuildingEntry.
- Add a zone-aware pick method in StreamedCityCatalog that can filter weighted entries by requested zone.
- In city builder, resolve a lot zone from nearest RoadSamplePoint before TryPickBuildingEntry.

Lot zone resolution strategy:
1. Query nearest runtime road sample to lot center within configurable max distance.
2. Use sample.cityZone when found.
3. Fallback to Mixed when no sample is available.

Selection behavior:
- Prefer zone-matching entries.
- If none match, fallback to Mixed-enabled entries.
- If still none, fallback to existing unfiltered selection path.

### Phase 4: Ordering and Wait-System Hardening
File targets:
- Assets/Scripts/World/Roads/EasyRoadsRoadGameplayBridge.cs
- Assets/Scripts/World/City/StreamedMapMagicCityBuilder.cs
- Assets/Scripts/World/WorldManager.cs

Design:
- Keep syncOnTileApplied as the default driver.
- Keep city wait gate enabled when using gameplay-road lots.
- Optionally add a "road sync completed" timestamp or per-tile marker if stricter sequencing is needed later.

### Acceptance Criteria
- A road named with residential tokens yields Residential zone samples.
- Lots nearest those roads preferentially spawn Residential catalog entries.
- If roads are missing or unsynced, city builder safely falls back without deadlocking.
- Debug logs can print: lot position, resolved zone, chosen entry id.

### Suggested Implementation Order
1. Schema + baker propagation (safe, isolated).
2. Bridge zone mapping from names.
3. Catalog and city picker filtering.
4. Logging and validation tooling.
