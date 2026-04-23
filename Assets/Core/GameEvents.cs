using UnityEngine;
using Zombera.AI;
using Zombera.Characters;
using Zombera.Combat;
using Zombera.Inventory;
using Zombera.Systems;

namespace Zombera.Core
{
    /// <summary>
    /// Raised when a unit takes damage.
    /// </summary>
    public struct UnitDamagedEvent : IGameEvent
    {
        public string UnitId;
        public UnitRole Role;
        public float Amount;
        public float CurrentHealth;
        public float MaxHealth;
        public Vector3 Position;
        public GameObject UnitObject;
        public GameObject DamageSource;
    }

    /// <summary>
    /// Raised when a unit dies.
    /// </summary>
    public struct UnitDeathEvent : IGameEvent
    {
        public string UnitId;
        public UnitRole Role;
        public Vector3 Position;
        public GameObject UnitObject;
        public GameObject DamageSource;
    }

    /// <summary>
    /// Raised when loot is generated for a container.
    /// </summary>
    public struct LootGeneratedEvent : IGameEvent
    {
        public string ContainerId;
        public LootLocationType LocationType;
        public int ItemCount;
        public float TotalWeight;
        public Vector3 Position;
    }

    /// <summary>
    /// Raised when a zombie is spawned.
    /// </summary>
    public struct ZombieSpawnedEvent : IGameEvent
    {
        public string ZombieTypeId;
        public Vector3 Position;
        public ZombieAI Zombie;
    }

    /// <summary>
    /// Raised when a base structure finishes construction.
    /// </summary>
    public struct BuildingCompletedEvent : IGameEvent
    {
        public string BuildingId;
        public Vector3 Position;
        public GameObject BuildingObject;
    }

    /// <summary>
    /// Raised when a squad command is issued.
    /// </summary>
    public struct SquadCommandIssuedEvent : IGameEvent
    {
        public SquadCommandType CommandType;
        public Vector3 TargetPosition;
        public int MemberCount;
    }

    /// <summary>
    /// Raised on world simulation pulse ticks.
    /// </summary>
    public struct WorldSimulationTickEvent : IGameEvent
    {
        public float DeltaTime;
        public Vector3 PlayerPosition;
    }

    /// <summary>
    /// Raised when a tactical combat encounter starts.
    /// </summary>
    public struct CombatEncounterStartedEvent : IGameEvent
    {
        public int EncounterId;
        public Unit Initiator;
        public Unit Defender;
        public Vector3 Position;
    }

    /// <summary>
    /// Raised at the start of a tactical attack windup, before damage resolves.
    /// </summary>
    public struct CombatAttackWindupEvent : IGameEvent
    {
        public int EncounterId;
        public Unit Attacker;
        public Unit Defender;
        public float WindupSeconds;
        public float HitChance01;
    }

    /// <summary>
    /// Raised once per tactical tick after resolving one attack exchange.
    /// </summary>
    public struct CombatTickResolvedEvent : IGameEvent
    {
        public int EncounterId;
        public Unit Attacker;
        public Unit Defender;
        public CombatAttackStyle AttackStyle;
        public CombatReactionArea PreferredReactionArea;
        public bool DidHit;
        public bool DidDefenderDodge;
        public bool IsCritical;
        public float Damage;
        public float HitChance01;
        public float AttackerStunChance01;
    }

    /// <summary>
    /// Raised when a tactical combat encounter ends.
    /// </summary>
    public struct CombatEncounterEndedEvent : IGameEvent
    {
        public int EncounterId;
        public Unit Winner;
        public Unit Loser;
        public string Reason;
    }

    /// <summary>
    /// Raised when a unit fully loots a container (all items transferred to their inventory).
    /// </summary>
    public struct ContainerLootedEvent : IGameEvent
    {
        public string ContainerId;
        public Vector3 Position;
        public int ItemCount;
        public GameObject LooterObject;
    }

    /// <summary>
    /// Raised when a noise occurs at a world position (gunshot, explosion, shout).
    /// Nearby zombies with a NoiseListener component will investigate the source.
    /// </summary>
    public struct NoiseEvent : IGameEvent
    {
        public Vector3 Position;
        public float Radius;
        public NoiseType NoiseType;
        public GameObject Source;
    }

    public enum NoiseType
    {
        Generic,
        Gunshot,
        Explosion,
        Voice,
    }

    /// <summary>
    /// Raised whenever the GameManager transitions between GameState values.
    /// </summary>
    public struct GameStateChangedEvent : IGameEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    /// <summary>
    /// Raised when a unit's encumbrance tier changes (e.g. Light → Heavy).
    /// </summary>
    public struct EncumbranceChangedEvent : IGameEvent
    {
        public GameObject InventoryObject;
        public EncumbranceState PreviousState;
        public EncumbranceState NewState;
        public float CarryRatio;
    }

    /// <summary>
    /// Raised when a squad member is added to or removed from the active roster.
    /// </summary>
    public struct SquadRosterChangedEvent : IGameEvent
    {
        public SquadMember Member;
        public bool WasAdded; // true = added, false = removed
    }

    /// <summary>
    /// Raised when the recruitment system wants to display a dialogue to the player.
    /// UI listeners subscribe to this to open the dialogue panel.
    /// </summary>
    public struct DialogueRequestedEvent : IGameEvent
    {
        public Systems.DialogueEvent DialogueData;
        public Systems.SurvivorAI Survivor;
    }
}