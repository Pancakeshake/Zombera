You are a senior Unity rendering/terrain engineer. Your task is to optimize and improve the terrain painting system in my game, with TWO equally important goals:

1. SIGNIFICANTLY INCREASE TERRAIN PAINTING / TERRAIN GENERATION SPEED
2. INCREASE VISUAL QUALITY WITHOUT CREATING UNNECESSARY PERFORMANCE COST

Do NOT immediately start changing code. First inspect the existing project and understand exactly how terrain painting currently works.

==================================================
PHASE 1 — AUDIT THE EXISTING SYSTEM
==================================================

Inspect the entire terrain generation and painting pipeline.

Find and document:

- All TerrainData creation/modification code
- TerrainLayer creation and assignment
- SetAlphamaps / alphamap writes
- SetHeights / heightmap writes
- TerrainData.SyncTexture
- Terrain.Flush
- TerrainCollider updates
- Any GPU/compute shader terrain processing
- Any CPU texture generation
- Any RenderTexture usage
- Any Texture2D ReadPixels / Apply calls
- Any NativeArray / Burst / Jobs usage
- Any MapMagic2 integration
- Any MicroSplat integration
- Any custom terrain shaders
- Any biome masks
- Any splat/weight map generation
- Any road/city masks
- Any vegetation masks
- Any erosion/height processing
- Any terrain tile stitching
- Any LOD generation
- Any editor-only versus runtime generation code

Trace the COMPLETE data flow:

World generation
→ height generation
→ biome classification
→ masks
→ terrain layers
→ alphamap/splat weights
→ shader/material
→ terrain rendering

Identify where the actual bottlenecks are.

Do not assume the bottleneck is Terrain.SetAlphamaps.

Measure before optimizing.

==================================================
PHASE 2 — CREATE A PERFORMANCE BASELINE
==================================================

Create an editor/debug profiling mode that measures:

- Total terrain generation time
- Height generation time
- Biome generation time
- Mask generation time
- TerrainLayer setup time
- Alphamap generation time
- Alphamap upload time
- TerrainData update time
- Collider update time
- Shader/material setup time
- GPU upload time
- CPU memory allocated
- GPU memory where measurable
- Number of terrain tiles
- Terrain resolution
- Alphamap resolution
- Number of terrain layers
- Number of texture samples
- Number of passes

Record results per terrain tile and for the entire world.

If Unity Profiler markers are appropriate, add custom ProfilerMarker instrumentation.

Create a benchmark that can be run repeatedly so optimizations can be compared against the original implementation.

IMPORTANT:

Do not accept an optimization simply because the code looks faster.

Prove that it is faster using measurements.

==================================================
PHASE 3 — FIND THE BIGGEST BOTTLENECKS
==================================================

Rank the bottlenecks by:

1. Time consumed
2. Memory allocation
3. GPU cost
4. Number of repeated operations
5. Scalability as world size increases

Pay particular attention to:

- Per-pixel C# loops
- Repeated Get/Set operations
- Repeated TerrainData API calls
- Texture2D.Apply()
- SetAlphamaps()
- Temporary allocations
- Garbage collection
- Recalculating masks
- Recalculating biome classification
- Duplicate noise calculations
- Duplicate texture sampling
- Reprocessing pixels for each terrain layer
- Processing terrain tiles independently when calculations could be shared
- Synchronizing CPU/GPU unnecessarily
- Excessive resolution
- Excessive terrain layers

==================================================
PHASE 4 — OPTIMIZE THE TERRAIN PAINTING PIPELINE
==================================================

Implement the highest-value optimizations.

Prefer this architecture where appropriate:

WORLD DATA
    ↓
SHARED HEIGHT / BIOME / MASK DATA
    ↓
GPU OR BURST/JOBS PROCESSING
    ↓
COMPACT TERRAIN WEIGHT DATA
    ↓
SINGLE/LOW-NUMBER TERRAIN UPLOADS
    ↓
SHADER-BASED DETAIL

Avoid doing expensive work separately for every terrain layer.

For CPU processing:

- Use Burst where appropriate
- Use Unity Jobs where appropriate
- Use NativeArray
- Avoid managed allocations
- Avoid LINQ
- Avoid per-pixel object creation
- Avoid unnecessary copies
- Process contiguous memory
- Parallelize independent terrain regions
- Cache reusable calculations
- Calculate shared values once

For GPU processing:

Consider compute shaders for operations that are clearly massively parallel, especially:

- biome classification
- noise evaluation
- slope calculation
- curvature
- height-based masks
- moisture
- temperature
- biome weights
- road masks
- distance fields
- terrain layer weight generation

Do NOT move something to GPU merely because "GPU = faster".

Benchmark CPU Burst/Jobs against Compute Shader implementations.

==================================================
PHASE 5 — REDUCE REDUNDANT CALCULATIONS
==================================================

Look for calculations such as:

noise(x,z)
slope(x,z)
height(x,z)
moisture(x,z)
temperature(x,z)

being calculated repeatedly by different systems.

Create shared intermediate data where beneficial.

For example:

TerrainPixelData

could contain reusable values such as:

height
slope
curvature
moisture
temperature
biomeId
roadInfluence
cityInfluence
waterInfluence

Then derive terrain weights from those values rather than recalculating the same information.

However, do not blindly store everything.

Analyze memory versus compute tradeoffs.

==================================================
PHASE 6 — IMPROVE VISUAL QUALITY
==================================================

