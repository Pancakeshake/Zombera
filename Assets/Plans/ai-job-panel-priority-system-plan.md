# AI Plan: RimWorld-Style Job Panel + Priority Work System

## 1) Goal
Create a Jobs panel where each squad member has per-job priorities, then drive autonomous work selection using a RimWorld-like scheduler.

Initial jobs to support:
- Looting
- Digging / Mining
- Building
- Guarding
- Cooking
- Crafting

Core behavior target:
- Priority value per member per job (1-4 active, disabled/off)
- Lowest number wins (1 is highest)
- If equal priority, break ties by distance, urgency, and skill fit
- Members continuously reevaluate when jobs complete/fail/become blocked

## 2) Existing Systems You Can Reuse

### Reusable runtime behavior
- Construction work loop already exists via `WorkerAI` + `ConstructionJob`.
- Crafting already has a queue and persistence (`CraftingService`, `CraftingSaveProvider`).
- Loot containers already support deterministic generation and transfer (`LootContainer`).
- Squad membership and selection are centralized in `SquadManager`.

### Reusable UI architecture
- Squad management screen already supports top tabs and tab-specific controllers.
- Formations tab has already been added in your current branch, so Jobs should follow the same tab pattern.

### Reusable save/load architecture
- Provider-based save system already supports modular persistence by domain.
- Add a dedicated jobs provider instead of overloading existing providers.

## 3) High-Level Architecture

### 3.1 New Core Domain Types
Add new files under a jobs/work domain (suggested folder: `Assets/01_Game/03_Characters/Work/`).

- `WorkJobType` enum
  - Looting, Mining, Building, Guarding, Cooking, Crafting

- `WorkPriorityLevel` enum/int mapping
  - Off = 0
  - Priority1 = 1
  - Priority2 = 2
  - Priority3 = 3
  - Priority4 = 4

- `WorkAssignmentProfile`
  - Per-member dictionary: `WorkJobType -> WorkPriorityLevel`
  - Optional per-job toggles / minimum skill thresholds

- `WorkTaskState`
  - Available, Reserved, InProgress, Completed, Failed, Blocked

- `WorkTaskDescriptor`
  - Stable task id
  - Job type
  - Position / target object
  - Reservation owner member id
  - Urgency score
  - Required capabilities (tool/skill/station)

### 3.2 New Runtime Services

- `WorkManager` (global orchestrator)
  - Collects tasks from all task providers
  - Maintains reservation table
  - Resolves best task for each worker
  - Handles requeue/cancel/failure

- `WorkTaskProvider` interface
  - `GetAvailableTasks(List<WorkTaskDescriptor> buffer)`
  - Implementations:
    - `BuildingWorkTaskProvider` (from `ConstructionJob`)
    - `CraftingWorkTaskProvider` (from crafting queue/stations)
    - `LootWorkTaskProvider` (from unopened/partially looted containers)
    - `GuardWorkTaskProvider` (from guard posts or defend zones)
    - `MiningWorkTaskProvider` (from mine designations)
    - `CookingWorkTaskProvider` (from cooking queues)

- `WorkerBrain` (per squad member)
  - Reads member priorities
  - Requests next task from `WorkManager`
  - Executes via adapters to existing systems
  - Reports completion/failure/blocked states

## 4) RimWorld-Style Priority Algorithm

Each idle worker computes a score per candidate task:

`finalScore = priorityWeight + distanceWeight + urgencyWeight + skillWeight + statePenalty`

Recommended weighting:
- `priorityWeight`: dominant term (e.g., priority * 1000)
- `distanceWeight`: moderate (NavMesh path length)
- `urgencyWeight`: negative for urgent tasks (fire, attack, decay, starvation)
- `skillWeight`: negative when worker has strong relevant skill
- `statePenalty`: large positive if blocked/risky/unreachable

Selection rules:
1. Ignore tasks where worker priority is Off.
2. Ignore tasks worker cannot perform (missing station/tool/skill).
3. Sort by `priority` first.
4. Tie-break using `finalScore`.
5. Reserve chosen task atomically.

