using Isle.Core.Util;
using UnityEngine;

namespace Isle.Gameplay.Character
{
    /// <summary>
    /// Interim placeholder shape for the player character (PROJECT_STATE.md §Decided without a
    /// spec, 2026-09-17) — retires T-001/T-002's jointed cutout rig entirely. Character/graphics
    /// design is undecided past this; a real rig replaces it in Phase 11
    /// (<c>docs/design/GDD.md</c> §Character presentation, <c>docs/ART_PIPELINE.md</c>).
    /// <para>
    /// Three plain circles, generated at runtime the same way every other undecided-art object in
    /// the game is (<see cref="PlaceholderVisuals"/>, T-017) rather than baked to disk like the
    /// retired rig was: one large body circle that also serves as the face, plus two small hand
    /// circles either side of it. In this top-down view "either side" reads as both left/right and
    /// front/back at once — there is no separate front-facing sprite.
    /// </para>
    /// <para>
    /// No bones, no sockets: nothing outside this class reads any of these transforms
    /// (Absolute Rule 7, presentation boundary). When a real design lands, this whole component is
    /// the only thing that needs replacing.
    /// </para>
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        // Unspecced (PROJECT_STATE.md §Decided without a spec) — picked to roughly fill one tile,
        // matching the retired rig's "character height ~1.5 tiles" without inventing a new number.
        const float BodyDiameter = 1f;
        const float HandDiameter = 0.35f;
        const float HandOffset = 0.5f;
        const float PixelsPerUnit = 32f; // ART_PIPELINE §Style: tile size.

        void Awake() => BuildVisual();

        // Split from Awake so EditMode tests can trigger it directly rather than depending on
        // Unity's edit-time Awake timing (see PlayerVisualTests). Public: the test assembly is
        // separate, so internal wouldn't be visible without an InternalsVisibleTo entry.
        public void BuildVisual()
        {
            // No tags to derive a colour from (Absolute Rule 4) — this isn't a definition, so it
            // falls back to PlaceholderVisuals' neutral grey, same as everything else undrawn.
            var color = PlaceholderVisuals.ColorForTags(null);

            SpawnCircle("Body", Vector2.zero, BodyDiameter, sortingOrder: 0, color);
            SpawnCircle("Hand_A", new Vector2(-HandOffset, 0f), HandDiameter, sortingOrder: 1, color);
            SpawnCircle("Hand_B", new Vector2(HandOffset, 0f), HandDiameter, sortingOrder: 1, color);
        }

        void SpawnCircle(string name, Vector2 localPosition, float diameter, int sortingOrder, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = localPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            var pixels = Mathf.RoundToInt(diameter * PixelsPerUnit);
            renderer.sprite = PlaceholderVisuals.AsSprite(PlaceholderVisuals.Circle(pixels, color), PixelsPerUnit);
            renderer.sortingOrder = sortingOrder;
        }
    }
}
