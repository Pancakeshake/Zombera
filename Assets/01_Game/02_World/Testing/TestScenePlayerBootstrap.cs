using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Zombera.Characters;
using Zombera.Core;
using Zombera.Systems;
using Zombera.UI;
using Zombera.World;

namespace Zombera.Testing
{
    /// <summary>
    /// Lightweight player bootstrap for test/dev scenes.
    /// Spawns the Player prefab, applies a random appearance,
    /// then enables input — without requiring GameManager or PlayerSpawner.
    ///
    /// Place this on any GameObject in your test scene.
    /// Assign <see cref="playerPrefab"/> (defaults to Assets/Prefabs/Player/Player.prefab),
    /// a <see cref="spawnPoint"/>, and optionally a <see cref="worldCamera"/>.
    /// </summary>
    public sealed class TestScenePlayerBootstrap : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("The Player prefab. Must have Unit, UnitController, and PlayerInputController.")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("Where to spawn the player. Falls back to this transform's position if unset.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Camera")]
        [Tooltip("The world camera with PlayerFollowCamera. Auto-found in scene if unset.")]
        [SerializeField] private Camera worldCamera;

        [Header("Appearance")]
        [Tooltip("Randomise spawned character appearance on spawn.")]
        [SerializeField] private bool randomiseAppearance;

        [Header("Debug")]
        [Tooltip("Emit detailed setup logs to help diagnose spawn/camera/bootstrap issues.")]
        [SerializeField] private bool verboseLogs = true;

        // ── Runtime state ──────────────────────────────────────────────────────
        private GameObject _playerInstance;

        private void Start()
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[TestScenePlayerBootstrap] No player prefab assigned.", this);
                return;
            }

            EnsurePlayableGameStateForTesting();

            var runtimeSpawner = FindFirstObjectByType<PlayerSpawner>();
            if (runtimeSpawner != null)
            {
                LogInfo("Runtime PlayerSpawner detected in scene; TestScenePlayerBootstrap will stand down.");
                enabled = false;
                return;
            }

            LogInfo($"Start: prefab='{playerPrefab.name}', spawnPoint={(spawnPoint != null ? spawnPoint.name : "(self)")}, camera={(worldCamera != null ? worldCamera.name : "(auto)")}, randomiseAppearance={randomiseAppearance}.");

            DisablePreviewArtifactsInScene();

            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            // 0. Wait for world readiness if WorldManager is present.
            yield return StartCoroutine(WaitForWorldReady());

            // 1. Instantiate
            var pos = spawnPoint != null ? spawnPoint.position : transform.position;
            var rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            _playerInstance = AcquireOrReusePlayerRoot(pos, rot);
            if (_playerInstance == null)
            {
                Debug.LogError("[TestScenePlayerBootstrap] Could not acquire or instantiate player instance.", this);
                yield break;
            }

            _playerInstance.name = "Player_Test";
            LogInfo($"Spawned '{_playerInstance.name}' at {pos} rotY={rot.eulerAngles.y:F1}.");

            RemoveKnownContaminants(_playerInstance);

            // Runtime rebuild/setup can recreate animators and clear their controllers.
            // Capture the initial controller so we can restore it after deferred appearance callbacks.
            var sourceAnimator = _playerInstance.GetComponentInChildren<Animator>();
            var preservedAnimatorController =
                sourceAnimator != null ? sourceAnimator.runtimeAnimatorController : null;

            var unit = _playerInstance.GetComponent<Unit>();
            if (unit != null)
                ApplyCharacterSelectionToPlayer(unit);
            else
                LogInfo("Spawned instance has no Unit component.");

            // 2. Disable input while spawn styling finishes
            var input = _playerInstance.GetComponent<PlayerInputController>();
            if (input != null)
            {
                input.enabled = false;
                LogInfo("PlayerInputController disabled for bootstrap.");
            }
            else
            {
                LogInfo("No PlayerInputController found on spawned player.");
            }

            // Match runtime: inject gameplay system refs and ensure one usable EventSystem.
            PlayerSpawnWiringService.WireInputSystems(_playerInstance);
            RuntimeUiEventSystemUtility.EnsureInteractiveEventSystem();
            LogInfo("Injected input systems + ensured UI EventSystem.");
            if (unit != null)
            {
                PlayerSpawnWiringService.BindHudToUnit(unit);
                LogInfo("Bound HUD to spawned player unit.");
            }

            // 3. Prepare spawn hierarchy cleanup/styling
            var avatar = SpawnAppearanceStylingService.PrepareAvatarForSanitizedSpawn(_playerInstance);
            var unitController = _playerInstance.GetComponent<UnitController>();
            var appearanceStyling = new SpawnAppearanceStylingService(this);
            LogInfo($"Spawn styling prepare result: avatarRoot={(avatar != null ? "found" : "missing")}, unitController={(unitController != null ? "found" : "missing")}.");

            // 4. Optionally randomise appearance via runtime visual variant spawner
            if (randomiseAppearance && avatar != null)
            {
                var spawner = _playerInstance.GetComponent<NpcAppearanceVariantSpawner>();
                if (spawner == null)
                    spawner = _playerInstance.AddComponent<NpcAppearanceVariantSpawner>();

                spawner.ApplyRandomAppearanceNow(force: true);
                LogInfo("Applied random appearance via runtime visual variant spawner.");
            }
            else
            {
                if (avatar != null)
                {
                    // Match runtime: always run next-frame spawn styling path.
                    var profileJson = string.Empty; // In bootstrap testing, we might not have a selection state set.
                    appearanceStyling.StartApplySelectedAppearanceNextFrame(_playerInstance, profileJson, avatar,
                        unitController);
        LogInfo("Queued runtime next-frame styling flow.");
                }
                else
                {
                    LogInfo("No preview/styling avatar root on spawned player.");
                }
            }

            // 6. Wire camera
            WireCamera(_playerInstance);
            StartCoroutine(CameraAndPreviewGuardRoutine(_playerInstance));

            // 6b. Match runtime: enable/warp NavMeshAgent only when NavMesh exists.
            if (unitController != null)
            {
                if (TryHasNearbyNavMesh(_playerInstance.transform.position, out var navPos))
                {
                    LogInfo($"Nearby NavMesh found at {navPos}; enabling agent now.");
                    unitController.ForceEnableAgent();
                }
                else
                {
                    LogInfo("No nearby NavMesh at bootstrap; waiting up to 12s for NavMesh before enabling agent.");
                    StartCoroutine(ForceEnableAgentWhenNavMeshReady(unitController));
                }
            }

            // 7. Give one frame for spawn-time styling/setup.
            if (avatar != null)
            {
                yield return null;
                LogInfo("Spawn styling frame completed.");
            }
            else
            {
                yield return null;
            }

            RestoreMissingAnimatorController(_playerInstance, preservedAnimatorController);

            // 8. Enable input
            if (input != null)
            {
                input.enabled = true;
                LogInfo("PlayerInputController re-enabled.");
            }

            Debug.Log("[TestScenePlayerBootstrap] Player ready.", _playerInstance);
        }

        private IEnumerator WaitForWorldReady()
        {
            var wm = FindFirstObjectByType<WorldManager>();
            if (wm == null)
            {
                LogInfo("No WorldManager found; skipping readiness wait.");
                yield break;
            }

            LogInfo("Waiting for world readiness...");
            var start = Time.unscaledTime;
            var timeout = wm.MaxSecondsToWaitForSpawnDependencyOrder;

            while (Time.unscaledTime - start < timeout)
            {
                if (wm.IsCharacterSpawnDependencyOrderReady(out var pending))
                {
                    LogInfo("World ready for character spawn.");
                    yield break;
                }

                if (verboseLogs)
                    LogInfo($"Still waiting for {pending}...");

                yield return new WaitForSecondsRealtime(0.5f);
            }

            Debug.LogWarning("[TestScenePlayerBootstrap] Timed out waiting for world readiness. Proceeding anyway.");
        }

        private void WireCamera(GameObject player)
        {
            if (player == null) return;

            var focusPoint = player.transform.position;
            LogInfo($"WireCamera: focus={focusPoint}, preferred={(worldCamera != null ? worldCamera.name : "(auto)")}.");
            var cam = PlayerSpawnWiringService.EnsureWorldCamera(worldCamera, focusPoint);
            if (cam == null)
            {
                Debug.LogWarning("[TestScenePlayerBootstrap] EnsureWorldCamera returned null; camera follow will fail.", this);
                return;
            }

            PlayerSpawnWiringService.BindCameraToUnit(player, cam);

            var follow = cam.GetComponent<PlayerFollowCamera>();
            LogInfo($"WireCamera complete: camera='{cam.name}', hasFollow={(follow != null)}, mainCamera={(Camera.main != null ? Camera.main.name : "none")}.");

            // Keep serialized reference in sync for later reruns in the same scene.
            worldCamera = cam;
        }

        private IEnumerator CameraAndPreviewGuardRoutine(GameObject player)
        {
            // Late startup systems can occasionally override camera follow or spawn preview artifacts.
            // Re-assert camera binding and preview cleanup a few times.
            var delays = new[] { 0.25f, 0.75f, 1.5f };
            for (var i = 0; i < delays.Length; i++)
            {
                yield return new WaitForSecondsRealtime(delays[i]);

                if (player == null) yield break;

                DisablePreviewArtifactsInScene();
                WireCamera(player);
                LogInfo($"Guard pass {i + 1}/{delays.Length}: reapplied preview cleanup + camera binding.");
            }
        }

        private void RestoreMissingAnimatorController(GameObject playerRoot,
            RuntimeAnimatorController fallbackController)
        {
            if (playerRoot == null || fallbackController == null) return;

            var animators = playerRoot.GetComponentsInChildren<Animator>(true);
            var restoredCount = 0;
            for (var i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator == null) continue;

                // Force restoration in test bootstrap to override runtime controller churn.
                if (animator.runtimeAnimatorController != fallbackController)
                {
                    animator.runtimeAnimatorController = fallbackController;
                    animator.Rebind();
                    animator.Update(0f);
                    restoredCount++;
                }
            }

            if (restoredCount > 0)
                LogInfo($"Restored AnimatorController on {restoredCount} animator(s) after spawn styling.");
        }

        private static bool TryHasNearbyNavMesh(Vector3 origin, out Vector3 navPosition)
        {
            const int walkableAreaMask = 1;
            var sampleOrigin = origin + Vector3.up * 2f;

            if (NavMesh.SamplePosition(sampleOrigin, out var hit, 24f, walkableAreaMask)
                || NavMesh.SamplePosition(sampleOrigin, out hit, 48f, NavMesh.AllAreas))
            {
                navPosition = hit.position;
                return true;
            }

            navPosition = Vector3.zero;
            return false;
        }

        private IEnumerator ForceEnableAgentWhenNavMeshReady(UnitController controller)
        {
            if (controller == null) yield break;

            const float timeoutSeconds = 12f;
            var start = Time.unscaledTime;
            var nextProgressLog = start + 1f;

            while (Time.unscaledTime - start < timeoutSeconds)
            {
                if (TryHasNearbyNavMesh(controller.transform.position, out var navPos))
                {
                    controller.ForceEnableAgent();
                    LogInfo($"NavMesh became available at {navPos}; ForceEnableAgent invoked.");
                    yield break;
                }

                if (verboseLogs && Time.unscaledTime >= nextProgressLog)
                {
                    nextProgressLog = Time.unscaledTime + 1f;
                    LogInfo($"Still waiting for NavMesh near {controller.transform.position}...");
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            Debug.LogWarning("[TestScenePlayerBootstrap] Timed out waiting for NavMesh; UnitController.ForceEnableAgent was not called.", this);
        }

        private static void ApplyCharacterSelectionToPlayer(Unit player)
        {
            if (player == null || !CharacterSelectionState.HasSelection) return;

            if (!string.IsNullOrWhiteSpace(CharacterSelectionState.SelectedCharacterName))
                player.name = CharacterSelectionState.SelectedCharacterName;

            if (player.Controller != null)
                player.Controller.SetMoveSpeed(CharacterSelectionState.SelectedMoveSpeed);

            if (player.Inventory != null)
                player.Inventory.SetWeightLimit(CharacterSelectionState.SelectedCarryCapacity);

            if (player.Stats != null)
            {
                player.Stats.ResetAllSkillsToLevelOne();
                player.Stats.SetStamina(CharacterSelectionState.SelectedStamina);
                player.Stats.SetStrengthBaseHealth(CharacterSelectionState.SelectedMaxHealth, true);
            }
            else if (player.Health != null)
            {
                player.Health.SetMaxHealth(CharacterSelectionState.SelectedMaxHealth, true);
            }
        }

        private GameObject AcquireOrReusePlayerRoot(Vector3 worldPosition, Quaternion worldRotation)
        {
            var scene = gameObject.scene;
            var units = FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            Unit best = null;
            var bestScore = int.MinValue;
            var candidateCount = 0;

            foreach (var unit in units)
            {
                if (!CanReusePlayerCandidate(unit, scene)) continue;

                candidateCount++;
                var score = ScorePlayerCandidate(unit);
                if (score <= bestScore) continue;

                best = unit;
                bestScore = score;
            }

            if (best != null)
            {
                var reused = best.gameObject;
                reused.transform.SetPositionAndRotation(worldPosition, worldRotation);
                LogInfo($"Reusing existing player candidate '{reused.name}' (candidates={candidateCount}, score={bestScore}).");
                return reused;
            }

            LogInfo("No reusable player candidate found; instantiating player prefab.");
            return Instantiate(playerPrefab, worldPosition, worldRotation);
        }

        private static bool CanReusePlayerCandidate(Unit unit, Scene scene)
        {
            if (unit == null) return false;

            var go = unit.gameObject;
            if (!go.activeInHierarchy) return false;
            if (go.scene != scene) return false;

            // Accept designated player role, or obvious player-like roots with input attached.
            if (unit.Role == UnitRole.Player) return true;
            if (go.GetComponent<PlayerInputController>() == null) return false;

            var n = go.name;
            return !string.IsNullOrWhiteSpace(n)
                   && n.IndexOf("player", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ScorePlayerCandidate(Unit unit)
        {
            var score = 0;
            if (unit.Role == UnitRole.Player) score += 4;

            var go = unit.gameObject;
            if (go.GetComponent<PlayerInputController>() != null) score += 3;

            var n = go.name;
            if (!string.IsNullOrWhiteSpace(n)
                && n.IndexOf("player", System.StringComparison.OrdinalIgnoreCase) >= 0)
                score += 2;

            if (go.activeInHierarchy) score += 1;
            return score;
        }

        private void DisablePreviewArtifactsInScene()
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var disabled = 0;
            foreach (var t in transforms)
            {
                if (t == null) continue;
                var go = t.gameObject;

                var name = go.name;
                var isPreview = name.StartsWith("UMAPreviewAvatar", System.StringComparison.Ordinal)
                                || name.StartsWith("UMAPreviewCamera", System.StringComparison.Ordinal)
                                || name.IndexOf("PreviewAvatar", System.StringComparison.OrdinalIgnoreCase) >= 0
                                || name.IndexOf("PreviewCamera", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isPreview) continue;

                if (go.TryGetComponent<Camera>(out var cam)) cam.enabled = false;

                if (!go.activeSelf) continue;

                go.SetActive(false);
                disabled++;
            }

            if (disabled > 0)
                LogInfo($"Disabled {disabled} preview artifact object(s) across loaded scenes.");
        }

        private void RemoveKnownContaminants(GameObject root)
        {
            if (root == null) return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            var removed = 0;
            foreach (var t in transforms)
            {
                if (t == null || t == root.transform) continue;

                if (!string.Equals(t.name, "Wall_Full_01", System.StringComparison.Ordinal)) continue;

                Destroy(t.gameObject);
                removed++;
            }

            if (removed > 0)
                LogInfo($"Removed {removed} known contaminated child object(s) from spawned player root.");
        }

        private void EnsurePlayableGameStateForTesting()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState is GameState.LoadingWorld or GameState.Playing or GameState.Paused) return;

            var previous = gm.CurrentState;
            gm.SetGameState(GameState.Playing);
            LogInfo($"Forced GameManager state to Playing for test scene (was {previous}).");
        }

        private void LogInfo(string message)
        {
            if (!verboseLogs) return;
            Debug.Log($"[TestScenePlayerBootstrap] {message}", this);
        }
    }
}
