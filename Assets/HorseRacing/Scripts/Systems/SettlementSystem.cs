using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 結算系統（PRD §12）。比對玩家所有投注與比賽結果，計算派彩、更新資金、回傳摘要。
    /// 註：投注本金已於下注當下扣除，故結算僅將派彩（含本金）加回資金。
    /// </summary>
    public static class SettlementSystem
    {
        public class BetOutcome
        {
            public Bet Bet;
            public bool Won;
            public long Payout; // 含本金
        }

        public class SettlementResult
        {
            public long TotalStaked;
            public long TotalPayout;
            public long Net; // TotalPayout - TotalStaked（顯示盈虧用）
            public readonly List<BetOutcome> Outcomes = new List<BetOutcome>();
        }

        public static SettlementResult Settle(PlayerState player, List<Bet> bets, RaceResult result)
        {
            var summary = new SettlementResult();
            if (bets != null)
            {
                for (int i = 0; i < bets.Count; i++)
                {
                    var bet = bets[i];
                    long payout = BettingSystem.SettleBet(bet, result);
                    summary.Outcomes.Add(new BetOutcome
                    {
                        Bet = bet,
                        Won = payout > 0,
                        Payout = payout
                    });
                    summary.TotalStaked += bet.Amount;
                    summary.TotalPayout += payout;
                }
            }
            summary.Net = summary.TotalPayout - summary.TotalStaked;

            if (player != null) player.Money += summary.TotalPayout;
            return summary;
        }
    }
}
