#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JBooth.MicroSplat;

namespace Zombera.Editor.StyleMatch
{
    /// <summary>Agent-curated sequential iterations — one hypothesis + pipeline+capture each step.</summary>
    public sealed partial class StyleMatchLoopRunner
    {
        private static AgentMarathonState _agentMarathon;

        public static bool IsAgentMarathonRunning => _agentMarathon != null;

        public static bool StartAgentMarathon(int count, out string error)
        {
            error = "Agent marathon disabled — use strict agent-in-the-loop (one hypothesis → capture → compare per turn).";
            return false;
        }

        public static bool StopAgentMarathon(out string error)
        {
            error = null;
            if (!IsAgentMarathonRunning)
            {
                error = "No agent marathon running.";
                return false;
            }

            ShutdownAgentMarathon("stopped by user");
            return true;
        }

        private static void AgentMarathonTick()
        {
            if (_agentMarathon == null)
                return;

            try
            {
                switch (_agentMarathon.Phase)
                {
                    case AgentMarathonPhase.WaitReady:
                        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                            return;

                        var scenePath = "Assets/00_Scenes/02_System Dev Scenes/World Generation.unity";
                        if (EditorSceneManager.GetActiveScene().path != scenePath)
                            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                        _agentMarathon.Phase = AgentMarathonPhase.Mutate;
                        break;

                    case AgentMarathonPhase.Mutate:
                        if (!RunAgentMarathonMutation(_agentMarathon))
                        {
                            AppendSessionLog(
                                _agentMarathon.CurrentIteration,
                                "agent: (mutation failed) " + _agentMarathon.LastNote,
                                "fail");
                            AdvanceAgentMarathonOrFinish();
                            return;
                        }

                        _agentMarathon.Phase = AgentMarathonPhase.RunIteration;
                        break;

                    case AgentMarathonPhase.RunIteration:
                        RunAgentMarathonIteration(_agentMarathon);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[StyleMatch] Agent marathon error at iter " + _agentMarathon.CurrentIteration + ": " + ex);
                ShutdownAgentMarathon("error: " + ex.Message);
            }
        }

        private static bool RunAgentMarathonMutation(AgentMarathonState state)
        {
            var step = state.StepIndex % AgentMarathonSteps.Count;
            var (ok, note) = AgentMarathonSteps.Apply(step);
            state.LastNote = note;
            Debug.Log(
                "[StyleMatch] Agent marathon iter " + state.CurrentIteration +
                " step " + step + (ok ? "" : " FAILED") + ": " + note);
            return ok;
        }

        private static void RunAgentMarathonIteration(AgentMarathonState state)
        {
            var options = StyleMatchSkillIterationOptions.PipelineRunAfterChange(state.LastNote);

            if (!TryRunSkillIteration(state.CurrentIteration, options, out var result, out var error))
            {
                Debug.LogWarning("[StyleMatch] Agent marathon iter " + state.CurrentIteration + " failed: " + error);
                AppendSessionLog(state.CurrentIteration, "agent:" + state.LastNote, "fail: " + error);
            }
            else
            {
                Debug.Log(
                    "[StyleMatch] Agent marathon iter " + state.CurrentIteration +
                    " ok — " + result.CaptureBytes + " bytes");
                AppendSessionLog(state.CurrentIteration, "agent:" + state.LastNote, "ok");
            }

            state.StepIndex++;
            AdvanceAgentMarathonOrFinish();
        }

        private static void AdvanceAgentMarathonOrFinish()
        {
            if (_agentMarathon.StepIndex >= AgentMarathonSteps.Count)
            {
                ShutdownAgentMarathon("completed " + AgentMarathonSteps.Count + " steps");
                return;
            }

            _agentMarathon.CurrentIteration++;
            _agentMarathon.Phase = AgentMarathonPhase.WaitReady;
        }

        private static void ShutdownAgentMarathon(string reason)
        {
            EditorApplication.update -= AgentMarathonTick;
            WriteStatus("agent-marathon " + reason);
            Debug.Log("[StyleMatch] Agent marathon finished: " + reason);
            _agentMarathon = null;
        }

        private sealed class AgentMarathonState
        {
            public int CurrentIteration;
            public int StepIndex;
            public AgentMarathonPhase Phase;
            public string LastNote = "none";
        }

        private enum AgentMarathonPhase
        {
            WaitReady,
            Mutate,
            RunIteration,
        }

        private static class AgentMarathonSteps
        {
            public static int Count => _steps.Length;

            private static readonly Func<(bool ok, string note)>[] _steps =
            {
                StepMountainDefaults,
                () => MutForest(1.35f, "valley forest density x1.35"),
                () => MutAddFloat("_beachInlandBlendMeters", 12f, 8f, 48f, "widen beach inland blend +12m"),
                () => MutAddFloat("_snowMinElevationMeters", -28f, 400f, 700f, "lower snow line -28m"),
                () => MutEnviro("valley fog + clear midday"),
                () => MutAddFloat("_dirtBandMinElevationMeters", -18f, 80f, 400f, "valley dirt band -18m"),
                () => MutAddFloat("_mainGrassNoiseScaleMeters", -10f, 24f, 120f, "tighter valley grass noise -10m"),
                () => MutAddFloat("_rockMinElevationMeters", -22f, 250f, 500f, "rock band starts lower -22m"),
                () => MutCliffContrast(0.09f),
                () => MutForest(1.25f, "hills/plains trees x1.25"),
                () => MutAddFloat("_snowFullElevationMeters", -32f, 600f, 950f, "snow full -32m"),
                () => MutAddFloat("_beachMaxElevationAboveSea", 1.5f, 2f, 12f, "beach elevation +1.5m"),
                () => MutCompileMicroSplat(),
                () => MutSnowBrightness(0.07f),
                () => MutAddFloat("_rockFullElevationMeters", 25f, 450f, 700f, "rock full +25m"),
                () => MutNatureGlobal(1.2f),
                () => MutTriplanar(0.09f),
                () => MutAddFloat("_rockExposureNoiseScaleMeters", -6f, 8f, 96f, "rock noise scale -6m"),
                () => MutInterpolationContrast(0.06f),
                () => MutAddFloat("_cliffSnowMaxWeight", 0.04f, 0.2f, 0.88f, "cliff snow weight +0.04"),
            };

            public static (bool ok, string note) Apply(int index) =>
                _steps[index % _steps.Length]();

            private static (bool ok, string note) StepMountainDefaults()
            {
                var ok = StyleMatchPainterMutations.ApplyMountainElevationBandDefaults();
                ok &= StyleMatchPainterMutations.SetFloat("_dirtBandMinElevationMeters", 200f, 80f, 400f);
                ok &= StyleMatchPainterMutations.SetFloat("_dirtBandFullElevationMeters", 290f, 120f, 500f);
                return (ok, "mountain+dirt band defaults (agent)");
            }

            private static (bool ok, string note) MutForest(float mult, string label)
            {
                var ok = StyleMatchProfileMutations.BoostBiomeNatureDensity(
                    new[] { "Forest", "Hills", "Plains" }, mult);
                return (ok, label);
            }

            private static (bool ok, string note) MutNatureGlobal(float mult)
            {
                var ok = StyleMatchProfileMutations.ScaleNatureDensity(mult);
                return (ok, "global nature density x" + mult.ToString("F2"));
            }

            private static (bool ok, string note) MutAddFloat(
                string field, float delta, float min, float max, string label)
            {
                var ok = StyleMatchPainterMutations.AddFloat(field, delta, min, max);
                return (ok, label);
            }

            private static (bool ok, string note) MutEnviro(string label)
            {
                var ok = StyleMatchEnviroSetup.SetClearMidday();
                return (ok, label);
            }

            private static (bool ok, string note) MutCliffContrast(float delta)
            {
                var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                    MicroSplatPropData.PerTexFloat.Contrast, delta, 5, 10, 0f, 2f);
                return (ok, "cliff contrast +" + delta.ToString("F3"));
            }

            private static (bool ok, string note) MutSnowBrightness(float delta)
            {
                var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                    MicroSplatPropData.PerTexFloat.Brightness, delta, 10, 10, 0f, 2f);
                return (ok, "snow brightness +" + delta.ToString("F3"));
            }

            private static (bool ok, string note) MutTriplanar(float delta)
            {
                var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                    MicroSplatPropData.PerTexFloat.TriplanarContrast, delta, 5, 10, 0f, 2f);
                return (ok, "cliff triplanar +" + delta.ToString("F3"));
            }

            private static (bool ok, string note) MutInterpolationContrast(float delta)
            {
                var ok = StyleMatchMicroSplatMutations.BumpFloat(
                    MicroSplatPropData.PerTexFloat.InterpolationContrast, delta, 0f, 2.5f);
                return (ok, "interpolation contrast +" + delta.ToString("F3"));
            }

            private static (bool ok, string note) MutCompileMicroSplat()
            {
                var ok = StyleMatchMicroSplatMutations.CompileWorldConfig();
                return (ok, "compile MicroSplat arrays");
            }
        }
    }
}
#endif
