using System.Collections;
using UnityEngine;

namespace Zombera.World.CityPipeline.WorldBuilder
{
    public interface IWorldSurfaceSyncBackend
    {
        void MarkDirty(Terrain terrain, RectInt alphamapRect);
        void ResetToBase(Terrain terrain, RectInt alphamapRect, int baseLayerIndex);
        IEnumerator Flush();

        /// <summary>
        ///     True when this backend owns alphamap→control sync for <paramref name="terrain"/>
        ///     (e.g. MapMagic MaterialPropertySerializer path).
        /// </summary>
        bool OwnsControlSync(Terrain terrain);

        /// <summary>
        ///     Writes alphamap weights into vendor control textures for the dirty rect.
        ///     No-op when <see cref="OwnsControlSync"/> is false.
        /// </summary>
        void SyncAlphamapsToControls(Terrain terrain, float[,,] alphas, int xBase, int zBase);

        bool TryGetControlTexture(Terrain terrain, int controlIndex, out Texture2D texture);

        void SetControlTexture(Terrain terrain, int controlIndex, Texture2D texture);

        void ApplyControls(Terrain terrain);
    }
}
