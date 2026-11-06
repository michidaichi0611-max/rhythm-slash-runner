using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Minimal tween runner on unscaled real time. UI motion only: nothing here may drive judgement or the music clock.
    /// </summary>
    public sealed class Anim : MonoBehaviour
    {
        sealed class Job { public float Start, Duration; public Action<float> Step; public Action Done; public object Owner; }

        static Anim instance;
        readonly List<Job> jobs = new List<Job>(64);
        readonly List<Job> adding = new List<Job>(16);

        static Anim Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Anim");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Anim>();
                }
                return instance;
            }
        }

        /// <summary>Runs <paramref name="step"/>(0..1) for <paramref name="duration"/> seconds. A new tween with the same owner replaces the old one.</summary>
        public static void Run(float duration, Action<float> step, Action done = null, object owner = null)
        {
            Anim a = Instance;
            if (owner != null) Cancel(owner);
            var job = new Job { Start = Time.unscaledTime, Duration = Mathf.Max(.0001f, duration), Step = step, Done = done, Owner = owner };
            a.adding.Add(job);
            step(0f);
        }

        public static void Delay(float seconds, Action done, object owner = null) { Run(seconds, _ => { }, done, owner); }

        public static void Cancel(object owner)
        {
            if (instance == null || owner == null) return;
            instance.jobs.RemoveAll(j => j.Owner == owner);
            instance.adding.RemoveAll(j => j.Owner == owner);
        }

        void Update()
        {
            if (adding.Count > 0) { jobs.AddRange(adding); adding.Clear(); }
            float now = Time.unscaledTime;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                Job j = jobs[i];
                float t = Mathf.Clamp01((now - j.Start) / j.Duration);
                try { j.Step(t); } catch (Exception e) { Debug.LogException(e); jobs.RemoveAt(i); continue; }
                if (t >= 1f)
                {
                    jobs.RemoveAt(i);
                    if (j.Done != null) { try { j.Done(); } catch (Exception e) { Debug.LogException(e); } }
                }
            }
        }

        // easing
        public static float OutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }
        public static float OutCubic(float t) { return 1 - Mathf.Pow(1 - t, 3); }
        public static float InOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1) / 2; }
        public static float Punch(float t) { return Mathf.Sin(t * Mathf.PI) * (1 - t); }
    }
}
