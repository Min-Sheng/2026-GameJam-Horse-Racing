using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 分析師系統（PRD §7）：提供額外但不完全可信的情報。
    /// 以 InitialScore 排名作為「真相」基準，依正確率決定每則陳述為真或誤導。
    /// </summary>
    public static class AnalystSystem
    {
        public static AnalystReport GenerateReport(List<Horse> horses, AnalystTier tier, AnalystConfig cfg, IRandom rng)
        {
            float accuracy = cfg.GetAccuracy(tier);

            // 以 InitialScore 排名，前三名視為「強」
            var ranked = new List<Horse>(horses);
            ranked.Sort((a, b) =>
            {
                int cmp = b.InitialScore.CompareTo(a.InitialScore);
                return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
            });
            var topThree = new HashSet<int>();
            for (int i = 0; i < ranked.Count && i < 3; i++) topThree.Add(ranked[i].Id);

            // 隨機挑選不重複的馬產生陳述
            var pool = new List<Horse>(horses);
            rng.Shuffle(pool);
            int n = System.Math.Min(cfg.statementsPerReport, pool.Count);

            var report = new AnalystReport { Tier = tier };
            for (int i = 0; i < n; i++)
            {
                var h = pool[i];
                bool actualGood = topThree.Contains(h.Id);
                bool truthful = rng.Value() < accuracy;
                bool reportedGood = truthful ? actualGood : !actualGood;
                string verdict = reportedGood ? "有機會進入前三名" : "表現不被看好";
                report.Statements.Add($"Horse {h.Id} {verdict}");
            }
            return report;
        }
    }
}
