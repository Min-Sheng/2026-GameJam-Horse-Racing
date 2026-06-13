using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 投注設定（PRD §10）。六種投注的派彩倍率（含本金）皆由 Config 管理，不得寫死。
    /// 註：獨贏(Win)實際派彩採用 OddsSystem 的動態每馬賠率；此處倍率作為其餘玩法與顯示基準。
    /// </summary>
    [CreateAssetMenu(fileName = "BettingConfig", menuName = "HorseRacing/Betting Config")]
    public class BettingConfig : ScriptableObject
    {
        [System.Serializable]
        public class BetTypeEntry
        {
            public BetType type;
            public string displayName;
            [Tooltip("派彩倍率（含本金）")] public float payoutMultiplier;
            [Tooltip("需要選擇的馬匹數量")] public int selectionCount = 1;
            [Tooltip("選擇是否需依順序（Exacta/Trifecta = true）")] public bool ordered;
        }

        [Tooltip("下注輪次數量（PRD §2：三次下注）")] public int bettingRounds = 3;

        public List<BetTypeEntry> betTypes = new List<BetTypeEntry>();

        public BetTypeEntry Get(BetType type)
        {
            for (int i = 0; i < betTypes.Count; i++)
                if (betTypes[i].type == type) return betTypes[i];
            return null;
        }
    }
}
