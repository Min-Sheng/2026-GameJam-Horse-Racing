using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// 賽道動畫（2D 卷軸側視視角）。
    /// 賽道圖片比畫面寬很多，畫面像 sliding window 一樣從圖片左端（起點）
    /// 平移到圖片右端（終點）。馬匹在畫面中根據相對速度差前後錯開。
    /// </summary>
    public class RaceView : MonoBehaviour
    {
        private GameUI _ui;
        private Sprite _horseSprite;
        private RectTransform _self;
        private bool _built;
        private bool _playing;

        // UI 元素
        private Image _bg;
        private RectTransform _bgRT;
        private readonly List<RectTransform> _markers = new List<RectTransform>();
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _rankText;
        private Image _finishLine;
        private RectTransform _finishLineRT;

        // 卷軸參數
        private const float RaceDuration = 5.0f;       // 比賽總時長（秒）
        private const float BgWidthMultiplier = 4f;    // 背景圖寬度 = 畫面寬度 × 此值
        private const float HorseAreaTop = 0.52f;      // 馬匹 Y 區域上界（畫面百分比）
        private const float HorseAreaBottom = 0.88f;   // 馬匹 Y 區域下界
        private const float HorseCenterX = 0.35f;      // 馬匹群在畫面中的基準 X
        private const float HorseSpreadX = 0.25f;      // 第1名到第8名最大 X 差距

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

            // 需要一個遮罩容器來裁切超出畫面的背景
            var maskGo = UIFactory.NewUIObject("Mask", transform);
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.color = Color.white;
            maskImg.raycastTarget = false;
            var mask = maskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var maskRT = (RectTransform)maskGo.transform;
            UIFactory.Stretch(maskRT);

            // 背景圖（比畫面寬 BgWidthMultiplier 倍，水平平移實現捲動）
            _bg = UIFactory.Rect(maskGo.transform, "TrackBg", Color.white);
            _bg.preserveAspect = false;
            _bg.raycastTarget = false;
            _bg.type = Image.Type.Simple;
            _bgRT = _bg.rectTransform;
            // 初始設定：左對齊，寬度在 Run() 中依實際畫面尺寸設定
            _bgRT.anchorMin = new Vector2(0, 0);
            _bgRT.anchorMax = new Vector2(0, 1);
            _bgRT.pivot = new Vector2(0, 0.5f);

            // 終點線（在背景上，隨背景移動）
            _finishLine = UIFactory.Rect(maskGo.transform, "FinishLine", new Color(1f, 0.2f, 0.2f, 0.8f));
            _finishLine.raycastTarget = false;
            _finishLineRT = _finishLine.rectTransform;
            _finishLineRT.anchorMin = new Vector2(0, 0.5f);
            _finishLineRT.anchorMax = new Vector2(0, 0.5f);
            _finishLineRT.pivot = new Vector2(0.5f, 0.5f);
            _finishLineRT.sizeDelta = new Vector2(6, 0);

            // 8 匹馬（在遮罩外面，不被裁切）
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                var marker = UIFactory.NewUIObject("Horse" + horseId, transform);
                var mrt = (RectTransform)marker.transform;
                mrt.anchorMin = mrt.anchorMax = new Vector2(0, 1);
                mrt.pivot = new Vector2(0.5f, 0.5f);
                mrt.sizeDelta = new Vector2(80, 64);

                var horseImg = UIFactory.Rect(marker.transform, "Sprite", Color.white);
                horseImg.sprite = _horseSprite;
                horseImg.preserveAspect = true;
                horseImg.raycastTarget = false;
                UIFactory.Stretch(horseImg.rectTransform);

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
            _bg.sprite = GetTrackSprite(gm.Round.Track);
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

            // 設定背景寬度
            float bgWidth = width * BgWidthMultiplier;
            _bgRT.sizeDelta = new Vector2(bgWidth, 0);
            _bgRT.anchoredPosition = new Vector2(0, 0);

            float maxScroll = bgWidth - width;
            float finishLineLocalX = bgWidth * 0.95f;
            _finishLineRT.sizeDelta = new Vector2(6, height * 0.5f);

            // 馬匹大小
            float horseH = height / 4f;
            float horseW = horseH * 1.25f;
            for (int i = 0; i < 8; i++)
                _markers[i].sizeDelta = new Vector2(horseW, horseH);

            // Y 位置
            float topY = height * HorseAreaTop;
            float bottomY = height * HorseAreaBottom;
            var laneY = new float[8];
            for (int i = 0; i < 8; i++)
            {
                float t = (i + 0.5f) / 8f;
                laneY[i] = -(topY + t * (bottomY - topY));
            }

            var result = gm.Round.Result;
            int horseCount = result.RankToHorseId.Length;

            // 從 StagePositions 建立每匹馬的正規化進度（0~1）
            // stageProgress[stage][horseIdx] = 在該段結束時的正規化位移
            float maxCumulative = 0f;
            if (result.StagePositions != null && result.StagePositions.Length > 0)
            {
                var lastStage = result.StagePositions[result.StagePositions.Length - 1];
                for (int i = 0; i < horseCount; i++)
                    if (lastStage[i] > maxCumulative) maxCumulative = lastStage[i];
            }
            if (maxCumulative < 1f) maxCumulative = 1f;

            // 建立進度 keyframes: [0]=起點(0), [1]=stage1結束, [2]=stage2結束, [3]=stage3結束
            // 每匹馬有 4 個 keyframe 值
            var keyframes = new float[horseCount][];
            for (int i = 0; i < horseCount; i++)
            {
                keyframes[i] = new float[4];
                keyframes[i][0] = 0f;
                if (result.StagePositions != null)
                {
                    for (int s = 0; s < result.StagePositions.Length && s < 3; s++)
                        keyframes[i][s + 1] = result.StagePositions[s][i] / maxCumulative;
                }
            }

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

            // 動畫時間分配：每段佔 RaceDuration/3 秒
            float stageDuration = RaceDuration / 3f;
            float totalDuration = RaceDuration * 1.15f;
            var finishedOrder = new List<int>();
            var hasFinished = new HashSet<int>();
            int shownStage = 0;
            float elapsed = 0f;

            // 初始位置
            float basePx = width * HorseCenterX;
            for (int i = 0; i < 8; i++)
                _markers[i].anchoredPosition = new Vector2(basePx, laneY[i]);

            float scrollSpeedPxPerSec = maxScroll / RaceDuration;

            bool allPassed = false;
            float finishMaxTime = totalDuration + 4f;

            while (!allPassed && elapsed < finishMaxTime)
            {
                elapsed += Time.deltaTime;

                // 計算每匹馬在當前時間的進度（插值 keyframes）
                var horseProgress = new float[horseCount];
                for (int i = 0; i < horseCount; i++)
                {
                    // 當前時間對應哪個段
                    float normalizedTime = elapsed / RaceDuration;
                    if (normalizedTime >= 1f)
                    {
                        horseProgress[i] = keyframes[i][3];
                    }
                    else
                    {
                        // 3 段，每段佔 1/3
                        float stageFloat = normalizedTime * 3f;
                        int stageIdx = Mathf.Clamp((int)stageFloat, 0, 2);
                        float stageT = stageFloat - stageIdx;
                        // 在 keyframes[stageIdx] 和 keyframes[stageIdx+1] 之間插值
                        float from = keyframes[i][stageIdx];
                        float to = keyframes[i][stageIdx + 1];
                        horseProgress[i] = Mathf.Lerp(from, to, stageT);
                    }
                }

                // 領先馬進度
                float leaderProgress = 0f;
                for (int i = 0; i < horseCount; i++)
                    if (horseProgress[i] > leaderProgress) leaderProgress = horseProgress[i];
                if (leaderProgress < 0.001f) leaderProgress = 0.001f;

                // 背景捲動
                float scrollProgress = Mathf.Clamp01(leaderProgress);
                float scrollX = scrollProgress * maxScroll;
                _bgRT.anchoredPosition = new Vector2(-scrollX, 0);

                // 終點線
                float finishScreenX = finishLineLocalX - scrollX;
                _finishLineRT.anchoredPosition = new Vector2(finishScreenX, 0);

                bool bgStopped = scrollProgress >= 1f;

                // 馬匹位置
                allPassed = true;
                for (int i = 0; i < horseCount; i++)
                {
                    float p = horseProgress[i];
                    float x;

                    if (!bgStopped)
                    {
                        float diff = p - leaderProgress;
                        x = basePx + diff * width * HorseSpreadX / 0.05f;
                    }
                    else
                    {
                        float pAtStop = horseProgress[i]; // 已到最終進度
                        float extraProgress = (elapsed / RaceDuration - 1f) * (keyframes[i][3]);
                        float baseAtStop = basePx + (keyframes[i][3] - keyframes[result.RankToHorseId[0] - 1][3])
                            / keyframes[result.RankToHorseId[0] - 1][3] * width * HorseSpreadX / 0.05f;
                        float extraPx = Mathf.Max(0, elapsed - RaceDuration) * scrollSpeedPxPerSec * keyframes[i][3] / maxCumulative;
                        x = baseAtStop + extraPx;
                    }

                    x = Mathf.Clamp(x, horseW * -0.5f, width + horseW);

                    bool pastFinish = x >= finishScreenX + horseW;
                    float bob = pastFinish ? 0f : Mathf.Sin(elapsed * 11f + i * 0.8f) * 3.5f;
                    _markers[i].anchoredPosition = new Vector2(x, laneY[i] + bob);

                    if (x >= finishScreenX)
                    {
                        if (!hasFinished.Contains(i + 1))
                        {
                            hasFinished.Add(i + 1);
                            finishedOrder.Add(i + 1);
                        }
                    }
                    else
                    {
                        allPassed = false;
                    }
                }

                // 名次
                if (finishedOrder.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int r = 0; r < finishedOrder.Count; r++)
                        sb.Append($"第{r + 1}名: Horse {finishedOrder[r]}\n");
                    _rankText.text = sb.ToString();
                }

                // 階段事件
                float eventPhase = Mathf.Clamp01(elapsed / totalDuration);
                int stageByTime = eventPhase < 0.33f ? 1 : (eventPhase < 0.66f ? 2 : 3);
                if (stageByTime > shownStage)
                {
                    shownStage = stageByTime;
                    if (!string.IsNullOrEmpty(stageMsg[shownStage]))
                        _eventText.text = "賽事事件：" + stageMsg[shownStage];
                }

                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            _playing = false;
            _ui.OnRaceAnimationDone();
        }
    }
}
