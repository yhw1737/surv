using Isle.World.Weather;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>Screen-space rain streaks and snowflakes while <see cref="WeatherController"/> says so, plus a faint
    /// tint, fading in and out over <see cref="WeatherFadeSeconds"/>. Drawn behind the HUD's windows and not at all over
    /// the full-screen inventory. Pure presentation; particle counts and speeds are look-and-feel values.</summary>
    public sealed class WeatherOverlay : MonoBehaviour
    {
        const int RainDrops = 140;
        const int SnowFlakes = 90;

        /// <summary>How long rain or snow takes to come in or clear away on screen (SYS-WORLD-02, 2026-10-12).</summary>
        public const float WeatherFadeSeconds = 6f;

        readonly Vector2[] _particles = new Vector2[RainDrops];
        bool _seeded;
        bool _snow;

        /// <summary>0 = clear, 1 = full rain or snow; eases toward the weather's state.</summary>
        public float Intensity { get; private set; }

        void Update()
        {
            var weather = WeatherController.Instance;
            var falling = weather != null && (weather.IsRaining || weather.IsSnowing);
            if (falling) _snow = weather.IsSnowing;
            Intensity = Mathf.MoveTowards(Intensity, falling ? 1f : 0f, Time.deltaTime / WeatherFadeSeconds);
        }

        void OnGUI()
        {
            GUI.depth = 10; // behind the HUD's windows (depth 0)
            if (Event.current.type != EventType.Repaint || Intensity <= 0f) return;
            if (Isle.UI.Inventory.InventoryScreen.Instance is { Open: true }) return;

            if (!_seeded)
            {
                for (var i = 0; i < _particles.Length; i++) _particles[i] = new Vector2(Random.value, Random.value);
                _seeded = true;
            }

            var snow = _snow;
            GUI.color = snow ? new Color(1f, 1f, 1f, 0.06f * Intensity) : new Color(0.2f, 0.3f, 0.5f, 0.12f * Intensity);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            var count = Mathf.RoundToInt((snow ? SnowFlakes : RainDrops) * Intensity);
            var fall = snow ? 0.08f : 1.6f;
            GUI.color = snow ? new Color(1f, 1f, 1f, 0.9f * Intensity) : new Color(0.75f, 0.85f, 1f, 0.55f * Intensity);
            for (var i = 0; i < count; i++)
            {
                var p = _particles[i];
                p.y = Mathf.Repeat(p.y + fall * Time.deltaTime, 1f);
                p.x = Mathf.Repeat(p.x + (snow ? Mathf.Sin(Time.time + i) * 0.0008f : 0.25f * Time.deltaTime), 1f);
                _particles[i] = p;

                var x = p.x * Screen.width;
                var y = p.y * Screen.height;
                var rect = snow ? new Rect(x, y, 4f, 4f) : new Rect(x, y, 1.5f, 16f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
