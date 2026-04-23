using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombera.Systems
{
    /// <summary>
    /// Renders a soft directional blind-spot overlay behind the player's current facing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogOfWarVisionOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FogOfWarVisionSource visionSource;

        [Header("Overlay")]
        [SerializeField] private bool overlayEnabled = true;
        [SerializeField] private bool showOverlayInEditMode;
        [SerializeField, Min(0.01f)] private float overlayHeightOffsetMeters = 0.12f;
        [SerializeField, Min(2f)] private float outerOverlayRadiusMeters = 1200f;
        [SerializeField, Min(0.1f)] private float edgeFadeWidthMeters = 7f;
        [SerializeField, Min(64)] private int radialSegments = 384;
        [SerializeField] private Color fogOverlayColor = new Color(0f, 0f, 0f, 0.78f);
        [SerializeField] private bool drawBlindSpotConeOverlay = true;
        [SerializeField] private Color blindSpotOverlayColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float blindSpotCenterAlphaMultiplier = 0.28f;
        [SerializeField, Range(0.05f, 0.95f)] private float blindSpotMiddleRadiusMultiplier = 0.35f;
        [SerializeField, Range(0f, 60f)] private float blindSpotEdgeSoftnessDegrees = 28f;
        [SerializeField, Range(0.5f, 0.999f)] private float blindSpotOuterFadeStartMultiplier = 0.93f;
        [SerializeField] private bool useVerticalBlindSpotVolume = true;
        [SerializeField, Min(1f)] private float blindSpotVerticalCoverageHeightMeters = 2000f;

        private const string OverlayRootName = "FogVisionOverlay";

        private GameObject overlayRoot;
        private MeshFilter overlayMeshFilter;
        private MeshRenderer overlayMeshRenderer;
        private Mesh overlayMesh;
        private Material overlayMaterial;
        private bool materialUsesVertexColor = true;

        private float cachedInnerRadius = -1f;
        private float cachedOuterRadius = -1f;
        private float cachedFadeWidth = -1f;
        private float cachedVisionAngle = -1f;
        private int cachedSegments = -1;
        private Color cachedColor = new Color(-1f, -1f, -1f, -1f);
        private bool cachedBlindSpotOverlayEnabled;
        private Color cachedBlindSpotColor = new Color(-1f, -1f, -1f, -1f);
        private float cachedBlindSpotEdgeSoftness = -1f;
        private float cachedBlindSpotOuterFadeStartMultiplier = -1f;
        private bool cachedUseVerticalBlindSpotVolume;
        private float cachedBlindSpotVerticalCoverageHeight = -1f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (!FogOfWarRuntimeConfig.FeatureEnabled)
            {
                if (Application.isPlaying)
                {
                    overlayRoot = ResolveExistingOverlayRoot();
                    if (overlayRoot != null)
                    {
                        Destroy(overlayRoot);
                        overlayRoot = null;
                    }

                    Destroy(this);
                }
                else
                {
                    enabled = false;
                }

                return;
            }

            ResolveReferences();

            if (!Application.isPlaying && !showOverlayInEditMode)
            {
                overlayRoot = ResolveExistingOverlayRoot();

                if (overlayRoot != null)
                {
                    if (overlayMeshFilter == null)
                    {
                        overlayMeshFilter = overlayRoot.GetComponent<MeshFilter>();
                    }

                    if (overlayMeshRenderer == null)
                    {
                        overlayMeshRenderer = overlayRoot.GetComponent<MeshRenderer>();
                    }

                    if (overlayMeshRenderer != null)
                    {
                        overlayMeshRenderer.enabled = false;
                    }
                }

                return;
            }

            EnsureOverlayObjects();
        }

        private void LateUpdate()
        {
            if (!FogOfWarRuntimeConfig.FeatureEnabled)
            {
                return;
            }

            ResolveReferences();

            bool allowOverlayInCurrentMode = Application.isPlaying || showOverlayInEditMode;
            if (!allowOverlayInCurrentMode)
            {
                if (overlayMeshRenderer != null)
                {
                    overlayMeshRenderer.enabled = false;
                }

                return;
            }

            EnsureOverlayObjects();

            if (overlayMeshRenderer == null || overlayRoot == null)
            {
                return;
            }

            bool canRender = overlayEnabled
                && visionSource != null
                && (Application.isPlaying ? visionSource.IsOperational : showOverlayInEditMode);

            overlayMeshRenderer.enabled = canRender;
            if (!canRender)
            {
                return;
            }

            overlayRoot.transform.localPosition = new Vector3(0f, overlayHeightOffsetMeters, 0f);
            overlayRoot.transform.localRotation = Quaternion.identity;

            float innerRadius = Mathf.Max(0.5f, visionSource.CurrentVisionRangeMeters);
            float outerRadius = Mathf.Max(innerRadius + 0.5f, outerOverlayRadiusMeters);
            float fadeWidth = Mathf.Clamp(edgeFadeWidthMeters, 0.1f, outerRadius - innerRadius);
            float visionAngle = Mathf.Clamp(visionSource.CurrentVisionAngleDegrees, 1f, 360f);
            int segments = Mathf.Max(24, radialSegments);
            bool useVerticalVolume = useVerticalBlindSpotVolume;
            float verticalCoverageHeight = Mathf.Max(1f, blindSpotVerticalCoverageHeightMeters);

            if (NeedsMeshRebuild(
                innerRadius,
                outerRadius,
                fadeWidth,
                visionAngle,
                segments,
                fogOverlayColor,
                drawBlindSpotConeOverlay,
                blindSpotOverlayColor,
                blindSpotEdgeSoftnessDegrees,
                blindSpotOuterFadeStartMultiplier,
                useVerticalVolume,
                verticalCoverageHeight))
            {
                RebuildOverlayMesh(
                    innerRadius,
                    outerRadius,
                    fadeWidth,
                    visionAngle,
                    segments,
                    fogOverlayColor,
                    drawBlindSpotConeOverlay,
                    blindSpotOverlayColor,
                    blindSpotEdgeSoftnessDegrees,
                    blindSpotOuterFadeStartMultiplier,
                    useVerticalVolume,
                    verticalCoverageHeight);
            }

            if (!materialUsesVertexColor)
            {
                ApplyMaterialTint(blindSpotOverlayColor);
            }
        }

        private void OnDisable()
        {
            if (overlayMeshRenderer != null)
            {
                overlayMeshRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (overlayMesh != null)
            {
                DestroyImmediate(overlayMesh);
                overlayMesh = null;
            }

            if (overlayMaterial != null)
            {
                DestroyImmediate(overlayMaterial);
                overlayMaterial = null;
            }

            if (overlayRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(overlayRoot);
                }
                else
                {
                    DestroyImmediate(overlayRoot);
                }

                overlayRoot = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            overlayHeightOffsetMeters = Mathf.Max(0.01f, overlayHeightOffsetMeters);
            outerOverlayRadiusMeters = Mathf.Max(2f, outerOverlayRadiusMeters);
            edgeFadeWidthMeters = Mathf.Max(0.1f, edgeFadeWidthMeters);
            radialSegments = Mathf.Max(64, radialSegments);
            blindSpotCenterAlphaMultiplier = Mathf.Clamp01(blindSpotCenterAlphaMultiplier);
            blindSpotMiddleRadiusMultiplier = Mathf.Clamp(blindSpotMiddleRadiusMultiplier, 0.05f, 0.95f);
            blindSpotEdgeSoftnessDegrees = Mathf.Clamp(blindSpotEdgeSoftnessDegrees, 0f, 60f);
            blindSpotOuterFadeStartMultiplier = Mathf.Clamp(blindSpotOuterFadeStartMultiplier, 0.5f, 0.999f);
            blindSpotVerticalCoverageHeightMeters = Mathf.Max(1f, blindSpotVerticalCoverageHeightMeters);

            ResolveReferences();

            cachedInnerRadius = -1f;
            cachedOuterRadius = -1f;
            cachedFadeWidth = -1f;
            cachedVisionAngle = -1f;
            cachedSegments = -1;
            cachedColor = new Color(-1f, -1f, -1f, -1f);
            cachedBlindSpotOverlayEnabled = false;
            cachedBlindSpotColor = new Color(-1f, -1f, -1f, -1f);
            cachedBlindSpotEdgeSoftness = -1f;
            cachedBlindSpotOuterFadeStartMultiplier = -1f;
            cachedUseVerticalBlindSpotVolume = false;
            cachedBlindSpotVerticalCoverageHeight = -1f;
        }
#endif

        private void ResolveReferences()
        {
            if (visionSource == null)
            {
                visionSource = GetComponent<FogOfWarVisionSource>();
            }
        }

        private void EnsureOverlayObjects()
        {
            // Always reconcile existing children first so stale duplicates get cleaned.
            GameObject existingOverlayRoot = ResolveExistingOverlayRoot();
            if (existingOverlayRoot != null)
            {
                overlayRoot = existingOverlayRoot;
            }

            if (overlayRoot == null)
            {
                overlayRoot = new GameObject(OverlayRootName);
                overlayRoot.transform.SetParent(transform, false);
                overlayRoot.transform.localPosition = new Vector3(0f, overlayHeightOffsetMeters, 0f);
                overlayRoot.transform.localRotation = Quaternion.identity;
                overlayRoot.transform.localScale = Vector3.one;
            }

            if (overlayMeshFilter == null)
            {
                overlayMeshFilter = overlayRoot.GetComponent<MeshFilter>();
                if (overlayMeshFilter == null)
                {
                    overlayMeshFilter = overlayRoot.AddComponent<MeshFilter>();
                }
            }

            if (overlayMeshRenderer == null)
            {
                overlayMeshRenderer = overlayRoot.GetComponent<MeshRenderer>();
                if (overlayMeshRenderer == null)
                {
                    overlayMeshRenderer = overlayRoot.AddComponent<MeshRenderer>();
                }

                overlayMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                overlayMeshRenderer.receiveShadows = false;
                overlayMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
                overlayMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                overlayMeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            if (overlayMesh == null)
            {
                overlayMesh = new Mesh
                {
                    name = "FogVisionOverlayMesh"
                };
                overlayMesh.MarkDynamic();
            }

            if (overlayMeshFilter.sharedMesh != overlayMesh)
            {
                overlayMeshFilter.sharedMesh = overlayMesh;
            }

            if (overlayMaterial == null)
            {
                overlayMaterial = CreateOverlayMaterial();
            }

            if (overlayMaterial != null && overlayMeshRenderer.sharedMaterial != overlayMaterial)
            {
                overlayMeshRenderer.sharedMaterial = overlayMaterial;
            }
        }

        private GameObject ResolveExistingOverlayRoot()
        {
            List<Transform> matches = new List<Transform>();
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name == OverlayRootName)
                {
                    matches.Add(child);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            Transform selectedRoot = matches[0];

            // Clean up duplicate overlay roots left behind by previous editor/domain reloads.
            for (int i = 1; i < matches.Count; i++)
            {
                if (matches[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(matches[i].gameObject);
                }
                else
                {
                    DestroyImmediate(matches[i].gameObject);
                }
            }

            return selectedRoot != null ? selectedRoot.gameObject : null;
        }

        private bool NeedsMeshRebuild(
            float innerRadius,
            float outerRadius,
            float fadeWidth,
            float visionAngle,
            int segments,
            Color color,
            bool includeBlindSpotOverlay,
            Color blindSpotColor,
            float blindSpotEdgeSoftness,
            float blindSpotOuterFadeStart,
            bool useVerticalVolume,
            float verticalCoverageHeight)
        {
            return overlayMesh == null
                || Mathf.Abs(innerRadius - cachedInnerRadius) > 0.02f
                || Mathf.Abs(outerRadius - cachedOuterRadius) > 0.02f
                || Mathf.Abs(fadeWidth - cachedFadeWidth) > 0.02f
                || Mathf.Abs(visionAngle - cachedVisionAngle) > 0.02f
                || segments != cachedSegments
                || color != cachedColor
                || includeBlindSpotOverlay != cachedBlindSpotOverlayEnabled
                || blindSpotColor != cachedBlindSpotColor
                || Mathf.Abs(blindSpotEdgeSoftness - cachedBlindSpotEdgeSoftness) > 0.02f
                || Mathf.Abs(blindSpotOuterFadeStart - cachedBlindSpotOuterFadeStartMultiplier) > 0.002f
                || useVerticalVolume != cachedUseVerticalBlindSpotVolume
                || Mathf.Abs(verticalCoverageHeight - cachedBlindSpotVerticalCoverageHeight) > 0.5f;
        }

        private void RebuildOverlayMesh(
            float innerRadius,
            float outerRadius,
            float fadeWidth,
            float visionAngle,
            int segments,
            Color color,
            bool includeBlindSpotOverlay,
            Color blindSpotColor,
            float blindSpotEdgeSoftness,
            float blindSpotOuterFadeStart,
            bool useVerticalVolume,
            float verticalCoverageHeight)
        {
            if (overlayMesh == null)
            {
                return;
            }

            float fadeOuterRadius = Mathf.Min(outerRadius - 0.05f, innerRadius + fadeWidth);
            if (fadeOuterRadius <= innerRadius)
            {
                fadeOuterRadius = innerRadius + 0.05f;
            }

            List<Vector3> vertices = new List<Vector3>((segments + 1) * 6);
            List<Color> colors = new List<Color>((segments + 1) * 6);
            List<Vector2> uvs = new List<Vector2>((segments + 1) * 6);
            List<Vector3> normals = new List<Vector3>((segments + 1) * 6);
            List<int> triangles = new List<int>(segments * 36);

            if (includeBlindSpotOverlay && visionAngle < 359f)
            {
                if (useVerticalVolume)
                {
                    AppendBlindSpotConeOverlayVolume(
                        innerRadius,
                        outerRadius,
                        visionAngle,
                        segments,
                        blindSpotColor,
                        blindSpotEdgeSoftness,
                        blindSpotOuterFadeStart,
                        Mathf.Max(1f, verticalCoverageHeight),
                        vertices,
                        colors,
                        uvs,
                        normals,
                        triangles);
                }
                else
                {
                    AppendBlindSpotConeOverlay(
                        innerRadius,
                        outerRadius,
                        visionAngle,
                        segments,
                        blindSpotColor,
                        blindSpotEdgeSoftness,
                        blindSpotOuterFadeStart,
                        vertices,
                        colors,
                        uvs,
                        normals,
                        triangles);
                }
            }

            overlayMesh.Clear();
            overlayMesh.SetVertices(vertices);
            overlayMesh.SetColors(colors);
            overlayMesh.SetUVs(0, uvs);
            overlayMesh.SetNormals(normals);
            overlayMesh.SetTriangles(triangles, 0);
            overlayMesh.RecalculateBounds();

            cachedInnerRadius = innerRadius;
            cachedOuterRadius = outerRadius;
            cachedFadeWidth = fadeWidth;
            cachedVisionAngle = visionAngle;
            cachedSegments = segments;
            cachedColor = color;
            cachedBlindSpotOverlayEnabled = includeBlindSpotOverlay;
            cachedBlindSpotColor = blindSpotColor;
            cachedBlindSpotEdgeSoftness = blindSpotEdgeSoftness;
            cachedBlindSpotOuterFadeStartMultiplier = blindSpotOuterFadeStart;
            cachedUseVerticalBlindSpotVolume = useVerticalVolume;
            cachedBlindSpotVerticalCoverageHeight = verticalCoverageHeight;
        }

        private void AppendBlindSpotConeOverlayVolume(
            float innerRadius,
            float outerRadius,
            float visionAngle,
            int segments,
            Color blindSpotColor,
            float edgeSoftnessDegrees,
            float outerFadeStartMultiplier,
            float verticalCoverageHeight,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uvs,
            List<Vector3> normals,
            List<int> triangles)
        {
            List<Vector3> planarVertices = new List<Vector3>();
            List<Color> planarColors = new List<Color>();
            List<Vector2> planarUvs = new List<Vector2>();
            List<int> planarTriangles = new List<int>();

            BuildBlindSpotConeTemplate(
                innerRadius,
                outerRadius,
                visionAngle,
                segments,
                blindSpotColor,
                edgeSoftnessDegrees,
                outerFadeStartMultiplier,
                planarVertices,
                planarColors,
                planarUvs,
                planarTriangles);

            if (planarVertices.Count == 0 || planarTriangles.Count == 0)
            {
                return;
            }

            float halfHeight = Mathf.Max(0.5f, verticalCoverageHeight * 0.5f);
            int baseVertexIndex = vertices.Count;

            for (int i = 0; i < planarVertices.Count; i++)
            {
                Vector3 planar = planarVertices[i];
                Color color = planarColors[i];
                Vector2 uv = planarUvs[i];

                vertices.Add(new Vector3(planar.x, -halfHeight, planar.z));
                colors.Add(color);
                uvs.Add(new Vector2(uv.x, 0f));
                normals.Add(Vector3.up);

                vertices.Add(new Vector3(planar.x, halfHeight, planar.z));
                colors.Add(color);
                uvs.Add(new Vector2(uv.x, 1f));
                normals.Add(Vector3.up);
            }

            Dictionary<long, int> edgeUseCounts = new Dictionary<long, int>(planarTriangles.Count);
            Dictionary<long, Vector2Int> firstSeenOrientedEdges = new Dictionary<long, Vector2Int>(planarTriangles.Count);

            for (int tri = 0; tri < planarTriangles.Count; tri += 3)
            {
                int a = planarTriangles[tri];
                int b = planarTriangles[tri + 1];
                int c = planarTriangles[tri + 2];

                int aBottom = baseVertexIndex + a * 2;
                int aTop = aBottom + 1;
                int bBottom = baseVertexIndex + b * 2;
                int bTop = bBottom + 1;
                int cBottom = baseVertexIndex + c * 2;
                int cTop = cBottom + 1;

                AddDoubleSidedTriangle(aTop, bTop, cTop, triangles);
                AddDoubleSidedTriangle(cBottom, bBottom, aBottom, triangles);

                RegisterEdge(a, b, edgeUseCounts, firstSeenOrientedEdges);
                RegisterEdge(b, c, edgeUseCounts, firstSeenOrientedEdges);
                RegisterEdge(c, a, edgeUseCounts, firstSeenOrientedEdges);
            }

            foreach (KeyValuePair<long, int> edgeEntry in edgeUseCounts)
            {
                if (edgeEntry.Value != 1)
                {
                    continue;
                }

                Vector2Int orientedEdge = firstSeenOrientedEdges[edgeEntry.Key];
                int a = orientedEdge.x;
                int b = orientedEdge.y;

                int aBottom = baseVertexIndex + a * 2;
                int aTop = aBottom + 1;
                int bBottom = baseVertexIndex + b * 2;
                int bTop = bBottom + 1;

                AddDoubleSidedQuad(aBottom, bBottom, aTop, bTop, triangles);
            }
        }

        private void BuildBlindSpotConeTemplate(
            float innerRadius,
            float outerRadius,
            float visionAngle,
            int segments,
            Color blindSpotColor,
            float edgeSoftnessDegrees,
            float outerFadeStartMultiplier,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uvs,
            List<int> triangles)
        {
            float blindSweepDegrees = Mathf.Clamp(360f - visionAngle, 0f, 359f);
            if (blindSweepDegrees <= 0.01f)
            {
                return;
            }

            float halfBlindSweep = blindSweepDegrees * 0.5f;
            float startDegrees = 180f - halfBlindSweep;
            float endDegrees = 180f + halfBlindSweep;

            int blindSegments = Mathf.Max(96, Mathf.CeilToInt(segments * (blindSweepDegrees / 360f)));
            float middleRadius = Mathf.Max(0.05f, innerRadius * blindSpotMiddleRadiusMultiplier);
            float clampedOuterFadeStart = Mathf.Clamp(outerFadeStartMultiplier, 0.5f, 0.999f);
            float outerFadeStartRadius = Mathf.Max(middleRadius + 0.05f, outerRadius * clampedOuterFadeStart);

            float centerAlpha = blindSpotColor.a * blindSpotCenterAlphaMultiplier;
            float middleAlpha = blindSpotColor.a * Mathf.Lerp(blindSpotCenterAlphaMultiplier, 1f, 0.62f);
            float outerAlpha = blindSpotColor.a;

            const int ringCount = 4;
            int baseVertexIndex = vertices.Count;

            for (int segment = 0; segment <= blindSegments; segment++)
            {
                float t = segment / (float)blindSegments;
                float degrees = Mathf.Lerp(startDegrees, endDegrees, t);
                Vector3 direction = BuildPlanarDirectionFromForward(degrees);

                float boundaryDistance = Mathf.Min(Mathf.Abs(degrees - startDegrees), Mathf.Abs(endDegrees - degrees));
                float edgeBlend = edgeSoftnessDegrees <= 0.001f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(boundaryDistance / edgeSoftnessDegrees));

                vertices.Add(direction * 0.01f);
                vertices.Add(direction * middleRadius);
                vertices.Add(direction * outerFadeStartRadius);
                vertices.Add(direction * outerRadius);

                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, centerAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, middleAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, outerAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, 0f));

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(0.33f, t));
                uvs.Add(new Vector2(0.66f, t));
                uvs.Add(new Vector2(1f, t));
            }

            for (int segment = 0; segment < blindSegments; segment++)
            {
                int current = baseVertexIndex + segment * ringCount;
                int next = current + ringCount;

                AddSingleSidedBandTriangles(current, current + 1, next, next + 1, triangles);
                AddSingleSidedBandTriangles(current + 1, current + 2, next + 1, next + 2, triangles);
                AddSingleSidedBandTriangles(current + 2, current + 3, next + 2, next + 3, triangles);
            }
        }

        private static void AddSingleSidedBandTriangles(int innerCurrent, int outerCurrent, int innerNext, int outerNext, List<int> triangles)
        {
            triangles.Add(innerCurrent);
            triangles.Add(outerCurrent);
            triangles.Add(innerNext);

            triangles.Add(innerNext);
            triangles.Add(outerCurrent);
            triangles.Add(outerNext);
        }

        private static void AddDoubleSidedTriangle(int a, int b, int c, List<int> triangles)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);

            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
        }

        private static void AddDoubleSidedQuad(int a, int b, int c, int d, List<int> triangles)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);

            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(d);

            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);

            triangles.Add(d);
            triangles.Add(c);
            triangles.Add(b);
        }

        private static void RegisterEdge(
            int a,
            int b,
            Dictionary<long, int> edgeUseCounts,
            Dictionary<long, Vector2Int> firstSeenOrientedEdges)
        {
            long key = EncodeEdgeKey(a, b);
            if (edgeUseCounts.TryGetValue(key, out int count))
            {
                edgeUseCounts[key] = count + 1;
                return;
            }

            edgeUseCounts[key] = 1;
            firstSeenOrientedEdges[key] = new Vector2Int(a, b);
        }

        private static long EncodeEdgeKey(int a, int b)
        {
            int min = a < b ? a : b;
            int max = a < b ? b : a;
            return ((long)min << 32) | (uint)max;
        }

        private void AppendRadialFogRing(
            float innerRadius,
            float fadeOuterRadius,
            float outerRadius,
            int segments,
            Color color,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uvs,
            List<Vector3> normals,
            List<int> triangles)
        {
            const int ringCount = 3;
            int baseVertexIndex = vertices.Count;

            Color clear = new Color(color.r, color.g, color.b, 0f);
            Color dark = color;

            for (int segment = 0; segment <= segments; segment++)
            {
                float t = segment / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices.Add(new Vector3(cos * innerRadius, 0f, sin * innerRadius));
                vertices.Add(new Vector3(cos * fadeOuterRadius, 0f, sin * fadeOuterRadius));
                vertices.Add(new Vector3(cos * outerRadius, 0f, sin * outerRadius));

                colors.Add(clear);
                colors.Add(dark);
                colors.Add(dark);

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(0.5f, t));
                uvs.Add(new Vector2(1f, t));

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int current = baseVertexIndex + segment * ringCount;
                int next = current + ringCount;

                AddDoubleSidedBandTriangles(current, current + 1, next, next + 1, triangles);
                AddDoubleSidedBandTriangles(current + 1, current + 2, next + 1, next + 2, triangles);
            }
        }

        private void AppendBlindSpotConeOverlay(
            float innerRadius,
            float outerRadius,
            float visionAngle,
            int segments,
            Color blindSpotColor,
            float edgeSoftnessDegrees,
            float outerFadeStartMultiplier,
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> uvs,
            List<Vector3> normals,
            List<int> triangles)
        {
            float blindSweepDegrees = Mathf.Clamp(360f - visionAngle, 0f, 359f);
            if (blindSweepDegrees <= 0.01f)
            {
                return;
            }

            float halfBlindSweep = blindSweepDegrees * 0.5f;
            float startDegrees = 180f - halfBlindSweep;
            float endDegrees = 180f + halfBlindSweep;

            int blindSegments = Mathf.Max(96, Mathf.CeilToInt(segments * (blindSweepDegrees / 360f)));
            float middleRadius = Mathf.Max(0.05f, innerRadius * blindSpotMiddleRadiusMultiplier);
            float clampedOuterFadeStart = Mathf.Clamp(outerFadeStartMultiplier, 0.5f, 0.999f);
            float outerFadeStartRadius = Mathf.Max(middleRadius + 0.05f, outerRadius * clampedOuterFadeStart);

            float centerAlpha = blindSpotColor.a * blindSpotCenterAlphaMultiplier;
            float middleAlpha = blindSpotColor.a * Mathf.Lerp(blindSpotCenterAlphaMultiplier, 1f, 0.62f);
            float outerAlpha = blindSpotColor.a;

            const int ringCount = 4;
            int baseVertexIndex = vertices.Count;

            for (int segment = 0; segment <= blindSegments; segment++)
            {
                float t = segment / (float)blindSegments;
                float degrees = Mathf.Lerp(startDegrees, endDegrees, t);
                Vector3 direction = BuildPlanarDirectionFromForward(degrees);

                float boundaryDistance = Mathf.Min(Mathf.Abs(degrees - startDegrees), Mathf.Abs(endDegrees - degrees));
                float edgeBlend = edgeSoftnessDegrees <= 0.001f
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(boundaryDistance / edgeSoftnessDegrees));

                vertices.Add(direction * 0.01f);
                vertices.Add(direction * middleRadius);
                vertices.Add(direction * outerFadeStartRadius);
                vertices.Add(direction * outerRadius);

                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, centerAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, middleAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, outerAlpha * edgeBlend));
                colors.Add(new Color(blindSpotColor.r, blindSpotColor.g, blindSpotColor.b, 0f));

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(0.33f, t));
                uvs.Add(new Vector2(0.66f, t));
                uvs.Add(new Vector2(1f, t));

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
            }

            for (int segment = 0; segment < blindSegments; segment++)
            {
                int current = baseVertexIndex + segment * ringCount;
                int next = current + ringCount;

                AddDoubleSidedBandTriangles(current, current + 1, next, next + 1, triangles);
                AddDoubleSidedBandTriangles(current + 1, current + 2, next + 1, next + 2, triangles);
                AddDoubleSidedBandTriangles(current + 2, current + 3, next + 2, next + 3, triangles);
            }
        }

        private static Vector3 BuildPlanarDirectionFromForward(float degreesFromForward)
        {
            float radians = degreesFromForward * Mathf.Deg2Rad;
            float x = Mathf.Sin(radians);
            float z = Mathf.Cos(radians);
            return new Vector3(x, 0f, z);
        }

        private static void AddDoubleSidedBandTriangles(int innerCurrent, int outerCurrent, int innerNext, int outerNext, List<int> triangles)
        {
            // Top-facing
            triangles.Add(innerCurrent);
            triangles.Add(outerCurrent);
            triangles.Add(innerNext);

            triangles.Add(innerNext);
            triangles.Add(outerCurrent);
            triangles.Add(outerNext);

            // Bottom-facing
            triangles.Add(innerNext);
            triangles.Add(outerCurrent);
            triangles.Add(innerCurrent);

            triangles.Add(outerNext);
            triangles.Add(outerCurrent);
            triangles.Add(innerNext);
        }

        private Material CreateOverlayMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            materialUsesVertexColor = shader != null;

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                materialUsesVertexColor = false;
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
                materialUsesVertexColor = false;
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
                materialUsesVertexColor = false;
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "FogVisionOverlayMaterial",
                renderQueue = (int)RenderQueue.Transparent
            };

            if (materialUsesVertexColor)
            {
                ApplyMaterialTint(material, Color.white);
            }
            else
            {
                ApplyMaterialTint(material, fogOverlayColor);
            }

            return material;
        }

        private void ApplyMaterialTint(Color tint)
        {
            if (overlayMaterial == null)
            {
                return;
            }

            ApplyMaterialTint(overlayMaterial, tint);
        }

        private static void ApplyMaterialTint(Material material, Color tint)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
        }
    }
}
