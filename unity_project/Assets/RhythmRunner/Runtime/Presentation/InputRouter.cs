using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    public struct LaneTap
    {
        public int Lane;
        public double Dsp;
        public double InputTime;
        /// <summary>The lane was let go (ends holds). Releases are never debounced.</summary>
        public bool Release;
    }

    /// <summary>
    /// Collects lane presses from touch, mouse and keyboard with their Input System event timestamps and converts them
    /// to DSP time through <see cref="ClockBridge"/>. The frame on which the event is processed never decides timing.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        public readonly ClockBridge Bridge = new ClockBridge();
        readonly Queue<LaneTap> pending = new Queue<LaneTap>();
        InputAction pointerPress;
        InputAction touchPress;
        InputAction keyLeft, keyCenter, keyRight, keyPause;
        double lastTapInputTime = double.NegativeInfinity;
        // which lane each pointer pressed, so its release ends the right hold (mouse = -1, touches by touchId)
        readonly Dictionary<int, int> pointerLane = new Dictionary<int, int>();
        // slides (round 11): the lane each pointer is over right now, followed while it is held down
        readonly Dictionary<int, int> pointerNow = new Dictionary<int, int>();
        readonly List<int> pointerIds = new List<int>(4);
        int lastSource = int.MinValue; // the newest press: a pointer id, or 1000 + lane for a key
        readonly int[] keysDown = new int[3];

        /// <summary>The lane under the newest finger (or key) still held; -1 when nothing is held.</summary>
        public int HeldLane
        {
            get
            {
                int lane;
                if (lastSource >= 1000) { if (keysDown[lastSource - 1000] > 0) return lastSource - 1000; }
                else if (pointerNow.TryGetValue(lastSource, out lane)) return lane;
                foreach (int l in pointerNow.Values) return l;
                for (int l = 0; l < 3; l++) if (keysDown[l] > 0) return l;
                return -1;
            }
        }

        /// <summary>The newest press came from a key (keyboards switch lanes by changing keys, which needs more slack).</summary>
        public bool LastWasKey { get { return lastSource >= 1000; } }

        public bool AnyHeld { get { return pointerNow.Count > 0 || keysDown[0] + keysDown[1] + keysDown[2] > 0; } }

        /// <summary>Screen-space rect (pixels) of the playfield; presses outside the lower tap zone are ignored.</summary>
        public Rect Playfield;
        /// <summary>Normalized (0 = bottom) height below which presses count as lane taps.</summary>
        public float TapZoneTop = .62f;
        public int DebounceMs = 100;
        public bool Accepting;
        public System.Action BackRequested;

        void OnEnable()
        {
            pointerPress = new InputAction("pointer", InputActionType.PassThrough, "<Mouse>/leftButton");
            pointerPress.performed += ctx =>
            {
                if (ctx.ReadValueAsButton()) OnPointer(-1, Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero, ctx.time);
                else OnPointerUp(-1, ctx.time);
            };
            touchPress = new InputAction("touch", InputActionType.PassThrough);
            touchPress.AddBinding("<Touchscreen>/touch*/press");
            touchPress.performed += ctx =>
            {
                var touch = ctx.control.parent as TouchControl;
                if (touch == null) return;
                int id = touch.touchId.ReadValue();
                if (ctx.ReadValueAsButton()) OnPointer(id, touch.position.ReadValue(), ctx.time);
                else OnPointerUp(id, ctx.time);
            };
            keyLeft = Key("<Keyboard>/a", "<Keyboard>/leftArrow", 0);
            keyCenter = Key("<Keyboard>/s", "<Keyboard>/downArrow", 1);
            keyRight = Key("<Keyboard>/d", "<Keyboard>/rightArrow", 2);
            // Esc is also what Android's back button / back gesture arrives as.
            keyPause = new InputAction("back", InputActionType.Button, "<Keyboard>/escape");
            keyPause.AddBinding("<Keyboard>/p");
            keyPause.performed += _ => { if (BackRequested != null) BackRequested(); };
            foreach (InputAction a in All()) a.Enable();
        }

        void OnDisable()
        {
            foreach (InputAction a in All()) { a.Disable(); a.Dispose(); }
        }

        IEnumerable<InputAction> All() { yield return pointerPress; yield return touchPress; yield return keyLeft; yield return keyCenter; yield return keyRight; yield return keyPause; }

        InputAction Key(string a, string b, int lane)
        {
            var action = new InputAction("lane" + lane, InputActionType.PassThrough, a);
            action.AddBinding(b);
            action.performed += ctx =>
            {
                if (ctx.ReadValueAsButton()) { keysDown[lane]++; lastSource = 1000 + lane; if (keysDown[lane] == 1) Enqueue(lane, ctx.time); }
                else if (keysDown[lane] > 0) { keysDown[lane]--; if (keysDown[lane] == 0) EnqueueRelease(lane, ctx.time); }
            };
            return action;
        }

        void Update()
        {
            // Pair the input timeline with the audio clock once per frame.
            Bridge.Sample(InputState.currentTime, AudioSettings.dspTime);
            // follow held fingers across the lanes (slides)
            if (pointerNow.Count == 0) return;
            pointerIds.Clear(); pointerIds.AddRange(pointerNow.Keys);
            foreach (int id in pointerIds)
            {
                if (id == -1) { if (Mouse.current != null) pointerNow[id] = LaneAt(Mouse.current.position.ReadValue().x); continue; }
                if (Touchscreen.current == null) continue;
                foreach (TouchControl t in Touchscreen.current.touches)
                    if (t.touchId.ReadValue() == id && t.press.isPressed) { pointerNow[id] = LaneAt(t.position.ReadValue().x); break; }
            }
        }

        int LaneAt(float screenX)
        {
            float x = Playfield.width > 0 ? (screenX - Playfield.xMin) / Playfield.width : .5f;
            return Mathf.Clamp((int)(x * 3f), 0, 2);
        }

        void OnPointer(int pointer, Vector2 screen, double time)
        {
            if (Playfield.width <= 0 || !Playfield.Contains(screen)) return;
            float x = (screen.x - Playfield.xMin) / Playfield.width;
            float y = (screen.y - Playfield.yMin) / Playfield.height;
            if (y > TapZoneTop) return; // HUD and pause button live above the tap zone
            int lane = Mathf.Clamp((int)(x * 3f), 0, 2);
            pointerLane[pointer] = lane;
            pointerNow[pointer] = lane; lastSource = pointer;
            Enqueue(lane, time);
        }

        void OnPointerUp(int pointer, double time)
        {
            int lane;
            if (!pointerLane.TryGetValue(pointer, out lane)) return;
            pointerLane.Remove(pointer); pointerNow.Remove(pointer);
            // another finger still on the same lane keeps the hold going
            foreach (int other in pointerLane.Values) if (other == lane) return;
            EnqueueRelease(lane, time);
        }

        void EnqueueRelease(int lane, double inputTime)
        {
            if (!Accepting) return;
            pending.Enqueue(new LaneTap { Lane = lane, InputTime = inputTime, Dsp = Bridge.ToDsp(inputTime), Release = true });
        }

        void Enqueue(int lane, double inputTime)
        {
            if (!Accepting) return;
            if ((inputTime - lastTapInputTime) * 1000.0 < DebounceMs) return;
            lastTapInputTime = inputTime;
            pending.Enqueue(new LaneTap { Lane = lane, InputTime = inputTime, Dsp = Bridge.ToDsp(inputTime) });
        }

        /// <summary>Taps in arrival (= timestamp) order since the last call.</summary>
        public bool TryDequeue(out LaneTap tap)
        {
            if (pending.Count > 0) { tap = pending.Dequeue(); return true; }
            tap = default(LaneTap);
            return false;
        }

        public void Clear() { pending.Clear(); pointerLane.Clear(); pointerNow.Clear(); lastSource = int.MinValue; for (int i = 0; i < keysDown.Length; i++) keysDown[i] = 0; }
    }
}
