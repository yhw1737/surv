using System;

namespace Isle.Gameplay.Skills
{
    /// <summary>
    /// SYS-SKILL-01 §Focus — the 7-day median distinct-active-players count that feeds
    /// <see cref="FocusCalculator.ActivePlayerFactor"/>. A ring buffer of daily distinct counts,
    /// not concurrent players: this is what stops gaming the multiplayer discount by briefly
    /// connecting a friend.
    /// <para>
    /// ponytail: the median of an even-sized window can land between two integers (e.g. 2 and 3
    /// distinct-player days average to 2.5), but <c>ActivePlayerFactor</c> only has bands for
    /// 1/2/3/&gt;=4 — no spec value exists for a fractional n. Rounded to the nearest int here;
    /// this only matters during a world's first 6 days, before the window fills. Flagged in
    /// PROJECT_STATE.md §Decided without a spec.
    /// </para>
    /// </summary>
    public sealed class ActivityTracker
    {
        const int WindowDays = 7;

        readonly int[] _dailyDistinctCounts = new int[WindowDays];
        int _recordedDays;
        int _nextSlot;

        /// <summary>Call once per elapsed in-game day with that day's distinct active player count.</summary>
        public void RecordDay(int distinctActivePlayers)
        {
            _dailyDistinctCounts[_nextSlot] = distinctActivePlayers;
            _nextSlot = (_nextSlot + 1) % WindowDays;
            if (_recordedDays < WindowDays) _recordedDays++;
        }

        /// <summary>Median of every recorded day so far (up to the last 7). Defaults to 1 (solo) before the first day is recorded.</summary>
        public int MedianActivePlayers()
        {
            if (_recordedDays == 0) return 1;

            var values = new int[_recordedDays];
            Array.Copy(_dailyDistinctCounts, values, _recordedDays);
            Array.Sort(values);

            var mid = _recordedDays / 2;
            if (_recordedDays % 2 == 1) return values[mid];

            return (int)Math.Round((values[mid - 1] + values[mid]) / 2.0, MidpointRounding.AwayFromZero);
        }
    }
}
