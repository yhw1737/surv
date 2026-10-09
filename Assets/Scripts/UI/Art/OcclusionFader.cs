using System.Collections.Generic;
using Isle.Core.Util;
using Isle.Gameplay.Character;
using Isle.World.Island;
using Isle.World.Objects;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// In the tilted view a tree or a station standing in front of you can hide you. Anything standing nearer the camera
    /// than the local player whose drawing covers the player on screen fades to see-through, and back when you step
    /// out. Presentation only; it keeps whatever colour the game gave the sprite and only lowers its alpha.
    /// </summary>
    public sealed class OcclusionFader : MonoBehaviour
    {
        /// <summary>Alpha of a sprite hiding the player. [invented look]</summary>
        const float FadedAlpha = 0.35f;

        /// <summary>Fade rate, per second. [invented look]</summary>
        const float FadeSpeed = 7f;

        /// <summary>How far in front to look for sprites that could hide the player, in tiles (the tallest tree).</summary>
        const float SearchTiles = 6f;

        /// <summary>The player's drawn height and half-width, in tiles, for its on-screen box.</summary>
        const float PlayerHeightTiles = 1.9f;
        const float PlayerHalfWidthTiles = 0.35f;

        sealed class Fade
        {
            public float Amount;
            public Color Base;
            public Color Written;
            public bool Wanted;
        }

        readonly Dictionary<SpriteRenderer, Fade> _fades = new();
        readonly List<SpriteRenderer> _done = new();

        void LateUpdate()
        {
            var camera = Camera.main;
            PlayerInteraction player = null;
            foreach (var p in PlayerInteraction.All)
                if (p.IsOwner) player = p;

            foreach (var fade in _fades.Values) fade.Wanted = false;
            if (camera != null && player != null) MarkOccluders(camera, StickFigureView.PositionOf(player));

            var step = FadeSpeed * Time.deltaTime;
            _done.Clear();
            foreach (var (renderer, fade) in _fades)
            {
                if (renderer == null)
                {
                    _done.Add(renderer);
                    continue;
                }
                // Someone else recoloured it (a felled tree's ghost tint): that's the new base.
                if (renderer.color != fade.Written) fade.Base = renderer.color;
                fade.Amount = Mathf.MoveTowards(fade.Amount, fade.Wanted ? 1f : 0f, step);
                var colour = fade.Base;
                colour.a *= Mathf.Lerp(1f, FadedAlpha, fade.Amount);
                renderer.color = colour;
                fade.Written = colour;
                if (fade.Amount <= 0f && !fade.Wanted)
                {
                    renderer.color = fade.Base;
                    _done.Add(renderer);
                }
            }
            foreach (var renderer in _done) _fades.Remove(renderer);
        }

        void MarkOccluders(Camera camera, Vector2 feet)
        {
            var playerBox = ScreenBox(camera, feet, PlayerHalfWidthTiles, PlayerHeightTiles);
            var world = IslandWorld.Instance;
            if (world != null)
                foreach (var node in world.NodesNear(feet, SearchTiles))
                    if (node.View != null && node.Def.Gather != null) Consider(camera, node.View, node.Position, feet, playerBox);
            foreach (var instance in WorldObjectRegistry.All)
                if (instance != null && instance.TryGetComponent<SpriteRenderer>(out var sprite))
                    Consider(camera, sprite, instance.transform.position, feet, playerBox);
        }

        void Consider(Camera camera, SpriteRenderer renderer, Vector2 foot, Vector2 feet, Rect playerBox)
        {
            // Only what stands nearer the camera (south of the player) and isn't lying flat can hide them.
            if (!renderer.enabled || foot.y >= feet.y || feet.y - foot.y > SearchTiles || Mathf.Abs(foot.x - feet.x) > SearchTiles) return;
            if (Mathf.Abs(renderer.transform.rotation.eulerAngles.x) < 1f) return;
            if (!ScreenRect(camera, renderer.bounds).Overlaps(playerBox)) return;
            if (!_fades.TryGetValue(renderer, out var fade))
                _fades[renderer] = fade = new Fade { Base = renderer.color, Written = renderer.color };
            fade.Wanted = true;
        }

        static Rect ScreenBox(Camera camera, Vector2 feet, float halfWidth, float height)
        {
            var up = ViewTilt.Standing * Vector3.up;
            var a = camera.WorldToScreenPoint(new Vector3(feet.x - halfWidth, feet.y, 0f));
            var b = camera.WorldToScreenPoint(new Vector3(feet.x + halfWidth, feet.y, 0f) + up * height);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static Rect ScreenRect(Camera camera, Bounds bounds)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var p = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
