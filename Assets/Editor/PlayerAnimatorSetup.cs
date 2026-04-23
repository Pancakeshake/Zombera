using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Zombera.Characters;

namespace Zombera.Editor
{
    /// <summary>
    /// Rebuilds Player_Default.controller.
    /// Menu: Tools/Zombera/Animation/Rebuild Player Default Controller
    /// </summary>
    public static class PlayerAnimatorSetup
    {
        private const string ClipsFolder    = "Assets/Animations/Players";
        private const string ControllerPath = "Assets/Animations/Players/Player_Default.controller";

        [MenuItem("Tools/Zombera/Animation/Rebuild Player Default Controller")]
        public static void RebuildController()
        {
            // ── Load clips ───────────────────────────────────────────────
            AnimationClip idle        = Clip("Idle/Armature_Idle_Loop");
            AnimationClip walkLoop    = Clip("Walk/Armature_Walk_Loop");
            AnimationClip walkFwd     = ClipAny("Walk/Armature_Walk_Fwd_Loop", "Walk/Armature_Walk_Loop");
            AnimationClip walkBwd     = ClipAny("Walk/Armature_Walk_Bwd_Loop", "Walk/Armature_Walk_Loop");
            AnimationClip walkLeft    = ClipAny("Walk/Armature_Walk_L_Loop");
            AnimationClip walkRight   = ClipAny("Walk/Armature_Walk_R_Loop");
            AnimationClip walkFwdL    = ClipAny("Walk/Armature_Walk_Fwd_L_Loop", "Walk/Armature_Walk_Fwd_L", "Walk/Armature_Walk_Fwd_Loop");
            AnimationClip walkFwdR    = ClipAny("Walk/Armature_Walk_Fwd_R_Loop", "Walk/Armature_Walk_Fwd_Loop");
            AnimationClip walkBwdL    = ClipAny("Walk/Armature_Walk_Bwd_L_Loop", "Walk/Armature_Walk_Bwd_Loop");
            AnimationClip walkBwdR    = ClipAny("Walk/Armature_Walk_Bwd_R_Loop", "Walk/Armature_Walk_Bwd_Loop");

            AnimationClip jogFwd      = Clip("Jog/Armature_Jog_Fwd_Loop");
            AnimationClip jogBwd      = Clip("Jog/Armature_Jog_Bwd_Loop");
            AnimationClip jogLeft     = Clip("Jog/Armature_Jog_Left_Loop");
            AnimationClip jogRight    = Clip("Jog/Armature_Jog_Right_Loop");
            AnimationClip jogFwdL     = Clip("Jog/Armature_Jog_Fwd_L_Loop");
            AnimationClip jogFwdR     = Clip("Jog/Armature_Jog_Fwd_R_Loop");
            AnimationClip jogBwdR     = Clip("Jog/Armature_Jog_Bwd_R_Loop");

            AnimationClip sprintEnter = Clip("Sprint/Armature_Sprint_Enter");
            AnimationClip sprintLoop  = Clip("Sprint/Armature_Sprint_Loop");
            AnimationClip sprintExit  = Clip("Sprint/Armature_Sprint_Exit");

            AnimationClip crouchEnter = Clip("Crouch/Armature_Crouch_Enter");
            AnimationClip crouchIdle  = Clip("Crouch/Armature_Crouch_Idle_Loop");
            AnimationClip crouchFwd   = Clip("Crouch/Armature_Crouch_Fwd_Loop");
            AnimationClip crouchBwd   = Clip("Crouch/Armature_Crouch_Bwd_Loop");
            AnimationClip crouchLeft  = Clip("Crouch/Armature_Crouch_Left_Loop");
            AnimationClip crouchRight = Clip("Crouch/Armature_Crouch_Right_Loop");
            AnimationClip crouchFwdL  = Clip("Crouch/Armature_Crouch_Fwd_L_Loop");
            AnimationClip crouchFwdR  = Clip("Crouch/Armature_Crouch_Fwd_R_Loop");
            AnimationClip crouchBwdL  = Clip("Crouch/Armature_Crouch_Bwd_L_Loop");
            AnimationClip crouchBwdR  = Clip("Crouch/Armature_Crouch_Bwd_R_Loop");
            AnimationClip crouchExit  = Clip("Crouch/Armature_Crouch_Exit");

            AnimationClip crawlEnter  = Clip("Crawl/Armature_Crawl_Enter");
            AnimationClip crawlIdle   = Clip("Crawl/Armature_Crawl_Idle_Loop");
            AnimationClip crawlFwd    = Clip("Crawl/Armature_Crawl_Fwd_Loop");
            AnimationClip crawlBwd    = Clip("Crawl/Armature_Crawl_Bwd_Loop");
            AnimationClip crawlLeft   = Clip("Crawl/Armature_Crawl_Left_Loop");
            AnimationClip crawlRight  = Clip("Crawl/Armature_Crawl_Right_Loop");
            AnimationClip crawlExit   = Clip("Crawl/Armature_Crawl_Exit");

            AnimationClip sitEnter    = Clip("Actions/Armature_GroundSit_Enter");
            AnimationClip sitIdle     = Clip("Actions/Armature_GroundSit_Idle_Loop");
            AnimationClip sitExit     = Clip("Actions/Armature_GroundSit_Exit");

            AnimationClip bowAimNeutral = Clip("Bow/Armature_Bow_Aim_Neutral");
            AnimationClip bowAimUp      = null;
            AnimationClip bowAimDown    = null;
            AnimationClip bowNotch      = Clip("Bow/Armature_Bow_Notch");
            AnimationClip bowShoot      = Clip("Bow/Armature_Bow_Shoot");
            AnimationClip bowRapid      = null;

            AnimationClip hitChest      = Clip("Hit Reactions/Armature_Hit_Chest");
            AnimationClip hitHead       = Clip("Hit Reactions/Armature_Hit_Head");
            AnimationClip hitShoulderL  = Clip("Hit Reactions/Armature_Hit_Shoulder_L");
            AnimationClip hitShoulderR  = Clip("Hit Reactions/Armature_Hit_Shoulder_R");

            AnimationClip attackJab      = ClipAny("Combat/Armature_Punch_Jab");
            AnimationClip attackCross    = ClipAny("Combat/Armature_Punch_Cross");
            AnimationClip attackHook     = ClipAny("Combat/Armature_Melee_Hook");
            AnimationClip attackUppercut = ClipAny("Combat/Armature_Melee_Uppercut");
            AnimationClip attackKnee     = ClipAny("Combat/Armature_Melee_Knee");
            AnimationClip attackCombo    = ClipAny("Combat/Armature_Melee_Combo");
            AnimationClip attackLowKick  = ClipAny("Combat/Armature_Kick", "Combat/Armature_PunchKick_Enter", "Combat/Armature_Melee_Knee");
            AnimationClip combatIdle     = ClipFromFbx("Combat/Bouncing Fight Idle.fbx", "Bouncing Fight Idle", strictPreferredName: true);
            AnimationClip death01        = Clip("Death/Armature_Death01 1");
            AnimationClip dodgeLeft      = Clip("Dodge/Armature_Dodge_Left");
            AnimationClip dodgeRight     = Clip("Dodge/Armature_Dodge_Right");

            // ── Load or create controller ────────────────────────────────
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            AnimatorControllerLayer[] layers = ctrl.layers;
            if (layers != null && layers.Length > 0)
            {
                layers[0].iKPass = true;
                ctrl.layers = layers;
            }

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;

            foreach (ChildAnimatorState cs in sm.states)
                sm.RemoveState(cs.state);
            sm.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();
            sm.entryTransitions    = System.Array.Empty<AnimatorTransition>();

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (sub is BlendTree bt) AssetDatabase.RemoveObjectFromAsset(bt);

            while (ctrl.parameters.Length > 0)
                ctrl.RemoveParameter(0);

            // ── Parameters ───────────────────────────────────────────────
            ctrl.AddParameter("Speed",          AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityX",      AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelocityZ",      AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsInCombat",     AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsSprinting",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsCrouching",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsCrawling",     AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsSitting",      AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("BowEquipped",    AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("BowAimY",        AnimatorControllerParameterType.Float);
            ctrl.AddParameter("BowNotch",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("BowShoot",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("BowRapidFire",   AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("AttackTrigger",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackJabTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackCrossTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackHookTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackUppercutTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackKneeTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackComboTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("AttackLowKickTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitTrigger",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DodgeLeftTrigger",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DodgeRightTrigger", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DieTrigger",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IsDead",         AnimatorControllerParameterType.Bool);

            // ── States ───────────────────────────────────────────────────
            AnimatorState idleState       = sm.AddState("Idle");
            AnimatorState combatIdleState = sm.AddState("CombatIdle");
            AnimatorState locoState       = sm.AddState("Locomotion");
            AnimatorState combatLocoState = sm.AddState("CombatLocomotion");
            AnimatorState sprintEnterState= sm.AddState("Sprint_Enter");
            AnimatorState sprintState     = sm.AddState("Sprint");
            AnimatorState sprintExitState = sm.AddState("Sprint_Exit");
            AnimatorState crouchEnterState= sm.AddState("Crouch_Enter");
            AnimatorState crouchLocoState = sm.AddState("CrouchLocomotion");
            AnimatorState crouchExitState = sm.AddState("Crouch_Exit");
            AnimatorState crawlEnterState = sm.AddState("Crawl_Enter");
            AnimatorState crawlLocoState  = sm.AddState("CrawlLocomotion");
            AnimatorState crawlExitState  = sm.AddState("Crawl_Exit");
            AnimatorState sitEnterState   = sm.AddState("Sit_Enter");
            AnimatorState sitState        = sm.AddState("Sit");
            AnimatorState sitExitState    = sm.AddState("Sit_Exit");
            AnimatorState bowAimState     = sm.AddState("BowAim");
            AnimatorState bowNotchState   = sm.AddState("BowNotch");
            AnimatorState bowShootState   = sm.AddState("BowShoot");
            AnimatorState bowRapidState   = sm.AddState("BowRapidFire");
            AnimatorState attackState     = sm.AddState("Attack");
            AnimatorState attackJabState  = sm.AddState("Attack_Jab");
            AnimatorState attackCrossState= sm.AddState("Attack_Cross");
            AnimatorState attackHookState = sm.AddState("Attack_Hook");
            AnimatorState attackUppercutState = sm.AddState("Attack_Uppercut");
            AnimatorState attackKneeState = sm.AddState("Attack_Knee");
            AnimatorState attackComboState = sm.AddState("Attack_Combo");
            AnimatorState attackLowKickState = sm.AddState("Attack_LowKick");
            AnimatorState hitState        = sm.AddState("Hit");
            AnimatorState dodgeLeftState  = sm.AddState("DodgeLeft");
            AnimatorState dodgeRightState = sm.AddState("DodgeRight");
            AnimatorState deadState       = sm.AddState("Dead");

            // Keep combat-idle feet stable when the source clip includes subtle vertical motion.
            combatIdleState.iKOnFeet = true;

            sm.defaultState = idleState;

            idleState.motion        = idle;
            combatIdleState.motion  = combatIdle != null ? combatIdle : idle;
            sprintEnterState.motion = sprintEnter;
            sprintState.motion      = sprintLoop;
            sprintExitState.motion  = sprintExit;
            crouchEnterState.motion = crouchEnter;
            crouchExitState.motion  = crouchExit;
            crawlEnterState.motion  = crawlEnter;
            crawlExitState.motion   = crawlExit;
            sitEnterState.motion    = sitEnter;
            sitState.motion         = sitIdle;
            sitExitState.motion     = sitExit;
            bowNotchState.motion    = bowNotch;
            bowShootState.motion    = bowShoot;
            bowRapidState.motion    = bowRapid;
            attackState.motion      = attackJab;
            attackJabState.motion   = attackJab;
            attackCrossState.motion = attackCross != null ? attackCross : attackJab;
            attackHookState.motion  = attackHook != null ? attackHook : attackJab;
            attackUppercutState.motion = attackUppercut != null ? attackUppercut : attackJab;
            attackKneeState.motion  = attackKnee != null ? attackKnee : attackJab;
            attackComboState.motion = attackCombo != null ? attackCombo : attackKnee;
            attackLowKickState.motion = attackLowKick != null ? attackLowKick : attackKnee;
            hitState.motion         = hitChest;
            dodgeLeftState.motion   = dodgeLeft;
            dodgeRightState.motion  = dodgeRight;
            deadState.motion        = death01;

            // ── Locomotion blend tree (walk/jog, 2D FreeformCartesian) ───
            BlendTree locoTree = MakeTree(ctrl, "Locomotion");
            if (walkLoop != null) locoTree.AddChild(walkLoop, new Vector2(0f,    0f));   // center = slow walk
            if (jogFwd   != null) locoTree.AddChild(jogFwd,   new Vector2(0f,    1f));
            if (jogBwd   != null) locoTree.AddChild(jogBwd,   new Vector2(0f,   -1f));
            if (jogLeft  != null) locoTree.AddChild(jogLeft,  new Vector2(-1f,   0f));
            if (jogRight != null) locoTree.AddChild(jogRight, new Vector2(1f,    0f));
            if (jogFwdL  != null) locoTree.AddChild(jogFwdL,  new Vector2(-0.7f, 0.7f));
            if (jogFwdR  != null) locoTree.AddChild(jogFwdR,  new Vector2(0.7f,  0.7f));
            if (jogBwdR  != null) locoTree.AddChild(jogBwdR,  new Vector2(0.7f, -0.7f));
            locoState.motion = locoTree;

            // ── Combat locomotion blend tree (walk-only 2D strafe/positioning) ─
            BlendTree combatLocoTree = MakeTree(ctrl, "CombatLocomotion");
            // Prefer forward walk as the center combat movement pose so combat positioning
            // naturally blends through forward/backward/diagonal walk loops.
            AnimationClip combatCenter = walkFwd != null ? walkFwd : walkLoop;
            if (combatCenter != null) combatLocoTree.AddChild(combatCenter,  new Vector2(0f,    0f));
            if (walkFwd   != null) combatLocoTree.AddChild(walkFwd,   new Vector2(0f,    1f));
            if (walkBwd   != null) combatLocoTree.AddChild(walkBwd,   new Vector2(0f,   -1f));
            if (walkLeft  != null) combatLocoTree.AddChild(walkLeft,  new Vector2(-1f,   0f));
            if (walkRight != null) combatLocoTree.AddChild(walkRight, new Vector2(1f,    0f));
            if (walkFwdL  != null) combatLocoTree.AddChild(walkFwdL,  new Vector2(-0.7f, 0.7f));
            if (walkFwdR  != null) combatLocoTree.AddChild(walkFwdR,  new Vector2(0.7f,  0.7f));
            if (walkBwdL  != null) combatLocoTree.AddChild(walkBwdL,  new Vector2(-0.7f,-0.7f));
            if (walkBwdR  != null) combatLocoTree.AddChild(walkBwdR,  new Vector2(0.7f, -0.7f));
            combatLocoState.motion = combatLocoTree;

            // ── Crouch locomotion blend tree ─────────────────────────────
            BlendTree crouchTree = MakeTree(ctrl, "CrouchLocomotion");
            if (crouchIdle  != null) crouchTree.AddChild(crouchIdle,  new Vector2(0f,    0f));
            if (crouchFwd   != null) crouchTree.AddChild(crouchFwd,   new Vector2(0f,    1f));
            if (crouchBwd   != null) crouchTree.AddChild(crouchBwd,   new Vector2(0f,   -1f));
            if (crouchLeft  != null) crouchTree.AddChild(crouchLeft,  new Vector2(-1f,   0f));
            if (crouchRight != null) crouchTree.AddChild(crouchRight, new Vector2(1f,    0f));
            if (crouchFwdL  != null) crouchTree.AddChild(crouchFwdL,  new Vector2(-0.7f, 0.7f));
            if (crouchFwdR  != null) crouchTree.AddChild(crouchFwdR,  new Vector2(0.7f,  0.7f));
            if (crouchBwdL  != null) crouchTree.AddChild(crouchBwdL,  new Vector2(-0.7f,-0.7f));
            if (crouchBwdR  != null) crouchTree.AddChild(crouchBwdR,  new Vector2(0.7f, -0.7f));
            crouchLocoState.motion = crouchTree;

            // ── Crawl locomotion blend tree ──────────────────────────────
            BlendTree crawlTree = MakeTree(ctrl, "CrawlLocomotion");
            if (crawlIdle  != null) crawlTree.AddChild(crawlIdle,  new Vector2(0f,   0f));
            if (crawlFwd   != null) crawlTree.AddChild(crawlFwd,   new Vector2(0f,   1f));
            if (crawlBwd   != null) crawlTree.AddChild(crawlBwd,   new Vector2(0f,  -1f));
            if (crawlLeft  != null) crawlTree.AddChild(crawlLeft,  new Vector2(-1f,  0f));
            if (crawlRight != null) crawlTree.AddChild(crawlRight, new Vector2(1f,   0f));
            crawlLocoState.motion = crawlTree;

            // ── Bow aim blend tree (1D on BowAimY) ───────────────────────
            BlendTree bowTree = new BlendTree
            {
                name           = "BowAim",
                blendType      = BlendTreeType.Simple1D,
                blendParameter = "BowAimY"
            };
            AssetDatabase.AddObjectToAsset(bowTree, ctrl);
            if (bowAimNeutral != null)
            {
                bowTree.AddChild(bowAimNeutral, -1f);
                bowTree.AddChild(bowAimNeutral,  0f);
                bowTree.AddChild(bowAimNeutral,  1f);
            }
            bowAimState.motion = bowTree;

            // ── AnyState transitions ──────────────────────────────────────
            // Death (highest priority)
            AnyTo(sm, deadState, duration: 0.1f).AddCondition(AnimatorConditionMode.If, 0, "IsDead");

            // Hit
            var tHit = AnyTo(sm, hitState, duration: 0.05f);
            tHit.AddCondition(AnimatorConditionMode.If,    0, "HitTrigger");
            tHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            // Dodge
            var tDodgeL = AnyTo(sm, dodgeLeftState, duration: 0.05f);
            tDodgeL.AddCondition(AnimatorConditionMode.If,    0, "DodgeLeftTrigger");
            tDodgeL.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            var tDodgeR = AnyTo(sm, dodgeRightState, duration: 0.05f);
            tDodgeR.AddCondition(AnimatorConditionMode.If,    0, "DodgeRightTrigger");
            tDodgeR.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            // Attack
            var tAttack = AnyTo(sm, attackState, duration: 0.08f);
            tAttack.AddCondition(AnimatorConditionMode.If,    0, "AttackTrigger");
            tAttack.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackJab = AnyTo(sm, attackJabState, duration: 0.08f);
            tAttackJab.AddCondition(AnimatorConditionMode.If,    0, "AttackJabTrigger");
            tAttackJab.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackCross = AnyTo(sm, attackCrossState, duration: 0.08f);
            tAttackCross.AddCondition(AnimatorConditionMode.If,    0, "AttackCrossTrigger");
            tAttackCross.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackHook = AnyTo(sm, attackHookState, duration: 0.08f);
            tAttackHook.AddCondition(AnimatorConditionMode.If,    0, "AttackHookTrigger");
            tAttackHook.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackUppercut = AnyTo(sm, attackUppercutState, duration: 0.08f);
            tAttackUppercut.AddCondition(AnimatorConditionMode.If,    0, "AttackUppercutTrigger");
            tAttackUppercut.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackKnee = AnyTo(sm, attackKneeState, duration: 0.08f);
            tAttackKnee.AddCondition(AnimatorConditionMode.If,    0, "AttackKneeTrigger");
            tAttackKnee.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackCombo = AnyTo(sm, attackComboState, duration: 0.08f);
            tAttackCombo.AddCondition(AnimatorConditionMode.If,    0, "AttackComboTrigger");
            tAttackCombo.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            var tAttackLowKick = AnyTo(sm, attackLowKickState, duration: 0.08f);
            tAttackLowKick.AddCondition(AnimatorConditionMode.If,    0, "AttackLowKickTrigger");
            tAttackLowKick.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");

            // Bow equipped → BowAim (from non-bow states)
            var tBowEnter = AnyTo(sm, bowAimState, duration: 0.2f);
            tBowEnter.AddCondition(AnimatorConditionMode.If, 0, "BowEquipped");
            tBowEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tBowEnter.canTransitionToSelf = false;

            // ── Sprint chain ──────────────────────────────────────────────
            // Idle/Loco → Sprint_Enter
            AddBoolTransition(idleState,  sprintEnterState, "IsSprinting", value: true);
            AddBoolTransition(locoState,  sprintEnterState, "IsSprinting", value: true);
            // Sprint_Enter → Sprint
            ExitTo(sprintEnterState, sprintState, exitTime: 0.9f, duration: 0.05f);
            // Sprint → Locomotion when sprint is disabled but movement continues (toggle-off / stamina depletion).
            var tSprintToLoco = sprintState.AddTransition(locoState);
            tSprintToLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsSprinting");
            tSprintToLoco.AddCondition(AnimatorConditionMode.Greater, 0.09f, "Speed");
            tSprintToLoco.duration = 0.08f;
            tSprintToLoco.hasExitTime = false;

            // Sprint → Sprint_Exit only when movement has effectively ended (arrived/stopped).
            var tSprintToExit = sprintState.AddTransition(sprintExitState);
            tSprintToExit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
            tSprintToExit.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tSprintToExit.duration = 0.08f;
            tSprintToExit.hasExitTime = false;

            // Sprint_Exit is a stop-out animation, so it always resolves to idle.
            ExitTo(sprintExitState, idleState, exitTime: 0.8f, duration: 0.1f);

            // ── Idle ↔ Locomotion ─────────────────────────────────────────
            var tIdleToLoco = idleState.AddTransition(locoState);
            tIdleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f,  "Speed");
            tIdleToLoco.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsInCombat");
            tIdleToLoco.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsSprinting");
            tIdleToLoco.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsCrouching");
            tIdleToLoco.AddCondition(AnimatorConditionMode.IfNot,   0,     "IsCrawling");
            tIdleToLoco.duration    = 0.2f;
            tIdleToLoco.hasExitTime = false;

            var tLocoToIdle = locoState.AddTransition(idleState);
            tLocoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsInCombat");
            tLocoToIdle.duration    = 0.2f;
            tLocoToIdle.hasExitTime = false;

            // ── Locomotion ↔ Combat locomotion ─────────────────────────
            // In combat, movement uses walk-only directional blending for close-range positioning.
            var tIdleToCombatLoco = idleState.AddTransition(combatLocoState);
            tIdleToCombatLoco.AddCondition(AnimatorConditionMode.If,      0, "IsInCombat");
            tIdleToCombatLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tIdleToCombatLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsCrouching");
            tIdleToCombatLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsCrawling");
            tIdleToCombatLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsSitting");
            tIdleToCombatLoco.duration = 0.12f;
            tIdleToCombatLoco.hasExitTime = false;

            var tIdleToCombatIdle = idleState.AddTransition(combatIdleState);
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.If,    0, "IsInCombat");
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawling");
            tIdleToCombatIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSitting");
            tIdleToCombatIdle.duration = 0.08f;
            tIdleToCombatIdle.hasExitTime = false;

            var tLocoToCombatLoco = locoState.AddTransition(combatLocoState);
            tLocoToCombatLoco.AddCondition(AnimatorConditionMode.If,      0, "IsInCombat");
            tLocoToCombatLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tLocoToCombatLoco.duration = 0.12f;
            tLocoToCombatLoco.hasExitTime = false;

            var tLocoToCombatIdle = locoState.AddTransition(combatIdleState);
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.If,    0, "IsInCombat");
            tLocoToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tLocoToCombatIdle.duration = 0.12f;
            tLocoToCombatIdle.hasExitTime = false;

            var tSprintToCombatLoco = sprintState.AddTransition(combatLocoState);
            tSprintToCombatLoco.AddCondition(AnimatorConditionMode.If,      0, "IsInCombat");
            tSprintToCombatLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tSprintToCombatLoco.duration = 0.08f;
            tSprintToCombatLoco.hasExitTime = false;

            var tSprintToCombatIdle = sprintState.AddTransition(combatIdleState);
            tSprintToCombatIdle.AddCondition(AnimatorConditionMode.If,    0, "IsInCombat");
            tSprintToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tSprintToCombatIdle.duration = 0.08f;
            tSprintToCombatIdle.hasExitTime = false;

            var tCombatLocoToCombatIdle = combatLocoState.AddTransition(combatIdleState);
            tCombatLocoToCombatIdle.AddCondition(AnimatorConditionMode.If,    0, "IsInCombat");
            tCombatLocoToCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tCombatLocoToCombatIdle.duration = 0.12f;
            tCombatLocoToCombatIdle.hasExitTime = false;

            var tCombatLocoToIdle = combatLocoState.AddTransition(idleState);
            tCombatLocoToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsInCombat");
            tCombatLocoToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tCombatLocoToIdle.duration = 0.12f;
            tCombatLocoToIdle.hasExitTime = false;

            var tCombatLocoToLoco = combatLocoState.AddTransition(locoState);
            tCombatLocoToLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsInCombat");
            tCombatLocoToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tCombatLocoToLoco.duration = 0.12f;
            tCombatLocoToLoco.hasExitTime = false;

            var tCombatIdleToCombatLoco = combatIdleState.AddTransition(combatLocoState);
            tCombatIdleToCombatLoco.AddCondition(AnimatorConditionMode.If,      0, "IsInCombat");
            tCombatIdleToCombatLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tCombatIdleToCombatLoco.duration = 0.12f;
            tCombatIdleToCombatLoco.hasExitTime = false;

            var tCombatIdleToIdle = combatIdleState.AddTransition(idleState);
            tCombatIdleToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsInCombat");
            tCombatIdleToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tCombatIdleToIdle.duration = 0.12f;
            tCombatIdleToIdle.hasExitTime = false;

            var tCombatIdleToLoco = combatIdleState.AddTransition(locoState);
            tCombatIdleToLoco.AddCondition(AnimatorConditionMode.IfNot,   0, "IsInCombat");
            tCombatIdleToLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tCombatIdleToLoco.duration = 0.12f;
            tCombatIdleToLoco.hasExitTime = false;

            // ── Crouch chain ──────────────────────────────────────────────
            AddBoolTransition(idleState,  crouchEnterState, "IsCrouching", value: true);
            AddBoolTransition(locoState,  crouchEnterState, "IsCrouching", value: true);
            AddBoolTransition(combatLocoState, crouchEnterState, "IsCrouching", value: true);
            AddBoolTransition(combatIdleState, crouchEnterState, "IsCrouching", value: true);
            ExitTo(crouchEnterState, crouchLocoState, exitTime: 0.9f, duration: 0.1f);
            AddBoolTransition(crouchLocoState, crouchExitState, "IsCrouching", value: false);
            ExitTo(crouchExitState, idleState, exitTime: 0.9f, duration: 0.1f);

            // ── Crawl chain ───────────────────────────────────────────────
            AddBoolTransition(idleState,  crawlEnterState, "IsCrawling", value: true);
            AddBoolTransition(combatLocoState, crawlEnterState, "IsCrawling", value: true);
            AddBoolTransition(combatIdleState, crawlEnterState, "IsCrawling", value: true);
            ExitTo(crawlEnterState, crawlLocoState, exitTime: 0.9f, duration: 0.1f);
            AddBoolTransition(crawlLocoState, crawlExitState, "IsCrawling", value: false);
            ExitTo(crawlExitState, idleState, exitTime: 0.9f, duration: 0.1f);

            // ── Sit chain ─────────────────────────────────────────────────
            AddBoolTransition(idleState, sitEnterState, "IsSitting", value: true);
            AddBoolTransition(combatLocoState, sitEnterState, "IsSitting", value: true);
            AddBoolTransition(combatIdleState, sitEnterState, "IsSitting", value: true);
            ExitTo(sitEnterState, sitState, exitTime: 0.9f, duration: 0.1f);
            AddBoolTransition(sitState, sitExitState, "IsSitting", value: false);
            ExitTo(sitExitState, idleState, exitTime: 0.9f, duration: 0.1f);

            // ── Bow sub-state transitions ─────────────────────────────────
            // BowAim → BowNotch / BowShoot / BowRapidFire
            var tBowNotch = bowAimState.AddTransition(bowNotchState);
            tBowNotch.AddCondition(AnimatorConditionMode.If, 0, "BowNotch");
            tBowNotch.duration = 0.05f; tBowNotch.hasExitTime = false;

            var tBowShoot = bowAimState.AddTransition(bowShootState);
            tBowShoot.AddCondition(AnimatorConditionMode.If, 0, "BowShoot");
            tBowShoot.duration = 0.05f; tBowShoot.hasExitTime = false;

            var tBowNotchToShoot = bowNotchState.AddTransition(bowShootState);
            tBowNotchToShoot.AddCondition(AnimatorConditionMode.If, 0, "BowShoot");
            tBowNotchToShoot.duration = 0.03f; tBowNotchToShoot.hasExitTime = false;

            var tBowRapid = bowAimState.AddTransition(bowRapidState);
            tBowRapid.AddCondition(AnimatorConditionMode.If, 0, "BowRapidFire");
            tBowRapid.duration = 0.05f; tBowRapid.hasExitTime = false;

            ExitTo(bowNotchState, bowAimState, exitTime: 0.9f, duration: 0.1f);
            ExitTo(bowShootState, bowAimState, exitTime: 0.9f, duration: 0.1f);

            var tRapidExit = bowRapidState.AddTransition(bowAimState);
            tRapidExit.AddCondition(AnimatorConditionMode.IfNot, 0, "BowRapidFire");
            tRapidExit.duration = 0.1f; tRapidExit.hasExitTime = false;

            // BowAim → Idle when bow unequipped
            var tBowExit = bowAimState.AddTransition(idleState);
            tBowExit.AddCondition(AnimatorConditionMode.IfNot, 0, "BowEquipped");
            tBowExit.duration = 0.2f; tBowExit.hasExitTime = false;

            var tBowNotchExit = bowNotchState.AddTransition(idleState);
            tBowNotchExit.AddCondition(AnimatorConditionMode.IfNot, 0, "BowEquipped");
            tBowNotchExit.duration = 0.05f; tBowNotchExit.hasExitTime = false;

            var tBowShootExit = bowShootState.AddTransition(idleState);
            tBowShootExit.AddCondition(AnimatorConditionMode.IfNot, 0, "BowEquipped");
            tBowShootExit.duration = 0.05f; tBowShootExit.hasExitTime = false;

            // ── Attack / Hit / Dodge exits ───────────────────────────────
            // In combat: return directly to combat idle/loco based on Speed.
            // Out of combat: return to normal idle.
            AddActionStateExitTransitions(attackState,      idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackJabState,   idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackCrossState, idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackHookState,  idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackUppercutState, idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackKneeState,  idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackComboState, idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(attackLowKickState, idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.15f);
            AddActionStateExitTransitions(hitState,         idleState, combatIdleState, combatLocoState, exitTime: 0.80f, duration: 0.10f);
            AddActionStateExitTransitions(dodgeLeftState,   idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.10f);
            AddActionStateExitTransitions(dodgeRightState,  idleState, combatIdleState, combatLocoState, exitTime: 0.85f, duration: 0.10f);

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Zombera] Player_Default controller rebuilt.\n"
                + "  States   : Idle | CombatIdle | Locomotion(walk+jog 2D) | CombatLocomotion(walk 2D) | Sprint | Crouch | Crawl | Sit | BowAim | Attack_Jab/Cross/Hook/Uppercut/Knee/Combo/LowKick | Hit | Dead\n"
                + "  Sprint   : separate state — enters on IsSprinting=true, consumes stamina via UnitController\n"
                + "  Walk/Jog : speed-based 2D blend tree (VelocityX/VelocityZ, normalized 0-1)\n"
                + "  Combat   : IsInCombat=true uses CombatIdle when stationary and walk-only directional blend while moving\n"
                + "  Attacks  : weighted triggers supported (AttackJab/Cross/Hook/Uppercut/Knee/Combo/LowKick)\n"
                + "  Bow      : BowAim 1D blend (BowAimY) with Notch/Shoot/RapidFire sub-states");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static AnimationClip Clip(string name)
            => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsFolder}/{name}.anim");

        private static AnimationClip ClipAny(params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string candidate = names[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                AnimationClip clip = Clip(candidate);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private static AnimationClip ClipFromFbx(string relativeFbxPath, string preferredClipName = null, bool strictPreferredName = false)
        {
            if (string.IsNullOrWhiteSpace(relativeFbxPath))
            {
                return null;
            }

            string fullPath = $"{ClipsFolder}/{relativeFbxPath}";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fullPath);
            AnimationClip fallback = null;

            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip))
                {
                    continue;
                }

                if (clip.name.StartsWith("__preview__", System.StringComparison.OrdinalIgnoreCase))
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

            if (strictPreferredName && !string.IsNullOrWhiteSpace(preferredClipName))
            {
                Debug.LogWarning($"[Zombera] Clip '{preferredClipName}' was not found in '{fullPath}'. Falling back to standard idle for combat idle to avoid unstable retargeting.");
                return null;
            }

            return fallback;
        }

