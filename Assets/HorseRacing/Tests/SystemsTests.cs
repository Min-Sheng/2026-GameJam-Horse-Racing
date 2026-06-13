using System.Collections.Generic;
using NUnit.Framework;

namespace HorseRacing.Tests
{
    public class HorseSystemTests
    {
        [Test]
        public void GenerateHorses_ProducesUniqueBonusPermutation()
        {
            var cfg = ConfigFactory.Game(8, 30);
            var rng = new SystemRandom(12345);
            var horses = HorseSystem.GenerateHorses(cfg, rng);

            Assert.AreEqual(8, horses.Count);
            var seen = new HashSet<int>();
            foreach (var h in horses)
            {
                Assert.AreEqual(30, h.BaseSpeed);
                Assert.IsTrue(h.HiddenBonus >= 0 && h.HiddenBonus <= 7);
                Assert.IsTrue(seen.Add(h.HiddenBonus), "隱藏加成必須唯一");
            }
            Assert.AreEqual(8, seen.Count, "0..7 應全數出現一次");
        }

        [Test]
        public void GenerateHorses_AssignsSequentialIds()
        {
            var horses = HorseSystem.GenerateHorses(ConfigFactory.Game(8, 30), new SystemRandom(1));
            for (int i = 0; i < horses.Count; i++) Assert.AreEqual(i + 1, horses[i].Id);
        }
    }

    public class OddsSystemTests
    {
        [Test]
        public void ComputeOdds_RanksByScore_TieBreakLowerId()
        {
            // H1=35, H2=37, H3=37(同分→馬號小者較前)
            var horses = ConfigFactory.Horses(30, 5, 7, 7);
            var odds = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 0);

            Assert.AreEqual(2, odds[0].HorseId); // rank1: H2 (37, id較小)
            Assert.AreEqual(3, odds[1].HorseId); // rank2: H3 (37)
            Assert.AreEqual(1, odds[2].HorseId); // rank3: H1 (35)
        }

