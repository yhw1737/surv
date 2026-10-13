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
    /// T-151 playtest round 2: the inventory driven by a real (virtual) mouse through the event system, the way a player
    /// drags — bag to backpack, onto the worn backpack, onto the ground panel, and from the ground list into a bag.
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
        public IEnumerator Mouse_DragsBetweenBagsGroundAndWornBag()
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
            var pack = Def("isle:straw_backpack");
            Assert.IsTrue(inventory.Slots.TryEquip("back", pack));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:berries"), new Isle.Core.Vec2Int(0, 0), false, 3));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:wood"), new Isle.Core.Vec2Int(1, 0), false, 2));
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:stone"), new Isle.Core.Vec2Int(2, 0), false, 2));

            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
            var screen = InventoryScreen.Instance;
            screen.Show();
            for (var i = 0; i < 10; i++) yield return null;
            var backpack = inventory.Slots.BagFor("back");

            // 1. Bag → backpack grid.
            yield return Drag(() => Icon(0, "isle:berries"), () => Centre(Grid(1)));
            yield return WaitUntil(() => backpack.Placements.Any(p => p.Item.Id.Value == "isle:berries"), 3f, "a drag from the bag to the backpack didn't move the berries");

            // 2. Bag → onto the worn backpack in its slot: into the backpack.
            for (var i = 0; i < 10; i++) yield return null;
            yield return Drag(() => Icon(0, "isle:wood"), () => Centre((RectTransform)Object.FindObjectsByType<EquipSlotView>(FindObjectsSortMode.None).First(s => s.SlotName == "back").transform));
            yield return WaitUntil(() => backpack.Placements.Any(p => p.Item.Id.Value == "isle:wood"), 3f, "dropping on the worn backpack didn't put the wood in it");

            // 3. Bag → the ground panel: dropped.
            var ground = (RectTransform)Object.FindObjectsByType<DropZone>(FindObjectsSortMode.None).First(z => z.name == "Ground").transform;
            for (var i = 0; i < 10; i++) yield return null;
            yield return Drag(() => Icon(0, "isle:stone"), () => Centre(ground));
            yield return WaitUntil(() => LootPiles.All.Count == 1, 3f, "dropping on the ground panel made no pile");
            Assert.IsFalse(inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:stone"), "stone still in the bag");

            // 4. The ground list shows it; drag it back into the bag.
            yield return WaitUntil(() => screen.GroundRowCount == 1, 2f, "the ground list doesn't show the dropped stone");
            yield return Drag(() => (RectTransform)Object.FindObjectsByType<GroundItemView>(FindObjectsSortMode.None).First().transform, () => Centre(Grid(0)));
            yield return WaitUntil(() => inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:stone"), 3f, "dragging from the ground list didn't pick the stone up");
            Assert.AreEqual(0, LootPiles.All.Count, "the emptied pile stayed");

            // 5. Right-click a ground item: pick up.
            LootPiles.Drop(player.transform.position, new[] { (Def("isle:fiber"), 2) });
            yield return WaitUntil(() => screen.GroundRowCount == 1, 2f, "the new pile isn't listed");
            var labels = screen.OpenGroundMenuFor(0);
            Assert.Contains(Lang.Get("@ui.ctx_pick_up"), labels);
            Assert.IsTrue(screen.PressMenu(Lang.Get("@ui.ctx_pick_up")));
            yield return WaitUntil(() => InventoryOps.Count(inventory.Containers(), NamespacedId.Parse("isle:fiber")) == 2, 3f, "pick up from the menu took nothing");
            // 6. A campfire: drag wood from the bag onto its fuel grid.
            var fire = StructureFactory.Build(DefRegistry.Get<WorldObjectDef>(NamespacedId.Parse("isle:campfire")), (Vector2)player.transform.position + Vector2.right * 0.6f);
            var burner = fire.GetComponent<Isle.Gameplay.World.CampfireInteraction>();
            inventory.Bag.Clear();
            Assert.IsTrue(inventory.Bag.TryPlace(Def("isle:wood"), new Isle.Core.Vec2Int(0, 0), false, 3));
            screen.OpenCampfire(burner);
            for (var i = 0; i < 10; i++) yield return null;
            yield return Drag(() => Icon(0, "isle:wood"), () => Centre(Object.FindObjectsByType<GridView>(FindObjectsSortMode.None).First(g => g.Campfire != null)));
            yield return WaitUntil(() => burner.Grid.Placements.Sum(p => p.Count) == 3, 3f, "dragging wood onto the fire's grid did nothing");
            Assert.IsFalse(inventory.Bag.Placements.Any(p => p.Item.Id.Value == "isle:wood"), "the wood stayed in the bag"); // the backpack still holds step 2's
            screen.Close();
            StructureFactory.Remove(fire);
        }

        static ItemDef Def(string id) => DefRegistry.Get<ItemDef>(NamespacedId.Parse(id));

        static GridView Grid(int container) =>
            Object.FindObjectsByType<GridView>(FindObjectsSortMode.None).First(g => g.ContainerIndex == container && g.Network != null);

        static RectTransform Icon(int container, string item) =>
            (RectTransform)Grid(container).GetComponentsInChildren<DragHandler>().First(d => d.name == $"Item_{item}").transform;

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
