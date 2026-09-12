#region

using UnityEngine;

#endregion

namespace Zombera.Systems.Digging
{
    /// <summary>
    ///     Handles hold-to-dig behavior and paints a target terrain layer on completion.
    /// </summary>
    public sealed class DiggingSystem : MonoBehaviour
    {
        [Header("Dig Config")] [SerializeField]
        private float digDuration = 1.5f;

        [SerializeField] private float digRadius = 1.5f;
        [SerializeField] private int digLayerIndex;
        private float _elapsed;

        private Terrain _targetTerrain;

        public bool IsDigging { get; private set; }
        public float Progress01 { get; private set; }
        public Vector3 TargetPosition { get; private set; }

        private void Update()
        {
            if (!IsDigging) return;

            if (_targetTerrain == null || _targetTerrain.terrainData == null)
            {
                CancelDig();
                return;
            }

            var safeDuration = Mathf.Max(0.05f, digDuration);
            _elapsed += Time.deltaTime;
            Progress01 = Mathf.Clamp01(_elapsed / safeDuration);

            if (Progress01 < 1f) return;

            ApplyDigToTerrain(_targetTerrain, TargetPosition);
            CancelDig();
        }

        public bool StartDig(Vector3 worldPoint)
        {
            var terrain = ResolveTerrainAt(worldPoint);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning("[DiggingSystem] Cannot start dig: no terrain at target point.");
                return false;
            }

            if (!CanModifyLayer(terrain.terrainData)) return false;

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

        private bool CanModifyLayer(TerrainData data)
        {
            if (data.alphamapLayers <= 0)
            {
                Debug.LogWarning("[DiggingSystem] Terrain has no paintable layers.");
                return false;
            }

            if (digLayerIndex >= 0 && digLayerIndex < data.alphamapLayers) return true;

            Debug.LogWarning(
                $"[DiggingSystem] digLayerIndex {digLayerIndex} is out of range for {data.alphamapLayers} layers.");
            return false;
        }

        private void ApplyDigToTerrain(Terrain terrain, Vector3 worldPoint)
        {
            var data = terrain.terrainData;
            if (data == null || !CanModifyLayer(data)) return;

            if (!TryBuildDigPaintRegion(terrain, data, worldPoint, out var region)) return;

            var alpha = data.GetAlphamaps(region.MinX, region.MinZ, region.Width, region.Height);
            PaintDigRegion(data, alpha, region);

            data.SetAlphamaps(region.MinX, region.MinZ, alpha);
            terrain.Flush();
        }

        private bool TryBuildDigPaintRegion(Terrain terrain, TerrainData data, Vector3 worldPoint,
            out DigPaintRegion region)
        {
            region = default;

            var terrainOrigin = terrain.transform.position;
            var terrainSize = data.size;

            var nx = Mathf.Clamp01((worldPoint.x - terrainOrigin.x) / Mathf.Max(0.001f, terrainSize.x));
            var nz = Mathf.Clamp01((worldPoint.z - terrainOrigin.z) / Mathf.Max(0.001f, terrainSize.z));

            region.CenterX = Mathf.RoundToInt(nx * (data.alphamapWidth - 1));
            region.CenterZ = Mathf.RoundToInt(nz * (data.alphamapHeight - 1));

            region.RadiusX = Mathf.Max(1,
                Mathf.RoundToInt(digRadius * data.alphamapWidth / Mathf.Max(0.001f, terrainSize.x)));
            region.RadiusZ = Mathf.Max(1,
                Mathf.RoundToInt(digRadius * data.alphamapHeight / Mathf.Max(0.001f, terrainSize.z)));

            var maxX = Mathf.Clamp(region.CenterX + region.RadiusX, 0, data.alphamapWidth - 1);
            var maxZ = Mathf.Clamp(region.CenterZ + region.RadiusZ, 0, data.alphamapHeight - 1);

            region.MinX = Mathf.Clamp(region.CenterX - region.RadiusX, 0, data.alphamapWidth - 1);
            region.MinZ = Mathf.Clamp(region.CenterZ - region.RadiusZ, 0, data.alphamapHeight - 1);
            region.Width = maxX - region.MinX + 1;
            region.Height = maxZ - region.MinZ + 1;

            return region is { Width: > 0, Height: > 0 };
        }

        private void PaintDigRegion(TerrainData data, float[,,] alpha, DigPaintRegion region)
        {
            for (var z = 0; z < region.Height; z++)
            {
                for (var x = 0; x < region.Width; x++)
                {
                    var strength = ComputeDigStrength(region, x, z);
                    if (strength <= 0f) continue;

                    ApplyDigBlend(data.alphamapLayers, alpha, x, z, strength);
                }
            }
        }

        private static float ComputeDigStrength(DigPaintRegion region, int x, int z)
        {
            var dx = (region.MinX + x - region.CenterX) / (float)region.RadiusX;
            var dz = (region.MinZ + z - region.CenterZ) / (float)region.RadiusZ;
            var distance01 = Mathf.Sqrt(dx * dx + dz * dz);
            return distance01 > 1f ? 0f : 1f - distance01;
        }

        private void ApplyDigBlend(int layers, float[,,] alpha, int x, int z, float strength)
        {
            var currentTarget = alpha[z, x, digLayerIndex];
            var newTarget = Mathf.Lerp(currentTarget, 1f, strength);
            var remaining = Mathf.Clamp01(1f - newTarget);

            var otherSum = 0f;
            for (var l = 0; l < layers; l++)
            {
                if (l == digLayerIndex) continue;

                otherSum += alpha[z, x, l];
            }

            if (otherSum <= 0.0001f)
            {
                SetTargetOnlyBlend(layers, alpha, x, z);
                return;
            }

            NormalizeBlendLayers(layers, alpha, x, z, newTarget, remaining, otherSum);
        }

        private void SetTargetOnlyBlend(int layers, float[,,] alpha, int x, int z)
        {
            for (var l = 0; l < layers; l++) alpha[z, x, l] = l == digLayerIndex ? 1f : 0f;
        }

        private void NormalizeBlendLayers(int layers, float[,,] alpha, int x, int z, float newTarget, float remaining,
            float otherSum)
        {
            for (var l = 0; l < layers; l++)
                if (l == digLayerIndex)
                    alpha[z, x, l] = newTarget;
                else
                    alpha[z, x, l] = alpha[z, x, l] / otherSum * remaining;
        }

        private static Terrain ResolveTerrainAt(Vector3 worldPoint)
        {
            var active = Terrain.activeTerrain;
            if (ContainsPoint(active, worldPoint)) return active;

            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                if (ContainsPoint(terrain, worldPoint))
                    return terrain;

            return active;
        }

        private static bool ContainsPoint(Terrain terrain, Vector3 worldPoint)
        {
            if (terrain is not { terrainData: { } terrainData }) return false;

            var origin = terrain.transform.position;
            var size = terrainData.size;
            return worldPoint.x >= origin.x &&
                   worldPoint.x <= origin.x + size.x &&
                   worldPoint.z >= origin.z &&
                   worldPoint.z <= origin.z + size.z;
        }

        private struct DigPaintRegion
        {
            public int CenterX;
            public int CenterZ;
            public int RadiusX;
            public int RadiusZ;
            public int MinX;
            public int MinZ;
            public int Width;
            public int Height;
        }
    }
}