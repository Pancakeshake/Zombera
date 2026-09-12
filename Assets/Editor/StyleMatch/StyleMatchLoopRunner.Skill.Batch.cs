#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using JBooth.MicroSplat;

namespace Zombera.Editor.StyleMatch
{
    public sealed partial class StyleMatchLoopRunner
    {
        private static SkillBatchState _skillBatch;

        public static bool IsSkillBatchRunning => _skillBatch != null;

        [MenuItem("Tools/World/Style Match/Run 25 Skill Iterations (Auto)")]
        public static void MenuRunTwentyFiveSkillIterations() =>
            StartSkillBatch(0, 25, out _, continueFromLatestCapture: true);

        [MenuItem("Tools/World/Style Match/Run 50 Skill Iterations (Auto)")]
        public static void MenuRunFiftySkillIterations() =>
            StartSkillBatch(0, 50, out _, continueFromLatestCapture: true);

        /// <summary>Runs <paramref name="count"/> skill iterations back-to-back with visual-match mutations.</summary>
        public static bool StartSkillBatch(int count, out string error) =>
            StartSkillBatch(0, count, out error, continueFromLatestCapture: true);

        public static bool StartSkillBatch(
            int planOffset,
            int count,
            out string error,
            bool continueFromLatestCapture = true)
        {
            error = null;
            if (IsSkillBatchRunning)
            {
                error = "Skill batch is already running.";
                return false;
            }

            if (IsRunning)
            {
                error = "Deprecated batch loop is running; stop it first.";
                return false;
            }

            if (count <= 0)
            {
                error = "count must be positive.";
                return false;
            }

            var startIter = continueFromLatestCapture ? FindLatestCapturedIteration() + 1 : 1;
            if (startIter + count - 1 > DefaultSkillMaxIterations)
            {
                error = "Would exceed max skill iterations (" + DefaultSkillMaxIterations + ").";
                return false;
            }

            EnsureSkillSessionFiles();
            _skillBatch = new SkillBatchState
            {
                StartIteration = startIter,
                CurrentIteration = startIter,
                EndIteration = startIter + count - 1,
                PlanOffset = planOffset,
                Phase = SkillBatchPhase.WaitReady,
            };

            EditorApplication.update += SkillBatchTick;
            WriteStatus("skill-batch starting iter " + startIter + "–" + _skillBatch.EndIteration);
            Debug.Log("[StyleMatch] Skill batch started: iter " + startIter + " to " + _skillBatch.EndIteration);
            return true;
        }

        public static bool StopSkillBatch(out string error)
        {
            error = null;
            if (!IsSkillBatchRunning)
            {
                error = "No skill batch is running.";
                return false;
            }

            ShutdownSkillBatch("stopped by user");
            return true;
        }

