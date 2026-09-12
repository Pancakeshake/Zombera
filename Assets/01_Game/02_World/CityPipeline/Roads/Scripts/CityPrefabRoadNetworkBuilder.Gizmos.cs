using UnityEngine;
using Zombera.World.City;
using Zombera.World.CityPipeline.WorldBuilder.Tiles;

namespace Zombera.World.Roads
{
    public sealed partial class CityPrefabRoadNetworkBuilder
    {
        // ──────────────────────────────────────────────
        //  Scene gizmos
        // ──────────────────────────────────────────────

        [SerializeField, HideInInspector] private bool drawWorldRegionGizmos = true;

        private void OnDrawGizmos()
        {
            if (!drawWorldRegionGizmos)
                return;

            DrawWorldRegionGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (Layout == null) return;

            var center = ResolveLayoutCenterXZ();
            var previewSeed = Layout.layoutSeed != 0 ? Layout.layoutSeed : 12345;

            if (Layout.footprintShape == CityFootprintShape.Square)
            {
                Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.85f);
                Layout.GetFootprintSquare(center, out var x0, out var x1, out var z0, out var z1, previewSeed);
                DrawSquareGizmo(x0, x1, z0, z1);

                if (Layout.generateArterialRing)
                {
                    Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
                    Layout.GetArterialSquare(center, out x0, out x1, out z0, out z1, previewSeed);
                    DrawSquareGizmo(x0, x1, z0, z1);
                }
            }
            else
            {
                Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.85f);
                DrawCircleGizmo(center, Layout.cityRadiusMeters, 64);

                if (Layout.generateArterialRing)
                {
                    Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
                    DrawCircleGizmo(center, Layout.cityRadiusMeters * Layout.arterialRingRadiusNormalized, 48);
                }
            }
        }

        private void DrawWorldRegionGizmos()
        {
            var region = ActiveRegionAsset;
            if (region == null)
                return;

            if (!TryResolveWorldRegionGizmoBounds(out var terrainBounds))
                return;

            var groundY = SampleGroundHeight(terrainBounds.center);
            WorldRegionScatterGizmoDrawer.DrawRegionSites(
                region,
                groundY,
                (site, index) => TryResolveSiteFootprintRect(site, index, out var rect) ? rect : default);
        }

        private bool TryResolveWorldRegionGizmoBounds(out Rect terrainBounds)
        {
            terrainBounds = default;

            var stack = transform.Find("WorldBuilderStack");
            var catalog = stack != null ? stack.GetComponent<WorldTileCatalog>() : null;
            if (catalog != null && catalog.Session.WorldBoundsXZ.width > 0f)
            {
                terrainBounds = WorldTerrainBoundsResolver.Resolve(
                    catalog,
                    catalog.Session,
                    catalog.transform.root);
                if (terrainBounds.width > 0f)
                    return true;
            }

            if (WorldTileInfoUtility.TryGetWorldTerrainGridBounds(out terrainBounds))
                return true;

            if (_hasPipelineWorldBounds &&
                _pipelineWorldBoundsXZ.width > 0f &&
                _pipelineWorldBoundsXZ.height > 0f)
            {
                terrainBounds = _pipelineWorldBoundsXZ;
                return true;
            }

            return false;
        }

        private static void DrawSquareGizmo(float xMin, float xMax, float zMin, float zMax)
        {
            var a = new Vector3(xMin, 0f, zMin);
            var b = new Vector3(xMax, 0f, zMin);
            var c = new Vector3(xMax, 0f, zMax);
            var d = new Vector3(xMin, 0f, zMax);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        private static void DrawCircleGizmo(Vector2 center, float radius, int segments)
        {
            if (radius <= 0f) return;

            var prev = center + new Vector2(radius, 0f);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(
                    new Vector3(prev.x, 0f, prev.y),
                    new Vector3(next.x, 0f, next.y));
                prev = next;
            }
        }
    }
}
