# Implementation Plan: Horse Racing PRD Alignment

## Overview

This plan aligns the existing Unity 2D horse racing betting simulation with all PRD §2–§14 requirements. The codebase already has functional Config, Domain, Systems, Flow, and UI layers. Tasks focus on verifying completeness, filling implementation gaps (analyst UI, shop UI refinements, game-over flows, multi-round state), and adding comprehensive property-based tests for all 24 correctness properties.

## Tasks

- [x] 1. Verify and complete Config layer ScriptableObject coverage
  - [x] 1.1 Audit GameConfigDatabase and ensure all sub-config references are wired
    - Verify `GameConfigDatabase` exposes all 8 sub-configs: game, messageCards, odds, track, events, analyst, betting, shop
    - Ensure no hard-coded game values exist in GameManager or Systems; all values flow from config
    - Add any missing fields identified during audit (e.g. `totalRounds` in GameConfig if absent)
    - _Requirements: 19.1, 19.2_

  - [x] 1.2 Verify AnalystConfig completeness
    - Ensure `AnalystConfig` has fields: juniorPrice, seniorPrice, juniorAccuracy, seniorAccuracy, statementsPerReport
    - Add `GetPrice(AnalystTier)` and `GetAccuracy(AnalystTier)` helpers if not present
    - Validate seniorAccuracy > juniorAccuracy constraint is documented in Inspector tooltip
    - _Requirements: 7.1, 7.2, 7.3, 7.7_

  - [x] 1.3 Verify MessageCardConfig bonus-to-description table completeness
    - Ensure entries cover all values in the default `hiddenBonusPool` (0..7)
    - Add fallback description for unmapped bonus values
    - _Requirements: 4.2, 4.4_

- [x] 2. Verify and complete Domain layer data models
  - [x] 2.1 Audit Horse model and add InitialScore property
    - Ensure `Horse` has: Id, BaseSpeed, HiddenBonus, TrackModifier, StageEventModifiers (List<int>), FinalSpeed
    - Verify `InitialScore` property returns `BaseSpeed + HiddenBonus`
    - _Requirements: 3.1, 3.2, 10.2_

  - [x] 2.2 Verify Enums completeness
    - Confirm `BetType` has all six values: Win, Place, Quinella, Exacta, Trio, Trifecta
    - Confirm `TrackType` has: Grass, Mud, Snow
    - Confirm `EventTarget` has: RandomSingleHorse, AllHorses
    - Confirm `AnalystTier` has: Junior, Senior
    - _Requirements: 6.1, 7.1, 8.3, 11.1_