        [Test]
        public void ComputeOdds_OddsWorsenAcrossRounds()
        {
            var horses = ConfigFactory.Horses(30, 7, 0);
            float r0 = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 0)[0].WinOdds;
            float r1 = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 1)[0].WinOdds;
            float r2 = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 2)[0].WinOdds;
            Assert.Greater(r0, r1);
            Assert.Greater(r1, r2);
        }
    }

    public class TrackSystemTests
    {
        [Test]
        public void ApplyTrackModifiers_WritesPerHorseModifier()
        {
            var horses = ConfigFactory.Horses(30, 0, 0, 0, 0);
            TrackSystem.ApplyTrackModifiers(horses, TrackType.Mud, ConfigFactory.Track());
            Assert.AreEqual(1, horses[0].TrackModifier);  // H1 Mud +1
            Assert.AreEqual(2, horses[1].TrackModifier);  // H2 Mud +2
            Assert.AreEqual(-1, horses[2].TrackModifier);  // H3 Mud -1
            Assert.AreEqual(0, horses[3].TrackModifier);  // H4 Mud +0
        }
    }

    public class MessageCardSystemTests
    {
        [Test]
        public void DrawCards_ThreeDistinctHorses_WithMappedDescriptions()
        {
            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++) mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "B" + i });
            var horses = ConfigFactory.Horses(30, 0, 1, 2, 3, 4, 5, 6, 7);

            var rng = new FakeRandom(); // identity shuffle → 取前三匹
            var cards = MessageCardSystem.DrawCards(horses, mc, rng, 3);

            Assert.AreEqual(3, cards.Count);
            var ids = new HashSet<int>();
            foreach (var c in cards) Assert.IsTrue(ids.Add(c.HorseId), "揭露的馬不可重複");
            Assert.AreEqual("B0", cards[0].Description); // H1 bonus0
            Assert.AreEqual(0, cards[0].Round);
            Assert.AreEqual(2, cards[2].Round);
        }
    }

    public class RaceSimulationTests
    {
        [Test]
        public void Simulate_NoEvents_RanksByFinalSpeed()
        {
            // 無事件（空事件庫）：FinalSpeed = Base + Hidden + Track
            var horses = ConfigFactory.Horses(30, 0, 1, 2, 3);
            var rng = new FakeRandom();
            var result = RaceSimulationSystem.Simulate(horses, TrackType.Grass, ConfigFactory.Track(),
                ConfigFactory.Events(), rng, new List<ProtectionCardDefinition>());

            // Grass 修正: H1+1 H2-2 H3+1 H4+2 → final: H1=31,H2=29,H3=33,H4=35
            Assert.AreEqual(4, result.RankToHorseId[0]); // 35
            Assert.AreEqual(3, result.RankToHorseId[1]); // 33
            Assert.AreEqual(1, result.RankToHorseId[2]); // 31
            Assert.AreEqual(2, result.RankToHorseId[3]); // 29
        }

        [Test]
        public void Simulate_TieBreak_LowerIdWins()
        {
            // 兩匹同最終速度：草地 H1(30+0+1=31) 與 H7(30+0+1=31) 同分 → 馬號小者勝
            var horses = ConfigFactory.Horses(30, 0, -10, -10, -10, -10, -10, 0, -10);
            var result = RaceSimulationSystem.Simulate(horses, TrackType.Grass, ConfigFactory.Track(),
                ConfigFactory.Events(), new FakeRandom(), new List<ProtectionCardDefinition>());
            Assert.AreEqual(1, result.RankToHorseId[0]); // H1 早於 H7
            Assert.AreEqual(7, result.RankToHorseId[1]);
        }
    }

    public class EventSystemTests
    {
        [Test]
        public void ResolveStage_EventAlwaysTriggers_AppliesModifier()
        {
            var horses = ConfigFactory.Horses(30, 0, 0);
            var ev = ConfigFactory.Event("Slip", 1f, -2, EventTarget.AllHorses);
            var db = ConfigFactory.Events(ev);
            var logs = EventSystem.ResolveStage(1, horses, db, new FakeRandom(), new List<ProtectionCardDefinition>());

            Assert.AreEqual(2, logs.Count);
            Assert.AreEqual(-2, horses[0].EventModifierTotal);
            Assert.IsFalse(logs[0].Defended);
        }

        [Test]
        public void ResolveStage_ProtectionCard_DefendsAndIsConsumed()
        {
            var horses = ConfigFactory.Horses(30, 0);
            var ev = ConfigFactory.Event("Slip", 1f, -2, EventTarget.RandomSingleHorse);
            var db = ConfigFactory.Events(ev);
            var card = ConfigFactory.Card("AntiSlip", ev, 1f); // 必定防禦
            var held = new List<ProtectionCardDefinition> { card };

            var logs = EventSystem.ResolveStage(1, horses, db, new FakeRandom(new float[] { 0f, 0f }), held);

            Assert.AreEqual(1, logs.Count);
            Assert.IsTrue(logs[0].Defended);
            Assert.AreEqual(0, horses[0].EventModifierTotal, "防禦成功不應扣速度");
            Assert.AreEqual(0, held.Count, "防禦卡應被消耗");
        }

        [Test]
        public void ResolveStage_ZeroChance_NeverTriggers()
        {
            var horses = ConfigFactory.Horses(30, 0, 0);
            var db = ConfigFactory.Events(ConfigFactory.Event("Slip", 0f, -2, EventTarget.AllHorses));
            var logs = EventSystem.ResolveStage(1, horses, db, new FakeRandom(new float[] { 0.99f, 0.99f }), null);
            Assert.AreEqual(0, logs.Count);
        }
    }

    public class BettingSystemTests
    {
        private RaceResult MakeResult(params int[] rankToHorse)
        {
            var r = new RaceResult { RankToHorseId = rankToHorse };
            for (int i = 0; i < rankToHorse.Length; i++)
                r.Standings.Add(new HorseRaceResult { HorseId = rankToHorse[i], Rank = i + 1 });
            return r;
        }

        [Test]
        public void Win_PaysWhenHorseIsFirst()
        {
            var result = MakeResult(5, 2, 7, 1);
            var bet = new Bet(BetType.Win, 100, new[] { 5 }, 0, 3f);
            Assert.IsTrue(BettingSystem.IsWin(bet, result));
            Assert.AreEqual(300, BettingSystem.SettleBet(bet, result));

            var lose = new Bet(BetType.Win, 100, new[] { 2 }, 0, 3f);
            Assert.AreEqual(0, BettingSystem.SettleBet(lose, result));
        }

        [Test]
        public void Place_PaysWhenInTopThree()
        {
            var result = MakeResult(5, 2, 7, 1);
            Assert.IsTrue(BettingSystem.IsWin(new Bet(BetType.Place, 100, new[] { 7 }, 0, 1.5f), result));
            Assert.IsFalse(BettingSystem.IsWin(new Bet(BetType.Place, 100, new[] { 1 }, 0, 1.5f), result));
        }

        [Test]
        public void QuinellaVsExacta_OrderMatters()
        {
            var result = MakeResult(5, 2, 7, 1);
            // 前兩名 = {5,2}
            Assert.IsTrue(BettingSystem.IsWin(new Bet(BetType.Quinella, 100, new[] { 2, 5 }, 0, 5f), result)); // 不分順序
            Assert.IsFalse(BettingSystem.IsWin(new Bet(BetType.Exacta, 100, new[] { 2, 5 }, 0, 8f), result)); // 順序錯
            Assert.IsTrue(BettingSystem.IsWin(new Bet(BetType.Exacta, 100, new[] { 5, 2 }, 0, 8f), result));  // 順序對
        }

        [Test]
        public void TrioVsTrifecta_OrderMatters()
        {
            var result = MakeResult(5, 2, 7, 1);
            Assert.IsTrue(BettingSystem.IsWin(new Bet(BetType.Trio, 100, new[] { 7, 5, 2 }, 0, 15f), result));
            Assert.IsFalse(BettingSystem.IsWin(new Bet(BetType.Trifecta, 100, new[] { 7, 5, 2 }, 0, 30f), result));
            Assert.IsTrue(BettingSystem.IsWin(new Bet(BetType.Trifecta, 100, new[] { 5, 2, 7 }, 0, 30f), result));
            Assert.AreEqual(3000, BettingSystem.SettleBet(new Bet(BetType.Trifecta, 100, new[] { 5, 2, 7 }, 0, 30f), result));
        }

        [Test]
        public void CreateBet_Win_LocksDynamicOdds()
        {
            var horses = ConfigFactory.Horses(30, 7, 0); // H1 favorite
            var odds = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 0);
            var bet = BettingSystem.CreateBet(BetType.Win, 100, new[] { 1 }, 0, ConfigFactory.Betting(), odds);
            Assert.AreEqual(2.0f, bet.PayoutMultiplier, 0.001f); // rank1 baseRankOdds[0]
        }
    }

    public class ShopAndSettlementTests
    {
        [Test]
        public void Buy_DeductsMoney_RespectsMaxHeld()
        {
            var player = new PlayerState(1000);
            var ev = ConfigFactory.Event("Slip", 1f, -2);
            var card = ConfigFactory.Card("AntiSlip", ev, 0.5f, 150);
            var shop = ConfigFactory.Shop(3, card);

            Assert.IsTrue(ShopSystem.Buy(player, card, shop));
            Assert.AreEqual(850, player.Money);
            Assert.IsTrue(ShopSystem.Buy(player, card, shop));
            Assert.IsTrue(ShopSystem.Buy(player, card, shop));
            Assert.IsFalse(ShopSystem.Buy(player, card, shop), "超過最大持有數應失敗");
            Assert.AreEqual(3, player.ProtectionCards.Count);
            Assert.AreEqual(550, player.Money);
        }

        [Test]
        public void Buy_FailsWhenInsufficientFunds()
        {
            var player = new PlayerState(100);
            var card = ConfigFactory.Card("Pricey", ConfigFactory.Event("Slip", 1f, -2), 0.5f, 150);
            Assert.IsFalse(ShopSystem.Buy(player, card, ConfigFactory.Shop(3, card)));
            Assert.AreEqual(100, player.Money);
        }

        [Test]
        public void Settle_AddsPayoutsAndComputesNet()
        {
            var player = new PlayerState(1000); // 本金已於下注時扣除
            var result = new RaceResult { RankToHorseId = new[] { 5, 2, 7, 1 } };
            for (int i = 0; i < 4; i++) result.Standings.Add(new HorseRaceResult { HorseId = result.RankToHorseId[i], Rank = i + 1 });

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 100, new[] { 5 }, 0, 3f),  // 贏 → 300
                new Bet(BetType.Win, 100, new[] { 2 }, 0, 3f),  // 輸 → 0
            };
            var s = SettlementSystem.Settle(player, bets, result);
            Assert.AreEqual(200, s.TotalStaked);
            Assert.AreEqual(300, s.TotalPayout);
            Assert.AreEqual(100, s.Net);
            Assert.AreEqual(1300, player.Money);
        }
    }
}
