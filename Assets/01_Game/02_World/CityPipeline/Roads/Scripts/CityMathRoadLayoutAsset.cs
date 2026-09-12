using UnityEngine;

namespace Zombera.World.Roads
{
    /// <summary>
    ///     ScriptableObject wrapper for CityMathRoadLayout, so layout math lives
    ///     as a shareable asset instead of inline serialized data on the builder.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Zombera/City/Layout (CityMathRoadLayout)",
        fileName = "CityMathRoadLayout",
        order = 300)]
    public sealed class CityMathRoadLayoutAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Layout parameters for the math-driven city road grid.")]
        private CityMathRoadLayout data = new();

        public CityMathRoadLayout Data => data;

        public static CityMathRoadLayoutAsset CreateDefault()
        {
            var asset = CreateInstance<CityMathRoadLayoutAsset>();
            asset.data = new CityMathRoadLayout();
            return asset;
        }
    }
}
