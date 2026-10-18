// Unified input for the ride: mouse + keys (Mac), touch (drag anywhere to aim, plus on-screen
// buttons; dragging from FIRE also aims), and game controllers (iPhone/iPad/Mac, and Apple TV).
// Ported from core/input.js, with the controller added.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Schofield
{
    public struct Frame
    {
        public float dx, dy;                         // aim change, radians
        public bool fire, firePressed, reload, swap, deadeye, scope, pause, whip, brake, dynamite;
    }

    public class GameInput
    {
        public float sens = 1; public bool invertY;
        public bool enabled;
        public bool touch;            // a phone or tablet: show the buttons, aim assist
        public bool pad;              // last input came from a controller (aim assist, button hints)
        public static bool TV => Application.platform == RuntimePlatform.tvOS;
        float dx, dy;
        bool fireHeld, firePressed, reload, swap, deadeye, scope, pause, whipKey, brakeKey, dynamite;

        // on-screen buttons the HUD registers: role -> rect
        public readonly Dictionary<string, RectTransform> buttons = new Dictionary<string, RectTransform>();
        readonly Dictionary<int, (string role, Vector2 last)> fingers = new Dictionary<int, (string, Vector2)>();
        public readonly HashSet<string> held = new HashSet<string>();   // buttons under a finger (for the pressed look)
        // floating aim stick on a phone (left half of the screen): where the thumb landed and how far it has pushed
        public Vector2? stickOrigin; public Vector2 stickOffset; int stickId = -1;

        public GameInput()
        {
            touch = Application.isMobilePlatform && Application.platform != RuntimePlatform.tvOS;
            sens = PlayerPrefs.GetFloat("schofield.sens", 1);
            pad = TV;
            invertY = PlayerPrefs.GetInt("schofield.invertY", 0) == 1;
        }

        public void Save() { PlayerPrefs.SetFloat("schofield.sens", sens); PlayerPrefs.SetInt("schofield.invertY", invertY ? 1 : 0); }

        public void SetEnabled(bool v)
        {
            enabled = v;
            if (!v) { fireHeld = false; whipKey = brakeKey = false; fingers.Clear(); held.Clear(); stickOrigin = null; stickId = -1; Unlock(); }
        }

        public void Lock()
        {
            if (touch || Application.platform == RuntimePlatform.tvOS) return;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        public void Unlock() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        public Frame Consume()
        {
            var r = new Frame
            {
                dx = dx, dy = dy * (invertY ? -1 : 1), fire = fireHeld, firePressed = firePressed,
                reload = reload, swap = swap, deadeye = deadeye, scope = scope, pause = pause, dynamite = dynamite,
                whip = whipKey || held.Contains("whip"), brake = brakeKey || held.Contains("brake"),
            };
            dx = dy = 0;
            firePressed = reload = swap = deadeye = scope = pause = dynamite = false;
            return r;
        }

        /// poll the devices; call once per frame before Consume
        public void Poll(float rdt)
        {
            pause |= PausePressed();
            if (TV) pad = true;        // the Apple TV is played with a controller; its remote isn't a keyboard
            if (!enabled) return;
            Keys();
            MouseAim();
            Pad(rdt);
            Touches(rdt);
            ApplyStick(rdt);
            if (TV) pad = true;
        }

        static bool PausePressed()
        {
            var k = Keyboard.current;
            if (k != null && (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame)) return true;
            var g = Gamepad.current;
            return g != null && g.startButton.wasPressedThisFrame;
        }

        void Keys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.rKey.wasPressedThisFrame) reload = true;
            if (k.qKey.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame || k.digit2Key.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) swap = true;
            if (k.fKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame) scope = true;
            if (k.gKey.wasPressedThisFrame) dynamite = true;
            whipKey = k.wKey.isPressed || k.leftShiftKey.isPressed;
            brakeKey = k.sKey.isPressed || k.leftCtrlKey.isPressed;
            if (k.anyKey.wasPressedThisFrame) pad = false;
        }

        void MouseAim()
        {
            var m = Mouse.current;
            if (m == null || touch) return;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked)
            {
                if (m.leftButton.wasPressedThisFrame) Lock();
                return;
            }
            var d = m.delta.ReadValue();
            if (d.sqrMagnitude > 0) pad = false;
            dx += d.x * 0.0022f * sens;
            dy -= d.y * 0.0022f * sens;          // screen y runs up in Unity, down in the browser
            if (m.leftButton.wasPressedThisFrame) { fireHeld = true; firePressed = true; pad = false; }
            if (m.leftButton.wasReleasedThisFrame) fireHeld = false;
            if (m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) scope = true;
        }

        bool padFire;
        void Pad(float rdt)
        {
            var g = Gamepad.current;
            if (g == null) return;
            // left stick aims; right stick up whips, down reins in
            var st = g.leftStick.ReadValue();
            var ls = g.rightStick.ReadValue();
            float mag = st.magnitude;
            if (mag > 0.12f)
            {
                // response curve: fine control near the centre, fast turns at the edge
                float k = Mathf.Pow((mag - 0.12f) / 0.88f, 1.8f) / mag;
                dx += st.x * k * 2.6f * rdt * sens;
                dy -= st.y * k * 1.7f * rdt * sens;
                pad = true;
            }
            bool trig = g.rightTrigger.ReadValue() > 0.4f;
            if (trig && !padFire) { firePressed = true; pad = true; }
            if (trig) fireHeld = true; else if (padFire) fireHeld = false;
            padFire = trig;
            if (g.leftTrigger.wasPressedThisFrame) { scope = true; pad = true; }
            if (g.buttonNorth.wasPressedThisFrame) { swap = true; pad = true; }
            if (g.rightShoulder.wasPressedThisFrame) { dynamite = true; pad = true; }
            if (g.buttonWest.wasPressedThisFrame) { reload = true; pad = true; }
            if (g.dpad.left.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame) { swap = true; pad = true; }
            bool whip = g.buttonSouth.isPressed || ls.y > 0.6f, brake = g.buttonEast.isPressed || ls.y < -0.6f;
            if (whip) held.Add("padwhip"); else held.Remove("padwhip");
            if (brake) held.Add("padbrake"); else held.Remove("padbrake");
            whipKey |= whip; brakeKey |= brake;
        }

        void Touches(float rdt)
        {
            var ts = Touchscreen.current;
            if (ts == null || !touch) return;
            float pts = Mathf.Max(1, Screen.dpi > 0 ? Screen.dpi / 163f : 2);   // pixels per point
            float widthPts = Screen.width / pts;
            float k = 0.0052f * sens * (900f / Mathf.Max(600, widthPts)) / pts;
            foreach (TouchControl t in ts.touches)
            {
                int id = t.touchId.ReadValue();
                var phase = t.phase.ReadValue();
                var pos = t.position.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began && !fingers.ContainsKey(id))
                {
                    string role = "aim";
                    foreach (var kv in buttons)
                        if (kv.Value && kv.Value.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(kv.Value, pos, null)) { role = kv.Key; break; }
                    // a thumb landing on empty screen on the left half grabs the floating stick
                    if (role == "aim" && pos.x < Screen.width * 0.5f && stickId < 0) { role = "stick"; stickId = id; stickOrigin = pos; stickOffset = Vector2.zero; }
                    fingers[id] = (role, pos);
                    pad = false;
                    switch (role)
                    {
                        case "fire": fireHeld = true; firePressed = true; break;
                        case "reload": reload = true; break;
                        case "swap": swap = true; break;
                        case "deadeye": deadeye = true; break;
                        case "scope": scope = true; break;
                        case "dynamite": dynamite = true; break;
                        case "pause": pause = true; break;
                    }
                    if (role != "aim") held.Add(role);
                }
                else if (fingers.TryGetValue(id, out var f))
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    {
                        if (f.role == "stick") stickOffset = Vector2.ClampMagnitude(pos - stickOrigin.Value, StickRadiusPx);
                        else if (f.role == "aim" || f.role == "fire")
                        {
                            var d = pos - f.last;
                            dx += d.x * k; dy -= d.y * k;
                        }
                        fingers[id] = (f.role, pos);
                    }
                    else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        if (f.role == "fire") fireHeld = false;
                        if (f.role == "stick") { stickId = -1; stickOrigin = null; stickOffset = Vector2.zero; }
                        held.Remove(f.role);
                        fingers.Remove(id);
                    }
                }
            }
        }

        float StickRadiusPx => 60 * Mathf.Max(1, Screen.dpi > 0 ? Screen.dpi / 163f : 2);

        /// the floating stick turns the view like a controller stick: gentle near the centre, fast at the rim
        void ApplyStick(float rdt)
        {
            if (!stickOrigin.HasValue) return;
            var v = stickOffset / StickRadiusPx;
            float mag = v.magnitude;
            if (mag < 0.08f) return;
            float k = Mathf.Pow((mag - 0.08f) / 0.92f, 1.7f) / mag;
            dx += v.x * k * 2.4f * rdt * sens;
            dy -= v.y * k * 1.5f * rdt * sens;
        }

        /// menus: any "confirm" from keys, pad or a click
        public static bool Confirm()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (g != null && g.buttonSouth.wasPressedThisFrame);
        }
    }
}
