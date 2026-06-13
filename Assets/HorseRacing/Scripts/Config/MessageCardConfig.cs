using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 消息卡文字對照（PRD §4）：隱藏加成值 → 玩家可讀的模糊描述。
    /// 文字內容支援後台（Inspector）自由編輯。
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

        public List<Entry> entries = new List<Entry>();

        /// <summary>取得某加成值的描述；找不到時回傳預設字串。</summary>
        public string GetDescription(int bonus)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].bonus == bonus) return entries[i].description;
            return "狀態不明";
        }
    }
}
