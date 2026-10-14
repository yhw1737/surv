using System.Collections;
using System.Linq;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Building;
using Isle.Gameplay.Character;
using Isle.Gameplay.Inventory;
using Isle.Modding.Defs;
using Isle.UI.Inventory;
using Isle.UI.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// T-305 SYS-INV-02: the list inventory driven by a real (virtual) mouse through the event system, the way a player
    /// drags — a row onto a tab, Shift+drag one, onto the ground list and back, off the panels, onto a campfire.
    /// Request-level tests passed while dragging failed, so this goes through the same input path a hand does.
    /// </summary>
    public sealed class InventoryDragTests
    {
        static readonly string ScratchSave = System.IO.Path.Combine(Application.temporaryCachePath, "isle_drag_save.json");
        Mouse _mouse;
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [SetUp]
        public void SetUp()
        {
            GameSession.SkipMenu = true;
            SaveGame.FilePathOverride = ScratchSave;
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            DefRegistry.Clear();
            Assert.IsEmpty(DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions")));
            // A batch run has no focused window; let the virtual mouse through anyway.
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [TearDown]
        public void TearDown()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            foreach (var save in Object.FindObjectsByType<SaveGame>(FindObjectsSortMode.None)) Object.DestroyImmediate(save);
            if (System.IO.File.Exists(ScratchSave)) System.IO.File.Delete(ScratchSave);
            SaveGame.FilePathOverride = null;
            GameSession.SkipMenu = false;
            LootPiles.Clear();
            StructureFactory.Clear();
            DefRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator Mouse_DragsRowsBetweenListsGroundAndFire()
        {
            Isle.Networking.IsleNetworkManager.Find()?.StopHost();
            yield return WaitUntil(() => !FishNet.InstanceFinder.IsServerStarted && !FishNet.InstanceFinder.IsClientStarted, 5f, "the previous host never stopped");
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return WaitUntil(() => PlayerInteraction.Local != null, 15f, "no player");
            if (Object.FindFirstObjectByType<MainMenu>() != null) { GameSession.StartNew(99); yield return WaitUntil(() => PlayerInteraction.Local != null, 15f, "no player after start"); }
            for (var i = 0; i < 60; i++) yield return null;

            var player = PlayerInteraction.Local;
            var inventory = player.GetComponent<InventoryNetwork>();
            inventory.Bag.Clear();
            inventory.Slots.Clear();
            LootPiles.Clear();
            Assert.IsTrue(inventory.Slots.TryEquip("back", Def("isle:straw_backpack")));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:berries"), new Isle.Core.Vec2Int(-1, 0), false, 3));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:wood"), new Isle.Core.Vec2Int(-1, 0), false, 5));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:stone"), new Isle.Core.Vec2Int(-1, 0), false, 2));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:fiber"), new Isle.Core.Vec2Int(-1, 0), false, 2));

            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            var screen = InventoryScreen.Instance;
            screen.Show();
            screen.SelectLeft(0);
            screen.SelectRight(ListSource.Ground);
            for (var i = 0; i < 10; i++) yield return null;
            var backpack = inventory.Slots.BagFor("back");

            // T-307: food that spoils shows a freshness bar; a stone doesn't.
            Assert.IsNotNull(Row(screen.LeftList, "isle:berries").Find("Freshness"), "no freshness bar on berries");
            Assert.IsNull(Row(screen.LeftList, "isle:stone").Find("Freshness"), "a stone has a freshness bar");

            // 1. SYS-INV-02 #4: a pockets row onto the backpack's tab moves it.
            yield return Drag(() => Row(screen.LeftList, "isle:berries"), () => Centre(Tab(ListSource.Container, 1)));
            yield return WaitUntil(() => backpack.Placements.Any(p => p.Item.Id.Value == "isle:berries"), 3f, "a row dropped on the backpack tab didn't move");
            Assert.IsFalse(inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:berries"), "the berries stayed in the pockets");

            // 2. SYS-INV-02 #5: Shift+drag a stack of 5 moves one.
            yield return Shift(true);
            yield return Drag(() => Row(screen.LeftList, "isle:wood"), () => Centre(Tab(ListSource.Container, 1)));
            yield return Shift(false);
            yield return WaitUntil(() => backpack.Placements.Where(p => p.Item.Id.Value == "isle:wood").Sum(p => p.Count) == 1, 3f, "Shift+drag didn't move exactly one");
            Assert.AreEqual(4, inventory.Bag.Placements.Where(p => p.Item.Id.Value == "isle:wood").Sum(p => p.Count), "four should stay");

            // 3. A row onto the ground list: dropped.
            yield return Drag(() => Row(screen.LeftList, "isle:stone"), () => Centre(screen.RightList));
            yield return WaitUntil(() => LootPiles.All.Count == 1, 3f, "dropping on the ground list made no pile");
            Assert.IsFalse(inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:stone"), "stone still in the pockets");

            // 4. The ground row back onto the pockets list: picked up.
            yield return WaitUntil(() => screen.GroundRowCount == 1, 2f, "the ground list doesn't show the dropped stone");
            yield return Drag(() => Row(screen.RightList, "isle:stone"), () => Centre(screen.LeftList));
            yield return WaitUntil(() => inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:stone"), 3f, "dragging from the ground list didn't pick the stone up");
            Assert.AreEqual(0, LootPiles.All.Count, "the emptied pile stayed");

            // 5. Off both panels: dropped at your feet.
            yield return Drag(() => Row(screen.LeftList, "isle:fiber"), () => new Vector2(6f, 6f));
            yield return WaitUntil(() => LootPiles.All.Count == 1, 3f, "letting go outside the panels dropped nothing");

            // 6. Right-click a ground row: pick up.
            yield return WaitUntil(() => screen.GroundRowCount == 1, 2f, "the fiber isn't listed");
            var labels = screen.OpenRightMenuFor(0);
            Assert.Contains(Lang.Get("@ui.ctx_pick_up"), labels);
            Assert.IsTrue(screen.PressMenu(Lang.Get("@ui.ctx_pick_up")));
            yield return WaitUntil(() => InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:fiber")) == 2, 3f, "pick up from the menu took nothing");

            // 7. A campfire: drag wood from the pockets onto its fuel list.
            var fire = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:campfire")), (Vector2)player.transform.position + Vector2.right * 0.6f);
            var burner = fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>();
            screen.OpenCampfire(burner);
            yield return WaitUntil(() => screen.RightSelected == ListSource.Campfire && screen.RightList != null && screen.RightList.Source == ListSource.Campfire, 2f, "the campfire tab didn't open");
            yield return Drag(() => Row(screen.LeftList, "isle:wood"), () => Centre(screen.RightList));
            yield return WaitUntil(() => burner.Grid.Placements.Sum(p => p.Count) == 4, 3f, "dragging wood onto the fire's list did nothing");
            Assert.IsFalse(inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:wood"), "the wood stayed in the pockets");
            screen.Close();
            StructureFactory.Remove(fire);
        }

        Keyboard _keyboard;

        IEnumerator Shift(bool shift)
        {
            InputSystem.QueueStateEvent(_keyboard, shift ? new KeyboardState(UnityEngine.InputSystem.Key.LeftShift) : new KeyboardState());
            for (var i = 0; i < 3; i++) yield return null;
        }

        static RectTransform Row(ItemListView list, string item) =>
            (RectTransform)list.GetComponentsInChildren<ItemRow>().First(r => r.Entry.Item.Id.Value == item).transform;

        static ListTab Tab(ListSource source, int index) =>
            Object.FindObjectsByType<ListTab>(FindObjectsSortMode.None).First(t => t.Source == source && t.ContainerIndex == index);

        static ItemDef Def(string id) => DefRegistry.Get<ItemDef>(NamespacedId.Parse(id));

        static Vector2 Centre(Component c)
        {
            var r = (RectTransform)c.transform;
            return RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center));
        }

        /// <param name="from">Found after the wait, not before — the last move's redraw replaces the icons.</param>
        IEnumerator Drag(System.Func<RectTransform> from, System.Func<Vector2> to)
        {
            // The last move's answer (and its redraw) lands first, so the icon pressed is the one that stays.
            var network = PlayerInteraction.Local.GetComponent<InventoryNetwork>();
            yield return WaitUntil(() => !network.RequestPending, 3f, "a request never got its answer");
            for (var i = 0; i < 10; i++) yield return null;
            var start = Centre(from());
            var end = to();
            yield return Mouse(start, false);
            yield return Mouse(start, true);
            for (var s = 1; s <= 10; s++) yield return Mouse(Vector2.Lerp(start, end, s / 10f), true);
            yield return Mouse(end, false);
        }

        IEnumerator Mouse(Vector2 at, bool down)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = at, buttons = (ushort)(down ? 1 : 0) });
            for (var i = 0; i < 3; i++) yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string failure)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail(failure);
                yield return null;
            }
        }
    }
}
