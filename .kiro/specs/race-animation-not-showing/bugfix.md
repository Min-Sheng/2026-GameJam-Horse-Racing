# Bugfix Requirements Document

## Introduction

When the game transitions to the Racing phase, the race panel is shown but no horse markers are visible on the track. The `RaceView` component creates 8 horse markers and animates them left-to-right using a coroutine, but the horses do not appear visually during the race animation. The betting screen and all other panels work correctly.

The root cause is a layout timing issue: the race panel (`_racePanel`) is inactive during the Betting phase and is activated in the same frame that `RaceView.Play()` is called. The `RaceView.Run()` coroutine waits only one frame (`yield return null`) before reading `_self.rect.width` and `_self.rect.height` to compute horse positions. However, because the panel was just activated, Unity's layout system may not have fully rebuilt the RectTransform dimensions yet — resulting in a zero or incorrect `rect` size. When `width` is 0, all horse markers are positioned outside the visible area (or within a zero-size container), making them invisible.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the game transitions to GamePhase.Racing THEN the system activates the race panel and immediately calls `RaceView.Play()`, which reads layout dimensions after only one yield frame — resulting in `_self.rect` returning zero or stale dimensions for the newly-activated panel

1.2 WHEN `_self.rect.width` is zero or incorrect THEN the system calculates horse positions with invalid coordinates (startX=70, endX=-90), placing all horse markers outside the visible field area

1.3 WHEN horse markers are positioned outside the visible container bounds THEN the system displays an empty black race field with no horses visible to the player

### Expected Behavior (Correct)

2.1 WHEN the game transitions to GamePhase.Racing THEN the system SHALL ensure the race panel's layout dimensions are fully resolved before computing horse positions

2.2 WHEN computing horse animation positions THEN the system SHALL use valid, non-zero field dimensions that reflect the actual rendered size of the race track area

2.3 WHEN the race animation plays THEN the system SHALL display all 8 horse markers visibly on the race track, animating left-to-right with arrival order matching the simulated race result standings

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the race animation completes (all horses reach finish line) THEN the system SHALL CONTINUE TO call `OnRaceAnimationDone()` which advances the game to the Settlement phase

3.2 WHEN the game is in any phase other than Racing (MainMenu, Betting, Settlement, Shop, GameOver) THEN the system SHALL CONTINUE TO show the correct panel for that phase with the race panel hidden

3.3 WHEN the race animation is already playing and `Play()` is called again THEN the system SHALL CONTINUE TO ignore the duplicate call (the `_playing` guard prevents re-entry)

3.4 WHEN multiple rounds are played sequentially THEN the system SHALL CONTINUE TO correctly animate each race with fresh horse positions matching each round's race result
