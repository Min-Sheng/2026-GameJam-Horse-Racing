using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HorseRacing.Tests
{
    /// <summary>
    /// Verifies multi-round state persistence in GameManager (Task 7.5).
    /// Requirements: 20.1, 20.2, 20.3, 20.4
    /// </summary>
    public class GameManagerMultiRoundTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGM");
            _gm = _go.AddComponent<GameManager>();
            _gm.config = ConfigFactory.FullConfig(totalRounds: 5, startingMoney: 3000, minBet: 50);
            _gm.randomSeed = 42; // deterministic

            // In EditMode tests, Awake() may not be called automatically.
            // Use reflection to invoke it so _rng and Player are initialized.
            var awakeMethod = typeof(GameManager).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null)
                awakeMethod.Invoke(_gm, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// Simulates a full round: Betting → Racing → Settlement → Shop,
        /// then returns the state for assertions.
        /// </summary>
        private void PlayFullRound(long betAmount = 100, BetType betType = BetType.Win)
        {
            // Ensure in Betting phase
            Assert.AreEqual(GamePhase.Betting, _gm.Phase,
                "Expected Betting phase at start of PlayFullRound");

            // Place a bet (use horse 1 for simplicity)
            if (betAmount > 0 && _gm.Player.Money >= betAmount)
            {
                _gm.PlaceBet(betType, betAmount, new[] { 1 });
            }

            // Advance through all betting rounds
            for (int i = 0; i < _gm.BettingRounds - 1; i++)
            {
                _gm.ConfirmBettingRound();
            }

            // Last ConfirmBettingRound triggers StartRace
            _gm.ConfirmBettingRound();
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "Expected Racing phase after last betting round");

            // Complete race and settle
            _gm.CompleteRaceAndSettle();

            // If not game over, proceed to Shop
            if (_gm.Phase == GamePhase.Settlement)
            {
                _gm.EnterShop();
                Assert.AreEqual(GamePhase.Shop, _gm.Phase, "Expected Shop phase");
            }
        }

        // ====================================================================
        // Requirement 20.1: PlayerState.Money persists across rounds
        // ====================================================================

        [Test]
        public void MoneyPersistsAcrossRounds_Req20_1()
        {
            _gm.StartNewRound();
            long initialMoney = _gm.Player.Money;
            Assert.AreEqual(3000, initialMoney);

            // Place a bet and play through
            _gm.PlaceBet(BetType.Win, 100, new[] { 1 });
            long afterBet = _gm.Player.Money;
            Assert.AreEqual(2900, afterBet, "Money should decrease after bet");

            // Advance to racing
            for (int i = 0; i < _gm.BettingRounds - 1; i++)
                _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // triggers StartRace

            // Complete settlement
            _gm.CompleteRaceAndSettle();
            long afterSettlement = _gm.Player.Money;

            // Go to shop
            _gm.EnterShop();

            // Start next round
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "Should be in Betting for round 2");

            // Verify money persists from the settlement result
            Assert.AreEqual(afterSettlement, _gm.Player.Money,
                "Req 20.1: Money must persist across rounds (from settlement to next round)");
        }

        [Test]
        public void MoneyPersistsAcrossMultipleRounds_Req20_1()
        {
            _gm.StartNewRound();

            // Track money through multiple rounds
            long moneyAfterRound1;
            long moneyAfterRound2;

            // Round 1
            PlayFullRound(betAmount: 100);
            moneyAfterRound1 = _gm.Player.Money;

            // Start Round 2
            _gm.NextRound();
            Assert.AreEqual(moneyAfterRound1, _gm.Player.Money,
                "Money at start of round 2 must equal money at end of round 1");

            // Round 2
            PlayFullRound(betAmount: 200);
            moneyAfterRound2 = _gm.Player.Money;

            // Start Round 3
            if (_gm.Phase != GamePhase.GameOver)
            {
                _gm.NextRound();
                Assert.AreEqual(moneyAfterRound2, _gm.Player.Money,
                    "Money at start of round 3 must equal money at end of round 2");
            }
        }

        // ====================================================================
        // Requirement 20.1: ProtectionCards persist across rounds
        // ====================================================================

        [Test]
        public void ProtectionCardsPersistAcrossRounds_Req20_1()
        {
            _gm.StartNewRound();

            // Play through round 1 without betting (just advance)
            PlayFullRound(betAmount: 0);

            // Buy a protection card in shop
            var shopCard = _gm.config.shop.availableCards[0];
            bool bought = _gm.BuyProtectionCard(shopCard);
            Assert.IsTrue(bought, "Should be able to buy a protection card in shop");
            Assert.AreEqual(1, _gm.Player.ProtectionCards.Count, "Should have 1 card after purchase");
            var heldCard = _gm.Player.ProtectionCards[0];

            // Start next round
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase);

            // Verify protection card persists
            Assert.AreEqual(1, _gm.Player.ProtectionCards.Count,
                "Req 20.1: ProtectionCards must persist across rounds");
            Assert.AreEqual(heldCard, _gm.Player.ProtectionCards[0],
                "The same card instance must persist");
        }

        [Test]
        public void MultipleProtectionCardsPersistAcrossRounds_Req20_1()
        {
            _gm.StartNewRound();
            PlayFullRound(betAmount: 0);

            // Buy two protection cards
            var shopCard = _gm.config.shop.availableCards[0];
            _gm.BuyProtectionCard(shopCard);
            _gm.BuyProtectionCard(shopCard);
            Assert.AreEqual(2, _gm.Player.ProtectionCards.Count);

            // Next round
            _gm.NextRound();
            Assert.AreEqual(2, _gm.Player.ProtectionCards.Count,
                "All purchased protection cards must persist into the next round");
        }

        // ====================================================================
        // Requirement 20.2: New RoundContext is created each round
        // ====================================================================

        [Test]
        public void NewRoundContextCreatedEachRound_Req20_2()
        {
            _gm.StartNewRound();
            var round1Ctx = _gm.Round;
            var round1Horses = new List<Horse>(round1Ctx.Horses);
            var round1Odds = new List<HorseOdds>(round1Ctx.CurrentOdds);
            var round1Cards = new List<MessageCard>(round1Ctx.AllCards);
            var round1Track = round1Ctx.Track;

            Assert.IsNotNull(round1Ctx);
            Assert.AreEqual(8, round1Horses.Count);
            Assert.IsTrue(round1Odds.Count > 0);
            Assert.IsTrue(round1Cards.Count > 0);

            // Play through round 1
            PlayFullRound(betAmount: 0);
            _gm.NextRound();

            // Verify Round 2 has a new RoundContext
            var round2Ctx = _gm.Round;
            Assert.IsNotNull(round2Ctx);
            Assert.AreNotSame(round1Ctx, round2Ctx,
                "Req 20.2: A new RoundContext must be created each round");

            // Verify fresh horses (new instances)
            Assert.AreEqual(8, round2Ctx.Horses.Count, "Should have 8 horses in new round");
            // Horses should be fresh instances (not the same object references)
            for (int i = 0; i < round2Ctx.Horses.Count; i++)
            {
                Assert.AreNotSame(round1Horses[i], round2Ctx.Horses[i],
                    $"Horse {i + 1} should be a new instance in the new round");
            }

            // Verify fresh odds
            Assert.IsTrue(round2Ctx.CurrentOdds.Count > 0, "Should have odds in new round");
            Assert.AreNotSame(round1Odds, round2Ctx.CurrentOdds,
                "Odds list should be a new instance");

            // Verify fresh cards
            Assert.IsTrue(round2Ctx.AllCards.Count > 0, "Should have cards in new round");
            Assert.AreNotSame(round1Cards, round2Ctx.AllCards,
                "Cards list should be a new instance");

            // Verify bets are empty in new round
            Assert.AreEqual(0, round2Ctx.Bets.Count,
                "Bets should be empty at start of new round");

            // Verify CurrentBettingRound resets
            Assert.AreEqual(0, round2Ctx.CurrentBettingRound,
                "CurrentBettingRound should reset to 0 in new round");
        }

        [Test]
        public void RoundContextHasFreshHorsesWithResetState_Req20_2()
        {
            _gm.StartNewRound();

            // Play through round 1 with events that modify horses
            // After the race, horses will have TrackModifier and StageEventModifiers set
            PlayFullRound(betAmount: 50);

            // Start next round
            _gm.NextRound();

            // Verify new round horses are clean (no leftover state from previous race)
            foreach (var horse in _gm.Round.Horses)
            {
                // New horses are generated fresh from HorseSystem.GenerateHorses,
                // so their StageEventModifiers should be empty and TrackModifier should be 0
                Assert.AreEqual(0, horse.TrackModifier,
                    $"Horse {horse.Id} TrackModifier should be 0 in fresh round");
                Assert.AreEqual(0, horse.StageEventModifiers.Count,
                    $"Horse {horse.Id} StageEventModifiers should be empty in fresh round");
            }
        }

        // ====================================================================
        // Requirement 20.3: RoundNumber increments correctly
        // ====================================================================

        [Test]
        public void RoundNumberIncrementsCorrectly_Req20_3()
        {
            Assert.AreEqual(0, _gm.RoundNumber, "RoundNumber should start at 0 before first round");

            _gm.StartNewRound();
            Assert.AreEqual(1, _gm.RoundNumber, "RoundNumber should be 1 after first StartNewRound");

            PlayFullRound(betAmount: 0);
            _gm.NextRound();
            Assert.AreEqual(2, _gm.RoundNumber, "RoundNumber should be 2 after second StartNewRound");

            PlayFullRound(betAmount: 0);
            _gm.NextRound();
            Assert.AreEqual(3, _gm.RoundNumber, "RoundNumber should be 3 after third StartNewRound");
        }

        [Test]
        public void RoundNumberIncrementsSequentiallyUpToTotalRounds_Req20_3()
        {
            // Set totalRounds to 3 for faster test
            _gm.config.game.totalRounds = 3;

            _gm.StartNewRound();
            Assert.AreEqual(1, _gm.RoundNumber);

            PlayFullRound(betAmount: 0);
            _gm.NextRound(); // Round 2
            Assert.AreEqual(2, _gm.RoundNumber);

            PlayFullRound(betAmount: 0);
            _gm.NextRound(); // Round 3
            Assert.AreEqual(3, _gm.RoundNumber);

            PlayFullRound(betAmount: 0);
            _gm.NextRound(); // Should trigger game over (totalRounds = 3, already played 3)

            // After 3 rounds, StartNewRound should detect totalRounds reached
            Assert.AreEqual(GamePhase.GameOver, _gm.Phase,
                "Should be GameOver after totalRounds reached");
        }

        // ====================================================================
        // Requirement 20.4: Consumed protection cards remain consumed
        // ====================================================================

        [Test]
        public void ConsumedProtectionCardsRemainConsumed_Req20_4()
        {
            // Create a config with an event that always triggers
            var ev = ConfigFactory.Event("Slip", 1f, -2, EventTarget.RandomSingleHorse);
            var card = ConfigFactory.Card("AntiSlip", ev, 1f, 100); // 100% defend chance
            _gm.config.events = ConfigFactory.Events(ev);
            _gm.config.shop = ConfigFactory.Shop(3, card);

            _gm.StartNewRound();
            PlayFullRound(betAmount: 0);

            // Buy a protection card in shop
            _gm.BuyProtectionCard(card);
            Assert.AreEqual(1, _gm.Player.ProtectionCards.Count, "Should have 1 card after purchase");

            // Start next round - the event will trigger and consume the card during race
            _gm.NextRound();
            Assert.AreEqual(1, _gm.Player.ProtectionCards.Count,
                "Card should still be held at start of round (before race)");

            // Place a bet and advance to race where the event should trigger and consume the card
            _gm.PlaceBet(BetType.Win, 50, new[] { 1 });
            for (int i = 0; i < _gm.BettingRounds - 1; i++)
                _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // triggers StartRace → Simulate uses protections

            // After race, the card should be consumed (event always triggers, card matches event)
            Assert.AreEqual(0, _gm.Player.ProtectionCards.Count,
                "Req 20.4: Protection card should be consumed during race");

            // Complete settlement and go to shop
            _gm.CompleteRaceAndSettle();
            if (_gm.Phase == GamePhase.Settlement)
                _gm.EnterShop();

            // Verify card remains consumed
            Assert.AreEqual(0, _gm.Player.ProtectionCards.Count,
                "Consumed card should remain consumed in shop phase");

            // Start another round
            if (_gm.Phase == GamePhase.Shop)
                _gm.NextRound();

            if (_gm.Phase != GamePhase.GameOver)
            {
                // Card must still be consumed in the subsequent round
                Assert.AreEqual(0, _gm.Player.ProtectionCards.Count,
                    "Req 20.4: Consumed card must remain consumed in subsequent rounds");
            }
        }

        [Test]
        public void UnconsumedCardsRemainWhileConsumedAreGone_Req20_4()
        {
            // Create two different events and corresponding cards
            var ev1 = ConfigFactory.Event("Slip", 1f, -2, EventTarget.RandomSingleHorse);
            var ev2 = ConfigFactory.Event("Stumble", 0f, -3, EventTarget.RandomSingleHorse); // never triggers
            var card1 = ConfigFactory.Card("AntiSlip", ev1, 1f, 100);
            var card2 = ConfigFactory.Card("AntiStumble", ev2, 1f, 100);

            _gm.config.events = ConfigFactory.Events(ev1); // only ev1 triggers
            _gm.config.shop = ConfigFactory.Shop(3, card1, card2);

            _gm.StartNewRound();
            PlayFullRound(betAmount: 0);

            // Buy both cards
            _gm.BuyProtectionCard(card1);
            _gm.BuyProtectionCard(card2);
            Assert.AreEqual(2, _gm.Player.ProtectionCards.Count);

            // Next round: ev1 triggers (consuming card1), ev2 never triggers (card2 preserved)
            _gm.NextRound();
            Assert.AreEqual(2, _gm.Player.ProtectionCards.Count,
                "Both cards should exist before the race");

            // Race simulation will consume card1 (matches ev1 which always triggers)
            _gm.PlaceBet(BetType.Win, 50, new[] { 1 });
            for (int i = 0; i < _gm.BettingRounds - 1; i++)
                _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // triggers race

            // card1 should be consumed (ev1 always triggers), card2 should remain (ev2 never triggers)
            Assert.AreEqual(1, _gm.Player.ProtectionCards.Count,
                "Only card2 should remain (card1 consumed by ev1)");
            Assert.AreEqual(card2, _gm.Player.ProtectionCards[0],
                "The remaining card should be card2 (ev2 never triggered)");

            // Continue to next round
            _gm.CompleteRaceAndSettle();
            if (_gm.Phase == GamePhase.Settlement)
                _gm.EnterShop();
            if (_gm.Phase == GamePhase.Shop)
                _gm.NextRound();

            if (_gm.Phase != GamePhase.GameOver)
            {
                // card1 remains consumed, card2 persists
                Assert.AreEqual(1, _gm.Player.ProtectionCards.Count,
                    "Req 20.4: Consumed card1 stays consumed, card2 persists into round 3");
                Assert.AreEqual(card2, _gm.Player.ProtectionCards[0]);
            }
        }

        // ====================================================================
        // Combined: PlayerState reference persists while RoundContext is replaced
        // ====================================================================

        [Test]
        public void PlayerStatePersistsWhileRoundContextIsReplaced()
        {
            _gm.StartNewRound();
            var playerRef = _gm.Player;
            var round1Ref = _gm.Round;

            PlayFullRound(betAmount: 0);
            _gm.NextRound();

            // PlayerState is the same object reference across rounds
            Assert.AreSame(playerRef, _gm.Player,
                "PlayerState should be the same instance across rounds (persistent)");

            // RoundContext is a different object reference
            Assert.AreNotSame(round1Ref, _gm.Round,
                "RoundContext should be a NEW instance each round");
        }
    }
}
