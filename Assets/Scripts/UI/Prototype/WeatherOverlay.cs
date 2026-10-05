using Isle.World.Weather;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>Screen-space rain streaks and snowflakes while <see cref="WeatherController"/> says so, plus a faint
    /// tint. Pure presentation; particle counts and speeds are look-and-feel values.</summary>
    public sealed class WeatherOverlay : MonoBehaviour
    {
        const int RainDrops = 140;
        const int SnowFlakes = 90;

        readonly Vector2[] _particles = new Vector2[RainDrops];
        bool _seeded;

        void OnGUI()
        {
            var weather = WeatherController.Instance;
            if (weather == null || Event.current.type != EventType.Repaint) return;
            if (!weather.IsRaining && !weather.IsSnowing) return;

            if (!_seeded)
            {
                for (var i = 0; i < _particles.Length; i++) _particles[i] = new Vector2(Random.value, Random.value);
                _seeded = true;
            }

            var snow = weather.IsSnowing;
            GUI.color = snow ? new Color(1f, 1f, 1f, 0.06f) : new Color(0.2f, 0.3f, 0.5f, 0.12f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            var count = snow ? SnowFlakes : RainDrops;
            var fall = snow ? 0.08f : 1.6f;
            GUI.color = snow ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.75f, 0.85f, 1f, 0.55f);
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
