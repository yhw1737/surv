using UnityEngine;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// Player options, kept in PlayerPrefs (per machine, not per save): language, display mode, vSync, frame cap and
    /// master volume. <see cref="Apply"/> runs at boot and whenever the options screen changes something.
    /// Presentation only — nothing here touches the simulation.
    /// </summary>
    public static class GameOptions
    {
        const string LanguageKey = "isle.language";
        const string FullscreenKey = "isle.fullscreen";
        const string VSyncKey = "isle.vsync";
        const string FrameCapKey = "isle.framecap";
        const string VolumeKey = "isle.volume";

        /// <summary>Frame caps offered when vSync is off; 0 = unlimited.</summary>
        public static readonly int[] FrameCaps = { 60, 120, 144, 240, 0 };

        /// <summary>"auto" follows the system language; otherwise a <c>lang/*.json</c> code.</summary>
        public static string Language
        {
            get => PlayerPrefs.GetString(LanguageKey, "auto");
            set { PlayerPrefs.SetString(LanguageKey, value); Lang.Reload(); }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); Apply(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); Apply(); }
        }

        /// <summary>Used when vSync is off (SYS-CHAR-02: display rate with vSync, 120 otherwise).</summary>
        public static int FrameCap
        {
            get => PlayerPrefs.GetInt(FrameCapKey, 120);
            set { PlayerPrefs.SetInt(FrameCapKey, value); Apply(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); Apply(); }
        }

        public static void Apply()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FrameCap <= 0 ? -1 : FrameCap;
            AudioListener.volume = Volume;
            if (!Application.isEditor && Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        }
    }
}
