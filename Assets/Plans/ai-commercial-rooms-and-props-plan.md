# AI Plan: Commercial Rooms & Props (Shop Interiors)

## 1) Goal

Give Commercial buildings store-like interiors: 1–2 rooms, a proper retail room
pool (sales floor + back rooms), room types that a shop needs, and fuseboxes only
on non-storefront walls.

## 2) Current State (verified 2026-08-24)

- `RoomType` enum is residential-only (LivingRoom, Kitchen, Bedroom, Bathroom,
  Hallway, Dining, Storage, Garage, Entry, Utility, Stairwell).
- Room planning is template-driven; commercial templates can be authored but there
  is no commercial-specific room plan. Legacy fallback splits rooms by
  `MaxRoomsPerFloor` with no type meaning.
- `RoomSettings` SO (`RoomTypeConfig[]`): `AllowOpenPlan`, `BlockFrontDoor`,
  floor % min/max per type. Door picking already respects `BlockFrontDoor`.
- `PlaceFuseBox` picks ANY ground-floor wall that isn't a door/window — including
  the storefront. Should be back-only for commercial.
- `InteriorPropConfig` maps `RoomType → RoomPropEntry[]` for prop placement; new
  room types default to no props until configured.

## 3) New Room Types (enum additions, `BuildingArchetypeModels.cs`)

| RoomType | Purpose | AllowOpenPlan | BlockFrontDoor | Notes |
|---|---|---|---|---|
| `SalesFloor` | main retail area (front of shop) | true | **false** | door side lives here |
| `Stockroom` | back storage | true | true | |
| `CommercialKitchen` | takeaway/bakery/food prep | false | true | |
| `ChangeRoom` | fitting rooms | false | true | only on larger stores |
| `Office` | back office | false | true | |
| `StaffRoom` | break room | false | true | optional pool member |

Reuse existing: `Bathroom` (staff toilet), `Hallway`/`Entry` where useful.

Additions required per new type:
- `RoomSettings.RoomTypeConfig` defaults (`DefaultFor`) — sensible flags/floor %.
- Room color map (`Models.cs`) entries for the new types (room-label gizmos).
- `RoomSettings` SO asset update (existing assets don't pick up code defaults —
  edit the asset once via script, same as `DistrictLotTerrainLayout` before).
- `InteriorPropConfig` prop entries (content-dependent, see §7).

## 4) Commercial Room Planning (1–2 rooms)

New `PlanCommercialRooms(context)` in Planning.cs, used when
`context.BuildingCategory == Commercial` (before the template path so commercial
is deterministic):

- **Split**: footprint split into a FRONT strip (street side, depth ≈ 55–65% of
  footprint depth) and a BACK strip.
- **Front room**: `SalesFloor` by default; when the back room is
  `CommercialKitchen` the front becomes a `Dining` area (customer seating facing
  the storefront glass).
- **Back room** = 1 room from a weighted pool when the back strip is large enough:
  `Stockroom` (heavy weight), `CommercialKitchen`, `Office`, `StaffRoom`,
  `ChangeRoom` (only via clothing templates). Weighted pick per building;
  kitchen templates require `CommercialKitchen` so it's always at the back.
- Result: 1 room (small shops) or 2 rooms (typical) — no more.
- Interior wall between front/back; back rooms get doorways into the sales floor
  via the existing interior-wall doorway planner.

Door picking: with `BlockFrontDoor` set on every back room type, existing
`PickNonBlockingDoorIndices` naturally places the street door on the SalesFloor
side — no special door logic needed.

## 5) FuseBox — Opposite Wall Only (decided)

`PlaceFuseBox` change: when commercial, candidates = ONLY the wall opposite the
front door side (back wall) — never the sides (shops snap side-to-side) and never
the storefront. No candidates → skip with a debug log.

### Side Windows (decided)

- Snapping shop types must NOT get side windows (they butt against neighbours).
- Standalone commercial buildings (anchors) DO keep side windows.
- Implementation: new template flag `AllowSideWindows` (default true). Commercial
  window placement: `!AllowSideWindows` → windows only on the back wall.

## 6) Commercial Subtypes & Template Batches (new scope, decided)

- **Subtypes via templates**: e.g. a `ClothingStore` template pins
  `FixedWidthCells/FixedDepthCells` (all buildings of that template share one size)
  and declares its room pool (clothing → `SalesFloor` + `ChangeRoom` in the back
  pool). `ChangeRoom` is RESERVED for such templates, not in the generic pool.
- **Variant-set batch generation**: a tool ("Generate Commercial Variant Set")
  that, for one template + one exterior skin (e.g. `4x4 Brick`), generates ALL
  variants for that footprint into a folder, sharing the same external material:

  ```
  Assets/02_Shared/Buildings/Buildings_Modular_Complete/
    Commercial/
      ClothingStore/
        4x4/
          Brick/
            ClothingStore_Brick_000001.prefab
            ClothingStore_Brick_000002.prefab
            ...
  ```

  Variant axes: door position on the storefront, glass-run composition, interior
  back-room pick, roof flat/parapet pick. The city catalog loader already accepts
  deeper nesting (district = first subfolder under the assembled root).
- **Door parts**: the storefront needs door-in-glass kit pieces (`ShopGlass_Door`
  single/double) so door placement varies per variant — new kit part work item
  (Blender, same conventions as the window set).

## 7) Files To Change

- `BuildingArchetypeModels.cs` — enum additions.
- `RoomSettings.cs` — `DefaultFor` flags for new types.
- `ModularSingleLevelHouseGeneratorTool.Models.cs` — room color map.
- `ModularSingleLevelHouseGeneratorTool.Planning.cs` — `PlanCommercialRooms` +
  hook in `PlanFloorRooms`.
- `ModularSingleLevelHouseGeneratorTool.Floors.cs` — `PlaceFuseBox` back-only.
- SO assets: `RoomSettings.asset`, `InteriorPropConfig` (script edit).

## 7) Props (content dependency)

`InteriorPropConfig` needs entries per new room type; requires prop prefabs in
`Assets/02_Shared/Prefabs/Props/` (shelves, counters, racks). Plan this as a
follow-up once we know which prop packs are available — room types land first.

## 8) Open Decisions

1. ~~Back-room pool + weights~~ — approved: `Stockroom` (heavy), `CommercialKitchen`,
   `Office`, `StaffRoom`; `ChangeRoom` only via clothing-type templates.
2. ~~FuseBox~~ — opposite wall only; sides never (snap-together), front never.
3. Room threshold: 1 room only when the back strip < 2×2 cells, else 2 rooms
   (proposed default — confirm).
4. ~~ChangeRoom~~ — reserved for subtype templates (clothing store).
5. ~~Naming~~ — `SalesFloor`.
6. Variant-set folder layout (`<District>/<Subtype>/<WxD>/<Skin>/…`) — confirm.
7. Door-in-glass kit parts: single + double door variants — scope into batch v1 or
   follow-up?

## 9) Suggested Order

1. Enum + defaults + color map (no visual change).
2. `PlanCommercialRooms` + hook; generate 2–3 test shops, verify room labels.
3. FuseBox opposite-wall-only + `AllowSideWindows` flag; verify.
4. Update `RoomSettings.asset` via script.
5. Commercial template(s): `ClothingStore` pilot (pinned size, ChangeRoom pool).
6. Variant-set batch tool + door-in-glass kit parts.
7. Prop entries for the new room types (once prop prefabs are identified).
8. Test buildings + user review.
