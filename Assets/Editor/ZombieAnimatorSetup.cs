using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Zombera.AI;

namespace Zombera.Editor
{
    /// <summary>
    /// Rebuilds Zombie_Default.controller and wires ZombieAnimationController clip assignments.
    /// Menus:
    ///   Tools/Zombera/Animation/Rebuild Zombie Default Controller
    ///   Tools/Zombera/Animation/Wire Selected Zombie Components
    /// </summary>
    public static class ZombieAnimatorSetup
    {
        private const string ClipsFolder    = "Assets/Animations/Zombies";
        private const string ControllerPath = "Assets/Animations/Zombies/Zombie_Default.controller";

        // ── Clip names ───────────────────────────────────────────────────
        // Deaths  : Armature_Death01, Armature_Death02, Death3
        // Attacks : Zombie_Bite (base), Zombie_Scratch (variant)
        // Hits    : Hit_Chest_Light (base), Hit_Head_Light, Hit_Shoulder*, Hit_Stomach_Light, Hit_Knocked Down, Hit_Blown_Away_Heavy
        // Walk    : 8 directional - Fwd/Bwd/L/R + diagonals
        // Other   : Zombie_Idle, Zombie_Spawn

        [MenuItem("Tools/Zombera/Animation/Rebuild Zombie Default Controller")]
        public static void RebuildController()
        {
            // Deaths
            AnimationClip death01   = LoadClip("Death/Armature_Death01");
            AnimationClip death02   = LoadClip("Death/Armature_Death02");
            AnimationClip death03   = LoadClip("Death/Armature_Death03");
            if (death03 == null)
            {
                death03 = LoadClipFromFbx("Death/Death3.fbx", "Death3");
            }
            if (death03 == null)
            {
                death03 = LoadClipFromFbx("Death/Death3.fbx");
            }

            // Attacks
            AnimationClip biteClip    = LoadClip("Combat/Zombie_Bite");
            AnimationClip scratchClip = LoadClip("Combat/Zombie_Scratch");

            // Hit reactions (base = Chest, rest are overrideVariants at runtime)
            AnimationClip hitChest       = LoadClip("Hit Reactions/Hit_Chest_Light");
            AnimationClip hitHead        = LoadClip("Hit Reactions/Hit_Head_Light");
            AnimationClip hitKnockback   = LoadClip("Hit Reactions/Hit_Blown_Away_Heavy");
            AnimationClip hitKnockbackRm = LoadClip("Hit Reactions/Hit_Knocked Down");
            AnimationClip hitShoulderL   = LoadClip("Hit Reactions/Hit_Shoulder_Left_Light");
            AnimationClip hitShoulderR   = LoadClip("Hit Reactions/Hit_Shoulder_Light");
            AnimationClip hitStomach     = LoadClip("Hit Reactions/Hit_Stomach_Light");
            AnimationClip hitReaction    = LoadClipFromFbx("Hit Reactions/Zombie_Reaction.fbx", "Zombie_Reaction");

            // Locomotion
            AnimationClip walkFwd  = LoadClip("Walk/Armature_Zombie_Walk_Fwd_Loop");
            AnimationClip walkBwd  = LoadClip("Walk/Armature_Zombie_Walk_Bwd_Loop");
            AnimationClip walkL    = LoadClip("Walk/Armature_Zombie_Walk_L_Loop");
            AnimationClip walkR    = LoadClip("Walk/Armature_Zombie_Walk_R_Loop");
            AnimationClip walkFwdL = LoadClip("Walk/Armature_Zombie_Walk_Fwd_L_Loop");
            AnimationClip walkFwdR = LoadClip("Walk/Armature_Zombie_Walk_Fwd_R_Loop");
            AnimationClip walkBwdL = LoadClip("Walk/Armature_Zombie_Walk_Bwd_L_Loop");
            AnimationClip walkBwdR = LoadClip("Walk/Armature_Zombie_Walk_Bwd_R_Loop");

            // Other
            AnimationClip idleClip  = LoadClip("Idle/Zombie_Idle");
            AnimationClip spawnClip = LoadClip("Actions/Zombie_Spawn");
            AnimationClip combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx", "Combat_Idle_Zombie");
            if (combatIdleClip == null)
            {
                combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx");
            }

            if (combatIdleClip == null)
            {
                combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx", "Combat_Idle_Zombie2");
            }

            if (combatIdleClip == null)
            {
                combatIdleClip = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx");
            }

            if (combatIdleClip == null)
            {
                combatIdleClip = idleClip;
            }

            if (hitChest == null)
            {
                hitChest = hitReaction;
            }

            // ── Load or create controller (in-place to preserve GUID) ────
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            // Clear existing states
            foreach (ChildAnimatorState cs in sm.states)
                sm.RemoveState(cs.state);

            sm.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();
            sm.entryTransitions    = System.Array.Empty<AnimatorTransition>();

            // Remove orphaned BlendTree sub-assets from previous builds
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (subAsset is BlendTree bt)
                    AssetDatabase.RemoveObjectFromAsset(bt);
            }

