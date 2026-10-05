using System.Collections;
using System.Linq;
using Isle.Gameplay.Hunting;
using Isle.Modding.Defs;
using Isle.World.Island;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Isle.Tests.PlayMode
{
    /// <summary>
    /// Runtime smoke test for the solo prototype's world layer: the island builds, nodes and creatures
    /// are placed from the real definitions, and nothing throws while it runs a few frames. Networking
    /// (ServerRpc paths) isn't started here — those need a live Play session.
    /// </summary>
    public sealed class PrototypeSmokeTests
    {
        GameObject _root;

        [SetUp]
        public void SetUp()
        {
            DefRegistry.Clear();
            var errors = DefinitionBootstrap.Load(System.IO.Path.Combine(Application.streamingAssetsPath, "definitions"));
            Assert.IsEmpty(errors, "definition load errors: " + string.Join("; ", errors.Select(e => e.Message)));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            DefRegistry.Clear();
        }

        [UnityTest]
        public IEnumerator Island_BuildsNodesAndCreatures_WithoutErrors()
        {
            _root = new GameObject("Prototype");
            _root.AddComponent<IslandWorld>();
            _root.AddComponent<CreatureDirector>();

            for (var frame = 0; frame < 5; frame++) yield return null;

            Assert.IsNotNull(IslandWorld.Instance);
            Assert.Greater(IslandWorld.Instance.Nodes.Count, 0, "no resource nodes placed");
            Assert.IsTrue(IslandWorld.Instance.Nodes.Any(n => n.IsHarvestable), "no harvestable node placed");
            Assert.IsTrue(IslandWorld.Instance.Nodes.Any(n => n.IsDrinkable), "no water source placed");
            Assert.IsNotNull(CreatureDirector.Instance);
            Assert.Greater(CreatureDirector.Instance.Creatures.Count, 0, "no creatures spawned");
        }
    }
}
