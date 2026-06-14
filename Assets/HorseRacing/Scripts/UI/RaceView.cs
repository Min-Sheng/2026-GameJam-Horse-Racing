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
        private RectTransform _self;
        private bool _built;
        private bool _playing;

        // UI 元素
        private Image _bg;
        private RectTransform _bgRT;
        private readonly List<RectTransform> _markers = new List<RectTransform>();
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _rankText;
        private TextMeshProUGUI _trackLabel;
        private Image _finishLine;
        private RectTransform _finishLineRT;

        // 子系統
        private MinimapController _minimap;
        private EventCardController _eventCards;
        private RaceAnimConfig _raceAnimConfig;

        // Sprite animation state (pre-allocated, zero per-frame allocation)
        private HorseSpriteConfig _horseSpriteConfig;
        private readonly Image[] _horseImages = new Image[8];
        private readonly Sprite[][] _horseSpriteArrays = new Sprite[8][];
        private readonly bool[] _finished = new bool[8];

        // 卷軸參數
        private const float RaceDuration = 5.0f;       // 比賽總時長（秒）
        private const float BgWidthMultiplier = 4f;    // 背景圖寬度 = 畫面寬度 × 此值
        private const float HorseAreaTop = 0.52f;      // 馬匹 Y 區域上界（畫面百分比）
        private const float HorseAreaBottom = 0.88f;   // 馬匹 Y 區域下界
        private const float HorseCenterX = 0.35f;      // 馬匹群在畫面中的基準 X
        private const float HorseSpreadX = 0.25f;      // 第1名到第8名最大 X 差距

        private Sprite _trackGrass, _trackMud, _trackSnow;

        public void Init(GameUI ui, Sprite grass, Sprite mud, Sprite snow, HorseSpriteConfig horseSpriteConfig)
        {
            _ui = ui;
            _trackGrass = grass;
            _trackMud = mud;
            _trackSnow = snow;
            _horseSpriteConfig = horseSpriteConfig;
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
                // Set initial sprite from config (will be updated in Play() anyway)
                if (_horseSpriteConfig != null)
                {
                    var initSprites = _horseSpriteConfig.GetSprites(i + 1);
                    if (initSprites != null && initSprites.Length > 0 && initSprites[0] != null)
                        horseImg.sprite = initSprites[0];
                }
                horseImg.preserveAspect = true;
                horseImg.raycastTarget = false;
                UIFactory.Stretch(horseImg.rectTransform);

                // Store reference for sprite animation (zero per-frame allocation)
                _horseImages[i] = horseImg;

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

            // 賽道類型標籤（左上角）
            _trackLabel = UIFactory.Text(transform, "", 24, TextAlignmentOptions.TopLeft, UIFactory.TextMain);
            var trt = _trackLabel.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.90f);
            trt.anchorMax = new Vector2(0.35f, 1f);
            trt.offsetMin = new Vector2(16, 0);
            trt.offsetMax = new Vector2(0, -8);

            // 子系統初始化：EventCardController（先加，sibling order 較低）
            _eventCards = gameObject.AddComponent<EventCardController>();
            _eventCards.Init(_self, _raceAnimConfig);

            // 子系統初始化：MinimapController（後加，sibling order 較高，渲染在上層不被遮蔽）
            _minimap = gameObject.AddComponent<MinimapController>();
            _minimap.Init(_self, _raceAnimConfig);
        }

        public void Play(GameManager gm)
        {
            _raceAnimConfig = gm.config != null ? gm.config.raceAnim : null;
            Build();
            if (_playing) return;
            _bg.sprite = GetTrackSprite(gm.Round.Track);
            _trackLabel.text = "賽道：" + GetTrackDisplayName(gm.Round.Track);

            // Cache sprite arrays from config and reset finished state (zero per-frame allocation)
            for (int i = 0; i < 8; i++)
            {
                _horseSpriteArrays[i] = _horseSpriteConfig != null
                    ? _horseSpriteConfig.GetSprites(i + 1)
                    : null;
                _finished[i] = false;

                // Set initial sprite to each horse's frame 0 (replaces shared _horseSprite)
                var initFrames = _horseSpriteArrays[i];
                if (initFrames != null && initFrames.Length >= 1 && initFrames[0] != null)
                    _horseImages[i].sprite = initFrames[0];
            }

            StartCoroutine(Run(gm));
        }

        private static string GetTrackDisplayName(TrackType t)
        {
            switch (t)
            {
                case TrackType.Grass: return "草地";
                case TrackType.Mud: return "泥地";
                case TrackType.Snow: return "雪地";
            }
            return t.ToString();
        }

        private IEnumerator Run(GameManager gm)
        {
            _playing = true;
            _eventText.gameObject.SetActive(false); // 隱藏舊事件文字，改用 EventCardController
            _rankText.text = "";

            // Read sprite frame rate from config, clamped to [1,30]
            int spriteFps = _raceAnimConfig != null ? _raceAnimConfig.spriteFrameRate : 8;
            if (spriteFps < 1) spriteFps = 1;
            else if (spriteFps > 30) spriteFps = 30;

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

            // 設定背景寬度（比畫面寬 N 倍）
            float bgWidth = width * BgWidthMultiplier;
            _bgRT.sizeDelta = new Vector2(bgWidth, 0);
            _bgRT.anchoredPosition = new Vector2(0, 0); // 初始：左端對齊畫面左端

            // sliding window 的最大偏移 = bgWidth - 畫面寬
            float maxScroll = bgWidth - width;

            // 終點線位置（在背景右端附近，距右邊 5%）
            float finishLineLocalX = bgWidth * 0.95f;
            _finishLineRT.sizeDelta = new Vector2(6, height * 0.5f);

            // 馬匹大小 = 畫面高度 / 4
            float horseH = height / 4f;
            float horseW = horseH * 1.25f;
            for (int i = 0; i < 8; i++)
                _markers[i].sizeDelta = new Vector2(horseW, horseH);

            // Y 位置（下半部）
            float topY = height * HorseAreaTop;
            float bottomY = height * HorseAreaBottom;
            var laneY = new float[8];
            for (int i = 0; i < 8; i++)
            {
                float t = (i + 0.5f) / 8f;
                laneY[i] = -(topY + t * (bottomY - topY));
            }

            var result = gm.Round.Result;

            // 每匹馬的速率（第1名最快）
            var horseRate = new float[8];
            for (int rank = 0; rank < result.RankToHorseId.Length; rank++)
            {
                int hid = result.RankToHorseId[rank];
                horseRate[hid - 1] = 1f / (RaceDuration + rank * RaceDuration * 0.05f);
            }

            // 事件依階段分組（供 EventCardController 使用）
            var stageEvents = new List<StageEventLog>[4];
            for (int s = 0; s < 4; s++) stageEvents[s] = new List<StageEventLog>();
            foreach (var e in result.Events)
                stageEvents[e.Stage].Add(e);

            float totalDuration = RaceDuration * 1.15f;
            var finishedOrder = new List<int>();
            var hasFinished = new HashSet<int>();
            int shownStage = 0;
            float elapsed = 0f;

            // 初始位置
            float basePx = width * HorseCenterX;
            for (int i = 0; i < 8; i++)
                _markers[i].anchoredPosition = new Vector2(basePx, laneY[i]);

            // 背景捲動速度（px/s）— 用來算馬在螢幕上的絕對速度
            float scrollSpeedPxPerSec = maxScroll / RaceDuration;
            float leaderRate = horseRate[result.RankToHorseId[0] - 1];

            // === 單一迴圈：背景捲完後馬繼續跑 ===
            bool allPassed = false;
            float finishMaxTime = totalDuration + 4f; // 安全上限

            // 小地圖：賽事開始時顯示
            _minimap.Show();

            // 事件卡片系統：賽事開始時顯示
            _eventCards.Show();

            // 用於每幀傳遞馬匹進度給小地圖
            var currentProgress = new float[8];

            while (!allPassed && elapsed < finishMaxTime)
            {
                elapsed += Time.deltaTime;

                // 領先馬的進度（不 clamp，超過 1.0 代表已過終點）
                float leaderProgressRaw = 0f;
                for (int i = 0; i < 8; i++)
                {
                    float p = elapsed * horseRate[i];
                    if (p > leaderProgressRaw) leaderProgressRaw = p;
                }

                // 背景 sliding window：clamp 在 0~1（到終點即停）
                float scrollProgress = Mathf.Clamp01(leaderProgressRaw);
                float scrollX = scrollProgress * maxScroll;
                _bgRT.anchoredPosition = new Vector2(-scrollX, 0);

                // 終點線跟隨背景
                float finishScreenX = finishLineLocalX - scrollX;
                _finishLineRT.anchoredPosition = new Vector2(finishScreenX, 0);

                // 背景是否已停止捲動
                bool bgStopped = scrollProgress >= 1f;

                // 馬匹位置
                allPassed = true;
                for (int i = 0; i < 8; i++)
                {
                    float p = elapsed * horseRate[i]; // 不 clamp，可超過 1.0
                    float x;

                    if (!bgStopped)
                    {
                        // 階段一：相對位移（領先者在 basePx，落後者在左邊）
                        float diff = p - leaderProgressRaw;
                        x = basePx + diff * width * HorseSpreadX / 0.05f;
                    }
                    else
                    {
                        // 階段二：背景不動，馬以自身世界速度向右推進
                        // 基準 = 背景停止瞬間的位置 + 之後的位移
                        // 背景停止時 leaderProgressRaw ≈ 1.0 (leader), 此時 leader 的 diff=0 → x=basePx
                        // 之後每匹馬的 screenX 增加 = (自身速度) * dt 投影
                        // 累計位移 = (p - scrollStopProgress_i) * (scrollSpeed / leaderRate) 
                        // scrollStopProgress_i = 那匹馬在背景停止瞬間的進度 = 1.0/leaderRate * horseRate[i]... 
                        // 簡化：直接用 (p - 1.0) 代表超過終點的量，加上相對差
                        float pAtStop = horseRate[i] / leaderRate; // 背景停止時該馬的進度
                        float diffAtStop = pAtStop - 1f; // 與領先者的差距（≤0）
                        float baseAtStop = basePx + diffAtStop * width * HorseSpreadX / 0.05f;
                        // 停止後的額外位移
                        float extraProgress = p - pAtStop;
                        float extraPx = extraProgress * (scrollSpeedPxPerSec / leaderRate);
                        x = baseAtStop + extraPx;
                    }

                    // 小地圖進度：基於馬匹在整條背景上的絕對位置（clamp 前）
                    float horseAbsX = x + scrollX;
                    currentProgress[i] = Mathf.Clamp01(horseAbsX / finishLineLocalX);

                    x = Mathf.Clamp(x, horseW * -0.5f, width + horseW);

                    // 奔跑彈跳（過了終點線就停）
                    bool pastFinish = x >= finishScreenX + horseW;
                    float bob = pastFinish ? 0f : Mathf.Sin(elapsed * 11f + i * 0.8f) * 3.5f;
                    _markers[i].anchoredPosition = new Vector2(x, laneY[i] + bob);

                    // 判斷是否過終點線
                    if (x >= finishScreenX)
                    {
                        if (!hasFinished.Contains(i + 1))
                        {
                            hasFinished.Add(i + 1);
                            finishedOrder.Add(i + 1);
                        }

                        // Finish: set finished flag and assign frame 0
                        if (!_finished[i])
                        {
                            _finished[i] = true;
                            var finFrames = _horseSpriteArrays[i];
                            if (finFrames != null && finFrames.Length >= 1)
                            {
                                var frame0 = finFrames[0];
                                if (frame0 != null)
                                    _horseImages[i].sprite = frame0;
                            }
                        }
                    }
                    else
                    {
                        allPassed = false;
                    }

                    // Frame-cycling sprite animation (zero allocation)
                    if (!_finished[i])
                    {
                        var frames = _horseSpriteArrays[i];
                        if (frames != null && frames.Length >= 1)
                        {
                            int frameIndex = (int)(elapsed * spriteFps) % frames.Length;
                            var sprite = frames[frameIndex];
                            if (sprite != null)
                                _horseImages[i].sprite = sprite;
                            // If sprite is null, retain previous sprite (no assignment)
                        }
                        // If frames is null or empty, skip animation (keep current sprite)
                    }
                }

                // 小地圖：每幀更新各馬匹進度位置
                _minimap.UpdatePositions(currentProgress);

                // 名次
                if (finishedOrder.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int r = 0; r < finishedOrder.Count; r++)
                        sb.Append($"第{r + 1}名: Horse {finishedOrder[r]}\n");
                    _rankText.text = sb.ToString();
                }

                // 階段事件：使用 EventCardController 顯示卡片
                float eventPhase = Mathf.Clamp01(elapsed / totalDuration);
                int stageByTime = eventPhase < 0.33f ? 1 : (eventPhase < 0.66f ? 2 : 3);
                if (stageByTime > shownStage)
                {
                    shownStage = stageByTime;
                    foreach (var e in stageEvents[shownStage])
                        _eventCards.EnqueueEvent(e);
                }

                yield return null;
            }

            // 賽事結束：清理子系統
            _minimap.Hide();
            _eventCards.Clear();
            _eventCards.Hide();

            yield return new WaitForSeconds(1.5f);
            _playing = false;
            _ui.OnRaceAnimationDone();
        }
    }
}
