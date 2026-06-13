using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 遊戲流程總控（PRD §2）。串接所有純 C# 系統，維護玩家狀態與當局資料。
    /// 本類別不引用任何 UI 型別；UI 透過事件與公開屬性讀取狀態。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("設定資料庫（PRD §14：全部數值由此載入）")]
        public GameConfigDatabase config;

        [Header("隨機種子（0 = 每次隨機）")]
        public int randomSeed = 0;

        // --- 狀態 ---
        public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
        public PlayerState Player { get; private set; }
        public RoundContext Round { get; private set; }
        public int RoundNumber { get; private set; }

        private IRandom _rng;

        /// <summary>任何狀態變更時觸發，UI 據此刷新。</summary>
        public event Action OnStateChanged;
        /// <summary>有事件/訊息提示時觸發（字串訊息）。</summary>
        public event Action<string> OnNotice;

        public int BettingRounds => config != null && config.betting != null ? config.betting.bettingRounds : 3;
        public bool IsLastBettingRound => Round != null && Round.CurrentBettingRound >= BettingRounds - 1;

        /// <summary>遊戲結束原因。</summary>
        public string GameOverReason { get; private set; }
        /// <summary>是否獲勝（最終資金 >= 起始資金）。</summary>
        public bool GameWon { get; private set; }

        private int TotalRounds => config != null && config.game != null ? config.game.totalRounds : 0;

        private void Notify() => OnStateChanged?.Invoke();
        private void Notice(string msg) { OnNotice?.Invoke(msg); }

        private void Awake()
        {
            _rng = randomSeed != 0 ? new SystemRandom(randomSeed) : new SystemRandom();
            if (config != null && config.game != null)
                Player = new PlayerState(config.game.startingMoney);
        }

        // ====================================================================
        // 回合流程
        // ====================================================================

        /// <summary>開始新的一回合（PRD §2 前段）。</summary>
        public void StartNewRound()
        {
            if (config == null) { Debug.LogError("GameManager: config 未設定"); return; }
            if (Player == null) Player = new PlayerState(config.game.startingMoney);

            // 檢查回合上限
            if (TotalRounds > 0 && RoundNumber >= TotalRounds)
            {
                TriggerGameOver($"已完成 {TotalRounds} 回合！");
                return;
            }

            RoundNumber++;
            Round = new RoundContext();

            // 1. 產生馬匹與隱藏加成
            Round.Horses = HorseSystem.GenerateHorses(config.game, _rng);

            // 2. 預先決定賽道（隱藏，開賽才公布）
            Round.Track = TrackSystem.PickTrack(config.track, _rng);
            Round.TrackRevealed = false;

            // 3. 抽三張消息卡（逐輪揭露）
            Round.AllCards = MessageCardSystem.DrawCards(Round.Horses, config.messageCards, _rng, BettingRounds);

            // 4. 進入第一下注輪，計算賠率
            Round.CurrentBettingRound = 0;
            RecomputeOdds();

            Phase = GamePhase.Betting;
            Notice($"第 {RoundNumber} 回合開始");
            Notify();
        }

        private void RecomputeOdds()
        {
            Round.CurrentOdds = OddsSystem.ComputeOdds(Round.Horses, config.odds, Round.CurrentBettingRound);
        }

        /// <summary>下注（PRD §10）。立即扣除本金。</summary>
        public bool PlaceBet(BetType type, long amount, int[] horseIds)
        {
            if (Phase != GamePhase.Betting) return false;
            if (amount < config.game.minBetAmount) { Notice($"最低下注 {config.game.minBetAmount}"); return false; }
            if (amount > Player.Money) { Notice("資金不足"); return false; }
            if (horseIds == null || horseIds.Length == 0) return false;

            var bet = BettingSystem.CreateBet(type, amount, horseIds, Round.CurrentBettingRound, config.betting, Round.CurrentOdds);
            Player.Money -= amount;
            Round.Bets.Add(bet);
            Notice($"已下注：{config.betting.Get(type)?.displayName} {amount}");
            Notify();
            return true;
        }

        /// <summary>購買分析師情報（PRD §7）。僅最後一輪可購買，每回合一次。</summary>
        public bool BuyAnalystReport(AnalystTier tier)
        {
            if (Phase != GamePhase.Betting) return false;
            if (Round.PurchasedReport != null) { Notice("本回合已購買情報"); return false; }
            long price = config.analyst.GetPrice(tier);
            if (Player.Money < price) { Notice("資金不足"); return false; }

            Player.Money -= price;
            Round.PurchasedReport = AnalystSystem.GenerateReport(Round.Horses, tier, config.analyst, _rng);
            Notice($"購買{(tier == AnalystTier.Senior ? "資深" : "初級")}分析師情報");
            Notify();
            return true;
        }

        /// <summary>確認本輪下注，進入下一下注輪；最後一輪後可開賽。</summary>
        public void ConfirmBettingRound()
        {
            if (Phase != GamePhase.Betting) return;
            if (Round.CurrentBettingRound < BettingRounds - 1)
            {
                Round.CurrentBettingRound++;
                RecomputeOdds(); // 賠率逐輪變差
                Notice($"進入第 {Round.CurrentBettingRound + 1} 次下注，賠率已更新");
                Notify();
            }
            else
            {
                StartRace();
            }
        }

        /// <summary>開賽：公布賽道並模擬比賽（PRD §9）。</summary>
        public void StartRace()
        {
            if (Phase != GamePhase.Betting) return;
            Round.TrackRevealed = true;
            Phase = GamePhase.Racing;
            Round.Result = RaceSimulationSystem.Simulate(
                Round.Horses, Round.Track, config.track, config.events, _rng, Player.ProtectionCards, config.game);
            Notice($"公布賽道：{config.track.GetTrackName(Round.Track)}");
            Notify();
        }

        /// <summary>賽事動畫播放完成後呼叫，進行結算（PRD §12）。</summary>
        public void CompleteRaceAndSettle()
        {
            if (Phase != GamePhase.Racing) return;
            Round.Settlement = SettlementSystem.Settle(Player, Round.Bets, Round.Result);
            Phase = GamePhase.Settlement;
            long net = Round.Settlement.Net;
            Notice(net >= 0 ? $"本回合獲利 {net}" : $"本回合虧損 {-net}");
            Notify();

            // 破產判定
            if (Player.Money <= 0)
                TriggerGameOver("資金耗盡！");
        }

        /// <summary>進入商店（PRD §11）。</summary>
        public void EnterShop()
        {
            if (Phase != GamePhase.Settlement) return;
            Phase = GamePhase.Shop;
            Notify();
        }

        /// <summary>購買防禦卡（PRD §11）。</summary>
        public bool BuyProtectionCard(ProtectionCardDefinition card)
        {
            if (Phase != GamePhase.Shop) return false;
            bool ok = ShopSystem.Buy(Player, card, config.shop);
            Notice(ok ? $"購買 {card.cardName}" : "無法購買（資金不足或已達上限）");
            if (ok) Notify();
            return ok;
        }

        /// <summary>開始下一回合。</summary>
        public void NextRound()
        {
            if (Phase != GamePhase.Shop && Phase != GamePhase.Settlement) return;

            // 低於最低下注金額也視為破產
            if (Player.Money < config.game.minBetAmount && Player.Money > 0)
            {
                TriggerGameOver("資金不足以下注！");
                return;
            }

            StartNewRound();
        }

        /// <summary>觸發遊戲結束。</summary>
        private void TriggerGameOver(string reason)
        {
            GameOverReason = reason;
            long starting = config != null && config.game != null ? config.game.startingMoney : 1000;
            GameWon = Player != null && Player.Money >= starting;
            Phase = GamePhase.GameOver;
            Notice(reason);
            Notify();
        }

        /// <summary>重新開始遊戲（回主選單）。</summary>
        public void RestartGame()
        {
            RoundNumber = 0;
            Round = null;
            Player = null;
            GameOverReason = null;
            GameWon = false;
            Phase = GamePhase.MainMenu;
            Notify();
        }

        // 便利查詢
        public HorseOdds GetOdds(int horseId) => OddsSystem.GetForHorse(Round.CurrentOdds, horseId);
        public List<MessageCard> GetRevealedCards() => Round != null ? Round.RevealedCards : new List<MessageCard>();
    }
}
