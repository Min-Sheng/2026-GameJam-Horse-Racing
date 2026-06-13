using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 隨機事件系統（PRD §8/§9）。每階段獨立判定每個事件是否觸發；
    /// 命中時挑選目標馬並套用速度修正。若玩家持有對應防禦卡，先進行防禦判定
    /// （PRD §11）：嘗試使用即消耗該卡，成功則完全抵銷。
    /// </summary>
    public static class EventSystem
    {
        /// <summary>
        /// 解析單一階段的所有事件。
        /// </summary>
        /// <param name="protections">玩家持有的防禦卡；使用後會自此清單移除（傳入 PlayerState.ProtectionCards）。</param>
        public static List<StageEventLog> ResolveStage(
            int stage,
            List<Horse> horses,
            EventDatabase db,
            IRandom rng,
            List<ProtectionCardDefinition> protections)
        {
            var logs = new List<StageEventLog>();
            if (db == null || db.events == null) return logs;

            for (int e = 0; e < db.events.Count; e++)
            {
                var def = db.events[e];
                if (def == null) continue;
                if (rng.Value() >= def.triggerChance) continue; // 未觸發

                // 選擇目標馬
                var targets = SelectTargets(def, horses, rng);
                for (int t = 0; t < targets.Count; t++)
                {
                    var horse = targets[t];
                    bool defended = TryDefend(def, rng, protections);
                    int applied = defended ? 0 : def.speedModifier;
                    if (!defended) horse.StageEventModifiers.Add(def.speedModifier);

                    logs.Add(new StageEventLog
                    {
                        Stage = stage,
                        HorseId = horse.Id,
                        EventName = def.displayName,
                        SpeedModifier = applied,
                        Defended = defended
                    });
                }
            }
            return logs;
        }

        private static List<Horse> SelectTargets(EventDefinition def, List<Horse> horses, IRandom rng)
        {
            var list = new List<Horse>();
            if (horses.Count == 0) return list;
            if (def.target == EventTarget.AllHorses)
                list.AddRange(horses);
            else
                list.Add(horses[rng.Next(horses.Count)]);
            return list;
        }

        /// <summary>嘗試以持有的防禦卡防禦此事件；使用即消耗。回傳是否成功防禦。</summary>
        private static bool TryDefend(EventDefinition def, IRandom rng, List<ProtectionCardDefinition> protections)
        {
            if (protections == null) return false;
            for (int i = 0; i < protections.Count; i++)
            {
                var card = protections[i];
                if (card != null && card.targetEvent == def)
                {
                    protections.RemoveAt(i); // 消耗（嘗試即用掉）
                    return rng.Value() < card.defendChance;
                }
            }
            return false;
        }
    }
}
