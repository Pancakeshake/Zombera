# Reserve City Pads

| Field | Value |
|-------|-------|
| StageId | `ReserveCityPads` |
| Section | Planning Field |
| Parent | [Build_Pipeline_Reference_Prompt.md](Build_Pipeline_Reference_Prompt.md) |
| Index | [0_Stage_Issue_Index.md](0_Stage_Issue_Index.md) |

Runs **after** Generate Base Landforms and **before** Erode Landforms.

## Does

1. Provisional site pick (relaxed slope/footprint/continuity; landform + orogen; biomes/hydrology may be null).
2. Builds city-footprint + `cityPadFlatMarginMeters` (default 10) flat cores with `cityPadFalloffMinMeters` blend apron.
3. Stamps cores+blend into `LandformField`, optional edge talus; stores `Artifacts.CityPads`.

Later erosion freezes cores (apron erodes); hydrology/carve exclude cores; Apply City Pads reasserts cores after highways.