            // Clear and re-add parameters
            while (ctrl.parameters.Length > 0)
                ctrl.RemoveParameter(0);

            ctrl.AddParameter("Speed",         AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityX",     AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityZ",     AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AttackTrigger",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AltAttackTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitTrigger",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DodgeTrigger",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DieTrigger",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsDead",        AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsInCombat",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("SpawnTrigger",  AnimatorControllerParameterType.Trigger);

            // ── States ───────────────────────────────────────────────────
            AnimatorState idleState      = sm.AddState("Idle");
            AnimatorState combatIdleState = sm.AddState("CombatIdle");
            AnimatorState spawnState     = sm.AddState("Spawn");
            AnimatorState locoState      = sm.AddState("Locomotion");
            AnimatorState attackState    = sm.AddState("Attack");     // Bite
            AnimatorState attackAltState = sm.AddState("AttackAlt"); // Scratch
            AnimatorState hitState       = sm.AddState("Hit");        // base = HitChest, variants = all Hit_* clips
            AnimatorState dead1State     = sm.AddState("Dead1");      // Death01
            AnimatorState dead2State     = sm.AddState("Dead2");      // Death02
            AnimatorState dead3State     = null;
            if (death03 != null)
            {
                dead3State = sm.AddState("Dead3");
            }

            idleState.motion      = idleClip;
            combatIdleState.motion = combatIdleClip;
            spawnState.motion     = spawnClip;
            attackState.motion    = biteClip;
            attackAltState.motion = scratchClip;
            hitState.motion       = hitChest;
            dead1State.motion     = death01;
            dead2State.motion     = death02;
            if (dead3State != null)
            {
                dead3State.motion = death03;
            }

            sm.defaultState = idleState;

            // ── Locomotion 2D blend tree (8-directional) ─────────────────
            BlendTree locoTree = new BlendTree
            {
                name            = "Locomotion",
                blendType       = BlendTreeType.FreeformCartesian2D,
                blendParameter  = "VelocityX",
                blendParameterY = "VelocityZ"
            };
            AssetDatabase.AddObjectToAsset(locoTree, ctrl);

            if (walkFwd  != null) locoTree.AddChild(walkFwd,  new Vector2( 0f,    1f));
            if (walkBwd  != null) locoTree.AddChild(walkBwd,  new Vector2( 0f,   -1f));
            if (walkL    != null) locoTree.AddChild(walkL,    new Vector2(-1f,    0f));
            if (walkR    != null) locoTree.AddChild(walkR,    new Vector2( 1f,    0f));
            if (walkFwdL != null) locoTree.AddChild(walkFwdL, new Vector2(-0.7f,  0.7f));
            if (walkFwdR != null) locoTree.AddChild(walkFwdR, new Vector2( 0.7f,  0.7f));
            if (walkBwdL != null) locoTree.AddChild(walkBwdL, new Vector2(-0.7f, -0.7f));
            if (walkBwdR != null) locoTree.AddChild(walkBwdR, new Vector2( 0.7f, -0.7f));

            locoState.motion = locoTree;

            // ── AnyState transitions (priority order matters) ────────────

            // IsDead → Dead1 or Dead2 — controller randomly picks state (50/50 via float)
            // Simpler: use two separate AnyState→Dead transitions driven by IsDead, rely on
            // ZombieAnimationController override randomisation for clip swap, so only one Dead state needed.
            // But user wants two distinct death states, so we wire both with IsDead + random float.
            ctrl.AddParameter("DeathRoll", AnimatorControllerParameterType.Float);

            AnimatorStateTransition tDead1 = sm.AddAnyStateTransition(dead1State);
            tDead1.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            tDead1.AddCondition(AnimatorConditionMode.Less, dead3State != null ? 0.3334f : 0.5f, "DeathRoll");
            tDead1.duration = 0.1f;
            tDead1.hasExitTime = false;
            tDead1.canTransitionToSelf = false;

            AnimatorStateTransition tDead2 = sm.AddAnyStateTransition(dead2State);
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
                AnimatorStateTransition tDead3 = sm.AddAnyStateTransition(dead3State);
                tDead3.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                tDead3.AddCondition(AnimatorConditionMode.Greater, 0.6666f, "DeathRoll");
                tDead3.duration = 0.1f;
                tDead3.hasExitTime = false;
                tDead3.canTransitionToSelf = false;
            }

