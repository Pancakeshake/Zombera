using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Zombera.Testing
{
    public enum AnimationActionType { Trigger, SetBool, SetFloat, CrossFade }

    [Serializable]
    public class AnimationStep
    {
        public AnimationActionType actionType;
        public string parameterName;
        public float floatValue;
        public bool boolValue;
        [Min(0f)]
        public float waitTime = 2.0f;
    }

    public class AnimationCycler : MonoBehaviour
    {
        [Header("Settings")]
        public bool autoCycle = true;
        public bool loop = true;
        [Min(0.01f)]
        public float defaultInterval = 2f;
        public int currentStepIndex = 0;

        [Header("Sequence")]
        public List<AnimationStep> steps = new List<AnimationStep>();

        private Animator _animator;
        private Coroutine _cycleCoroutine;
        private bool _wasAutoCycle;

        private void Start()
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            // Ensure the game is unpaused for the test scene
            if (Time.timeScale < 0.01f)
            {
                Time.timeScale = 1.0f;
                Debug.Log("[AnimationCycler] Force-unpaused the game (Time.timeScale = 1).");
            }

            _wasAutoCycle = autoCycle;

            if (autoCycle) StartCycle();
        }

        private void OnEnable()
        {
            if (autoCycle) StartCycle();
        }

        private void OnDisable()
        {
            StopCycle();
        }

        private void Update()
        {
            if (_wasAutoCycle == autoCycle) return;

            _wasAutoCycle = autoCycle;

            if (autoCycle)
                StartCycle();
            else
                StopCycle();
        }

        private IEnumerator CycleRoutine()
        {
            while (autoCycle && steps.Count > 0)
            {
                ClampCurrentStepIndex();
                ExecuteCurrentStep();

                var interval = GetCurrentStepInterval();
                yield return new WaitForSeconds(interval);

                if (steps.Count == 0) break;

                if (!loop)
                {
                    if (currentStepIndex >= steps.Count - 1)
                    {
                        autoCycle = false;
                        _wasAutoCycle = false;
                        break;
                    }

                    currentStepIndex++;
                    continue;
                }

                currentStepIndex = (currentStepIndex + 1) % steps.Count;
            }

            _cycleCoroutine = null;
        }

        [ContextMenu("Execute Current Step")]
        public void ExecuteCurrentStep()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || steps.Count == 0) return;

            ClampCurrentStepIndex();
            var step = steps[currentStepIndex];
            Debug.Log($"[AnimationCycler] Step {currentStepIndex}: {step.actionType} '{step.parameterName}'");

            switch (step.actionType)
            {
                case AnimationActionType.Trigger:
                    _animator.SetTrigger(step.parameterName);
                    break;
                case AnimationActionType.SetBool:
                    _animator.SetBool(step.parameterName, step.boolValue);
                    break;
                case AnimationActionType.SetFloat:
                    _animator.SetFloat(step.parameterName, step.floatValue);
                    break;
                case AnimationActionType.CrossFade:
                    // Force the state name directly on layer 0 (Standard Base Layer)
                    _animator.CrossFadeInFixedTime(step.parameterName, 0.25f, 0);
                    break;
            }
        }

        [ContextMenu("Next Step")]
        public void NextStep()
        {
            if (steps.Count == 0) return;

            currentStepIndex = (currentStepIndex + 1) % steps.Count;
            ExecuteCurrentStep();
        }

        [ContextMenu("Previous Step")]
        public void PreviousStep()
        {
            if (steps.Count == 0) return;

            currentStepIndex--;
            if (currentStepIndex < 0) currentStepIndex = steps.Count - 1;
            ExecuteCurrentStep();
        }

        [ContextMenu("Start Cycle")]
        public void StartCycle()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || steps.Count == 0) return;
            if (_cycleCoroutine != null) return;

            ClampCurrentStepIndex();
            _cycleCoroutine = StartCoroutine(CycleRoutine());
        }

        [ContextMenu("Stop Cycle")]
        public void StopCycle()
        {
            if (_cycleCoroutine == null) return;

            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }

        private void ClampCurrentStepIndex()
        {
            if (steps.Count == 0)
            {
                currentStepIndex = 0;
                return;
            }

            if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
                currentStepIndex = 0;
        }

        private float GetCurrentStepInterval()
        {
            if (steps.Count == 0) return Mathf.Max(0.01f, defaultInterval);

            ClampCurrentStepIndex();
            var wait = steps[currentStepIndex].waitTime;
            return wait > 0f ? wait : Mathf.Max(0.01f, defaultInterval);
        }
    }
}