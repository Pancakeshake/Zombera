#region

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

#endregion

namespace Zombera.Editor
{
    /// <summary>
    ///     Rewires Zombie_Default.controller from clips under <c>Assets/Animations/Zombies</c>.
    ///     Menu: Tools/Utilities/Animation/Rewire Zombie Default Controller
    /// </summary>
    public static class ZombieAnimatorSetup
    {
        private const string ClipsFolder = "Assets/Animations/Zombies";
        private const string ControllerPath = "Assets/Animations/Zombies/Zombie_Default.controller";

        // ── Clip names ───────────────────────────────────────────────────
        // Deaths  : Armature_Death01, Armature_Death02, Death3
        // Attacks : Zombie_Bite (base), Zombie_Scratch (variant)
        // Hits    : Hit_Chest_Light (base), Hit_Head_Light, Hit_Shoulder*, Hit_Stomach_Light, Hit_Knocked Down, Hit_Blown_Away_Heavy
        // Walk    : 8 directional - Fwd/Bwd/L/R + diagonals
        // Other   : Zombie_Idle, Zombie_Spawn

        [MenuItem("Tools/Utilities/Animation/Rewire Zombie Default Controller", priority = -500)]
        public static void RebuildController()
        {
            // Deaths
            var death01 = LoadClip("Death/Armature_Death01");
            var death02 = LoadClip("Death/Armature_Death02");
            var death03 = LoadClip("Death/Armature_Death03");
            if (death03 == null) death03 = LoadClipFromFbx("Death/Death3.fbx", "Death3");
            if (death03 == null) death03 = LoadClipFromFbx("Death/Death3.fbx");

            // Attacks
            var biteClip = LoadClip("Combat/Zombie_Bite");
            var scratchClip = LoadClip("Combat/Zombie_Scratch");

            // Hit reactions (base = Chest, rest are overrideVariants at runtime)
            var hitChest = LoadClip("Hit Reactions/Hit_Chest_Light");
            var hitHead = LoadClip("Hit Reactions/Hit_Head_Light");
            var hitKnockback = LoadClip("Hit Reactions/Hit_Blown_Away_Heavy");
            var hitKnockbackRm = LoadClip("Hit Reactions/Hit_Knocked Down");
            var hitShoulderL = LoadClip("Hit Reactions/Hit_Shoulder_Left_Light");
            var hitShoulderR = LoadClip("Hit Reactions/Hit_Shoulder_Light");
            var hitStomach = LoadClip("Hit Reactions/Hit_Stomach_Light");
            var hitReaction = LoadClipFromFbx("Hit Reactions/Zombie_Reaction.fbx", "Zombie_Reaction");

            // Locomotion
            var walkFwd = LoadClip("Walk/Armature_Zombie_Walk_Fwd_Loop");
            var walkBwd = LoadClip("Walk/Armature_Zombie_Walk_Bwd_Loop");
            var walkL = LoadClip("Walk/Armature_Zombie_Walk_L_Loop");
            var walkR = LoadClip("Walk/Armature_Zombie_Walk_R_Loop");
            var walkFwdL = LoadClip("Walk/Armature_Zombie_Walk_Fwd_L_Loop");
            var walkFwdR = LoadClip("Walk/Armature_Zombie_Walk_Fwd_R_Loop");
            var walkBwdL = LoadClip("Walk/Armature_Zombie_Walk_Bwd_L_Loop");
            var walkBwdR = LoadClip("Walk/Armature_Zombie_Walk_Bwd_R_Loop");

            // Other
            var idleClip = LoadClip("Idle/Zombie_Idle");
            var spawnClip = LoadClip("Actions/Zombie_Spawn");
            var combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx", "Combat_Idle_Zombie");
            if (combatIdleClip == null) combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx");

            if (combatIdleClip == null)
                combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx", "Combat_Idle_Zombie2");

            if (combatIdleClip == null) combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx");

            if (combatIdleClip == null) combatIdleClip = idleClip;

            if (hitChest == null) hitChest = hitReaction;

            // ── Load or create controller (in-place to preserve GUID) ────
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var sm = ctrl.layers[0].stateMachine;

            // Clear existing states
            foreach (var cs in sm.states)
                sm.RemoveState(cs.state);

            sm.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            sm.entryTransitions = Array.Empty<AnimatorTransition>();

            // Remove orphaned BlendTree sub-assets from previous builds
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (subAsset is BlendTree bt)
                    AssetDatabase.RemoveObjectFromAsset(bt);

            // Clear and re-add parameters
            while (ctrl.parameters.Length > 0)
                ctrl.RemoveParameter(0);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AltAttackTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DodgeTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DieTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsInCombat", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("SpawnTrigger", AnimatorControllerParameterType.Trigger);

            // ── States ───────────────────────────────────────────────────
            var idleState = sm.AddState("Idle");
            var combatIdleState = sm.AddState("CombatIdle");
            var spawnState = sm.AddState("Spawn");
            var locoState = sm.AddState("Locomotion");
            var attackState = sm.AddState("Attack"); // Bite
            var attackAltState = sm.AddState("AttackAlt"); // Scratch
            var hitState = sm.AddState("Hit"); // base = HitChest, variants = all Hit_* clips
            var dead1State = sm.AddState("Dead1"); // Death01
            var dead2State = sm.AddState("Dead2"); // Death02
            AnimatorState dead3State = null;
            if (death03 != null) dead3State = sm.AddState("Dead3");

            idleState.motion = idleClip;
            combatIdleState.motion = combatIdleClip;
            spawnState.motion = spawnClip;
            attackState.motion = biteClip;
            attackAltState.motion = scratchClip;
            hitState.motion = hitChest;
            dead1State.motion = death01;
            dead2State.motion = death02;
            if (dead3State != null) dead3State.motion = death03;

            sm.defaultState = idleState;

            // ── Locomotion 2D blend tree (8-directional) ─────────────────
            var locoTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "VelocityX",
                blendParameterY = "VelocityZ"
            };
            AssetDatabase.AddObjectToAsset(locoTree, ctrl);