            // AttackTrigger → Attack (Bite)
            AnimatorStateTransition tAnyAttack = sm.AddAnyStateTransition(attackState);
            tAnyAttack.AddCondition(AnimatorConditionMode.If,    0, "AttackTrigger");
            tAnyAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyAttack.duration            = 0.08f;
            tAnyAttack.hasExitTime         = false;
            tAnyAttack.canTransitionToSelf = false;

            // AltAttackTrigger → AttackAlt (Scratch)
            AnimatorStateTransition tAnyAltAttack = sm.AddAnyStateTransition(attackAltState);
            tAnyAltAttack.AddCondition(AnimatorConditionMode.If,    0, "AltAttackTrigger");
            tAnyAltAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyAltAttack.duration            = 0.08f;
            tAnyAltAttack.hasExitTime         = false;
            tAnyAltAttack.canTransitionToSelf = false;

            // HitTrigger → Hit (HitChest base, all other Hit_* clips are runtime variants)
            AnimatorStateTransition tAnyHit = sm.AddAnyStateTransition(hitState);
            tAnyHit.AddCondition(AnimatorConditionMode.If,    0, "HitTrigger");
            tAnyHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tAnyHit.duration            = 0.05f;
            tAnyHit.hasExitTime         = false;
            tAnyHit.canTransitionToSelf = false;

            // SpawnTrigger → Spawn
            AnimatorStateTransition tAnySpawn = sm.AddAnyStateTransition(spawnState);
            tAnySpawn.AddCondition(AnimatorConditionMode.If, 0, "SpawnTrigger");
            tAnySpawn.duration            = 0.05f;
            tAnySpawn.hasExitTime         = false;
            tAnySpawn.canTransitionToSelf = false;

            // ── Normal transitions ────────────────────────────────────────
            // Idle / CombatIdle / Locomotion
            AddTransition(idleState,  locoState, speed: (AnimatorConditionMode.Greater, 0.1f));

            AnimatorStateTransition tIdleToCombatIdle = idleState.AddTransition(combatIdleState);
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsInCombat");
            tIdleToCombatIdle.duration = 0.12f;
            tIdleToCombatIdle.hasExitTime = false;

            AnimatorStateTransition tCombatIdleToIdle = combatIdleState.AddTransition(idleState);
            tCombatIdleToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInCombat");
            tCombatIdleToIdle.duration = 0.12f;
            tCombatIdleToIdle.hasExitTime = false;

            AnimatorStateTransition tLocoToIdle = locoState.AddTransition(idleState);
            tLocoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsInCombat");
            tLocoToIdle.duration = 0.12f;
            tLocoToIdle.hasExitTime = false;

            AnimatorStateTransition tLocoToCombatIdle = locoState.AddTransition(combatIdleState);
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsInCombat");
            tLocoToCombatIdle.duration = 0.12f;
            tLocoToCombatIdle.hasExitTime = false;

            AnimatorStateTransition tCombatIdleToLoco = combatIdleState.AddTransition(locoState);
            tCombatIdleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tCombatIdleToLoco.duration = 0.12f;
            tCombatIdleToLoco.hasExitTime = false;

