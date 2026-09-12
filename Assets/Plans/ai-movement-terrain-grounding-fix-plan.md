# Production-Ready Movement: NavMesh for Pathfinding, Terrain for Height

## 1) Architecture Answer

**Keep NavMeshAgent for X/Z pathfinding. Use terrain physics as the sole authority for Y (height).**

This is already what the codebase is designed to do. The infrastructure exists:
- `MovementDestinationResolver` unified click destination resolver
- `MovementGroundingProfile` ScriptableObject with all settings
- `UnitController.FootGrounding` — LateUpdate terrain Y snap
- `UnitController.FallbackTick.TryClampFallbackGrounding` — off-navmesh Y clamp
- `UnitNavUtils.TrySampleTieredNearReferenceY` — Y-filtered NavMesh sampling
- `UnitGroundingManager` — seam corrections on streaming tile events

**The system is not working because the foot snap is turned off in the profile and scoped too narrowly.**

Do not replace the NavMesh system. Enable and extend the grounding correction layer that is already built.

---

## 2) Current Scan Findings — What Is Actually Broken

### Finding 1: FootSnap disabled in profile asset
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
footSnap:
  enabled: 0       # <-- OFF: no frame-rate Y correction fires
  threshold: 0.2
  speed: 8
  interval: 0.12
```

`UnitController.FootGrounding.TryApplyRuntimeFootGrounding()` reads this flag first and returns early. The per-unit terrain Y correction never runs. This is the primary cause of persistent floating.

### Finding 2: FootGrounding only runs for Player role
File: [Assets/01_Game/03_Characters/Controller/UnitController.FootGrounding.cs](Assets/01_Game/03_Characters/Controller/UnitController.FootGrounding.cs) line 23

```csharp
if (role != UnitRole.Player) return;
```

Squad members and survivors are excluded. They get zero LateUpdate terrain correction.

### Finding 3: Ground layer mask defaults to name-lookup fallback
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
ground:
  layers:
    m_Bits: 0   # <-- zero mask: falls back to name-lookup at runtime
```

`MovementGroundLayers.Resolve` looks up `"Ground"`, `"Terrain"`, `"Default"` by name. If your terrain/ground GameObjects are not on exactly those layers, the ground probe raycast hits nothing and `TryProjectGround` returns false, disabling all Y correction silently.

### Finding 4: FallbackTick clamp interval is 0.1 seconds
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
fallback:
  enabled: 1
  threshold: 0.35   # only corrects when 0.35m above ground
  speed: 12
  interval: 0.1     # runs at ~10Hz
```

Units can float 0.34m above terrain indefinitely (below threshold) or float in 0.1s windows between corrections.

### Finding 5: NavMesh `baseOffset` may not match visual pivot
File: [Assets/01_Game/03_Characters/Controller/UnitController.cs](Assets/01_Game/03_Characters/Controller/UnitController.cs)

```csharp
[SerializeField] private float navMeshBaseOffset = 0f;
```

Default is 0. For bottom-pivot character models this is correct. However if any zombie/squad prefab has `NavMeshAgent.baseOffset` set to a positive value in the Inspector, the agent's transform will sit that many metres above the navmesh surface, making the unit visually float.

### Finding 6: `probeUpMeters: 3` on a 120m down probe
For a unit floating significantly above terrain (e.g. camera spawn above terrain before navmesh loads), a 3m upward origin is not enough to escape geometry before the probe ray goes down 120m. Should be raised for streaming environments.

---

## 3) Why Units Go Through the Map

When the NavMeshAgent fails to bind (streaming tile gap, rebake seam), movement falls to `UpdateUsingFallbackMovement`. This applies X/Z delta only:

```csharp
transform.position += movementDelta;   // Y is untouched
```

`TryClampFallbackGrounding` fires every 0.1s and uses a 0.35m threshold. During a streaming seam or rebake, if the terrain mesh is briefly absent, the probe raycast misses and no clamp fires — unit continues with uncorrected Y and can walk into sloped terrain at the wrong height.

---

## 4) Production Fix Plan

### Step 1 — Enable and tune FootSnap in the profile  (immediate, asset edit only)
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
footSnap:
  enabled: 1       # turn on
  threshold: 0.05  # correct any gap > 5cm
  speed: 14        # fast enough to feel instant at normal speeds
  interval: 0.05   # ~20Hz corrections, not once per 120ms
```

