using UnityEngine;
using Zombera.Characters;

namespace Zombera.Testing
{
    public class AnimationPreviewer : MonoBehaviour
    {
        [SerializeField] private PlayerAnimationController animationController;
        [SerializeField] private Animator animator;
        [SerializeField] private bool autoCycle = false;
        [SerializeField] private float cycleInterval = 4f;

        private float _nextCycleTime;

        private void Awake()
        {
            if (animationController == null) animationController = GetComponent<PlayerAnimationController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (autoCycle && Application.isPlaying && Time.time >= _nextCycleTime)
            {
                PlayRandomAnimation();
                _nextCycleTime = Time.time + cycleInterval;
            }
        }

        [ContextMenu("Play Random Animation")]
        public void PlayRandomAnimation()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) return;

            int mode = Random.Range(0, 5);
            
            if (mode < 3)
            {
                // Trigger common combat animations
                string[] triggers = { "AttackTrigger", "DodgeLeftTrigger", "DodgeRightTrigger", "HitTrigger", "CombatEntryTrigger" };
                string trigger = triggers[Random.Range(0, triggers.Length)];
                animator.SetTrigger(trigger);
                Debug.Log($"[AnimationPreviewer] Triggered: {trigger}");
            }
            else
            {
                // Play a random clip from the folder variants
                if (animationController == null) animationController = GetComponent<PlayerAnimationController>();
                if (animationController != null && animationController.FolderClips != null && animationController.FolderClips.Length > 0)
                {
                    AnimationClip clip = animationController.FolderClips[Random.Range(0, animationController.FolderClips.Length)];
                    animator.CrossFadeInFixedTime(clip.name, 0.25f);
                    Debug.Log($"[AnimationPreviewer] Playing clip: {clip.name}");
                }
            }
        }
        
        [ContextMenu("Stop Auto-Cycle")]
        public void StopAutoCycle() => autoCycle = false;
        
        [ContextMenu("Start Auto-Cycle")]
        public void StartAutoCycle() 
        {
            autoCycle = true;
            _nextCycleTime = Time.time;
        }
    }
}