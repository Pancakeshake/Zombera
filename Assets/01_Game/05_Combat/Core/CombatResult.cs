namespace Zombera.Combat
{
    /// <summary>
    ///     Data-only output of a single combat exchange evaluation.
    /// </summary>
    public readonly struct CombatResult
    {
        public bool DidHit { get; }
        public bool IsCritical { get; }
        public float Damage { get; }
        public float HitChance01 { get; }
        public readonly float HitRoll01;
        public readonly float CriticalRoll01;

        public CombatResult(
            bool didHit,
            bool isCritical,
            float damage,
            float hitChance01,
            float hitRoll01,
            float criticalRoll01)
        {
            DidHit = didHit;
            IsCritical = isCritical;
            Damage = damage;
            HitChance01 = hitChance01;
            HitRoll01 = hitRoll01;
            CriticalRoll01 = criticalRoll01;
        }
    }
}