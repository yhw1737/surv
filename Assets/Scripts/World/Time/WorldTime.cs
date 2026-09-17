using FishNet.Object;

namespace Isle.World.Time
{
    /// <summary>
    /// SYS-WORLD-02 §Location: the live <see cref="WorldClock"/> holder T-030 never shipped — that
    /// task added the type but nothing ever instantiated or ticked it (<c>Vitals</c> only used the
    /// static <see cref="WorldClock.MinutesPerRealSecond"/> constant). Server-authoritative only, no
    /// client sync yet (T-030's own gap) — nothing client-facing reads time today.
    /// </summary>
    public sealed class WorldTime : NetworkBehaviour
    {
        public static WorldTime Instance { get; private set; }

        public WorldClock Clock { get; private set; }

        void Awake()
        {
            Instance = this;
            Clock = new WorldClock();
        }

        void Update()
        {
            if (!IsServer) return;
            Clock.Tick(UnityEngine.Time.deltaTime);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