        private static BlendTree MakeTree(AnimatorController ctrl, string treeName)
        {
            BlendTree tree = new BlendTree
            {
                name            = treeName,
                blendType       = BlendTreeType.FreeformCartesian2D,
                blendParameter  = "VelocityX",
                blendParameterY = "VelocityZ"
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            return tree;
        }

        private static AnimatorStateTransition AnyTo(AnimatorStateMachine sm, AnimatorState dest,
            float duration)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(dest);
            t.duration            = duration;
            t.hasExitTime         = false;
            t.canTransitionToSelf = false;
            return t;
        }

        private static AnimatorStateTransition ExitTo(AnimatorState from, AnimatorState to,
            float exitTime, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime    = exitTime;
            t.duration    = duration;
            return t;
        }

        private static void AddActionStateExitTransitions(
            AnimatorState actionState,
            AnimatorState idleState,
            AnimatorState combatIdleState,
            AnimatorState combatLocoState,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition toCombatIdle = ExitTo(actionState, combatIdleState, exitTime, duration);
            toCombatIdle.AddCondition(AnimatorConditionMode.If, 0, "IsInCombat");
            toCombatIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            AnimatorStateTransition toCombatLoco = ExitTo(actionState, combatLocoState, exitTime, duration);
            toCombatLoco.AddCondition(AnimatorConditionMode.If, 0, "IsInCombat");
            toCombatLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = ExitTo(actionState, idleState, exitTime, duration);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsInCombat");
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to,
            string param, bool value)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
            t.duration    = 0.1f;
            t.hasExitTime = false;
        }
    }
}
