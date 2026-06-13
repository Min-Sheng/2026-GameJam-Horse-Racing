using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>馬匹系統（PRD §3）：產生 N 匹馬並唯一分配隱藏加成。</summary>
    public static class HorseSystem
    {
        /// <summary>
        /// 產生馬匹。每匹馬固定 BaseSpeed；隱藏加成池洗牌後唯一分配
        /// （每值僅出現一次、每匹馬僅取得一個）。
        /// </summary>
        public static List<Horse> GenerateHorses(GameConfig cfg, IRandom rng)
        {
            int count = cfg.horseCount;
            var pool = new List<int>(cfg.hiddenBonusPool);
            // 池不足時補 0，確保每匹馬都有值
            while (pool.Count < count) pool.Add(0);
            rng.Shuffle(pool);

            var horses = new List<Horse>(count);
            for (int i = 0; i < count; i++)
            {
                horses.Add(new Horse
                {
                    Id = i + 1,
                    BaseSpeed = cfg.baseSpeed,
                    HiddenBonus = pool[i]
                });
            }
            return horses;
        }
    }
}
