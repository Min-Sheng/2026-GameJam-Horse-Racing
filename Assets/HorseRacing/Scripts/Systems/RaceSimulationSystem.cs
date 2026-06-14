using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 賽事模擬系統（PRD §9）。分三階段模擬比賽，每階段各自計算速度並累積位移。
    /// 每段速度 = Base + Hidden + Condition + Track + 該段事件 + 該段隨機浮動。
    /// 最終名次依總累積位移決定；同距時馬號較小者勝出（Tie Break）。
    /// </summary>
    public static class RaceSimulationSystem
    {
        public const int StageCount = 3;

        /// <summary>每段隨機浮動範圍 [-StageFluctuation, +StageFluctuation]。</summary>
        private const int StageFluctuation = 2;

        /// <summary>最後一段（衝刺段）浮動範圍。</summary>
        private const int SprintFluctuation = 3;

        public static RaceResult Simulate(
            List<Horse> horses,
            TrackType track,
            TrackConfig trackCfg,
            EventDatabase events,
            IRandom rng,
            List<ProtectionCardDefinition> protections)
        {
            int count = horses.Count;

            // 重置並套用賽道修正
            for (int i = 0; i < count; i++) horses[i].ResetForRace();
            TrackSystem.ApplyTrackModifiers(horses, track, trackCfg);

            var result = new RaceResult { Track = track };
            result.StagePositions = new float[StageCount][];

            // 累積位移（每匹馬）
            var cumulative = new float[count];

            // 三階段逐段模擬
            for (int stage = 1; stage <= StageCount; stage++)
            {
                // 1. 該階段事件
                var logs = EventSystem.ResolveStage(stage, horses, events, rng, protections);
                result.Events.AddRange(logs);

                // 2. 計算該段每匹馬的速度並累積
                int fluctRange = (stage == StageCount) ? SprintFluctuation : StageFluctuation;
                int fluctFactor = (stage == StageCount) ? 10 : 5;

                for (int i = 0; i < count; i++)
                {
                    // 該段的事件修正（只算這一段的）
                    int stageEventMod = 0;
                    foreach (var log in logs)
                    {
                        if (log.HorseId == horses[i].Id && !log.Defended)
                            stageEventMod += log.SpeedModifier;
                    }

                    // 該段隨機浮動
                    int fluctuation = rng.Range(-fluctRange, fluctRange + 1);

                    // 該段速度
                    float stageSpeed = horses[i].BaseSpeed + horses[i].HiddenBonus
                        + horses[i].ConditionBonus + horses[i].TrackModifier
                        + stageEventMod + fluctuation * fluctFactor;

                    // 確保速度不為負
                    if (stageSpeed < 1) stageSpeed = 1;

                    cumulative[i] += stageSpeed;
                }

                // 記錄該段結束後的累積位移
                result.StagePositions[stage - 1] = new float[count];
                for (int i = 0; i < count; i++)
                    result.StagePositions[stage - 1][i] = cumulative[i];
            }

            // 依最終累積位移排名（同距：馬號小者勝）
            var ranked = new List<int>();
            for (int i = 0; i < count; i++) ranked.Add(i);
            ranked.Sort((a, b) =>
            {
                int cmp = cumulative[b].CompareTo(cumulative[a]);
                return cmp != 0 ? cmp : horses[a].Id.CompareTo(horses[b].Id);
            });

            result.RankToHorseId = new int[count];
            for (int i = 0; i < ranked.Count; i++)
            {
                int horseIdx = ranked[i];
                result.Standings.Add(new HorseRaceResult
                {
                    HorseId = horses[horseIdx].Id,
                    FinalSpeed = (int)cumulative[horseIdx],
                    Rank = i + 1
                });
                result.RankToHorseId[i] = horses[horseIdx].Id;
            }

            // 把事件修正加回 Horse 的 StageEventModifiers（維持相容性）
            for (int i = 0; i < count; i++)
            {
                horses[i].StageEventModifiers.Clear();
                int totalMod = 0;
                foreach (var e in result.Events)
                    if (e.HorseId == horses[i].Id && !e.Defended)
                        totalMod += e.SpeedModifier;
                horses[i].StageEventModifiers.Add(totalMod);
            }

            return result;
        }
    }
}
