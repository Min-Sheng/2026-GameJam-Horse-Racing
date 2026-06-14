using UnityEngine;

namespace HorseRacing
{
    /// <summary>全域基礎設定（PRD §3）。所有數值皆可於 Inspector 調整，不得 Hardcode。</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "HorseRacing/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("馬匹（PRD §3）")]
        [Tooltip("每場馬匹數量")] public int horseCount = 8;
        [Tooltip("所有馬匹固定基礎速度")] public int baseSpeed = 100;

        [Tooltip("隱藏加成池：隨機唯一分配給每匹馬，每值僅出現一次")]
        public int[] hiddenBonusPool = { 0, 1, 2, 3, 4, 5, 6, 7 };

        [Header("資金")]
        [Tooltip("玩家起始資金")] public long startingMoney = 3000;
        [Tooltip("單筆下注最小金額")] public long minBetAmount = 50;

        [Header("遊戲規則")]
        [Tooltip("總回合數；0 = 無限制")]
        public int totalRounds = 5;

        [Tooltip("每場比賽的階段數（PRD §10：每階段獨立判定事件）")]
        public int stageCount = 3;
    }
}
