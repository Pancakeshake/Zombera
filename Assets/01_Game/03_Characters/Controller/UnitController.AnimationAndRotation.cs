#region

using UnityEngine;

#endregion

namespace Zombera.Characters
{
    public sealed partial class UnitController
    {
        public void Rotate(Vector3 direction)
        {
            var planarDirection = new Vector3(direction.x, 0f, direction.z);

            if (planarDirection.sqrMagnitude <= 0.0001f) return;

            var targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        public void FacePositionInstant(Vector3 worldPosition)
        {
            var planarDirection = worldPosition - transform.position;
            planarDirection.y = 0f;

            if (planarDirection.sqrMagnitude <= 0.0001f) return;

            transform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
        }

        public void RotateTowardsPosition(Vector3 worldPosition, float maxDegreesPerSecond)
        {
            var planarDirection = worldPosition - transform.position;
            planarDirection.y = 0f;

            if (planarDirection.sqrMagnitude <= 0.0001f) return;

            var targetRotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            var turnSpeed = Mathf.Max(0f, maxDegreesPerSecond);

            if (turnSpeed <= 0f)
            {
                transform.rotation = targetRotation;
                return;
            }

            transform.rotation =
                Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        /// <summary>
        ///     Sets the animator Speed parameter. Fetches the Animator lazily because
        ///     character visuals (and Animator) can initialize asynchronously after spawn.
        /// </summary>
        private void UpdateAnimator(float speed)
        {
            if (_animator == null)
            {
                var now = Time.unscaledTime;
                if (now < _nextAnimatorResolveAt) return;

                _nextAnimatorResolveAt = now + AnimatorResolveRetrySeconds;
                _animator = GetComponentInChildren<Animator>();
                if (_animator == null) return;
            }

            if (speed <= 0.01f || !IsMoving)
            {
                _animator.SetFloat(SpeedHash, 0f);
                return;
            }

            // Smooth damp so blend tree transitions feel natural.
            _animator.SetFloat(SpeedHash, speed, animDampTime, Time.deltaTime);
        }
    }
}
