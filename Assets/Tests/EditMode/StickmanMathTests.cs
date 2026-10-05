using Isle.UI.Art;
using NUnit.Framework;
using UnityEngine;

namespace Isle.Tests.EditMode
{
    /// <summary>SYS-CHAR-02 §Verification 1–6.</summary>
    public sealed class StickmanMathTests
    {
        const float Tolerance = 1e-3f;

        [Test]
        public void TwoBoneIk_InReach_KeepsLengthsAndHitsTarget()
        {
            var root = new Vector2(0f, 1f);
            var target = new Vector2(0.3f, 0.5f);
            var joint = TwoBoneIk.Solve(root, target, 0.4f, 0.4f, bend: 1f);
            Assert.AreEqual(0.4f, Vector2.Distance(root, joint), Tolerance);
            Assert.AreEqual(0.4f, Vector2.Distance(joint, target), Tolerance);
        }

        [Test]
        public void TwoBoneIk_OutOfReach_StraightTowardTarget()
        {
            var root = Vector2.zero;
            var joint = TwoBoneIk.Solve(root, new Vector2(5f, 0f), 0.4f, 0.4f, bend: 1f);
            Assert.AreEqual(0.4f, joint.x, Tolerance);
            Assert.AreEqual(0f, joint.y, Tolerance);
        }

        [Test]
        public void TwoBoneIk_BendSign_ChoosesSide()
        {
            var a = TwoBoneIk.Solve(Vector2.zero, new Vector2(0f, -0.6f), 0.4f, 0.4f, bend: 1f);
            var b = TwoBoneIk.Solve(Vector2.zero, new Vector2(0f, -0.6f), 0.4f, 0.4f, bend: -1f);
            Assert.Greater(a.x * b.x, -1f);
            Assert.Less(a.x * b.x, 0f, "opposite bend signs should put the joint on opposite sides");
        }

        [Test]
        public void Spring_ManySteps_ConvergesWithoutOvershoot()
        {
            float value = 0f, velocity = 0f, max = 0f;
            for (var i = 0; i < 600; i++)
            {
                Spring.Damp(ref value, ref velocity, 1f, frequency: 8f, dt: 1f / 120f);
                max = Mathf.Max(max, value);
            }
            Assert.AreEqual(1f, value, Tolerance);
            Assert.LessOrEqual(max, 1f + Tolerance);
        }

        [Test]
        public void Spring_FrameRateIndependent()
        {
            float a = 0f, av = 0f, b = 0f, bv = 0f;
            for (var i = 0; i < 30; i++) Spring.Damp(ref a, ref av, 1f, 8f, 1f / 30f);
            for (var i = 0; i < 240; i++) Spring.Damp(ref b, ref bv, 1f, 8f, 1f / 240f);
            Assert.AreEqual(a, b, 0.01f);
        }

        [Test]
        public void TickInterpolation_HalfWay_IsMidpoint()
        {
            var p = TickInterpolation.Sample(new Vector2(0f, 0f), new Vector2(2f, 4f), previousTime: 1f, currentTime: 1.1f, now: 1.15f, tickInterval: 0.1f);
            Assert.AreEqual(1f, p.x, Tolerance);
            Assert.AreEqual(2f, p.y, Tolerance);
        }

        [Test]
        public void TickInterpolation_LongAfterTick_IsCurrent()
        {
            var p = TickInterpolation.Sample(Vector2.zero, Vector2.one, 1f, 1.1f, now: 5f, tickInterval: 0.1f);
            Assert.AreEqual(1f, p.x, Tolerance);
        }

        [Test]
        public void Gait_Advance_WrapsAndScalesWithStride()
        {
            Assert.AreEqual(0.25f, Gait.Advance(0f, distance: 0.25f, stride: 1f), Tolerance);
            Assert.AreEqual(0.1f, Gait.Advance(0.9f, distance: 0.2f, stride: 1f), Tolerance);
            Assert.AreEqual(0.5f, Gait.Advance(0f, distance: 0.5f, stride: 1f), Tolerance);
        }

        [Test]
        public void VectorMesh_QuadPerPrimitive()
        {
            var vm = new VectorMesh();
            vm.Stroke(Vector2.zero, Vector2.one, 0.1f, Color.black);
            vm.Disk(Vector2.zero, 0.3f, Color.white);
            var mesh = new Mesh();
            vm.Fill(mesh);
            Assert.AreEqual(8, mesh.vertexCount);
            Assert.AreEqual(12, mesh.triangles.Length);
        }

        [Test]
        public void VectorMesh_Polygon_FanTriangles()
        {
            var vm = new VectorMesh();
            vm.Polygon(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, Color.red);
            var mesh = new Mesh();
            vm.Fill(mesh);
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(6, mesh.triangles.Length);
        }
    }
}
