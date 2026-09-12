using System;
using UnityEngine;

namespace Zombera.Core
{
    [Serializable]
    public sealed class FormationSaveData
    {
        public bool hasData;
        public int activeFormation;
        public float slotSpacing = 1.4f;
        public float depthSpacing = 1.4f;
        public float lateralBias = 1f;
        public float depthBias = 1f;
        public bool applyToWholeSquad = true;
    }
}
