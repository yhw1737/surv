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
            foreach (var creature in director.Creatures)
            {
                if (creature.View == null || !creature.View.activeInHierarchy || _drawn.Contains(creature)) continue;
                _material ??= VectorMaterial.Create();
                if (_material == null) return;
                CreatureFigure.Attach(creature, _material);
                _drawn.Add(creature);
            }

            // Dead creatures leave the director's list; forget them now and then.
            if (Time.time >= _nextSweepAt)
            {
                _nextSweepAt = Time.time + 5f;
                _drawn.RemoveWhere(c => c.View == null);
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
