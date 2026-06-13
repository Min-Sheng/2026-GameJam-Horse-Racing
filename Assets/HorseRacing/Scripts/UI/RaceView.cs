using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// 賽道動畫（2D 卷軸側視視角）。
    /// 畫面只顯示局部賽道，背景持續向左捲動模擬馬匹前進。
    /// 馬匹在畫面中央附近奔跑，依速度差異前後錯開。
    /// 接近終點時背景停止捲動，馬匹衝向終點線。
    /// </summary>
    public class RaceView : MonoBehaviour
    {
        private GameUI _ui;
        private Sprite _horseSprite;
        private RectTransform _self;
        private bool _built;
        private bool _playing;

        // UI 元素
        private RawImage _bg;
        private RectTransform _bgRT;
        private readonly List<RectTransform> _markers = new List<RectTransform>();
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _rankText;
        private Image _finishLine;
        private RectTransform _finishLineRT;

        // 卷軸參數
        private const float RaceDuration = 5.0f;       // 比賽總時長（秒）
        private const float ScrollSpeed = 600f;        // 背景捲動速度（px/s，基於 1920 寬）
        private const float HorseAreaTop = 0.52f;      // 馬匹 Y 區域上界
        private const float HorseAreaBottom = 0.88f;   // 馬匹 Y 區域下界
        private const float HorseCenterX = 0.25f;      // 馬匹群的基準 X 位置（畫面百分比）
        private const float HorseSpreadX = 0.30f;      // 第1名與第8名的最大 X 差距（百分比）

        // 用於背景 tiling
        private Sprite _trackGrass, _trackMud, _trackSnow;

        public void Init(GameUI ui, Sprite horse, Sprite grass, Sprite mud, Sprite snow)
        {
            _ui = ui;
            _horseSprite = horse;
            _trackGrass = grass;
            _trackMud = mud;
            _trackSnow = snow;
            _self = (RectTransform)transform;
        }

        private Sprite GetTrackSprite(TrackType t)
        {
            switch (t)
            {
                case TrackType.Grass: return _trackGrass;
                case TrackType.Mud: return _trackMud;
                case TrackType.Snow: return _trackSnow;
            }
            return _trackGrass;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            // 背景使用 RawImage + UV offset 實現無限捲動
            var bgGo = UIFactory.NewUIObject("TrackBg", transform);
            _bg = bgGo.AddComponent<RawImage>();
            _bg.raycastTarget = false;
            _bg.color = Color.white;
            _bgRT = (RectTransform)bgGo.transform;
            // 背景拉寬為畫面 3 倍寬以確保 tiling 無接縫
            _bgRT.anchorMin = Vector2.zero;
            _bgRT.anchorMax = Vector2.one;
            _bgRT.offsetMin = Vector2.zero;
            _bgRT.offsetMax = Vector2.zero;

            // 終點線（初始隱藏在畫面右外側，接近終點才滑入）
            _finishLine = UIFactory.Rect(transform, "FinishLine", new Color(1f, 0.2f, 0.2f, 0.8f));
            _finishLine.raycastTarget = false;
            _finishLineRT = _finishLine.rectTransform;
            _finishLineRT.anchorMin = new Vector2(0, 0.5f);
            _finishLineRT.anchorMax = new Vector2(0, 0.5f);
            _finishLineRT.pivot = new Vector2(0.5f, 0.5f);
            _finishLineRT.sizeDelta = new Vector2(6, 0); // 高度在 Run 設定
            _finishLineRT.anchoredPosition = new Vector2(2000, 0); // 初始在畫面外

            // 8 匹馬
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                var marker = UIFactory.NewUIObject("Horse" + horseId, transform);
                var mrt = (RectTransform)marker.transform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0, 1);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.sizeDelta = new Vector2(80, 64);

                // 馬匹圖片
                var horseImg = UIFactory.Rect(marker.transform, "Sprite", Color.white);
                horseImg.sprite = _horseSprite;
                horseImg.preserveAspect = true;
                horseImg.raycastTarget = false;
                UIFactory.Stretch(horseImg.rectTransform);

                // 號碼標籤
                var numBg = UIFactory.Rect(marker.transform, "NumBg", UIFactory.HorseColors[i]);
                numBg.raycastTarget = false;
                var nbrt = numBg.rectTransform;
                nbrt.anchorMin = new Vector2(0.5f, 1f);
                nbrt.anchorMax = new Vector2(0.5f, 1f);
                nbrt.pivot = new Vector2(0.5f, 0f);
                nbrt.sizeDelta = new Vector2(26, 20);
                nbrt.anchoredPosition = new Vector2(0, 2);

                var num = UIFactory.Text(numBg.transform, horseId.ToString(), 14, TextAlignmentOptions.Center, Color.white);
                UIFactory.Stretch(num.rectTransform);

                _markers.Add(mrt);
            }

            // 事件文字（底部）
            _eventText = UIFactory.Text(transform, "", 22, TextAlignmentOptions.BottomLeft, UIFactory.TextMain);
            var ert = _eventText.rectTransform;
            ert.anchorMin = new Vector2(0, 0);
            ert.anchorMax = new Vector2(1, 0.10f);
            ert.offsetMin = new Vector2(16, 8);
            ert.offsetMax = new Vector2(-16, 0);

            // 名次文字（右上角）
            _rankText = UIFactory.Text(transform, "", 20, TextAlignmentOptions.TopRight, UIFactory.Accent);
            var rrt = _rankText.rectTransform;
            rrt.anchorMin = new Vector2(0.72f, 0.85f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.offsetMin = new Vector2(0, 0);
            rrt.offsetMax = new Vector2(-12, -8);
        }

        public void Play(GameManager gm)
        {
            Build();
            if (_playing) return;

            // 設定背景 texture（從 Sprite 取出）
            var trackSprite = GetTrackSprite(gm.Round.Track);
            if (trackSprite != null && trackSprite.texture != null)
            {
                _bg.texture = trackSprite.texture;
                _bg.uvRect = new Rect(0, 0, 3, 1); // tiling 3x 水平
            }

            StartCoroutine(Run(gm));
        }

        private IEnumerator Run(GameManager gm)
        {
            _playing = true;
            _eventText.text = "";
            _rankText.text = "";

            yield return null;
            yield return null;

            float width = _self.rect.width;
            float height = _self.rect.height;

            int waitFrames = 0;
            while ((width < 1f || height < 1f) && waitFrames < 10)
            {
                yield return null;
                width = _self.rect.width;
                height = _self.rect.height;
                waitFrames++;
            }

            // 馬匹大小 = 畫面高度 / 4
            float horseH = height / 4f;
            float horseW = horseH * 1.25f;
            for (int i = 0; i < 8; i++)
                _markers[i].sizeDelta = new Vector2(horseW, horseH);

            // 終點線高度 = 畫面高度一半
            _finishLineRT.sizeDelta = new Vector2(6, height * 0.5f);

            // Y 位置（下半部，微小錯開允許重疊）
            float topY = height * HorseAreaTop;
            float bottomY = height * HorseAreaBottom;
            var laneY = new float[8];
            for (int i = 0; i < 8; i++)
            {
                float t = (i + 0.5f) / 8f;
                laneY[i] = -(topY + t * (bottomY - topY));
            }

            var result = gm.Round.Result;

            // 每匹馬的「進度速率」：第1名最快
            // 歸一化：第1名在 RaceDuration 時到達 1.0，後續依次慢
            var horseRate = new float[8]; // index = horseId - 1
            for (int rank = 0; rank < result.RankToHorseId.Length; rank++)
            {
                int hid = result.RankToHorseId[rank];
                // 第1名 rate = 1.0/RaceDuration, 每落後一名慢 5%
                horseRate[hid - 1] = 1f / (RaceDuration + rank * RaceDuration * 0.05f);
            }

            // 初始位置
            float basePx = width * HorseCenterX;
            for (int i = 0; i < 8; i++)
                _markers[i].anchoredPosition = new Vector2(basePx, laneY[i]);

            // 終點線初始在畫面外右側
            float finishLineStartX = width + 100f;
            float finishLineEndX = width * 0.85f;
            _finishLineRT.anchoredPosition = new Vector2(finishLineStartX, 0);

            // 事件文字
            var stageMsg = new string[4];
            foreach (var e in result.Events)
            {
                string defended = e.Defended ? "（已防禦）" : $"{(e.SpeedModifier > 0 ? "+" : "")}{e.SpeedModifier}";
                string line = $"Horse{e.HorseId} {e.EventName} {defended}";
                stageMsg[e.Stage] = string.IsNullOrEmpty(stageMsg[e.Stage])
                    ? line
                    : stageMsg[e.Stage] + "　|　" + line;
            }

            float totalDuration = RaceDuration * 1.15f; // 多留 15% 讓最後一名跑完
            var finishedOrder = new List<int>();
            var hasFinished = new HashSet<int>();

            int shownStage = 0;
            float elapsed = 0f;
            float bgOffset = 0f;

            // === 主賽跑迴圈 ===
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float raceProgress = elapsed / RaceDuration; // 第1名的進度

                // 背景捲動（UV offset 向左移動）
                bgOffset += ScrollSpeed * Time.deltaTime / width;
                if (_bg.texture != null)
                    _bg.uvRect = new Rect(bgOffset, 0, 3, 1);

                // 計算每匹馬的位置
                // 馬匹 X = basePx + (自己的進度 - 領先馬進度) * spreadWidth
                // 這樣領先的馬在右邊，落後的在左邊
                float leaderProgress = 0f;
                for (int i = 0; i < 8; i++)
                {
                    float p = Mathf.Clamp01(elapsed * horseRate[i]);
                    if (p > leaderProgress) leaderProgress = p;
                }

                for (int i = 0; i < 8; i++)
                {
                    float p = Mathf.Clamp01(elapsed * horseRate[i]);
                    float relativePos = (p - leaderProgress); // -差距 ~ 0
                    float x = basePx + width * HorseSpreadX * (1f + relativePos);

                    // 奔跑彈跳
                    float bob = p < 1f ? Mathf.Sin(elapsed * 11f + i * 0.8f) * 3.5f : 0f;
                    _markers[i].anchoredPosition = new Vector2(x, laneY[i] + bob);

                    // 記錄到達
                    if (p >= 1f && !hasFinished.Contains(i + 1))
                    {
                        hasFinished.Add(i + 1);
                        finishedOrder.Add(i + 1);
                    }
                }

                // 終點線：在最後 25% 時間滑入畫面
                float finishPhase = Mathf.Clamp01((raceProgress - 0.75f) / 0.25f);
                float finishX = Mathf.Lerp(finishLineStartX, finishLineEndX, finishPhase * finishPhase);
                _finishLineRT.anchoredPosition = new Vector2(finishX, 0);

                // 更新名次顯示
                if (finishedOrder.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int r = 0; r < finishedOrder.Count; r++)
                        sb.Append($"第{r + 1}名: Horse {finishedOrder[r]}\n");
                    _rankText.text = sb.ToString();
                }

                // 顯示階段事件
                int stageByTime = elapsed < totalDuration * 0.33f ? 1 : (elapsed < totalDuration * 0.66f ? 2 : 3);
                if (stageByTime > shownStage)
                {
                    shownStage = stageByTime;
                    if (!string.IsNullOrEmpty(stageMsg[shownStage]))
                        _eventText.text = "賽事事件：" + stageMsg[shownStage];
                }

                yield return null;
            }

            // 停止捲動，等待一下
            yield return new WaitForSeconds(1.5f);
            _playing = false;
            _ui.OnRaceAnimationDone();
        }
    }
}
