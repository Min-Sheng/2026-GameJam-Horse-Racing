using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>遊戲流程階段（對應 PRD §2 核心循環）。</summary>
    public enum GamePhase
    {
        MainMenu,   // 主畫面
        Betting,    // 下注（含三輪，期間揭露消息卡、更新賠率、分析師）
        Racing,     // 賽事進行（動畫）
        Settlement, // 結算
        Shop,       // 商店
        GameOver    // 遊戲結束（資金耗盡或達到回合上限）
    }

    /// <summary>單一回合的所有當局資料。</summary>
    public class RoundContext
    {
        public List<Horse> Horses = new List<Horse>();

        /// <summary>本回合抽到的三張消息卡（依輪次逐張揭露）。</summary>
        public List<MessageCard> AllCards = new List<MessageCard>();

        public int CurrentBettingRound;            // 0..(rounds-1)
        public List<HorseOdds> CurrentOdds = new List<HorseOdds>();

        public TrackType Track;                    // 開賽前已決定但隱藏
        public bool TrackRevealed;

        public AnalystReport PurchasedReport;      // 購買後才有

        public List<Bet> Bets = new List<Bet>();
        public RaceResult Result;
        public SettlementSystem.SettlementResult Settlement;

        /// <summary>已揭露的消息卡（截至目前輪次）。</summary>
        public List<MessageCard> RevealedCards
        {
            get
            {
                var list = new List<MessageCard>();
                for (int i = 0; i < AllCards.Count; i++)
                    if (AllCards[i].Round <= CurrentBettingRound) list.Add(AllCards[i]);
                return list;
            }
        }
    }
}
