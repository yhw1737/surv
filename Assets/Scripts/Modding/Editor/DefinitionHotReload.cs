using Isle.Modding.Defs;
using UnityEditor;
using UnityEngine;

namespace Isle.Modding.Editor
{
    /// <summary>SYS-CORE-01 §Hot reload: plain <c>F5</c> (no modifier — the <c>_</c> menu item prefix)
    /// re-parses <c>Assets/StreamingAssets/definitions</c> and applies it to the live registry.</summary>
    static class DefinitionHotReload
    {
        [MenuItem("Isle/Reload Definitions _F5")]
        static void Reload()
        {
            var root = Application.streamingAssetsPath + "/definitions";
            var errors = DefinitionBootstrap.Reload(root);

            if (errors.Count == 0)
            {
                Debug.Log($"[Isle] Definitions reloaded from {root}.");
                return;
            }

            foreach (var error in errors)
                Debug.LogError($"[Isle] {error}");
        }
    }
}
