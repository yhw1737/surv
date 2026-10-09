using System.Collections.Generic;
using Isle.Gameplay.Hunting;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>Gives every creature a <see cref="CreatureFigure"/> once its view exists. Presentation only.</summary>
    public sealed class CreatureFigureDirector : MonoBehaviour
    {
        readonly HashSet<Creature> _drawn = new();
        Material _material;
        float _nextSweepAt;

        void Update()
        {
            var director = CreatureDirector.Instance;
            if (director == null) return;
            if (!Draw(director.Creatures)) return;
            // A carcass put back down after hauling gets a fresh view, which needs its figure too.
            if (!Draw(director.Carcasses)) return;

            // Dead creatures leave the director's list; forget them now and then.
            if (Time.time >= _nextSweepAt)
            {
                _nextSweepAt = Time.time + 5f;
                _drawn.RemoveWhere(c => c.View == null);
            }
        }

        bool Draw(IReadOnlyList<Creature> creatures)
        {
            foreach (var creature in creatures)
            {
                if (creature.View == null || !creature.View.activeInHierarchy || _drawn.Contains(creature)) continue;
                _material ??= VectorMaterial.Create();
                if (_material == null) return false;
                CreatureFigure.Attach(creature, _material);
                _drawn.Add(creature);
            }
            return true;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
