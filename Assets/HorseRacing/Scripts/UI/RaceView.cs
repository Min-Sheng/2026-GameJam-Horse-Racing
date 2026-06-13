using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// 賽道動畫（側視視角）。賽道背景滿版顯示，8 匹馬由左向右奔跑，
    /// 以微小 Y 偏移模擬前後排列（靠近底部的馬視覺上較「前」）。
    /// 抵達順序與 RaceSimulationSystem 名次一致。
    /// </summary>
    public class RaceView : MonoBehaviour
    {
        private GameUI _ui;
        private Sprite _horseSprite;
        private Image _bg;
        private RectTransform _self;
        private bool _built;
        private bool _playing;

        private readonly List<RectTransform> _markers = new List<RectTransform>();
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _rankText;
        private Image _finishLine;

        // 側視：馬匹在畫面下半部跑，上半部是天空/背景
        private const float HorseAreaTop = 0.35f;    // 馬匹區域從畫面 35% 開始（上方留給天空）
        private const float HorseAreaBottom = 0.85f; // 馬匹區域到畫面 85%（下方留給事件文字）
        private const float StartX = 0.05f;          // 起跑 X（百分比）
        private const float EndX = 0.92f;            // 終點 X（百分比）
        private const float HorseSize = 80f;         // 馬匹圖片尺寸（寬）
        private const float HorseSizeH = 64f;        // 馬匹圖片尺寸（高）

        public void Init(GameUI ui, Sprite horse, Sprite grass, Sprite mud, Sprite snow)
        {
            _ui = ui;
            _horseSprite = horse;
            _self = (RectTransform)transform;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            // 背景（賽道圖滿版）
            _bg = UIFactory.Rect(transform, "TrackBg", Color.white);
            UIFactory.Stretch(_bg.rectTransform);
            _bg.preserveAspect = false;
            _bg.raycastTarget = false;
            _bg.type = Image.Type.Simple;

            // 終點線
            _finishLine = UIFactory.Rect(transform, "FinishLine", new Color(1f, 0.2f, 0.2f, 0.7f));
            _finishLine.raycastTarget = false;
            var fr = _finishLine.rectTransform;
            fr.anchorMin = new Vector2(EndX, 0.1f);
            fr.anchorMax = new Vector2(EndX, 0.9f);
            fr.pivot = new Vector2(0.5f, 0.5f);
            fr.sizeDelta = new Vector2(4, 0);
            fr.anchoredPosition = Vector2.zero;

            // 8 匹馬
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                var marker = UIFactory.NewUIObject("Horse" + horseId, transform);
                var mrt = (RectTransform)marker.transform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0, 1);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.sizeDelta = new Vector2(HorseSize, HorseSizeH);

                // 馬匹圖片
                var horseImg = UIFactory.Rect(marker.transform, "Sprite", Color.white);
                horseImg.sprite = _horseSprite;
                horseImg.preserveAspect = true;
                horseImg.raycastTarget = false;
                var hrt = horseImg.rectTransform;
                UIFactory.Stretch(hrt);

                // 號碼標籤（馬匹上方）
                var numBg = UIFactory.Rect(marker.transform, "NumBg", UIFactory.HorseColors[i]);
                numBg.raycastTarget = false;
                var nbrt = numBg.rectTransform;
                nbrt.anchorMin = new Vector2(0.5f, 1f);
                nbrt.anchorMax = new Vector2(0.5f, 1f);
                nbrt.pivot = new Vector2(0.5f, 0f);
                nbrt.sizeDelta = new Vector2(28, 22);
                nbrt.anchoredPosition = new Vector2(0, 2);

                var num = UIFactory.Text(numBg.transform, horseId.ToString(), 16, TextAlignmentOptions.Center, Color.white);
                UIFactory.Stretch(num.rectTransform);

                _markers.Add(mrt);
            }

            // 事件文字（底部）
            _eventText = UIFactory.Text(transform, "", 22, TextAlignmentOptions.BottomLeft, UIFactory.TextMain);
            var ert = _eventText.rectTransform;
            ert.anchorMin = new Vector2(0, 0);
            ert.anchorMax = new Vector2(1, 0.12f);
            ert.offsetMin = new Vector2(16, 8);
            ert.offsetMax = new Vector2(-16, 0);

            // 名次文字（右上角）
            _rankText = UIFactory.Text(transform, "", 20, TextAlignmentOptions.TopRight, UIFactory.Accent);
            var rrt = _rankText.rectTransform;
            rrt.anchorMin = new Vector2(0.7f, 0.88f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.offsetMin = new Vector2(0, 0);
            rrt.offsetMax = new Vector2(-12, -8);
        }

        public void Play(GameManager gm)
        {
            Build();
            if (_playing) return;
            _bg.sprite = _ui.TrackSprite(gm.Round.Track);
            StartCoroutine(Run(gm));
        }

        private IEnumerator Run(GameManager gm)
        {
            _playing = true;
            _eventText.text = "";
            _rankText.text = "";
            yield return null; // 等一幀讓版面尺寸生效
            yield return null; // 多等一幀確保 layout 完成

            float width = _self.rect.width;
            float height = _self.rect.height;

            // 如果尺寸仍為 0，再等幾幀
            int waitFrames = 0;
            while ((width < 1f || height < 1f) && waitFrames < 10)
            {
                yield return null;
                width = _self.rect.width;
                height = _self.rect.height;
                waitFrames++;
            }

            float startPx = width * StartX;
            float endPx = width * EndX;

            // 計算每匹馬的 Y 位置（側視：微小錯開模擬前後）
            float topY = height * HorseAreaTop;
            float bottomY = height * HorseAreaBottom;
            var laneY = new float[8];
            for (int i = 0; i < 8; i++)
            {
                // 均勻分佈在 HorseAreaTop ~ HorseAreaBottom 之間
                float t = (i + 0.5f) / 8f;
                laneY[i] = -(topY + t * (bottomY - topY));
            }

            var result = gm.Round.Result;

            // 每匹馬的完成時間依名次
            var finishTime = new Dictionary<int, float>();
            float baseDuration = 4.0f;
            for (int rank = 0; rank < result.RankToHorseId.Length; rank++)
                finishTime[result.RankToHorseId[rank]] = baseDuration + rank * 0.25f;

            // 設定起跑位置
            for (int i = 0; i < 8; i++)
                _markers[i].anchoredPosition = new Vector2(startPx, laneY[i]);

            // 各階段事件文字
            var stageMsg = new string[4];
            foreach (var e in result.Events)
            {
                string defended = e.Defended ? "（已防禦）" : $"{(e.SpeedModifier > 0 ? "+" : "")}{e.SpeedModifier}";
                string line = $"Horse{e.HorseId} {e.EventName} {defended}";
                stageMsg[e.Stage] = string.IsNullOrEmpty(stageMsg[e.Stage])
                    ? line
                    : stageMsg[e.Stage] + "　|　" + line;
            }

            float duration = 0f;
            foreach (var kv in finishTime)
                if (kv.Value > duration) duration = kv.Value;
            duration += 0.3f;

            // 記錄到達順序
            var finishedOrder = new List<int>();
            var hasFinished = new HashSet<int>();

            int shownStage = 0;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < 8; i++)
                {
                    int horseId = i + 1;
                    float ft = finishTime.TryGetValue(horseId, out var f) ? f : duration;
                    float p = Mathf.Clamp01(elapsed / ft);
                    float eased = p * p * (3f - 2f * p); // smoothstep
                    float x = Mathf.Lerp(startPx, endPx, eased);

                    // 奔跑時的上下彈跳
                    float bob = p < 1f ? Mathf.Sin(elapsed * 10f + i * 0.7f) * 4f : 0f;
                    _markers[i].anchoredPosition = new Vector2(x, laneY[i] + bob);

                    // 記錄到達
                    if (p >= 1f && !hasFinished.Contains(horseId))
                    {
                        hasFinished.Add(horseId);
                        finishedOrder.Add(horseId);
                    }
                }

                // 更新名次顯示
                if (finishedOrder.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int r = 0; r < finishedOrder.Count; r++)
                        sb.Append($"第{r + 1}名: Horse {finishedOrder[r]}\n");
                    _rankText.text = sb.ToString();
                }

                // 顯示階段事件
                int stageByTime = elapsed < duration * 0.33f ? 1 : (elapsed < duration * 0.66f ? 2 : 3);
                if (stageByTime > shownStage)
                {
                    shownStage = stageByTime;
                    if (!string.IsNullOrEmpty(stageMsg[shownStage]))
                        _eventText.text = "賽事事件：" + stageMsg[shownStage];
                }
                yield return null;
            }

            // 確保全部到達終點
            for (int i = 0; i < 8; i++)
                _markers[i].anchoredPosition = new Vector2(endPx, laneY[i]);

            yield return new WaitForSeconds(1.2f);
            _playing = false;
            _ui.OnRaceAnimationDone();
        }
    }
}
