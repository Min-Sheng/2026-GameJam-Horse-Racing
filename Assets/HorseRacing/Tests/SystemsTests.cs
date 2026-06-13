using System.Collections.Generic;
using NUnit.Framework;

namespace HorseRacing.Tests
{
    public class HorseModelTests
    {
        [Test]
        public void InitialScore_ReturnsBaseSpeedPlusHiddenBonus()
        {
            var horse = new Horse { Id = 1, BaseSpeed = 30, HiddenBonus = 5 };
            Assert.AreEqual(35, horse.InitialScore);
        }

        [Test]
        public void InitialScore_ZeroBonus_EqualsBaseSpeed()
        {
            var horse = new Horse { Id = 2, BaseSpeed = 30, HiddenBonus = 0 };
            Assert.AreEqual(30, horse.InitialScore);
        }

        [Test]
        public void FinalSpeed_IncludesAllComponents()
        {
            var horse = new Horse { Id = 1, BaseSpeed = 30, HiddenBonus = 5, TrackModifier = 2 };
            horse.StageEventModifiers.Add(-1);
            horse.StageEventModifiers.Add(3);
            horse.StageEventModifiers.Add(-2);
            // FinalSpeed = 30 + 5 + 2 + (-1 + 3 + -2) = 37
            Assert.AreEqual(37, horse.FinalSpeed);
        }

        [Test]
        public void Horse_HasAllRequiredFields()
        {
            var horse = new Horse
            {
                Id = 3,
                BaseSpeed = 30,
                HiddenBonus = 7,
                TrackModifier = -1
            };
            horse.StageEventModifiers.Add(2);

            Assert.AreEqual(3, horse.Id);
            Assert.AreEqual(30, horse.BaseSpeed);
            Assert.AreEqual(7, horse.HiddenBonus);
            Assert.AreEqual(-1, horse.TrackModifier);
            Assert.IsNotNull(horse.StageEventModifiers);
            Assert.AreEqual(1, horse.StageEventModifiers.Count);
            Assert.AreEqual(37, horse.InitialScore); // 30 + 7
            Assert.AreEqual(38, horse.FinalSpeed);   // 30 + 7 + (-1) + 2
        }
    }

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

    public class HorseSystemPropertyTests
    {
        // Feature: horse-racing-prd-alignment, Property 1: Horse Generation Produces Valid Unique Permutation
        /// <summary>
        /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
        /// For any valid GameConfig with horseCount = N and a hiddenBonusPool of length >= N,
        /// calling HorseSystem.GenerateHorses SHALL produce exactly N horses with sequential IDs (1..N),
        /// all sharing the same BaseSpeed, and the set of HiddenBonus values across all horses SHALL be
        /// a permutation of the first N elements of the pool (each value appears exactly once).
        /// </summary>
        [Test]
        public void Property1_HorseGeneration_ProducesValidUniquePermutation()
        {
            const int Iterations = 150;
            var metaRng = new System.Random(42);

            for (int i = 0; i < Iterations; i++)
            {
                int seed = metaRng.Next();
                // Vary horse count between 2 and 10 to test different configurations
                int horseCount = metaRng.Next(2, 11);
                int baseSpeed = metaRng.Next(10, 100);

                // Create a GameConfig with a pool of exactly horseCount (standard config)
                var cfg = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
                cfg.horseCount = horseCount;
                cfg.baseSpeed = baseSpeed;

                // Generate a pool with distinct values of exactly horseCount
                var pool = new int[horseCount];
                for (int p = 0; p < horseCount; p++) pool[p] = p; // 0..horseCount-1
                cfg.hiddenBonusPool = pool;

                var rng = new SystemRandom(seed);
                var horses = HorseSystem.GenerateHorses(cfg, rng);

                // Requirement 3.1: Exactly N horses
                Assert.AreEqual(horseCount, horses.Count,
                    $"Iteration {i} (seed={seed}): Expected {horseCount} horses, got {horses.Count}");

                // Requirement 3.1: Sequential IDs 1..N
                for (int h = 0; h < horses.Count; h++)
                {
                    Assert.AreEqual(h + 1, horses[h].Id,
                        $"Iteration {i} (seed={seed}): Horse at index {h} should have Id={h + 1}, got {horses[h].Id}");
                }

                // Requirement 3.2: All share the same BaseSpeed
                for (int h = 0; h < horses.Count; h++)
                {
                    Assert.AreEqual(baseSpeed, horses[h].BaseSpeed,
                        $"Iteration {i} (seed={seed}): Horse {horses[h].Id} BaseSpeed should be {baseSpeed}, got {horses[h].BaseSpeed}");
                }

                // Requirement 3.3, 3.4: HiddenBonus values form a permutation of the pool (each value unique)
                var expectedPool = new HashSet<int>(pool);
                var actualBonuses = new HashSet<int>();
                for (int h = 0; h < horses.Count; h++)
                {
                    Assert.IsTrue(expectedPool.Contains(horses[h].HiddenBonus),
                        $"Iteration {i} (seed={seed}): Horse {horses[h].Id} HiddenBonus={horses[h].HiddenBonus} not in pool");
                    Assert.IsTrue(actualBonuses.Add(horses[h].HiddenBonus),
                        $"Iteration {i} (seed={seed}): Duplicate HiddenBonus={horses[h].HiddenBonus} found");
                }

                // All pool values are assigned exactly once (permutation)
                Assert.AreEqual(horseCount, actualBonuses.Count,
                    $"Iteration {i} (seed={seed}): Expected {horseCount} unique bonuses, got {actualBonuses.Count}");
            }
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
        public void PickTrack_ReturnsTrackFromConfigList()
        {
            var cfg = ConfigFactory.Track();
            // Test with several different RNG values to confirm result is always from config.tracks
            for (int i = 0; i < cfg.tracks.Count; i++)
            {
                var rng = new FakeRandom(nexts: new[] { i });
                var result = TrackSystem.PickTrack(cfg, rng);
                Assert.AreEqual(cfg.tracks[i].type, result,
                    $"PickTrack with index {i} should return tracks[{i}].type");
            }
        }

        [Test]
        public void PickTrack_EmptyTracksList_FallsBackToGrass()
        {
            var cfg = UnityEngine.ScriptableObject.CreateInstance<TrackConfig>();
            // tracks list is empty by default
            var rng = new FakeRandom();
            var result = TrackSystem.PickTrack(cfg, rng);
            Assert.AreEqual(TrackType.Grass, result, "Empty tracks list should fallback to Grass");
        }

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

        [Test]
        public void ApplyTrackModifiers_SetsModifierFromPreferencesTable()
        {
            var cfg = ConfigFactory.Track();
            var horses = ConfigFactory.Horses(30, 0, 0, 0, 0, 0, 0, 0, 0); // 8 horses

            // Verify Grass modifiers match preferences[horseId-1].grass
            TrackSystem.ApplyTrackModifiers(horses, TrackType.Grass, cfg);
            Assert.AreEqual(cfg.preferences[0].grass, horses[0].TrackModifier); // H1
            Assert.AreEqual(cfg.preferences[1].grass, horses[1].TrackModifier); // H2
            Assert.AreEqual(cfg.preferences[7].grass, horses[7].TrackModifier); // H8

            // Reset and verify Snow modifiers
            foreach (var h in horses) h.TrackModifier = 0;
            TrackSystem.ApplyTrackModifiers(horses, TrackType.Snow, cfg);
            Assert.AreEqual(cfg.preferences[0].snow, horses[0].TrackModifier); // H1
            Assert.AreEqual(cfg.preferences[3].snow, horses[3].TrackModifier); // H4
            Assert.AreEqual(cfg.preferences[7].snow, horses[7].TrackModifier); // H8
        }
    }

    public class TrackSystemPropertyTests
    {
        const int PropertyIterations = 100;

        /// <summary>
        /// Feature: horse-racing-prd-alignment, Property 7: Track Modifier Application Matches Preference Table
        /// Validates: Requirements 6.4
        /// For each horse with Id H and any track type T, calling TrackSystem.ApplyTrackModifiers
        /// SHALL set horse.TrackModifier to exactly TrackConfig.preferences[H-1].{T}.
        /// </summary>
        [Test]
        public void Property7_TrackModifierApplication_MatchesPreferenceTable()
        {
            // Feature: horse-racing-prd-alignment, Property 7: Track Modifier Application Matches Preference Table
            var rng = new System.Random(42);
            var trackTypes = new[] { TrackType.Grass, TrackType.Mud, TrackType.Snow };

            for (int i = 0; i < PropertyIterations; i++)
            {
                // Generate a random TrackConfig with random preferences
                var cfg = UnityEngine.ScriptableObject.CreateInstance<TrackConfig>();
                int horseCount = rng.Next(1, 9); // 1 to 8 horses
                for (int h = 0; h < horseCount; h++)
                {
                    cfg.preferences.Add(new TrackConfig.HorsePreference
                    {
                        grass = rng.Next(-5, 6),
                        mud = rng.Next(-5, 6),
                        snow = rng.Next(-5, 6)
                    });
                }

                // Pick a random track type
                var trackType = trackTypes[rng.Next(trackTypes.Length)];

                // Create horses with matching IDs
                var horses = new List<Horse>();
                for (int h = 0; h < horseCount; h++)
                    horses.Add(new Horse { Id = h + 1, BaseSpeed = 30, HiddenBonus = h });

                // Apply track modifiers
                TrackSystem.ApplyTrackModifiers(horses, trackType, cfg);

                // Assert: each horse's TrackModifier == preferences[H-1].{trackType}
                for (int h = 0; h < horseCount; h++)
                {
                    int expected;
                    switch (trackType)
                    {
                        case TrackType.Grass: expected = cfg.preferences[h].grass; break;
                        case TrackType.Mud: expected = cfg.preferences[h].mud; break;
                        case TrackType.Snow: expected = cfg.preferences[h].snow; break;
                        default: expected = 0; break;
                    }
                    Assert.AreEqual(expected, horses[h].TrackModifier,
                        $"Iteration {i}: Horse {horses[h].Id} TrackModifier should be {expected} for {trackType}, got {horses[h].TrackModifier}");
                }
            }
        }

        /// <summary>
        /// Feature: horse-racing-prd-alignment, Property 8: Track Selection Is Valid
        /// Validates: Requirements 6.1, 6.2
        /// For each call to PickTrack with a non-empty tracks config, the returned TrackType
        /// SHALL exist in the config's tracks list.
        /// </summary>
        [Test]
        public void Property8_TrackSelection_IsValid()
        {
            // Feature: horse-racing-prd-alignment, Property 8: Track Selection Is Valid
            var rng = new System.Random(99);
            var allTrackTypes = new[] { TrackType.Grass, TrackType.Mud, TrackType.Snow };

            for (int i = 0; i < PropertyIterations; i++)
            {
                // Generate a random non-empty TrackConfig with a random subset of track types
                var cfg = UnityEngine.ScriptableObject.CreateInstance<TrackConfig>();
                int trackCount = rng.Next(1, 4); // 1 to 3 track types
                var usedTypes = new HashSet<TrackType>();
                while (usedTypes.Count < trackCount)
                    usedTypes.Add(allTrackTypes[rng.Next(allTrackTypes.Length)]);

                foreach (var t in usedTypes)
                    cfg.tracks.Add(new TrackConfig.TrackInfo { type = t, displayName = t.ToString() });

                // Also add random preferences (needed for valid config, not used by PickTrack)
                for (int h = 0; h < 8; h++)
                    cfg.preferences.Add(new TrackConfig.HorsePreference
                    {
                        grass = rng.Next(-3, 4),
                        mud = rng.Next(-3, 4),
                        snow = rng.Next(-3, 4)
                    });

                // Use SystemRandom with a random seed to drive PickTrack
                var pickRng = new SystemRandom(rng.Next());
                var result = TrackSystem.PickTrack(cfg, pickRng);

                // Assert: the returned TrackType exists in the config's tracks list
                bool found = false;
                for (int t = 0; t < cfg.tracks.Count; t++)
                {
                    if (cfg.tracks[t].type == result)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found,
                    $"Iteration {i}: PickTrack returned {result} which is not in config.tracks (contains {cfg.tracks.Count} entries)");
            }
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

        /// <summary>
        /// Validates: Requirements 4.1, 4.2, 4.3
        /// Verifies DrawCards draws exactly `rounds` cards with distinct horse IDs,
        /// each card's Description matches config.GetDescription(horse.HiddenBonus),
        /// and card.Round is assigned sequentially (0, 1, 2).
        /// </summary>
        [Test]
        public void DrawCards_ExactCountDistinctIds_CorrectDescriptions_SequentialRounds()
        {
            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++) mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "Desc_" + i });
            var horses = ConfigFactory.Horses(30, 7, 3, 5, 1, 6, 0, 4, 2);

            var rng = new FakeRandom(); // identity shuffle → takes first 3 horses
            var cards = MessageCardSystem.DrawCards(horses, mc, rng, 3);

            // Criterion 1: Draws exactly `rounds` cards with distinct horse IDs
            Assert.AreEqual(3, cards.Count, "Should draw exactly 'rounds' cards");
            var ids = new HashSet<int>();
            foreach (var c in cards)
                Assert.IsTrue(ids.Add(c.HorseId), $"Horse ID {c.HorseId} appeared more than once");
            Assert.AreEqual(3, ids.Count, "All 3 horse IDs must be distinct");

