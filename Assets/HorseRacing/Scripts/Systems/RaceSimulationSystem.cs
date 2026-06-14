using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 賽事模擬系統（PRD §9）。套用賽道修正後，依設定的階段數判定事件，
    /// 以 FinalSpeed = Base + Hidden + Condition + Track + Σ(Stage1..N 事件) 決定名次；
    /// 同速時馬號較小者勝出（Tie Break）。
    /// 另外記錄每階段的確定性累積位移（StagePositions）供賽事動畫使用，
    /// 最後一段恰好等於 FinalSpeed，確保動畫終點順序與名次一致。
    /// </summary>
    public static class RaceSimulationSystem
    {
        /// <summary>預設階段數（當未提供 GameConfig 時的後備值）。</summary>
        public const int DefaultStageCount = 3;

        public static RaceResult Simulate(
            List<Horse> horses,
            TrackType track,
            TrackConfig trackCfg,
            EventDatabase events,
            IRandom rng,
            List<ProtectionCardDefinition> protections,
            GameConfig gameCfg = null)
        {
            int stageCount = gameCfg != null ? gameCfg.stageCount : DefaultStageCount;
            int count = horses.Count;

            // 重置並套用賽道修正
            for (int i = 0; i < count; i++) horses[i].ResetForRace();
            TrackSystem.ApplyTrackModifiers(horses, track, trackCfg);

            var result = new RaceResult { Track = track };
            result.StagePositions = new float[stageCount][];

            // N 階段事件
            for (int stage = 1; stage <= stageCount; stage++)
            {
                var logs = EventSystem.ResolveStage(stage, horses, events, rng, protections);
                result.Events.AddRange(logs);

                // 記錄該階段結束後的確定性累積位移（供賽事動畫使用）。
                // partialScore = Base+Hidden+Condition+Track+目前已發生事件，
                // 乘上階段進度 (stage/stageCount)，使最後一段恰好等於 FinalSpeed，
                // 保證動畫終點順序與最終名次一致。
                result.StagePositions[stage - 1] = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float partialScore = horses[i].BaseSpeed + horses[i].HiddenBonus
                        + horses[i].ConditionBonus + horses[i].TrackModifier
                        + horses[i].EventModifierTotal;
                    float pos = partialScore * stage / stageCount;
                    if (pos < 0) pos = 0;
                    result.StagePositions[stage - 1][i] = pos;
                }
            }

            // 依最終速度排名（同速：馬號小者勝）
            var ranked = new List<Horse>(horses);
            ranked.Sort((a, b) =>
            {
                int cmp = b.FinalSpeed.CompareTo(a.FinalSpeed);
                return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
            });

            result.RankToHorseId = new int[ranked.Count];
            for (int i = 0; i < ranked.Count; i++)
            {
                result.Standings.Add(new HorseRaceResult
                {
                    HorseId = ranked[i].Id,
                    FinalSpeed = ranked[i].FinalSpeed,
                    Rank = i + 1
                });
                result.RankToHorseId[i] = ranked[i].Id;
            }
            return result;
        }
    }
}
