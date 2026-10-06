using FishNet.Object;
using Isle.Gameplay.Character;
using Isle.World.Island;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Follows the local player with the scene's main camera. Presentation only — no state, no authority.
    /// The view size is a presentation value (how many tiles fit on screen), not a balance number.
    /// </summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        /// <summary>Orthographic half-height in tiles. Project Zomboid / Don't Starve framing (developer, 2026-10-06): the
        /// character is small on screen (~1/11 of its height) and trees tower over it.</summary>
        const float ViewHalfHeightTiles = 8.5f;
        const float Smoothing = 8f;

        Camera _camera;

        void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var target = LocalPlayer();
            if (target == null) return;

            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = IslandWorld.SeaColour;
            _camera.orthographicSize = ViewHalfHeightTiles;
            // Follow the drawn figure, which moves every frame, rather than the transform, which moves per network tick.
            var at = Isle.UI.Art.StickFigureView.PositionOf(target);
            var goal = new Vector3(at.x, at.y, _camera.transform.position.z);
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, goal, 1f - Mathf.Exp(-Smoothing * Time.deltaTime));
        }

        static PlayerInteraction LocalPlayer()
        {
            foreach (var player in PlayerInteraction.All)
                if (player.IsOwner) return player;
            return null;
        }
    }
}
