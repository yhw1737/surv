using Isle.Data;
using Isle.Gameplay.Character;
using Isle.Gameplay.Feedback;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>T-306: the shots of the shipwreck intro, in order.</summary>
    public enum IntroShot { Sailing, Storm, Sinking, Dark, Waking, Done }

    /// <summary>
    /// T-306: when each cutscene shot starts and how things fade — pure, so the timing is testable. Presentation values
    /// only ([proposed], PROJECT_STATE §Decided without a spec).
    /// </summary>
    public static class CutsceneTimeline
    {
        // Title card (game launch).
        public const float TitleFadeIn = 1.2f;
        public const float TitleHold = 2.3f;
        public const float TitleFadeOut = 1.0f;
        public const float TitleLength = TitleFadeIn + TitleHold + TitleFadeOut;

        // Shipwreck intro (a fresh island).
        public const float StormAt = 3.5f;
        public const float SinkingAt = 7f;
        public const float DarkAt = 9f;
        public const float WakingAt = 10.5f;
        public const float StandUpAt = 13f;
        public const float IntroLength = 14f;
        public const float WakeFadeSeconds = 1.8f;
        public const float CaptionFadeSeconds = 0.4f;

        /// <summary>Lightning strikes during the storm and the sinking.</summary>
        public static readonly float[] LightningAt = { 4.2f, 5.6f, 6.6f, 7.8f };

        /// <summary>The title card's opacity: in, hold, out.</summary>
        public static float TitleAlpha(float t) =>
            t < TitleFadeIn ? Mathf.Clamp01(t / TitleFadeIn)
            : t < TitleFadeIn + TitleHold ? 1f
            : Mathf.Clamp01(1f - (t - TitleFadeIn - TitleHold) / TitleFadeOut);

        public static IntroShot Shot(float t) =>
            t < StormAt ? IntroShot.Sailing
            : t < SinkingAt ? IntroShot.Storm
            : t < DarkAt ? IntroShot.Sinking
            : t < WakingAt ? IntroShot.Dark
            : t < IntroLength ? IntroShot.Waking
            : IntroShot.Done;

        /// <summary>Which caption shows: one per shot, the sinking and the dark share one.</summary>
        public static int CaptionIndex(float t, int captions)
        {
            var index = Shot(t) switch
            {
                IntroShot.Sailing => 0,
                IntroShot.Storm => 1,
                IntroShot.Sinking or IntroShot.Dark => 2,
                IntroShot.Waking => 3,
                _ => -1,
            };
            return index < captions ? index : -1;
        }

        public static float ShotStart(IntroShot shot) => shot switch
        {
            IntroShot.Storm => StormAt,
            IntroShot.Sinking => SinkingAt,
            IntroShot.Dark => DarkAt,
            IntroShot.Waking => WakingAt,
            IntroShot.Done => IntroLength,
            _ => 0f,
        };

        /// <summary>Opacity of the caption: it fades in at the start of its shot.</summary>
        public static float CaptionAlpha(float t)
        {
            var shot = Shot(t);
            var start = shot == IntroShot.Dark ? SinkingAt : ShotStart(shot);
            return Mathf.Clamp01((t - start) / CaptionFadeSeconds);
        }

        /// <summary>Black over the world while waking: fades out, with one slow blink.</summary>
        public static float WakeDarkness(float t)
        {
            if (t < WakingAt) return 1f;
            var dark = 1f - Mathf.Clamp01((t - WakingAt) / WakeFadeSeconds);
            var blink = Mathf.Clamp01(1f - Mathf.Abs(t - (WakingAt + WakeFadeSeconds + 0.4f)) / 0.25f) * 0.7f;
            return Mathf.Max(dark, blink);
        }

        /// <summary>Skipping jumps to the waking shot; skipping again (or while waking) ends it.</summary>
        public static float SkipFrom(float t) => t < WakingAt ? WakingAt + WakeFadeSeconds * 0.5f : IntroLength;

        /// <summary>How strong the lightning flash is now (0–1).</summary>
        public static float Flash(float t)
        {
            var flash = 0f;
            foreach (var at in LightningAt) flash = Mathf.Max(flash, t >= at ? 1f - (t - at) / 0.3f : 0f);
            return Mathf.Clamp01(flash);
        }
    }

    /// <summary>Flat-shape drawing for the cutscenes (stage 1 placeholders: rectangles, a disc, solid colour).</summary>
    static class CutsceneArt
    {
        static Texture2D _disc;

        public static void Rect(float x, float y, float w, float h, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public static void Disc(Vector2 centre, float radius, Color colour)
        {
            if (_disc == null)
            {
                const int size = 64;
                _disc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    _disc.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(size / 2f - d)));
                }
                _disc.Apply();
            }
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f), _disc);
            GUI.color = previous;
        }

        /// <summary>A vertical gradient in bands.</summary>
        public static void Gradient(float y0, float y1, Color top, Color bottom, int bands = 24)
        {
            var h = (y1 - y0) / bands;
            for (var i = 0; i < bands; i++) Rect(0f, y0 + i * h, Screen.width, h + 1f, Color.Lerp(top, bottom, i / (bands - 1f)));
        }

        /// <summary>Rows of moving wave crests between <paramref name="y0"/> and the bottom.</summary>
        public static void Waves(float y0, float t, Color crest, float speed, float amplitude)
        {
            var rows = 9;
            for (var r = 0; r < rows; r++)
            {
                var depth = (r + 1f) / rows;
                var y = Mathf.Lerp(y0, Screen.height, depth * depth) + Mathf.Sin(t * 1.3f + r) * amplitude * depth;
                var width = 20f + 90f * depth;
                var count = 7 + r;
                for (var i = 0; i < count; i++)
                {
                    var x = Mathf.Repeat(i * Screen.width / (float)count + t * speed * (0.3f + depth) + r * 37f, Screen.width + width) - width;
                    Rect(x, y, width, 2f + 3f * depth, new Color(crest.r, crest.g, crest.b, crest.a * (0.4f + 0.6f * depth)));
                }
            }
        }

        public static void Text(Rect rect, string text, int size, Color colour, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var gs = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = style, wordWrap = true };
            gs.normal.textColor = new Color(0f, 0f, 0f, colour.a * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, gs);
            gs.normal.textColor = colour;
            GUI.Label(rect, text, gs);
        }

        public static bool SkipPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
        }

        /// <summary>Clicks and keys go to the cutscene, not to the menu or the HUD under it.</summary>
        public static void EatInput()
        {
            var e = Event.current;
            if (e.type is EventType.MouseDown or EventType.MouseUp or EventType.KeyDown or EventType.ScrollWheel) e.Use();
        }
    }

    /// <summary>T-306: the title card once per launch, over the main menu — the sea at night, "ISLE", then the menu.</summary>
    public sealed class TitleCutscene : MonoBehaviour
    {
        /// <summary>Shown already this run (it doesn't come back after Back to menu).</summary>
        public static bool Shown { get; private set; }

        public static bool Playing { get; private set; }

        float _startedAt = -1f;

        void Start()
        {
            Shown = true;
            Playing = true;
            _startedAt = Time.unscaledTime;
        }

        void OnDestroy() => Playing = false;

        void Update()
        {
            var t = Time.unscaledTime - _startedAt;
            if (CutsceneArt.SkipPressed() && t < CutsceneTimeline.TitleFadeIn + CutsceneTimeline.TitleHold)
                _startedAt = Time.unscaledTime - (CutsceneTimeline.TitleFadeIn + CutsceneTimeline.TitleHold);
            if (t >= CutsceneTimeline.TitleLength) Destroy(this);
        }

        /// <summary>Tests: ends it now.</summary>
        public void Skip() => Destroy(this);

        void OnGUI()
        {
            GUI.depth = -200;
            var t = Time.unscaledTime - _startedAt;
            var alpha = t < CutsceneTimeline.TitleFadeIn + CutsceneTimeline.TitleHold ? 1f : CutsceneTimeline.TitleAlpha(t);
            var text = CutsceneTimeline.TitleAlpha(t);
            var w = Screen.width;
            var h = Screen.height;
            var horizon = h * 0.6f;

            CutsceneArt.Gradient(0f, horizon, new Color(0.03f, 0.04f, 0.09f, alpha), new Color(0.13f, 0.14f, 0.24f, alpha));
            CutsceneArt.Disc(new Vector2(w * 0.72f, h * 0.24f), h * 0.06f, new Color(0.96f, 0.93f, 0.8f, alpha));
            CutsceneArt.Gradient(horizon, h, new Color(0.06f, 0.09f, 0.16f, alpha), new Color(0.02f, 0.03f, 0.07f, alpha), 12);
            CutsceneArt.Rect(w * 0.72f - h * 0.05f, horizon + 4f, h * 0.1f, h * 0.25f, new Color(0.9f, 0.85f, 0.6f, alpha * 0.12f)); // moon path
            CutsceneArt.Waves(horizon, t, new Color(0.5f, 0.6f, 0.75f, 0.5f * alpha), 18f, 2f);
            // A low island on the horizon.
            CutsceneArt.Rect(w * 0.18f, horizon - h * 0.025f, w * 0.22f, h * 0.025f, new Color(0.02f, 0.03f, 0.05f, alpha));
            CutsceneArt.Rect(w * 0.24f, horizon - h * 0.045f, w * 0.08f, h * 0.02f, new Color(0.02f, 0.03f, 0.05f, alpha));

            CutsceneArt.Text(new Rect(0f, h * 0.3f, w, 140f), "ISLE", 120, new Color(1f, 0.96f, 0.86f, text), TextAnchor.MiddleCenter, FontStyle.Bold);
            CutsceneArt.Text(new Rect(0f, h * 0.3f + 130f, w, 40f), Lang.Get("@ui.title_tagline"), 22, new Color(0.85f, 0.82f, 0.74f, text * 0.9f), TextAnchor.MiddleCenter);
            if (alpha >= 1f)
                CutsceneArt.Text(new Rect(0f, h - 50f, w - 30f, 30f), Lang.Get("@ui.cutscene_skip"), 14, new Color(0.7f, 0.7f, 0.7f, 0.6f * text), TextAnchor.MiddleRight);
            CutsceneArt.EatInput();
        }
    }

    /// <summary>
    /// T-306: the shipwreck intro on a fresh island — a cruise ship at night, the storm, the sinking, the dark, then you
    /// wake lying on the beach and get up. Captions are the scenario's <c>intro</c> lang keys. Any key skips. The local
    /// player is frozen until it ends. Presentation only: the world was already set up by <c>StartDirector</c>.
    /// </summary>
    public sealed class ShipwreckCutscene : MonoBehaviour
    {
        public static ShipwreckCutscene Instance { get; private set; }

        /// <summary>PlayMode tests (which skip the menu) don't sit through it.</summary>
        public static bool Enabled => !GameSession.SkipMenu;

        public bool Playing { get; private set; }
        public float Elapsed => Playing ? Time.unscaledTime - _startedAt : 0f;
        public IntroShot CurrentShot => Playing ? CutsceneTimeline.Shot(Elapsed) : IntroShot.Done;

        float _startedAt;
        string[] _captions;
        PlayerInteraction _frozen;

        void Awake()
        {
            Instance = this;
            GameFeed.ScenarioBegan += OnScenarioBegan;
        }

        void OnDestroy()
        {
            GameFeed.ScenarioBegan -= OnScenarioBegan;
            if (Instance == this) Instance = null;
            Finish();
        }

        void OnScenarioBegan(object scenario)
        {
            if (!Enabled || scenario is not ScenarioDef def || def.Intro == null || def.Intro.Length == 0) return;
            Play(def.Intro);
        }

        /// <summary>Starts the intro with these captions (lang keys).</summary>
        public void Play(string[] captions)
        {
            _captions = captions;
            _startedAt = Time.unscaledTime;
            Playing = true;
            Isle.UI.Art.StickFigureView.LocalLying = true;
        }

        /// <summary>Tests: like pressing a key.</summary>
        public void Skip() => _startedAt = Time.unscaledTime - CutsceneTimeline.SkipFrom(Elapsed);

        void Update()
        {
            if (!Playing) return;
            var player = PlayerInteraction.Local;
            if (player != null && player.TryGetComponent<Isle.Networking.PlayerMovement>(out var movement) && !movement.Frozen)
            {
                movement.Frozen = true;
                _frozen = player;
            }
            if (CutsceneArt.SkipPressed()) Skip();
            if (Elapsed >= CutsceneTimeline.StandUpAt) Isle.UI.Art.StickFigureView.LocalLying = false;
            if (Elapsed >= CutsceneTimeline.IntroLength) Finish();
        }

        void Finish()
        {
            Playing = false;
            Isle.UI.Art.StickFigureView.LocalLying = false;
            if (_frozen != null && _frozen.TryGetComponent<Isle.Networking.PlayerMovement>(out var movement) && !_frozen.Sleeping) movement.Frozen = false;
            _frozen = null;
        }

        void OnGUI()
        {
            if (!Playing) return;
            GUI.depth = -150;
            var t = Elapsed;
            var shot = CutsceneTimeline.Shot(t);
            var w = Screen.width;
            var h = Screen.height;

            if (shot is IntroShot.Sailing or IntroShot.Storm or IntroShot.Sinking) DrawSea(t, shot);
            else if (shot == IntroShot.Dark) CutsceneArt.Rect(0f, 0f, w, h, Color.black);
            else CutsceneArt.Rect(0f, 0f, w, h, new Color(0f, 0f, 0f, CutsceneTimeline.WakeDarkness(t)));

            // Letterbox, pulling away as you wake.
            var bars = shot == IntroShot.Waking ? Mathf.Clamp01(1f - (t - CutsceneTimeline.WakingAt) / 2f) : 1f;
            CutsceneArt.Rect(0f, 0f, w, h * 0.1f * bars, Color.black);
            CutsceneArt.Rect(0f, h - h * 0.1f * bars, w, h * 0.1f * bars, Color.black);

            var index = CutsceneTimeline.CaptionIndex(t, _captions?.Length ?? 0);
            if (index >= 0)
            {
                var a = CutsceneTimeline.CaptionAlpha(t);
                if (shot == IntroShot.Waking) a *= Mathf.Clamp01((CutsceneTimeline.IntroLength - t) / 0.6f);
                CutsceneArt.Text(new Rect(w * 0.1f, h * 0.9f - 70f, w * 0.8f, 60f), Lang.Get(_captions[index]), 26, new Color(1f, 0.96f, 0.88f, a), TextAnchor.MiddleCenter);
            }
            if (shot != IntroShot.Waking)
                CutsceneArt.Text(new Rect(0f, h - 34f, w - 24f, 26f), Lang.Get("@ui.cutscene_skip"), 13, new Color(0.7f, 0.7f, 0.7f, 0.6f), TextAnchor.MiddleRight);
            CutsceneArt.EatInput();
        }

        void DrawSea(float t, IntroShot shot)
        {
            var w = Screen.width;
            var h = Screen.height;
            var storm = shot == IntroShot.Sailing ? 0f : Mathf.Clamp01((t - CutsceneTimeline.StormAt) / 1.5f);
            var horizon = h * 0.58f;
            var waterline = h * 0.7f;

            CutsceneArt.Gradient(0f, horizon, Color.Lerp(new Color(0.04f, 0.05f, 0.12f), new Color(0.02f, 0.02f, 0.04f), storm),
                Color.Lerp(new Color(0.14f, 0.15f, 0.26f), new Color(0.07f, 0.07f, 0.1f), storm));
            if (storm < 1f) CutsceneArt.Disc(new Vector2(w * 0.8f, h * 0.2f), h * 0.05f, new Color(0.95f, 0.92f, 0.8f, 1f - storm));
            // Storm clouds rolling in.
            for (var i = 0; i < 7; i++)
            {
                var x = Mathf.Repeat(i * w / 6f + t * 25f, w + 400f) - 300f;
                CutsceneArt.Rect(x, h * (0.08f + 0.05f * (i % 3)), 300f + 60f * (i % 2), h * 0.07f, new Color(0.1f, 0.1f, 0.13f, storm * 0.9f));
            }
            CutsceneArt.Gradient(horizon, h, Color.Lerp(new Color(0.06f, 0.1f, 0.18f), new Color(0.04f, 0.05f, 0.08f), storm), new Color(0.01f, 0.02f, 0.05f), 12);
            CutsceneArt.Waves(horizon, t, new Color(0.55f, 0.65f, 0.8f, 0.5f), 20f + 60f * storm, 2f + 10f * storm);

            DrawShip(t, shot, storm, waterline);

            // The near water in front of the ship (it hides the hull below the waterline, and the ship as it sinks).
            var swell = Mathf.Sin(t * 2.1f) * (3f + 14f * storm);
            CutsceneArt.Gradient(waterline + swell, h, new Color(0.05f, 0.08f, 0.15f), new Color(0.01f, 0.02f, 0.05f), 10);
            CutsceneArt.Waves(waterline + swell, t + 3f, new Color(0.7f, 0.8f, 0.9f, 0.6f), 30f + 80f * storm, 3f + 12f * storm);

            if (storm > 0f) DrawRain(t, storm);
            var flash = CutsceneTimeline.Flash(t);
            if (flash > 0f)
            {
                DrawBolt(t, flash);
                CutsceneArt.Rect(0f, 0f, w, h, new Color(0.9f, 0.92f, 1f, flash * 0.55f));
            }
            // The sinking fades to black.
            if (shot == IntroShot.Sinking)
                CutsceneArt.Rect(0f, 0f, w, h, new Color(0f, 0f, 0f, Mathf.Clamp01((t - CutsceneTimeline.SinkingAt - 0.8f) / 1.2f)));
        }

        void DrawShip(float t, IntroShot shot, float storm, float waterline)
        {
            var w = Screen.width;
            var h = Screen.height;
            var length = w * 0.3f;
            var hullHeight = h * 0.06f;
            var cx = Mathf.Lerp(-length * 0.5f, w * 0.48f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / CutsceneTimeline.StormAt)));
            var sink = shot == IntroShot.Sinking ? (t - CutsceneTimeline.SinkingAt) / (CutsceneTimeline.DarkAt - CutsceneTimeline.SinkingAt) : 0f;
            var bob = Mathf.Sin(t * 1.7f) * (2f + 10f * storm);
            var angle = Mathf.Sin(t * 1.3f) * (1.5f + 7f * storm) - 22f * sink;
            var bottom = waterline + hullHeight * 0.35f + bob + sink * h * 0.22f;

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2(cx, bottom - hullHeight));
            var left = cx - length * 0.5f;
            var dark = new Color(0.12f, 0.12f, 0.14f);
            // Hull, its red keel band and a pointed bow (stepped blocks).
            CutsceneArt.Rect(left, bottom - hullHeight, length, hullHeight, dark);
            CutsceneArt.Rect(left, bottom - hullHeight * 0.3f, length, hullHeight * 0.3f, new Color(0.45f, 0.12f, 0.1f));
            for (var i = 0; i < 4; i++)
                CutsceneArt.Rect(left + length + i * 6f, bottom - hullHeight + i * hullHeight * 0.22f, 6f, hullHeight * (1f - i * 0.22f), dark);
            // Decks and the funnel.
            var light = new Color(0.85f, 0.85f, 0.82f);
            CutsceneArt.Rect(left + length * 0.12f, bottom - hullHeight - h * 0.045f, length * 0.72f, h * 0.045f, light);
            CutsceneArt.Rect(left + length * 0.25f, bottom - hullHeight - h * 0.085f, length * 0.45f, h * 0.04f, light);
            CutsceneArt.Rect(left + length * 0.5f, bottom - hullHeight - h * 0.135f, length * 0.08f, h * 0.05f, new Color(0.7f, 0.25f, 0.2f));
            CutsceneArt.Rect(left + length * 0.5f, bottom - hullHeight - h * 0.135f, length * 0.08f, h * 0.012f, dark);
            // Lit windows; they flicker out as it goes down.
            var lit = shot == IntroShot.Sinking ? (Mathf.Repeat(t * 7f, 1f) > sink ? 1f : 0f) : 1f;
            for (var row = 0; row < 2; row++)
            for (var i = 0; i < 14 - row * 5; i++)
                CutsceneArt.Rect(left + length * (0.15f + row * 0.13f) + i * length * 0.048f, bottom - hullHeight - h * (0.032f + row * 0.04f), length * 0.022f, h * 0.012f,
                    new Color(1f, 0.85f, 0.45f, lit));
            GUI.matrix = matrix;
        }

        static void DrawRain(float t, float storm)
        {
            var w = Screen.width;
            var h = Screen.height;
            var drops = Mathf.RoundToInt(160 * storm);
            var matrix = GUI.matrix;
            for (var i = 0; i < drops; i++)
            {
                var seed = i * 7919;
                var x = Mathf.Repeat(seed * 0.618f + t * 260f, w + 100f) - 50f;
                var y = Mathf.Repeat(seed * 0.37f + t * 1400f + i * 13f, h + 40f) - 20f;
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(18f, new Vector2(x, y));
                CutsceneArt.Rect(x, y, 2f, 22f, new Color(0.7f, 0.75f, 0.85f, 0.35f));
            }
            GUI.matrix = matrix;
        }

        static void DrawBolt(float t, float flash)
        {
            var w = Screen.width;
            var h = Screen.height;
            var strike = 0;
            for (var i = 0; i < CutsceneTimeline.LightningAt.Length; i++)
                if (t >= CutsceneTimeline.LightningAt[i]) strike = i;
            var x = w * (0.2f + 0.17f * strike % 0.7f);
            var y = 0f;
            var matrix = GUI.matrix;
            for (var s = 0; s < 6; s++)
            {
                var length = h * 0.1f;
                var turn = (s % 2 == 0 ? 1f : -1f) * (20f + 9f * ((strike + s) % 3));
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(turn, new Vector2(x, y));
                CutsceneArt.Rect(x - 2f, y, 4f, length, new Color(1f, 1f, 0.95f, flash));
                x += -Mathf.Sin(turn * Mathf.Deg2Rad) * length;
                y += Mathf.Cos(turn * Mathf.Deg2Rad) * length;
            }
            GUI.matrix = matrix;
        }
    }
}