        private static void SkillBatchTick()
        {
            if (_skillBatch == null)
                return;

            try
            {
                switch (_skillBatch.Phase)
                {
                    case SkillBatchPhase.WaitReady:
                        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                            return;
                        _skillBatch.Phase = SkillBatchPhase.Mutate;
                        break;

                    case SkillBatchPhase.Mutate:
                        if (!RunSkillBatchMutation(_skillBatch))
                        {
                            AppendSessionLog(
                                _skillBatch.CurrentIteration,
                                "change: (mutation failed) | test: skipped",
                                "fail");
                            if (_skillBatch.CurrentIteration >= _skillBatch.EndIteration)
                            {
                                ShutdownSkillBatch("completed with mutation failures");
                                return;
                            }

                            _skillBatch.CurrentIteration++;
                            _skillBatch.Phase = SkillBatchPhase.WaitReady;
                            return;
                        }

                        _skillBatch.Phase = SkillBatchPhase.RunIteration;
                        break;

                    case SkillBatchPhase.RunIteration:
                        RunSkillBatchIteration(_skillBatch);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[StyleMatch] Skill batch error at iter " + _skillBatch.CurrentIteration + ": " + ex);
                ShutdownSkillBatch("error: " + ex.Message);
            }
        }

        private static bool RunSkillBatchMutation(SkillBatchState batch)
        {
            var slot = batch.PlanOffset + (batch.CurrentIteration - batch.StartIteration);
            var (ok, summary) = ApplySkillVisualMutation(slot);
            batch.LastMutation = summary;
            Debug.Log(
                "[StyleMatch] Skill batch iter " + batch.CurrentIteration +
                " mutation" + (ok ? "" : " FAILED") + ": " + summary);
            return ok;
        }

        private static void RunSkillBatchIteration(SkillBatchState batch)
        {
            var options = StyleMatchSkillIterationOptions.PipelineRunAfterChange(batch.LastMutation);

            if (!TryRunSkillIteration(batch.CurrentIteration, options, out var result, out var error))
            {
                Debug.LogWarning(
                    "[StyleMatch] Skill batch iter " + batch.CurrentIteration + " failed: " + error);
                AppendSessionLog(batch.CurrentIteration, "batch:" + batch.LastMutation, "fail: " + error);
            }
            else
            {
                Debug.Log(
                    "[StyleMatch] Skill batch iter " + batch.CurrentIteration +
                    " ok — " + result.CaptureBytes + " bytes");
            }

            if (batch.CurrentIteration >= batch.EndIteration)
            {
                ShutdownSkillBatch("completed iter " + batch.StartIteration + "–" + batch.EndIteration);
                return;
            }

            batch.CurrentIteration++;
            batch.Phase = SkillBatchPhase.WaitReady;
        }

        private static void ShutdownSkillBatch(string reason)
        {
            EditorApplication.update -= SkillBatchTick;
            WriteStatus("skill-batch " + reason);
            Debug.Log("[StyleMatch] Skill batch finished: " + reason);
            _skillBatch = null;
        }

        private sealed class SkillBatchState
        {
            public int StartIteration;
            public int CurrentIteration;
            public int EndIteration;
            public int PlanOffset;
            public SkillBatchPhase Phase;
            public string LastMutation = "none";
        }

        private enum SkillBatchPhase
        {
            WaitReady,
            Mutate,
            RunIteration,
        }

        private const int SkillMutationSlotCount = 25;

        private static (bool ok, string summary) ApplySkillVisualMutation(int planSlot)
        {
            var slot = ModPositive(planSlot, SkillMutationSlotCount);
            var cycle = planSlot / SkillMutationSlotCount;
            var scale = 1f + cycle * 0.12f;

            try
            {
                return slot switch
                {
                    0 => Mut0_MountainDefaults(),
                    1 => Mut1_SnowMin(-22f * scale),
                    2 => Mut2_CliffSnowWeight(0.035f * scale),
                    3 => Mut3_RockMin(-18f * scale),
                    4 => Mut4_CliffContrast(0.09f * scale),
                    5 => Mut5_ForestDensity(1f + 0.12f * scale),
                    6 => Mut6_SnowFull(-35f * scale),
                    7 => Mut7_SnowLayerBrightness(0.06f * scale),
                    8 => Mut8_GrassNoise(-5f * scale),
                    9 => Mut9_RockFull(22f * scale),
                    10 => Mut10_NatureDensity(1f + 0.08f * scale),
                    11 => Mut11_ValleyFog(),
                    12 => Mut12_DirtBandLower(-15f * scale),
                    13 => Mut13_HeightContrast(0.08f * scale),
                    14 => Mut14_ForestDensity(1f + 0.1f * scale),
                    15 => Mut15_CliffSnowWeight(0.025f * scale),
                    16 => Mut16_RockMin(-12f * scale),
                    17 => Mut17_TriplanarCliffs(0.08f * scale),
                    18 => Mut18_SnowFull(-25f * scale),
                    19 => Mut19_RockNoiseScale(-3f * scale),
                    20 => Mut20_NatureDensity(1f + 0.06f * scale),
                    21 => Mut21_SnowMin(-15f * scale),
                    22 => Mut22_CompileMicroSplat(),
                    23 => Mut23_RockFull(18f * scale),
                    24 => Mut24_InterpolationContrast(0.05f * scale),
                    _ => (false, "noop slot"),
                };
            }
            catch (Exception ex)
            {
                return (false, "FAILED: " + ex.Message);
            }
        }

        private static int ModPositive(int value, int mod)
        {
            var r = value % mod;
            return r < 0 ? r + mod : r;
        }

        private static (bool ok, string summary) Mut0_MountainDefaults()
        {
            var ok = StyleMatchPainterMutations.ApplyMountainElevationBandDefaults();
            ok &= StyleMatchPainterMutations.SetFloat("_dirtBandMinElevationMeters", 200f, 80f, 400f);
            ok &= StyleMatchPainterMutations.SetFloat("_dirtBandFullElevationMeters", 290f, 120f, 500f);
            return (ok, "mountain+dirt band defaults");
        }

        private static (bool ok, string summary) Mut1_SnowMin(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_snowMinElevationMeters", delta, 400f, 700f);
            return (ok, "snow min " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut2_CliffSnowWeight(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_cliffSnowMaxWeight", delta, 0.2f, 0.88f);
            return (ok, "cliff snow weight +" + delta.ToString("F3"));
        }

        private static (bool ok, string summary) Mut3_RockMin(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_rockMinElevationMeters", delta, 350f, 700f);
            return (ok, "rock min " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut4_CliffContrast(float delta)
        {
            var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.Contrast, delta, 5, 10, 0f, 2f);
            return (ok, "cliff contrast +" + delta.ToString("F3"));
        }

        private static (bool ok, string summary) Mut5_ForestDensity(float mult) =>
            MutForestDensity(mult, "forest/hills");

        private static (bool ok, string summary) Mut6_SnowFull(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_snowFullElevationMeters", delta, 600f, 950f);
            return (ok, "snow full " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut7_SnowLayerBrightness(float delta)
        {
            var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.Brightness, delta, 10, 10, 0f, 2f);
            return (ok, "snow brightness +" + delta.ToString("F3"));
        }

        private static (bool ok, string summary) Mut8_GrassNoise(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_mainGrassNoiseScaleMeters", delta, 24f, 120f);
            return (ok, "grass noise " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut9_RockFull(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_rockFullElevationMeters", delta, 750f, 1050f);
            return (ok, "rock full +" + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut10_NatureDensity(float mult)
        {
            var ok = StyleMatchProfileMutations.ScaleNatureDensity(mult);
            return (ok, "nature density x" + mult.ToString("F2"));
        }

        private static (bool ok, string summary) Mut11_ValleyFog()
        {
            var ok = StyleMatchEnviroSetup.SetClearMidday();
            return (ok, "valley fog + clear midday");
        }

        private static (bool ok, string summary) Mut12_DirtBandLower(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_dirtBandMinElevationMeters", delta, 80f, 400f);
            ok &= StyleMatchPainterMutations.AddFloat("_dirtBandFullElevationMeters", delta, 120f, 500f);
            return (ok, "dirt band " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut13_HeightContrast(float delta)
        {
            var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.HeightContrast, delta, 5, 10, 0f, 2f);
            return (ok, "cliff height contrast +" + delta.ToString("F3"));
        }

        private static (bool ok, string summary) Mut14_ForestDensity(float mult) =>
            MutForestDensity(mult, "forest");

        private static (bool ok, string summary) MutForestDensity(float mult, string label)
        {
            var ok = StyleMatchProfileMutations.BoostBiomeNatureDensity(
                new[] { "Forest", "Hills", "Plains" }, mult);
            return (ok, label + " trees x" + mult.ToString("F2"));
        }

        private static (bool ok, string summary) Mut15_CliffSnowWeight(float delta) => Mut2_CliffSnowWeight(delta);

        private static (bool ok, string summary) Mut16_RockMin(float delta) => Mut3_RockMin(delta);

        private static (bool ok, string summary) Mut17_TriplanarCliffs(float delta)
        {
            var ok = StyleMatchMicroSplatMutations.BumpFloatOnLayers(
                MicroSplatPropData.PerTexFloat.TriplanarContrast, delta, 5, 10, 0f, 2f);
            return (ok, "cliff triplanar +" + delta.ToString("F3"));
        }

        private static (bool ok, string summary) Mut18_SnowFull(float delta) => Mut6_SnowFull(delta);

        private static (bool ok, string summary) Mut19_RockNoiseScale(float delta)
        {
            var ok = StyleMatchPainterMutations.AddFloat("_rockExposureNoiseScaleMeters", delta, 8f, 96f);
            return (ok, "rock noise scale " + (delta > 0 ? "+" : "") + delta.ToString("F0") + "m");
        }

        private static (bool ok, string summary) Mut20_NatureDensity(float mult) => Mut10_NatureDensity(mult);

        private static (bool ok, string summary) Mut21_SnowMin(float delta) => Mut1_SnowMin(delta);

        private static (bool ok, string summary) Mut22_CompileMicroSplat()
        {
            var ok = StyleMatchMicroSplatMutations.CompileWorldConfig();
            return (ok, "compile MicroSplat arrays");
        }

        private static (bool ok, string summary) Mut23_RockFull(float delta) => Mut9_RockFull(delta);

        private static (bool ok, string summary) Mut24_InterpolationContrast(float delta)
        {
            var ok = StyleMatchMicroSplatMutations.BumpFloat(
                MicroSplatPropData.PerTexFloat.InterpolationContrast, delta, 0f, 2.5f);
            return (ok, "interpolation contrast +" + delta.ToString("F3"));
        }
    }
}
#endif
