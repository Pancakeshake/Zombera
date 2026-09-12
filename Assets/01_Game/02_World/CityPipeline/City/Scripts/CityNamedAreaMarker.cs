using System.Collections.Generic;
using UnityEngine;
using Zombera.World.Roads;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zombera.World.City
{
    /// <summary>
    ///     Scene marker for one city block / named district in the prefab creation hub.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityNamedAreaMarker : MonoBehaviour
    {
        [SerializeField] private int areaId;
        [SerializeField] private string displayName = "Residential";
        [SerializeField] private string clusterName = string.Empty;
        [SerializeField] private int gridX;
        [SerializeField] private int gridZ;
        [SerializeField] private CityDistrictType districtType = CityDistrictType.Residential;
        [SerializeField] private Rect boundsXZ;
        [SerializeField] private float areaSquareMeters;
        [SerializeField] private Vector2 cityCenterXZ;
        [SerializeField] private CityBlockCornerMask roundedCorners = CityBlockCornerMask.None;
        [SerializeField] private float arterialCornerRadiusMeters;
        [SerializeField] private Vector2[] outlineXZ = System.Array.Empty<Vector2>();
        [SerializeField] private Vector2 gizmoPivotWorldXZ;
        [SerializeField] private bool hasGizmoPivot;

        private readonly List<Vector2> _hubShiftedOutlineScratch = new(16);

        public int AreaId => areaId;
        public string DisplayName => displayName;
        public string ClusterName => clusterName;
        public int GridX => gridX;
        public int GridZ => gridZ;
        public CityDistrictType DistrictType => districtType;
        public Rect BoundsXZ => boundsXZ;
        public float AreaSquareMeters => areaSquareMeters;
        public Vector2 CityCenterXZ => cityCenterXZ;
        public CityBlockCornerMask RoundedCorners => roundedCorners;
        public float ArterialCornerRadiusMeters => arterialCornerRadiusMeters;
        public Vector2[] OutlineXZ => outlineXZ;

        public void Apply(CityNamedArea area)
        {
            if (area == null) return;

            areaId = area.id;
            displayName = area.displayName;
            clusterName = area.clusterName;
            gridX = area.gridX;
            gridZ = area.gridZ;
            districtType = area.districtType;
            boundsXZ = area.boundsXZ;
            areaSquareMeters = area.areaSquareMeters;
            cityCenterXZ = area.cityCenterXZ;
            roundedCorners = area.roundedCorners;
            arterialCornerRadiusMeters = area.arterialCornerRadiusMeters;
            outlineXZ = area.outlineXZ ?? System.Array.Empty<Vector2>();
            name = "Area_" + displayName + "_" + areaId;
            RefreshGizmoPivot();
        }

        /// <summary>
        ///     Call after <see cref="Apply"/> once the transform world position is final.
        /// </summary>
        public void RefreshGizmoPivot()
        {
            gizmoPivotWorldXZ = new Vector2(transform.position.x, transform.position.z);
            hasGizmoPivot = true;
        }

        /// <summary>
        ///     World-space delta from serialized outline/bounds coords to the marker's current transform.
        /// </summary>
        public Vector2 GetHubShiftXZ()
        {
            if (!hasGizmoPivot)
                RefreshGizmoPivot();

            var pos = transform.position;
            return new Vector2(pos.x - gizmoPivotWorldXZ.x, pos.z - gizmoPivotWorldXZ.y);
        }

        public IReadOnlyList<Vector2> GetHubShiftedOutlineXZ()
        {
            var shift = GetHubShiftXZ();
            _hubShiftedOutlineScratch.Clear();

            if (outlineXZ != null && outlineXZ.Length >= 3)
            {
                for (var i = 0; i < outlineXZ.Length; i++)
                    _hubShiftedOutlineScratch.Add(outlineXZ[i] + shift);
                return _hubShiftedOutlineScratch;
            }

            var rect = boundsXZ;
            _hubShiftedOutlineScratch.Add(new Vector2(rect.xMin + shift.x, rect.yMin + shift.y));
            _hubShiftedOutlineScratch.Add(new Vector2(rect.xMax + shift.x, rect.yMin + shift.y));
            _hubShiftedOutlineScratch.Add(new Vector2(rect.xMax + shift.x, rect.yMax + shift.y));
            _hubShiftedOutlineScratch.Add(new Vector2(rect.xMin + shift.x, rect.yMax + shift.y));
            return _hubShiftedOutlineScratch;
        }

        public Rect GetHubShiftedBoundsXZ()
        {
            var shift = GetHubShiftXZ();
            return new Rect(boundsXZ.x + shift.x, boundsXZ.y + shift.y, boundsXZ.width, boundsXZ.height);
        }

        public static RoadCityZone ToRoadCityZone(CityDistrictType district)
        {
            return district switch
            {
                CityDistrictType.Residential => RoadCityZone.Residential,
                CityDistrictType.Commercial => RoadCityZone.Commercial,
                CityDistrictType.Industrial => RoadCityZone.Industrial,
                CityDistrictType.Hospital => RoadCityZone.Service,
                CityDistrictType.Military => RoadCityZone.Service,
                CityDistrictType.CityCore => RoadCityZone.CityCore,
                CityDistrictType.Park => RoadCityZone.Mixed,
                _ => RoadCityZone.Mixed
            };
        }

        private void OnDrawGizmos()
        {
            if (ShouldSuppressEditorGizmo(transform))
                return;

            DrawAreaGizmo(0.22f);
        }

        private void OnDrawGizmosSelected()
        {
            if (ShouldSuppressEditorGizmo(transform))
                return;

            DrawAreaGizmo(0.38f);
        }

        private static bool ShouldSuppressEditorGizmo(Transform areaTransform)
        {
            if (areaTransform == null)
                return false;

#if UNITY_EDITOR
            // DistrictFill meshes are the authoritative editor view — but only when
            // their renderer is actually enabled. When disabled (residential lots), gizmos
            // must fall through so lot fills are the sole visual.
            var fill = areaTransform.Find("DistrictFill");
            if (fill != null)
            {
                var mr = fill.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                    return true;
            }
#endif
            return false;
        }

        private void DrawAreaGizmo(float alpha)
        {
            if (boundsXZ.width <= 0f || boundsXZ.height <= 0f) return;

            var color = DistrictColor(districtType);
            color.a = alpha;
            var y = transform.position.y + 0.05f;
            var hubShift = GetHubShiftXZ();

            DrawAreaFill(color, hubShift, y);

            Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, alpha + 0.35f));
            DrawOutline(y, hubShift);
        }

        private void DrawAreaFill(Color color, Vector2 hubShift, float y)
        {
#if UNITY_EDITOR
            if (outlineXZ != null && outlineXZ.Length >= 3)
            {
                var fill = new Vector3[outlineXZ.Length];
                for (var i = 0; i < outlineXZ.Length; i++)
                    fill[i] = ToShiftedWorld(outlineXZ[i], hubShift, y);

                Handles.color = color;
                Handles.DrawAAConvexPolygon(fill);
                return;
            }
#endif
            DrawBoundsCubeFill(color, boundsXZ, hubShift, y);
        }

        private static void DrawBoundsCubeFill(Color color, Rect bounds, Vector2 hubShift, float y)
        {
            Gizmos.color = color;
            var center = ToShiftedWorld(bounds.center, hubShift, y);
            var size = new Vector3(bounds.width, 0.08f, bounds.height);
            Gizmos.DrawCube(center, size);
        }

        private static Vector3 ToShiftedWorld(Vector2 worldXZ, Vector2 hubShift, float y)
        {
            return new Vector3(worldXZ.x + hubShift.x, y, worldXZ.y + hubShift.y);
        }

        private void DrawOutline(float y, Vector2 hubShift)
        {
            if (outlineXZ != null && outlineXZ.Length >= 2)
            {
                for (var i = 0; i < outlineXZ.Length; i++)
                {
                    var a = ToShiftedWorld(outlineXZ[i], hubShift, y);
                    var b = ToShiftedWorld(outlineXZ[(i + 1) % outlineXZ.Length], hubShift, y);
                    Gizmos.DrawLine(a, b);
                }

                return;
            }

            DrawRectOutline(y, boundsXZ, hubShift);
        }

        private static void DrawRectOutline(float y, Rect rect, Vector2 hubShift)
        {
            var y0 = rect.yMin + hubShift.y;
            var y1 = rect.yMax + hubShift.y;
            var x0 = rect.xMin + hubShift.x;
            var x1 = rect.xMax + hubShift.x;
            var a = new Vector3(x0, y, y0);
            var b = new Vector3(x1, y, y0);
            var c = new Vector3(x1, y, y1);
            var d = new Vector3(x0, y, y1);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        public static Color DistrictColor(CityDistrictType district)
        {
            return district switch
            {
                CityDistrictType.Residential => new Color(0.35f, 0.72f, 1f),
                CityDistrictType.Commercial => new Color(1f, 0.82f, 0.2f),
                CityDistrictType.Industrial => new Color(0.75f, 0.75f, 0.78f),
                CityDistrictType.Hospital => new Color(1f, 0.35f, 0.45f),
                CityDistrictType.Military => new Color(0.45f, 0.85f, 0.45f),
                CityDistrictType.CityCore => new Color(1f, 0.55f, 0.15f),
                CityDistrictType.Park => new Color(0.3f, 0.9f, 0.45f),
                _ => new Color(0.8f, 0.8f, 0.85f)
            };
        }
    }
}
