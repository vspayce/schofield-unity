// Small helpers for building the uGUI interface in code: rects, images, text, buttons, generated
// sprites (solid, soft disc, ring), and the western palette.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Schofield
{
    public static class UIKit
    {
        public static readonly Color Paper = MathX.Hex("#f3e6c8"), Ink = MathX.Hex("#2a1b10"), Gold = MathX.Hex("#c9a04a"), Blood = MathX.Hex("#e0492f"), Dim = new Color(0, 0, 0, 0.45f);
        static Font body, title;
        public static Font Body => body ? body : body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Font Title { get { if (!title) title = Assets.Font("Rye-Regular"); return title ? title : Body; } }

        static Sprite solid, disc, ring;
        public static Sprite Solid => solid ? solid : solid = Make(4, (x, y) => 1);
        public static Sprite Disc => disc ? disc : disc = Make(64, (x, y) => Mathf.Clamp01((1 - Mathf.Sqrt(x * x + y * y)) * 8));
        public static Sprite Ring => ring ? ring : ring = Make(128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Clamp01((0.06f - Mathf.Abs(r - 0.9f)) * 40); });

        static Sprite triDown;
        /// a down-pointing marker triangle, soft-edged
        public static Sprite TriDown => triDown ? triDown : triDown = Make(64, (x, y) =>
        {
            float w = 0.92f * (y + 0.82f) / 1.64f;
            float d = Mathf.Min(0.82f - y, (w - Mathf.Abs(x)) * 0.87f);
            return Mathf.Clamp01(d * 20);
        });

        static Sprite Make(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1, 1, 1, alpha((x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1));
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);   // pos is the centre unless a pivot is given
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite, Color? color = null)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite ? sprite : Solid; im.color = color ?? Color.white; im.raycastTarget = false;
            im.preserveAspect = sprite != null && sprite != Solid;
            return im;
        }

        public static Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string s, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool titleFont = false)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = titleFont ? Title : Body; t.fontSize = fontSize; t.color = color; t.alignment = align; t.text = s;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.7f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static int lastClick = -10;

        static Sprite fadeRight;
        /// A soft shade that is darkest at the left edge and fades out to the right (behind the title menu).
        public static Sprite FadeRight()
        {
            if (fadeRight) return fadeRight;
            var t = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 128; x++) { float a = 1 - x / 127f; t.SetPixel(x, 0, new Color(1, 1, 1, a * a * (3 - 2 * a))); }
            t.Apply();
            return fadeRight = Sprite.Create(t, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f));
        }

        public static Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, System.Action onClick, int fontSize = 30)
        {
            var im = Img(name, parent, anchor, pos, size, null, new Color(0.17f, 0.11f, 0.07f, 0.92f));
            im.raycastTarget = true;
            var ol = im.gameObject.AddComponent<Outline>(); ol.effectColor = Gold; ol.effectDistance = new Vector2(2, -2);
            var b = im.gameObject.AddComponent<Button>();
            var cb = b.colors; cb.highlightedColor = new Color(1.3f, 1.15f, 0.9f); cb.selectedColor = new Color(1.4f, 1.2f, 0.9f); cb.pressedColor = new Color(0.8f, 0.7f, 0.6f); b.colors = cb;
            b.onClick.AddListener(() =>
            {
                if (Debug.isDebugBuild) Debug.Log($"[ui] click {name} frame {Time.frameCount}");
                if (Time.frameCount - lastClick < 3) return;     // one press, one click (the event system and the menu's own confirm can both fire)
                lastClick = Time.frameCount;
                Sound.I?.Play("ui_click", 0.6f); onClick();
            });
            var t = Txt("Label", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, label, fontSize, Paper, TextAnchor.MiddleCenter, true);
            return b;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // a module added at run time has no actions in a player build (only the editor fills them in), so no
            // click, tap or controller press ever reached a button: give it the default UI actions
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            Object.DontDestroyOnLoad(go);
        }

        public static Canvas Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 1;
            return c;
        }

        /// a child that covers the screen's safe area (clear of the notch and home indicator)
        public static RectTransform Safe(Transform canvas)
        {
            var rt = Fill("Safe", canvas);
            rt.gameObject.AddComponent<SafeArea>();
            return rt;
        }
    }

    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
