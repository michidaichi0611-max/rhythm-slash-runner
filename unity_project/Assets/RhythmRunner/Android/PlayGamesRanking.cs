// The plugin compiles only for Android (its sources are inside #if UNITY_ANDROID), so this file does too.
#if UNITY_ANDROID
using System;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine;
using RhythmSlashRunner.Presentation;

namespace RhythmSlashRunner.AndroidPlatform
{
    /// <summary>
    /// Round 33: the Android weekly board through Google Play Games (plugin 2.3.0, Assets/GooglePlayGames). Two things come
    /// from Google Play Console and are set by the owner: the game's Play Games setup (Window → Google Play Games → Setup →
    /// Android setup, which writes the app id) and the leaderboard id below. Until both exist the board stays off and the
    /// game shows "準備中".
    /// </summary>
    public sealed class PlayGamesRanking : IRankingBackend
    {
        /// <summary>The leaderboard id from Play Console (e.g. "CgkI…EAIQAQ"); empty = not set up yet.</summary>
        public const string AndroidLeaderboardId = "";

        public bool Ready { get { return !string.IsNullOrEmpty(AndroidLeaderboardId); } }

        public void SignIn(Action<bool> done)
        {
            try { PlayGamesPlatform.Instance.Authenticate(status => done(status == SignInStatus.Success)); }
            catch (Exception e) { Debug.LogWarning("[Ranking] Play Games sign-in failed: " + e.Message); done(false); }
        }

        public void Submit(int distance)
        {
            PlayGamesPlatform.Instance.ReportScore(distance, AndroidLeaderboardId, ok => { if (!ok) Debug.LogWarning("[Ranking] score not sent"); });
        }

        public void Show() { PlayGamesPlatform.Instance.ShowLeaderboardUI(AndroidLeaderboardId); }

#if UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            var backend = new PlayGamesRanking();
            if (!backend.Ready) return; // not set up in Play Console yet: no board, no sign-in prompt
            PlayGamesPlatform.Activate();
            Ranking.Backend = backend;
        }
#endif
    }
}
#endif
