using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Isle.Networking
{
    /// <summary>
    /// T-020: wires FishNet's <see cref="NetworkManager"/> to a Tugboat listen server — the host
    /// starts both a server and a local client, so solo play runs the same host-authoritative
    /// path co-op does (SYS-NET-01 §Transports). Steam P2P is a second transport added in T-025
    /// (Phase 10); this is Tugboat-only on purpose.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(Tugboat))]
    public sealed class IsleNetworkManager : MonoBehaviour
    {
        NetworkManager _fishNet;

        void Awake()
        {
            _fishNet = GetComponent<NetworkManager>();
            _fishNet.TransportManager.Transport = GetComponent<Tugboat>();
        }

        void Start()
        {
            _fishNet.ServerManager.StartConnection();
            _fishNet.ClientManager.StartConnection();
        }
    }
}
