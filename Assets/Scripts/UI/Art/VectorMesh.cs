using System.Collections.Generic;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// SYS-CHAR-02 §Rendering: collects vector primitives into one mesh, in painter's order. Every primitive is a quad
    /// (polygons a fan) whose UVs carry a distance value the <c>Isle/Vector</c> shader turns into an anti-aliased edge
    /// with screen-space derivatives — crisp at any zoom, nothing rasterised ahead of time.
    /// <para>UV layout: xy = position in the primitive's own [-1, 1] space, z = mode (0 stroke: edge at |y| = 1,
    /// 1 disk: edge at length(xy) = 1, 2 solid fill).</para>
    /// </summary>
    public sealed class VectorMesh
    {
        const float ModeStroke = 0f, ModeDisk = 1f, ModeSolid = 2f;

        readonly List<Vector3> _positions = new();
        readonly List<Color32> _colours = new();
        readonly List<Vector4> _uvs = new();
        readonly List<int> _triangles = new();

        /// <summary>Applied to every point as it's added — the figure uses it to mirror for facing and to tilt/roll.</summary>
        public System.Func<Vector2, Vector2> Transform { get; set; }

        public void Clear()
        {
            _positions.Clear();
            _colours.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        Vector3 Point(Vector2 p) => Transform != null ? Transform(p) : p;

        /// <summary>A straight band from a to b, flat-ended (pair with <see cref="Disk"/> for round caps).</summary>
        public void Stroke(Vector2 a, Vector2 b, float width, Color colour)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 1e-10f) dir = Vector2.right;
            var normal = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            Quad(a - normal, b - normal, b + normal, a + normal,
                new Vector4(0, -1, ModeStroke), new Vector4(0, -1, ModeStroke), new Vector4(0, 1, ModeStroke), new Vector4(0, 1, ModeStroke), colour);
        }

        /// <summary>A stroke with round caps — limbs, shafts, lines.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color colour)
        {
            Stroke(a, b, width, colour);
            Disk(a, width * 0.5f, colour);
            Disk(b, width * 0.5f, colour);
        }

        /// <summary>A polyline of round-capped segments (curves, bow limbs, fishing line).</summary>
        public void Polyline(IReadOnlyList<Vector2> points, float width, Color colour)
        {
            for (var i = 0; i + 1 < points.Count; i++) Stroke(points[i], points[i + 1], width, colour);
            for (var i = 0; i < points.Count; i++) Disk(points[i], width * 0.5f, colour);
        }

        public void Disk(Vector2 centre, float radius, Color colour) => Ellipse(centre, radius, radius, colour);

        public void Ellipse(Vector2 centre, float rx, float ry, Color colour)
        {
            Quad(centre + new Vector2(-rx, -ry), centre + new Vector2(rx, -ry), centre + new Vector2(rx, ry), centre + new Vector2(-rx, ry),
                new Vector4(-1, -1, ModeDisk), new Vector4(1, -1, ModeDisk), new Vector4(1, 1, ModeDisk), new Vector4(-1, 1, ModeDisk), colour);
        }

        /// <summary>A filled convex polygon, triangle fan, no edge softening — outline it with <see cref="Outline"/>.</summary>
        public void Polygon(IReadOnlyList<Vector2> points, Color colour)
        {
            var start = _positions.Count;
            Color32 c = colour;
            foreach (var p in points)
            {
                _positions.Add(Point(p));
                _colours.Add(c);
                _uvs.Add(new Vector4(0, 0, ModeSolid));
            }
            for (var i = 1; i + 1 < points.Count; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + i);
                _triangles.Add(start + i + 1);
            }
        }

        /// <summary>A closed outline around a polygon (anti-aliased, round corners).</summary>
        public void Outline(IReadOnlyList<Vector2> points, float width, Color colour)
        {
            for (var i = 0; i < points.Count; i++) Stroke(points[i], points[(i + 1) % points.Count], width, colour);
            for (var i = 0; i < points.Count; i++) Disk(points[i], width * 0.5f, colour);
        }

        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector4 ua, Vector4 ub, Vector4 uc, Vector4 ud, Color colour)
        {
            var start = _positions.Count;
            Color32 col = colour;
            _positions.Add(Point(a));
            _positions.Add(Point(b));
            _positions.Add(Point(c));
            _positions.Add(Point(d));
            for (var i = 0; i < 4; i++) _colours.Add(col);
            _uvs.Add(ua);
            _uvs.Add(ub);
            _uvs.Add(uc);
            _uvs.Add(ud);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
        }

        public int VertexCount => _positions.Count;

        public Color32 ColourAt(int vertex) => _colours[vertex];

        /// <summary>Blends every vertex from <paramref name="start"/> on toward <paramref name="tint"/> by
        /// <paramref name="amount"/> (0–1), keeping alpha — a body turning green, red or blue.</summary>
        public void TintFrom(int start, Color tint, float amount)
        {
            if (amount <= 0f) return;
            for (var i = start; i < _colours.Count; i++)
            {
                Color c = _colours[i];
                var blended = Color.Lerp(c, tint, amount);
                blended.a = c.a;
                _colours[i] = blended;
            }
        }

        /// <summary>Moves every vertex from <paramref name="start"/> on (shiver, hunch, pant).</summary>
        public void MoveFrom(int start, System.Func<Vector3, Vector3> move)
        {
            for (var i = start; i < _positions.Count; i++) _positions[i] = move(_positions[i]);
        }

        /// <summary>Writes the collected primitives into <paramref name="mesh"/>, replacing what was there.</summary>
        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_positions);
            mesh.SetColors(_colours);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0, calculateBounds: true);
        }
    }
}