Reevaluation triggers:
- Task state changed
- Job created/removed
- Worker damaged/downed
- Higher-priority urgent task appears
- Every N seconds fallback polling (0.5-1.0s)

## 5) Execution Integration by Job Type

### Looting
- Source tasks from nearby `LootContainer` with remaining contents.
- Execution path:
  - Move to container
  - Open/transfer all to inventory/base storage
  - Mark complete when emptied or full inventory reached

### Building
- Bridge to existing `ConstructionJob` + `WorkerAI` flow.
- Replace direct ad hoc assignment with reservation-based assignment from `WorkManager`.

### Crafting
- Use existing crafting queue as task source.
- Convert queued entries into assignable work packets:
  - Crafter can be fixed to specific member or auto-assigned by priority.

### Guarding
- Add guard posts/areas (designations).
- Guard task behavior:
  - Move to post
  - Hold/defend state
  - Reacquire if displaced

### Digging / Mining
- Add mine designation objects (voxel/node/rock references).
- Mining task execution:
  - Move in range
  - Perform mining ticks
  - Spawn resource output

### Cooking
- Add cooking queue similar to crafting with station requirement.
- Reuse crafting-style duration/progress/inventory transaction concepts.

## 6) Jobs Panel UX Plan

Add a new top tab: Jobs (in the same squad management UI where Formations now exists).

Panel layout:
- Left: member list
- Right: priority matrix (rows=job types, columns=priority values)
- Header: copy preset, enable manual priorities, auto-assign toggle
- Footer: apply to selected, apply to all, reset defaults

Interactions:
- Click cycles Off -> 4 -> 3 -> 2 -> 1 -> Off
- Shift-click applies row priority to all selected members
- Right click opens quick preset menu:
  - Builder
  - Crafter
  - Scavenger
  - Guard
  - Balanced

Visual states:
- Color intensity by priority (1 strongest)
- Disabled rows if worker incapable (e.g., no required skill)
- Warnings for no workers assigned to a critical job type

## 7) Data and Persistence Plan

Add save data schema:
- `JobSystemSaveData`
  - Per-member priority map
  - Global panel settings
  - Optional per-zone guard designations

Add provider:
- `JobSaveProvider : ISaveProvider`
  - Save priority maps and job settings
  - Restore after player/squad spawn but before simulation resumes

Suggested load order:
- After squad identities are restored
- Before AI work loop starts

## 8) Suggested File-Level Implementation Roadmap

### Phase 1: Data + Scheduler Skeleton
- Create domain enums/models
- Implement `WorkManager` with reservation map
- Implement `BuildingWorkTaskProvider` only
- Hook one worker to scheduler (single job type)

### Phase 2: Jobs Tab UI
- Add Jobs tab button/root/controller
- Bind to live squad roster
- Edit and apply per-member priorities
- Display current active task per member

### Phase 3: Expand Task Providers
- Add Looting provider
- Add Crafting provider
- Add Guarding provider
- Add Mining and Cooking providers

### Phase 4: Persistence + Recovery
- Add `JobSystemSaveData`
- Add `JobSaveProvider`
- Restore worker profiles and active reservations safely

### Phase 5: Polish + Debug
- Add debug overlay for task selection reasons
- Add fail-safe stuck detection/reassignment
- Add telemetry logs for task churn and starvation

## 9) Acceptance Criteria
- User can set per-member priorities for all target job types.
- Idle workers autonomously select valid tasks based on priorities.
- Equal-priority tasks resolve with deterministic tie-break logic.
- Workers can switch to urgent higher-priority tasks.
- Priorities persist across save/load.
- No duplicate reservations; no two workers claim same atomic task.

## 10) Immediate First Implementation Slice (Recommended)
Build this vertical slice first:
1. Jobs tab UI with member x job priority matrix.
2. WorkManager + reservation table.
3. Building job provider backed by `ConstructionJob`.
4. One worker brain path that can do Building from priorities.

This gives a full loop quickly and validates the architecture before adding mining/cooking/guarding complexity.
