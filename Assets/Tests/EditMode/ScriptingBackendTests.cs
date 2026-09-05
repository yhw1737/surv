using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Isle.Tests.EditMode
{
    /// <summary>
    /// ADR-001: Mono is a prerequisite for Tier 3 modding. IL2CPP AOT-compiles IL to C++,
    /// which blocks runtime assembly loading. Switching the backend silently would not break
    /// any other test, so it gets its own.
    /// </summary>
    public sealed class ScriptingBackendTests
    {
        [Test]
        public void StandaloneBackend_IsMono()
        {
            Assert.AreEqual(
                ScriptingImplementation.Mono2x,
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone));
        }
    }
}
