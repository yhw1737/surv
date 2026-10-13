using System;
using System.Collections.Generic;
using System.Linq;

namespace Isle.Gameplay.Character
{
    public enum BodyPart { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }

    public enum WoundKind { Cut, Puncture, Bruise, Burn }

    /// <summary>One wound (SYS-MED-01).</summary>
    public sealed class Wound
    {
        public BodyPart Part;
        public WoundKind Kind;
        public float Severity;
        public bool Tended;
        public float Quality;

        /// <summary>The worst this wound has been (decides a scar).</summary>
        public float Peak;

        /// <summary>In-game minutes since it was made.</summary>
        public float Age;

        /// <summary>0 = clean; grows to 1 when infected (§Infection).</summary>
        public float Infection;
        public bool InfectionChecked;

        /// <summary>§Fractures: a broken limb — heals slowly until splinted.</summary>
        public bool Fracture;
        public bool Splinted;

        public bool Infected => Infection > 0f;
        public bool Bleeds => !Tended && Injuries.BleedRate(Kind) > 0f;
        public float BleedPerSecond => Bleeds ? Severity * Injuries.BleedRate(Kind) : 0f;
    }

    /// <summary>
    /// SYS-MED-01: a body's wounds — where hits land, how they bleed and heal, what they cost the limbs, and tending.
    /// Pure state and maths (no Unity); <see cref="Vitals"/> owns one per player and feeds it time and hits.
    /// </summary>
    public sealed class Injuries
    {
        public const float RegenPerSecond = 0.05f;
        public const float HealPerMinute = 0.03f;
        public const float TendHealBonus = 4f;
        public const float LimbDisabledSeverity = 30f;
        public const float LimbPenalty = 0.4f;
        public const int MaxWounds = 12;
        public const float TendRoll = 0.1f;

        // §Infection
        public const float InfectionCheckMinutes = 60f;
        public const float InfectionChance = 0.25f;
        public const float InfectionStart = 0.05f;
        public const float InfectionGrowthPerMinute = 0.004f;
        public const float ImmunityPerMinute = 0.003f;
        public const float InfectionHpDrainPerSecond = 0.25f;

        // §Fractures
        public const float FractureSeverity = 15f;
        public const float FractureChance = 0.5f;
        public const float FractureLimbFactor = 2f;
        public const float UnsplintedHealFactor = 0.25f;

        // §Scars
        public const float ScarSeverity = 15f;
        public const float ScarTendQuality = 0.4f;
        public const float ScarMaxHealth = 2f;
        public const int MaxScars = 5;

        readonly List<BodyPart> _scars = new();
        public IReadOnlyList<BodyPart> Scars => _scars;

        static readonly (BodyPart Part, float Weight)[] PartWeights =
        {
            (BodyPart.Torso, 0.40f), (BodyPart.Head, 0.10f), (BodyPart.LeftArm, 0.125f), (BodyPart.RightArm, 0.125f),
            (BodyPart.LeftLeg, 0.125f), (BodyPart.RightLeg, 0.125f),
        };

        readonly List<Wound> _wounds = new();
        public IReadOnlyList<Wound> Wounds => _wounds;

        public static float BleedRate(WoundKind kind) => kind switch
        {
            WoundKind.Cut => 0.008f,
            WoundKind.Puncture => 0.010f,
            _ => 0f,
        };

        /// <summary>The wound a damage type leaves, or null for none (toxic).</summary>
        public static WoundKind? KindFor(string damageType) => damageType switch
        {
            Combat.DamageTypes.Slash => WoundKind.Cut,
            Combat.DamageTypes.Pierce => WoundKind.Puncture,
            Combat.DamageTypes.Blunt => WoundKind.Bruise,
            Combat.DamageTypes.Heat => WoundKind.Burn,
            _ => null,
        };

        /// <summary>Which part a hit lands on for a roll in [0, 1).</summary>
        public static BodyPart PartFor(float roll)
        {
            var acc = 0f;
            foreach (var (part, weight) in PartWeights)
            {
                acc += weight;
                if (roll < acc) return part;
            }
            return BodyPart.Torso;
        }

        public float TotalSeverity => _wounds.Sum(w => w.Severity);
        public float MaxHealth(float gaugeMax) => Math.Max(0f, gaugeMax - TotalSeverity - ScarMaxHealth * _scars.Count);
        public float BleedPerSecond => _wounds.Sum(w => w.BleedPerSecond);

        /// <summary>§Infection: health an infection takes each second, in proportion to how far it has gone.</summary>
        public float InfectionDrainPerSecond => _wounds.Sum(w => InfectionHpDrainPerSecond * w.Infection);
        public bool Bleeding => _wounds.Any(w => w.Bleeds);
        public bool AnyUntended => _wounds.Any(w => !w.Tended);

        /// <summary>A hit that got through: a wound of <paramref name="severity"/> on the part the roll picks.</summary>
        public Wound AddHit(string damageType, float severity, float partRoll) => AddHit(damageType, severity, PartFor(partRoll), 1f);

        /// <summary>A hit on a known part; <paramref name="fractureRoll"/> below <see cref="FractureChance"/> breaks a limb
        /// on a heavy blunt hit.</summary>
        public Wound AddHit(string damageType, float severity, BodyPart part, float fractureRoll)
        {
            if (severity <= 0f || KindFor(damageType) is not { } kind) return null;
            if (_wounds.Count >= MaxWounds)
            {
                var merge = _wounds.Where(w => w.Part == part && w.Kind == kind).OrderByDescending(w => w.Severity).FirstOrDefault()
                            ?? _wounds.OrderByDescending(w => w.Severity).First();
                merge.Severity += severity;
                merge.Tended = false;
                return merge;
            }
            var wound = new Wound { Part = part, Kind = kind, Severity = severity, Peak = severity };
            wound.Fracture = kind == WoundKind.Bruise && IsLimb(part) && severity >= FractureSeverity && fractureRoll < FractureChance;
            _wounds.Add(wound);
            return wound;
        }

