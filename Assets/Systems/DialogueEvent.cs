using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombera.Systems
{
    /// <summary>
    /// Data asset for survivor dialogue events used during recruitment interactions.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombera/Systems/Dialogue Event", fileName = "DialogueEvent")]
    public sealed class DialogueEvent : ScriptableObject
    {
        public string eventId;
        public string title;
        [TextArea(2, 6)] public string body;
        public List<DialogueOption> options = new List<DialogueOption>();

        // TODO: Add branching conditions and consequence payloads.
        // TODO: Add localization keys.
    }

    [Serializable]
    public sealed class DialogueOption
    {
        public string optionText;
        public int moraleImpact;
        public int recruitmentChanceModifier;
    }
}