            if (walkFwd != null) locoTree.AddChild(walkFwd, new Vector2(0f, 1f));
            if (walkBwd != null) locoTree.AddChild(walkBwd, new Vector2(0f, -1f));
            if (walkL != null) locoTree.AddChild(walkL, new Vector2(-1f, 0f));
            if (walkR != null) locoTree.AddChild(walkR, new Vector2(1f, 0f));
            if (walkFwdL != null) locoTree.AddChild(walkFwdL, new Vector2(-0.7f, 0.7f));
            if (walkFwdR != null) locoTree.AddChild(walkFwdR, new Vector2(0.7f, 0.7f));
            if (walkBwdL != null) locoTree.AddChild(walkBwdL, new Vector2(-0.7f, -0.7f));
            if (walkBwdR != null) locoTree.AddChild(walkBwdR, new Vector2(0.7f, -0.7f));

            locoState.motion = locoTree;

            // ── AnyState transitions (priority order matters) ────────────

            // IsDead → Dead1 or Dead2 — controller randomly picks state (50/50 via float)
            // Simpler: use two separate AnyState→Dead transitions driven by IsDead, rely on
            // ZombieAnimationController override randomisation for clip swap, so only one Dead state needed.
            // But user wants two distinct death states, so we wire both with IsDead + random float.
            ctrl.AddParameter("DeathRoll", AnimatorControllerParameterType.Float);

            var tDead1 = sm.AddAnyStateTransition(dead1State);
            tDead1.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            tDead1.AddCondition(AnimatorConditionMode.Less, dead3State != null ? 0.3334f : 0.5f, "DeathRoll");
            tDead1.duration = 0.1f;
            tDead1.hasExitTime = false;
            tDead1.canTransitionToSelf = false;

            var tDead2 = sm.AddAnyStateTransition(dead2State);
            tDead2.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            if (dead3State != null)
            {
                tDead2.AddCondition(AnimatorConditionMode.Greater, 0.3333f, "DeathRoll");
                tDead2.AddCondition(AnimatorConditionMode.Less, 0.6667f, "DeathRoll");
            }
            else
            {
                tDead2.AddCondition(AnimatorConditionMode.Greater, 0.49f, "DeathRoll");
            }

