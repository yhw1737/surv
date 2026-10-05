using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>SYS-CHAR-02: two-bone IK for knees and elbows. Pure.</summary>
    public static class TwoBoneIk
    {
        /// <summary>The middle joint for a limb from <paramref name="root"/> reaching for <paramref name="target"/>.
        /// <paramref name="bend"/> +1 puts the joint on the left of the root→target direction (a knee bending forward
        /// for a downward leg facing +x), −1 on the right. Out of reach, the limb points straight at the target.</summary>
        public static Vector2 Solve(Vector2 root, Vector2 target, float upper, float lower, float bend)
        {
            var toTarget = target - root;
            var distance = toTarget.magnitude;
            if (distance < 1e-5f) return root + Vector2.down * upper;
            var dir = toTarget / distance;
            if (distance >= upper + lower) return root + dir * upper;
            distance = Mathf.Max(distance, Mathf.Abs(upper - lower) + 1e-4f);

            // Law of cosines: how far along root→target the joint sits, and how far off the line.
            var along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            var off = Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            var perp = new Vector2(-dir.y, dir.x);
            return root + dir * along + perp * off * (bend < 0f ? -1f : 1f);
        }
    }

    /// <summary>Critically damped spring — chases a target without overshoot, the same at any frame rate. Pure.</summary>
    public static class Spring
    {
        public static void Damp(ref float value, ref float velocity, float target, float frequency, float dt)
        {
            // Exact solution of a critically damped oscillator over dt (Ryan Juckett's closed form).
            var omega = frequency;
            var x = value - target;
            var exp = Mathf.Exp(-omega * dt);
            var temp = (velocity + omega * x) * dt;
            value = target + (x + temp) * exp;
            velocity = (velocity - omega * temp) * exp;
        }

        public static void Damp(ref Vector2 value, ref Vector2 velocity, Vector2 target, float frequency, float dt)
        {
            var vx = velocity.x;
            var vy = velocity.y;
            var x = value.x;
            var y = value.y;
            Damp(ref x, ref vx, target.x, frequency, dt);
            Damp(ref y, ref vy, target.y, frequency, dt);
            value = new Vector2(x, y);
            velocity = new Vector2(vx, vy);
        }
    }

    /// <summary>Draws a networked object between its last two tick positions so it moves every rendered frame. Pure.</summary>
    public static class TickInterpolation
    {
        public static Vector2 Sample(Vector2 previous, Vector2 current, float previousTime, float currentTime, float now, float tickInterval)
        {
            // Show the state one tick behind: at the moment a tick lands we're still at `previous`, and we reach
            // `current` one tick interval later.
            var t = tickInterval <= 0f ? 1f : Mathf.Clamp01((now - currentTime) / tickInterval);
            return Vector2.LerpUnclamped(previous, current, t);
        }
    }

    /// <summary>Walk/run cycle phase in [0, 1), advanced by distance travelled. Pure.</summary>
    public static class Gait
    {
        public static float Advance(float phase, float distance, float stride) =>
            stride <= 0f ? phase : Mathf.Repeat(phase + distance / stride, 1f);
    }
}
