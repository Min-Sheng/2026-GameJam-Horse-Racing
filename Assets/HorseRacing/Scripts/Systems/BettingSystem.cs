using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 投注系統（PRD §10）。建立投注（鎖定當下倍率）並依比賽結果判定輸贏與派彩。
    /// 獨贏採用 OddsSystem 的動態每馬賠率；其餘玩法採 BettingConfig 固定倍率。
    /// 所有倍率皆含本金。
    /// </summary>
    public static class BettingSystem
    {
        /// <summary>建立一筆投注，鎖定派彩倍率。currentOdds 僅獨贏需要（可為 null）。</summary>
        public static Bet CreateBet(BetType type, long amount, int[] horseIds, int round,
            BettingConfig betCfg, List<HorseOdds> currentOdds)
        {
            float multiplier;
            if (type == BetType.Win && currentOdds != null && horseIds != null && horseIds.Length > 0)
            {
                var o = OddsSystem.GetForHorse(currentOdds, horseIds[0]);
                var entry = betCfg != null ? betCfg.Get(type) : null;
                multiplier = o != null ? o.WinOdds : (entry != null ? entry.payoutMultiplier : 1f);
            }
            else
            {
                var entry = betCfg != null ? betCfg.Get(type) : null;
                multiplier = entry != null ? entry.payoutMultiplier : 1f;
            }
            return new Bet(type, amount, horseIds, round, multiplier);
        }

        public static bool IsWin(Bet bet, RaceResult result)
        {
            int[] sel = bet.HorseIds;
            if (sel == null || sel.Length == 0 || result.RankToHorseId == null) return false;
            int[] rank = result.RankToHorseId;

            switch (bet.Type)
            {
                case BetType.Win:
                    return rank.Length >= 1 && sel[0] == rank[0];

                case BetType.Place:
                    return InTopN(sel[0], rank, 3);

                case BetType.Quinella: // 前兩名，不分順序
                    return sel.Length >= 2 && rank.Length >= 2 &&
                           SameSet(sel, 2, rank, 2);

                case BetType.Exacta: // 前兩名，順序正確
                    return sel.Length >= 2 && rank.Length >= 2 &&
                           sel[0] == rank[0] && sel[1] == rank[1];

                case BetType.Trio: // 前三名，不分順序
                    return sel.Length >= 3 && rank.Length >= 3 &&
                           SameSet(sel, 3, rank, 3);

                case BetType.Trifecta: // 前三名，順序正確
                    return sel.Length >= 3 && rank.Length >= 3 &&
                           sel[0] == rank[0] && sel[1] == rank[1] && sel[2] == rank[2];
            }
            return false;
        }

        /// <summary>派彩（含本金）：贏則 round(amount * multiplier)，輸則 0。</summary>
        public static long SettleBet(Bet bet, RaceResult result)
        {
            if (!IsWin(bet, result)) return 0;
            return (long)System.Math.Round(bet.Amount * (double)bet.PayoutMultiplier);
        }

        private static bool InTopN(int horseId, int[] rank, int n)
        {
            for (int i = 0; i < n && i < rank.Length; i++)
                if (rank[i] == horseId) return true;
            return false;
        }

        private static bool SameSet(int[] sel, int selCount, int[] rank, int rankCount)
        {
            for (int i = 0; i < selCount; i++)
            {
                bool found = false;
                for (int j = 0; j < rankCount; j++)
                    if (sel[i] == rank[j]) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
