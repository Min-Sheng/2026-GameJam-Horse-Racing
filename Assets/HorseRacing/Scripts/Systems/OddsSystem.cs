using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 賠率系統（PRD §5）。依 InitialScore 排名（同分馬號小者較前），
    /// 由 OddsConfig 計算每匹馬的獨贏賠率；下注輪次越後賠率越差。
    /// </summary>
    public static class OddsSystem
    {
        /// <summary>
        /// 計算所有馬的賠率，回傳依名次（rank 1 起）排序的清單。
        /// </summary>
        public static List<HorseOdds> ComputeOdds(List<Horse> horses, OddsConfig cfg, int round)
        {
            var sorted = new List<Horse>(horses);
            sorted.Sort((a, b) =>
            {
                int cmp = b.InitialScore.CompareTo(a.InitialScore); // 分數高者在前
                if (cmp != 0) return cmp;
                return a.Id.CompareTo(b.Id);                        // 同分：馬號小者在前
            });

            var result = new List<HorseOdds>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                result.Add(new HorseOdds
                {
                    HorseId = sorted[i].Id,
                    Rank = i + 1,
                    WinOdds = cfg.GetWinOdds(i, round)
                });
            }
            return result;
        }

        public static HorseOdds GetForHorse(List<HorseOdds> odds, int horseId)
        {
            for (int i = 0; i < odds.Count; i++)
                if (odds[i].HorseId == horseId) return odds[i];
            return null;
        }
    }
}
