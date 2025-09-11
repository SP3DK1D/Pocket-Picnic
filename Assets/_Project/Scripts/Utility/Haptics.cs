using UnityEngine;

namespace CatchTheFruit
{
    /// <summary>
    /// Minimal cross-platform haptics wrapper.
    /// • No dependencies. Works even if you don't have Settings/toggles yet.
    /// • On device (iOS/Android): Handheld.Vibrate().
    /// • In Editor/PC: no-op (logs if enableEditorLogs = true).
    /// </summary>
    public static class Haptics
    {
        public enum Type { Light, Medium, Heavy }

        // You can toggle this at runtime if you add a settings UI later.
        public static bool Enabled = true;
        public static bool enableEditorLogs = false;

        // Simple anti-spam throttle so multiple calls in 1 frame don't buzz forever.
        static float _lastAt;
        const float _minInterval = 0.03f; // seconds

        public static void Play(Type type = Type.Light)
        {
            if (!Enabled) return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastAt < _minInterval) return;
            _lastAt = now;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            // Baseline portable buzz; for nuanced iOS Taptic feedback use a native plugin later.
            Handheld.Vibrate();
#else
            if (enableEditorLogs) Debug.Log($"[Haptics] {type} (Editor no-op)");
#endif
        }

        public static void PlayLight() => Play(Type.Light);
        public static void PlayMedium() => Play(Type.Medium);
        public static void PlayHeavy() => Play(Type.Heavy);
    }
}
