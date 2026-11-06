using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Presentation
{
    public enum Stem { Base = 0, Drums = 1, Bass = 2, Lead = 3, Fever = 4 }

    public enum Sfx
    {
        // timing (cue band)
        Cue, OffbeatTick, CountHigh, CountLow,
        // slicing
        MatSoft, MatJuicy, MatWood, SliceTone, PerfectShimmer, Miss, ComboBreak,
        // decorations
        WeaponEnergy, WeaponWood, WeaponBanana,
        // rewards / stingers
        Coin, CoinBurst, Jara, Kyuin, FeverEnter, FeverSweep, FeverExit, Combo10, Combo25, Fanfare, NewRecord, Purchase, EndChord,
        ResultTick, ResultJan,
        // ui
        UiSelect, UiConfirm, UiBack, UiDenied, LoadDone,
        // round 2: more materials, hold / mash, boss fights, stage changes
        MatCrystal, MatMetal, HoldTick, HoldDone, HoldBreak, MashHit, MashBreak, BossRoar, BossHit, BossDefeat, BossEscape, Heal, SpeedUp, GateWhoosh,
        // round 3: speed, items, boss gimmicks
        WindLoop, PassWhoosh, ItemGet, BoostStart, DroneSlash, BarrierUp, BarrierBreak, ChargeWarn, GuardHit, Critical, RageRoar,
        // round 4: boss entrance, boss voice, rush
        BossDrumRoll, BossGrunt, RushSlash,
        // round 9: a cut sound per kind of object, the boss shattering
        MatSplash, MatCrisp, MatBread, MatSqueak, MatSpring, MatDice, BossShatter,
        // round 11: slides
        MatSlurp, MatClack,
        // round 17: recorded+layered cut sounds (Resources/Audio/Sfx, tools/audio/build_cut_sfx.py), hold chopping, soldier parries
        Blade, ChopCrisp, ChopWood, ChopBread, ChopNoodle, ChopBlade, ParryClang, ParryFinish,
        // round 22: new weapons' swing sounds (recorded files; synthesised stand-ins below)
        WeaponRoller, WeaponSaibashi, WeaponIce, WeaponLasso, WeaponCardboard, WeaponSmoke,
        // round 27: the giant item, bombs
        GiantStomp, GiantBeep, GiantPop, BombFuse, BombBoom, BombDodge,
        // legacy names kept for callers
        SliceSoft = MatSoft, SliceJuicy = MatJuicy, SliceWood = MatWood, PerfectShine = PerfectShimmer, UiTap = UiSelect,
    }

    /// <summary>
    /// Pure C# PCM synthesis (thread-safe: System.Math only). Recipes follow review/round0/committee_sound.md:
    /// seamless loops (writes wrap around), saturated bass/kick with click so phone speakers reproduce them,
    /// cue/tick use unpitched timbres in a band no other sound occupies.
    /// </summary>
    public static partial class SynthBank
    {
        /// <summary>Loop length of the pre-round-6 music (kept for callers that phase-align to 8-beat chunks).</summary>
        public const int LoopBeats = 8;
        public const double Bpm = 120;

        // Stems can be rendered at any stage tempo (one worker thread per tempo), so the beat length is per thread.
        [ThreadStatic] static double stemSpb;
        static double Spb { get { return stemSpb > 0 ? stemSpb : 60.0 / Bpm; } }

        // C major, two bars: C - Am - F - G (two beats each)
        static readonly double[][] Chords =
        {
            new[] { 261.63, 329.63, 392.00 }, new[] { 220.00, 261.63, 329.63 }, new[] { 174.61, 220.00, 261.63 }, new[] { 196.00, 246.94, 293.66 },
        };
        static readonly double[] Roots = { 130.81, 110.00, 87.31, 98.00 };

        public static float[] BuildSfx(Sfx id, int rate)
        {
            var rng = new Rng(97 + (int)id * 7919);
            switch (id)
            {
                // round 17: these normally come from recordings; the synthesised stand-ins only play if a file is missing
                case Sfx.Blade: return BuildSfx(Sfx.MatSoft, rate);
                case Sfx.ChopCrisp: case Sfx.ChopWood: case Sfx.ChopBread: case Sfx.ChopNoodle: case Sfx.ChopBlade: return BuildSfx(Sfx.HoldTick, rate);
                case Sfx.ParryClang: case Sfx.ParryFinish: return BuildSfx(Sfx.MatMetal, rate);
                case Sfx.WeaponRoller: case Sfx.WeaponLasso: case Sfx.WeaponCardboard: case Sfx.WeaponSmoke: return BuildSfx(Sfx.WeaponBanana, rate);
                case Sfx.WeaponSaibashi: return BuildSfx(Sfx.WeaponWood, rate);
                case Sfx.WeaponIce: return BuildSfx(Sfx.WeaponEnergy, rate);
                case Sfx.Cue: // clave: band-limited noise + inharmonic partials (no other sound uses this timbre)
                    return Render(rate, .05, (t, r) => (Bp(r, ref rng) * .6 + Math.Sin(2 * Math.PI * 2200 * t) * .5 + Math.Sin(2 * Math.PI * 3400 * t) * .35) * Env(t, .0005, .012) * .55);
                case Sfx.OffbeatTick: // offbeat guide "chit": closed-hat noise + 4.8 kHz pip (round 2: louder and longer so runs of offbeats read)
                    return Render(rate, .06, (t, r) => (Math.Sin(2 * Math.PI * 4800 * t) * .6 + Hp(r, ref rng) * .55) * Env(t, .0003, .012) * .6);
                case Sfx.CountHigh:
                    return Render(rate, .25, (t, r) => (Math.Sin(2 * Math.PI * 1046.5 * t) + .3 * Math.Sin(2 * Math.PI * 2093 * t)) * Env(t, .002, .07) * .45);
                case Sfx.CountLow:
                    return Render(rate, .25, (t, r) => (Math.Sin(2 * Math.PI * 784 * t) + .3 * Math.Sin(2 * Math.PI * 1568 * t)) * Env(t, .002, .07) * .4);
                case Sfx.MatSoft:
                    return Render(rate, .12, (t, r) => Hp(r, ref rng) * Env(t, .001, .012) * .5 + Math.Sin(2 * Math.PI * (1200 + 1200 * Math.Min(1, t / .03)) * t) * Env(t, .001, .015) * .12);
                case Sfx.MatJuicy:
                    return Render(rate, .2, (t, r) => Bp(r, ref rng) * Env(t, .002, .03) * .55 + Math.Sin(2 * Math.PI * 120 * t) * Env(t, .001, .03) * .35);
                case Sfx.MatWood: // tuned wood block (modal)
                    return Render(rate, .2, (t, r) => (Math.Sin(2 * Math.PI * 520 * t) * Env(t, .0005, .04) + .5 * Math.Sin(2 * Math.PI * 520 * 2.67 * t) * Env(t, .0005, .02) + .3 * Math.Sin(2 * Math.PI * 520 * 4.1 * t) * Env(t, .0005, .01)) * .45 + Noise(ref rng) * Env(t, .0002, .003) * .3);
                case Sfx.SliceTone: // glass marimba at C5, pitched up the pentatonic ladder by AudioSource.pitch
                    return Render(rate, .35, (t, r) =>
                    {
                        double f = 523.25, mod = 2 * Math.Exp(-t / .05);
                        return (Math.Sin(2 * Math.PI * f * t + mod * Math.Sin(2 * Math.PI * f * 3.5 * t)) * Env(t, .001, .07) + .25 * Math.Sin(2 * Math.PI * f * 4 * t) * Env(t, .001, .015)) * .42;
                    });
                case Sfx.PerfectShimmer:
                    return Render(rate, .45, (t, r) => (Math.Sin(2 * Math.PI * 1318.5 * t) + Math.Sin(2 * Math.PI * 1568 * 1.015 * t) * .7 + Math.Sin(2 * Math.PI * 5274 * .985 * t) * .35) * Env(t, .002, .15) * .14 + Hp(r, ref rng) * Env(t, .001, .03) * .06);
                case Sfx.Miss:
                    return Render(rate, .25, (t, r) => Bp(r, ref rng) * Math.Sin(Math.PI * Math.Min(1, t / .2)) * .25 + Math.Sin(2 * Math.PI * 200 * t) * Env(t, .001, .03) * .3);
                case Sfx.ComboBreak:
                    return Render(rate, .3, (t, r) => Lp(r, ref rng, 4000 - 3700 * Math.Min(1, t / .25)) * Env(t, .005, .12) * .4);
                case Sfx.WeaponEnergy:
                {
                    double ph = 0;
                    return Render(rate, .2, (t, r) => { ph += 2 * Math.PI * (900 + 1400 * t + 60 * Math.Sin(t * 180)) / rate; return Math.Sin(ph) * Env(t, .002, .05) * .2; });
                }
                case Sfx.WeaponWood:
                    return Render(rate, .15, (t, r) => Math.Sin(2 * Math.PI * 210 * t) * Env(t, .001, .025) * .35 + Noise(ref rng) * Env(t, .0002, .005) * .2);
                case Sfx.WeaponBanana:
                    return Render(rate, .3, (t, r) => Math.Sin(2 * Math.PI * (260 + 90 * Math.Sin(t * 50)) * t) * Env(t, .003, .08) * .3);
                case Sfx.Coin: // A6 → E7 with metallic partial
                    return Render(rate, .3, (t, r) => { double f = t < .04 ? 1760 : 2637; return (Math.Sin(2 * Math.PI * f * t) + .3 * Math.Sin(2 * Math.PI * f * 3 * t) + .2 * Math.Sin(2 * Math.PI * f * 2.76 * t)) * Env(t, .001, .12) * .32; });
                case Sfx.CoinBurst:
                    return Grains(rate, .32, 4, .12, ref rng, .3);
                case Sfx.Jara:
                    return Grains(rate, 1.5, 40, 1.2, ref rng, .26);
                case Sfx.Kyuin: // jackpot tease: glide 880→2637 Hz then vibrato (phase accumulated for a clean glide)
                {
                    double ph = 0;
                    return Render(rate, .85, (t, r) =>
                    {
                        double f = t < .35 ? 880 * Math.Pow(2637 / 880.0, t / .35) : 2637 * Math.Pow(2, .025 * Math.Sin(2 * Math.PI * 6.5 * t));
                        ph += 2 * Math.PI * f / rate;
                        double env = Math.Min(1, t / .02) * (t > .65 ? Math.Max(0, 1 - (t - .65) / .2) : 1);
                        return (Math.Sin(ph) * .5 + Math.Sign(Math.Sin(ph)) * .18) * env * .35;
                    });
                }
                case Sfx.FeverEnter:
                    return Render(rate, 1.2, (t, r) =>
                    {
                        double sub = Math.Tanh(2 * Math.Sin(2 * Math.PI * (110 - 70 * Math.Min(1, t / .3)) * t)) * Env(t, .002, .25) * .55;
                        double crash = Hp(r, ref rng) * Env(t, .001, .6) * .3;
                        double chord = 0;
                        foreach (double f in new[] { 261.63, 329.63, 392.0, 523.25, 659.25 }) chord += Saw(f * 1.006, t) + Saw(f * .994, t);
                        return sub + crash + chord * Env(t, .005, .5) * .045;
                    });
                case Sfx.FeverSweep: // pentatonic gliss C6→C7
                    return Render(rate, .45, (t, r) => { int step = Math.Min(7, (int)(t / .025)); double[] p = { 1046.5, 1174.7, 1318.5, 1568, 1760, 1568, 1760, 2637 }; double tt = t - step * .025; return Math.Sin(2 * Math.PI * p[step] * tt) * Env(tt, .001, .08) * .25; });
                case Sfx.FeverExit:
                {
                    double ph = 0;
                    return Render(rate, .5, (t, r) => { ph += 2 * Math.PI * 1600 * Math.Pow(200 / 1600.0, Math.Min(1, t / .4)) / rate; return Math.Sin(ph) * Env(t, .005, .15) * .3; });
                }
                case Sfx.Combo10:
                    return Arp(rate, new[] { 1046.5, 1318.5, 1568.0 }, .05, .15, .3);
                case Sfx.Combo25:
                    return Arp(rate, new[] { 1046.5, 1318.5, 1568.0, 2093.0 }, .05, .15, .32);
                case Sfx.Fanfare:
                    return Render(rate, 1.1, (t, r) =>
                    {
                        double[] n = { 523.25, 659.25, 784.0, 1046.5 };
                        int k = Math.Min(3, (int)(t / .09));
                        double v = 0;
                        for (int j = 0; j <= k; j++) { double tj = t - j * .09; v += (Saw(n[j], tj) * .5 + Math.Sin(2 * Math.PI * n[j] * tj)) * Env(tj, .004, j == 3 ? .6 : .15); }
                        return v * .14;
                    });
                case Sfx.NewRecord:
                    return Render(rate, 1.8, (t, r) =>
                    {
                        double[] n = { 523.25, 659.25, 784.0, 1046.5 };
                        double v = 0;
                        for (int j = 0; j < 4; j++) { double tj = t - j * .12; if (tj >= 0) v += (Math.Sin(2 * Math.PI * n[j] * tj) + .3 * Math.Sin(4 * Math.PI * n[j] * tj)) * Env(tj, .003, j == 3 ? .8 : .2); }
                        foreach (double f in new[] { 523.25, 659.25, 784.0, 987.77, 1174.7 }) v += Math.Sin(2 * Math.PI * f * t) * Env(t - .4, .05, .8) * .25 * (t > .4 ? 1 : 0);
                        return v * .16;
                    });
                case Sfx.Purchase: // register "kachak" + coin + low thump
                    return Render(rate, .55, (t, r) => Bp(r, ref rng) * (Env(t, .0005, .015) + Env(t - .03, .0005, .015) * (t > .03 ? 1 : 0)) * .4 + Math.Sin(2 * Math.PI * 150 * t) * Env(t, .001, .06) * .4 + (t > .08 ? Math.Sin(2 * Math.PI * 2637 * (t - .08)) * Env(t - .08, .001, .12) * .25 : 0));
                case Sfx.EndChord:
                    return Render(rate, 1.4, (t, r) => { double v = 0; foreach (double f in new[] { 261.63, 329.63, 392.0, 523.25 }) v += Saw(f, t) * .5 + Math.Sin(2 * Math.PI * f * t); return v * Env(t, .01, .6) * .07; });
                case Sfx.ResultTick:
                    return Render(rate, .02, (t, r) => Math.Sign(Math.Sin(2 * Math.PI * 3000 * t)) * Env(t, .0003, .004) * .18);
                case Sfx.ResultJan:
                    return Render(rate, .9, (t, r) => { double v = 0; foreach (double f in new[] { 523.25, 659.25, 784.0, 1046.5 }) v += Saw(f, t); return v * Env(t, .003, .4) * .07 + Hp(r, ref rng) * Env(t, .001, .5) * .2; });
                case Sfx.UiSelect:
                    return Render(rate, .05, (t, r) => (Math.Sin(2 * Math.PI * 1760 * t) + .3 * Math.Sin(2 * Math.PI * 3520 * t)) * Env(t, .001, .015) * .3);
                case Sfx.UiConfirm:
                    return Render(rate, .14, (t, r) => Math.Sin(2 * Math.PI * (t < .035 ? 1318.5 : 1760) * t) * Env(t < .035 ? t : t - .035, .001, .04) * .32);
                case Sfx.UiBack:
                    return Render(rate, .12, (t, r) => Math.Sin(2 * Math.PI * (t < .035 ? 1760 : 1318.5) * t) * Env(t < .035 ? t : t - .035, .001, .04) * .26);
                case Sfx.UiDenied:
                    return Render(rate, .2, (t, r) => Tri(330, t) * (Env(t, .002, .04) + (t > .06 ? Env(t - .06, .002, .04) : 0)) * .3);
                case Sfx.LoadDone:
                    return Render(rate, .35, (t, r) => Math.Sin(2 * Math.PI * (t < .1 ? 1046.5 : 1568) * t + 1.4 * Math.Sin(2 * Math.PI * 1465 * t)) * Env(t, .002, .2) * .25);
                // ---- round 2 (partials keep clear of the 2.2 / 3.4 kHz cue band)
                case Sfx.MatCrystal: // glassy "kiin": inharmonic high partials
                    return Render(rate, .45, (t, r) => (Math.Sin(2 * Math.PI * 1318.5 * t) * Env(t, .001, .12) + .6 * Math.Sin(2 * Math.PI * 2793 * t) * Env(t, .001, .08) + .4 * Math.Sin(2 * Math.PI * 4186 * t) * Env(t, .001, .05) + .25 * Math.Sin(2 * Math.PI * 5587 * t) * Env(t, .001, .03)) * .3 + Hp(r, ref rng) * Env(t, .0005, .004) * .2);
                case Sfx.MatMetal: // metallic clank
                    return Render(rate, .3, (t, r) => (Math.Sin(2 * Math.PI * 523 * t) + .7 * Math.Sin(2 * Math.PI * 1397 * t) + .5 * Math.Sin(2 * Math.PI * 1789 * t)) * Env(t, .0005, .05) * .28 + Bp(r, ref rng) * Env(t, .0003, .01) * .5);
                case Sfx.HoldTick: // small crunchy "shk" while holding
                    return Render(rate, .05, (t, r) => Hp(r, ref rng) * Env(t, .0005, .008) * .35 + Math.Sin(2 * Math.PI * 900 * t) * Env(t, .0005, .006) * .1);
                case Sfx.HoldDone:
                    return Arp(rate, new[] { 784.0, 1046.5, 1318.5, 1568.0 }, .035, .12, .3);
                case Sfx.HoldBreak:
                {
                    double ph = 0;
                    return Render(rate, .3, (t, r) => { ph += 2 * Math.PI * (700 - 450 * Math.Min(1, t / .25)) / rate; return Math.Sin(ph) * Env(t, .003, .1) * .3; });
                }
                case Sfx.MashHit: // punchy thud with a crack on top
                    return Render(rate, .18, (t, r) => Math.Tanh(2.5 * Math.Sin(2 * Math.PI * (90 + 120 * Math.Exp(-t / .02)) * t)) * Env(t, .001, .05) * .45 + Bp(r, ref rng) * Env(t, .0005, .012) * .45);
                case Sfx.MashBreak: // the object bursts open
                    return Render(rate, 1.0, (t, r) => Lp(r, ref rng, 5000 - 4200 * Math.Min(1, t / .6)) * Env(t, .001, .25) * .55 + Math.Tanh(3 * Math.Sin(2 * Math.PI * (60 + 90 * Math.Exp(-t / .05)) * t)) * Env(t, .001, .2) * .5);
                case Sfx.BossRoar: // low growl with wobble
                    return Render(rate, 1.1, (t, r) => (Math.Tanh(2.5 * Saw(62 + 6 * Math.Sin(2 * Math.PI * 7 * t), t)) * .5 + Lp(r, ref rng, 900) * .5) * Math.Min(1, t / .08) * Math.Max(0, 1 - t / 1.1) * .5);
                case Sfx.BossHit:
                    return Render(rate, .35, (t, r) => Bp(r, ref rng) * Env(t, .0005, .03) * .6 + Math.Tanh(2 * Math.Sin(2 * Math.PI * (320 - 180 * Math.Min(1, t / .15)) * t)) * Env(t, .001, .08) * .4);
                case Sfx.BossDefeat: // explosion + victory chord
                    return Render(rate, 2.2, (t, r) =>
                    {
                        double boom = Lp(r, ref rng, 3000 - 2600 * Math.Min(1, t / 1.2)) * Env(t, .001, .5) * .6 + Math.Tanh(3 * Math.Sin(2 * Math.PI * (45 + 80 * Math.Exp(-t / .08)) * t)) * Env(t, .001, .35) * .6;
                        double chord = 0;
                        if (t > .45) foreach (double f in new[] { 523.25, 659.25, 784.0, 1046.5 }) chord += Saw(f * 1.004, t) + Saw(f * .996, t);
                        return boom + chord * Env(t - .45, .01, .7) * .035;
                    });
                case Sfx.BossEscape: // comedic slide whistle down
                {
                    double ph = 0;
                    return Render(rate, .8, (t, r) => { ph += 2 * Math.PI * 1300 * Math.Pow(300 / 1300.0, Math.Min(1, t / .7)) / rate; return Math.Sin(ph) * Math.Min(1, t / .02) * Math.Max(0, 1 - t / .8) * .3; });
                }
                case Sfx.Heal:
                    return Arp(rate, new[] { 1046.5, 1318.5, 1568.0, 1760.0 }, .06, .25, .26);
                case Sfx.SpeedUp: // riser into a hit
                {
                    double ph = 0;
                    return Render(rate, 1.2, (t, r) =>
                    {
                        double rise = t < .8 ? t / .8 : 1;
                        ph += 2 * Math.PI * (220 * Math.Pow(4, rise)) / rate;
                        double sweep = (Math.Sin(ph) * .3 + Hp(r, ref rng) * .4 * rise) * (t < .8 ? rise : Math.Exp(-(t - .8) / .05));
                        double hit = t >= .8 ? (Math.Tanh(3 * Math.Sin(2 * Math.PI * (55 + 100 * Math.Exp(-(t - .8) / .04)) * (t - .8))) * .6 + Lp(r, ref rng, 6000) * .3) * Math.Exp(-(t - .8) / .18) : 0;
                        return (sweep * .5 + hit) * .8;
                    });
                }
                case Sfx.WindLoop: // 2 s of rushing air, crossfaded into itself so it loops without a seam
                {
                    int n = 2 * rate, m = rate / 5;
                    double lp = 0, lp2 = 0;
                    var a = new double[n + m];
                    for (int i = 0; i < a.Length; i++)
                    {
                        double t = (double)i / rate, x = Noise(ref rng);
                        double cut = .05 + .03 * Math.Sin(2 * Math.PI * .5 * t) + .02 * Math.Sin(2 * Math.PI * 1.5 * t + 1);
                        lp += (x - lp) * cut; lp2 += (lp - lp2) * .5;
                        a[i] = (lp - lp2 * .3) * 1.4;
                    }
                    var loop = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        double v = i < m ? a[i] * ((double)i / m) + a[n + i] * (1 - (double)i / m) : a[i];
                        loop[i] = (float)Math.Max(-1, Math.Min(1, v));
                    }
                    return loop;
                }
                case Sfx.PassWhoosh: // something rushing past the ear
                    return Render(rate, .35, (t, r) => Lp(r, ref rng, 900 + 5000 * Math.Sin(Math.PI * Math.Min(1, t / .35))) * Math.Pow(Math.Sin(Math.PI * Math.Min(1, t / .35)), 2) * .55);
                case Sfx.ItemGet: // bright "pyururin" up-sweep with sparkle
                    return Render(rate, .55, (t, r) =>
                    {
                        double[] n = { 1046.5, 1318.5, 1568.0, 2093.0 * .84, 2637.0 };
                        int k = Math.Min(4, (int)(t / .05));
                        double tk = t - k * .05;
                        return (Math.Sin(2 * Math.PI * n[k] * tk) + .3 * Math.Sin(4 * Math.PI * n[k] * tk)) * Env(tk, .002, .12) * .3 + Hp(r, ref rng) * Env(t, .001, .2) * .05;
                    });
                case Sfx.BoostStart: // ignition + jet roar
                    return Render(rate, 1.0, (t, r) => Lp(r, ref rng, 1500 + 3000 * Math.Min(1, t / .3)) * Math.Min(1, t / .03) * Math.Exp(-t / .6) * .6 + Math.Tanh(2 * Math.Sin(2 * Math.PI * (70 + 60 * Math.Min(1, t / .4)) * t)) * Env(t, .002, .3) * .35);
                case Sfx.DroneSlash: // quick electric zip
                {
                    double ph = 0;
                    return Render(rate, .16, (t, r) => { ph += 2 * Math.PI * (1500 + 4000 * t) / rate; return (Math.Sign(Math.Sin(ph)) * .15 + Hp(r, ref rng) * .3) * Env(t, .001, .04); });
                }
                case Sfx.BarrierUp:
                    return Render(rate, .6, (t, r) => (Math.Sin(2 * Math.PI * 784 * t) + Math.Sin(2 * Math.PI * 1175 * 1.003 * t) * .7 + Math.Sin(2 * Math.PI * 1568 * t) * .5) * Math.Min(1, t / .05) * Math.Exp(-t / .25) * .18);
                case Sfx.BarrierBreak: // glass pop
                    return Render(rate, .5, (t, r) => Hp(r, ref rng) * Env(t, .0005, .06) * .5 + (Math.Sin(2 * Math.PI * 1760 * t) + Math.Sin(2 * Math.PI * 2793 * t) * .6) * Env(t, .001, .09) * .2);
                case Sfx.ChargeWarn: // two-tone alarm that climbs
                    return Render(rate, 1.0, (t, r) => Math.Sign(Math.Sin(2 * Math.PI * ((int)(t / .125) % 2 == 0 ? 660 : 880) * (1 + t * .5) * t)) * Math.Min(1, t / .01) * Math.Max(0, 1 - t / 1.0) * .14);
                case Sfx.GuardHit: // huge clang + counter swoosh
                    return Render(rate, .9, (t, r) => (Math.Sin(2 * Math.PI * 330 * t) + .7 * Math.Sin(2 * Math.PI * 879 * t) + .5 * Math.Sin(2 * Math.PI * 1397 * t)) * Env(t, .0005, .2) * .25 + Bp(r, ref rng) * Env(t, .0005, .05) * .5 + Math.Tanh(3 * Math.Sin(2 * Math.PI * (50 + 80 * Math.Exp(-t / .06)) * t)) * Env(t, .001, .25) * .4);
                case Sfx.Critical: // hit-stop "zudon"
                    return Render(rate, .7, (t, r) => Math.Tanh(4 * Math.Sin(2 * Math.PI * (40 + 120 * Math.Exp(-t / .05)) * t)) * Env(t, .001, .3) * .55 + Lp(r, ref rng, 6000 - 5000 * Math.Min(1, t / .4)) * Env(t, .0005, .15) * .45);
                case Sfx.RageRoar: // lower, longer roar with distortion
                    return Render(rate, 1.6, (t, r) => (Math.Tanh(4 * Saw(48 + 10 * Math.Sin(2 * Math.PI * 9 * t), t)) * .55 + Lp(r, ref rng, 1400) * .55) * Math.Min(1, t / .1) * Math.Max(0, 1 - t / 1.6) * .55);
                case Sfx.BossDrumRoll: // snare roll swelling into a crash (boss entrance)
                    return Render(rate, 1.6, (t, r) =>
                    {
                        double roll = t < 1.3 ? Bp(r, ref rng) * (.2 + .6 * t / 1.3) * (.6 + .4 * Math.Abs(Math.Sin(Math.PI * t * (12 + 20 * t)))) : 0;
                        double crash = t >= 1.3 ? (Hp(r, ref rng) * .6 + Math.Tanh(3 * Math.Sin(2 * Math.PI * (50 + 70 * Math.Exp(-(t - 1.3) / .05)) * (t - 1.3))) * .6) * Math.Exp(-(t - 1.3) / .25) : 0;
                        return (roll + crash) * .7;
                    });
                case Sfx.BossGrunt: // low formant "ngh"
                    return Render(rate, .35, (t, r) => (Saw(95 - 30 * t, t) * (Math.Sin(2 * Math.PI * 600 * t) * .5 + .5) * .6 + Lp(r, ref rng, 800) * .3) * Math.Min(1, t / .02) * Math.Exp(-t / .12) * .7);
                case Sfx.RushSlash: // quick bright "jak"
                    return Render(rate, .12, (t, r) => Hp(r, ref rng) * Env(t, .0005, .02) * .55 + Math.Sin(2 * Math.PI * (1600 - 6000 * t) * t) * Env(t, .001, .025) * .3);
                // ---- round 27: giant and bombs
                case Sfx.GiantStomp: // a heavy "dosun": low thump that drops in pitch, a little dust
                    return Render(rate, .45, (t, r) => Math.Tanh(3 * Math.Sin(2 * Math.PI * (38 + 70 * Math.Exp(-t / .04)) * t)) * Env(t, .002, .18) * .6 + Lp(r, ref rng, 700) * Env(t, .002, .07) * .35);
                case Sfx.GiantBeep: // "pi": a short square beep, the last-beats warning
                    return Render(rate, .14, (t, r) => Math.Sign(Math.Sin(2 * Math.PI * 1320 * t)) * Math.Min(1, t / .004) * Math.Max(0, 1 - t / .12) * .16);
                case Sfx.GiantPop: // "pon": a cork pop and a puff of air, falling back to size
                    return Render(rate, .5, (t, r) => Math.Sin(2 * Math.PI * (520 - 380 * Math.Min(1, t / .12)) * t) * Env(t, .001, .08) * .45 + Lp(r, ref rng, 2500 - 2000 * Math.Min(1, t / .3)) * Env(t, .003, .18) * .35);
                case Sfx.BombFuse: // "chiri-chiri": a crackling fuse
                    return Render(rate, .5, (t, r) => Hp(r, ref rng) * (Math.Sin(2 * Math.PI * 37 * t) > .3 ? 1 : .2) * Math.Min(1, t / .02) * Math.Max(0, 1 - t / .5) * .4);
                case Sfx.BombBoom: // "dokan": a crack, a deep boom and a long rumble
                    return Render(rate, 1.2, (t, r) => Math.Tanh(4 * Math.Sin(2 * Math.PI * (30 + 90 * Math.Exp(-t / .05)) * t)) * Env(t, .001, .35) * .6 + Lp(r, ref rng, 4000 - 3600 * Math.Min(1, t / .5)) * Env(t, .0005, .4) * .55);
                case Sfx.BombDodge: // "hyun": a quick whoosh past the ear
                    return Render(rate, .3, (t, r) => Bp(r, ref rng) * Math.Pow(Math.Sin(Math.PI * Math.Min(1, t / .3)), 2) * .4 + Math.Sin(2 * Math.PI * (900 + 900 * t) * t) * Env(t, .01, .08) * .1);
                // ---- round 9: what you cut sounds like what it is (layered over the common blade sound)
                case Sfx.MatSplash: // fruit: a wet "jubah" — muffled splash, a bubble, two droplets
                    return Render(rate, .32, (t, r) =>
                        Lp(r, ref rng, 2600 - 2200 * Math.Min(1, t / .12)) * Env(t, .002, .06) * .8
                        + Math.Sin(2 * Math.PI * (180 + 700 * Math.Exp(-t / .03)) * t) * Env(t, .001, .05) * .3
                        + Plip(t - .08, 1250) * .22 + Plip(t - .14, 980) * .16);
                case Sfx.MatCrisp: // vegetables: a crisp "shaki" — bright crackle and a snap
                    return Render(rate, .16, (t, r) => Hp(r, ref rng) * (Math.Sin(2 * Math.PI * 95 * t) > -.2 ? 1 : .25) * Env(t, .0005, .035) * .75 + Math.Sin(2 * Math.PI * 1500 * t) * Env(t, .0003, .008) * .3);
                case Sfx.MatBread: // bread: crust crackling "saku-saku" over a soft thud
                {
                    double[] at = { 0, .012, .021, .035, .047, .062 };
                    return Render(rate, .2, (t, r) =>
                    {
                        double crackle = 0;
                        foreach (double a in at) crackle += Env(t - a, .0003, .006);
                        return Hp(r, ref rng) * crackle * .5 + Math.Sin(2 * Math.PI * 160 * t) * Env(t, .002, .03) * .3;
                    });
                }
                case Sfx.MatSqueak: // rubber duck: "pyu!"
                {
                    double ph = 0;
                    return Render(rate, .2, (t, r) =>
                    {
                        ph += 2 * Math.PI * (950 + 650 * Math.Min(1, t / .1) + 40 * Math.Sin(2 * Math.PI * 30 * t)) / rate;
                        return (Math.Sin(ph) * .7 + Math.Sign(Math.Sin(ph)) * .12) * Math.Min(1, t / .008) * Math.Max(0, 1 - t / .2) * .45;
                    });
                }
                case Sfx.MatSpring: // clock: a tick and a "boing" from the spring inside
                {
                    double ph = 0;
                    return Render(rate, .45, (t, r) =>
                    {
                        ph += 2 * Math.PI * 240 * (1 + .3 * Math.Sin(2 * Math.PI * 13 * t) * Math.Exp(-t / .25)) / rate;
                        return Math.Sin(ph) * Env(t, .002, .14) * .45 + Bp(r, ref rng) * Env(t, .0003, .004) * .5;
                    });
                }
                case Sfx.MatDice: // dice: two plastic clacks
                    return Render(rate, .14, (t, r) => (Bp(r, ref rng) * .6 + Math.Sin(2 * Math.PI * 1900 * t) * .4) * (Env(t, .0003, .008) + .55 * Env(t - .055, .0003, .007)) * .7);
                case Sfx.BossShatter: // the boss bursts like glass over a deep boom
                {
                    float[] glass = Grains(rate, 1.2, 26, .45, ref rng, .5);
                    return Render(rate, 1.2, (t, r) =>
                        glass[Math.Min(glass.Length - 1, r)] + Hp(r, ref rng) * Env(t, .0005, .12) * .45
                        + Math.Tanh(3 * Math.Sin(2 * Math.PI * (42 + 90 * Math.Exp(-t / .06)) * t)) * Env(t, .001, .4) * .55);
                }
                case Sfx.MatSlurp: // noodles: a wet "zuru" — a gliding, bubbly low-passed noise
                {
                    double ph = 0;
                    return Render(rate, .26, (t, r) =>
                    {
                        ph += 2 * Math.PI * (260 + 520 * Math.Min(1, t / .18)) / rate;
                        double gurgle = .55 + .45 * Math.Sin(2 * Math.PI * 38 * t);
                        return Lp(r, ref rng, 1200 + 2400 * Math.Min(1, t / .2)) * gurgle * Math.Min(1, t / .02) * Math.Max(0, 1 - t / .26) * .8 + Math.Sin(ph) * Env(t, .01, .08) * .12;
                    });
                }
                case Sfx.MatClack: // toy train: "gatan" — two wooden knocks over a low rumble
                    return Render(rate, .22, (t, r) =>
                        (Math.Sin(2 * Math.PI * 330 * t) * Env(t, .0005, .025) + Math.Sin(2 * Math.PI * 290 * (t - .09)) * Env(t - .09, .0005, .03) * .9) * .45
                        + Bp(r, ref rng) * (Env(t, .0003, .006) + Env(t - .09, .0003, .006)) * .5 + Math.Sin(2 * Math.PI * 70 * t) * Env(t, .005, .1) * .2);
                case Sfx.GateWhoosh:
                    return Render(rate, .7, (t, r) => Lp(r, ref rng, 600 + 4000 * Math.Sin(Math.PI * Math.Min(1, t / .7))) * Math.Sin(Math.PI * Math.Min(1, t / .7)) * .5);
            }
            return new float[1];
        }

        // ---------------------------------------------------------------- building blocks

        struct Rng
        {
            uint s;
            public Rng(int seed) { s = (uint)seed * 2654435761u + 1; }
            public double Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s / (double)uint.MaxValue; }
        }

        static double Noise(ref Rng r) { return r.Next() * 2 - 1; }

        // One-pole filters keep their state in the per-sound closure through these statics-free helpers.
        sealed class FilterState { public double lp, lp2, hpPrev, hpOut; }
        [ThreadStatic] static FilterState fs;
        static FilterState F { get { return fs ?? (fs = new FilterState()); } }

        static double Hp(int i, ref Rng r) { double x = Noise(ref r); var f = F; if (i == 0) { f.hpPrev = 0; f.hpOut = 0; } f.hpOut = .7 * (f.hpOut + x - f.hpPrev); f.hpPrev = x; return f.hpOut; }
        static double Bp(int i, ref Rng r) { double x = Noise(ref r); var f = F; if (i == 0) { f.lp = 0; f.lp2 = 0; } f.lp += (x - f.lp) * .35; f.lp2 += (f.lp - f.lp2) * .2; return f.lp - f.lp2; }
        static double Lp(int i, ref Rng r, double cutoff) { double x = Noise(ref r); var f = F; if (i == 0) f.lp = 0; double a = Math.Min(1, cutoff / 8000.0); f.lp += (x - f.lp) * a; return f.lp; }

        /// <summary>A water droplet: a short sine that drops quickly in pitch (0 before it starts).</summary>
        static double Plip(double t, double f) { return t < 0 ? 0 : Math.Sin(2 * Math.PI * f * (1 + 2.5 * Math.Exp(-t / .012)) * t) * Env(t, .0005, .018); }

        static double Env(double t, double attack, double tau) { if (t < 0) return 0; return Math.Min(1, t / Math.Max(1e-5, attack)) * Math.Exp(-t / tau); }
        static double Saw(double f, double t) { double p = f * t; return 2 * (p - Math.Floor(p + .5)); }
        static double Tri(double f, double t) { return 2 * Math.Abs(Saw(f, t)) - 1; }

        static float[] Render(int rate, double seconds, Func<double, int, double> sample)
        {
            int n = (int)(seconds * rate);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = (float)Math.Max(-1, Math.Min(1, sample((double)i / rate, i)));
            return d;
        }

        static float[] Arp(int rate, double[] notes, double step, double tau, double gain)
        {
            return Render(rate, notes.Length * step + tau * 3, (t, r) =>
            {
                double v = 0;
                for (int j = 0; j < notes.Length; j++) { double tj = t - j * step; if (tj >= 0) v += Tri(notes[j], tj) * Env(tj, .002, tau); }
                return v * gain;
            });
        }

        static float[] Grains(int rate, double seconds, int count, double spread, ref Rng rng, double gain)
        {
            int n = (int)(seconds * rate);
            var d = new float[n];
            double[] pitches = { 1568, 1760, 2349, 2637 };
            for (int g = 0; g < count; g++)
            {
                double start = count == 1 ? 0 : rng.Next() * spread;
                double f = pitches[(int)(rng.Next() * pitches.Length) % pitches.Length] * (1 + (rng.Next() - .5) * .02);
                double tau = .03 + rng.Next() * .03;
                int s0 = (int)(start * rate), len = (int)(tau * 6 * rate);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    double t = (double)i / rate;
                    double v = (Math.Sin(2 * Math.PI * f * t) + .5 * Math.Sin(2 * Math.PI * f * 2.76 * t) + .25 * Math.Sin(2 * Math.PI * f * 5.4 * t)) * Math.Exp(-t / tau);
                    d[s0 + i] += (float)(v * gain * .5);
                }
            }
            double fadeOut = .3;
            for (int i = 0; i < n; i++) { double t = (double)i / rate; double env = Math.Min(1, t / .03) * Math.Min(1, (seconds - t) / fadeOut); d[i] = (float)Math.Max(-1, Math.Min(1, d[i] * env)); }
            return d;
        }

    }
}