        public static bool IsLimb(BodyPart part) => part is BodyPart.LeftArm or BodyPart.RightArm or BodyPart.LeftLeg or BodyPart.RightLeg;

        /// <summary>Wounds close over <paramref name="minutes"/> in-game minutes — faster when tended, slowly while a
        /// fracture is unsplinted, not at all while infected; an hour in, an open wound may turn infected
        /// (<paramref name="roll"/>); infections grow or fade; a bad wound that closed badly scars.</summary>
        public void Heal(float minutes, Func<float> roll = null)
        {
            foreach (var w in _wounds)
            {
                w.Age += minutes;
                if (!w.InfectionChecked && w.Age >= InfectionCheckMinutes && w.Kind != WoundKind.Bruise)
                {
                    w.InfectionChecked = true;
                    var chance = InfectionChance * (w.Tended ? 1f - w.Quality : 1f);
                    if ((roll?.Invoke() ?? 1f) < chance) w.Infection = InfectionStart;
                }
                if (w.Infected)
                {
                    var growth = InfectionGrowthPerMinute * (w.Tended ? 1f - w.Quality : 1f);
                    w.Infection = Math.Clamp(w.Infection + (growth - ImmunityPerMinute) * minutes, 0f, 1f);
                    if (w.Infection <= 0f) w.Infection = 0f;
                    continue; // an infected wound doesn't close
                }
                var rate = HealPerMinute * (w.Tended ? 1f + TendHealBonus * w.Quality : 1f);
                if (w.Fracture && !w.Splinted) rate *= UnsplintedHealFactor;
                w.Severity -= rate * minutes;
            }
            foreach (var closed in _wounds.Where(w => w.Severity <= 0f))
                if (closed.Peak >= ScarSeverity && (!closed.Tended || closed.Quality < ScarTendQuality) && _scars.Count < MaxScars)
                    _scars.Add(closed.Part);
            _wounds.RemoveAll(w => w.Severity <= 0f);
        }

        /// <summary>§Tending: the tender's skill scales the medicine — ×0.6 untrained, ×1.0 at 25, ×1.4 at 50.</summary>
        public static float TendQuality(float medicineQuality, int level) =>
            medicineQuality * (TendSkillFloor + TendSkillSpan * Math.Clamp(level, 0, 50) / 50f);

        public const float TendSkillFloor = 0.6f;
        public const float TendSkillSpan = 0.8f;

        /// <summary>Whether anything would take a tending (untended, infected, or an unsplinted fracture for a splint).</summary>
        public bool NeedsTending(bool splint) => _wounds.Any(w => !w.Tended || w.Infected || (splint && w.Fracture && !w.Splinted));

        /// <summary>Tends one wound: a splint goes to an unsplinted fracture; otherwise the wound bleeding most, then an
        /// infected one (re-tending it if this tends better), then the worst untended one. Null when nothing needs it.</summary>
        public Wound Tend(float quality, float roll, bool splint = false)
        {
            var q = Math.Clamp(quality + roll, 0f, 1f);
            Wound target = null;
            if (splint) target = _wounds.Where(w => w.Fracture && !w.Splinted).OrderByDescending(w => w.Severity).FirstOrDefault();
            target ??= _wounds.Where(w => !w.Tended).OrderByDescending(w => w.BleedPerSecond).ThenByDescending(w => w.Infected).ThenByDescending(w => w.Severity).FirstOrDefault();
            target ??= _wounds.Where(w => w.Infected && w.Quality < q).OrderByDescending(w => w.Infection).FirstOrDefault();
            if (target == null) return null;
            target.Tended = true;
            target.Quality = Math.Max(target.Quality, q);
            if (splint && target.Fracture) target.Splinted = true;
            return target;
        }

        public static float LimbFraction(IEnumerable<Wound> wounds, BodyPart part) =>
            Math.Min(1f, wounds.Where(w => w.Part == part).Sum(w => w.Severity * (w.Fracture && !w.Splinted ? FractureLimbFactor : 1f)) / LimbDisabledSeverity);

        public float LegMult => 1f - LimbPenalty * (LimbFraction(_wounds, BodyPart.LeftLeg) + LimbFraction(_wounds, BodyPart.RightLeg)) * 0.5f;
        public float ArmMult => 1f - LimbPenalty * (LimbFraction(_wounds, BodyPart.LeftArm) + LimbFraction(_wounds, BodyPart.RightArm)) * 0.5f;

        /// <summary>Death and respawn: a fresh body — wounds gone. Scars stay unless <paramref name="scars"/>.</summary>
        public void Clear(bool scars = true)
        {
            _wounds.Clear();
            if (scars) _scars.Clear();
        }

        public void Restore(Wound wound)
        {
            if (wound.Severity > 0f) _wounds.Add(wound);
        }

        public void RestoreScar(BodyPart part)
        {
            if (_scars.Count < MaxScars) _scars.Add(part);
        }

        /// <summary>§Armor by part: the body parts an equip slot's armor covers.</summary>
        public static BodyPart[] Covers(string slot) => slot switch
        {
            "head" => new[] { BodyPart.Head },
            "shirt" or "chest" => new[] { BodyPart.Torso, BodyPart.LeftArm, BodyPart.RightArm },
            "legs" or "feet" => new[] { BodyPart.LeftLeg, BodyPart.RightLeg },
            "back" or "belt" => new[] { BodyPart.Torso },
            _ => System.Array.Empty<BodyPart>(),
        };
    }
}
