using System;
using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Stores/loads player settings (SFX, Haptics). Static, lightweight.
    /// Everyone reads SettingsManager.SfxOn / HapticsOn and calls SetSfx / SetHaptics.
    /// </summary>
    public static class SettingsManager
    {
        const string K_Sfx = "set_sfx";
        const string K_Hap = "set_haptics";

        public static bool SfxOn { get; private set; }
        public static bool HapticsOn { get; private set; }

        public static event Action OnChanged;

        static SettingsManager()
        {
            SfxOn = PlayerPrefs.GetInt(K_Sfx, 1) == 1;
            HapticsOn = PlayerPrefs.GetInt(K_Hap, 1) == 1;
        }

        public static void SetSfx(bool on)
        {
            SfxOn = on;
            PlayerPrefs.SetInt(K_Sfx, on ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void SetHaptics(bool on)
        {
            HapticsOn = on;
            PlayerPrefs.SetInt(K_Hap, on ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
