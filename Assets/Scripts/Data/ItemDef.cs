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

        /// <summary>One of <c>head chest legs feet back belt main_hand off_hand</c> (SYS-INV-01
        /// §Containers), null for items that can't be equipped. A plain string, not an enum — same
        /// reasoning as <see cref="SkillDef.Pool"/> (T-018): the closed set is a validator concern.</summary>
        public string EquipSlot { get; init; }

        /// <summary>Bag-type equipment only (backpacks, belt pouches): the separate grid it opens
        /// when equipped (SYS-INV-01 §Containers: "Bags add a separate grid; the base grid never
        /// grows"). Null for non-bag equipment. Distinct from <see cref="Grid"/>, which is this
        /// item's own footprint while it sits inside another container.</summary>
        public GridSize? BagGrid { get; init; }

        /// <summary>The <see cref="WeaponDef"/> this item wields as, when it's a weapon (SCHEMA §Items).
        /// Invalid (default) for everything else — the item is just an item.</summary>
        public NamespacedId Weapon { get; init; }

        /// <summary>The <see cref="WorldObjectDef"/> this item becomes when placed (a campfire kit, a crate).
        /// Invalid (default) for items that can't be placed.</summary>
        public NamespacedId Places { get; init; }

        /// <summary>Seeds only: the <see cref="CropDef"/> planted when this item is placed (its <see cref="Places"/>
        /// names the plot object).</summary>
        public NamespacedId Plants { get; init; }

        /// <summary>Clothing only: degrees added to <c>clothingBonus</c> in SYS-SURV-01's target temperature while
        /// worn. 0 for everything else.</summary>
        public float Warmth { get; init; }

        /// <summary>Dishes only: buffs granted on eating, from the cook's tag reactions (SYS-COOK-01).</summary>
        public NamespacedId[] Buffs { get; init; }

        /// <summary>Dishes only: multiplier on those buffs' durations (method buff_duration, care tag).</summary>
        public float BuffDurationMult { get; init; } = 1f;

        /// <summary>Dishes only: SYS-COOK-01 dishSignature (method + main ingredient tag), for satiety fatigue.</summary>
        public string DishSignature { get; init; }

        /// <summary>Armor while worn, summed into SYS-COMBAT-01's <c>totalArmor</c>. 0 for everything else.</summary>
        public float Armor { get; init; }

        /// <summary>Skill level needed to use this item for its purpose (SYS-FISH-01: the rod needs Fishing 5). Null: none.</summary>
        public SkillRequirement Requires { get; init; }

        /// <summary>Tiles of light cast while equipped (a torch). 0 casts none.</summary>
        public float LightRadius { get; init; }

        /// <summary>How this item looks worn on the stick figure (SYS-CHAR-02). Null: not drawn while worn.</summary>
        public WearSpec Wear { get; init; }

        /// <summary>How this item looks held in the figure's hand (SYS-CHAR-02). Null: not drawn while held.</summary>
        public HoldSpec Hold { get; init; }
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
