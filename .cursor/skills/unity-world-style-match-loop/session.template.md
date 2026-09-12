# Style Match Session

Copy to `Library/StyleMatch/session.md` on first run. Update **every iteration** and **every scheduled wake** before exiting.

## Session header (required — parse on every wake)

```yaml
schema: style-match-session/v1
status: running          # running | goal_met | blocked | stopped
mode: style              # style | perf
iteration: 0
max_iterations: 20
pipeline_end: BindWeatherConsumers
reference: reference.png
success_goal: "B>=6, S>=5, overall>=5.5"
best_iteration: null
best_overall: null
last_hypothesis: null
last_capture: null
last_sim: null
next_wake_action: baseline   # baseline | continue | stop
stop_reason: null            # goal_met | max_iterations | mcp_down | compile_blocked | user_stopped
mcp_preflight_ok: null       # true | false — set each wake
last_wake_at: null           # ISO-8601 UTC
pending_steps_count: 0
```

## Style match loop state

- Reference: reference.png
- Planned steps: 0 pending — see living list below
- Pipeline end: BindWeatherConsumers
- Goal: B≥6, S≥5, overall≥5.5
- Iteration: 0 / 20
- Capture: StyleMatchCamera 1920×1080, aerial framing
- Best iteration: none (overall —)
- Last change: (none)
- Status: running

| Iter | Step(s) executed | B | T | S | V | W | A | L | Overall | sim | Errors |
|------|------------------|---|---|---|---|---|---|---|---------|-----|--------|

### Planned steps (living — update every iteration)

```markdown
- [ ] s1 | registry | Run through PaintNaturalSurfaces | T,S | pending
```

## Perf gate (optional)

| Iter | Mode | totalMs | Δ total vs baseline | Slowest regression | Action |
|------|------|---------|---------------------|--------------------|--------|

## Wake log (append one line per scheduled wake)

| wake_at | action | result |
|---------|--------|--------|