- [x] 3. Verify and complete core Systems implementations
  - [x] 3.1 Verify HorseSystem.GenerateHorses produces valid unique permutation
    - Confirm sequential IDs 1..N, same BaseSpeed, unique HiddenBonus from pool
    - Ensure Fisher-Yates shuffle via IRandom for bonus assignment
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x]* 3.2 Write property test: Horse Generation Produces Valid Unique Permutation
    - **Property 1: Horse Generation Produces Valid Unique Permutation**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

  - [x] 3.3 Verify MessageCardSystem.DrawCards logic
    - Confirm draws exactly `rounds` cards with distinct horse IDs
    - Confirm each card's Description matches `config.GetDescription(horse.HiddenBonus)`
    - Confirm card.Round is assigned sequentially (0, 1, 2)
    - _Requirements: 4.1, 4.2, 4.3_

  - [x]* 3.4 Write property tests: Message Card Drawing and Reveal Filtering
    - **Property 2: Message Card Drawing Selects Distinct Horses with Correct Descriptions**
    - **Validates: Requirements 4.1, 4.2**
    - **Property 3: Message Card Reveal Filtering by Round**
    - **Validates: Requirements 4.3**

  - [x] 3.5 Verify OddsSystem.ComputeOdds formula and ranking
    - Confirm ranking by InitialScore desc with Id tie-break
    - Confirm formula: `max(minOdds, baseRankOdds[rank] × roundPayoutMultiplier[round])`
    - Confirm odds decrease across rounds when multiplier is decreasing
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x]* 3.6 Write property tests: Odds Ranking, Formula, and Monotonic Decrease
    - **Property 4: Odds Ranking with Tie-Break**
    - **Validates: Requirements 5.1, 5.2**
    - **Property 5: Odds Formula Correctness**
    - **Validates: Requirements 5.3, 5.4, 5.5**
    - **Property 6: Odds Monotonic Decrease Across Rounds**
    - **Validates: Requirements 5.6**

  - [x] 3.7 Verify TrackSystem: PickTrack and ApplyTrackModifiers
    - Confirm PickTrack returns a TrackType from config.tracks list
    - Confirm ApplyTrackModifiers sets horse.TrackModifier to `preferences[horseId-1].{trackType}`
    - _Requirements: 6.1, 6.2, 6.4, 6.5_

  - [x]* 3.8 Write property tests: Track Selection and Modifier Application
    - **Property 7: Track Modifier Application Matches Preference Table**
    - **Validates: Requirements 6.4**
    - **Property 8: Track Selection Is Valid**
    - **Validates: Requirements 6.1, 6.2**

  - [x] 3.9 Verify AnalystSystem.GenerateReport logic
    - Confirm produces exactly `statementsPerReport` statements
    - Confirm accuracy mechanism: RNG < accuracy → truthful, else misleading
    - Confirm truthful = correctly identifies top-3 by InitialScore
    - _Requirements: 7.2, 7.4_

  - [x]* 3.10 Write property test: Analyst Report Statement Count and Accuracy
    - **Property 9: Analyst Report Statement Count and Accuracy Mechanism**
    - **Validates: Requirements 7.2, 7.4**

  - [x] 3.11 Verify EventSystem.ResolveStage trigger and targeting logic
    - Confirm per-event trigger check: RNG < triggerChance
    - Confirm AllHorses affects all, RandomSingleHorse picks exactly one
    - Confirm speedModifier is appended to horse.StageEventModifiers
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x]* 3.12 Write property tests: Event Trigger and Speed Modifier Application
    - **Property 10: Event Trigger Mechanism**
    - **Validates: Requirements 8.1, 8.2, 8.3**
    - **Property 11: Event Speed Modifier Application**
    - **Validates: Requirements 8.4**

  - [x] 3.13 Verify EventSystem defense card logic
    - Confirm: matching negative event → consume card always
    - Confirm: if RNG < defendChance → modifier = 0, else full modifier applied
    - Confirm: card removed from list regardless of defense success
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x]* 3.14 Write property test: Defense Card Consumption and Effect
    - **Property 12: Defense Card Consumption and Effect**
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Verify and complete RaceSimulation, Betting, Shop, and Settlement systems
  - [x] 5.1 Verify RaceSimulationSystem.Simulate three-stage pipeline
    - Confirm: ApplyTrackModifiers → 3× ResolveStage → compute FinalSpeed → rank
    - Confirm FinalSpeed = BaseSpeed + HiddenBonus + TrackModifier + Σ(StageEventModifiers)
    - Confirm ranking by FinalSpeed desc with Id tie-break
    - Confirm RaceResult.RankToHorseId is populated correctly
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x]* 5.2 Write property tests: FinalSpeed Formula and Race Result Ranking
    - **Property 14: FinalSpeed Formula Invariant**
    - **Validates: Requirements 10.2**
    - **Property 15: Race Result Ranking with Tie-Break**
    - **Validates: Requirements 10.3, 10.4, 10.5**

  - [x] 5.3 Verify BettingSystem: CreateBet payout multiplier locking and IsWin logic
    - Confirm Win bet locks dynamic WinOdds at bet time
    - Confirm non-Win bets use BettingConfig.payoutMultiplier
    - Confirm IsWin for all 6 bet types matches specification
    - Confirm SettleBet returns `round(Amount × PayoutMultiplier)` for wins, 0 for losses
    - _Requirements: 11.2, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8, 11.9_

  - [x]* 5.4 Write property tests: Bet Type Outcome and Payout Multiplier Source
    - **Property 16: Bet Type Outcome Correctness**
    - **Validates: Requirements 11.2, 11.3, 11.4, 11.5, 11.6, 11.7**
    - **Property 17: Bet Payout Multiplier Source**
    - **Validates: Requirements 11.8, 11.9**

  - [x] 5.5 Verify ShopSystem.Buy and CanBuy guards
    - Confirm maxHeldCards enforcement
    - Confirm price deduction and card addition on success
    - Confirm rejection when money < price
    - _Requirements: 13.2, 13.3, 13.4, 13.5_

  - [x]* 5.6 Write property tests: Shop Purchase, Rejection, and Max Hold
    - **Property 13: Protection Card Maximum Hold Invariant**
    - **Validates: Requirements 9.5, 13.4**
    - **Property 20: Shop Purchase Deduction and Addition**
    - **Validates: Requirements 13.2, 13.3**
    - **Property 21: Shop Purchase Rejection**
    - **Validates: Requirements 13.5**

  - [x] 5.7 Verify SettlementSystem.Settle arithmetic
    - Confirm TotalStaked = Σ(bet.Amount)
    - Confirm TotalPayout = Σ(round(bet.Amount × bet.PayoutMultiplier)) for winning bets
    - Confirm Net = TotalPayout - TotalStaked
    - Confirm player.Money increases by exactly TotalPayout
    - _Requirements: 14.1, 14.2, 14.3, 14.4_

  - [x]* 5.8 Write property test: Settlement Arithmetic Consistency
    - **Property 22: Settlement Arithmetic Consistency**
    - **Validates: Requirements 14.1, 14.2, 14.3, 14.4**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement and verify GameManager FSM and game flow logic
  - [x] 7.1 Verify GameManager PlaceBet validation guards
    - Confirm rejection when: amount < minBetAmount, amount > Money, horseIds null/empty, Phase != Betting
    - Confirm player.Money deduction on success
    - Confirm Notice messages for each rejection case
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

  - [x]* 7.2 Write property tests: Bet Validation Guards and Successful Bet Deduction
    - **Property 18: Bet Validation Guards**
    - **Validates: Requirements 12.1, 12.2, 12.5**
    - **Property 19: Successful Bet Deducts Amount**
    - **Validates: Requirements 12.3**

  - [x] 7.3 Verify GameManager game-over conditions and win/loss determination
    - Confirm Money <= 0 → GameOver after settlement
    - Confirm Money < minBetAmount && > 0 → GameOver on NextRound
    - Confirm totalRounds reached → GameOver on StartNewRound
    - Confirm GameWon = (Money >= startingMoney)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x]* 7.4 Write property test: Game Over Win/Loss Determination
    - **Property 24: Game Over Win/Loss Determination**
    - **Validates: Requirements 2.4, 2.5**

  - [x] 7.5 Verify multi-round state persistence
    - Confirm PlayerState.Money and ProtectionCards persist across rounds
    - Confirm new RoundContext is created each round (fresh horses, odds, cards, track)
    - Confirm RoundNumber increments correctly
    - Confirm consumed protection cards remain consumed in subsequent rounds
    - _Requirements: 20.1, 20.2, 20.3, 20.4_

  - [x]* 7.6 Write property test: Multi-Round Money Accounting Invariant
    - **Property 23: Multi-Round Money Accounting Invariant**
    - **Validates: Requirements 20.1, 20.5**

  - [x] 7.7 Verify GameManager state machine transitions
    - Confirm full cycle: MainMenu → Betting → Racing → Settlement → Shop → Betting (next round)
    - Confirm ConfirmBettingRound advances round or triggers StartRace on last round
    - Confirm BuyAnalystReport: only once per round, deducts price, requires sufficient funds
    - Confirm EnterShop only from Settlement, BuyProtectionCard only from Shop
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 7.5, 7.6_

