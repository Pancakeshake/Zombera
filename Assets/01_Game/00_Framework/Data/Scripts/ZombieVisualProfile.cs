#region

using UnityEngine;

#endregion

namespace Zombera.Data
{
    /// <summary>
    ///     Zombie visual profile used by runtime appearance randomization.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Data/Zombie Visual Profile", fileName = "ZombieVisualProfile")]
    public sealed class ZombieVisualProfile : ScriptableObject
    {
        [Header("Scale")]
        [Tooltip("Uniform local scale randomization range.")]
        public Vector2 scaleRange = new(0.95f, 1.10f);

        [Header("Renderer Tint")]
        [Tooltip("Apply subtle random tinting to renderer materials.")]
        public bool applyRendererTint = true;

        [Range(0f, 1f)] public float rendererTintStrength = 0.15f;

        [Tooltip("Color palette used when renderer tinting is enabled.")]
        public Color[] rendererTintPalette =
        {
            new(0.50f, 0.60f, 0.52f, 1f),
            new(0.58f, 0.54f, 0.48f, 1f),
            new(0.42f, 0.48f, 0.45f, 1f)
        };
    }
}
