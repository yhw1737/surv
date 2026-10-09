using FishNet.Object;
using Isle.Core.Util;
using Isle.Gameplay.Character;
using Isle.World.Island;
using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Follows the local player with the scene's main camera: a perspective camera looking down at the ground from the
    /// south, leaned <see cref="ViewTilt.PitchDegrees"/> off straight down (developer, 2026-10-09). Presentation only —
    /// no state, no authority. The view size is a presentation value (how many tiles fit on screen), not a balance number.
    /// </summary>
    public sealed class PlayerCamera : MonoBehaviour
    {
        /// <summary>Half-height of the view, in tiles, at the point the camera looks at. Project Zomboid / Don't Starve framing (developer, 2026-10-06): the
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

            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = IslandWorld.SeaColour;
            // Follow the drawn figure, which moves every frame, rather than the transform, which moves per network tick.
            var at = (Vector2)Isle.UI.Art.StickFigureView.PositionOf(target);
            _focus = _hasFocus ? Vector2.Lerp(_focus, at, 1f - Mathf.Exp(-Smoothing * Time.deltaTime)) : at;
            _hasFocus = true;
            Frame(_camera, _focus, ViewHalfHeightTiles);
        }

        /// <summary>Points <paramref name="camera"/> at <paramref name="focus"/> on the ground with the tilted perspective
        /// view, far enough back that <paramref name="halfHeightTiles"/> tiles fit above and below the focus.</summary>
        public static void Frame(Camera camera, Vector2 focus, float halfHeightTiles)
        {
            camera.orthographic = false;
            camera.fieldOfView = ViewTilt.FieldOfView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 500f;
            var distance = halfHeightTiles / Mathf.Tan(ViewTilt.FieldOfView * 0.5f * Mathf.Deg2Rad);
            var rotation = ViewTilt.Standing;
            camera.transform.rotation = rotation;
            camera.transform.position = new Vector3(focus.x, focus.y, 0f) - rotation * Vector3.forward * distance;
            ViewTilt.Focus = focus;
        }

        Vector2 _focus;
        bool _hasFocus;

        static PlayerInteraction LocalPlayer()
        {
            foreach (var player in PlayerInteraction.All)
                if (player.IsOwner) return player;
            return null;
        }
    }
}
