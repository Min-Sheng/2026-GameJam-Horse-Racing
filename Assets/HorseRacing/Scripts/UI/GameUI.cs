using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HorseRacing.UI
{
    /// <summary>
    /// 程式化建立全部畫面（Main/Betting/Race/Result/Shop）並連接 GameManager。
    /// 賽道動畫於 RaceView（M5）負責，這裡只建立容器與切換邏輯。
    /// </summary>
    [RequireComponent(typeof(GameManager))]
    public class GameUI : MonoBehaviour
    {
        [Header("資料庫由 GameManager 取得")]
        public TMP_FontAsset chineseFont;

        [Header("美術素材")]
        public Sprite trackGrass;
        public Sprite trackMud;
        public Sprite trackSnow;

        [Header("角色 Sprite 配置")]
        public HorseSpriteConfig horseSpriteConfig;

        private GameManager _gm;
        private GameConfigDatabase Cfg => _gm.config;

        // 面板
        private GameObject _menuPanel, _bettingPanel, _racePanel, _resultPanel, _shopPanel, _gameOverPanel, _analystPanel;

        // 頂列
        private TextMeshProUGUI _moneyText, _roundText, _cardsText, _phaseText, _noticeText;

        // 下注狀態
        private BetType _selectedBetType = BetType.Win;
        private bool _betTypeChosen = false;
        private readonly List<int> _selectedHorses = new List<int>();
        private long _stake = 100;

        // 下注面板控制項
        private TextMeshProUGUI _bettingTitle, _selectionText, _betDescText, _betsSummary, _analystText;
        private TMP_InputField _stakeInput;
        private readonly List<TextMeshProUGUI> _horseRowText = new List<TextMeshProUGUI>();
        private readonly List<Button> _horseRowButton = new List<Button>();
        private readonly List<Image> _horseStatusImages = new List<Image>();
        private GameObject _analystSection;
        private Button _confirmButton;
        private TextMeshProUGUI _confirmLabel;
        private readonly List<Button> _betTypeButtons = new List<Button>();
        private BetType[] _betTypeOrder;

        // 結果/商店/賽道
        private TextMeshProUGUI _resultText, _shopHeldText, _gameOverText;
        private GameObject _shopCardsContainer;
        private RaceView _raceView;

        private void Awake()
        {
            _gm = GetComponent<GameManager>();
            UIFactory.Font = chineseFont;

            // Auto-load HorseSpriteConfig: try Resources, then create at runtime from sprite sheets
            if (horseSpriteConfig == null)
            {
                horseSpriteConfig = Resources.Load<HorseSpriteConfig>("HorseSpriteConfig");
            }
            if (horseSpriteConfig == null)
            {
                horseSpriteConfig = CreateRuntimeSpriteConfig();
            }
        }

        /// <summary>
        /// Creates a HorseSpriteConfig at runtime by loading sprites from Resources/Horses/ folder.
        /// Sprite sheet PNGs must be in Assets/HorseRacing/Resources/Horses/ with spriteMode=Multiple.
        /// Falls back to loading from Art/Horses/ path patterns if available.
        /// </summary>
        private HorseSpriteConfig CreateRuntimeSpriteConfig()
        {
            var config = ScriptableObject.CreateInstance<HorseSpriteConfig>();
            // Horse order: 1.horse_ 2.spongebob 3.cat 4.grandma 5.goldfish 6.tardis 7.thief 8.tombstone
            string[] spriteNames = { "horse_", "spongebob", "cat", "grandma", "goldfish", "tardis", "thief", "tombstone" };

            for (int i = 0; i < 8; i++)
            {
                var sprites = Resources.LoadAll<Sprite>("Horses/" + spriteNames[i]);
                if (sprites != null && sprites.Length > 0)
                {
                    config.entries[i].frames = sprites;
                }
            }

            // Default frames = horse_ sprites
            var defaultSprites = Resources.LoadAll<Sprite>("Horses/horse_");
            if (defaultSprites != null && defaultSprites.Length > 0)
                config.defaultFrames = defaultSprites;
            else if (config.entries[0].frames != null)
                config.defaultFrames = config.entries[0].frames;

            if (config.defaultFrames == null || config.defaultFrames.Length == 0)
                Debug.LogWarning("[GameUI] Could not load horse sprites from Resources/Horses/. Icons will not display.");

            return config;
        }

        private void Start()
        {
            BuildUI();
            _gm.OnStateChanged += Refresh;
            _gm.OnNotice += ShowNotice;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_gm != null) { _gm.OnStateChanged -= Refresh; _gm.OnNotice -= ShowNotice; }
        }

        // ====================================================================
        // 建構
        // ====================================================================
        private void BuildUI()
        {
            // Canvas — 16:9 (1920×1080) 基準
            var canvasGo = new GameObject("HorseRacingCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var root = UIFactory.Panel_(canvas.transform, "Root", UIFactory.Dark);
            UIFactory.VLayout(root, 0, 0, TextAnchor.UpperCenter, true, false, true, false);

            // TopBar — 顯示回合與剩餘資金
            var topBar = UIFactory.Rect(root.transform, "TopBar", UIFactory.Panel).gameObject;
            UIFactory.LE(topBar, prefH: 36, flexH: 0);
            UIFactory.HLayout(topBar, 16, 8, TextAnchor.MiddleLeft, false, true, true, true);
            _roundText = UIFactory.Text(topBar.transform, "", 22, TextAlignmentOptions.Left, UIFactory.TextMain);
            UIFactory.LE(_roundText.gameObject, flexW: 1);
            _moneyText = UIFactory.Text(topBar.transform, "", 22, TextAlignmentOptions.Right, UIFactory.Accent);
            UIFactory.LE(_moneyText.gameObject, flexW: 1);
            _cardsText = UIFactory.Text(topBar.transform, "", 22, TextAlignmentOptions.Right, UIFactory.TextDim);
            UIFactory.LE(_cardsText.gameObject, flexW: 0.6f);

            // 隱藏的輔助文字節點（避免 null reference）
            var hidden = UIFactory.NewUIObject("HiddenInfo", root.transform);
            hidden.SetActive(false);
            _noticeText = UIFactory.Text(hidden.transform, "", 1);
            _phaseText = UIFactory.Text(hidden.transform, "", 1);

            // Content area
            var content = UIFactory.NewUIObject("Content", root.transform);
            content.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            UIFactory.LE(content, flexH: 1);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = Vector2.zero; contentRT.anchorMax = Vector2.one;
            contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;

            _menuPanel = BuildMenuPanel(content.transform);
            _bettingPanel = BuildBettingPanel(content.transform);
            _analystPanel = BuildAnalystPanel(content.transform);
            _racePanel = BuildRacePanel(content.transform);
            _resultPanel = BuildResultPanel(content.transform);
            _shopPanel = BuildShopPanel(content.transform);
            _gameOverPanel = BuildGameOverPanel(content.transform);
        }

        private GameObject BuildMenuPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "MenuPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 30, 60, TextAnchor.MiddleCenter, true, false, false, false);
            var title = UIFactory.Text(p.transform, "賽馬投注模擬", 72, TextAlignmentOptions.Center, UIFactory.Accent);
            UIFactory.LE(title.gameObject, prefH: 100, prefW: 900);
            var sub = UIFactory.Text(p.transform, "靠情報與推理，三輪下注，賺取最大利潤", 30, TextAlignmentOptions.Center, UIFactory.TextDim);
            UIFactory.LE(sub.gameObject, prefH: 50, prefW: 1000);
            string rounds = Cfg != null && Cfg.game != null && Cfg.game.totalRounds > 0
                ? $"共 {Cfg.game.totalRounds} 回合，起始資金 {Cfg.game.startingMoney}"
                : $"起始資金 {(Cfg != null && Cfg.game != null ? Cfg.game.startingMoney.ToString() : "？")}";
            var roundsInfo = UIFactory.Text(p.transform, rounds, 24, TextAlignmentOptions.Center, UIFactory.TextDim);
            UIFactory.LE(roundsInfo.gameObject, prefH: 40, prefW: 1000);
            var btn = UIFactory.Button(p.transform, "開始遊戲", 36, () => _gm.StartNewRound(), UIFactory.AccentGreen);
            UIFactory.LE(btn.gameObject, prefW: 320, prefH: 80);
            return p;
        }

        // ---------------- Betting ----------------
        private GameObject BuildBettingPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "BettingPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 6, 16, TextAnchor.UpperCenter, true, false, true, false);

            _bettingTitle = UIFactory.Text(p.transform, "下注階段", 30, TextAlignmentOptions.Left, UIFactory.TextMain);
            UIFactory.LE(_bettingTitle.gameObject, prefH: 40);

            var body = UIFactory.NewUIObject("Body", p.transform);
            body.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            UIFactory.LE(body, flexH: 1);
            UIFactory.HLayout(body, 16, 0, TextAnchor.UpperLeft, true, true, false, true);

            // 左：馬匹列表
            var left = UIFactory.Rect(body.transform, "HorseList", UIFactory.Panel).gameObject;
            UIFactory.LE(left, flexW: 1.2f);
            UIFactory.VLayout(left, 4, 10, TextAnchor.UpperCenter, true, false, true, false);
            UIFactory.Text(left.transform, "馬匹 / 賠率 / 情報", 22, TextAlignmentOptions.Center, UIFactory.TextDim);
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                var row = UIFactory.Rect(left.transform, "Row" + horseId, UIFactory.Card).gameObject;
                UIFactory.LE(row, minH: 52, prefH: 52);
                var btn = row.AddComponent<Button>();
                btn.targetGraphic = row.GetComponent<Image>();
                btn.onClick.AddListener(() => ToggleHorse(horseId));
                UIFactory.HLayout(row, 10, 10, TextAnchor.MiddleLeft, false, true, false, true);

                // Horse icon: use sprite from HorseSpriteConfig
                var chipGo = UIFactory.NewUIObject("HorseIcon", row.transform);
                var chipImg = chipGo.AddComponent<Image>();
                chipImg.preserveAspect = true;
                if (horseSpriteConfig != null)
                {
                    var sprites = horseSpriteConfig.GetSprites(horseId);
                    if (sprites != null && sprites.Length > 0 && sprites[0] != null)
                        chipImg.sprite = sprites[0];
                }
                else
                {
                    Debug.LogError("[GameUI] horseSpriteConfig is null. No icon available.");
                }
                UIFactory.LE(chipGo, prefW: 52, minH: 52, prefH: 52, flexW: 0);

                var txt = UIFactory.Text(row.transform, "", 20, TextAlignmentOptions.Left);
                txt.raycastTarget = false;
                UIFactory.LE(txt.gameObject, flexW: 1);

                var statusGo = UIFactory.NewUIObject("StatusImg", row.transform);
                var statusImg = statusGo.AddComponent<Image>();
                statusImg.preserveAspect = true;
                statusImg.raycastTarget = false;
                UIFactory.LE(statusGo, prefH: 48, prefW: 48);
                statusGo.SetActive(false);
                _horseStatusImages.Add(statusImg);

                _horseRowButton.Add(btn);
                _horseRowText.Add(txt);
            }

            // 右：下注控制
            var right = UIFactory.Rect(body.transform, "Controls", UIFactory.Panel).gameObject;
            UIFactory.LE(right, flexW: 1f);
            UIFactory.VLayout(right, 6, 12, TextAnchor.UpperCenter, true, false, true, false);

            UIFactory.Text(right.transform, "選擇玩法", 22, TextAlignmentOptions.Left, UIFactory.TextDim);
            var betTypesWrap = UIFactory.NewUIObject("BetTypes", right.transform);
            betTypesWrap.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var grid = betTypesWrap.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 44); grid.spacing = new Vector2(8, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            UIFactory.LE(betTypesWrap, prefH: 100);
            // 按鈕排序：單勝｜馬單｜三連單 / 複勝｜馬連｜三連複
            _betTypeOrder = new BetType[] { BetType.Win, BetType.Exacta, BetType.Trifecta, BetType.Place, BetType.Quinella, BetType.Trio };
            foreach (BetType bt in _betTypeOrder)
            {
                var entry = Cfg.betting.Get(bt);
                string label = entry != null ? $"{entry.displayName} x{entry.payoutMultiplier}" : bt.ToString();
                var btBtn = UIFactory.Button(betTypesWrap.transform, label, 16, () => SelectBetType(bt), UIFactory.Card);
                _betTypeButtons.Add(btBtn);
            }

            _betDescText = UIFactory.Text(right.transform, "", 18, TextAlignmentOptions.Left, UIFactory.TextDim);
            UIFactory.LE(_betDescText.gameObject, prefH: 26);

            _selectionText = UIFactory.Text(right.transform, "玩法：（未選）　已選馬：（無）", 20, TextAlignmentOptions.Left, UIFactory.TextMain);
            UIFactory.LE(_selectionText.gameObject, prefH: 32);

            // 金額
            UIFactory.Text(right.transform, "下注金額", 22, TextAlignmentOptions.Left, UIFactory.TextDim);
            var stakeRow = UIFactory.NewUIObject("StakeRow", right.transform);
            var stakeRowImg = stakeRow.AddComponent<Image>();
            stakeRowImg.color = new Color(0, 0, 0, 0);
            stakeRowImg.raycastTarget = false;
            UIFactory.HLayout(stakeRow, 4, 0, TextAnchor.MiddleCenter, false, true, false, true);
            UIFactory.LE(stakeRow, prefH: 36, flexH: 0);

            // << : -100
            var btnMM = UIFactory.Button(stakeRow.transform, "<<", 18, () => { _stake = System.Math.Max(0, _stake - 100); SyncStakeInput(); RefreshBetting(); }, UIFactory.Card);
            UIFactory.LE(btnMM.gameObject, prefW: 44);
            // < : -50
            var btnM = UIFactory.Button(stakeRow.transform, "<", 18, () => { _stake = System.Math.Max(0, _stake - 50); SyncStakeInput(); RefreshBetting(); }, UIFactory.Card);
            UIFactory.LE(btnM.gameObject, prefW: 36);

            // 輸入框
            var inputGo = UIFactory.NewUIObject("StakeInput", stakeRow.transform);
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.14f, 0.15f, 0.18f, 1f);
            UIFactory.LE(inputGo, flexW: 1, prefH: 36);

            // TextViewport — 限制 InputField 的觸碰區域
            var viewport = UIFactory.NewUIObject("Viewport", inputGo.transform);
            viewport.AddComponent<RectMask2D>();
            var vpRT = viewport.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(6, 2); vpRT.offsetMax = new Vector2(-6, -2);

            var inputTextGo = UIFactory.NewUIObject("Text", viewport.transform);
            var inputTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            if (UIFactory.Font != null) inputTmp.font = UIFactory.Font;
            inputTmp.fontSize = 24;
            inputTmp.alignment = TextAlignmentOptions.Center;
            inputTmp.color = UIFactory.Accent;
            var inputRT = inputTextGo.GetComponent<RectTransform>();
            inputRT.anchorMin = Vector2.zero; inputRT.anchorMax = Vector2.one;
            inputRT.offsetMin = Vector2.zero; inputRT.offsetMax = Vector2.zero;
            // Placeholder
            var phGo = UIFactory.NewUIObject("Placeholder", viewport.transform);
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            if (UIFactory.Font != null) phTmp.font = UIFactory.Font;
            phTmp.fontSize = 24;
            phTmp.alignment = TextAlignmentOptions.Center;
            phTmp.color = UIFactory.TextDim;
            phTmp.text = "輸入金額";
            var phRT = phGo.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

            _stakeInput = inputGo.AddComponent<TMP_InputField>();
            _stakeInput.textViewport = vpRT;
            _stakeInput.textComponent = inputTmp;
            _stakeInput.placeholder = phTmp;
            _stakeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _stakeInput.text = _stake.ToString();
            _stakeInput.onEndEdit.AddListener(OnStakeInputChanged);

            // > : +50
            var btnP = UIFactory.Button(stakeRow.transform, ">", 18, () => { _stake += 50; SyncStakeInput(); RefreshBetting(); }, UIFactory.Card);
            UIFactory.LE(btnP.gameObject, prefW: 36);
            // >> : +100
            var btnPP = UIFactory.Button(stakeRow.transform, ">>", 18, () => { _stake += 100; SyncStakeInput(); RefreshBetting(); }, UIFactory.Card);
            UIFactory.LE(btnPP.gameObject, prefW: 44);

            var placeBtn = UIFactory.Button(right.transform, "下注", 24, PlaceBet, UIFactory.AccentGreen);
            UIFactory.LE(placeBtn.gameObject, prefH: 52);

            _betsSummary = UIFactory.Text(right.transform, "本回合尚未下注", 18, TextAlignmentOptions.Left, UIFactory.TextDim);
            UIFactory.LE(_betsSummary.gameObject, prefH: 90, flexH: 1);

            return p;
        }

        // ---------------- Analyst (獨立畫面) ----------------
        private GameObject BuildAnalystPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "AnalystPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 20, 40, TextAnchor.MiddleCenter, true, false, false, false);

            UIFactory.Text(p.transform, "購買分析師情報", 36, TextAlignmentOptions.Center, UIFactory.Accent);
            var desc = UIFactory.Text(p.transform, "在最後一輪下注前，可購買情報來輔助決策", 22, TextAlignmentOptions.Center, UIFactory.TextDim);
            UIFactory.LE(desc.gameObject, prefH: 40);

            var btnRow = UIFactory.NewUIObject("BtnRow", p.transform);
            btnRow.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            UIFactory.HLayout(btnRow, 16, 0, TextAnchor.MiddleCenter, false, true, false, false);
            UIFactory.LE(btnRow, prefH: 60);
            var jBtn = UIFactory.Button(btnRow.transform, $"初級情報 ${Cfg.analyst.juniorPrice}", 22, () => { _gm.BuyAnalystReport(AnalystTier.Junior); RefreshAnalystPanel(); }, UIFactory.Accent);
            UIFactory.LE(jBtn.gameObject, prefW: 260, prefH: 56);
            var sBtn = UIFactory.Button(btnRow.transform, $"資深情報 ${Cfg.analyst.seniorPrice}", 22, () => { _gm.BuyAnalystReport(AnalystTier.Senior); RefreshAnalystPanel(); }, UIFactory.Accent);
            UIFactory.LE(sBtn.gameObject, prefW: 260, prefH: 56);

            _analystText = UIFactory.Text(p.transform, "", 20, TextAlignmentOptions.Center, UIFactory.TextMain);
            UIFactory.LE(_analystText.gameObject, prefH: 80, prefW: 800);

            var skipBtn = UIFactory.Button(p.transform, "進入最後一輪下注", 24, OnAnalystDone, UIFactory.AccentGreen);
            UIFactory.LE(skipBtn.gameObject, prefW: 320, prefH: 56);

            return p;
        }

        private void RefreshAnalystPanel()
        {
            if (_gm.Round != null && _gm.Round.PurchasedReport != null)
                _analystText.text = "分析師情報：\n" + string.Join("\n", _gm.Round.PurchasedReport.Statements);
        }

        private void OnAnalystDone()
        {
            _analystPanel.SetActive(false);
            _bettingPanel.SetActive(true);
            RefreshBetting();
        }

        // ---------------- Race ----------------
        private GameObject BuildRacePanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "RacePanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 8, 12, TextAnchor.UpperCenter, true, false, true, false);
            var title = UIFactory.Text(p.transform, "比賽進行中", 30, TextAlignmentOptions.Center, UIFactory.TextMain);
            UIFactory.LE(title.gameObject, prefH: 40);

            var field = UIFactory.Rect(p.transform, "Field", Color.black).gameObject;
            UIFactory.LE(field, flexH: 1);

            _raceView = field.AddComponent<RaceView>();
            _raceView.Init(this, trackGrass, trackMud, trackSnow, horseSpriteConfig);

            return p;
        }

        // ---------------- Result ----------------
        private GameObject BuildResultPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "ResultPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 12, 20, TextAnchor.UpperCenter, true, false, true, false);
            UIFactory.Text(p.transform, "比賽結果", 36, TextAlignmentOptions.Center, UIFactory.Accent);

            var box = UIFactory.Rect(p.transform, "ResultBox", UIFactory.Panel).gameObject;
            UIFactory.LE(box, flexH: 1);
            UIFactory.VLayout(box, 6, 18, TextAnchor.UpperLeft, true, false, true, false);
            _resultText = UIFactory.Text(box.transform, "", 22, TextAlignmentOptions.TopLeft, UIFactory.TextMain);
            UIFactory.LE(_resultText.gameObject, flexH: 1);

            var btn = UIFactory.Button(p.transform, "進入商店", 28, () => _gm.EnterShop(), UIFactory.AccentGreen);
            UIFactory.LE(btn.gameObject, prefH: 64, prefW: 320);
            return p;
        }

        // ---------------- Shop ----------------
        private GameObject BuildShopPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "ShopPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 12, 20, TextAnchor.UpperCenter, true, false, true, false);
            UIFactory.Text(p.transform, "商店 — 購買防禦卡（最多 3 張）", 32, TextAlignmentOptions.Center, UIFactory.Accent);

            _shopCardsContainer = UIFactory.Rect(p.transform, "Cards", UIFactory.Panel).gameObject;
            UIFactory.LE(_shopCardsContainer, flexH: 1);
            UIFactory.VLayout(_shopCardsContainer, 10, 16, TextAnchor.UpperCenter, true, false, true, false);

            _shopHeldText = UIFactory.Text(p.transform, "", 22, TextAlignmentOptions.Center, UIFactory.TextDim);
            UIFactory.LE(_shopHeldText.gameObject, prefH: 36);

            var btn = UIFactory.Button(p.transform, "開始下一回合", 28, () => _gm.NextRound(), UIFactory.AccentGreen);
            UIFactory.LE(btn.gameObject, prefH: 64, prefW: 320);
            return p;
        }

        // ---------------- Game Over ----------------
        private GameObject BuildGameOverPanel(Transform parent)
        {
            var p = UIFactory.Panel_(parent, "GameOverPanel", new Color(0, 0, 0, 0));
            UIFactory.VLayout(p, 30, 60, TextAnchor.MiddleCenter, true, false, false, false);

            var title = UIFactory.Text(p.transform, "遊戲結束", 64, TextAlignmentOptions.Center, UIFactory.AccentRed);
            UIFactory.LE(title.gameObject, prefH: 90, prefW: 800);

            _gameOverText = UIFactory.Text(p.transform, "", 30, TextAlignmentOptions.Center, UIFactory.TextMain);
            UIFactory.LE(_gameOverText.gameObject, prefH: 160, prefW: 900);

            var btn = UIFactory.Button(p.transform, "再玩一次", 36, () => _gm.RestartGame(), UIFactory.AccentGreen);
            UIFactory.LE(btn.gameObject, prefW: 320, prefH: 80);
            return p;
        }

        // ====================================================================
        // 互動
        // ====================================================================
        private void SelectBetType(BetType bt)
        {
            _selectedBetType = bt;
            _betTypeChosen = true;
            _selectedHorses.Clear();
            RefreshBetting();
        }

        private void ToggleHorse(int horseId)
        {
            if (_gm.Phase != GamePhase.Betting) return;
            if (!_betTypeChosen) return;
            int need = Cfg.betting.Get(_selectedBetType)?.selectionCount ?? 1;
            if (_selectedHorses.Contains(horseId)) _selectedHorses.Remove(horseId);
            else
            {
                if (_selectedHorses.Count >= need) _selectedHorses.RemoveAt(0);
                _selectedHorses.Add(horseId);
            }
            RefreshBetting();
        }

        private void PlaceBet()
        {
            int need = Cfg.betting.Get(_selectedBetType)?.selectionCount ?? 1;
            if (_selectedHorses.Count != need) { ShowNotice($"此玩法需選擇 {need} 匹馬"); return; }
            if (_stake <= 0) { ShowNotice("請設定下注金額"); return; }
            if (_gm.PlaceBet(_selectedBetType, _stake, _selectedHorses.ToArray()))
            {
                _selectedHorses.Clear();
                _betTypeChosen = false;
                // 若即將進入最後一輪，先顯示分析師畫面
                bool willBeLastRound = _gm.Round != null &&
                    _gm.Round.CurrentBettingRound == _gm.BettingRounds - 2;
                _gm.ConfirmBettingRound();
                if (willBeLastRound && _gm.Phase == GamePhase.Betting)
                {
                    _bettingPanel.SetActive(false);
                    _analystPanel.SetActive(true);
                    _analystText.text = "（可購買情報，或直接進入最後一輪下注）";
                }
            }
        }

        private void OnStakeInputChanged(string value)
        {
            if (long.TryParse(value, out long parsed))
                _stake = System.Math.Max(0, parsed);
            else
                _stake = 0;
            SyncStakeInput();
            RefreshBetting();
        }

        private void SyncStakeInput()
        {
            if (_stakeInput != null)
                _stakeInput.text = _stake.ToString();
        }

        // ====================================================================
        // 刷新
        // ====================================================================
        private void ShowNotice(string msg)
        {
            if (_noticeText != null) _noticeText.text = msg;
        }

        private void Refresh()
        {
            // 頂列
            if (_gm.Player != null)
            {
                _moneyText.text = $"資金 {_gm.Player.Money}";
                _cardsText.text = $"防禦卡 {_gm.Player.ProtectionCards.Count}/{Cfg.shop.maxHeldCards}";
            }
            _roundText.text = Cfg != null && Cfg.game != null && Cfg.game.totalRounds > 0
                ? $"回合 {_gm.RoundNumber}/{Cfg.game.totalRounds}"
                : $"回合 {_gm.RoundNumber}";
            _phaseText.text = PhaseName(_gm.Phase);

            _menuPanel.SetActive(_gm.Phase == GamePhase.MainMenu);
            _bettingPanel.SetActive(_gm.Phase == GamePhase.Betting);
            _analystPanel.SetActive(false);
            _racePanel.SetActive(_gm.Phase == GamePhase.Racing);
            _resultPanel.SetActive(_gm.Phase == GamePhase.Settlement);
            _shopPanel.SetActive(_gm.Phase == GamePhase.Shop);
            _gameOverPanel.SetActive(_gm.Phase == GamePhase.GameOver);

            switch (_gm.Phase)
            {
                case GamePhase.Betting: RefreshBetting(); break;
                case GamePhase.Racing: _raceView.Play(_gm); break;
                case GamePhase.Settlement: RefreshResult(); break;
                case GamePhase.Shop: RefreshShop(); break;
                case GamePhase.GameOver: RefreshGameOver(); break;
            }
        }

        private void RefreshBetting()
        {
            if (_gm.Round == null) return;
            int round = _gm.Round.CurrentBettingRound + 1;
            _bettingTitle.text = $"下注階段 — 第 {round}/{_gm.BettingRounds} 輪      賽道：？（開賽公布）";

            var revealed = _gm.Round.RevealedCards;
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                var odds = _gm.GetOdds(horseId);
                string card = "";
                foreach (var c in revealed) if (c.HorseId == horseId) card = "　情報：" + c.Description;
                string sel = _selectedHorses.Contains(horseId) ? $"[已選{_selectedHorses.IndexOf(horseId) + 1}] " : "";
                _horseRowText[i].text = $"{sel}Horse {horseId}　賠率 {(odds != null ? odds.WinOdds.ToString("0.00") : "-")}{card}";
                var img = _horseRowButton[i].GetComponent<Image>();
                img.color = _selectedHorses.Contains(horseId) ? UIFactory.AccentGreen : UIFactory.Card;
                _horseRowButton[i].interactable = _betTypeChosen;
                _horseRowText[i].color = _betTypeChosen ? UIFactory.TextMain : UIFactory.TextDim;
            }

            // 更新狀態圖片（只在有情報揭露時顯示）
            // HorseID → HorseStatusConfig index mapping:
            // Horse1=馬(6), Horse2=石頭(2), Horse3=貓利(3), Horse4=輪椅(4), Horse5=金魚(5), Horse6=Tardis(7), Horse7=囚犯(0), Horse8=墓碑(1)
            int[] horseIdToStatusIndex = { 6, 2, 3, 4, 5, 7, 0, 1 };
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1;
                bool hasCard = false;
                foreach (var c in revealed) if (c.HorseId == horseId) { hasCard = true; break; }

                if (hasCard)
                {
                    int statusHorseIdx = horseIdToStatusIndex[i];
                    var sprite = StatusImageSystem.GetStatusSprite(Cfg.horseStatus, statusHorseIdx, _gm.Round.Horses[i].HiddenBonus);
                    if (sprite != null)
                    {
                        _horseStatusImages[i].sprite = sprite;
                        _horseStatusImages[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        _horseStatusImages[i].gameObject.SetActive(false);
                    }
                }
                else
                {
                    _horseStatusImages[i].gameObject.SetActive(false);
                }
            }

            var betEntry = _betTypeChosen ? Cfg.betting.Get(_selectedBetType) : null;
            string betTypeName = betEntry != null ? betEntry.displayName : "（未選）";
            bool ordered = betEntry != null && betEntry.ordered;
            string horseSep = ordered ? " → " : " - ";

            // 玩法說明
            if (_betTypeChosen && betEntry != null)
            {
                string desc = "";
                switch (_selectedBetType)
                {
                    case BetType.Win: desc = "第 1 名"; break;
                    case BetType.Place: desc = "進前三名"; break;
                    case BetType.Quinella: desc = "前兩名，不分順序"; break;
                    case BetType.Exacta: desc = "前兩名，順序要對"; break;
                    case BetType.Trio: desc = "前 3 名，不分順序"; break;
                    case BetType.Trifecta: desc = "前 3 名，順序要對"; break;
                }
                _betDescText.text = $"{betEntry.displayName}：{desc} ； 需選 {betEntry.selectionCount} 匹馬";
            }
            else
            {
                _betDescText.text = "";
            }

            _selectionText.text = "玩法：" + betTypeName
                + "　已選馬：" + (_selectedHorses.Count > 0
                    ? string.Join(horseSep, _selectedHorses.ConvertAll(h => "H" + h))
                    : "（無）");
            SyncStakeInput();

            // 玩法按鈕高亮
            for (int i = 0; i < _betTypeButtons.Count && i < _betTypeOrder.Length; i++)
            {
                var btnImg = _betTypeButtons[i].GetComponent<Image>();
                btnImg.color = (_betTypeChosen && _betTypeOrder[i] == _selectedBetType) ? UIFactory.AccentGreen : UIFactory.Card;
            }

            // 下注摘要
            var sb = new StringBuilder("本回合下注：");
            if (_gm.Round.Bets.Count == 0) sb.Append("（無）");
            foreach (var b in _gm.Round.Bets)
                sb.Append($"\n· {Cfg.betting.Get(b.Type)?.displayName} {string.Join(",", System.Array.ConvertAll(b.HorseIds, x => "H" + x))} ${b.Amount} (x{b.PayoutMultiplier:0.00})");
            _betsSummary.text = sb.ToString();

            // 若已購買情報，在下注畫面也顯示
            if (_gm.Round.PurchasedReport != null)
                _betsSummary.text += "\n\n分析師情報：\n" + string.Join("\n", _gm.Round.PurchasedReport.Statements);
        }

        private void RefreshResult()
        {
            var r = _gm.Round.Result;
            var s = _gm.Round.Settlement;
            var sb = new StringBuilder();
            sb.Append($"賽道：{Cfg.track.GetTrackName(r.Track)}\n\n最終名次：");
            foreach (var st in r.Standings)
                sb.Append($"\n  {st.Rank}. Horse {st.HorseId}　最終速度 {st.FinalSpeed}");
            if (r.Events.Count > 0)
            {
                sb.Append("\n\n賽事事件：");
                foreach (var e in r.Events)
                    sb.Append($"\n  Stage{e.Stage}　Horse{e.HorseId}　{e.EventName}　{(e.Defended ? "(已防禦)" : (e.SpeedModifier > 0 ? "+" : "") + e.SpeedModifier)}");
            }
            sb.Append("\n\n投注結果：");
            if (s.Outcomes.Count == 0) sb.Append("（本回合未下注）");
            foreach (var o in s.Outcomes)
                sb.Append($"\n  {Cfg.betting.Get(o.Bet.Type)?.displayName} {string.Join(",", System.Array.ConvertAll(o.Bet.HorseIds, x => "H" + x))} ${o.Bet.Amount} → {(o.Won ? "中獎 +" + o.Payout : "未中")}");
            sb.Append($"\n\n總投注 {s.TotalStaked}　總派彩 {s.TotalPayout}　");
            sb.Append(s.Net >= 0 ? $"<color=#3FA463>盈虧 +{s.Net}</color>" : $"<color=#C74747>盈虧 {s.Net}</color>");
            sb.Append($"\n目前資金：{_gm.Player.Money}");
            _resultText.text = sb.ToString();
        }

        private void RefreshShop()
        {
            foreach (Transform c in _shopCardsContainer.transform) Destroy(c.gameObject);
            foreach (var card in Cfg.shop.availableCards)
            {
                var row = UIFactory.Rect(_shopCardsContainer.transform, "Card_" + card.cardName, UIFactory.Card).gameObject;
                UIFactory.LE(row, minH: 76, prefH: 76);
                UIFactory.HLayout(row, 14, 14, TextAnchor.MiddleLeft, false, true, false, true);
                string targetName = card.targetEvent != null ? card.targetEvent.displayName : "未知";
                var info = UIFactory.Text(row.transform, $"{card.cardName}　${card.price}　防禦對象：{targetName}\n防禦率 {card.defendChance:P0}",
                    20, TextAlignmentOptions.Left);
                UIFactory.LE(info.gameObject, flexW: 1);
                var captured = card;
                bool canBuy = ShopSystem.CanBuy(_gm.Player, card, Cfg.shop);
                var b = UIFactory.Button(row.transform, "購買", 22, () => _gm.BuyProtectionCard(captured), canBuy ? UIFactory.AccentGreen : UIFactory.Card);
                b.interactable = canBuy;
                UIFactory.LE(b.gameObject, prefW: 120);
            }
            var held = new StringBuilder($"持有防禦卡 {_gm.Player.ProtectionCards.Count}/{Cfg.shop.maxHeldCards}：");
            if (_gm.Player.ProtectionCards.Count == 0) held.Append("（無）");
            foreach (var c in _gm.Player.ProtectionCards) held.Append(" [" + c.cardName + "]");
            _shopHeldText.text = held.ToString();
        }

        public Sprite TrackSprite(TrackType t)
        {
            switch (t)
            {
                case TrackType.Grass: return trackGrass;
                case TrackType.Mud: return trackMud;
                case TrackType.Snow: return trackSnow;
            }
            return trackGrass;
        }

        /// <summary>賽道動畫播完由 RaceView 呼叫，進入結算。</summary>
        public void OnRaceAnimationDone() => _gm.CompleteRaceAndSettle();

        private void RefreshGameOver()
        {
            if (_gameOverText == null) return;
            long starting = Cfg.game != null ? Cfg.game.startingMoney : 1000;
            long money = _gm.Player != null ? _gm.Player.Money : 0;
            long profit = money - starting;

            string profitStr = profit >= 0
                ? $"<color=#3FA463>盈利 +{profit}</color>"
                : $"<color=#C74747>虧損 {profit}</color>";

            string winStr = _gm.GameWon ? "🏆 恭喜獲勝！" : "😢 遊戲落敗";
            _gameOverText.text = $"{winStr}\n\n{_gm.GameOverReason}\n\n最終資金：{money}\n{profitStr}\n共 {_gm.RoundNumber} 回合";
        }

        private static string PhaseName(GamePhase p)
        {
            switch (p)
            {
                case GamePhase.MainMenu: return "主畫面";
                case GamePhase.Betting: return "下注中";
                case GamePhase.Racing: return "比賽中";
                case GamePhase.Settlement: return "結算";
                case GamePhase.Shop: return "商店";
                case GamePhase.GameOver: return "遊戲結束";
            }
            return "";
        }
    }
}
