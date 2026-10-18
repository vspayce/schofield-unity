// Apple TV needs a game controller (the Siri Remote isn't enough to aim and shoot). While none is
// connected, cover the screen with a plain notice and hold the ride paused; it lifts on its own the
// moment a controller pairs.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Schofield
{
    public class ControllerGate
    {
        readonly GameObject root;
        public bool Blocking { get; private set; }

        public ControllerGate()
        {
            var canvas = UIKit.Canvas("ControllerGate", 900);
            Object.DontDestroyOnLoad(canvas.gameObject);
            root = canvas.gameObject;
            var bg = UIKit.Fill("Dim", canvas.transform).gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.03f, 0.02f, 0.92f);
            UIKit.Txt("Title", canvas.transform, new Vector2(0.5f, 0.56f), Vector2.zero, new Vector2(1200, 70), "CONNECT A GAME CONTROLLER", 44, UIKit.Gold, TextAnchor.MiddleCenter, true);
            UIKit.Txt("Body", canvas.transform, new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(1150, 110),
                "Schofield on Apple TV is played with a game controller (Xbox, PlayStation or MFi).\nPair one in Settings › Remotes and Devices › Bluetooth, and the game picks up where you left off.",
                22, UIKit.Paper, TextAnchor.MiddleCenter, true);
            root.SetActive(false);
        }

        /// a real controller: any gamepad that isn't the Siri Remote
        public static bool HasController()
        {
            foreach (var g in Gamepad.all)
            {
                string n = ((g.displayName ?? "") + " " + (g.name ?? "") + " " + (g.description.product ?? "")).ToLowerInvariant();
                if (!n.Contains("remote")) return true;
            }
            return false;
        }

        public void Update()
        {
            Blocking = GameInput.TV && !HasController();
            if (root.activeSelf != Blocking) root.SetActive(Blocking);
        }
    }
}
