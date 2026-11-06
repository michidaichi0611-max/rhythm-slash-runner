using System;
using System.IO;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Round 34: anonymous play data through Unity Analytics (com.unity.services.analytics), only after the player agreed
    /// (asked once at first launch, switchable in the settings). It shows where runs end, whether people come back, and
    /// whether the play limit drives them away. Errors (exceptions) are sent as "client_error" events and also kept in
    /// errors.log on the device. Needs the Unity project linked to Unity Cloud (Project ID); until then nothing is sent.
    /// Custom events are defined in the Unity Dashboard (Analytics → Event Manager) with the same names and parameters.
    /// </summary>
    public static class Analytics
    {
        static bool ready, starting, hooked;
        static int errorsSent;
        const int MaxErrorsPerSession = 10;

        public static async void Start()
        {
            HookErrors();
            if (ready || starting || Automation.Enabled) return;
            starting = true;
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                AnalyticsService.Instance.StartDataCollection();
                ready = true;
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] not available (is the project linked to Unity Cloud?): " + e.Message); }
            starting = false;
        }

        /// <summary>The player said no (or took consent back): stop and ask the service to forget this device.</summary>
        public static void Stop(bool forget)
        {
            if (!ready) return;
            try
            {
                AnalyticsService.Instance.StopDataCollection();
                if (forget) AnalyticsService.Instance.RequestDataDeletion();
            }
            catch (Exception e) { Debug.LogWarning("[Analytics] " + e.Message); }
            ready = false;
        }

        /// <summary>One event: name, then key/value pairs (int, float, bool or string).</summary>
        public static void Event(string name, params object[] keyValues)
        {
            if (!ready) return;
            try
            {
                var e = new CustomEvent(name);
                for (int i = 0; i + 1 < keyValues.Length; i += 2) e.Add((string)keyValues[i], keyValues[i + 1]);
                AnalyticsService.Instance.RecordEvent(e);
            }
            catch (Exception ex) { Debug.LogWarning("[Analytics] " + ex.Message); }
        }

        public static void HookErrors()
        {
            if (hooked) return;
            hooked = true;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type != LogType.Exception) return;
                string where = stack != null ? stack.Split('\n')[0] : "";
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, "errors.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 64 * 1024) File.Delete(path); // keep it small
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\n  " + where + "\n");
                }
                catch (Exception) { }
                if (errorsSent++ >= MaxErrorsPerSession) return;
                Event("client_error", "message", Trim(message, 120), "where", Trim(where, 120), "version", Application.version);
            };
        }

        static string Trim(string s, int n) { return string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n); }
    }
}
