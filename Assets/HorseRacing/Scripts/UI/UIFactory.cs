using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>程式化建立 uGUI/TMP 元件的小工具集。</summary>
    public static class UIFactory
    {
        /// <summary>所有建立的文字統一套用的中文字型（由 GameUI 設定）。</summary>
        public static TMP_FontAsset Font;

        public static readonly Color Dark = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        public static readonly Color Panel = new Color(0.18f, 0.20f, 0.24f, 0.98f);
        public static readonly Color Card = new Color(0.24f, 0.27f, 0.32f, 1f);
        public static readonly Color Accent = new Color(0.20f, 0.55f, 0.85f, 1f);
        public static readonly Color AccentGreen = new Color(0.22f, 0.62f, 0.38f, 1f);
        public static readonly Color AccentRed = new Color(0.78f, 0.28f, 0.28f, 1f);
        public static readonly Color TextMain = new Color(0.95f, 0.95f, 0.96f, 1f);
        public static readonly Color TextDim = new Color(0.70f, 0.72f, 0.76f, 1f);

        /// <summary>八匹馬的代表色。</summary>
        public static readonly Color[] HorseColors =
        {
            new Color(0.90f, 0.30f, 0.30f), new Color(0.30f, 0.55f, 0.90f),
            new Color(0.95f, 0.78f, 0.25f), new Color(0.40f, 0.80f, 0.45f),
            new Color(0.75f, 0.45f, 0.85f), new Color(0.95f, 0.55f, 0.25f),
            new Color(0.35f, 0.80f, 0.85f), new Color(0.90f, 0.45f, 0.65f),
        };

        public static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Rect(Transform parent, string name, Color color)
        {
            var go = NewUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static GameObject Panel_(Transform parent, string name, Color color)
        {
            var img = Rect(parent, name, color);
            Stretch(img.rectTransform);
            return img.gameObject;
        }

        public static TextMeshProUGUI Text(Transform parent, string content, float size,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var go = NewUIObject("Text", parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? TextMain;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, float fontSize, UnityAction onClick, Color? bg = null)
        {
            var img = Rect(parent, "Button_" + label, bg ?? Accent);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors; colors.highlightedColor = Color.Lerp(img.color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(img.color, Color.black, 0.2f);
            colors.disabledColor = new Color(img.color.r, img.color.g, img.color.b, 0.35f);
            btn.colors = colors;
            var t = Text(img.transform, label, fontSize);
            Stretch(t.rectTransform, 6, 6, 2, 2);
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, float spacing = 6,
            int padding = 8, TextAnchor align = TextAnchor.UpperCenter,
            bool cChildW = true, bool cChildH = false, bool fChildW = true, bool fChildH = false)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            // 垂直堆疊一律控制子物件高度，使 LayoutElement.preferredHeight/flexibleHeight 生效。
            v.childControlWidth = cChildW; v.childControlHeight = true;
            v.childForceExpandWidth = fChildW; v.childForceExpandHeight = fChildH;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float spacing = 6,
            int padding = 0, TextAnchor align = TextAnchor.MiddleLeft,
            bool cChildW = true, bool cChildH = true, bool fChildW = false, bool fChildH = false)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = align;
            h.childControlWidth = cChildW; h.childControlHeight = cChildH;
            h.childForceExpandWidth = fChildW; h.childForceExpandHeight = fChildH;
            return h;
        }

        public static LayoutElement LE(GameObject go, float minW = -1, float minH = -1,
            float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (minW >= 0) le.minWidth = minW;
            if (minH >= 0) le.minHeight = minH;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }
    }
}
