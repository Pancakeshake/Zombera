using UnityEngine;

namespace Zombera.UI
{
    /// <summary>
    ///     Marks a crafting-tab editor preview row and records which runtime prefab it templates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftingTabLayoutTemplate : MonoBehaviour
    {
        public enum TemplateKind
        {
            Category,
            Recipe,
            Ingredient
        }

        [SerializeField] private TemplateKind templateKind;

        public TemplateKind Kind => templateKind;

        public void Configure(TemplateKind kind)
        {
            templateKind = kind;
        }
    }
}
