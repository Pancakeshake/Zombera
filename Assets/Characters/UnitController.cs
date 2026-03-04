using UnityEngine;

namespace Zombera.Characters
{
    /// <summary>
    /// Handles unit movement and input routing for player-controlled and AI-driven units.
    /// </summary>
    public sealed class UnitController : MonoBehaviour
    {
        [SerializeField] private UnitRole role = UnitRole.Player;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float stoppingDistance = 0.15f;
        [SerializeField] private bool useRigidbodyMovement;
        [SerializeField] private Rigidbody movementBody;

        public UnitRole Role => role;
        public bool InputEnabled { get; private set; } = true;
        public Vector2 MoveInput { get; private set; }
        public Vector3 MoveTarget { get; private set; }
        public bool HasMoveTarget { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector3 desiredMoveDirection;

        private void Awake()
        {
            if (movementBody == null)
            {
                movementBody = GetComponent<Rigidbody>();
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;

            if (!enabled)
            {
                MoveInput = Vector2.zero;
            }

            // TODO: Lock local input while preserving AI/pathing control.
        }

        public void SetRole(UnitRole unitRole)
        {
            role = unitRole;
        }

        public void SetMoveInput(Vector2 input)
        {
            MoveInput = input;

            if (input.sqrMagnitude > 0f)
            {
                HasMoveTarget = false;
            }

            // TODO: Normalize against camera-relative movement settings.
        }

        public void MoveTo(Vector3 worldPosition)
        {
            MoveTarget = worldPosition;
            HasMoveTarget = true;
            IsMoving = true;

            // TODO: Integrate NavMesh/pathfinding movement command.
        }

        public void Stop()
        {
            MoveInput = Vector2.zero;
            HasMoveTarget = false;
            IsMoving = false;
            desiredMoveDirection = Vector3.zero;
        }

        public void Rotate(Vector3 direction)
        {
            Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z);

            if (planarDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private void Update()
        {
            if (!InputEnabled && role == UnitRole.Player)
            {
                IsMoving = false;
                return;
            }

            desiredMoveDirection = ResolveDesiredDirection();

            if (desiredMoveDirection.sqrMagnitude <= 0.0001f)
            {
                IsMoving = false;
                return;
            }

            IsMoving = true;
            Vector3 movementDelta = desiredMoveDirection.normalized * moveSpeed * Time.deltaTime;
            ApplyMovement(movementDelta);
            Rotate(desiredMoveDirection);

            // TODO: Apply acceleration curves and animation speed blending.
            // TODO: Replace direct movement with path steering when required.
        }

        private Vector3 ResolveDesiredDirection()
        {
            if (role == UnitRole.Player && MoveInput.sqrMagnitude > 0.0001f)
            {
                return new Vector3(MoveInput.x, 0f, MoveInput.y);
            }

            if (!HasMoveTarget)
            {
                return Vector3.zero;
            }

            Vector3 toTarget = MoveTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= stoppingDistance * stoppingDistance)
            {
                Stop();
                return Vector3.zero;
            }

            return toTarget;
        }

        private void ApplyMovement(Vector3 movementDelta)
        {
            if (useRigidbodyMovement && movementBody != null)
            {
                movementBody.MovePosition(movementBody.position + movementDelta);
                return;
            }

            transform.position += movementDelta;
        }
    }

    /// <summary>
    /// Supported unit archetypes for shared character systems.
    /// </summary>
    public enum UnitRole
    {
        Player,
        SquadMember,
        Survivor,
        Enemy,
        Zombie,
        Bandit
    }
}