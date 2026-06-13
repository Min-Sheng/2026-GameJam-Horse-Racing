using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.Tests
{
    /// <summary>可腳本化的隨機源，供確定性測試使用。</summary>
    public class FakeRandom : IRandom
    {
        private readonly Queue<float> _values;
        private readonly Queue<int> _nexts;
        public bool IdentityShuffle = true; // 預設不打亂，保留輸入順序

        public FakeRandom(float[] values = null, int[] nexts = null)
        {
            _values = new Queue<float>(values ?? new float[0]);
            _nexts = new Queue<int>(nexts ?? new int[0]);
        }

        public int Next(int maxExclusive) => _nexts.Count > 0 ? _nexts.Dequeue() % System.Math.Max(1, maxExclusive) : 0;
        public int Range(int minInclusive, int maxExclusive) => _nexts.Count > 0 ? _nexts.Dequeue() : minInclusive;
        public float Value() => _values.Count > 0 ? _values.Dequeue() : 0f;

        public void Shuffle<T>(IList<T> list)
        {
            if (IdentityShuffle) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }

    /// <summary>建立測試用設定資產（不落地，純記憶體）。</summary>
    public static class ConfigFactory
    {
        public static GameConfig Game(int count = 8, int baseSpeed = 30)
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.horseCount = count; c.baseSpeed = baseSpeed;
            var pool = new int[count];
            for (int i = 0; i < count; i++) pool[i] = i; // 0..count-1
            c.hiddenBonusPool = pool;
            c.startingMoney = 1000; c.minBetAmount = 50;
            return c;
        }

        public static OddsConfig Odds()
        {
            var c = ScriptableObject.CreateInstance<OddsConfig>();
            c.baseRankOdds = new float[] { 2.0f, 2.6f, 3.4f, 4.5f, 6.0f, 8.0f, 11.0f, 15.0f };
            c.roundPayoutMultiplier = new float[] { 1.0f, 0.9f, 0.8f };
            c.minOdds = 1.2f;
            return c;
        }

        public static TrackConfig Track()
        {
            var c = ScriptableObject.CreateInstance<TrackConfig>();
            void AddTrack(TrackType t, string n) { var ti = new TrackConfig.TrackInfo { type = t, displayName = n }; c.tracks.Add(ti); }
            AddTrack(TrackType.Grass, "草地"); AddTrack(TrackType.Mud, "泥地"); AddTrack(TrackType.Snow, "雪地");
            int[,] pref = { {1,1,-1},{-2,2,0},{1,-1,1},{2,0,-2},{-1,1,1},{0,-2,2},{1,1,-1},{0,-2,2} };
            for (int i = 0; i < 8; i++)
                c.preferences.Add(new TrackConfig.HorsePreference { grass = pref[i,0], mud = pref[i,1], snow = pref[i,2] });
            return c;
        }

        public static BettingConfig Betting()
        {
            var c = ScriptableObject.CreateInstance<BettingConfig>();
            c.bettingRounds = 3;
            void Add(BetType t, string n, float m, int sel, bool ord) =>
                c.betTypes.Add(new BettingConfig.BetTypeEntry { type = t, displayName = n, payoutMultiplier = m, selectionCount = sel, ordered = ord });
            Add(BetType.Win, "獨贏", 3f, 1, false);
            Add(BetType.Place, "位置", 1.5f, 1, false);
            Add(BetType.Quinella, "連贏", 5f, 2, false);
            Add(BetType.Exacta, "正連贏", 8f, 2, true);
            Add(BetType.Trio, "三重彩", 15f, 3, false);
            Add(BetType.Trifecta, "三連單", 30f, 3, true);
            return c;
        }

        public static EventDefinition Event(string name, float chance, int mod, EventTarget target = EventTarget.RandomSingleHorse)
        {
            var e = ScriptableObject.CreateInstance<EventDefinition>();
            e.eventName = name; e.displayName = name; e.triggerChance = chance; e.speedModifier = mod; e.target = target;
            return e;
        }

        public static EventDatabase Events(params EventDefinition[] defs)
        {
            var db = ScriptableObject.CreateInstance<EventDatabase>();
            db.events.AddRange(defs);
            return db;
        }

        public static ProtectionCardDefinition Card(string name, EventDefinition target, float defend, long price = 150)
        {
            var c = ScriptableObject.CreateInstance<ProtectionCardDefinition>();
            c.cardName = name; c.targetEvent = target; c.defendChance = defend; c.price = price;
            return c;
        }

        public static ShopConfig Shop(int maxHeld, params ProtectionCardDefinition[] cards)
        {
            var s = ScriptableObject.CreateInstance<ShopConfig>();
            s.maxHeldCards = maxHeld; s.availableCards.AddRange(cards);
            return s;
        }

        /// <summary>建立指定隱藏加成的馬匹清單（馬號 1..n）。</summary>
        public static List<Horse> Horses(int baseSpeed, params int[] hiddenBonuses)
        {
            var list = new List<Horse>();
            for (int i = 0; i < hiddenBonuses.Length; i++)
                list.Add(new Horse { Id = i + 1, BaseSpeed = baseSpeed, HiddenBonus = hiddenBonuses[i] });
            return list;
        }
    }
}
