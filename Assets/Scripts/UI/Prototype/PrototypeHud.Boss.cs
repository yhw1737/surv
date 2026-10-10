using System.Linq;
using Isle.Gameplay.Dungeons;
using Isle.Gameplay.Hunting;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>The boss health bar: across the top of the screen while the local player shares a floor with a living
    /// dungeon boss close by. Orange while it's overheated (hit it now), grey while it hides in its shell.</summary>
    public sealed partial class PrototypeHud
    {
        /// <summary>How close a boss must be for its bar to show, in tiles. [invented]</summary>
        const float BossBarRangeTiles = 16f;

        void DrawBossBar()
        {
            var boss = EngagedBoss();
            if (boss == null) return;
            const float width = 420f;
            var x = (Screen.width - width) * 0.5f;
            var y = Margin + 6f;
            Panel(new Rect(x - 10f, y - 4f, width + 20f, 44f));
            var title = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(x, y, width, 18f), Lang.Get(boss.Def.Name), title);
            var bar = new Rect(x, y + 20f, width, 12f);
            GUI.color = new Color(0.12f, 0.1f, 0.09f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = boss.IsOverheated ? new Color(1f, 0.55f, 0.15f)
                : boss.Shell.IsHidden(Time.time) ? new Color(0.55f, 0.55f, 0.55f)
                : UiTheme.Bad;
            GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * Mathf.Clamp01(boss.Health / Mathf.Max(1f, boss.MaxHealth)), bar.height - 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        Creature EngagedBoss()
        {
            var dungeons = DungeonDirector.Instance;
            var creatures = CreatureDirector.Instance;
            if (dungeons == null || creatures == null || _player == null) return null;
            Vector2 at = _player.transform.position;
            var floor = dungeons.FloorAt(at);
            if (floor == null) return null;
            foreach (var site in dungeons.Sites)
            {
                var boss = site.Boss;
                if (boss == null || site.BossDefeated || !creatures.Creatures.Contains(boss)) continue;
                if (dungeons.FloorAt(boss.Position) == floor && Vector2.Distance(boss.Position, at) <= BossBarRangeTiles) return boss;
            }
            return null;
        }
    }
}
