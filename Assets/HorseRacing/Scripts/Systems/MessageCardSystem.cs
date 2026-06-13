using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 消息卡系統（PRD §4）：玩家分三輪，每輪收到一張卡、揭露一匹（不重複）馬的模糊狀態。
    /// </summary>
    public static class MessageCardSystem
    {
        /// <summary>
        /// 抽取 rounds 張卡（預設 3），每張對應不同馬、標記所屬輪次。
        /// </summary>
        public static List<MessageCard> DrawCards(List<Horse> horses, MessageCardConfig cfg, IRandom rng, int rounds = 3)
        {
            var pool = new List<Horse>(horses);
            rng.Shuffle(pool);

            int n = System.Math.Min(rounds, pool.Count);
            var cards = new List<MessageCard>(n);
            for (int i = 0; i < n; i++)
            {
                var h = pool[i];
                cards.Add(new MessageCard
                {
                    HorseId = h.Id,
                    Description = cfg.GetDescription(h.HiddenBonus),
                    Round = i
                });
            }
            return cards;
        }
    }
}