- [x] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Verify and complete UI layer panels
  - [x] 9.1 Verify MainMenu panel displays title, starting money, total rounds, and start button
    - Confirm GameUI.BuildMenuPanel shows game title and description
    - Confirm starting money and total rounds info displayed
    - Confirm start button calls GameManager.StartNewRound()
    - _Requirements: 15.1, 15.2, 15.3_

  - [x] 9.2 Verify Betting panel completeness
    - Confirm displays all 8 horses with current odds
    - Confirm revealed message cards shown next to corresponding horses
    - Confirm current betting round indicator (N/3)
    - Confirm six bet type selection buttons
    - Confirm bet amount input/adjustment UI
    - Confirm bet summary list for current round
    - Confirm analyst purchase option visible on last round only
    - Confirm purchased analyst report statements displayed
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.5, 16.6, 16.7, 16.8_

  - [x] 9.3 Verify RaceView animation and display
    - Confirm 8 horses animate left-to-right on track
    - Confirm arrival order matches RaceResult.Standings
    - Confirm track type visual background or label displayed
    - Confirm animation completion triggers CompleteRaceAndSettle
    - _Requirements: 17.1, 17.2, 17.3, 17.4_

  - [x] 9.4 Verify Settlement/Result panel displays
    - Confirm track name, full standings, and per-horse FinalSpeed displayed
    - Confirm each bet outcome (win/loss) and payout shown
    - Confirm TotalStaked, TotalPayout, Net shown
    - Confirm "Enter Shop" button present
    - _Requirements: 18.1, 18.2, 18.3, 18.4_

  - [x] 9.5 Verify Shop panel displays and interactions
    - Confirm all available cards shown with name, price, target event, defend chance
    - Confirm current held cards count and detail displayed
    - Confirm "Next Round" button present
    - _Requirements: 13.1, 18.5, 18.6, 18.7_

  - [x] 9.6 Verify GameOver panel
    - Confirm displays game result (win/loss), reason, final money, and profit/loss
    - Confirm restart button returns to MainMenu
    - _Requirements: 2.6_

