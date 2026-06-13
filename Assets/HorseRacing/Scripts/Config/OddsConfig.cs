using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 賠率設定（PRD §5）。賠率公式獨立於程式碼之外、可由管理者調整。
    /// 每匹馬依 InitialScore 排名取得 baseRankOdds[rank-1]；
    /// 三次下注輪次套用 roundPayoutMultiplier（逐輪變差）。
    /// </summary>
    [CreateAssetMenu(fileName = "OddsConfig", menuName = "HorseRacing/Odds Config")]
    public class OddsConfig : ScriptableObject
    {
        [Header("各排名基礎獨贏倍率（含本金，index 0 = 第1名/最被看好）")]
        [Tooltip("長度需 >= 馬匹數；數值越前越低（莊家優勢內含）")]
        public float[] baseRankOdds = { 2.0f, 2.6f, 3.4f, 4.5f, 6.0f, 8.0f, 11.0f, 15.0f };

        [Header("三次下注輪次的賠率係數（PRD §5：逐輪變差）")]
        [Tooltip("index 0/1/2 = 第1/2/3 次下注；數值越小派彩越差")]
        public float[] roundPayoutMultiplier = { 1.0f, 0.9f, 0.8f };

        [Tooltip("賠率下限（含本金）")] public float minOdds = 1.2f;

        public float GetWinOdds(int rankIndex, int round)
        {
            float baseOdds = (rankIndex >= 0 && rankIndex < baseRankOdds.Length)
                ? baseRankOdds[rankIndex]
                : baseRankOdds[baseRankOdds.Length - 1];
            float mult = (round >= 0 && round < roundPayoutMultiplier.Length)
                ? roundPayoutMultiplier[round]
                : roundPayoutMultiplier[roundPayoutMultiplier.Length - 1];
            return Mathf.Max(minOdds, baseOdds * mult);
        }
    }
}