            tDead2.duration = 0.1f;
            tDead2.hasExitTime = false;
            tDead2.canTransitionToSelf = false;

            if (dead3State != null)
            {
                var tDead3 = sm.AddAnyStateTransition(dead3State);
                tDead3.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                tDead3.AddCondition(AnimatorConditionMode.Greater, 0.6666f, "DeathRoll");
                tDead3.duration = 0.1f;
                tDead3.hasExitTime = false;
                tDead3.canTransitionToSelf = false;
            }

            // AttackTrigger → Attack (Bite)
            var tAnyAttack = sm.AddAnyStateTransition(attackState);
            tAnyAttack.AddCondition(AnimatorConditionMode.If, 0, "AttackTrigger");
            tAnyAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyAttack.duration = 0.08f;
            tAnyAttack.hasExitTime = false;
            tAnyAttack.canTransitionToSelf = false;

            // AltAttackTrigger → AttackAlt (Scratch)
            var tAnyAltAttack = sm.AddAnyStateTransition(attackAltState);
            tAnyAltAttack.AddCondition(AnimatorConditionMode.If, 0, "AltAttackTrigger");
            tAnyAltAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyAltAttack.duration = 0.08f;
            tAnyAltAttack.hasExitTime = false;
            tAnyAltAttack.canTransitionToSelf = false;

            // HitTrigger → Hit (HitChest base, all other Hit_* clips are runtime variants)
            var tAnyHit = sm.AddAnyStateTransition(hitState);
            tAnyHit.AddCondition(AnimatorConditionMode.If, 0, "HitTrigger");
            tAnyHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyHit.duration = 0.05f;
            tAnyHit.hasExitTime = false;
            tAnyHit.canTransitionToSelf = false;

            // SpawnTrigger → Spawn
            var tAnySpawn = sm.AddAnyStateTransition(spawnState);
            tAnySpawn.AddCondition(AnimatorConditionMode.If, 0, "SpawnTrigger");
            tAnySpawn.duration = 0.05f;
            tAnySpawn.hasExitTime = false;
            tAnySpawn.canTransitionToSelf = false;

            // ── Normal transitions ────────────────────────────────────────
            // Idle / CombatIdle / Locomotion
            AddTransition(idleState, locoState, (AnimatorConditionMode.Greater, 0.1f));

            var tIdleToCombatIdle = idleState.AddTransition(combatIdleState);
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsInCombat");
            tIdleToCombatIdle.duration = 0.12f;
            tIdleToCombatIdle.hasExitTime = false;

            var tCombatIdleToIdle = combatIdleState.AddTransition(idleState);
            tCombatIdleToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInCombat");
            tCombatIdleToIdle.duration = 0.12f;
            tCombatIdleToIdle.hasExitTime = false;

            var tLocoToIdle = locoState.AddTransition(idleState);
            tLocoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInCombat");
            tLocoToIdle.duration = 0.12f;
            tLocoToIdle.hasExitTime = false;

            var tLocoToCombatIdle = locoState.AddTransition(combatIdleState);
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsInCombat");
            tLocoToCombatIdle.duration = 0.12f;
            tLocoToCombatIdle.hasExitTime = false;

            var tCombatIdleToLoco = combatIdleState.AddTransition(locoState);
            tCombatIdleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tCombatIdleToLoco.duration = 0.12f;
            tCombatIdleToLoco.hasExitTime = false;

