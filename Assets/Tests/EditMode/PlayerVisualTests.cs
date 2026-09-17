using Isle.Gameplay.Character;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>PROJECT_STATE.md §Decided without a spec, 2026-09-17 — T-001/T-002 retirement.</summary>
    public sealed class PlayerVisualTests
    {
        static Transform Spawn()
        {
            var go = new GameObject("player", typeof(PlayerVisual));
            go.GetComponent<PlayerVisual>().BuildVisual();
            return go.transform;
        }

        [Test]
        public void Awake_CreatesBodyAndTwoHands()
        {
            var root = Spawn();
            Assert.IsNotNull(root.Find("Body"));
            Assert.IsNotNull(root.Find("Hand_A"));
            Assert.IsNotNull(root.Find("Hand_B"));
        }

        [Test]
        public void Awake_EveryPartHasASprite()
        {
            var root = Spawn();
            foreach (var name in new[] { "Body", "Hand_A", "Hand_B" })
            {
                var renderer = root.Find(name).GetComponent<SpriteRenderer>();
                Assert.IsNotNull(renderer.sprite, $"{name} has no sprite");
            }
        }

        [Test]
        public void Awake_HandsAreMirroredEitherSideOfTheBody()
        {
            var root = Spawn();
            var body = root.Find("Body").localPosition;
            var handA = root.Find("Hand_A").localPosition;
            var handB = root.Find("Hand_B").localPosition;

            Assert.AreEqual(body, Vector3.zero);
            Assert.AreEqual(-handA.x, handB.x, 0.001f);
            Assert.AreNotEqual(0f, handA.x, "hands must be offset either side, not stacked on the body");
        }

        [Test]
        public void Awake_HandsDrawInFrontOfBody()
        {
            var root = Spawn();
            var bodyOrder = root.Find("Body").GetComponent<SpriteRenderer>().sortingOrder;
            var handOrder = root.Find("Hand_A").GetComponent<SpriteRenderer>().sortingOrder;
            Assert.Greater(handOrder, bodyOrder);
        }
    }
}
