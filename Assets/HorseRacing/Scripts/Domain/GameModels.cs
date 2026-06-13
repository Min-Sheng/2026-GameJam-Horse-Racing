using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>消息卡（PRD §4）：揭露某匹馬的模糊狀態描述。</summary>
    [System.Serializable]
    public class MessageCard
    {
        public int HorseId;
        public string Description;
        public int Round; // 於第幾輪（0..2）收到
    }

    /// <summary>單一匹馬的當前賠率（PRD §5）。Odds = 含本金的派彩倍率。</summary>
    [System.Serializable]
    public class HorseOdds
    {
        public int HorseId;
        public int Rank;     // 依 InitialScore 排名（1 = 最被看好）
        public float WinOdds; // 獨贏倍率（含本金），會隨下注輪次變差
    }

    /// <summary>一筆投注（PRD §10）。</summary>
    [System.Serializable]
    public class Bet
    {
        public BetType Type;
        public long Amount;
        public int[] HorseIds;       // 選擇的馬；Exacta/Trifecta 順序有意義
        public int Round;            // 下注輪次 0..2
        public float PayoutMultiplier; // 下注當下鎖定的派彩倍率（含本金）

        public Bet() { }
        public Bet(BetType type, long amount, int[] horseIds, int round, float payoutMultiplier)
        {
            Type = type; Amount = amount; HorseIds = horseIds; Round = round; PayoutMultiplier = payoutMultiplier;
        }
    }

    /// <summary>分析師情報（PRD §7）。Statements 內含真實與誤導混合。</summary>
    [System.Serializable]
    public class AnalystReport
    {
        public AnalystTier Tier;
        public List<string> Statements = new List<string>();
    }

    /// <summary>某階段觸發的事件紀錄（PRD §8/§9）。</summary>
    [System.Serializable]
    public class StageEventLog
    {
        public int Stage;          // 1..3
        public int HorseId;
        public string EventName;
        public int SpeedModifier;  // 實際套用的修正（被防禦時為 0）
        public bool Defended;      // 是否被防禦卡擋下（PRD §11）
    }

    /// <summary>單匹馬的比賽結果。</summary>
    [System.Serializable]
    public class HorseRaceResult
    {
        public int HorseId;
        public int FinalSpeed;
        public int Rank; // 1-based 名次
    }

    /// <summary>整場比賽結果（PRD §9/§12）。</summary>
    [System.Serializable]
    public class RaceResult
    {
        public TrackType Track;
        public List<HorseRaceResult> Standings = new List<HorseRaceResult>(); // 依名次排序
        public List<StageEventLog> Events = new List<StageEventLog>();

        /// <summary>名次 → 馬號（rank 1..N），rankToHorse[0] = 第一名馬號。</summary>
        public int[] RankToHorseId;

        public int GetRankOfHorse(int horseId)
        {
            foreach (var s in Standings) if (s.HorseId == horseId) return s.Rank;
            return -1;
        }
    }

    /// <summary>玩家狀態（資金與持有的防禦卡，PRD §11/§12）。</summary>
    [System.Serializable]
    public class PlayerState
    {
        public long Money;
        public readonly List<ProtectionCardDefinition> ProtectionCards = new List<ProtectionCardDefinition>();

        public PlayerState(long startingMoney) { Money = startingMoney; }
    }
}
