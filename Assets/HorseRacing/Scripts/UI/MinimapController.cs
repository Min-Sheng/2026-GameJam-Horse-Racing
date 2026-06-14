using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// 俯視小地圖控制器，管理 minimap 面板與 8 個 dot。
    /// 以鳥瞰矩形區域在畫面右上角即時顯示 8 匹馬的相對位置。
    /// </summary>
    public class MinimapController : MonoBehaviour
    {
        private RectTransform _panel;
        private Image[] _dots;
        private Image _startLine;
        private Image _finishLine;

        // Config defaults (used when no RaceAnimConfig is provided)
        private float _widthPercent = 0.30f;
        private float _heightPercent = 0.28f;
        private float _marginPx = 10f;
        private float _bgAlpha = 0.6f;

        /// <summary>
        /// 初始化 minimap 面板，建立背景、起/終點線。
        /// </summary>
        /// <param name="parentRT">父層 RectTransform（通常為 RaceView 的 _self）</param>
        /// <param name="config">動畫配置（可為 null，使用預設值）</param>
        public void Init(RectTransform parentRT, RaceAnimConfig config = null)
        {
            if (config != null)
            {
                _widthPercent = config.minimapWidthPercent;
                _heightPercent = config.minimapHeightPercent;
                _marginPx = config.minimapMarginPx;
                _bgAlpha = config.minimapBgAlpha;
            }

            BuildPanel(parentRT);
            BuildBoundaryLines();
            BuildDots();

            // 預設隱藏，等 Show() 被呼叫
            _panel.gameObject.SetActive(false);
        }

        /// <summary>顯示小地圖。</summary>
        public void Show()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(true);
        }

        /// <summary>隱藏小地圖。</summary>
        public void Hide()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 更新所有馬匹在小地圖上的位置。
        /// progress[i] 為 horse (i+1) 的進度 0.0~1.0+。
        /// </summary>
        public void UpdatePositions(float[] progress)
        {
            if (progress == null || progress.Length < 8)
            {
                Debug.LogWarning("[MinimapController] UpdatePositions: progress is null or length < 8, skipping update.");
                return;
            }

            if (_dots == null) return;

            float panelWidth = _panel.rect.width;
            if (panelWidth < 1f) return; // 尚未佈局完成

            // 終點線在 95% 位置，dot 的 progress 1.0 對應此處
            float finishX = panelWidth * 0.95f;

            for (int i = 0; i < 8; i++)
            {
                if (_dots[i] == null) continue;

                float clamped = Mathf.Clamp01(progress[i]);
                float dotX = clamped * finishX;
                _dots[i].rectTransform.anchoredPosition = new Vector2(dotX, 0f);
            }
        }

        private void BuildPanel(RectTransform parentRT)
        {
            // 建立面板容器
            var panelGo = UIFactory.NewUIObject("MinimapPanel", parentRT);
            _panel = (RectTransform)panelGo.transform;

            // 使用 anchor 定義相對尺寸，確保即使 parent rect 未解析也能正確顯示
            // 右上角區域：anchorMin.x = 1 - widthPercent, anchorMax = (1, 1)
            float marginXNorm = _marginPx / 1920f; // 以 1920 為基準的歸一化 margin
            float marginYNorm = _marginPx / 1080f; // 以 1080 為基準的歸一化 margin
            _panel.anchorMin = new Vector2(1f - _widthPercent + marginXNorm, 1f - _heightPercent + marginYNorm);
            _panel.anchorMax = new Vector2(1f - marginXNorm, 1f - marginYNorm);
            _panel.pivot = new Vector2(1, 1);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;

            // 半透明背景
            var bgColor = new Color(0f, 0f, 0f, _bgAlpha);
            var bgImg = panelGo.AddComponent<Image>();
            bgImg.color = bgColor;
            bgImg.raycastTarget = false;

            // 初始化 dots 陣列（由 BuildDots() 填充）
            _dots = new Image[8];
        }

        private void BuildBoundaryLines()
        {
            // 起點線（左邊界）
            var startGo = UIFactory.NewUIObject("StartLine", _panel);
            _startLine = startGo.AddComponent<Image>();
            _startLine.color = new Color(1f, 1f, 1f, 0.8f);
            _startLine.raycastTarget = false;
            var startRT = _startLine.rectTransform;
            startRT.anchorMin = new Vector2(0, 0);
            startRT.anchorMax = new Vector2(0, 1);
            startRT.pivot = new Vector2(0, 0.5f);
            startRT.sizeDelta = new Vector2(2, 0);
            startRT.anchoredPosition = Vector2.zero;

            // 終點線（賽道終點在 95% 位置，與右邊界保持間距）
            var finishGo = UIFactory.NewUIObject("FinishLine", _panel);
            _finishLine = finishGo.AddComponent<Image>();
            _finishLine.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            _finishLine.raycastTarget = false;
            var finishRT = _finishLine.rectTransform;
            finishRT.anchorMin = new Vector2(0.95f, 0);
            finishRT.anchorMax = new Vector2(0.95f, 1);
            finishRT.pivot = new Vector2(0.5f, 0.5f);
            finishRT.sizeDelta = new Vector2(2, 0);
            finishRT.anchoredPosition = Vector2.zero;
        }

        private void BuildDots()
        {
            for (int i = 0; i < 8; i++)
            {
                var dotGo = UIFactory.NewUIObject($"Dot_{i + 1}", _panel);
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.color = UIFactory.HorseColors[i];
                dotImg.raycastTarget = false;

                var dotRT = dotImg.rectTransform;
                // 使用 anchor 來定位垂直位置，避免依賴 rect.height
                // 各馬匹從上到下均分：anchorY 從 1.0 (top) 到 0.0 (bottom)
                float anchorY = 1f - (i / 7f);
                dotRT.anchorMin = new Vector2(0, anchorY);
                dotRT.anchorMax = new Vector2(0, anchorY);
                dotRT.pivot = new Vector2(0.5f, 0.5f);
                dotRT.sizeDelta = new Vector2(12, 12);
                dotRT.anchoredPosition = Vector2.zero;

                _dots[i] = dotImg;
            }
        }
    }
}
