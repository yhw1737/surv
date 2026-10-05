using Isle.UI.Prototype;
using NUnit.Framework;

namespace Isle.Tests.EditMode
{
    /// <summary>T-150 prototype: a save survives the trip to text and back with nothing lost.</summary>
    public sealed class SaveDataTests
    {
        [Test]
        public void RoundTrip_AllSections_Preserved()
        {
            var save = new SaveData
            {
                Seed = 4242,
                TotalMinutes = 3000,
                Health = 80f, Hunger = 50f, Thirst = 40f, Stamina = 30f, Temperature = 36.5f, HypothermiaSeverity = 0.2f,
                PlayerX = 3.5f, PlayerY = -2f, ShelterX = 2f, ShelterY = -1f,
            };
            var pack = new SavedEquip { Slot = "back", Item = "isle:straw_backpack" };
            pack.Contents.Add(new SavedStack { Item = "isle:berries", Count = 6 });
            save.Equipped.Add(new SavedEquip { Slot = "main_hand", Item = "isle:stone_spear" });
            save.Equipped.Add(pack);
            save.Skills.Add(new SavedSkill { Id = "isle:cooking", Xp = 1522.5 });
            save.Bag.Add(new SavedStack { Item = "isle:wood", X = 1, Y = 0, Rotated = false, Count = 4 });
            save.Nodes.Add(new SavedNode { X = 10, Y = 20, UsesLeft = 0, RespawnInSeconds = 12.5f });
            save.Fires.Add(new SavedFire { X = 2f, Y = 0f, Lit = true });
            var pile = new SavedPile { X = 1f, Y = 1f };
            pile.Items.Add(new SavedStack { Item = "isle:stone", Count = 2 });
            save.Piles.Add(pile);

            var back = SaveData.FromJson(save.ToJson());

            Assert.AreEqual(4242, back.Seed);
            Assert.AreEqual(3000, back.TotalMinutes);
            Assert.AreEqual(0.2f, back.HypothermiaSeverity, 1e-5f);
            Assert.AreEqual("isle:stone_spear", back.Equipped[0].Item);
            Assert.AreEqual(6, back.Equipped[1].Contents[0].Count);
            Assert.AreEqual(1522.5, back.Skills[0].Xp, 1e-6);
            Assert.AreEqual(4, back.Bag[0].Count);
            Assert.AreEqual(12.5f, back.Nodes[0].RespawnInSeconds, 1e-5f);
            Assert.IsTrue(back.Fires[0].Lit);
            Assert.AreEqual("isle:stone", back.Piles[0].Items[0].Item);
        }

        [Test]
        public void FromJson_Garbage_ReturnsNull()
        {
            Assert.IsNull(SaveData.FromJson("not json"));
            Assert.IsNull(SaveData.FromJson(string.Empty));
        }

        [Test]
        public void FromJson_OlderVersion_ReturnsNull()
        {
            var save = new SaveData { Version = SaveData.MinReadableVersion - 1 };
            Assert.IsNull(SaveData.FromJson(save.ToJson()));
        }

        [Test]
        public void FromJson_PreviousVersionWithoutSkills_LoadsWithNoSkills()
        {
            var json = new SaveData { Version = 3, Seed = 7 }.ToJson().Replace("\"Skills\":[],", "");
            var back = SaveData.FromJson(json);
            Assert.IsNotNull(back);
            Assert.AreEqual(7, back.Seed);
            Assert.IsNotNull(back.Skills);
        }
    }
}