This is the single biggest fix. No code change required.

### Step 2 — Extend FootGrounding to SquadMember and Survivor roles
File: [Assets/01_Game/03_Characters/Controller/UnitController.FootGrounding.cs](Assets/01_Game/03_Characters/Controller/UnitController.FootGrounding.cs)

Change:
```csharp
if (role != UnitRole.Player) return;
```

To:
```csharp
if (role != UnitRole.Player && role != UnitRole.SquadMember && role != UnitRole.Survivor) return;
```

### Step 3 — Set explicit ground layer mask in the profile
In Unity Inspector on `DefaultMovementGroundingProfile`:
- Open `Ground > Layers`
- Assign the layers your terrain/ground colliders actually use (likely `Terrain`, `Ground`, or `Default`)

Do not leave it at 0 where it relies on name-lookup. This silently disables all physics grounding when layer names differ.

### Step 4 — Reduce FallbackTick threshold and interval
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
fallback:
  enabled: 1
  threshold: 0.1  # correct when >10cm off terrain (was 0.35m)
  speed: 18       # snap faster when fully off navmesh
  interval: 0.05  # match footSnap interval
```

### Step 5 — Audit NavMeshAgent `baseOffset` on all prefabs
Check these prefabs in the Inspector and verify `NavMeshAgent.baseOffset = 0`:
- [Assets/Shared/Prefabs/Zombies/Zombie Type1.prefab](Assets/Shared/Prefabs/Zombies/Zombie%20Type1.prefab)
- All squad member prefabs under `Assets/01_Game/03_Characters`

A non-zero baseOffset on any prefab will cause that unit to float exactly `baseOffset` metres above the navmesh surface.

### Step 6 — Increase `probeUpMeters` for streaming safety
File: [Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset](Assets/01_Game/01_Core/Movement/Resources/DefaultMovementGroundingProfile.asset)

```yaml
ground:
  probeUpMeters: 10   # was 3 — enough to escape geometry on steep slopes
  probeDownMeters: 120
```

This ensures the ground probe ray starts high enough above the unit to always reach the terrain from above.

---

## 5) Code Changes Required

| File | Change |
|------|--------|
| [UnitController.FootGrounding.cs](Assets/01_Game/03_Characters/Controller/UnitController.FootGrounding.cs) | Remove `Player`-only role check — include `SquadMember` and `Survivor` |
| `DefaultMovementGroundingProfile.asset` | `footSnap.enabled = 1`, tune thresholds, set explicit layer mask, raise `probeUpMeters` |

Everything else is already correctly implemented and routes through `MovementDestinationResolver` and `MovementGroundingProfile`.

---

## 6) What NOT to Change

- **Do not disable NavMeshAgent or replace it with CharacterController.** The current architecture is correct. NavMesh for X/Z, terrain probe for Y.
- **Do not change `MovementDestinationResolver`.** Already unified and routes through `TryResolveGroundedPoint`.
- **Do not change `FollowController`.** Already updated to use `profile.TryResolveGroundedPosition`.
- **Do not change `UnitController.MoveCommands.ResolveNavMeshMoveDestination`.** Already updated to use `MovementDestinationResolver.TryValidateSlotPosition`.

---

## 7) Validation Checklist After Changes

1. Player walks on flat terrain — no gap between feet and ground.
2. Player walks on steep slope — no floating or clipping.
3. Squad members follow player across uneven terrain — no floating.
4. Group move command on uneven ground — all units land on terrain.
5. After streaming tile fires (MapMagic tile apply) — units correct within 1-2 frames, not stuck floating at seam height.
6. Player/unit walks to navmesh edge — does not fall through when stepping off.
7. Enable `logGroundingDiagnostics` on one UnitController prefab and verify `FootGroundContact` log events fire regularly in Play mode.