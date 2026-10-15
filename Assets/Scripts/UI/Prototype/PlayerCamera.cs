using FishNet.Object;
using Isle.Core.Util;
using Isle.Gameplay.Character;
using Isle.World.Island;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

            // T-307: Q / E turn the view a step around the player; everything standing turns to keep facing the camera.
            var kb = Keyboard.current;
            if (kb != null && GUIUtility.keyboardControl == 0 && Time.timeScale > 0f)
            {
                if (kb.qKey.wasPressedThisFrame) ViewTilt.Turn(-1);
                if (kb.eKey.wasPressedThisFrame) ViewTilt.Turn(1);
            }
            if (ViewTilt.StepTurn(Time.unscaledDeltaTime) || _sortedYaw != ViewTilt.YawDegrees) ApplySortAxis();

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
        float _sortedYaw;

        static readonly System.Reflection.FieldInfo SortAxisField =
            typeof(Renderer2DData).GetField("m_TransparencySortAxis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>T-307: standing things draw back to front along the screen's up direction. URP's 2D renderer reads
        /// the sort axis only from its asset, so it's set there (and put back to north when the camera goes away, so
        /// the asset isn't left turned).</summary>
        void ApplySortAxis()
        {
            _sortedYaw = ViewTilt.YawDegrees;
            SetSortAxis(ViewTilt.ScreenUp);
        }

        static void SetSortAxis(Vector2 up)
        {
            if (SortAxisField == null || GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset) return;
            foreach (var data in asset.rendererDataList)
                if (data is Renderer2DData renderer2D) SortAxisField.SetValue(renderer2D, new Vector3(up.x, up.y, 0f));
        }

        void OnDestroy()
        {
            ViewTilt.ResetYaw();
            SetSortAxis(Vector2.up);
        }

        static PlayerInteraction LocalPlayer()
        {
            foreach (var player in PlayerInteraction.All)
                if (player.IsOwner) return player;
            return null;
        }
    }
}
