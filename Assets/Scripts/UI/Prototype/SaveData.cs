using System;
using System.Collections.Generic;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// T-150 prototype save: everything needed to resume one solo island. Plain serializable fields, written
    /// with Unity's <see cref="JsonUtility"/> so the UI assembly needs no JSON library. Creatures, weather and
    /// the season temperature roll are not saved — they re-roll on load (PROJECT_STATE.md §Decided without a spec).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bump when a field changes meaning; an older file is ignored rather than misread.</summary>
        public const int CurrentVersion = 7;

        /// <summary>Oldest version still readable. v3 only lacks <see cref="Skills"/>, which then start from zero.</summary>
        public const int MinReadableVersion = 3;

        public int Version = CurrentVersion;
        public int Seed;
        public long TotalMinutes;

        public float Health, Hunger, Thirst, Stamina, Temperature, HypothermiaSeverity;
        public float PlayerX, PlayerY, ShelterX, ShelterY;

        /// <summary>Everything worn or wielded, one entry per occupied slot.</summary>
        public List<SavedEquip> Equipped = new();

        public List<SavedStack> Bag = new();
        public List<SavedNode> Nodes = new();
        public List<SavedFire> Fires = new();
        public List<SavedPile> Piles = new();
        public List<SavedStructure> Structures = new();
        public List<SavedSkill> Skills = new();

        /// <summary>SYS-MAP-01: explored fog cells, bit-packed base64. Empty = nothing explored.</summary>
        public string Explored = string.Empty;
        public List<SavedMarker> Markers = new();

        /// <summary>SYS-START-01: who this is — name and trait ids (skills are saved with the rest).</summary>
        public string SurvivorName;
        public List<string> Traits = new();

        /// <summary>T-150 (version 6): timed buffs with their expiry (world minutes, 0 = never), wetness, food freshness
        /// per carried container, satiety memory, and carcasses lying on the ground. Older saves load with none.</summary>
        public List<SavedBuff> Buffs = new();
        public float Wet;
        public List<SavedSpoil> Spoilage = new();
        public List<SavedSatiety> Satiety = new();
        public List<SavedCarcass> Carcasses = new();

        /// <summary>Version 7: what each dungeon remembers — its boss fallen, keys taken, gates opened or burned, the
        /// tide cache, veins mined (SYS-DUNG-01 §I/O).</summary>
        public List<Isle.Gameplay.Dungeons.DungeonDirector.SiteState> Dungeons = new();

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Null when the text isn't a save of the current version.</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var save = JsonUtility.FromJson<SaveData>(json);
                if (save == null || save.Version < MinReadableVersion || save.Version > CurrentVersion) return null;
                save.Skills ??= new List<SavedSkill>();
                save.Markers ??= new List<SavedMarker>();
                save.Traits ??= new List<string>();
                save.Explored ??= string.Empty;
                save.Buffs ??= new List<SavedBuff>();
                save.Spoilage ??= new List<SavedSpoil>();
                save.Satiety ??= new List<SavedSatiety>();
                save.Carcasses ??= new List<SavedCarcass>();
                save.Dungeons ??= new List<Isle.Gameplay.Dungeons.DungeonDirector.SiteState>();
                return save;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    [Serializable]
    public sealed class SavedMarker
    {
        public float X, Y;
        public int Colour;
    }

    [Serializable]
    public sealed class SavedSkill
    {
        public string Id;
        public double Xp;
    }

    /// <summary>One equip slot: the item in it, and — for a bag — what the bag holds.</summary>
    [Serializable]
    public sealed class SavedEquip
    {
        public string Slot;
        public string Item;

        /// <summary>SYS-CRAFT-02 wear; <see cref="WearMax"/> 0 = none recorded (brand new).</summary>
        public int Wear, WearMax;

        /// <summary>SYS-CRAFT-01 quality tier + 1; 0 = none (not crafted, or an old save).</summary>
        public int Quality;
        public List<SavedStack> Contents = new();
    }

    [Serializable]
    public sealed class SavedStack
    {
        public string Item;
        public int X, Y, Count;
        public bool Rotated;

        /// <summary>SYS-CRAFT-02 wear; <see cref="WearMax"/> 0 = none recorded (brand new).</summary>
        public int Wear, WearMax;

        /// <summary>SYS-CRAFT-01 quality tier + 1; 0 = none (not crafted, or an old save).</summary>
        public int Quality;

        /// <summary>SYS-HUNT-01: a carried carcass — which creature, and its body. Empty for anything else.</summary>
        public string CarcassOf;
        public float CarcassKg, CarcassCondition, CarcassKill, CarcassSpoil;

        public SavedStack WithCarcass(Isle.Gameplay.Inventory.ItemWear wear)
        {
            if (wear?.Carcass is not { } body) return this;
            CarcassOf = body.Def.Id.Value;
            CarcassKg = body.WeightKg;
            CarcassCondition = body.Condition;
            CarcassKill = body.KillFactor;
            CarcassSpoil = body.Spoilage;
            return this;
        }
    }

    /// <summary>Only nodes that differ from fresh are saved: a depleted node and how long until it's back.</summary>
    [Serializable]
    public sealed class SavedNode
    {
        public int X, Y, UsesLeft;
        public float RespawnInSeconds;
    }

    [Serializable]
    public sealed class SavedFire
    {
        public float X, Y;
        public bool Lit;
    }

    /// <summary>A player-built structure: what it is, where, and what it holds.</summary>
    [Serializable]
    public sealed class SavedStructure
    {
        public string Def;
        public float X, Y, Water;
        public string Crop;
        public long PlantedAt;
        public bool Lit;
        public List<SavedStack> Contents = new();
    }

    [Serializable]
    public sealed class SavedPile
    {
        public float X, Y;
        public string Shape;
        public List<SavedStack> Items = new();
    }

    [System.Serializable]
    public sealed class SavedBuff
    {
        public string Id;
        public long ExpiresAt;
    }

    /// <summary>Freshness of one item type in one carried container (index into the player's containers).</summary>
    [System.Serializable]
    public sealed class SavedSpoil
    {
        public int Container;
        public string Item;
        public float Value;
    }

    [System.Serializable]
    public sealed class SavedSatiety
    {
        public string Signature;
        public int Count;
        public long Since;
    }

    [System.Serializable]
    public sealed class SavedCarcass
    {
        public string Creature;
        public float X, Y, Kg, Condition, Kill, Spoil;
    }
}
