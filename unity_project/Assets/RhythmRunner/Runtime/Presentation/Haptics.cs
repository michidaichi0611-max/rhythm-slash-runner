using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Short haptic ticks. Never required for play (SOUND_SPEC 5). Android uses VibrationEffect (API 26+) with a plain
    /// vibrate fallback for API 25. iOS (round 32) uses the Taptic Engine through a tiny native plugin
    /// (Assets/Plugins/iOS/RsrHaptics.mm) — never the 0.4 s Handheld.Vibrate (review/round0 E06).
    /// </summary>
    public static class Haptics
    {
        public enum Strength { Light, Medium, Success }

        // Referencing Handheld.Vibrate makes Unity add android.permission.VIBRATE to the manifest; it is never called.
#pragma warning disable 0649
        static bool neverTrue;
#pragma warning restore 0649
        public static void EnsurePermissionReference()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (neverTrue) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk;
        static bool initialized;

        static void Init()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e) { Debug.LogWarning("Haptics unavailable: " + e.Message); vibrator = null; }
        }

        static void Vibrate(long ms, int amplitude)
        {
            Init();
            if (vibrator == null) return;
            try
            {
                if (sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        vibrator.Call("vibrate", oneShot);
                }
                else vibrator.Call("vibrate", ms);
            }
            catch (System.Exception e) { Debug.LogWarning("Haptics failed: " + e.Message); vibrator = null; }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void RsrHapticImpact(int style);
#endif

        public static void Play(Strength strength)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { RsrHapticImpact(strength == Strength.Light ? 0 : strength == Strength.Medium ? 1 : 2); }
            catch (System.Exception e) { Debug.LogWarning("Haptics failed: " + e.Message); }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            switch (strength)
            {
                case Strength.Light: Vibrate(10, 90); break;
                case Strength.Medium: Vibrate(22, 170); break;
                case Strength.Success: Vibrate(35, 220); break;
            }
#endif
        }

        public static void Tap() { Play(Strength.Light); }
    }
}
