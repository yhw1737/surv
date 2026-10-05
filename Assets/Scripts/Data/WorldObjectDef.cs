using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>SCHEMA.md §World objects. The def a placed <c>WorldObjectInstance</c> resolves
    /// against. <see cref="Spawn"/>, <see cref="Gather"/> and <see cref="Drink"/> are optional: a plain
    /// station (e.g. the campfire) sets none of them and is only ever placed by code.</summary>
    public sealed class WorldObjectDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@world_object.campfire"</c>. Never player-facing text.</summary>
        public string Name { get; init; }

        public string[] Tags { get; init; }

        /// <summary>Placeholder shape and colour. Null falls back to a tag-coloured circle.</summary>
        public VisualSpec Visual { get; init; }

        /// <summary>Where this object is scattered across the island (SYS-WORLD-03 §Placement). Null
        /// means it is never placed by the island generator.</summary>
        public SpawnSpec Spawn { get; init; }

        /// <summary>Harvestable node (SYS-WORLD-03 §Gathering). Null for non-harvestable objects.</summary>
        public GatherSpec Gather { get; init; }

        /// <summary>Water source (SYS-WORLD-03 §Water). Null for anything that isn't drinkable.</summary>
        public DrinkSpec Drink { get; init; }

        /// <summary>Where a line can be cast from (SYS-FISH-01). Null means nothing can be caught here.</summary>
        public FishingSpec Fishing { get; init; }

        /// <summary>A container's grid (SYS-INV-01 §Containers: wooden crate 10×6). Null for non-containers.</summary>
        public GridSize? Storage { get; init; }

        /// <summary>Collects rain to drink (SYS-SURV-01 §Water sources: rain catcher). Null otherwise.</summary>
        public RainCatcherSpec RainCatcher { get; init; }

        /// <summary>Tiles of light cast while active (a lit campfire). 0 casts none.</summary>
        public float LightRadius { get; init; }
    }

    public sealed class RainCatcherSpec
    {
        /// <summary>Drinks held when full. One drink = one unit.</summary>
        public float Capacity { get; init; }

        /// <summary>Units gained per real second of rain.</summary>
        public float FillPerSecond { get; init; }
    }

    /// <summary>Fishing conditions this spot provides. The terrain comes from the def's own
    /// <c>water/*</c> tag, and the water temperature from the ambient temperature — neither needs a field here.</summary>
    public sealed class FishingSpec
    {
        /// <summary>Depth of the spot, same units as the fish's <c>habitat.depth</c> band.</summary>
        public float Depth { get; init; }
    }

    /// <summary>What a harvestable node gives and how it comes back (SYS-WORLD-03 §Gathering).</summary>
    public sealed class GatherSpec
    {
        public NamespacedId Item { get; init; }
        public int Count { get; init; }

        /// <summary>Harvests before the node is depleted.</summary>
        public int Uses { get; init; }

        /// <summary>In-game minutes until a depleted node is restored.</summary>
        public float RespawnMinutes { get; init; }

        /// <summary>Stamina spent per harvest (SYS-SURV-01 §Stamina table, "gather").</summary>
        public float StaminaCost { get; init; }

        /// <summary>Tag a main-hand item needs for <see cref="ToolBonus"/> (e.g. <c>tool/axe</c>). Empty: no tool matters.</summary>
        public string ToolTag { get; init; }

        /// <summary>Extra units per harvest with the right tool in hand.</summary>
        public int ToolBonus { get; init; }
    }

    /// <summary>Names the water source; the thirst value itself lives in
    /// <c>VitalsCalculator.DrinkThirstDelta</c> (SYS-SURV-01 §Water sources), not in the def.</summary>
    public sealed class DrinkSpec
    {
        /// <summary>A <c>WaterSource</c> member name in snake_case, e.g. <c>"standing_water"</c>.</summary>
        public string Source { get; init; }
    }
}
