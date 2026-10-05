using System.Collections.Generic;
using Isle.Core.Ids;
using Isle.Data;
using Isle.Gameplay.Combat;
using Isle.Gameplay.Feedback;
using Isle.Gameplay.Hunting;
using Isle.Modding.Defs;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Presentation-only reactions to <see cref="GameFeed"/>: item toasts, floating damage numbers, creature HP
    /// bars and alert marks, and a red flash when the player is hurt. All timings and sizes are presentation
    /// values, not balance numbers.
    /// </summary>
    public sealed class FeedbackOverlay : MonoBehaviour
    {
        const float ToastSeconds = 2.5f;
        const int MaxToasts = 6;
        const float FloatSeconds = 0.9f;
        const float FloatRiseTiles = 0.8f;
        const float VignetteSeconds = 0.35f;
        const float HpBarSecondsAfterHit = 4f;

        readonly List<(string Text, float At)> _toasts = new();
        readonly List<(Vector2 Position, string Text, float At, Color Colour)> _floaters = new();
        float _hurtAt = float.NegativeInfinity;
        string _levelUp;
        float _levelUpAt = float.NegativeInfinity;
        const float LevelUpSeconds = 3f;
        (Vector2 At, float Reach, float Time) _swing = (Vector2.zero, 0f, float.NegativeInfinity);
        const float SwingSeconds = 0.18f;
        const int SwingDots = 16;
        GUIStyle _toastStyle;
        GUIStyle _floatStyle;
        GUIStyle _labelStyle;
        GUIStyle _bigStyle;

        void OnEnable()
        {
            GameFeed.ItemGained += OnItemGained;
            GameFeed.ItemDropped += OnItemDropped;
            GameFeed.CreatureHit += OnCreatureHit;
            GameFeed.PlayerHit += OnPlayerHit;
            GameFeed.PlayerSwing += OnPlayerSwing;
            GameFeed.Notice += OnNotice;
            GameFeed.XpGained += OnXpGained;
        }

        void OnDisable()
        {
            GameFeed.ItemGained -= OnItemGained;
            GameFeed.ItemDropped -= OnItemDropped;
            GameFeed.CreatureHit -= OnCreatureHit;
            GameFeed.PlayerHit -= OnPlayerHit;
            GameFeed.PlayerSwing -= OnPlayerSwing;
            GameFeed.Notice -= OnNotice;
            GameFeed.XpGained -= OnXpGained;
        }

        void OnItemGained(NamespacedId item, int count) => Toast($"+{count} {ItemName(item)}");
        void OnItemDropped(NamespacedId item, int count, Vector2 at) => Toast($"{Lang.Get("@ui.dropped")}: {count} {ItemName(item)}");
        void OnNotice(string key) => Toast(Lang.Get(key));

        void OnXpGained(NamespacedId skill, float amount, int newLevel)
        {
            var name = DefRegistry.TryGet<SkillDef>(skill, out var def) ? Lang.Get(def.Name) : skill.Value;
            Toast(string.Format(Lang.Get("@ui.xp_gain"), Mathf.RoundToInt(amount), name));
            if (newLevel > 0)
            {
                _levelUp = string.Format(Lang.Get("@ui.level_up"), name, newLevel);
                _levelUpAt = Time.time;
            }
        }
        void OnCreatureHit(Vector2 at, float damage) => _floaters.Add((at, Mathf.RoundToInt(damage).ToString(), Time.time, Color.yellow));
        void OnPlayerHit(float damage) => _hurtAt = Time.time;
        void OnPlayerSwing(Vector2 at, float reach) => _swing = (at, reach, Time.time);

        static string ItemName(NamespacedId item) =>
            DefRegistry.TryGet<ItemDef>(item, out var def) ? Lang.Get(def.Name) : item.Value;

        void Toast(string text)
        {
            _toasts.Add((text, Time.time));
            if (_toasts.Count > MaxToasts) _toasts.RemoveAt(0);
        }

        void OnGUI()
        {
            // Draw-only overlay: nothing here handles input, so skip the Layout/mouse events that would repeat the work.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            var camera = Camera.main;

            DrawVignette();
            if (camera != null)
            {
                DrawLabels(camera);
                DrawPlayerActions(camera);
                DrawMarkers(camera);
                DrawSwing(camera);
                DrawCreatureMarks(camera);
                DrawFloaters(camera);
            }
            DrawToasts();
            DrawLevelUp();
        }

        /// <summary>A level-up gets the middle of the screen for a moment, not just a toast line.</summary>
        void DrawLevelUp()
        {
            var age = Time.time - _levelUpAt;
            if (age > LevelUpSeconds || _levelUp == null) return;
            GUI.color = new Color(1f, 0.9f, 0.4f, Mathf.Clamp01((LevelUpSeconds - age) / 0.6f));
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.3f, 500f, 40f), _levelUp, _floatStyle);
            GUI.color = Color.white;
        }

        void DrawVignette()
        {
            var age = Time.time - _hurtAt;
            if (age > VignetteSeconds) return;
            GUI.color = new Color(1f, 0f, 0f, 0.35f * (1f - age / VignetteSeconds));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawCreatureMarks(Camera camera)
        {
            var director = CreatureDirector.Instance;
            if (director == null) return;

            foreach (var creature in director.Creatures)
            {
                var screen = ToGui(camera, creature.Position + Vector2.up * (creature.Radius + 0.25f));
                if (screen == null) continue;
                var point = screen.Value;

                if (creature.State == CreatureState.Alert)
                    GUI.Label(new Rect(point.x - 10f, point.y - 28f, 20f, 24f), "!", _floatStyle);

                if (Time.time - creature.LastHitAt > HpBarSecondsAfterHit || creature.MaxHealth <= 0f) continue;
                var fraction = Mathf.Clamp01(creature.Health / creature.MaxHealth);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(point.x - 20f, point.y, 40f, 6f), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.red, Color.green, fraction);
                GUI.DrawTexture(new Rect(point.x - 19f, point.y + 1f, 38f * fraction, 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        /// <summary>The local player's timed actions: a harvest progress bar over their head, and the fishing bobber
        /// with a "!" when something bites.</summary>
        void DrawPlayerActions(Camera camera)
        {
            foreach (var player in Isle.Gameplay.Character.PlayerInteraction.All)
            {
                if (!player.IsOwner) continue;

                if (player.Gathering != null && ToGui(camera, (Vector2)player.transform.position + Vector2.up * 0.9f) is { } head)
                {
                    var bar = new Rect(head.x - 30f, head.y, 60f, 8f);
                    GUI.color = Color.black;
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);
                    GUI.color = new Color(0.95f, 0.85f, 0.3f);
                    GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * player.GatherProgress, bar.height - 2f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                if (player.Cast == null) continue;
                var bob = Mathf.Sin(Time.time * 3f) * 0.05f;
                if (ToGui(camera, player.CastPoint + Vector2.up * bob) is not { } point) continue;
                var biting = player.Cast.State == Isle.Gameplay.Fishing.CastState.Bite;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(point.x - 6f, point.y - 6f, 12f, 12f), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 0.15f, 0.15f);
                GUI.DrawTexture(new Rect(point.x - 6f, point.y - 6f, 12f, 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (biting) GUI.Label(new Rect(point.x - 20f, point.y - 46f, 40f, 40f), "!", _bigStyle);
            }
        }

        /// <summary>SYS-MAP-01: map markers in the world — a pin with the distance when on screen, an arrow at the
        /// screen edge when not.</summary>
        void DrawMarkers(Camera camera)
        {
            var state = MapState.Instance;
            if (state == null) return;
            Vector2? me = null;
            foreach (var player in Isle.Gameplay.Character.PlayerInteraction.All)
                if (player.IsOwner) me = player.transform.position;
            if (me == null) return;

            const float inset = 28f;
            foreach (var marker in state.Markers.All)
            {
                var colour = Minimap.MarkerColours[marker.Colour % Minimap.MarkerColours.Length];
                var distance = Mathf.RoundToInt(Vector2.Distance(me.Value, marker.Position));
                if (ToGui(camera, marker.Position) is not { } p) continue;
                var onScreen = p.x > 0 && p.y > 0 && p.x < Screen.width && p.y < Screen.height;
                if (!onScreen)
                {
                    // Clamp along the line from the screen centre so the arrow points the right way.
                    var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    var dir = (p - centre).normalized;
                    var scale = Mathf.Min((Screen.width / 2f - inset) / Mathf.Max(Mathf.Abs(dir.x), 1e-4f), (Screen.height / 2f - inset) / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));
                    p = centre + dir * scale;
                }
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(p.x - 7f, p.y - 7f, 14f, 14f), Texture2D.whiteTexture);
                GUI.color = colour;
                GUI.DrawTexture(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(p.x - 40f, p.y + 6f, 80f, 18f), onScreen ? $"{distance}m" : $"➜ {distance}m", _labelStyle);
            }
        }

        const float CreatureLabelTiles = 7f;
        const float ThingLabelTiles = 3f;

        /// <summary>Names over what's near the player — the placeholder shapes are recognisable, the label makes sure.</summary>
        void DrawLabels(Camera camera)
        {
            Vector2? me = null;
            foreach (var player in Isle.Gameplay.Character.PlayerInteraction.All)
                if (player.IsOwner) me = player.transform.position;
            if (me == null) return;

            if (CreatureDirector.Instance != null)
                foreach (var creature in CreatureDirector.Instance.Creatures)
                    if (Vector2.Distance(me.Value, creature.Position) <= CreatureLabelTiles)
                        Label(camera, creature.Position + Vector2.down * (creature.Radius + 0.15f), Lang.Get(creature.Def.Name));

            var world = Isle.World.Island.IslandWorld.Instance;
            if (world != null)
                foreach (var node in world.NodesNear(me.Value, ThingLabelTiles))
                    if (Vector2.Distance(me.Value, node.Position) <= ThingLabelTiles)
                        Label(camera, node.Position + Vector2.down * 0.55f, Lang.Get(node.Def.Name));

            foreach (var instance in Isle.World.Objects.WorldObjectRegistry.All)
                if (instance.Def != null && Vector2.Distance(me.Value, instance.transform.position) <= ThingLabelTiles)
                    Label(camera, (Vector2)instance.transform.position + Vector2.down * 0.6f, Lang.Get(instance.Def.Name));
        }

        void Label(Camera camera, Vector2 world, string text)
        {
            var screen = ToGui(camera, world);
            if (screen == null) return;
            var rect = new Rect(screen.Value.x - 70f, screen.Value.y - 9f, 140f, 18f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, _labelStyle);
            GUI.color = Color.white;
            GUI.Label(rect, text, _labelStyle);
        }

        /// <summary>A ring of dots at the weapon's reach, fading out — shows the swing even on a miss.</summary>
        void DrawSwing(Camera camera)
        {
            var age = Time.time - _swing.Time;
            if (age > SwingSeconds) return;
            GUI.color = new Color(1f, 1f, 1f, 0.8f * (1f - age / SwingSeconds));
            for (var i = 0; i < SwingDots; i++)
            {
                var angle = i * Mathf.PI * 2f / SwingDots;
                var screen = ToGui(camera, _swing.At + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _swing.Reach);
                if (screen != null) GUI.DrawTexture(new Rect(screen.Value.x - 3f, screen.Value.y - 3f, 6f, 6f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        void DrawFloaters(Camera camera)
        {
            for (var i = _floaters.Count - 1; i >= 0; i--)
            {
                var (position, text, at, colour) = _floaters[i];
                var age = Time.time - at;
                if (age > FloatSeconds)
                {
                    _floaters.RemoveAt(i);
                    continue;
                }
                var screen = ToGui(camera, position + Vector2.up * (0.8f + FloatRiseTiles * age / FloatSeconds));
                if (screen == null) continue;
                GUI.color = new Color(colour.r, colour.g, colour.b, 1f - age / FloatSeconds);
                GUI.Label(new Rect(screen.Value.x - 30f, screen.Value.y - 12f, 60f, 24f), text, _floatStyle);
            }
            GUI.color = Color.white;
        }

        void DrawToasts()
        {
            _toasts.RemoveAll(t => Time.time - t.At > ToastSeconds);
            var y = Screen.height - 130f;
            for (var i = _toasts.Count - 1; i >= 0; i--)
            {
                var alpha = Mathf.Clamp01((ToastSeconds - (Time.time - _toasts[i].At)) / 0.5f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(Screen.width * 0.5f - 200f, y, 400f, 22f), _toasts[i].Text, _toastStyle);
                y -= 22f;
            }
            GUI.color = Color.white;
        }

        /// <summary>World position to IMGUI coordinates (y flipped), or null when behind the camera.</summary>
        static Vector2? ToGui(Camera camera, Vector2 world)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z < 0f) return null;
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        void EnsureStyles()
        {
            if (_toastStyle != null) return;
            _toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _floatStyle = new GUIStyle(_toastStyle) { fontSize = 18, fontStyle = FontStyle.Bold };
            _labelStyle = new GUIStyle(_toastStyle) { fontSize = 12 };
            _bigStyle = new GUIStyle(_toastStyle) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
        }
    }
}
