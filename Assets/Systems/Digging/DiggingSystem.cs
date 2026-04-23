using UnityEngine;

namespace Zombera.Systems.Digging
{
    /// <summary>
    /// Handles hold-to-dig behavior and paints a target terrain layer on completion.
    /// </summary>
    public sealed class DiggingSystem : MonoBehaviour
    {
        [Header("Dig Config")]
        [SerializeField] private float digDuration = 1.5f;
        [SerializeField] private float digRadius = 1.5f;
        [SerializeField] private int digLayerIndex = 0;

        public bool IsDigging { get; private set; }
        public float Progress01 { get; private set; }
        public Vector3 TargetPosition { get; private set; }

        private Terrain _targetTerrain;
        private float _elapsed;

        public bool StartDig(Vector3 worldPoint)
        {
            Terrain terrain = ResolveTerrainAt(worldPoint);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[DiggingSystem] Cannot start dig: no terrain at target point.");
                return false;
            }

            if (!CanModifyLayer(terrain.terrainData))
            {
                return false;
            }

            _targetTerrain = terrain;
            TargetPosition = worldPoint;
            _elapsed = 0f;
            Progress01 = 0f;
            IsDigging = true;
            return true;
        }

        public void CancelDig()
        {
            IsDigging = false;
            _elapsed = 0f;
            Progress01 = 0f;
        }

        private void Update()
        {
            if (!IsDigging)
            {
                return;
            }

            if (_targetTerrain == null || _targetTerrain.terrainData == null)
            {
                CancelDig();
                return;
            }

            float safeDuration = Mathf.Max(0.05f, digDuration);
            _elapsed += Time.deltaTime;
            Progress01 = Mathf.Clamp01(_elapsed / safeDuration);

            if (Progress01 >= 1f)
            {
                ApplyDigToTerrain(_targetTerrain, TargetPosition);
                CancelDig();
            }
        }

        private bool CanModifyLayer(TerrainData data)
        {
            if (data.alphamapLayers <= 0)
            {
                Debug.LogWarning("[DiggingSystem] Terrain has no paintable layers.");
                return false;
            }

            if (digLayerIndex < 0 || digLayerIndex >= data.alphamapLayers)
            {
                Debug.LogWarning($"[DiggingSystem] digLayerIndex {digLayerIndex} is out of range for {data.alphamapLayers} layers.");
                return false;
            }

            return true;
        }

        private void ApplyDigToTerrain(Terrain terrain, Vector3 worldPoint)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || !CanModifyLayer(data))
            {
                return;
            }

            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = data.size;

            float nx = Mathf.Clamp01((worldPoint.x - terrainOrigin.x) / Mathf.Max(0.001f, terrainSize.x));
            float nz = Mathf.Clamp01((worldPoint.z - terrainOrigin.z) / Mathf.Max(0.001f, terrainSize.z));

            int centerX = Mathf.RoundToInt(nx * (data.alphamapWidth - 1));
            int centerZ = Mathf.RoundToInt(nz * (data.alphamapHeight - 1));

            int radiusX = Mathf.Max(1, Mathf.RoundToInt(digRadius * data.alphamapWidth / Mathf.Max(0.001f, terrainSize.x)));
            int radiusZ = Mathf.Max(1, Mathf.RoundToInt(digRadius * data.alphamapHeight / Mathf.Max(0.001f, terrainSize.z)));

            int minX = Mathf.Clamp(centerX - radiusX, 0, data.alphamapWidth - 1);
            int maxX = Mathf.Clamp(centerX + radiusX, 0, data.alphamapWidth - 1);
            int minZ = Mathf.Clamp(centerZ - radiusZ, 0, data.alphamapHeight - 1);
            int maxZ = Mathf.Clamp(centerZ + radiusZ, 0, data.alphamapHeight - 1);

            int width = maxX - minX + 1;
            int height = maxZ - minZ + 1;

            float[,,] alpha = data.GetAlphamaps(minX, minZ, width, height);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (minX + x - centerX) / (float)radiusX;
                    float dz = (minZ + z - centerZ) / (float)radiusZ;
                    float distance01 = Mathf.Sqrt(dx * dx + dz * dz);
                    if (distance01 > 1f)
                    {
                        continue;
                    }

                    float strength = 1f - distance01;
                    int layers = data.alphamapLayers;

                    float currentTarget = alpha[z, x, digLayerIndex];
                    float newTarget = Mathf.Lerp(currentTarget, 1f, strength);
                    float remaining = Mathf.Clamp01(1f - newTarget);

                    float otherSum = 0f;
                    for (int l = 0; l < layers; l++)
                    {
                        if (l == digLayerIndex)
                        {
                            continue;
                        }

                        otherSum += alpha[z, x, l];
                    }

                    if (otherSum <= 0.0001f)
                    {
                        for (int l = 0; l < layers; l++)
                        {
                            alpha[z, x, l] = l == digLayerIndex ? 1f : 0f;
                        }
                    }
                    else
                    {
                        for (int l = 0; l < layers; l++)
                        {
                            if (l == digLayerIndex)
                            {
                                alpha[z, x, l] = newTarget;
                            }
                            else
                            {
                                alpha[z, x, l] = (alpha[z, x, l] / otherSum) * remaining;
                            }
                        }
                    }
                }
            }

            data.SetAlphamaps(minX, minZ, alpha);
            terrain.Flush();
        }

        private static Terrain ResolveTerrainAt(Vector3 worldPoint)
        {
            Terrain active = Terrain.activeTerrain;
            if (ContainsPoint(active, worldPoint))
            {
                return active;
            }

            foreach (Terrain terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                if (ContainsPoint(terrain, worldPoint))
                {
                    return terrain;
                }
            }

            return active;
        }

        private static bool ContainsPoint(Terrain terrain, Vector3 worldPoint)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return worldPoint.x >= origin.x &&
                   worldPoint.x <= origin.x + size.x &&
                   worldPoint.z >= origin.z &&
                   worldPoint.z <= origin.z + size.z;
        }
    }
}