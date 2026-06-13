using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 主設定資料庫（PRD §14）：彙整所有子設定，GameManager 由此單一入口載入。
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfigDatabase", menuName = "HorseRacing/Game Config Database")]
    public class GameConfigDatabase : ScriptableObject
    {
        public GameConfig game;
        public MessageCardConfig messageCards;
        public OddsConfig odds;
        public TrackConfig track;
        public EventDatabase events;
        public AnalystConfig analyst;
        public BettingConfig betting;
        public ShopConfig shop;
    }
}
