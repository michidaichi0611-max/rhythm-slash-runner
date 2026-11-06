using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Command-line diagnostics for the built player (never active in a normal launch):
    ///   -rsrShots &lt;dir&gt;   capture screenshots of each screen, a run, FEVER, a purchase and the result, then quit
    ///   -rsrAutoplay        bot judges every event on its beat (use with -rsrShots)
    ///   -rsrTier &lt;n&gt;     force a difficulty tier for the captured run
    /// </summary>
    public static class Automation
    {
        public static readonly string ShotDir = Arg("-rsrShots");
        public static readonly bool Autoplay = Has("-rsrAutoplay");
        public static readonly int? Tier = int.TryParse(Arg("-rsrTier"), out int t) ? t : (int?)null;
        /// <summary>-rsrStage n: chunks per stage for the captured run (reaches the boss and the speed-up sooner).</summary>
        /// <summary>-rsrItem booster|auto|barrier: every item of the captured run is this one.</summary>
        public static readonly string ForcedItem = Arg("-rsrItem");
        /// <summary>-rsrCharacter character_cat|character_robot: capture with that character equipped.</summary>
        public static readonly string Character = Arg("-rsrCharacter");
        /// <summary>-rsrWeapon weapon_lasso: capture with that weapon equipped (round 22).</summary>
        public static readonly string Weapon = Arg("-rsrWeapon");
        /// <summary>-rsrBoss boss_icecream: every stage gets this boss (to capture a new boss without luck).</summary>
        public static readonly string ForcedBoss = Arg("-rsrBoss");
        /// <summary>-rsrTheme n: every stage uses scenery n (0 kitchen … 5 freezer), to capture each stage's objects.</summary>
        public static readonly int? ForcedTheme = int.TryParse(Arg("-rsrTheme"), out int th) ? th : (int?)null;
        /// <summary>-rsrSoldier crow: soldier chunks use only this soldier (to capture a new enemy without luck).</summary>
        public static readonly string ForcedSoldier = Arg("-rsrSoldier");
        /// <summary>-rsrShotScale 2: captures at 2× the window (store screenshots at 1080×1920 from a 540×960 window).</summary>
        public static readonly int ShotScale = int.TryParse(Arg("-rsrShotScale"), out int ss) && ss > 1 ? ss : 1;
        /// <summary>-rsrLang en: play the captures in this language.</summary>
        public static readonly string Lang = Arg("-rsrLang");
        /// <summary>-rsrEnergy n: past the free days with n plays left and two drinks (to capture the plays line).</summary>
        public static readonly int? Energy = int.TryParse(Arg("-rsrEnergy"), out int en) ? en : (int?)null;
        /// <summary>-rsrPatterns split_: normal chunks use only patterns whose id starts with this (to capture a new pattern without luck).</summary>
        public static readonly string ForcedPatterns = Arg("-rsrPatterns");
        public static readonly int? StageChunks = int.TryParse(Arg("-rsrStage"), out int c) && c > 3 ? c : (int?)null;
        public static double AutoplayErrorBeats;
        public static bool Enabled { get { return ShotDir != null; } }

        static bool Has(string flag) { return Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0; }
        static string Arg(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(ShotDir);
            ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, name + ".png"), ShotScale);
            Debug.Log("[Automation] shot " + name);
            yield return null;
        }

        static IEnumerator Burst(string prefix, int count, float interval)
        {
            for (int i = 0; i < count; i++) { yield return new WaitForSecondsRealtime(interval); yield return Shot(prefix + "_" + i); }
        }

        /// <summary>Captures the first moment a condition holds during the run (holds and mashes come at random times).</summary>
        static IEnumerator WatchOnce(Func<bool> condition, string prefix)
        {
            float until = Time.realtimeSinceStartup + 240f;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            if (condition()) yield return Burst(prefix, 3, .18f);
        }

        public static IEnumerator Run(GameApp app)
        {
            yield return new WaitForSecondsRealtime(.3f);
            yield return Shot("00_splash");
            float until = Time.realtimeSinceStartup + 20f;
            while (!app.AutoBooted && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("01_home");
            foreach (string screen in new[] { "character", "weapon", "items", "special", "ranking", "event", "settings" })
            {
                app.AutoOpen(screen);
                yield return new WaitForSecondsRealtime(.45f);
                yield return Shot("02_" + screen);
            }
            // shop: grant coins, buy, capture the confirm dialog and the unboxing
            app.AutoGrantCoins(300);
            app.AutoOpen("weapon");
            yield return new WaitForSecondsRealtime(.4f);
            app.AutoBuyFirstWeapon();
            yield return new WaitForSecondsRealtime(.3f);
            yield return Shot("03_shop_confirm");
            app.AutoConfirmDialog();
            yield return Burst("04_unbox", 4, .3f);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("05_unbox_done");

            app.AutoOpen("home");
            yield return new WaitForSecondsRealtime(.3f);
            app.AutoStart(Tier);
            app.StartCoroutine(WatchOnce(() => app.AutoHolding, "22_hold"));
            app.StartCoroutine(WatchOnce(() => app.AutoSliding, "40_slide"));
            app.StartCoroutine(WatchOnce(() => app.AutoSlideComing, "41_slide_coming"));
            app.StartCoroutine(WatchOnce(() => app.AutoSoldier(false), "42_soldier"));
            app.StartCoroutine(WatchOnce(() => app.AutoSoldier(true), "43_boss_soldier"));
            app.StartCoroutine(WatchOnce(() => app.AutoMashOpen(false), "29_mash"));
            app.StartCoroutine(WatchOnce(() => app.AutoMashFull, "29_mash_full"));
            app.StartCoroutine(WatchOnce(() => app.AutoBoost, "31_boost"));
            app.StartCoroutine(WatchOnce(() => app.AutoCountdown, "36_boss_countdown"));
            app.StartCoroutine(WatchOnce(() => app.AutoTipOpen, "37_tip"));
            app.StartCoroutine(WatchOnce(() => app.AutoGiant, "32_giant"));
            app.StartCoroutine(WatchOnce(() => app.AutoGiantEnding, "32_giant_ending"));
            app.StartCoroutine(WatchOnce(() => app.AutoNear(e => e.Bomb), "44_bomb"));
            app.StartCoroutine(WatchOnce(() => app.AutoNear(e => e.Splits), "45_split"));
            app.StartCoroutine(WatchOnce(() => app.AutoNear(e => !double.IsNaN(e.SplitFromBeat)), "46_split_halves"));
            app.StartCoroutine(WatchOnce(() => app.AutoShield, "33_barrier"));
            app.StartCoroutine(WatchOnce(() => app.AutoRage, "34_rage"));
            app.StartCoroutine(WatchOnce(() => app.AutoCharging, "35_charge_guard"));
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("10_countin");
            yield return new WaitForSecondsRealtime(3.2f);
            yield return Shot("11_run_a");
            yield return Burst("12_hits", 4, .07f);
            // wait for combo milestones and FEVER (autoplay hits everything)
            until = Time.realtimeSinceStartup + 60f;
            while (app.AutoCombo < 10 && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("13_combo10", 3, .12f);
            while (app.AutoCombo < 45 && Time.realtimeSinceStartup < until) yield return null;
            yield return Shot("14_countdown");
            while (app.AutoCombo < 50 && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("15_fever_enter", 5, .15f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("16_fever");
            yield return Burst("16_fever_hits", 3, .08f);
            app.AutoPause();
            yield return new WaitForSecondsRealtime(.4f);
            yield return Shot("17_pause");
            app.AutoPauseSettings(true);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("17_pause_settings");
            app.AutoPauseSettings(false);
            yield return new WaitForSecondsRealtime(.4f);
            app.AutoResume();
            yield return new WaitForSecondsRealtime(.7f);
            yield return Shot("18_resume_count");
            yield return new WaitForSecondsRealtime(2.5f);
            // round 2: holds, boss fight, finisher, stage change
            until = Time.realtimeSinceStartup + 90f;
            while (!app.AutoHolding && !app.AutoBossVisible && Time.realtimeSinceStartup < until) yield return null;
            while (!app.AutoBossVisible && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("23_boss_warning", 3, .35f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Burst("24_boss_fight", 5, .1f);
            while (!app.AutoMashOpen(true) && !app.AutoBossResolved && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("25_finisher", 4, .2f);
            while (!app.AutoBossResolved && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("26_boss_down", 4, .15f);
            while (app.AutoStage < 1 && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("27_speedup", 5, .2f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("28_stage2");
            // stop hitting so the run ends by misses (fever end + hit poses)
            AutoplayErrorBeats = 10;
            yield return Burst("19_miss", 6, .25f);
            until = Time.realtimeSinceStartup + 60f;
            while (!app.AutoInResult && Time.realtimeSinceStartup < until) yield return null;
            yield return Burst("20_result", 6, .45f);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("21_result_final");
            app.AutoOpenCard();
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("30_result_card");
            Debug.Log("[Automation] done coins=" + app.AutoCoins);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }
    }
}
