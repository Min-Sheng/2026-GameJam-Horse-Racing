using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 一匹馬的當局狀態。所有數值由系統生成；玩家不可直接看見 HiddenBonus。
    /// </summary>
    [System.Serializable]
    public class Horse
    {
        public int Id;            // 1..N（馬號）
        public int BaseSpeed;     // 基礎速度（固定，PRD §3 = 30）
        public int HiddenBonus;   // 隱藏加成 0..7（PRD §3，玩家不可見）
        public int ConditionBonus;// 當日狀態隨機浮動（每回合隨機 -3 ~ +3）
        public int TrackModifier; // 賽道修正（開賽公布賽道後套用，PRD §6）

        /// <summary>各階段事件對此馬的速度修正紀錄（PRD §9，三階段）。</summary>
        public readonly List<int> StageEventModifiers = new List<int>();

        /// <summary>初始排序與賠率用分數（賽道/事件公布前）：Base + Hidden + Condition。</summary>
        public int InitialScore => BaseSpeed + HiddenBonus + ConditionBonus;

        /// <summary>所有事件修正加總。</summary>
        public int EventModifierTotal
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < StageEventModifiers.Count; i++) sum += StageEventModifiers[i];
                return sum;
            }
        }

        /// <summary>最終速度：Base + Hidden + Condition + Track + Σ(Stage1..3 事件)。</summary>
        public int FinalSpeed => BaseSpeed + HiddenBonus + ConditionBonus + TrackModifier + EventModifierTotal;

        public void ResetForRace()
        {
            TrackModifier = 0;
            StageEventModifiers.Clear();
        }
    }
}
