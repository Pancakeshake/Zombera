#region

using System;
using UnityEngine;

#endregion

namespace Zombera.UI
{
    internal static class SquadPortraitStripCaptureHelpers
    {
        private static readonly int SBaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int SMainTexId = Shader.PropertyToID("_MainTex");

        public static Vector3 ResolvePortraitForward(Transform head, Transform unitRoot)
        {
            if (unitRoot != null)
            {
                var rootForward = Vector3.ProjectOnPlane(unitRoot.forward, Vector3.up);
                if (rootForward.sqrMagnitude > 0.0001f)
                    return rootForward.normalized;
            }

            if (head != null)
            {
                var headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (headForward.sqrMagnitude > 0.0001f)
                {
                    // Humanoid head bones often point out the back of the skull; negate when root is unavailable.
                    return -headForward.normalized;
                }
            }

            return Vector3.forward;
        }

        public static bool TryResolvePortraitTexture(Transform unitRoot, Transform head, out Texture texture)
        {
            texture = null;
            if (unitRoot == null) return false;

            // First, prefer renderer nearest the head transform.
            if (head != null)
            {
                var headRenderer = head.GetComponentInChildren<Renderer>(true);
                if (headRenderer != null && TryGetTextureFromRenderer(headRenderer, out texture))
                    return true;
            }

            // Next, scan renderers and prefer those with head/face in the name.
            var renderers = unitRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                var rendererName = renderer.gameObject.name;
                if (string.IsNullOrEmpty(rendererName)) continue;

                var lowerName = rendererName.ToLowerInvariant();
                if ((lowerName.Contains("head") || lowerName.Contains("face"))
                    && TryGetTextureFromRenderer(renderer, out texture))
                    return true;
            }

            // Fallback: first renderer with a usable texture.
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (TryGetTextureFromRenderer(renderer, out texture))
                    return true;
            }

            return false;
        }

        public static bool TryGetTextureFromRenderer(Renderer renderer, out Texture texture)
        {
            texture = null;
            if (renderer == null) return false;

            var materials = renderer.sharedMaterials;
            if (materials != null)
            {
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!TryGetTextureFromMaterial(materials[i], out texture))
                        continue;

                    return true;
                }
            }

            return TryGetTextureFromMaterial(renderer.sharedMaterial, out texture);
        }

        public static bool TryGetTextureFromMaterial(Material mat, out Texture texture)
        {
            texture = null;
            if (mat == null) return false;

            if (mat.HasProperty(SBaseMapId)) texture = mat.GetTexture(SBaseMapId);
            if (texture == null && mat.HasProperty(SMainTexId)) texture = mat.GetTexture(SMainTexId);
            if (texture == null) texture = mat.mainTexture;

            return texture != null && texture.width > 0 && texture.height > 0;
        }

        public static bool TryFindOpaquePixelBounds(
            Color32[] pixels,
            int textureWidth,
            RectInt sourceRect,
            byte alphaCutoff,
            out RectInt bounds)
        {
            var minX = sourceRect.xMax;
            var minY = sourceRect.yMax;
            var maxX = sourceRect.xMin;
            var maxY = sourceRect.yMin;
            var found = false;

            for (var y = sourceRect.yMin; y < sourceRect.yMax; y++)
            {
                var row = y * textureWidth;
                for (var x = sourceRect.xMin; x < sourceRect.xMax; x++)
                {
                    if (pixels[row + x].a <= alphaCutoff) continue;

                    ExpandOpaqueBounds(x, y, ref found, ref minX, ref minY, ref maxX, ref maxY);
                }
            }

            if (!found)
            {
                bounds = sourceRect;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        public static Transform FindHeadTransform(Transform root)
        {
            if (root == null) return null;

            var animator = root.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null) return headBone;
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var child = all[i];
                var childName = child.name;
                if (string.IsNullOrEmpty(childName)) continue;

                var lowerName = childName.ToLowerInvariant();
                if (lowerName.Contains("head") || lowerName.Contains("face"))
                    return child;
            }

            return root;
        }

        private static void ExpandOpaqueBounds(
            int x,
            int y,
            ref bool found,
            ref int minX,
            ref int minY,
            ref int maxX,
            ref int maxY)
        {
            if (!found)
            {
                minX = maxX = x;
                minY = maxY = y;
                found = true;
                return;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
    }
}
