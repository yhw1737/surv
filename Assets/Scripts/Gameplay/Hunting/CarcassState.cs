using Isle.Data;

namespace Isle.Gameplay.Hunting
{
    /// <summary>SYS-HUNT-01: what a carried carcass remembers — which animal, how heavy, its condition, how cleanly it was
    /// killed and how far it has spoiled. Rides with the bag item (<c>ItemWear.Carcass</c>) and turns back into a world
    /// carcass when it's put down.</summary>
    public sealed class CarcassState
    {
        public CreatureDef Def;
        public float WeightKg;
        public float Condition = 1f;
        public float KillFactor = 1f;
        public float Spoilage;
    }
}
