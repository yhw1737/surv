namespace Isle.Gameplay.Combat
{
    /// <summary>SYS-COMBAT-01 §Creature AI: Idle → Alert → Engage → Flee. The prototype skips Alert
    /// (no noise system yet), so a preset either reacts on sight or ignores you.</summary>
    public enum CreatureState
    {
        Idle,
        Alert,
        Engage,
        Flee,
    }

    /// <summary>
    /// Transition presets named by <c>CreatureDef.ai</c>. Static pure, no Unity refs. The preset
    /// decides <i>which</i> transition happens; the vision radius it's measured against comes from the
    /// def's own <c>combat.vision_tiles</c>, never from here (SYS-COMBAT-01: "all parameters load from
    /// definitions").
    /// </summary>
    public static class CreatureBrain
    {
        public const string Skittish = "isle:skittish";
        public const string TerritorialCharger = "isle:territorial_charger";
        public const string AlertThenFlee = "isle:alert_then_flee";
        public const string Ambusher = "isle:ambusher";

        /// <summary>Whether a creature whose def lists <paramref name="activeTimes"/> (its <c>spawn.time</c>) is up
        /// during <paramref name="phase"/>. No list, or <c>"any"</c>, means always.</summary>
        public static bool IsAwake(string[] activeTimes, string phase)
        {
            if (activeTimes == null || activeTimes.Length == 0) return true;
            foreach (var time in activeTimes)
                if (time == "any" || time == phase) return true;
            return false;
        }

        /// <summary>Ambushers lie still until something comes close; every other preset wanders while idle.</summary>
        public static bool IsStationary(string aiPreset) => aiPreset == Ambusher;

        /// <summary>Hysteresis: once reacting, a creature disengages only at twice its vision radius,
        /// so it doesn't flicker back to Idle at the exact edge.</summary>
        const float DisengageMultiplier = 2f;

        /// <param name="alertElapsedSeconds">How long the creature has been in <see cref="CreatureState.Alert"/>.</param>
        /// <param name="alertSeconds">Only for <see cref="AlertThenFlee"/>: how long the alert lasts before fleeing.</param>
        public static CreatureState Next(CreatureState current, string aiPreset, float distanceTiles, float visionTiles,
            float alertElapsedSeconds = 0f, float alertSeconds = 0f)
        {
            var reacting = current != CreatureState.Idle;
            var inRange = reacting
                ? distanceTiles <= visionTiles * DisengageMultiplier
                : distanceTiles <= visionTiles;

            switch (aiPreset)
            {
                case Skittish:
                    return inRange ? CreatureState.Flee : CreatureState.Idle;
                case TerritorialCharger:
                case Ambusher:
                    return inRange ? CreatureState.Engage : CreatureState.Idle;
                case AlertThenFlee:
                    // Fleeing keeps the usual hysteresis; alerting has none — a creature that loses sight
                    // of you stops alerting at once.
                    if (current == CreatureState.Flee) return inRange ? CreatureState.Flee : CreatureState.Idle;
                    if (distanceTiles > visionTiles) return CreatureState.Idle;
                    if (current != CreatureState.Alert) return CreatureState.Alert;
                    return alertElapsedSeconds >= alertSeconds ? CreatureState.Flee : CreatureState.Alert;
                default:
                    return CreatureState.Idle;
            }
        }
    }
}