Improve visual quality WITHOUT simply increasing every texture to 4K or increasing every terrain resolution.

Investigate:

- Better biome transitions
- Multi-layer blending
- Slope-aware materials
- Height-aware materials
- Moisture-aware materials
- Rock exposure
- Dirt transitions
- Grass transitions
- Beach/sand transitions
- Snow transitions
- Road-edge transitions
- Natural variation
- Macro variation
- Micro detail
- Normal maps
- Detail maps
- Triplanar techniques where appropriate
- Anti-tiling techniques
- Distance-based texture detail
- Procedural variation
- Color variation
- Height blending

Aim for natural-looking transitions rather than obvious splatmap bands.

Avoid:

- obvious square texture repetition
- obvious circular noise patterns
- hard biome boundaries
- excessive texture layers
- texture swimming
- visible terrain tile seams
- excessive high-frequency noise
- muddy materials caused by excessive blending

==================================================
PHASE 7 — MICRO SPLAT / TERRAIN SHADER ANALYSIS
==================================================

My project may use MicroSplat.

If MicroSplat is present:

Inspect the current MicroSplat configuration and determine:

- Number of terrain layers
- Number of texture samples
- Number of enabled modules
- Shader variants
- Normal map usage
- Detail texture usage
- Distance blending
- Anti-tiling features
- Triplanar features
- Per-pixel operations
- GPU cost

Do NOT remove MicroSplat functionality blindly.

Determine which features produce the greatest visual improvement per GPU cost.

If the project does not use MicroSplat, design the solution so it remains compatible with the existing terrain rendering architecture.

==================================================
PHASE 8 — TERRAIN RESOLUTION STRATEGY
==================================================

Analyze whether the current terrain resolutions are appropriate.

Consider separately:

- Heightmap resolution
- Alphamap resolution
- Detail resolution
- Base map resolution
- Terrain tile size
- Texture resolution

Do not automatically increase resolution.

Determine where higher resolution actually improves visual quality and where shader/detail techniques would be more efficient.

The goal is:

HIGH PERCEIVED QUALITY
with
LOW ACTUAL DATA COST

==================================================
PHASE 9 — TILE / WORLD SCALABILITY
==================================================

My game uses a large procedural world.

Design the system so terrain generation scales efficiently across many terrain tiles.

Investigate:

- Tile boundaries
- Shared border data
- Overlapping generation
- Parallel tile processing
- Streaming
- Background generation
- Deferred terrain uploads
- Tile prioritization
- Dirty-region updates
- Partial repainting
- Caching generated masks
- Saving generated terrain data
- Regenerating only changed regions

If only one small area of terrain changes, do NOT regenerate the entire terrain tile unless Unity requires it.

Design an efficient dirty-region system where practical.

==================================================
PHASE 10 — QUALITY/PERFORMANCE SETTINGS
==================================================

Create configurable quality levels.

For example:

LOW
MEDIUM
HIGH
ULTRA

Control things such as:

- Terrain painting resolution
- Texture detail
- Number of active terrain layers
- Shader features
- Normal maps
- Macro variation
- Micro detail
- Distance blending
- Procedural variation
- Generation resolution

The system should allow the same world data to produce different rendering quality levels.

==================================================
PHASE 11 — IMPLEMENTATION RULES
==================================================

Follow these rules strictly:

- Preserve existing functionality unless there is a measurable reason to change it.
- Do not rewrite large systems unnecessarily.
- Do not introduce dependencies without justification.
- Do not duplicate existing systems.
- Reuse existing world/biome data.
- Keep editor generation and runtime generation clearly separated.
- Keep deterministic generation deterministic.
- Avoid hidden allocations.
- Avoid unnecessary GPU↔CPU synchronization.
- Avoid unnecessary texture copies.
- Avoid repeatedly uploading identical data.
- Keep terrain seams seamless.
- Maintain compatibility with the existing world-generation pipeline.

Every major optimization must include:

BEFORE:
X ms

AFTER:
Y ms

IMPROVEMENT:
X%

and memory impact where relevant.

==================================================
PHASE 12 — VALIDATION
==================================================

After implementation:

1. Generate a representative terrain tile.
2. Generate a large group of terrain tiles.
3. Compare generation time against the baseline.
4. Compare visual quality.
5. Check terrain seams.
6. Check biome boundaries.
7. Check roads.
8. Check cities.
9. Check vegetation masks.
10. Check terrain collider.
11. Check runtime loading.
12. Check saving/loading generated terrain.
13. Check regeneration after modifying terrain.
14. Check memory usage.
15. Check GC allocations.
16. Check GPU performance.

Create automated validation where practical.

==================================================
FINAL DELIVERABLE
==================================================

At the end, provide a technical report containing:

1. CURRENT PIPELINE
2. PERFORMANCE BASELINE
3. BOTTLENECKS
4. OPTIMIZATIONS IMPLEMENTED
5. BEFORE/AFTER BENCHMARKS
6. MEMORY IMPACT
7. GPU IMPACT
8. VISUAL QUALITY IMPROVEMENTS
9. REMAINING BOTTLENECKS
10. RECOMMENDED NEXT OPTIMIZATIONS

Also provide a simple table:

Optimization | Before | After | Improvement | Risk

Do not claim an optimization worked unless it was actually measured.

Most importantly:

DO NOT optimize blindly.

First understand the existing architecture, measure it, identify the dominant bottleneck, then make the smallest high-impact architectural changes necessary to achieve substantially faster terrain painting while improving visual quality.