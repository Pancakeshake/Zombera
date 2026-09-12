using UnityEngine;
using Zombera.Characters;
using Zombera.AI;
using Zombera.Systems;
using Zombera.Core;
using System.Collections.Generic;
using RuntimeZombieSpawner = Zombera.AI.ZombieSpawner;

namespace Zombera.Animation.Testing
{
    /// <summary>
    /// Debug UI for controlling animations on Player and Zombies in the Test_AnimationBlendTrees scene.
    /// Uses IMGUI for zero-dependency runtime control.
    /// </summary>
    public sealed class AnimationTestControllerUI : MonoBehaviour
    {
        private UnitController _playerController;
        private RuntimeZombieSpawner _zombieSpawner;

        private float _speed = 0f;
        private float _velocityX = 0f;
        private float _velocityZ = 0f;
        private bool _isInCombat = false;
        private bool _isSprinting = false;
        private bool _isCrouching = false;
        private bool _isCrawling = false;
        private float _playerHealth = 100f;

        private string[] _weaponTypes = { "None", "Melee", "Pistol", "Rifle", "Shotgun", "Bow" };
        private int _weaponIndex = 0;

        private void Start()
        {
            FindRefs();
        }

        private void FindRefs()
        {
            var units = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (unit.Role == UnitRole.Player)
                {
                    _playerController = unit;
                    break;
                }
            }
            _zombieSpawner = Object.FindAnyObjectByType<RuntimeZombieSpawner>();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, Screen.height - 20));
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b>Zombera Animation Tester</b>", GUILayout.Width(280));

            if (GUILayout.Button("Refresh References")) FindRefs();

            if (_playerController != null)
            {
                DrawPlayerControls();
            }
            else
            {
                GUILayout.Label("<color=red>Player not found!</color>");
            }

            GUILayout.Space(10);

            if (_zombieSpawner != null)
            {
                DrawZombieControls();
            }
            else
            {
                GUILayout.Label("<color=red>ZombieSpawner not found!</color>");
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawPlayerControls()
        {
            GUILayout.Label("<b>Player Controls</b>");
            
            GUILayout.Label($"Speed: {_speed:F2}");
            _speed = GUILayout.HorizontalSlider(_speed, 0f, 10f);

            GUILayout.Label($"Velocity X: {_velocityX:F2}");
            _velocityX = GUILayout.HorizontalSlider(_velocityX, -1f, 1f);

            GUILayout.Label($"Velocity Z: {_velocityZ:F2}");
            _velocityZ = GUILayout.HorizontalSlider(_velocityZ, -1f, 1f);

            GUILayout.Label($"Health: {_playerHealth:F0}");
            _playerHealth = GUILayout.HorizontalSlider(_playerHealth, 0f, 100f);

            _isInCombat = GUILayout.Toggle(_isInCombat, "Combat Mode");
            _isSprinting = GUILayout.Toggle(_isSprinting, "Sprinting");
            _isCrouching = GUILayout.Toggle(_isCrouching, "Crouching");
            _isCrawling = GUILayout.Toggle(_isCrawling, "Crawling");

            GUILayout.Space(5);
            GUILayout.Label("Weapon Type:");
            _weaponIndex = GUILayout.SelectionGrid(_weaponIndex, _weaponTypes, 3);

            // Apply to Player Animator
            var anim = _playerController.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetFloat("Speed", _speed);
                anim.SetFloat("VelocityX", _velocityX);
                anim.SetFloat("VelocityZ", _velocityZ);
                anim.SetBool("IsInCombat", _isInCombat);
                anim.SetBool("IsSprinting", _isSprinting);
                anim.SetBool("IsCrouching", _isCrouching);
                anim.SetBool("IsCrawling", _isCrawling);
                
                // Weapon specific booleans
                if (HasParameter(anim, "BowEquipped")) anim.SetBool("BowEquipped", _weaponTypes[_weaponIndex] == "Bow");
                if (HasParameter(anim, "HasWeapon")) anim.SetBool("HasWeapon", _weaponIndex > 0);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Attack")) anim.SetTrigger("AttackTrigger");
                if (GUILayout.Button("Hit")) anim.SetTrigger("HitTrigger");
                if (HasParameter(anim, "ReloadTrigger")) 
                {
                    if (GUILayout.Button("Reload")) anim.SetTrigger("ReloadTrigger");
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Kill Player") || _playerHealth <= 0) anim.SetBool("IsDead", true);
                if (GUILayout.Button("Revive Player") && _playerHealth > 0) anim.SetBool("IsDead", false);
                }

                // Apply health to UnitHealth if present
                var health = _playerController.GetComponent<UnitHealth>();
                if (health != null)
                {
                // Note: Manually setting health might conflict with systems, but for testing it's fine
                }
                }

                private bool HasParameter(Animator anim, string paramName)
                {
                if (anim == null) return false;
                foreach (var p in anim.parameters)
                {
                if (p.name == paramName) return true;
                }
                return false;
                }

        private void DrawZombieControls()
        {
            GUILayout.Label("<b>Zombie Controls</b>");
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn 1")) SpawnZombies(1);
            if (GUILayout.Button("Spawn 10")) SpawnZombies(10);
            if (GUILayout.Button("Spawn 50")) SpawnZombies(50);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Clear All Zombies"))
            {
                var zombies = Object.FindObjectsByType<ZombieController>(FindObjectsSortMode.None);
                foreach (var z in zombies) Destroy(z.gameObject);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Force All Hit"))
            {
                foreach (var z in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (z.gameObject.name.Contains("Zombie")) z.SetTrigger("HitTrigger");
                }
            }
            
            if (GUILayout.Button("Kill All Zombies"))
            {
                foreach (var z in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    if (z.gameObject.name.Contains("Zombie")) z.SetBool("IsDead", true);
                }
            }
        }

        private void SpawnZombies(int count)
        {
            if (_zombieSpawner == null)
            {
                Debug.LogWarning("[AnimTestUI] No ZombieSpawner in scene.");
                return;
            }

            var center = _playerController != null ? _playerController.transform.position : transform.position;
            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                var offset = Random.insideUnitSphere * 10f;
                offset.y = 0f;
                var zombie = _zombieSpawner.SpawnZombie(null, center + offset);
                if (zombie != null) spawned++;
            }

            if (spawned <= 0)
                Debug.LogWarning("[AnimTestUI] ZombieSpawner did not spawn any zombies. Check zombiePrefab wiring.");
            else
                Debug.Log($"[AnimTestUI] Spawned {spawned} zombie(s).");
        }
    }
}

