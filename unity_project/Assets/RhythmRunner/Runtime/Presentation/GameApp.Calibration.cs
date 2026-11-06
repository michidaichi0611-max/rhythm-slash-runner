using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RhythmSlashRunner.Persistence;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Settings → "自動で合わせる" (owner round 10): phones play sound late by different amounts, which makes on-time
    /// taps judge late. A click plays every half second; the player taps the lower screen with it; the median offset of
    /// eight taps (measured on the same audio clock and input timestamps as the game) becomes the timing correction.
    /// </summary>
    public sealed partial class GameApp
    {
        GameObject calibrationScreen;
        Text calibrationTitle, calibrationBody, calibrationCount;
        Image calibrationPulse;
        Button calibrationRetry;
        Coroutine calibrationRoutine;
        const int CalibrationTaps = 8, CalibrationLeadIn = 4;
        const double CalibrationInterval = .5;

        void BuildCalibration()
        {
            calibrationScreen = MakeScreen("Calibration", true);
            Image dim = calibrationScreen.AddComponent<Image>();
            dim.color = new Color(.01f, .02f, .06f, .92f);
            RectTransform safe = UiKit.Rect("Safe", calibrationScreen.transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            calibrationTitle = UiKit.Label("タイミングを自動で合わせる", safe, 54, UiKit.Gold, new Vector2(.05f, .84f), new Vector2(.95f, .92f), TextAnchor.MiddleCenter, FontStyle.Bold, true);
            calibrationBody = UiKit.Label("", safe, 38, Color.white, new Vector2(.07f, .66f), new Vector2(.93f, .83f), TextAnchor.MiddleCenter, FontStyle.Normal);
            calibrationPulse = UiKit.Image("Pulse", safe, ArtLibrary.Ring, UiKit.Cyan, new Vector2(.3f, .3f), new Vector2(.7f, .52f), true);
            calibrationCount = UiKit.Label("", safe, 90, Color.white, new Vector2(.1f, .33f), new Vector2(.9f, .49f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            calibrationRetry = UiKit.Button("もう一度", safe, UiKit.Cyan, new Vector2(.1f, .14f), new Vector2(.9f, .22f), StartCalibration, 52, true);
            UiKit.Button("もどる", safe, UiKit.NavyLight, new Vector2(.25f, .04f), new Vector2(.75f, .11f), CloseCalibration, 44);
            calibrationScreen.SetActive(false);
        }

        void OpenCalibration()
        {
            if (calibrationScreen == null) BuildCalibration();
            calibrationScreen.SetActive(true);
            calibrationScreen.transform.SetAsLastSibling();
            StartCalibration();
        }

        void StartCalibration()
        {
            if (calibrationRoutine != null) StopCoroutine(calibrationRoutine);
            calibrationRoutine = StartCoroutine(Calibrate());
        }

        void CloseCalibration()
        {
            if (calibrationRoutine != null) { StopCoroutine(calibrationRoutine); calibrationRoutine = null; }
            input.Accepting = false; input.Clear();
            audioDirector.Play(Sfx.UiBack);
            calibrationScreen.SetActive(false);
            RefreshAll();
        }

        IEnumerator Calibrate()
        {
            calibrationRetry.gameObject.SetActive(false);
            calibrationBody.text = "「カッ」という音に合わせて、\n画面の下半分をタップしてください。\n（最初の4回は聞くだけ。画面は見なくてOK）";
            calibrationCount.text = "";
            var offsets = new List<double>();
            input.Clear();
            input.Playfield = new Rect(0, 0, Screen.width, Screen.height);
            input.Accepting = true;
            double start = AudioSettings.dspTime + 1.0;
            int scheduled = 0, clicks = CalibrationLeadIn + CalibrationTaps + 6;
            while (true)
            {
                double now = AudioSettings.dspTime;
                while (scheduled < clicks && start + scheduled * CalibrationInterval < now + .25)
                {
                    audioDirector.PlayAt(Sfx.Cue, start + scheduled * CalibrationInterval);
                    scheduled++;
                }
                LaneTap tap;
                while (input.TryDequeue(out tap))
                {
                    int k = (int)System.Math.Round((tap.Dsp - start) / CalibrationInterval);
                    double diff = tap.Dsp - OutputLatency - (start + k * CalibrationInterval); // the buffer delay is compensated already
                    if (k >= CalibrationLeadIn && k < clicks && System.Math.Abs(diff) < .25) offsets.Add(diff * 1000.0);
                }
                // the ring only breathes softly: the ear, not the eye, should lead the taps
                float phase = (float)((now - start) / CalibrationInterval);
                float pulse = phase >= 0 ? Mathf.Exp(-(phase - Mathf.Floor(phase)) * 6f) : 0f;
                calibrationPulse.rectTransform.localScale = Vector3.one * (1f + .08f * pulse);
                calibrationPulse.color = new Color(UiKit.Cyan.r, UiKit.Cyan.g, UiKit.Cyan.b, .35f + .3f * pulse);
                int heard = Mathf.Clamp(Mathf.FloorToInt(phase) + 1, 0, CalibrationLeadIn);
                calibrationCount.text = phase < CalibrationLeadIn ? (phase < 0 ? "" : "聞いて… " + heard) : "あと " + Mathf.Max(0, CalibrationTaps - offsets.Count) + " 回";
                if (offsets.Count >= CalibrationTaps) break;
                if (scheduled >= clicks && now > start + clicks * CalibrationInterval + .4) break;
                yield return null;
            }
            input.Accepting = false; input.Clear();
            calibrationRetry.gameObject.SetActive(true);
            if (offsets.Count < CalibrationTaps / 2)
            {
                calibrationCount.text = "";
                calibrationBody.text = "うまく測れませんでした。\n音に合わせて、もう一度タップしてください。";
                yield break;
            }
            offsets.Sort();
            double median = offsets[offsets.Count / 2];
            int ms = Mathf.Clamp(Mathf.RoundToInt((float)median / 10f) * 10, -250, 250);
            profile.calibrationMs = ms;
            ProfileStore.TrySave(profile);
            audioDirector.Play(Sfx.UiConfirm);
            calibrationCount.text = (ms > 0 ? "+" : "") + ms + " ms";
            calibrationBody.text = "タイミング補正を " + (ms > 0 ? "+" : "") + ms + "ms にしました。\n（設定の「タイミング補正」でいつでも変えられます）";
        }
    }
}
