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

        [Header("Branching")]
        public List<DialogueBranch> branches = new List<DialogueBranch>();

        [Header("Localization")]
        public string titleLocKey;
        public string bodyLocKey;
    }

    [Serializable]
    public sealed class DialogueOption
    {
        public string optionText;
        public int moraleImpact;
        public int recruitmentChanceModifier;
    }

    /// <summary>
    /// Defines a conditional branch that fires a follow-up dialogue or outcome
    /// when the player selects a specific option.
    /// </summary>
    [Serializable]
    public sealed class DialogueBranch
    {
        public int triggerOptionIndex;
        public DialogueEvent nextEvent;
        public string consequenceTag; // e.g. "unlock_trade", "hostile_reaction"
    }
}