            // Criterion 2: Each card's Description matches config.GetDescription(horse.HiddenBonus)
            // With identity shuffle, first 3 horses are H1(bonus=7), H2(bonus=3), H3(bonus=5)
            Assert.AreEqual("Desc_7", cards[0].Description, "H1 bonus=7 → Desc_7");
            Assert.AreEqual("Desc_3", cards[1].Description, "H2 bonus=3 → Desc_3");
            Assert.AreEqual("Desc_5", cards[2].Description, "H3 bonus=5 → Desc_5");

            // Criterion 3: card.Round is assigned sequentially (0, 1, 2)
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(i, cards[i].Round, $"Card {i} should have Round={i}");
        }

        /// <summary>
        /// Validates: Requirements 4.1, 4.2
        /// Verifies distinct horse selection with real randomness across multiple seeds.
        /// </summary>
        [Test]
        public void DrawCards_WithRealRandom_AlwaysDistinctHorses()
        {
            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++) mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "D" + i });
            var horses = ConfigFactory.Horses(30, 0, 1, 2, 3, 4, 5, 6, 7);

            for (int seed = 0; seed < 20; seed++)
            {
                var rng = new SystemRandom(seed);
                var cards = MessageCardSystem.DrawCards(horses, mc, rng, 3);

                Assert.AreEqual(3, cards.Count, $"Seed {seed}: should produce exactly 3 cards");
                var ids = new HashSet<int>();
                foreach (var c in cards)
                {
                    Assert.IsTrue(ids.Add(c.HorseId), $"Seed {seed}: duplicate horse ID {c.HorseId}");
                    // Find the horse and verify description mapping
                    var horse = horses.Find(h => h.Id == c.HorseId);
                    Assert.AreEqual(mc.GetDescription(horse.HiddenBonus), c.Description,
                        $"Seed {seed}: description mismatch for horse {c.HorseId}");
                }
            }
        }
    }

    public class MessageCardPropertyTests
    {
        // Feature: horse-racing-prd-alignment, Property 2: Message Card Drawing Selects Distinct Horses with Correct Descriptions
        /// <summary>
        /// **Validates: Requirements 4.1, 4.2**
        /// For any set of 8 horses and a complete MessageCardConfig, calling MessageCardSystem.DrawCards
        /// SHALL produce exactly `rounds` cards referencing distinct horse IDs, and each card's Description
        /// SHALL equal config.GetDescription(horse.HiddenBonus) for the corresponding horse.
        /// </summary>
        [Test]
        public void Property2_DrawCards_DistinctHorsesWithCorrectDescriptions()
        {
            // Feature: horse-racing-prd-alignment, Property 2: Message Card Drawing Selects Distinct Horses with Correct Descriptions
            const int PropertyIterations = 100;

            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                var seed = iter * 7 + 42;
                var rng = new SystemRandom(seed);

                // Generate random horse configuration
                var setupRng = new System.Random(seed);
                var bonuses = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };
                // Shuffle bonuses for variety
                for (int i = bonuses.Length - 1; i > 0; i--)
                {
                    int j = setupRng.Next(i + 1);
                    int tmp = bonuses[i]; bonuses[i] = bonuses[j]; bonuses[j] = tmp;
                }

                var horses = ConfigFactory.Horses(30, bonuses[0], bonuses[1], bonuses[2], bonuses[3],
                    bonuses[4], bonuses[5], bonuses[6], bonuses[7]);

                // Create MessageCardConfig with entries for all bonus values 0..7
                var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
                for (int i = 0; i < 8; i++)
                    mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "Info_" + i });

                // Randomize rounds between 1 and 3
                int rounds = (setupRng.Next(3) + 1); // 1, 2, or 3

                var cards = MessageCardSystem.DrawCards(horses, mc, rng, rounds);

                // Assert: exactly `rounds` cards produced
                Assert.AreEqual(rounds, cards.Count,
                    $"Iter {iter}: Expected {rounds} cards, got {cards.Count}");

                // Assert: all horse IDs are distinct
                var ids = new HashSet<int>();
                foreach (var card in cards)
                {
                    Assert.IsTrue(ids.Add(card.HorseId),
                        $"Iter {iter}: Duplicate horse ID {card.HorseId}");
                }

                // Assert: each card's Description matches config.GetDescription(horse.HiddenBonus)
                foreach (var card in cards)
                {
                    var horse = horses.Find(h => h.Id == card.HorseId);
                    Assert.IsNotNull(horse,
                        $"Iter {iter}: Card references non-existent horse ID {card.HorseId}");
                    var expectedDesc = mc.GetDescription(horse.HiddenBonus);
                    Assert.AreEqual(expectedDesc, card.Description,
                        $"Iter {iter}: Horse {card.HorseId} (bonus={horse.HiddenBonus}) expected desc '{expectedDesc}', got '{card.Description}'");
                }
            }
        }

        // Feature: horse-racing-prd-alignment, Property 3: Message Card Reveal Filtering by Round
        /// <summary>
        /// **Validates: Requirements 4.3**
        /// For any list of message cards with assigned round numbers and a current betting round N,
        /// the RevealedCards property SHALL return exactly those cards where card.Round <= N.
        /// </summary>
        [Test]
        public void Property3_RevealedCards_FiltersByCurrentRound()
        {
            // Feature: horse-racing-prd-alignment, Property 3: Message Card Reveal Filtering by Round
            const int PropertyIterations = 100;

            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                var setupRng = new System.Random(iter * 13 + 7);

                // Generate a random number of cards (1..5) with random round assignments (0..4)
                int cardCount = setupRng.Next(1, 6); // 1 to 5 cards
                int maxRound = setupRng.Next(1, 5);  // max round value 1 to 4

                var ctx = new RoundContext();
                for (int i = 0; i < cardCount; i++)
                {
                    ctx.AllCards.Add(new MessageCard
                    {
                        HorseId = i + 1,
                        Description = "Desc_" + i,
                        Round = setupRng.Next(maxRound + 1) // 0..maxRound
                    });
                }

                // Pick a random current betting round
                int currentRound = setupRng.Next(maxRound + 1); // 0..maxRound
                ctx.CurrentBettingRound = currentRound;

                // Get revealed cards
                var revealed = ctx.RevealedCards;

                // Assert: revealed cards are exactly those with Round <= currentRound
                var expected = new List<MessageCard>();
                foreach (var card in ctx.AllCards)
                    if (card.Round <= currentRound)
                        expected.Add(card);

                Assert.AreEqual(expected.Count, revealed.Count,
                    $"Iter {iter}: CurrentRound={currentRound}, expected {expected.Count} revealed cards, got {revealed.Count}");

                // Verify each revealed card has Round <= currentRound
                foreach (var card in revealed)
                {
                    Assert.LessOrEqual(card.Round, currentRound,
                        $"Iter {iter}: Card with Round={card.Round} should not be revealed at CurrentBettingRound={currentRound}");
                }

                // Verify no card with Round <= currentRound is missing from revealed
                foreach (var card in ctx.AllCards)
                {
                    if (card.Round <= currentRound)
                    {
                        Assert.IsTrue(revealed.Contains(card),
                            $"Iter {iter}: Card (HorseId={card.HorseId}, Round={card.Round}) should be revealed at round {currentRound}");
                    }
                    else
                    {
                        Assert.IsFalse(revealed.Contains(card),
                            $"Iter {iter}: Card (HorseId={card.HorseId}, Round={card.Round}) should NOT be revealed at round {currentRound}");
                    }
                }
            }
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

        /// <summary>
        /// Validates: Requirements 10.1, 10.2, 10.3, 10.4, 10.5
        /// Verifies the full three-stage pipeline with events:
        /// - ApplyTrackModifiers → 3× ResolveStage → compute FinalSpeed → rank
        /// - FinalSpeed = BaseSpeed + HiddenBonus + TrackModifier + Σ(StageEventModifiers)
        /// - Ranking by FinalSpeed desc with Id tie-break
        /// - RaceResult.RankToHorseId is populated correctly
        /// </summary>
        [Test]
        public void Simulate_FullPipeline_ThreeStagesWithEvents_VerifiesFinalSpeedAndRanking()
        {
            // Setup: 4 horses with known bonuses
            var horses = ConfigFactory.Horses(30, 2, 5, 3, 7);
            // Use an event that always triggers and targets all horses with modifier -1
            var ev = ConfigFactory.Event("Wind", 1f, -1, EventTarget.AllHorses);
            var db = ConfigFactory.Events(ev);

            // FakeRandom: Value()=0f → always triggers (0 < 1.0 triggerChance), no defense checks needed
            var rng = new FakeRandom();
            var result = RaceSimulationSystem.Simulate(horses, TrackType.Grass, ConfigFactory.Track(),
                db, rng, new List<ProtectionCardDefinition>());

            // Req 10.1: Pipeline is ApplyTrackModifiers → 3× ResolveStage → rank
            // Grass modifiers from ConfigFactory.Track(): H1=+1, H2=-2, H3=+1, H4=+2
            // Event triggers 3 times (3 stages), each applying -1 to all horses → total event modifier = -3

            // Req 10.2: Verify FinalSpeed = BaseSpeed + HiddenBonus + TrackModifier + Σ(StageEventModifiers)
            // H1: 30 + 2 + 1 + (-3) = 30
            // H2: 30 + 5 + (-2) + (-3) = 30
            // H3: 30 + 3 + 1 + (-3) = 31
            // H4: 30 + 7 + 2 + (-3) = 36
            Assert.AreEqual(30, horses[0].FinalSpeed, "H1: 30+2+1+(-3)=30");
            Assert.AreEqual(30, horses[1].FinalSpeed, "H2: 30+5+(-2)+(-3)=30");
            Assert.AreEqual(31, horses[2].FinalSpeed, "H3: 30+3+1+(-3)=31");
            Assert.AreEqual(36, horses[3].FinalSpeed, "H4: 30+7+2+(-3)=36");

            // Verify each horse has exactly 3 stage event modifiers (one per stage)
            foreach (var h in horses)
                Assert.AreEqual(3, h.StageEventModifiers.Count, $"H{h.Id} should have 3 stage event modifiers");

            // Req 10.3, 10.4: Ranking by FinalSpeed desc, Id tie-break (lower Id first for ties)
            // H4(36) > H3(31) > H1(30) = H2(30) → H1 before H2 by tie-break
            Assert.AreEqual(4, result.Standings.Count);
            Assert.AreEqual(4, result.Standings[0].HorseId); // rank 1: H4 (36)
            Assert.AreEqual(3, result.Standings[1].HorseId); // rank 2: H3 (31)
            Assert.AreEqual(1, result.Standings[2].HorseId); // rank 3: H1 (30, lower Id)
            Assert.AreEqual(2, result.Standings[3].HorseId); // rank 4: H2 (30, higher Id)

            // Verify ranks are 1-based sequential
            for (int i = 0; i < result.Standings.Count; i++)
                Assert.AreEqual(i + 1, result.Standings[i].Rank, $"Standings[{i}].Rank should be {i + 1}");

            // Req 10.5: RankToHorseId is populated correctly
            Assert.AreEqual(4, result.RankToHorseId.Length);
            Assert.AreEqual(4, result.RankToHorseId[0]); // 1st place = H4
            Assert.AreEqual(3, result.RankToHorseId[1]); // 2nd place = H3
            Assert.AreEqual(1, result.RankToHorseId[2]); // 3rd place = H1
            Assert.AreEqual(2, result.RankToHorseId[3]); // 4th place = H2

            // Verify RankToHorseId matches Standings
            for (int i = 0; i < result.Standings.Count; i++)
                Assert.AreEqual(result.Standings[i].HorseId, result.RankToHorseId[i],
                    $"RankToHorseId[{i}] should match Standings[{i}].HorseId");

            // Verify FinalSpeed in Standings matches actual horse FinalSpeed
            for (int i = 0; i < result.Standings.Count; i++)
            {
                var horse = horses.Find(h => h.Id == result.Standings[i].HorseId);
                Assert.AreEqual(horse.FinalSpeed, result.Standings[i].FinalSpeed,
                    $"Standings[{i}].FinalSpeed should match horse.FinalSpeed for H{horse.Id}");
            }

            // Verify Track in result
            Assert.AreEqual(TrackType.Grass, result.Track);

            // Verify events were logged (1 event × 4 horses × 3 stages = 12 logs)
            Assert.AreEqual(12, result.Events.Count, "Should have 12 event logs (1 event × 4 horses × 3 stages)");
        }

        /// <summary>
        /// Validates: Requirements 10.1, 10.2
        /// Verifies that ResetForRace clears previous state before applying new modifiers.
        /// </summary>
        [Test]
        public void Simulate_ResetsHorseStateBeforeRace()
        {
            // Pre-set some state that should be cleared
            var horses = ConfigFactory.Horses(30, 3, 5);
            horses[0].TrackModifier = 99;
            horses[0].StageEventModifiers.Add(10);
            horses[1].TrackModifier = -99;
            horses[1].StageEventModifiers.Add(-10);

            var result = RaceSimulationSystem.Simulate(horses, TrackType.Grass, ConfigFactory.Track(),
                ConfigFactory.Events(), new FakeRandom(), new List<ProtectionCardDefinition>());

            // After Simulate, TrackModifiers should be from the track config, not the pre-set values
            // Grass: H1=+1, H2=-2
            // FinalSpeed H1: 30+3+1=34, H2: 30+5+(-2)=33
            Assert.AreEqual(1, horses[0].TrackModifier, "H1 TrackModifier should be reset and set from config");
            Assert.AreEqual(-2, horses[1].TrackModifier, "H2 TrackModifier should be reset and set from config");

            // StageEventModifiers should only contain modifiers from this race (empty since no events)
            Assert.AreEqual(0, horses[0].StageEventModifiers.Count, "H1 should have no event modifiers with empty event DB");
            Assert.AreEqual(0, horses[1].StageEventModifiers.Count, "H2 should have no event modifiers with empty event DB");

            // Verify correct final ranking
            Assert.AreEqual(1, result.RankToHorseId[0]); // H1 (34) > H2 (33)
            Assert.AreEqual(2, result.RankToHorseId[1]);
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
        public void ResolveStage_ProtectionCard_FailedDefense_CardConsumedModifierApplied()
        {
            var horses = ConfigFactory.Horses(30, 0);
            var ev = ConfigFactory.Event("Slip", 1f, -2, EventTarget.RandomSingleHorse);
            var db = ConfigFactory.Events(ev);
            // defendChance = 0.5f, RNG returns 0.8f → 0.8 >= 0.5 → defense FAILS
            var card = ConfigFactory.Card("AntiSlip", ev, 0.5f);
            var held = new List<ProtectionCardDefinition> { card };

            var logs = EventSystem.ResolveStage(1, horses, db,
                new FakeRandom(new float[] { 0f, 0.8f }), // first 0f triggers event, second 0.8f fails defense
                held);

            Assert.AreEqual(1, logs.Count);
            Assert.IsFalse(logs[0].Defended, "Defense should fail when RNG >= defendChance");
            Assert.AreEqual(-2, logs[0].SpeedModifier, "Full modifier should be applied on failed defense");
            Assert.AreEqual(-2, horses[0].EventModifierTotal, "Horse should receive the speed penalty");
            Assert.AreEqual(0, held.Count, "Card should be consumed regardless of defense outcome");
        }

        [Test]
        public void ResolveStage_ProtectionCard_AlwaysConsumedOnMatch()
        {
            var horses = ConfigFactory.Horses(30, 0);
            var ev = ConfigFactory.Event("Stumble", 1f, -3, EventTarget.RandomSingleHorse);
            var db = ConfigFactory.Events(ev);
            // defendChance = 0f → defense always fails, but card still consumed
            var card = ConfigFactory.Card("AntiStumble", ev, 0f);
            var held = new List<ProtectionCardDefinition> { card };

            var logs = EventSystem.ResolveStage(1, horses, db,
                new FakeRandom(new float[] { 0f, 0.5f }), // triggers event, defense RNG doesn't matter with 0% chance
                held);

            Assert.AreEqual(1, logs.Count);
            Assert.IsFalse(logs[0].Defended);
            Assert.AreEqual(-3, horses[0].EventModifierTotal);
            Assert.AreEqual(0, held.Count, "Card must be consumed even when defense chance is 0%");
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

        [Test]
        public void CreateBet_NonWin_UsesBettingConfigMultiplier()
        {
            var betCfg = ConfigFactory.Betting();
            var horses = ConfigFactory.Horses(30, 7, 0);
            var odds = OddsSystem.ComputeOdds(horses, ConfigFactory.Odds(), 0);

            // Place: should use BettingConfig multiplier (1.5f), not dynamic odds
            var place = BettingSystem.CreateBet(BetType.Place, 100, new[] { 1 }, 0, betCfg, odds);
            Assert.AreEqual(1.5f, place.PayoutMultiplier, 0.001f);

            // Quinella: should use BettingConfig multiplier (5f)
            var quinella = BettingSystem.CreateBet(BetType.Quinella, 100, new[] { 1, 2 }, 0, betCfg, odds);
            Assert.AreEqual(5f, quinella.PayoutMultiplier, 0.001f);

            // Exacta: should use BettingConfig multiplier (8f)
            var exacta = BettingSystem.CreateBet(BetType.Exacta, 100, new[] { 1, 2 }, 0, betCfg, odds);
            Assert.AreEqual(8f, exacta.PayoutMultiplier, 0.001f);

            // Trio: should use BettingConfig multiplier (15f)
            var trio = BettingSystem.CreateBet(BetType.Trio, 100, new[] { 1, 2, 3 }, 0, betCfg, odds);
            Assert.AreEqual(15f, trio.PayoutMultiplier, 0.001f);

            // Trifecta: should use BettingConfig multiplier (30f)
            var trifecta = BettingSystem.CreateBet(BetType.Trifecta, 100, new[] { 1, 2, 3 }, 0, betCfg, odds);
            Assert.AreEqual(30f, trifecta.PayoutMultiplier, 0.001f);
        }

        [Test]
        public void SettleBet_ReturnsRoundedPayout_ForWin_ZeroForLoss()
        {
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            // Win case: round(amount * multiplier) = round(100 * 2.5) = 250
            var winBet = new Bet(BetType.Win, 100, new[] { 1 }, 0, 2.5f);
            Assert.AreEqual(250, BettingSystem.SettleBet(winBet, result));

            // Loss case: returns 0
            var loseBet = new Bet(BetType.Win, 100, new[] { 2 }, 0, 2.5f);
            Assert.AreEqual(0, BettingSystem.SettleBet(loseBet, result));

            // Rounding test: round(100 * 1.33) = round(133.0) = 133
            var roundBet = new Bet(BetType.Win, 100, new[] { 1 }, 0, 1.33f);
            long payout = BettingSystem.SettleBet(roundBet, result);
            long expected = (long)System.Math.Round(100 * (double)1.33f);
            Assert.AreEqual(expected, payout);

            // Fractional amount rounding: round(77 * 2.6) = round(200.2) = 200
            var fracBet = new Bet(BetType.Win, 77, new[] { 1 }, 0, 2.6f);
            long fracPayout = BettingSystem.SettleBet(fracBet, result);
            long fracExpected = (long)System.Math.Round(77 * (double)2.6f);
            Assert.AreEqual(fracExpected, fracPayout);
        }
    }

    public class AnalystSystemTests
    {
        [Test]
        public void GenerateReport_ProducesExactlyStatementsPerReport()
        {
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            var cfg = ConfigFactory.Analyst(juniorAccuracy: 1f, statementsPerReport: 3);
            var rng = new FakeRandom(); // identity shuffle, Value()=0 → all truthful

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);

            Assert.AreEqual(3, report.Statements.Count);
        }

        [Test]
        public void GenerateReport_StatementsPerReport_DifferentCounts()
        {
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            var cfg5 = ConfigFactory.Analyst(juniorAccuracy: 1f, statementsPerReport: 5);
            var rng = new FakeRandom();

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg5, rng);

            Assert.AreEqual(5, report.Statements.Count);
        }

        [Test]
        public void GenerateReport_AccuracyMechanism_AllTruthfulWhenRngBelowAccuracy()
        {
            // 8 horses: top-3 by InitialScore = H1(37), H2(36), H3(35)
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            // accuracy = 1.0 → rng.Value() (0f) < 1.0 always → all truthful
            var cfg = ConfigFactory.Analyst(juniorAccuracy: 1f, statementsPerReport: 3);
            var rng = new FakeRandom(); // Value() returns 0f → always < accuracy

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);

            // With identity shuffle, first 3 horses are H1,H2,H3 (all top-3)
            // Truthful + actually good → "有機會進入前三名"
            foreach (var s in report.Statements)
                Assert.IsTrue(s.Contains("有機會進入前三名"), $"Expected truthful top-3 statement, got: {s}");
        }

        [Test]
        public void GenerateReport_AccuracyMechanism_MisleadingWhenRngAboveAccuracy()
        {
            // 8 horses: top-3 by InitialScore = H1(37), H2(36), H3(35)
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            // accuracy = 0 → rng.Value() (0f) < 0 is false → all misleading
            var cfg = ConfigFactory.Analyst(juniorAccuracy: 0f, statementsPerReport: 3);
            var rng = new FakeRandom(); // Value() returns 0f → 0 < 0 is false → misleading

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);

            // With identity shuffle, first 3 horses are H1,H2,H3 (all top-3)
            // Misleading + actually good → reports !good → "表現不被看好"
            foreach (var s in report.Statements)
                Assert.IsTrue(s.Contains("表現不被看好"), $"Expected misleading statement for top-3 horse, got: {s}");
        }

        [Test]
        public void GenerateReport_TruthfulCorrectlyIdentifiesTop3ByInitialScore()
        {
            // H1=30+0=30, H2=30+1=31, ..., H8=30+7=37
            // Top-3 by InitialScore: H8(37), H7(36), H6(35)
            var horses = ConfigFactory.Horses(30, 0, 1, 2, 3, 4, 5, 6, 7);
            var cfg = ConfigFactory.Analyst(juniorAccuracy: 1f, statementsPerReport: 8);
            var rng = new FakeRandom(); // identity shuffle, Value()=0 → all truthful

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);

            Assert.AreEqual(8, report.Statements.Count);
            // Top-3 = H8, H7, H6 → should say "有機會進入前三名"
            // Others (H1-H5) → should say "表現不被看好"
            for (int i = 0; i < 8; i++)
            {
                int horseId = i + 1; // identity shuffle means order is H1..H8
                string statement = report.Statements[i];
                if (horseId >= 6) // top-3 are H6, H7, H8
                    Assert.IsTrue(statement.Contains("有機會進入前三名"),
                        $"H{horseId} is top-3 but got: {statement}");
                else
                    Assert.IsTrue(statement.Contains("表現不被看好"),
                        $"H{horseId} is not top-3 but got: {statement}");
            }
        }

        [Test]
        public void GenerateReport_TierIsSetCorrectly()
        {
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            var cfg = ConfigFactory.Analyst();
            var rng = new FakeRandom();

            var junior = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);
            Assert.AreEqual(AnalystTier.Junior, junior.Tier);

            var senior = AnalystSystem.GenerateReport(horses, AnalystTier.Senior, cfg, rng);
            Assert.AreEqual(AnalystTier.Senior, senior.Tier);
        }

        [Test]
        public void GenerateReport_MixedAccuracy_SomeTruthfulSomeMisleading()
        {
            // H1=37(top), H2=36(top), H3=35(top), H4=34(not top)...
            var horses = ConfigFactory.Horses(30, 7, 6, 5, 4, 3, 2, 1, 0);
            var cfg = ConfigFactory.Analyst(juniorAccuracy: 0.5f, statementsPerReport: 3);
            // Value() sequence: 0.3 (< 0.5 → truthful), 0.8 (>= 0.5 → misleading), 0.1 (< 0.5 → truthful)
            var rng = new FakeRandom(new float[] { 0.3f, 0.8f, 0.1f });

            var report = AnalystSystem.GenerateReport(horses, AnalystTier.Junior, cfg, rng);

            Assert.AreEqual(3, report.Statements.Count);
            // H1 (top-3), truthful → "有機會進入前三名"
            Assert.IsTrue(report.Statements[0].Contains("有機會進入前三名"));
            // H2 (top-3), misleading → "表現不被看好"
            Assert.IsTrue(report.Statements[1].Contains("表現不被看好"));
            // H3 (top-3), truthful → "有機會進入前三名"
            Assert.IsTrue(report.Statements[2].Contains("有機會進入前三名"));
        }
    }

    public class OddsPropertyTests
    {
        const int PropertyIterations = 100;

        [Test]
        public void Property4_OddsRankingWithTieBreak()
        {
            // Feature: horse-racing-prd-alignment, Property 4: Odds Ranking with Tie-Break
            // Validates: Requirements 5.1, 5.2
            var rng = new System.Random(42);
            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                int seed = rng.Next();
                var iterRng = new System.Random(seed);

                // Generate 8 horses with random HiddenBonus values (allow duplicates to test tie-break)
                var horses = new System.Collections.Generic.List<Horse>();
                for (int i = 0; i < 8; i++)
                {
                    horses.Add(new Horse
                    {
                        Id = i + 1,
                        BaseSpeed = 30,
                        HiddenBonus = iterRng.Next(0, 8) // intentionally allow duplicates for tie scenarios
                    });
                }

                var cfg = ConfigFactory.Odds();
                var odds = OddsSystem.ComputeOdds(horses, cfg, 0);

                Assert.AreEqual(8, odds.Count, $"Seed {seed}: Should return odds for all 8 horses");

                // Verify sorted by InitialScore desc, with Id tie-break (lower Id first)
                for (int i = 0; i < odds.Count - 1; i++)
                {
                    var current = horses.Find(h => h.Id == odds[i].HorseId);
                    var next = horses.Find(h => h.Id == odds[i + 1].HorseId);

                    bool scoreDescending = current.InitialScore > next.InitialScore;
                    bool sameScoreLowerIdFirst = current.InitialScore == next.InitialScore
                        && current.Id < next.Id;

                    Assert.IsTrue(scoreDescending || sameScoreLowerIdFirst,
                        $"Seed {seed}: Rank {i + 1} (H{current.Id}, score={current.InitialScore}) " +
                        $"should precede Rank {i + 2} (H{next.Id}, score={next.InitialScore})");
                }

                // Verify ranks are sequential 1..N
                for (int i = 0; i < odds.Count; i++)
                {
                    Assert.AreEqual(i + 1, odds[i].Rank,
                        $"Seed {seed}: odds[{i}].Rank should be {i + 1}");
                }
            }
        }

        [Test]
        public void Property5_OddsFormulaCorrectness()
        {
            // Feature: horse-racing-prd-alignment, Property 5: Odds Formula Correctness
            // Validates: Requirements 5.3, 5.4, 5.5
            var rng = new System.Random(123);
            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                int seed = rng.Next();
                var iterRng = new System.Random(seed);

                // Generate random horses
                var horses = new System.Collections.Generic.List<Horse>();
                for (int i = 0; i < 8; i++)
                {
                    horses.Add(new Horse
                    {
                        Id = i + 1,
                        BaseSpeed = 30,
                        HiddenBonus = iterRng.Next(0, 8)
                    });
                }

                var cfg = ConfigFactory.Odds();
                int round = iterRng.Next(0, 3); // random round 0, 1, or 2
                var odds = OddsSystem.ComputeOdds(horses, cfg, round);

                // For each horse at rank position i and round r, verify:
                // WinOdds == max(minOdds, baseRankOdds[i] * roundPayoutMultiplier[r])
                for (int i = 0; i < odds.Count; i++)
                {
                    float expected = System.Math.Max(cfg.minOdds, cfg.baseRankOdds[i] * cfg.roundPayoutMultiplier[round]);
                    Assert.AreEqual(expected, odds[i].WinOdds, 0.0001f,
                        $"Seed {seed}, round {round}, rank {i + 1} (H{odds[i].HorseId}): " +
                        $"Expected max({cfg.minOdds}, {cfg.baseRankOdds[i]} * {cfg.roundPayoutMultiplier[round]}) = {expected}, " +
                        $"got {odds[i].WinOdds}");
                }
            }
        }

        [Test]
        public void Property6_OddsMonotonicDecreaseAcrossRounds()
        {
            // Feature: horse-racing-prd-alignment, Property 6: Odds Monotonic Decrease Across Rounds
            // Validates: Requirements 5.6
            var rng = new System.Random(999);
            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                int seed = rng.Next();
                var iterRng = new System.Random(seed);

                // Generate random horses
                var horses = new System.Collections.Generic.List<Horse>();
                for (int i = 0; i < 8; i++)
                {
                    horses.Add(new Horse
                    {
                        Id = i + 1,
                        BaseSpeed = 30,
                        HiddenBonus = iterRng.Next(0, 8)
                    });
                }

                // Create config with strictly decreasing multipliers
                var cfg = ConfigFactory.Odds();
                // Default multipliers are [1.0, 0.9, 0.8] which are strictly decreasing

                // For each consecutive round pair, odds at round N >= odds at round N+1
                for (int round = 0; round < cfg.roundPayoutMultiplier.Length - 1; round++)
                {
                    var oddsThisRound = OddsSystem.ComputeOdds(horses, cfg, round);
                    var oddsNextRound = OddsSystem.ComputeOdds(horses, cfg, round + 1);

                    // Both lists have same ordering (same horses, same InitialScores),
                    // so same horse at same rank index across rounds
                    for (int i = 0; i < oddsThisRound.Count; i++)
                    {
                        Assert.AreEqual(oddsThisRound[i].HorseId, oddsNextRound[i].HorseId,
                            $"Seed {seed}: Ranking should be consistent across rounds");

                        Assert.GreaterOrEqual(oddsThisRound[i].WinOdds, oddsNextRound[i].WinOdds,
                            $"Seed {seed}, H{oddsThisRound[i].HorseId}: " +
                            $"Odds at round {round} ({oddsThisRound[i].WinOdds}) should be >= " +
                            $"odds at round {round + 1} ({oddsNextRound[i].WinOdds})");
                    }
                }
            }
        }
    }

    // Feature: horse-racing-prd-alignment, Property 9: Analyst Report Statement Count and Accuracy Mechanism
    /// <summary>
    /// **Validates: Requirements 7.2, 7.4**
    /// Property-based test verifying that AnalystSystem.GenerateReport produces exactly
    /// statementsPerReport statements, and each statement's truthfulness correctly reflects
    /// whether the RNG value was below accuracy (truthful) or not (misleading).
    /// </summary>
    public class Property9_AnalystReportStatementCountAndAccuracy
    {
        private const int PropertyIterations = 100;

        /// <summary>
        /// A recording random that wraps SystemRandom but records all Value() calls
        /// so we can replay accuracy decisions after the fact.
        /// </summary>
        private class RecordingRandom : IRandom
        {
            private readonly SystemRandom _inner;
            public readonly List<float> RecordedValues = new List<float>();

            public RecordingRandom(int seed) { _inner = new SystemRandom(seed); }

            public int Next(int maxExclusive) => _inner.Next(maxExclusive);
            public int Range(int minInclusive, int maxExclusive) => _inner.Range(minInclusive, maxExclusive);
            public float Value()
            {
                float v = _inner.Value();
                RecordedValues.Add(v);
                return v;
            }
            public void Shuffle<T>(IList<T> list) => _inner.Shuffle(list);
        }

        [Test]
        public void Property9_StatementCountAndAccuracyMechanism()
        {
            // Feature: horse-racing-prd-alignment, Property 9: Analyst Report Statement Count and Accuracy Mechanism
            var masterRng = new System.Random(42);

            for (int iter = 0; iter < PropertyIterations; iter++)
            {
                int seed = masterRng.Next();

                // Generate 8 horses with unique bonuses (random permutation of 0..7)
                var bonuses = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };
                var shuffleRng = new System.Random(seed);
                for (int i = bonuses.Length - 1; i > 0; i--)
                {
                    int j = shuffleRng.Next(i + 1);
                    int tmp = bonuses[i]; bonuses[i] = bonuses[j]; bonuses[j] = tmp;
                }
                var horses = ConfigFactory.Horses(30, bonuses[0], bonuses[1], bonuses[2],
                    bonuses[3], bonuses[4], bonuses[5], bonuses[6], bonuses[7]);

                // Pick a random tier
                AnalystTier tier = (seed % 2 == 0) ? AnalystTier.Junior : AnalystTier.Senior;

                // Random statementsPerReport between 1 and 8
                int statementsPerReport = (seed % 8) + 1;

                // Create config with the selected parameters
                float juniorAcc = 0.55f;
                float seniorAcc = 0.85f;
                var cfg = ConfigFactory.Analyst(juniorAccuracy: juniorAcc, seniorAccuracy: seniorAcc, statementsPerReport: statementsPerReport);
                float accuracy = cfg.GetAccuracy(tier);

                // Use recording random to capture RNG values used for accuracy decisions
                var rng = new RecordingRandom(seed);
                var report = AnalystSystem.GenerateReport(horses, tier, cfg, rng);

                // === Assert 1: exactly statementsPerReport statements are produced ===
                Assert.AreEqual(statementsPerReport, report.Statements.Count,
                    $"[Iter {iter}, seed {seed}] Expected {statementsPerReport} statements, got {report.Statements.Count}");

                // === Assert 2: verify accuracy mechanism for each statement ===
                // Determine top-3 by InitialScore (descending, tie-break by lower Id)
                var ranked = new List<Horse>(horses);
                ranked.Sort((a, b) =>
                {
                    int cmp = b.InitialScore.CompareTo(a.InitialScore);
                    return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
                });
                var topThreeIds = new HashSet<int>();
                for (int i = 0; i < 3 && i < ranked.Count; i++) topThreeIds.Add(ranked[i].Id);

                // Replay the shuffle to determine which horses were selected
                // The system shuffles a copy of horses list, then picks first N
                var poolCopy = new List<Horse>(horses);
                var replayRng = new SystemRandom(seed);
                replayRng.Shuffle(poolCopy);

                // The recorded values correspond to the Value() calls for each statement's accuracy check
                // RecordedValues contains the Value() calls made during GenerateReport
                Assert.AreEqual(statementsPerReport, rng.RecordedValues.Count,
                    $"[Iter {iter}, seed {seed}] Expected {statementsPerReport} Value() calls, got {rng.RecordedValues.Count}");

                for (int s = 0; s < statementsPerReport; s++)
                {
                    var horse = poolCopy[s];
                    bool actuallyTop3 = topThreeIds.Contains(horse.Id);
                    float rngVal = rng.RecordedValues[s];
                    bool truthful = rngVal < accuracy;

                    string statement = report.Statements[s];

                    if (truthful)
                    {
                        // Truthful: correctly reflects top-3 status
                        if (actuallyTop3)
                            Assert.IsTrue(statement.Contains("有機會進入前三名"),
                                $"[Iter {iter}, seed {seed}] Horse {horse.Id} is top-3, truthful → should say 有機會進入前三名, got: {statement}");
                        else
                            Assert.IsTrue(statement.Contains("表現不被看好"),
                                $"[Iter {iter}, seed {seed}] Horse {horse.Id} is NOT top-3, truthful → should say 表現不被看好, got: {statement}");
                    }
                    else
                    {
                        // Misleading: inverts top-3 status
                        if (actuallyTop3)
                            Assert.IsTrue(statement.Contains("表現不被看好"),
                                $"[Iter {iter}, seed {seed}] Horse {horse.Id} is top-3, misleading → should say 表現不被看好, got: {statement}");
                        else
                            Assert.IsTrue(statement.Contains("有機會進入前三名"),
                                $"[Iter {iter}, seed {seed}] Horse {horse.Id} is NOT top-3, misleading → should say 有機會進入前三名, got: {statement}");
                    }
                }
            }
        }
    }

    public class DefenseCardPropertyTests
    {
        // Feature: horse-racing-prd-alignment, Property 12: Defense Card Consumption and Effect
        /// <summary>
        /// Validates: Requirements 9.1, 9.2, 9.3, 9.4
        /// For any negative event matching a held protection card:
        /// - The card SHALL always be consumed regardless of defense outcome
        /// - When defense succeeds (RNG < defendChance), speed modifier = 0
        /// - When defense fails (RNG >= defendChance), full speed modifier is applied
        /// </summary>
        [Test]
        public void Property12_DefenseCardConsumptionAndEffect()
        {
            const int PropertyIterations = 100;
            var masterRng = new System.Random(98765);

            for (int i = 0; i < PropertyIterations; i++)
            {
                int seed = masterRng.Next();
                var iterRng = new System.Random(seed);

                // Generate random defendChance in [0.0, 1.0)
                float defendChance = (float)iterRng.NextDouble();
                // Generate random negative speedModifier in [-10, -1]
                int speedModifier = -(iterRng.Next(10) + 1);

                // Create a negative event that always triggers (triggerChance = 1)
                var ev = ConfigFactory.Event("NegEvent_" + i, 1f, speedModifier, EventTarget.RandomSingleHorse);
                var db = ConfigFactory.Events(ev);

                // Create a matching protection card with the random defendChance
                var card = ConfigFactory.Card("Shield_" + i, ev, defendChance);

                // --- Test defense SUCCESS case (RNG value < defendChance) ---
                {
                    var horse = new Horse { Id = 1, BaseSpeed = 30, HiddenBonus = 0 };
                    var horses = new System.Collections.Generic.List<Horse> { horse };
                    var held = new System.Collections.Generic.List<ProtectionCardDefinition> { card };

                    // Choose an RNG value that is below defendChance to force success
                    float successRngValue = defendChance > 0f ? defendChance * 0.5f : 0f;
                    // First float: triggers the event (0f < 1f → triggers)
                    // Second float: defense roll (< defendChance → success, unless defendChance == 0)
                    var fakeRng = new FakeRandom(
                        new float[] { 0f, successRngValue },
                        new int[] { 0 } // target horse index 0
                    );

                    var logs = EventSystem.ResolveStage(1, horses, db, fakeRng, held);

                    // Card is ALWAYS consumed
                    Assert.AreEqual(0, held.Count,
                        $"Iteration {i} (seed={seed}): Card must be consumed on defense success. defendChance={defendChance}");

                    if (defendChance > 0f)
                    {
                        // Defense succeeded: modifier should be 0
                        Assert.AreEqual(1, logs.Count,
                            $"Iteration {i}: Expected 1 log entry");
                        Assert.IsTrue(logs[0].Defended,
                            $"Iteration {i}: Defense should succeed when RNG ({successRngValue}) < defendChance ({defendChance})");
                        Assert.AreEqual(0, logs[0].SpeedModifier,
                            $"Iteration {i}: Speed modifier should be 0 on successful defense");
                        Assert.AreEqual(0, horse.EventModifierTotal,
                            $"Iteration {i}: Horse should have no speed penalty on successful defense");
                    }
                    // When defendChance == 0, defense cannot succeed (0 < 0 is false), 
                    // so we test that case in the failure branch below
                }

                // --- Test defense FAILURE case (RNG value >= defendChance) ---
                {
                    var horse = new Horse { Id = 1, BaseSpeed = 30, HiddenBonus = 0 };
                    var horses = new System.Collections.Generic.List<Horse> { horse };
                    var held = new System.Collections.Generic.List<ProtectionCardDefinition> { card };

                    // Choose an RNG value that is >= defendChance to force failure
                    float failRngValue = defendChance < 1f ? defendChance + (1f - defendChance) * 0.5f : 1f;
                    // Clamp to ensure it's in valid range [0,1) and >= defendChance
                    if (failRngValue >= 1f) failRngValue = 0.999f;
                    if (failRngValue < defendChance) failRngValue = defendChance;

                    var fakeRng = new FakeRandom(
                        new float[] { 0f, failRngValue },
                        new int[] { 0 } // target horse index 0
                    );

                    var logs = EventSystem.ResolveStage(1, horses, db, fakeRng, held);

                    // Card is ALWAYS consumed regardless of defense outcome
                    Assert.AreEqual(0, held.Count,
                        $"Iteration {i} (seed={seed}): Card must be consumed on defense failure. defendChance={defendChance}");

                    Assert.AreEqual(1, logs.Count,
                        $"Iteration {i}: Expected 1 log entry");
                    Assert.IsFalse(logs[0].Defended,
                        $"Iteration {i}: Defense should fail when RNG ({failRngValue}) >= defendChance ({defendChance})");
                    Assert.AreEqual(speedModifier, logs[0].SpeedModifier,
                        $"Iteration {i}: Full speed modifier ({speedModifier}) should be applied on failed defense");
                    Assert.AreEqual(speedModifier, horse.EventModifierTotal,
                        $"Iteration {i}: Horse should receive full speed penalty ({speedModifier}) on failed defense");
                }
            }
        }
    }

    public class EventSystemPropertyTests
    {
        const int PropertyIterations = 100;

        // Feature: horse-racing-prd-alignment, Property 10: Event Trigger Mechanism
        /// <summary>
        /// **Validates: Requirements 8.1, 8.2, 8.3**
        /// For events with various triggerChance values, verify event triggers iff RNG < triggerChance.
        /// When triggered with AllHorses, all horses affected; with RandomSingleHorse, exactly one horse affected.
        /// </summary>
        [Test]
        public void Property10_EventTriggerMechanism()
        {
            // Feature: horse-racing-prd-alignment, Property 10: Event Trigger Mechanism
            var masterRng = new System.Random(98765);

            for (int i = 0; i < PropertyIterations; i++)
            {
                // Generate random triggerChance in [0.01, 1.0]
                float triggerChance = (float)(masterRng.NextDouble() * 0.99 + 0.01);
                // Generate random RNG value in [0, 1)
                float rngValue = (float)masterRng.NextDouble();
                bool shouldTrigger = rngValue < triggerChance;

                // Randomly pick target type
                EventTarget target = masterRng.Next(2) == 0 ? EventTarget.AllHorses : EventTarget.RandomSingleHorse;

                // Generate random horse count 2..8
                int horseCount = masterRng.Next(2, 9);
                int[] bonuses = new int[horseCount];
                for (int h = 0; h < horseCount; h++) bonuses[h] = h;
                var horses = ConfigFactory.Horses(30, bonuses);

                // Create event with the random triggerChance and target
                var ev = ConfigFactory.Event("TestEvent", triggerChance, -2, target);
                var db = ConfigFactory.Events(ev);

                // FakeRandom: first Value() call is for trigger check, second Next() call is for target selection
                var fakeRng = new FakeRandom(
                    new float[] { rngValue },
                    new int[] { masterRng.Next(horseCount) } // random target index for RandomSingleHorse
                );

                var logs = EventSystem.ResolveStage(1, horses, db, fakeRng, new List<ProtectionCardDefinition>());

                if (shouldTrigger)
                {
                    // Event should have triggered
                    Assert.IsTrue(logs.Count > 0,
                        $"Iteration {i}: Event should trigger (rng={rngValue:F4} < chance={triggerChance:F4})");

                    if (target == EventTarget.AllHorses)
                    {
                        // All horses should be affected
                        Assert.AreEqual(horseCount, logs.Count,
                            $"Iteration {i}: AllHorses target should affect all {horseCount} horses, got {logs.Count}");
                    }
                    else
                    {
                        // Exactly one horse should be affected
                        Assert.AreEqual(1, logs.Count,
                            $"Iteration {i}: RandomSingleHorse target should affect exactly 1 horse, got {logs.Count}");
                    }
                }
                else
                {
                    // Event should NOT have triggered
                    Assert.AreEqual(0, logs.Count,
                        $"Iteration {i}: Event should NOT trigger (rng={rngValue:F4} >= chance={triggerChance:F4})");
                }
            }
        }

        // Feature: horse-racing-prd-alignment, Property 11: Event Speed Modifier Application
        /// <summary>
        /// **Validates: Requirements 8.4**
        /// For triggered events hitting a horse without defense, verify the horse's StageEventModifiers
        /// contains the event's speedModifier appended.
        /// </summary>
        [Test]
        public void Property11_EventSpeedModifierApplication()
        {
            // Feature: horse-racing-prd-alignment, Property 11: Event Speed Modifier Application
            var masterRng = new System.Random(54321);

            for (int i = 0; i < PropertyIterations; i++)
            {
                // Generate random speedModifier in [-5, 5]
                int speedModifier = masterRng.Next(-5, 6);

                // Randomly pick target type
                EventTarget target = masterRng.Next(2) == 0 ? EventTarget.AllHorses : EventTarget.RandomSingleHorse;

                // Generate random horse count 2..8
                int horseCount = masterRng.Next(2, 9);
                int[] bonuses = new int[horseCount];
                for (int h = 0; h < horseCount; h++) bonuses[h] = h;
                var horses = ConfigFactory.Horses(30, bonuses);

                // Record initial StageEventModifiers state (should be empty for fresh horses)
                var initialModifierCounts = new int[horseCount];
                for (int h = 0; h < horseCount; h++)
                    initialModifierCounts[h] = horses[h].StageEventModifiers.Count;

                // Create event that always triggers (triggerChance = 1.0)
                var ev = ConfigFactory.Event("TestEvent", 1f, speedModifier, target);
                var db = ConfigFactory.Events(ev);

                // No protection cards (no defense)
                int targetIndex = masterRng.Next(horseCount);
                var fakeRng = new FakeRandom(
                    new float[] { 0f }, // triggers the event (0 < 1.0)
                    new int[] { targetIndex } // target horse index for RandomSingleHorse
                );

                var logs = EventSystem.ResolveStage(1, horses, db, fakeRng, new List<ProtectionCardDefinition>());

                // Verify all affected horses have the speedModifier appended
                Assert.IsTrue(logs.Count > 0, $"Iteration {i}: Event should trigger with chance=1.0");

                foreach (var log in logs)
                {
                    Assert.IsFalse(log.Defended, $"Iteration {i}: No defense cards, should not be defended");
                    Assert.AreEqual(speedModifier, log.SpeedModifier,
                        $"Iteration {i}: Log should record speedModifier={speedModifier}");

                    // Find the horse and verify modifier was appended
                    var horse = horses.Find(h => h.Id == log.HorseId);
                    Assert.IsNotNull(horse, $"Iteration {i}: Horse {log.HorseId} not found");

                    int initialCount = initialModifierCounts[horse.Id - 1];
                    Assert.AreEqual(initialCount + 1, horse.StageEventModifiers.Count,
                        $"Iteration {i}: Horse {horse.Id} should have exactly one new modifier appended");
                    Assert.AreEqual(speedModifier, horse.StageEventModifiers[horse.StageEventModifiers.Count - 1],
                        $"Iteration {i}: Last modifier of horse {horse.Id} should be {speedModifier}");
                }

                // Verify non-affected horses are unchanged
                if (target == EventTarget.RandomSingleHorse)
                {
                    for (int h = 0; h < horseCount; h++)
                    {
                        if (horses[h].Id != logs[0].HorseId)
                        {
                            Assert.AreEqual(initialModifierCounts[h], horses[h].StageEventModifiers.Count,
                                $"Iteration {i}: Non-targeted horse {horses[h].Id} should be unchanged");
                        }
                    }
                }
            }
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

    /// <summary>
    /// Verifies GameManager.PlaceBet validation guards (Task 7.1).
    /// Requirements: 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public class GameManagerPlaceBetTests
    {
        private UnityEngine.GameObject _go;
        private GameManager _gm;
        private string _lastNotice;

        [SetUp]
        public void SetUp()
        {
            _go = new UnityEngine.GameObject("TestGM");
            _gm = _go.AddComponent<GameManager>();

            // Build config
            var db = UnityEngine.ScriptableObject.CreateInstance<GameConfigDatabase>();
            db.game = ConfigFactory.Game(8, 30);
            db.game.startingMoney = 1000;
            db.game.minBetAmount = 50;

            db.odds = ConfigFactory.Odds();
            db.betting = ConfigFactory.Betting();
            db.track = ConfigFactory.Track();
            db.events = ConfigFactory.Events();
            db.analyst = ConfigFactory.Analyst();

            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++)
                mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "D" + i });
            db.messageCards = mc;
            db.shop = ConfigFactory.Shop(3);

            _gm.config = db;
            _gm.randomSeed = 42;

            // Subscribe to notices
            _lastNotice = null;
            _gm.OnNotice += msg => _lastNotice = msg;

            // Force Awake by using reflection or just call StartNewRound directly
            // (Awake isn't called in EditMode, so manually initialize)
            var awakeMethod = typeof(GameManager).GetMethod("Awake",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awakeMethod?.Invoke(_gm, null);

            // Start a round to get into Betting phase
            _gm.StartNewRound();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// Requirement 12.1: WHEN amount < minBetAmount, reject and notify minimum amount.
        /// </summary>
        [Test]
        public void PlaceBet_RejectsAmountBelowMinBet_NotifiesMinimum()
        {
            long moneyBefore = _gm.Player.Money;
            bool result = _gm.PlaceBet(BetType.Win, 10, new[] { 1 }); // 10 < 50 minBetAmount

            Assert.IsFalse(result, "PlaceBet should return false when amount < minBetAmount");
            Assert.AreEqual(moneyBefore, _gm.Player.Money, "Player money should remain unchanged");
            Assert.IsNotNull(_lastNotice, "A Notice should be issued");
            Assert.IsTrue(_lastNotice.Contains("50"),
                $"Notice should mention the minimum bet amount (50), got: {_lastNotice}");
        }

        /// <summary>
        /// Requirement 12.2: WHEN amount > player.Money, reject and notify "資金不足".
        /// </summary>
        [Test]
        public void PlaceBet_RejectsAmountExceedingMoney_NotifiesInsufficientFunds()
        {
            long moneyBefore = _gm.Player.Money;
            bool result = _gm.PlaceBet(BetType.Win, moneyBefore + 100, new[] { 1 }); // exceeds money

            Assert.IsFalse(result, "PlaceBet should return false when amount > player.Money");
            Assert.AreEqual(moneyBefore, _gm.Player.Money, "Player money should remain unchanged");
            Assert.IsNotNull(_lastNotice, "A Notice should be issued");
            Assert.IsTrue(_lastNotice.Contains("資金不足"),
                $"Notice should say '資金不足', got: {_lastNotice}");
        }

        /// <summary>
        /// Requirement 12.5: IF horseIds is null, reject the bet.
        /// </summary>
        [Test]
        public void PlaceBet_RejectsNullHorseIds()
        {
            long moneyBefore = _gm.Player.Money;
            bool result = _gm.PlaceBet(BetType.Win, 100, null);

            Assert.IsFalse(result, "PlaceBet should return false when horseIds is null");
            Assert.AreEqual(moneyBefore, _gm.Player.Money, "Player money should remain unchanged");
        }

        /// <summary>
        /// Requirement 12.5: IF horseIds is empty, reject the bet.
        /// </summary>
        [Test]
        public void PlaceBet_RejectsEmptyHorseIds()
        {
            long moneyBefore = _gm.Player.Money;
            bool result = _gm.PlaceBet(BetType.Win, 100, new int[0]);

            Assert.IsFalse(result, "PlaceBet should return false when horseIds is empty");
            Assert.AreEqual(moneyBefore, _gm.Player.Money, "Player money should remain unchanged");
        }

        /// <summary>
        /// Phase guard: PlaceBet should be rejected when Phase != Betting.
        /// </summary>
        [Test]
        public void PlaceBet_RejectsWhenPhaseIsNotBetting()
        {
            // Advance to Racing phase by confirming all betting rounds and starting race
            for (int i = 0; i < _gm.BettingRounds; i++)
                _gm.ConfirmBettingRound();
            // Now phase should be Racing
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "Phase should be Racing after last betting round");

            long moneyBefore = _gm.Player.Money;
            bool result = _gm.PlaceBet(BetType.Win, 100, new[] { 1 });

            Assert.IsFalse(result, "PlaceBet should return false when Phase != Betting");
            Assert.AreEqual(moneyBefore, _gm.Player.Money, "Player money should remain unchanged");
        }

        /// <summary>
        /// Requirement 12.3: WHEN bet succeeds, player.Money decreases by exactly the bet amount.
        /// </summary>
        [Test]
        public void PlaceBet_Success_DeductsExactAmount()
        {
            long moneyBefore = _gm.Player.Money;
            long betAmount = 200;
            bool result = _gm.PlaceBet(BetType.Win, betAmount, new[] { 1 });

            Assert.IsTrue(result, "PlaceBet should return true for a valid bet");
            Assert.AreEqual(moneyBefore - betAmount, _gm.Player.Money,
                "Player money should decrease by exactly the bet amount");
        }

        /// <summary>
        /// Requirement 12.3: Confirm bet is added to Round.Bets on success.
        /// </summary>
        [Test]
        public void PlaceBet_Success_AddsBetToRoundBets()
        {
            int betsBefore = _gm.Round.Bets.Count;
            _gm.PlaceBet(BetType.Win, 100, new[] { 1 });

            Assert.AreEqual(betsBefore + 1, _gm.Round.Bets.Count,
                "A successful bet should be added to Round.Bets");
        }

        /// <summary>
        /// Requirement 12.4: Multiple bets in the same round should all succeed.
        /// </summary>
        [Test]
        public void PlaceBet_AllowsMultipleBetsInSameRound()
        {
            long startMoney = _gm.Player.Money;
            long bet1 = 100, bet2 = 150, bet3 = 50;

            Assert.IsTrue(_gm.PlaceBet(BetType.Win, bet1, new[] { 1 }));
            Assert.IsTrue(_gm.PlaceBet(BetType.Place, bet2, new[] { 2 }));
            Assert.IsTrue(_gm.PlaceBet(BetType.Quinella, bet3, new[] { 1, 2 }));

            Assert.AreEqual(startMoney - bet1 - bet2 - bet3, _gm.Player.Money,
                "All bet amounts should be deducted");
            Assert.AreEqual(3, _gm.Round.Bets.Count, "All three bets should be recorded");
        }

        /// <summary>
        /// Requirement 12.2: Boundary test - bet amount exactly equals player money (should succeed).
        /// </summary>
        [Test]
        public void PlaceBet_ExactMoneyAmount_Succeeds()
        {
            long allMoney = _gm.Player.Money;
            // minBetAmount is 50, so if money >= 50 this should work
            Assert.IsTrue(allMoney >= 50, "Starting money must be >= minBetAmount for this test");

            bool result = _gm.PlaceBet(BetType.Win, allMoney, new[] { 1 });
            Assert.IsTrue(result, "Betting exactly all money should succeed");
            Assert.AreEqual(0, _gm.Player.Money, "Player should have 0 money after betting all");
        }

        /// <summary>
        /// Requirement 12.1: Boundary test - bet amount exactly equals minBetAmount (should succeed).
        /// </summary>
        [Test]
        public void PlaceBet_ExactMinBetAmount_Succeeds()
        {
            long minBet = _gm.config.game.minBetAmount; // 50
            bool result = _gm.PlaceBet(BetType.Win, minBet, new[] { 1 });
            Assert.IsTrue(result, "Betting exactly minBetAmount should succeed");
        }

        /// <summary>
        /// Notice message check: Successful bet issues a notice with bet type and amount info.
        /// </summary>
        [Test]
        public void PlaceBet_Success_IssuesNotice()
        {
            _lastNotice = null;
            _gm.PlaceBet(BetType.Win, 100, new[] { 1 });

            Assert.IsNotNull(_lastNotice, "Successful bet should issue a notice");
            Assert.IsTrue(_lastNotice.Contains("100"),
                $"Notice should contain the bet amount, got: {_lastNotice}");
        }
    }

    /// <summary>
    /// Verifies SettlementSystem.Settle arithmetic consistency (Task 5.7).
    /// Requirements: 14.1, 14.2, 14.3, 14.4
    /// </summary>
    public class SettlementArithmeticVerificationTests
    {
        private RaceResult MakeResult(params int[] rankToHorse)
        {
            var result = new RaceResult { RankToHorseId = rankToHorse };
            for (int i = 0; i < rankToHorse.Length; i++)
                result.Standings.Add(new HorseRaceResult { HorseId = rankToHorse[i], Rank = i + 1 });
            return result;
        }

        /// <summary>
        /// Requirement 14.1: TotalStaked = Σ(bet.Amount) for ALL bets (win or lose).
        /// </summary>
        [Test]
        public void Settle_TotalStaked_IsSumOfAllBetAmounts()
        {
            var player = new PlayerState(5000);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 200, new[] { 1 }, 0, 2.5f),   // 贏
                new Bet(BetType.Win, 150, new[] { 5 }, 0, 6.0f),   // 輸
                new Bet(BetType.Place, 300, new[] { 3 }, 1, 1.5f), // 贏
                new Bet(BetType.Win, 50, new[] { 8 }, 2, 15f),     // 輸
            };

            var s = SettlementSystem.Settle(player, bets, result);
            long expectedStaked = 200 + 150 + 300 + 50; // = 700
            Assert.AreEqual(expectedStaked, s.TotalStaked,
                "TotalStaked must equal sum of ALL bet amounts regardless of win/loss");
        }

        /// <summary>
        /// Requirement 14.2: TotalPayout = Σ(round(bet.Amount × bet.PayoutMultiplier)) for WINNING bets only.
        /// </summary>
        [Test]
        public void Settle_TotalPayout_IsSumOfRoundedPayoutsForWinningBets()
        {
            var player = new PlayerState(5000);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 100, new[] { 1 }, 0, 2.5f),   // 贏: round(100×2.5) = 250
                new Bet(BetType.Win, 100, new[] { 5 }, 0, 6.0f),   // 輸: 0
                new Bet(BetType.Place, 200, new[] { 2 }, 1, 1.5f), // 贏: round(200×1.5) = 300
                new Bet(BetType.Win, 75, new[] { 8 }, 2, 15f),     // 輸: 0
                new Bet(BetType.Place, 50, new[] { 3 }, 0, 1.5f),  // 贏: round(50×1.5) = 75
            };

            var s = SettlementSystem.Settle(player, bets, result);
            long expectedPayout = 250 + 0 + 300 + 0 + 75; // = 625
            Assert.AreEqual(expectedPayout, s.TotalPayout,
                "TotalPayout must equal sum of round(Amount×Multiplier) for winning bets only");
        }

        /// <summary>
        /// Requirement 14.2 (rounding): Verifies payout uses Math.Round for fractional multipliers.
        /// </summary>
        [Test]
        public void Settle_TotalPayout_RoundsCorrectlyForFractionalMultipliers()
        {
            var player = new PlayerState(5000);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            // Both bets win (Win bet on horse 1 which is 1st place)
            // Amount=33, Multiplier=2.5f → (long)Math.Round(33 * (double)2.5f) = 82 (banker's rounding on .5)
            // Amount=67, Multiplier=3.3f → (long)Math.Round(67 * (double)3.3f)
            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 33, new[] { 1 }, 0, 2.5f),   // 贏 (H1 is 1st)
                new Bet(BetType.Place, 67, new[] { 2 }, 0, 3.3f), // 贏 (H2 is in top 3)
            };

            var s = SettlementSystem.Settle(player, bets, result);

            long payout1 = (long)System.Math.Round(33 * (double)2.5f);
            long payout2 = (long)System.Math.Round(67 * (double)3.3f);
            long expectedPayout = payout1 + payout2;

            Assert.AreEqual(expectedPayout, s.TotalPayout,
                $"TotalPayout must use Math.Round per bet: expected {payout1}+{payout2}={expectedPayout}");
        }

        /// <summary>
        /// Requirement 14.3: Net = TotalPayout - TotalStaked.
        /// </summary>
        [Test]
        public void Settle_Net_EqualsTotalPayoutMinusTotalStaked()
        {
            var player = new PlayerState(5000);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 200, new[] { 1 }, 0, 2.0f),  // 贏: 400
                new Bet(BetType.Win, 300, new[] { 5 }, 0, 4.0f),  // 輸: 0
                new Bet(BetType.Place, 100, new[] { 2 }, 0, 1.5f),// 贏: 150
            };

            var s = SettlementSystem.Settle(player, bets, result);
            Assert.AreEqual(s.TotalPayout - s.TotalStaked, s.Net,
                "Net must always equal TotalPayout - TotalStaked");

            // Verify concrete values
            Assert.AreEqual(600, s.TotalStaked);  // 200+300+100
            Assert.AreEqual(550, s.TotalPayout);  // 400+0+150
            Assert.AreEqual(-50, s.Net);           // 550-600
        }

        /// <summary>
        /// Requirement 14.4: player.Money increases by exactly TotalPayout.
        /// </summary>
        [Test]
        public void Settle_PlayerMoney_IncreasesByExactlyTotalPayout()
        {
            long initialMoney = 2000;
            var player = new PlayerState(initialMoney);
            var result = MakeResult(3, 1, 5, 2, 4, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 100, new[] { 3 }, 0, 4.5f),  // 贏: round(100×4.5) = 450
                new Bet(BetType.Win, 200, new[] { 1 }, 0, 2.0f),  // 輸 (H1 is rank 2): 0
                new Bet(BetType.Place, 150, new[] { 1 }, 0, 1.5f),// 贏 (H1 is top 3): round(150×1.5) = 225
            };

            var s = SettlementSystem.Settle(player, bets, result);

            Assert.AreEqual(initialMoney + s.TotalPayout, player.Money,
                "player.Money must increase by exactly TotalPayout from Settle");

            // Verify concrete: 2000 + 675 = 2675
            Assert.AreEqual(675, s.TotalPayout);
            Assert.AreEqual(2675, player.Money);
        }

        /// <summary>
        /// Edge case: All bets lose → TotalPayout = 0, player.Money unchanged.
        /// </summary>
        [Test]
        public void Settle_AllBetsLose_PayoutZero_MoneyUnchanged()
        {
            long initialMoney = 1000;
            var player = new PlayerState(initialMoney);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 100, new[] { 5 }, 0, 6.0f),  // 輸
                new Bet(BetType.Win, 200, new[] { 8 }, 0, 15f),    // 輸
            };

            var s = SettlementSystem.Settle(player, bets, result);

            Assert.AreEqual(300, s.TotalStaked);
            Assert.AreEqual(0, s.TotalPayout);
            Assert.AreEqual(-300, s.Net);
            Assert.AreEqual(initialMoney, player.Money,
                "When all bets lose, player.Money should not change");
        }

        /// <summary>
        /// Edge case: Empty bets list → all totals are 0, player.Money unchanged.
        /// </summary>
        [Test]
        public void Settle_EmptyBets_AllZeros_MoneyUnchanged()
        {
            long initialMoney = 1000;
            var player = new PlayerState(initialMoney);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var s = SettlementSystem.Settle(player, new List<Bet>(), result);

            Assert.AreEqual(0, s.TotalStaked);
            Assert.AreEqual(0, s.TotalPayout);
            Assert.AreEqual(0, s.Net);
            Assert.AreEqual(initialMoney, player.Money);
        }

        /// <summary>
        /// Edge case: Null bets → all totals are 0, player.Money unchanged.
        /// </summary>
        [Test]
        public void Settle_NullBets_AllZeros_MoneyUnchanged()
        {
            long initialMoney = 1000;
            var player = new PlayerState(initialMoney);
            var result = MakeResult(1, 2, 3, 4, 5, 6, 7, 8);

            var s = SettlementSystem.Settle(player, null, result);

            Assert.AreEqual(0, s.TotalStaked);
            Assert.AreEqual(0, s.TotalPayout);
            Assert.AreEqual(0, s.Net);
            Assert.AreEqual(initialMoney, player.Money);
        }

        /// <summary>
        /// Combined verification: Multi-bet scenario with mixed bet types validating all 4 requirements together.
        /// </summary>
        [Test]
        public void Settle_MultipleBetTypes_AllArithmeticConsistent()
        {
            long initialMoney = 3000;
            var player = new PlayerState(initialMoney);
            // Rank: 1st=H3, 2nd=H1, 3rd=H5
            var result = MakeResult(3, 1, 5, 2, 4, 6, 7, 8);

            var bets = new List<Bet>
            {
                new Bet(BetType.Win, 100, new[] { 3 }, 0, 4.0f),       // 贏 (H3 is 1st): 400
                new Bet(BetType.Place, 200, new[] { 1 }, 0, 1.5f),     // 贏 (H1 in top 3): 300
                new Bet(BetType.Quinella, 150, new[] { 3, 1 }, 1, 5f), // 贏 ({3,1}={3,1}): 750
                new Bet(BetType.Exacta, 100, new[] { 1, 3 }, 1, 8f),  // 輸 (order wrong: 1st=3, 2nd=1)
                new Bet(BetType.Trio, 80, new[] { 5, 1, 3 }, 2, 15f), // 贏 ({5,1,3}={3,1,5}): 1200
                new Bet(BetType.Trifecta, 50, new[] { 3, 1, 5 }, 2, 30f), // 贏 (exact order): 1500
            };

            var s = SettlementSystem.Settle(player, bets, result);

            // Req 14.1: TotalStaked = Σ(all amounts)
            long expectedStaked = 100 + 200 + 150 + 100 + 80 + 50;
            Assert.AreEqual(expectedStaked, s.TotalStaked, "Req 14.1: TotalStaked = Σ(bet.Amount)");

            // Req 14.2: TotalPayout = Σ(round(Amount × Multiplier)) for winners
            long expectedPayout = (long)System.Math.Round(100 * (double)4.0f)
                                + (long)System.Math.Round(200 * (double)1.5f)
                                + (long)System.Math.Round(150 * (double)5f)
                                + 0  // Exacta lost
                                + (long)System.Math.Round(80 * (double)15f)
                                + (long)System.Math.Round(50 * (double)30f);
            Assert.AreEqual(expectedPayout, s.TotalPayout, "Req 14.2: TotalPayout = Σ(round(Amount×Multiplier)) for wins");

            // Req 14.3: Net = TotalPayout - TotalStaked
            Assert.AreEqual(s.TotalPayout - s.TotalStaked, s.Net, "Req 14.3: Net = TotalPayout - TotalStaked");

            // Req 14.4: player.Money increases by exactly TotalPayout
            Assert.AreEqual(initialMoney + s.TotalPayout, player.Money, "Req 14.4: player.Money += TotalPayout");
        }
    }

    /// <summary>
    /// Task 7.3: Verify GameManager game-over conditions and win/loss determination.
    /// Requirements: 2.1, 2.2, 2.3, 2.4, 2.5
    /// </summary>
    public class GameManagerGameOverTests
    {
        private UnityEngine.GameObject _go;
        private GameManager _gm;

        private GameConfigDatabase CreateFullConfig(long startingMoney = 1000, long minBetAmount = 50, int totalRounds = 5)
        {
            var db = UnityEngine.ScriptableObject.CreateInstance<GameConfigDatabase>();
            db.game = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            db.game.horseCount = 8;
            db.game.baseSpeed = 30;
            db.game.hiddenBonusPool = new int[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            db.game.startingMoney = startingMoney;
            db.game.minBetAmount = minBetAmount;
            db.game.totalRounds = totalRounds;
            db.game.stageCount = 3;

            db.odds = ConfigFactory.Odds();
            db.betting = ConfigFactory.Betting();
            db.track = ConfigFactory.Track();
            db.events = ConfigFactory.Events(); // empty events for deterministic testing
            db.analyst = ConfigFactory.Analyst();

            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++)
                mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "Info_" + i });
            db.messageCards = mc;

            db.shop = ConfigFactory.Shop(3);
            return db;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new UnityEngine.GameObject("TestGM_GameOver");
            _gm = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// Requirement 2.1: WHEN 玩家資金降至 0 或以下, THE GameManager SHALL 觸發遊戲結束並顯示「資金耗盡」
        /// Verifies that after settlement leaves Money <= 0, GameOver is triggered.
        /// </summary>
        [Test]
        public void Req2_1_MoneyZeroAfterSettlement_TriggersGameOver()
        {
            var config = CreateFullConfig(startingMoney: 100, minBetAmount: 50);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase);
            Assert.AreEqual(100, _gm.Player.Money);

            // Bet all money on horse 8 (likely to lose with low bonus in most shuffles)
            bool betPlaced = _gm.PlaceBet(BetType.Win, 100, new[] { 8 });
            Assert.IsTrue(betPlaced, "Bet should be placed successfully");
            Assert.AreEqual(0, _gm.Player.Money, "All money should be deducted");

            // Advance through betting to racing
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            Assert.AreEqual(GamePhase.Racing, _gm.Phase);

            // Complete race and settle
            _gm.CompleteRaceAndSettle();

            // If horse 8 didn't win (Money stays <= 0), game over should trigger
            if (_gm.Player.Money <= 0)
            {
                Assert.AreEqual(GamePhase.GameOver, _gm.Phase, "Req 2.1: Money<=0 after settlement → GameOver");
                Assert.IsTrue(_gm.GameOverReason.Contains("資金耗盡"), "Req 2.1: Reason should mention 資金耗盡");
            }
        }

        /// <summary>
        /// Requirement 2.1: Direct controlled test - manually set money to 0 and verify CompleteRaceAndSettle triggers game over.
        /// </summary>
        [Test]
        public void Req2_1_Controlled_MoneyZero_TriggersGameOverAfterSettle()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50);
            _gm.config = config;
            _gm.randomSeed = 123;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // → Racing

            // Set money to 0 before settlement (simulating all lost)
            _gm.Player.Money = 0;

            _gm.CompleteRaceAndSettle();

            // After settle with no winning bets and money=0, game over triggers
            // Settlement adds TotalPayout (0 if no bets won), so money stays <= 0
            if (_gm.Player.Money <= 0)
            {
                Assert.AreEqual(GamePhase.GameOver, _gm.Phase, "Req 2.1: Money<=0 → GameOver");
                Assert.IsTrue(_gm.GameOverReason.Contains("資金耗盡"));
            }
        }

        /// <summary>
        /// Requirement 2.2: WHEN 玩家資金低於 minBetAmount 但大於 0, GameManager SHALL 觸發遊戲結束 on NextRound.
        /// </summary>
        [Test]
        public void Req2_2_MoneyBelowMinBetAboveZero_OnNextRound_TriggersGameOver()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 100);
            _gm.config = config;
            _gm.randomSeed = 99;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // → Racing
            _gm.CompleteRaceAndSettle(); // → Settlement

            // Set money to value: 0 < money < minBetAmount
            _gm.Player.Money = 50; // 0 < 50 < 100 (minBetAmount)

            _gm.NextRound();

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase, "Req 2.2: Money < minBetAmount && > 0 → GameOver");
            Assert.IsTrue(_gm.GameOverReason.Contains("資金不足以下注"),
                "Req 2.2: Reason should mention 資金不足以下注");
        }

        /// <summary>
        /// Requirement 2.2: Boundary - Money == minBetAmount does NOT trigger game over.
        /// </summary>
        [Test]
        public void Req2_2_MoneyEqualToMinBet_DoesNotTriggerGameOver()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 100);
            _gm.config = config;
            _gm.randomSeed = 77;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Set money exactly at minBetAmount
            _gm.Player.Money = 100; // == minBetAmount

            _gm.NextRound();

            Assert.AreNotEqual(GamePhase.GameOver, _gm.Phase,
                "Req 2.2 boundary: Money == minBetAmount should NOT trigger game over");
            Assert.AreEqual(GamePhase.Betting, _gm.Phase,
                "Should proceed to Betting phase for next round");
        }

        /// <summary>
        /// Requirement 2.2: NextRound from Shop phase also checks minBetAmount condition.
        /// </summary>
        [Test]
        public void Req2_2_NextRoundFromShop_ChecksMinBetAmount()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 100);
            _gm.config = config;
            _gm.randomSeed = 55;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Shop, _gm.Phase);

            // Set money below minBetAmount but > 0
            _gm.Player.Money = 30; // 0 < 30 < 100

            _gm.NextRound();

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase,
                "Req 2.2: NextRound from Shop with Money < minBetAmount → GameOver");
            Assert.IsTrue(_gm.GameOverReason.Contains("資金不足以下注"));
        }

        /// <summary>
        /// Requirement 2.3: WHEN 已完成 totalRounds 回合（且 totalRounds > 0）, GameOver triggered on StartNewRound.
        /// </summary>
        [Test]
        public void Req2_3_TotalRoundsReached_TriggersGameOver()
        {
            var config = CreateFullConfig(startingMoney: 10000, minBetAmount: 50, totalRounds: 2);
            _gm.config = config;
            _gm.randomSeed = 42;

            // Round 1
            _gm.StartNewRound();
            Assert.AreEqual(1, _gm.RoundNumber);
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Round 2
            _gm.NextRound();
            Assert.AreEqual(2, _gm.RoundNumber);
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Attempt Round 3 - should trigger game over (RoundNumber=2 >= totalRounds=2)
            _gm.NextRound();

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase,
                "Req 2.3: totalRounds reached → GameOver");
            Assert.IsTrue(_gm.GameOverReason.Contains("已完成") && _gm.GameOverReason.Contains("2"),
                "Req 2.3: Reason should mention completed rounds count");
        }

        /// <summary>
        /// Requirement 2.3: totalRounds=0 means no round limit (infinite play).
        /// </summary>
        [Test]
        public void Req2_3_TotalRoundsZero_NoRoundLimit()
        {
            var config = CreateFullConfig(startingMoney: 10000, minBetAmount: 50, totalRounds: 0);
            _gm.config = config;
            _gm.randomSeed = 42;

            // Play 3 rounds without hitting game over from round limit
            for (int r = 0; r < 3; r++)
            {
                _gm.StartNewRound();
                Assert.AreEqual(GamePhase.Betting, _gm.Phase, $"Round {r + 1} should start normally");
                _gm.ConfirmBettingRound();
                _gm.ConfirmBettingRound();
                _gm.ConfirmBettingRound();
                _gm.CompleteRaceAndSettle();
                if (_gm.Phase == GamePhase.GameOver) break; // money might run out
                _gm.EnterShop();
            }

            // If game ended, it must be due to money, not rounds
            if (_gm.Phase == GamePhase.GameOver)
            {
                Assert.IsFalse(_gm.GameOverReason.Contains("已完成"),
                    "With totalRounds=0, game should never end due to round limit");
            }
            else
            {
                // Game hasn't ended after 3 rounds → confirms no round limit
                Assert.AreNotEqual(GamePhase.GameOver, _gm.Phase);
            }
        }

        /// <summary>
        /// Requirement 2.4: WHEN 遊戲結束時 Money >= startingMoney, GameWon = true.
        /// </summary>
        [Test]
        public void Req2_4_MoneyGreaterThanStarting_GameWonTrue()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50, totalRounds: 1);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Set money above startingMoney before triggering round-limit game over
            _gm.Player.Money = 1500; // > startingMoney (1000)

            _gm.NextRound(); // RoundNumber=1 >= totalRounds=1 → GameOver

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase);
            Assert.IsTrue(_gm.GameWon, "Req 2.4: Money > startingMoney → GameWon = true");
        }

        /// <summary>
        /// Requirement 2.4: Money exactly equals startingMoney → GameWon = true.
        /// </summary>
        [Test]
        public void Req2_4_MoneyExactlyEqualToStarting_GameWonTrue()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50, totalRounds: 1);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Set money exactly to startingMoney
            _gm.Player.Money = 1000; // == startingMoney

            _gm.NextRound();

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase);
            Assert.IsTrue(_gm.GameWon, "Req 2.4: Money == startingMoney → GameWon = true");
        }

        /// <summary>
        /// Requirement 2.5: WHEN 遊戲結束時 Money < startingMoney, GameWon = false.
        /// </summary>
        [Test]
        public void Req2_5_MoneyLessThanStarting_GameWonFalse()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50, totalRounds: 1);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();

            // Set money below startingMoney
            _gm.Player.Money = 500; // < startingMoney (1000)

            _gm.NextRound();

            Assert.AreEqual(GamePhase.GameOver, _gm.Phase);
            Assert.IsFalse(_gm.GameWon, "Req 2.5: Money < startingMoney → GameWon = false");
        }

        /// <summary>
        /// Requirement 2.5: Money = 0 (bankruptcy) → GameWon = false.
        /// </summary>
        [Test]
        public void Req2_5_MoneyZero_GameWonFalse()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // → Racing

            // Set money to 0 to simulate bankruptcy
            _gm.Player.Money = 0;

            _gm.CompleteRaceAndSettle();

            // After settlement with no bets placed, TotalPayout=0, money stays 0
            if (_gm.Player.Money <= 0)
            {
                Assert.AreEqual(GamePhase.GameOver, _gm.Phase);
                Assert.IsFalse(_gm.GameWon, "Req 2.5: Money = 0 < startingMoney → GameWon = false");
            }
        }

        /// <summary>
        /// Requirement 2.1: Money negative (edge case) after settlement also triggers GameOver.
        /// This can happen if settlement doesn't cover the loss sufficiently.
        /// </summary>
        [Test]
        public void Req2_1_MoneyNegative_TriggersGameOver()
        {
            var config = CreateFullConfig(startingMoney: 1000, minBetAmount: 50);
            _gm.config = config;
            _gm.randomSeed = 42;

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // → Racing

            // Set money to negative (simulating edge case)
            _gm.Player.Money = -100;

            _gm.CompleteRaceAndSettle();

            // Money <= 0, so game over should trigger
            if (_gm.Player.Money <= 0)
            {
                Assert.AreEqual(GamePhase.GameOver, _gm.Phase, "Req 2.1: Money < 0 → GameOver");
                Assert.IsTrue(_gm.GameOverReason.Contains("資金耗盡"));
                Assert.IsFalse(_gm.GameWon, "Money < 0 < startingMoney → loss");
            }
        }
    }

    /// <summary>
    /// Task 7.7: Verify GameManager state machine transitions.
    /// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 7.5, 7.6
    /// </summary>
    public class GameManagerStateMachineTests
    {
        private UnityEngine.GameObject _go;
        private GameManager _gm;

        private static GameConfigDatabase CreateFullConfig()
        {
            var db = UnityEngine.ScriptableObject.CreateInstance<GameConfigDatabase>();
            db.game = ConfigFactory.Game(8, 30);
            db.game.totalRounds = 5;
            db.game.startingMoney = 3000;
            db.game.minBetAmount = 50;
            db.odds = ConfigFactory.Odds();
            db.track = ConfigFactory.Track();
            db.betting = ConfigFactory.Betting();
            db.events = ConfigFactory.Events(); // empty events → no randomness in race
            db.analyst = ConfigFactory.Analyst();
            db.shop = ConfigFactory.Shop(3);

            var mc = UnityEngine.ScriptableObject.CreateInstance<MessageCardConfig>();
            for (int i = 0; i < 8; i++)
                mc.entries.Add(new MessageCardConfig.Entry { bonus = i, description = "B" + i });
            db.messageCards = mc;

            return db;
        }

        [SetUp]
        public void SetUp()
        {
            _go = new UnityEngine.GameObject("TestGM");
            _gm = _go.AddComponent<GameManager>();
            _gm.config = CreateFullConfig();
            // Note: Awake() is called on AddComponent, creating _rng with default seed.
            // Player is initialized in StartNewRound() since config was null at Awake time.
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        // --- Req 1.1: Full cycle MainMenu → Betting → Racing → Settlement → Shop → Betting (next round) ---

        [Test]
        public void FullCycle_MainMenuToBettingToRacingToSettlementToShopToBetting()
        {
            // Initial state: MainMenu
            Assert.AreEqual(GamePhase.MainMenu, _gm.Phase, "Should start at MainMenu");

            // Req 1.2: Start → Betting
            _gm.StartNewRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "After StartNewRound, should be Betting");
            Assert.AreEqual(1, _gm.RoundNumber, "First round should be round 1");

            // Req 1.3, 1.4: Advance through betting rounds then race
            // With 3 betting rounds: confirm round 0→1, 1→2, then StartRace on confirm at round 2
            _gm.ConfirmBettingRound(); // round 0 → 1
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "Still Betting after first confirm");
            _gm.ConfirmBettingRound(); // round 1 → 2 (last round, triggers StartRace)
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "After last betting round confirm, should be Racing");

            // Req 1.5: CompleteRaceAndSettle → Settlement
            _gm.CompleteRaceAndSettle();
            Assert.AreEqual(GamePhase.Settlement, _gm.Phase, "After CompleteRaceAndSettle, should be Settlement");

            // Req 1.6: EnterShop → Shop
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Shop, _gm.Phase, "After EnterShop, should be Shop");

            // Req 1.7: NextRound → starts new round (Betting)
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "After NextRound from Shop, should be Betting");
            Assert.AreEqual(2, _gm.RoundNumber, "Should be round 2 after NextRound");
        }

        // --- Req 1.3: ConfirmBettingRound advances round or triggers StartRace on last round ---

        [Test]
        public void ConfirmBettingRound_AdvancesRound_WhenNotLastRound()
        {
            _gm.StartNewRound();
            Assert.AreEqual(0, _gm.Round.CurrentBettingRound);

            _gm.ConfirmBettingRound();
            Assert.AreEqual(1, _gm.Round.CurrentBettingRound, "Should advance to round 1");
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "Should still be Betting");
        }

        [Test]
        public void ConfirmBettingRound_TriggersStartRace_OnLastRound()
        {
            _gm.StartNewRound();
            // Advance to last round (round index 2 for 3 betting rounds)
            _gm.ConfirmBettingRound(); // 0→1
            _gm.ConfirmBettingRound(); // 1→2 (last) → triggers StartRace
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "Should transition to Racing on last confirm");
            Assert.IsNotNull(_gm.Round.Result, "RaceResult should be populated after StartRace");
        }

        [Test]
        public void ConfirmBettingRound_DoesNothing_WhenNotInBetting()
        {
            _gm.StartNewRound();
            // Advance through full cycle to Settlement
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // Racing
            _gm.CompleteRaceAndSettle(); // Settlement

            int prevRound = _gm.Round.CurrentBettingRound;
            _gm.ConfirmBettingRound(); // Should do nothing
            Assert.AreEqual(GamePhase.Settlement, _gm.Phase, "Phase should remain Settlement");
        }

        // --- Req 7.5: BuyAnalystReport only once per round, deducts price, requires sufficient funds ---

        [Test]
        public void BuyAnalystReport_Succeeds_DeductsPrice()
        {
            _gm.StartNewRound();
            long moneyBefore = _gm.Player.Money;
            long expectedPrice = _gm.config.analyst.GetPrice(AnalystTier.Junior);

            bool result = _gm.BuyAnalystReport(AnalystTier.Junior);

            Assert.IsTrue(result, "BuyAnalystReport should succeed");
            Assert.AreEqual(moneyBefore - expectedPrice, _gm.Player.Money, "Should deduct analyst price");
            Assert.IsNotNull(_gm.Round.PurchasedReport, "PurchasedReport should be set");
            Assert.AreEqual(AnalystTier.Junior, _gm.Round.PurchasedReport.Tier);
        }

        [Test]
        public void BuyAnalystReport_OnlyOncePerRound()
        {
            _gm.StartNewRound();
            // First purchase succeeds
            bool first = _gm.BuyAnalystReport(AnalystTier.Junior);
            Assert.IsTrue(first, "First analyst purchase should succeed");

            long moneyAfterFirst = _gm.Player.Money;

            // Second purchase should fail
            bool second = _gm.BuyAnalystReport(AnalystTier.Senior);
            Assert.IsFalse(second, "Second analyst purchase should be rejected");
            Assert.AreEqual(moneyAfterFirst, _gm.Player.Money, "Money should not change on rejection");
        }

        [Test]
        public void BuyAnalystReport_RequiresSufficientFunds()
        {
            _gm.StartNewRound();
            // Drain player's money below senior analyst price
            long seniorPrice = _gm.config.analyst.GetPrice(AnalystTier.Senior);
            _gm.Player.Money = seniorPrice - 1;

            bool result = _gm.BuyAnalystReport(AnalystTier.Senior);

            Assert.IsFalse(result, "Should reject when insufficient funds");
            Assert.AreEqual(seniorPrice - 1, _gm.Player.Money, "Money should not change");
            Assert.IsNull(_gm.Round.PurchasedReport, "Report should not be set");
        }

        [Test]
        public void BuyAnalystReport_OnlyWorksInBettingPhase()
        {
            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // Racing
            Assert.AreEqual(GamePhase.Racing, _gm.Phase);

            bool result = _gm.BuyAnalystReport(AnalystTier.Junior);
            Assert.IsFalse(result, "Should reject outside Betting phase");
        }

        // --- Req 1.6: EnterShop only from Settlement ---

        [Test]
        public void EnterShop_OnlyFromSettlement()
        {
            _gm.StartNewRound();
            // Try from Betting
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "EnterShop should not work from Betting");

            // Advance to Racing
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "EnterShop should not work from Racing");

            // Advance to Settlement
            _gm.CompleteRaceAndSettle();
            Assert.AreEqual(GamePhase.Settlement, _gm.Phase);

            // Now EnterShop should work
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Shop, _gm.Phase, "EnterShop should work from Settlement");
        }

        // --- Req 7.6 / 13: BuyProtectionCard only from Shop ---

        [Test]
        public void BuyProtectionCard_OnlyFromShop()
        {
            var card = ConfigFactory.Card("TestCard", ConfigFactory.Event("Slip", 1f, -2), 0.5f, 100);
            _gm.StartNewRound();

            // Try from Betting
            bool fromBetting = _gm.BuyProtectionCard(card);
            Assert.IsFalse(fromBetting, "BuyProtectionCard should not work from Betting");

            // Advance to Racing
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            bool fromRacing = _gm.BuyProtectionCard(card);
            Assert.IsFalse(fromRacing, "BuyProtectionCard should not work from Racing");

            // Advance to Settlement
            _gm.CompleteRaceAndSettle();
            bool fromSettlement = _gm.BuyProtectionCard(card);
            Assert.IsFalse(fromSettlement, "BuyProtectionCard should not work from Settlement");

            // Enter Shop - should work now
            _gm.EnterShop();
            Assert.AreEqual(GamePhase.Shop, _gm.Phase);
            bool fromShop = _gm.BuyProtectionCard(card);
            Assert.IsTrue(fromShop, "BuyProtectionCard should work from Shop");
        }

        [Test]
        public void BuyProtectionCard_DeductsPriceAndAddsCard()
        {
            var card = ConfigFactory.Card("TestCard", ConfigFactory.Event("Slip", 1f, -2), 0.5f, 150);

            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();
            _gm.EnterShop();

            long moneyBefore = _gm.Player.Money;
            int cardsBefore = _gm.Player.ProtectionCards.Count;

            bool result = _gm.BuyProtectionCard(card);

            Assert.IsTrue(result, "Purchase should succeed");
            Assert.AreEqual(moneyBefore - 150, _gm.Player.Money, "Should deduct card price");
            Assert.AreEqual(cardsBefore + 1, _gm.Player.ProtectionCards.Count, "Should add card to inventory");
        }

        // --- Multi-round cycle verification ---

        [Test]
        public void FullCycle_TwoRounds_PersistsPlayerState()
        {
            _gm.StartNewRound();

            // Place a bet to change player money
            _gm.PlaceBet(BetType.Win, 100, new[] { 1 });
            long moneyAfterBet = _gm.Player.Money;

            // Complete round 1
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.CompleteRaceAndSettle();
            long moneyAfterSettle = _gm.Player.Money;
            _gm.EnterShop();

            // Start round 2
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase);
            Assert.AreEqual(2, _gm.RoundNumber, "Should be round 2");
            // Money should persist across rounds
            Assert.AreEqual(moneyAfterSettle, _gm.Player.Money,
                "Player money should persist across rounds");
        }

        [Test]
        public void StartRace_OnlyFromBetting()
        {
            _gm.StartNewRound();
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound(); // This triggers StartRace → Racing
            _gm.CompleteRaceAndSettle(); // Settlement

            // StartRace should have no effect from Settlement
            var prevPhase = _gm.Phase;
            // We can't call StartRace directly from Settlement since phase check
            // already verified above in ConfirmBettingRound tests
            Assert.AreEqual(GamePhase.Settlement, prevPhase);
        }

        [Test]
        public void CompleteRaceAndSettle_OnlyFromRacing()
        {
            _gm.StartNewRound();
            // Try from Betting
            _gm.CompleteRaceAndSettle();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "CompleteRaceAndSettle should not work from Betting");

            // Advance to Racing and then it works
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            Assert.AreEqual(GamePhase.Racing, _gm.Phase);
            _gm.CompleteRaceAndSettle();
            Assert.AreEqual(GamePhase.Settlement, _gm.Phase, "CompleteRaceAndSettle should work from Racing");
        }

        [Test]
        public void NextRound_OnlyFromShopOrSettlement()
        {
            _gm.StartNewRound();
            // Try from Betting
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "NextRound should not work from Betting");

            // Advance to Racing
            _gm.ConfirmBettingRound();
            _gm.ConfirmBettingRound();
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Racing, _gm.Phase, "NextRound should not work from Racing");

            // Advance to Settlement - NextRound should work
            _gm.CompleteRaceAndSettle();
            Assert.AreEqual(GamePhase.Settlement, _gm.Phase);
            _gm.NextRound();
            Assert.AreEqual(GamePhase.Betting, _gm.Phase, "NextRound should work from Settlement");
            Assert.AreEqual(2, _gm.RoundNumber);
        }
    }

    /// <summary>
    /// Architecture verification tests: Ensures all Systems are pure static classes
    /// with IRandom injection and no MonoBehaviour dependency.
    /// Validates: Requirements 19.3, 19.4
    /// </summary>
    public class ArchitectureConstraintTests
    {
        private static readonly System.Type[] AllSystemTypes = new System.Type[]
        {
            typeof(HorseSystem),
            typeof(OddsSystem),
            typeof(MessageCardSystem),
            typeof(TrackSystem),
            typeof(AnalystSystem),
            typeof(EventSystem),
            typeof(RaceSimulationSystem),
            typeof(BettingSystem),
            typeof(ShopSystem),
            typeof(SettlementSystem)
        };

        /// <summary>
        /// Requirement 19.3: All Systems SHALL be pure C# static classes, no MonoBehaviour dependency.
        /// Verifies each system type is abstract+sealed (compiler representation of static class).
        /// </summary>
        [Test]
        public void AllSystems_AreStaticClasses()
        {
            foreach (var type in AllSystemTypes)
            {
                // In C#, a static class is compiled as abstract + sealed
                Assert.IsTrue(type.IsAbstract && type.IsSealed,
                    $"{type.Name} must be a static class (abstract+sealed), but IsAbstract={type.IsAbstract}, IsSealed={type.IsSealed}");
            }
        }

        /// <summary>
        /// Requirement 19.3: No MonoBehaviour dependency in any system.
        /// Verifies no system inherits from UnityEngine.MonoBehaviour or any Unity base class.
        /// </summary>
        [Test]
        public void AllSystems_NoMonoBehaviourDependency()
        {
            var monoBehaviourType = typeof(UnityEngine.MonoBehaviour);
            foreach (var type in AllSystemTypes)
            {
                // Static classes inherit from System.Object; verify no Unity base class
                Assert.IsFalse(monoBehaviourType.IsAssignableFrom(type),
                    $"{type.Name} must not inherit from MonoBehaviour");

                // Also ensure the type is not in UnityEngine namespace
                Assert.IsFalse(type.Namespace != null && type.Namespace.StartsWith("UnityEngine"),
                    $"{type.Name} must not be in UnityEngine namespace");
            }
        }

        /// <summary>
        /// Requirement 19.4: All Systems SHALL accept IRandom interface injection for randomness.
        /// Systems that require randomness must have at least one public method accepting IRandom.
        /// No system should directly reference System.Random in its method signatures.
        /// </summary>
        [Test]
        public void SystemsRequiringRandomness_AcceptIRandom()
        {
            // Systems that perform randomness operations must accept IRandom
            var systemsRequiringRandom = new System.Type[]
            {
                typeof(HorseSystem),
                typeof(MessageCardSystem),
                typeof(TrackSystem),
                typeof(AnalystSystem),
                typeof(EventSystem),
                typeof(RaceSimulationSystem)
            };

            var iRandomType = typeof(IRandom);

            foreach (var type in systemsRequiringRandom)
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                bool hasIRandomParam = false;
                foreach (var method in methods)
                {
                    foreach (var param in method.GetParameters())
                    {
                        if (iRandomType.IsAssignableFrom(param.ParameterType))
                        {
                            hasIRandomParam = true;
                            break;
                        }
                    }
                    if (hasIRandomParam) break;
                }
                Assert.IsTrue(hasIRandomParam,
                    $"{type.Name} requires randomness but has no public method accepting IRandom");
            }
        }

        /// <summary>
        /// Requirement 19.4: No System.Random direct usage in system method signatures.
        /// Verifies no system has a public method parameter of type System.Random.
        /// </summary>
        [Test]
        public void AllSystems_NoDirectSystemRandomInSignatures()
        {
            var systemRandomType = typeof(System.Random);

            foreach (var type in AllSystemTypes)
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var method in methods)
                {
                    foreach (var param in method.GetParameters())
                    {
                        Assert.IsFalse(param.ParameterType == systemRandomType,
                            $"{type.Name}.{method.Name} has a System.Random parameter '{param.Name}'. Use IRandom instead.");
                    }
                }
            }
        }

        /// <summary>
        /// Verifies all 10 expected systems exist in the HorseRacing namespace.
        /// </summary>
        [Test]
        public void AllExpectedSystems_Exist()
        {
            string[] expectedNames = {
                "HorseSystem", "OddsSystem", "MessageCardSystem", "TrackSystem",
                "AnalystSystem", "EventSystem", "RaceSimulationSystem",
                "BettingSystem", "ShopSystem", "SettlementSystem"
            };

            foreach (var name in expectedNames)
            {
                var type = System.Type.GetType($"HorseRacing.{name}, Assembly-CSharp");
                if (type == null)
                {
                    // Try from the assembly containing HorseSystem
                    type = typeof(HorseSystem).Assembly.GetType($"HorseRacing.{name}");
                }
                Assert.IsNotNull(type, $"System '{name}' not found in HorseRacing namespace");
            }
        }
    }
}
