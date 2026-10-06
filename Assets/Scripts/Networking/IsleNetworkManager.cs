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

        /// <summary>Starts the listen server and the local client. Called when a game begins (the main menu's New
        /// game / Continue), not on scene load — the menu comes first.</summary>
        public void StartHost()
        {
            if (_fishNet == null) return;
            if (!_fishNet.ServerManager.Started) _fishNet.ServerManager.StartConnection();
            if (!_fishNet.ClientManager.Started) _fishNet.ClientManager.StartConnection();
        }

        /// <summary>Stops both, e.g. before returning to the main menu.</summary>
        public void StopHost()
        {
            if (_fishNet == null) return;
            if (_fishNet.ClientManager.Started) _fishNet.ClientManager.StopConnection();
            if (_fishNet.ServerManager.Started) _fishNet.ServerManager.StopConnection(true);
        }

        /// <summary>The live instance — the scene's, or the one FishNet kept across a scene reload.</summary>
        public static IsleNetworkManager Find() => FindFirstObjectByType<IsleNetworkManager>();
    }
}
