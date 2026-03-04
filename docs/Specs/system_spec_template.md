# SYSTEM SPECIFICATION

System Name:
[Example: ZombieAI]

System Type:
Gameplay System / Manager / Component / UI

---

# PURPOSE

Describe what the system does.

Example:

ZombieAI controls zombie behavior including detecting targets,
chasing the player, and attacking when in range.

---

# RELATED SYSTEMS

List other systems this interacts with.

Example:

UnitController
UnitHealth
CombatManager
WorldManager

---

# COMPONENTS

List scripts that belong to this system.

Example:

ZombieAI
ZombieStateMachine
ZombieSensors

---

# RESPONSIBILITIES

Define what the system must handle.

Example:

Detect nearby players or survivors.

Choose behavior based on distance.

Execute chase and attack logic.

Trigger combat when in range.

---

# DATA STRUCTURES

Define important variables.

Example:

detectionRadius
attackRange
currentTarget
currentState

---

# STATES (if applicable)

Example:

Idle
Wander
Chase
Attack

---

# STATE TRANSITIONS

Define how the system changes behavior.

Example:

Idle → Wander after random time.

Wander → Chase when player detected.

Chase → Attack when target within attack range.

Attack → Chase if target moves away.

---

# EVENTS

Define important events.

Example:

OnPlayerDetected
OnTargetLost
OnZombieAttack
OnZombieDeath

---

# PUBLIC METHODS

Define external functions.

Example:

SetTarget(Unit target)
TakeDamage(float amount)
Die()

---

# PERFORMANCE REQUIREMENTS

Define performance constraints.

Example:

AI must update using tick system.

Update interval:
0.4 seconds.

Avoid heavy logic in Update().

---

# DEBUG SUPPORT

Debug features required.

Example:

Show AI state above zombie.

Draw detection radius.

Log state transitions.

---

# FUTURE EXPANSION

Define possible upgrades.

Example:

Horde coordination.

Special zombie abilities.

Noise detection.

Group pathfinding.
