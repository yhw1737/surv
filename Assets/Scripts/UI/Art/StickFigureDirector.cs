using Isle.Gameplay.Character;
using UnityEngine;

namespace Isle.UI.Art
{
    /// <summary>
    /// SYS-CHAR-02: gives every player a <see cref="StickFigureView"/>. Presentation only.
    /// </summary>
    public sealed class StickFigureDirector : MonoBehaviour
    {
        Material _material;

        // Frame pacing (vSync / 120 fallback) moved to the player's options — Isle.UI.Prototype.GameOptions.

        void Update()
        {
            var players = PlayerInteraction.All;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || StickFigureView.Has(player)) continue;
                _material ??= VectorMaterial.Create();
                if (_material == null) return;
                StickFigureView.Attach(player, _material);
            }
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }

    /// <summary>The material for <c>Isle/Vector</c> meshes. The shader sits in Resources so builds include it.</summary>
    public static class VectorMaterial
    {
        public const string ShaderName = "Isle/Vector";

        public static Material Create()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Art] shader {ShaderName} not found");
                return null;
            }
            return new Material(shader) { name = "IsleVector" };
        }
    }
}
