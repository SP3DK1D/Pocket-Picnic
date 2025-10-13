using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Minimal cross-platform haptics wrapper.
    /// - No external deps. Safe to call anywhere.
    /// - On iOS/Android devices: Handheld.Vibrate().
    /// - In Editor/PC/WebGL: no-op (optional debug log).
    ///
    /// Throttling:
    /// Prevents spam by enforcing a tiny min interval between buzzes.
    /// </summary>
    public static class Haptics
    {
        public enum Type { Light, Medium, Heavy } // kept for future nuance; currently same baseline vibrate

        /// <summary>Global on/off (hook to your Options UI if desired).</summary>
        public static bool Enabled = true;

        /// <summary>Logs to Console in Editor when a haptic would play.</summary>
        public static bool EnableEditorLogs = false;

        // Anti-spam throttle
        static float _lastAtUnscaled;
        const float MinInterval = 0.03f; // seconds

        /// <summary>Fire a haptic (throttled). Type is advisory and future-proof.</summary>
        public static void Play(Type type = Type.Light)
        {
            if (!Enabled) return;

            float now = Time.unscaledTime;
            if (now - _lastAtUnscaled < MinInterval) return;
            _lastAtUnscaled = now;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            // Baseline portable buzz. If you later add a native plugin,
            // map Type to platform-specific patterns here.
            Handheld.Vibrate();
#else
            if (EnableEditorLogs)
                Debug.Log($"[Haptics] {type} (Editor no-op)");
#endif
        }

        // Small convenience aliases
        public static void Light() => Play(Type.Light);
        public static void Medium() => Play(Type.Medium);
        public static void Heavy() => Play(Type.Heavy);
    }
}
