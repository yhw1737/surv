using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>An item definition (SCHEMA §Items). The base type every stack points at.</summary>
    public sealed class ItemDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@item.raw_meat"</c>. Never player-facing text.</summary>
        public string Name { get; init; }

        public string[] Tags { get; init; }
        public GridSize Grid { get; init; }

        /// <summary>Nominal weight in kg. An individual stack may weigh more or less.</summary>
        public float Weight { get; init; }

        public int Stack { get; init; }

        /// <summary>Icon path relative to the content root; null falls back to a placeholder shape (T-017).</summary>
        public string Icon { get; init; }

        /// <summary>Null for items that cannot wear out.</summary>
        public int? Durability { get; init; }

        /// <summary>Null for items that never spoil.</summary>
        public SpoilageSpec Spoilage { get; init; }

        /// <summary>Null for inedible items.</summary>
        public NutritionSpec Nutrition { get; init; }
    }

    /// <summary>Spoilage inputs. Progress is computed in one elapsed-time pass (ARCHITECTURE §Deferred simulation).</summary>
    public sealed class SpoilageSpec
    {
        public float BaseHours { get; init; }

        /// <summary>Spoilage multiplier per degree.</summary>
        public float TempFactor { get; init; }

        /// <summary>What this becomes when it spoils.</summary>
        public NamespacedId Result { get; init; }
    }

    /// <summary>Nutrition before cooking modifiers (SYS-COOK-01 step 2).</summary>
    public sealed class NutritionSpec
    {
        public float Hunger { get; init; }
        public float Thirst { get; init; }

        /// <summary>0..1 chance of illness from eating this raw.</summary>
        public float SanitationRisk { get; init; }
    }
}