            // Attack, AttackAlt, Hit, Spawn → Idle on exit
            AddExitTransition(attackState, idleState, 0.85f, 0.15f);
            AddExitTransition(attackAltState, idleState, 0.85f, 0.15f);
            AddExitTransition(hitState, idleState, 0.8f, 0.1f);
            AddExitTransition(spawnState, idleState, 0.95f, 0.1f);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Zombera] Zombie_Default rebuilt.\n"
                      + "  States    : Idle | CombatIdle | Spawn | Locomotion (8-dir) | Attack | Hit | Dead1 | Dead2"
                      + (dead3State != null ? " | Dead3" : string.Empty) + "\n"
                      + "  Attacks   : Bite (AttackTrigger) — Scratch (AltAttackTrigger), randomly chosen per swing\n"
                      + "  Combat    : IsInCombat drives Idle <-> CombatIdle transitions\n"
                      + "  Hit reacts: Hit_Chest_Light (base) + all hit reaction clips (runtime variants)\n"
                      + (dead3State != null
                          ? "  Deaths    : Death01 / Death02 / Death03 via DeathRoll + runtime death overrides\n"
                          : "  Deaths    : Death01 (DeathRoll < 0.5) | Death02 (DeathRoll >= 0.5), randomised by ZombieAnimationController\n")
                      + "  Walk      : FreeformCartesian2D blend (8 clips)");
        }

        private static AnimationClip LoadClip(string clipPathOrName)
        {
            if (string.IsNullOrWhiteSpace(clipPathOrName)) return null;

            var normalized = clipPathOrName.Replace('\\', '/');
            var path = normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{ClipsFolder}/{normalized}";

            if (!path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) path += ".anim";

            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null) return direct;

            var clipName = normalized;
            var slashIndex = clipName.LastIndexOf('/');
            if (slashIndex >= 0) clipName = clipName.Substring(slashIndex + 1);

            if (clipName.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                clipName = clipName.Substring(0, clipName.Length - 5);

            var allClips = LoadAllZombieAnimationClips();
            for (var i = 0; i < allClips.Count; i++)
            {
                var clip = allClips[i];
                if (clip != null && string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase)) return clip;
            }

            return null;
        }

        private static AnimationClip LoadClipFromFbx(string relativeFbxPath, string preferredClipName = null)
        {
            if (string.IsNullOrWhiteSpace(relativeFbxPath)) return null;

            var fullPath = relativeFbxPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? relativeFbxPath
                : $"{ClipsFolder}/{relativeFbxPath}";

            var assets = AssetDatabase.LoadAllAssetsAtPath(fullPath);
            AnimationClip fallback = null;

            for (var i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip) || IsPreviewClip(clip)) continue;

                if (!string.IsNullOrWhiteSpace(preferredClipName)
                    && string.Equals(clip.name, preferredClipName, StringComparison.OrdinalIgnoreCase))
                    return clip;

                if (fallback == null) fallback = clip;
            }

            return fallback;
        }

        private static List<AnimationClip> LoadAllZombieAnimationClips()
        {
            var clips = new List<AnimationClip>();
            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipsFolder });
            for (var i = 0; i < clipGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (string.IsNullOrWhiteSpace(path) || !visitedPaths.Add(path)) continue;

                AddClipsFromAssetPath(path, clips);
            }

            AddClipsFromAssetPath($"{ClipsFolder}/Hit Reactions/Zombie_Reaction.fbx", clips);
            return clips;
        }

        private static void AddClipsFromAssetPath(string path, List<AnimationClip> destination)
        {
            if (string.IsNullOrWhiteSpace(path) || destination == null) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                var single = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (single != null && !IsPreviewClip(single)) AddUniqueClip(destination, single);

                return;
            }

            for (var i = 0; i < assets.Length; i++)
                if (assets[i] is AnimationClip clip && !IsPreviewClip(clip))
                    AddUniqueClip(destination, clip);
        }

        private static void AddUniqueClip(List<AnimationClip> clips, AnimationClip clip)
        {
            if (clips == null || clip == null || clips.Contains(clip)) return;

            clips.Add(clip);
        }

        private static bool IsPreviewClip(AnimationClip clip)
        {
            return clip != null
                   && clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddTransition(AnimatorState from, AnimatorState to,
            (AnimatorConditionMode mode, float threshold) speed)
        {
            var t = from.AddTransition(to);
            t.AddCondition(speed.mode, speed.threshold, "Speed");
            t.duration = 0.2f;
            t.hasExitTime = false;
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to,
            float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }
    }
}