            // Attack, AttackAlt, Hit, Spawn → Idle on exit
            AddExitTransition(attackState,    idleState, exitTime: 0.85f, duration: 0.15f);
            AddExitTransition(attackAltState, idleState, exitTime: 0.85f, duration: 0.15f);
            AddExitTransition(hitState,       idleState, exitTime: 0.8f,  duration: 0.1f);
            AddExitTransition(spawnState,     idleState, exitTime: 0.95f, duration: 0.1f);

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
                + "  Walk      : FreeformCartesian2D blend (8 clips)\n"
                + "  Run Wire Selected Zombie Components to auto-assign clips to ZombieAnimationController.");
        }

        [MenuItem("Tools/Zombera/Animation/Wire Selected Zombie Components")]
        public static void WireSelectedComponents()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                Debug.LogWarning("[Zombera] Select a zombie GameObject (or prefab root) first.");
                return;
            }

            ZombieAnimationController zac = selected.GetComponentInChildren<ZombieAnimationController>(true);
            if (zac == null)
            {
                Debug.LogWarning("[Zombera] No ZombieAnimationController found on selected object.");
                return;
            }

            ZombieWiringData data = BuildWiringData();
            SerializedObject so = new SerializedObject(zac);
            ApplyWiringProperties(so, data);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(zac);

            // If it is a prefab, save it
            string prefabPath = AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(prefabPath))
                AssetDatabase.SaveAssets();

            Debug.Log($"[Zombera] Wired ZombieAnimationController on '{selected.name}' with {data.ClipPool.Count} clips.\n"
                + "  baseAttackClip = Zombie_Bite\n"
                + "  baseCombatIdleClip = Combat_Idle_Zombie\n"
                + "  baseHitClip    = Hit_Chest_Light\n"
                + "  baseDeathClip  = Armature_Death01\n"
                + "  baseDeathClipSecondary = Armature_Death02\n"
                + "  combatIdleOverrideClips, reactionOverrideClips, deathOverrideClips, and zombieFolderClips are fully populated for runtime randomisation.");
        }

        [MenuItem("Tools/Zombera/Animation/Wire All Zombie Prefabs")]
        public static void WireAllZombiePrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Zombies" });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                Debug.LogWarning("[Zombera] No zombie prefabs found under Assets/Prefabs/Zombies.");
                return;
            }

            ZombieWiringData data = BuildWiringData();
            int wiredControllerCount = 0;
            int savedPrefabCount = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    continue;
                }

                bool changed = false;

                try
                {
                    ZombieAnimationController[] controllers = prefabRoot.GetComponentsInChildren<ZombieAnimationController>(true);
                    for (int j = 0; j < controllers.Length; j++)
                    {
                        ZombieAnimationController controller = controllers[j];
                        if (controller == null)
                        {
                            continue;
                        }

                        SerializedObject so = new SerializedObject(controller);
                        ApplyWiringProperties(so, data);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(controller);

                        changed = true;
                        wiredControllerCount++;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                        savedPrefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Zombera] Wired {wiredControllerCount} ZombieAnimationController component(s) across {savedPrefabCount} prefab(s).\n"
                + "  Combat idle, hit reactions, and death variants are now explicitly wired.");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private sealed class ZombieWiringData
        {
            public AnimationClip IdleClip;
            public AnimationClip CombatIdleBaseClip;
            public AnimationClip WalkForwardClip;
            public AnimationClip BiteClip;
            public AnimationClip ScratchClip;
            public AnimationClip HitBaseClip;
            public AnimationClip DeathPrimaryClip;
            public AnimationClip DeathSecondaryClip;
            public List<AnimationClip> ClipPool = new List<AnimationClip>();
            public List<AnimationClip> CombatIdleOverrides = new List<AnimationClip>();
            public List<AnimationClip> ReactionOverrides = new List<AnimationClip>();
            public List<AnimationClip> DeathOverrides = new List<AnimationClip>();
        }

        private static ZombieWiringData BuildWiringData()
        {
            var data = new ZombieWiringData
            {
                IdleClip = LoadClip("Idle/Zombie_Idle"),
                WalkForwardClip = LoadClip("Walk/Armature_Zombie_Walk_Fwd_Loop"),
                BiteClip = LoadClip("Combat/Zombie_Bite"),
                ScratchClip = LoadClip("Combat/Zombie_Scratch"),
                HitBaseClip = LoadClip("Hit Reactions/Hit_Chest_Light"),
                DeathPrimaryClip = LoadClip("Death/Armature_Death01"),
                DeathSecondaryClip = LoadClip("Death/Armature_Death02")
            };

            if (data.HitBaseClip == null)
            {
                data.HitBaseClip = LoadClipFromFbx("Hit Reactions/Zombie_Reaction.fbx", "Zombie_Reaction");
            }

            AnimationClip combatIdle01 = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx", "Combat_Idle_Zombie");
            if (combatIdle01 == null)
            {
                combatIdle01 = LoadClipFromFbx("Combat/Combat_Idle_Zombie.fbx");
            }

            AnimationClip combatIdle02 = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx", "Combat_Idle_Zombie2");
            if (combatIdle02 == null)
            {
                combatIdle02 = LoadClipFromFbx("Combat/Combat_Idle_Zombie2.fbx");
            }

            data.CombatIdleBaseClip = combatIdle01 != null ? combatIdle01 : combatIdle02;
            if (data.CombatIdleBaseClip == null)
            {
                data.CombatIdleBaseClip = data.IdleClip;
            }

            AddUniqueClip(data.CombatIdleOverrides, combatIdle01);
            AddUniqueClip(data.CombatIdleOverrides, combatIdle02);

            if (data.CombatIdleBaseClip == null && data.CombatIdleOverrides.Count > 0)
            {
                data.CombatIdleBaseClip = data.CombatIdleOverrides[0];
            }

            data.ClipPool = LoadAllZombieAnimationClips();
            data.ReactionOverrides = LoadAllAnimationClipsFromFolder($"{ClipsFolder}/Hit Reactions");
            data.DeathOverrides = LoadAllAnimationClipsFromFolder($"{ClipsFolder}/Death");
            return data;
        }

        private static void ApplyWiringProperties(SerializedObject so, ZombieWiringData data)
        {
            if (so == null || data == null)
            {
                return;
            }

            SerializedProperty enableVariantsProp = so.FindProperty("enableFolderClipVariants");
            if (enableVariantsProp != null)
            {
                enableVariantsProp.boolValue = true;
            }

            SerializedProperty idleProp = so.FindProperty("baseIdleClip");
            if (idleProp != null)
            {
                idleProp.objectReferenceValue = data.IdleClip;
            }

            SerializedProperty combatIdleBaseProp = so.FindProperty("baseCombatIdleClip");
            if (combatIdleBaseProp != null)
            {
                combatIdleBaseProp.objectReferenceValue = data.CombatIdleBaseClip;
            }

            SerializedProperty locomotionProp = so.FindProperty("baseLocomotionClip");
            if (locomotionProp != null)
            {
                locomotionProp.objectReferenceValue = data.WalkForwardClip;
            }

            SerializedProperty attackProp = so.FindProperty("baseAttackClip");
            if (attackProp != null)
            {
                attackProp.objectReferenceValue = data.BiteClip;
            }

            SerializedProperty altAttackProp = so.FindProperty("altAttackClip");
            if (altAttackProp != null)
            {
                altAttackProp.objectReferenceValue = data.ScratchClip;
            }

            SerializedProperty altAttackChanceProp = so.FindProperty("altAttackChance");
            if (altAttackChanceProp != null)
            {
                altAttackChanceProp.floatValue = 0.8f;
            }

            SerializedProperty hitProp = so.FindProperty("baseHitClip");
            if (hitProp != null)
            {
                hitProp.objectReferenceValue = data.HitBaseClip;
            }

            SerializedProperty deathPrimaryProp = so.FindProperty("baseDeathClip");
            if (deathPrimaryProp != null)
            {
                deathPrimaryProp.objectReferenceValue = data.DeathPrimaryClip;
            }

            SerializedProperty deathSecondaryProp = so.FindProperty("baseDeathClipSecondary");
            if (deathSecondaryProp != null)
            {
                deathSecondaryProp.objectReferenceValue = data.DeathSecondaryClip;
            }

            SetClipArrayProperty(so.FindProperty("zombieFolderClips"), data.ClipPool);
            SetClipArrayProperty(so.FindProperty("combatIdleOverrideClips"), data.CombatIdleOverrides);
            SetClipArrayProperty(so.FindProperty("reactionOverrideClips"), data.ReactionOverrides);
            SetClipArrayProperty(so.FindProperty("deathOverrideClips"), data.DeathOverrides);
        }

        private static AnimationClip LoadClip(string clipPathOrName)
        {
            if (string.IsNullOrWhiteSpace(clipPathOrName))
            {
                return null;
            }

            string normalized = clipPathOrName.Replace('\\', '/');
            string path = normalized.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{ClipsFolder}/{normalized}";

            if (!path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            {
                path += ".anim";
            }

            AnimationClip direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null)
            {
                return direct;
            }

            string clipName = normalized;
            int slashIndex = clipName.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                clipName = clipName.Substring(slashIndex + 1);
            }

            if (clipName.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            {
                clipName = clipName.Substring(0, clipName.Length - 5);
            }

            List<AnimationClip> allClips = LoadAllZombieAnimationClips();
            for (int i = 0; i < allClips.Count; i++)
            {
                AnimationClip clip = allClips[i];
                if (clip != null && string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }

        private static AnimationClip LoadClipFromFbx(string relativeFbxPath, string preferredClipName = null)
        {
            if (string.IsNullOrWhiteSpace(relativeFbxPath))
            {
                return null;
            }

            string fullPath = relativeFbxPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? relativeFbxPath
                : $"{ClipsFolder}/{relativeFbxPath}";

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fullPath);
            AnimationClip fallback = null;

            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip) || IsPreviewClip(clip))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(preferredClipName)
                    && string.Equals(clip.name, preferredClipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }

                if (fallback == null)
                {
                    fallback = clip;
                }
            }

            return fallback;
        }

        private static List<AnimationClip> LoadAllZombieAnimationClips()
        {
            var clips = new List<AnimationClip>();
            var visitedPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipsFolder });
            for (int i = 0; i < clipGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (string.IsNullOrWhiteSpace(path) || !visitedPaths.Add(path))
                {
                    continue;
                }

                AddClipsFromAssetPath(path, clips);
            }

            AddClipsFromAssetPath($"{ClipsFolder}/Hit Reactions/Zombie_Reaction.fbx", clips);
            return clips;
        }

        private static List<AnimationClip> LoadAllAnimationClipsFromFolder(string folderPath)
        {
            var clips = new List<AnimationClip>();
            var visitedPaths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                return clips;
            }

            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
            for (int i = 0; i < clipGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (string.IsNullOrWhiteSpace(path) || !visitedPaths.Add(path))
                {
                    continue;
                }

                AddClipsFromAssetPath(path, clips);
            }

            return clips;
        }

        private static void AddClipsFromAssetPath(string path, List<AnimationClip> destination)
        {
            if (string.IsNullOrWhiteSpace(path) || destination == null)
            {
                return;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                AnimationClip single = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (single != null && !IsPreviewClip(single))
                {
                    AddUniqueClip(destination, single);
                }

                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !IsPreviewClip(clip))
                {
                    AddUniqueClip(destination, clip);
                }
            }
        }

        private static void AddUniqueClip(List<AnimationClip> clips, AnimationClip clip)
        {
            if (clips == null || clip == null || clips.Contains(clip))
            {
                return;
            }

            clips.Add(clip);
        }

        private static void SetClipArrayProperty(SerializedProperty property, List<AnimationClip> clips)
        {
            if (property == null || clips == null)
            {
                return;
            }

            property.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        private static bool IsPreviewClip(AnimationClip clip)
        {
            return clip != null
                && clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void AddTransition(AnimatorState from, AnimatorState to,
            (AnimatorConditionMode mode, float threshold) speed)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.AddCondition(speed.mode, speed.threshold, "Speed");
            t.duration    = 0.2f;
            t.hasExitTime = false;
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to,
            float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime    = exitTime;
            t.duration    = duration;
        }
    }
}
