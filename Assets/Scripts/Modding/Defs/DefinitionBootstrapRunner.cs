using UnityEngine;

namespace Isle.Modding.Defs
{
    /// <summary>
    /// The runtime entry point <see cref="DefinitionBootstrap.Load"/> never had: it previously only
    /// ran from EditMode tests and the Editor-only <c>Isle/Reload Definitions</c> menu item, so
    /// <see cref="DefRegistry"/> was empty in every Play-mode session and build — anything resolving
    /// a def at runtime (e.g. <c>WorldObjectInstance.Awake</c>) silently failed
    /// (PROJECT_STATE.md §Decided without a spec).
    /// <para>
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> with <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>
    /// runs this before any <c>Awake</c> in the first loaded scene, on every scene and every launch —
    /// no scene GameObject to remember to add, unlike the first attempt at this fix
    /// (PROJECT_STATE.md §Decided without a spec records why that attempt still didn't run).
    /// </para>
    /// </summary>
    public static class DefinitionBootstrapRunner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            var root = Application.streamingAssetsPath + "/definitions";
            var errors = DefinitionBootstrap.Load(root);

            if (errors.Count == 0)
            {
                Debug.Log($"[Isle] Definitions loaded from {root}.");
                return;
            }

            foreach (var error in errors)
                Debug.LogError($"[Isle] {error}");
        }
    }
}
