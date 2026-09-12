using UnityEngine;
using System.Collections;

namespace Zombera.Testing
{
    /// <summary>
    /// Drives random animator parameters and triggers in Play Mode to test character movement and clothing fit.
    /// </summary>
    public class AnimationRandomizer : MonoBehaviour
    {
        [Header("Settings")]
        public bool testMode = true;
        public float actionInterval = 3.0f;

        private Animator _animator;
        
        // Parameter Hashes
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VelocityX = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZ = Animator.StringToHash("VelocityZ");
        private static readonly int AttackTrigger = Animator.StringToHash("AttackTrigger");
        private static readonly int HitTrigger = Animator.StringToHash("HitTrigger");
        private static readonly int IsInCombat = Animator.StringToHash("IsInCombat");
        private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");

        private void Start()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            
            if (testMode) StartCoroutine(RandomActionRoutine());
        }

        private void Update()
        {
            if (!testMode || _animator == null) return;

            // Simulate slight movement/sway to test blend trees
            float targetSpeed = Mathf.PingPong(Time.time * 0.3f, 0.4f);
            _animator.SetFloat(Speed, targetSpeed);
            _animator.SetFloat(VelocityX, Mathf.Sin(Time.time * 0.5f) * 0.3f);
            _animator.SetFloat(VelocityZ, Mathf.Cos(Time.time * 0.5f) * 0.3f);
        }

        private IEnumerator RandomActionRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(actionInterval);
                if (!testMode || _animator == null) continue;

                // Randomly trigger an action or posture change
                int roll = Random.Range(0, 4);
                switch (roll)
                {
                    case 0:
                        _animator.SetTrigger(AttackTrigger);
                        break;
                    case 1:
                        _animator.SetTrigger(HitTrigger);
                        break;
                    case 2:
                        _animator.SetBool(IsInCombat, !_animator.GetBool(IsInCombat));
                        break;
                    case 3:
                        _animator.SetBool(IsCrouching, !_animator.GetBool(IsCrouching));
                        break;
                }
            }
        }
    }
}