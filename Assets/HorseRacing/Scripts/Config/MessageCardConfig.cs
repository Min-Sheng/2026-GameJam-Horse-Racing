using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 消息卡文字對照（PRD §4）：隱藏加成值 → 玩家可讀的模糊描述。
    /// 文字內容支援後台（Inspector）自由編輯。
    /// entries 應覆蓋 GameConfig.hiddenBonusPool 中所有值（預設 0..7）。
    /// </summary>
    [CreateAssetMenu(fileName = "MessageCardConfig", menuName = "HorseRacing/Message Card Config")]
    public class MessageCardConfig : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public int bonus;            // 對應的隱藏加成值
            [TextArea] public string description;
        }

        [Tooltip("隱藏加成值 → 模糊描述對照表。應覆蓋 hiddenBonusPool 所有值（預設 0..7）。")]
        public List<Entry> entries = new List<Entry>();

        [Tooltip("當加成值未在 entries 中找到對應時使用的預設描述")]
        public string fallbackDescription = "狀態不明";

        /// <summary>取得某加成值的描述；找不到時回傳 fallbackDescription。</summary>
        public string GetDescription(int bonus)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].bonus == bonus) return entries[i].description;
            return fallbackDescription;
        }
    }
}
