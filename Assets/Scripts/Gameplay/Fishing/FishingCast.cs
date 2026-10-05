namespace Isle.Gameplay.Fishing
{
    public enum CastState { Waiting, Bite, Hooked, Missed }

    /// <summary>
    /// SYS-FISH-01 prototype revision: a cast line waits for a bite, then the angler has a short window to click
    /// and hook it. Pure state machine stepped by the server; the wait and window lengths come from the caller.
    /// </summary>
    public sealed class FishingCast
    {
        readonly float _biteAfter;
        readonly float _hookWindow;
        float _elapsed;
        float _biteElapsed;

        public FishingCast(float biteAfterSeconds, float hookWindowSeconds)
        {
            _biteAfter = biteAfterSeconds;
            _hookWindow = hookWindowSeconds;
        }

        public CastState State { get; private set; } = CastState.Waiting;

        /// <summary>0..1 through the hook window while biting — for the HUD's "!" urgency.</summary>
        public float BiteFraction => _hookWindow <= 0f ? 1f : _biteElapsed / _hookWindow;

        public void Step(float seconds, bool clicked)
        {
            switch (State)
            {
                case CastState.Waiting:
                    _elapsed += seconds;
                    if (_elapsed >= _biteAfter) State = CastState.Bite;
                    break;
                case CastState.Bite:
                    if (clicked)
                    {
                        State = CastState.Hooked;
                        break;
                    }
                    _biteElapsed += seconds;
                    if (_biteElapsed > _hookWindow) State = CastState.Missed;
                    break;
            }
        }
    }
}
