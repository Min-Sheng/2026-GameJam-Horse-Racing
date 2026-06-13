using UnityEngine;

namespace HorseRacing
{
    /// <summary>分析師設定（PRD §7）。價格與正確率皆可由管理介面調整，規則 N > M。</summary>
    [CreateAssetMenu(fileName = "AnalystConfig", menuName = "HorseRacing/Analyst Config")]
    public class AnalystConfig : ScriptableObject
    {
        [Header("初級分析師（M%）")]
        public long juniorPrice = 100;
        [Range(0f, 1f)] public float juniorAccuracy = 0.55f;

        [Header("資深分析師（N% > M%）")]
        public long seniorPrice = 300;
        [Range(0f, 1f)] public float seniorAccuracy = 0.85f;

        [Header("情報內容")]
        [Tooltip("每份情報產生的陳述條數")] public int statementsPerReport = 3;

        public long GetPrice(AnalystTier tier) => tier == AnalystTier.Senior ? seniorPrice : juniorPrice;
        public float GetAccuracy(AnalystTier tier) => tier == AnalystTier.Senior ? seniorAccuracy : juniorAccuracy;
    }
}
