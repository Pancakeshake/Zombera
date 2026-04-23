using UnityEngine;
using Zombera.Characters;

namespace Zombera.Inventory
{
    /// <summary>
    /// World pickup that transfers an item stack into a UnitInventory on interaction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField] private bool spawnVisualFromItemDefinition = true;
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private GameObject spawnedVisualRoot;

        public ItemDefinition ItemDefinition => itemDefinition;
        public int Quantity => Mathf.Max(1, quantity);

        private void Awake()
        {
            EnsureVisual();
        }

        public void Initialize(ItemDefinition definition, int stackQuantity)
        {
            itemDefinition = definition;
            quantity = Mathf.Max(1, stackQuantity);

            if (!spawnVisualFromItemDefinition)
            {
                return;
            }

            if (spawnedVisualRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedVisualRoot);
                }
                else
                {
                    DestroyImmediate(spawnedVisualRoot);
                }

                spawnedVisualRoot = null;
            }

            EnsureVisual();
        }

        public bool TryPickup(UnitInventory inventory)
        {
            if (inventory == null || itemDefinition == null)
            {
                return false;
            }

            if (!inventory.AddItem(itemDefinition, Mathf.Max(1, quantity)))
            {
                return false;
            }

            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        private void EnsureVisual()
        {
            if (!spawnVisualFromItemDefinition || itemDefinition == null || itemDefinition.worldPickupPrefab == null)
            {
                return;
            }

            if (spawnedVisualRoot != null)
            {
                return;
            }

            spawnedVisualRoot = Instantiate(itemDefinition.worldPickupPrefab, transform);
            spawnedVisualRoot.name = itemDefinition.worldPickupPrefab.name + "_WorldVisual";
            spawnedVisualRoot.transform.localPosition = Vector3.zero;
            spawnedVisualRoot.transform.localRotation = Quaternion.identity;
            spawnedVisualRoot.transform.localScale = Vector3.one;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            quantity = Mathf.Max(1, quantity);
        }
#endif
    }
}
