using System;
using System.Collections.Generic;
using UnityEngine;
using Zombera.Inventory;

namespace Zombera.BaseBuilding
{
    /// <summary>
    /// Stores base materials/resources used by construction jobs.
    /// </summary>
    public sealed class BaseStorage : MonoBehaviour
    {
        [SerializeField] private List<MaterialStack> materialStacks = new List<MaterialStack>();

        public IReadOnlyList<MaterialStack> MaterialStacks => materialStacks;

        public void AddMaterial(ItemDefinition itemDefinition, int amount)
        {
            if (itemDefinition == null || amount <= 0)
            {
                return;
            }

            for (int i = 0; i < materialStacks.Count; i++)
            {
                if (materialStacks[i].item == itemDefinition)
                {
                    MaterialStack stack = materialStacks[i];
                    stack.amount += amount;
                    materialStacks[i] = stack;
                    return;
                }
            }

            materialStacks.Add(new MaterialStack(itemDefinition, amount));
        }

        public bool RemoveMaterial(ItemDefinition itemDefinition, int amount)
        {
            if (itemDefinition == null || amount <= 0)
            {
                return false;
            }

            for (int i = 0; i < materialStacks.Count; i++)
            {
                if (materialStacks[i].item != itemDefinition)
                {
                    continue;
                }

                MaterialStack stack = materialStacks[i];

                if (stack.amount < amount)
                {
                    return false;
                }

                stack.amount -= amount;

                if (stack.amount <= 0)
                {
                    materialStacks.RemoveAt(i);
                }
                else
                {
                    materialStacks[i] = stack;
                }

                return true;
            }

            return false;
        }

        public int GetAmount(ItemDefinition itemDefinition)
        {
            if (itemDefinition == null)
            {
                return 0;
            }

            for (int i = 0; i < materialStacks.Count; i++)
            {
                if (materialStacks[i].item == itemDefinition)
                {
                    return materialStacks[i].amount;
                }
            }

            return 0;
        }

        // TODO: Support capacity limits and storage network routing.
        // TODO: Track reservation system for pending construction jobs.
    }

    [Serializable]
    public struct MaterialStack
    {
        public ItemDefinition item;
        public int amount;

        public MaterialStack(ItemDefinition itemDefinition, int stackAmount)
        {
            item = itemDefinition;
            amount = stackAmount;
        }
    }
}