- [x] 10. Verify architecture constraints and dependency direction
  - [x] 10.1 Verify assembly references and dependency direction
    - Confirm HorseRacing.Core has no reference to HorseRacing.UI
    - Confirm HorseRacing.UI references HorseRacing.Core only
    - Confirm HorseRacing.Tests references HorseRacing.Core only
    - Confirm GameManager has no `using HorseRacing.UI` or UI type references
    - _Requirements: 19.5, 19.6_

  - [x] 10.2 Verify all Systems are pure static classes with IRandom injection
    - Confirm all 9 systems (Horse, Odds, MessageCard, Track, Analyst, Event, RaceSimulation, Betting, Shop, Settlement) are static classes
    - Confirm no MonoBehaviour dependency in any system
    - Confirm IRandom is used for all randomness (no System.Random direct usage in systems)
    - _Requirements: 19.3, 19.4_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate the 24 universal correctness properties defined in the design
- Unit tests (already existing in `SystemsTests.cs`) validate specific examples and edge cases
- The project uses NUnit (Unity Test Framework) in EditMode for all tests
- Property tests use randomized iteration (100+ iterations) with `System.Random` seeds for reproducibility
- All property tests should follow the pattern: `// Feature: horse-racing-prd-alignment, Property N: {description}`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "2.1", "2.2"] },
    { "id": 1, "tasks": ["3.1", "3.3", "3.5", "3.7", "3.9", "3.11", "3.13"] },
    { "id": 2, "tasks": ["3.2", "3.4", "3.6", "3.8", "3.10", "3.12", "3.14"] },
    { "id": 3, "tasks": ["5.1", "5.3", "5.5", "5.7"] },
    { "id": 4, "tasks": ["5.2", "5.4", "5.6", "5.8"] },
    { "id": 5, "tasks": ["7.1", "7.3", "7.5", "7.7"] },
    { "id": 6, "tasks": ["7.2", "7.4", "7.6"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "9.6"] },
    { "id": 8, "tasks": ["10.1", "10.2"] }
  ]
}
```
