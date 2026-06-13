namespace HorseRacing
{
    /// <summary>賽道型別（PRD §6）。</summary>
    public enum TrackType
    {
        Grass, // 草地
        Mud,   // 泥地
        Snow   // 雪地
    }

    /// <summary>投注型別（PRD §10）。</summary>
    public enum BetType
    {
        Win,       // 獨贏：第一名
        Place,     // 位置：前三名
        Quinella,  // 連贏：前兩名不分順序
        Exacta,    // 正連贏：前兩名順序正確
        Trio,      // 三重彩：前三名不分順序
        Trifecta   // 三連單：前三名順序正確
    }

    /// <summary>隨機事件影響目標（PRD §8）。</summary>
    public enum EventTarget
    {
        RandomSingleHorse, // 隨機一匹馬
        AllHorses          // 全部馬匹
    }

    /// <summary>分析師等級（PRD §7）。</summary>
    public enum AnalystTier
    {
        Junior, // 初級分析師：價格較低，正確率 M%
        Senior  // 資深分析師：價格較高，正確率 N%
    }
}
