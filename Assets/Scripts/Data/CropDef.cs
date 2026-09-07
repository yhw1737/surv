using Isle.Core.Ids;

namespace Isle.Data
{
    /// <summary>A farmable crop (SCHEMA §Crops).</summary>
    public sealed class CropDef : IDefinition
    {
        public NamespacedId Id { get; init; }

        /// <summary>Language key, e.g. <c>"@crop.frost_barley"</c>.</summary>
        public string Name { get; init; }
        public string[] Tags { get; init; }
        public CropOutput Output { get; init; }
        public int GrowthDays { get; init; }

        /// <summary>Parsed but unused — breeding is post-EA (SCHEMA §Crops).</summary>
        public CropTraits Traits { get; init; }
    }

    /// <summary>What a mature plant yields.</summary>
    public sealed class CropOutput
    {
        public NamespacedId Item { get; init; }
        public int BaseCount { get; init; }
    }

    /// <summary>Heritable traits. Reserved for post-EA breeding.</summary>
    public sealed class CropTraits
    {
        public CropTrait GrowthSpeed { get; init; }
        public CropTrait Yield { get; init; }
    }

    /// <summary>A trait value and its spread across individuals.</summary>
    public sealed class CropTrait
    {
        public float Base { get; init; }
        public float Variance { get; init; }
    }
}
