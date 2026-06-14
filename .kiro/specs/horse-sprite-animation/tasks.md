# Implementation Plan: Horse Sprite Animation

## Overview

Replace the single shared `horseSprite` with per-character sprite sheet animations. This involves creating a `HorseSpriteConfig` ScriptableObject for sprite data management, extending `RaceAnimConfig` with a frame rate field, modifying `GameUI` to display per-horse icons in the betting panel, and modifying `RaceView` to play frame-by-frame running animations during the race. All animation logic must be zero-allocation and maintain 30fps with 8 simultaneous animations.

## Tasks

- [x] 1. Create HorseSpriteConfig ScriptableObject and data asset
  - [x] 1.1 Create `HorseSpriteConfig.cs` in `Assets/HorseRacing/Scripts/Config/`
    - Define `HorseSpriteEntry` struct with `Sprite[] frames` field
    - Define `HorseSpriteEntry[] entries` array (length 8) and `Sprite[] defaultFrames` fallback
    - Implement `GetSprites(int horseId)` method: return `entries[horseId-1].frames` if valid (non-null, length > 0), else return `defaultFrames`
    - Add `[CreateAssetMenu]` attribute with fileName "HorseSpriteConfig" and menuName "HorseRacing/Horse Sprite Config"
    - Add `OnValidate()` to log warning if `defaultFrames` is null or empty
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ]* 1.2 Write property test for `GetSprites` lookup (Property 1)
    - **Property 1: Config lookup always returns a valid non-empty sprite array**
    - Use FsCheck generators: random int for horseId, random config with some null/empty entries
    - Assert: result is never null, result.Length >= 1
    - Assert: for valid IDs [1,8] with valid entries, returns `entries[id-1].frames`; otherwise returns `defaultFrames`
    - **Validates: Requirements 2.3, 2.4, 2.5**

  - [x] 1.3 Create `HorseSpriteConfig.asset` in `Assets/HorseRacing/Data/`
    - Create the ScriptableObject asset via script or manually in Unity
    - Assign the 8 sprite sheet frame arrays (horse_, cat, goldfish, grandma, spongebob, tardis, thief, tombstone)
    - Assign `defaultFrames` to the horse_ sprite frames as fallback
    - _Requirements: 2.1, 2.3_

- [x] 2. Extend RaceAnimConfig with sprite frame rate
  - [x] 2.1 Add `spriteFrameRate` field to `RaceAnimConfig.cs`
    - Add `[Header("Horse Sprite Animation")]` section
    - Add `[Range(1, 30)] public int spriteFrameRate = 8;` field with tooltip
    - _Requirements: 4.2_

  - [ ]* 2.2 Write property test for frame rate clamping (Property 4)
    - **Property 4: Frame rate is clamped to valid range**
    - Use FsCheck generators: random int values including negatives, 0, large values
    - Assert: effective frame rate equals `Mathf.Clamp(value, 1, 30)`
    - **Validates: Requirements 4.2**

- [x] 3. Checkpoint - Ensure data layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Modify GameUI to display per-horse icons in the betting panel
  - [x] 4.1 Add `HorseSpriteConfig` reference to `GameUI` and update betting panel
    - Add `public HorseSpriteConfig horseSpriteConfig;` serialized field to `GameUI`
    - In `BuildBettingPanel()`, replace the 28×28 solid-color "Chip" Image with a sprite Image
    - Set sprite to `horseSpriteConfig.GetSprites(horseId)[0]` for each horse row
    - Set `preserveAspect = true` on the Image component
    - Set LayoutElement constraints: `prefW: 52, prefH: 52, minH: 52`
    - Add fallback: if `horseSpriteConfig` is null, log error and use existing `horseSprite`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 4.2 Write property test for betting icon correctness (Property 2)
    - **Property 2: Betting icon matches frame 0 from config**
    - Use FsCheck generators: random valid configs with distinct frame arrays
    - Assert: for any horseId in [1,8], the icon sprite equals `GetSprites(horseId)[0]`
    - **Validates: Requirements 3.1, 3.5**

- [x] 5. Modify RaceView for frame-by-frame sprite animation
  - [x] 5.1 Add sprite animation state and config references to `RaceView`
    - Add `HorseSpriteConfig` field, received via an updated `Init()` method (add parameter)
    - Pre-allocate `Image[] _horseImages` (length 8) — store references to horse sprite Image components created in `Build()`
    - Pre-allocate `Sprite[][] _horseSpriteArrays` (length 8) — cached from config at race start
    - Pre-allocate `bool[] _finished` (length 8) — track finished state per horse
    - Update `Init()` signature to accept `HorseSpriteConfig` and store reference
    - Update `GameUI.BuildRacePanel()` to pass `horseSpriteConfig` to `RaceView.Init()`
    - _Requirements: 4.4, 5.1, 5.2, 5.3_

  - [x] 5.2 Implement frame-cycling animation logic in `RaceView.Run()` coroutine
    - At race start, cache `_horseSpriteArrays[i] = horseSpriteConfig.GetSprites(i+1)` for each horse
    - Read `spriteFrameRate` from `RaceAnimConfig` (clamp to [1,30])
    - Each frame in the loop: compute `frameIndex = (int)(elapsed * fps) % arrayLength` for each horse
    - Assign `_horseImages[i].sprite = _horseSpriteArrays[i][frameIndex]`
    - If sprite array is null or length < 2, skip animation for that horse (keep static `_horseSprite`)
    - If individual sprite at frameIndex is null, skip assignment and retain previous sprite
    - When a horse crosses finish line, set `_finished[i] = true` and assign frame 0
    - Maintain existing bounce/bob motion for animated sprites
    - Zero allocation: no new arrays, lists, or string operations in the animation loop
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 5.2, 5.3, 5.4, 5.5_

  - [ ]* 5.3 Write property test for frame index calculation (Property 3)
    - **Property 3: Frame index calculation is correct**
    - Use FsCheck generators: random fps ∈ [1,30], random array length ∈ [2,20], random elapsed ∈ [0,60]
    - Assert: frameIndex == `(int)(elapsed * fps) % arrayLength`
    - Assert: frameIndex is always in range [0, arrayLength-1]
    - **Validates: Requirements 4.1, 4.4**

  - [ ]* 5.4 Write property test for null sprite safety (Property 5)
    - **Property 5: Null sprite entries never cause exceptions**
    - Use FsCheck generators: random arrays with null entries at random positions
    - Assert: frame update logic never throws an exception
    - Assert: last valid sprite is retained when current is null
    - **Validates: Requirements 5.5**

- [x] 6. Checkpoint - Ensure full integration compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Sprite sheet import validation
  - [ ] 7.1 Create editor validation script for sprite sheet import settings
    - Create `HorseSpriteValidator.cs` in `Assets/HorseRacing/Scripts/Config/` (or an Editor folder)
    - Validate each sprite sheet in `Assets/HorseRacing/Art/Horses/` has sprite mode "Multiple"
    - Validate `alphaIsTransparency` is enabled
    - Validate each sheet produces at least 2 sub-sprites after slicing
    - Log warning to Unity Console if a sheet has fewer than 2 frames
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [ ]* 7.2 Write unit tests for sprite import validation
    - Test that validator correctly identifies sheets with < 2 frames
    - Test that validator accepts properly configured sheets
    - _Requirements: 1.1, 1.2, 1.5_

- [ ] 8. Final checkpoint - Ensure all tests pass and components wire together
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- All animation arrays are pre-allocated in `Build()` to guarantee zero per-frame heap allocation
- The `Init()` signature change in RaceView (task 5.1) must be coordinated with the GameUI call site (also in 5.1)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.2"] },
    { "id": 2, "tasks": ["4.1", "5.1"] },
    { "id": 3, "tasks": ["4.2", "5.2"] },
    { "id": 4, "tasks": ["5.3", "5.4", "7.1"] },
    { "id": 5, "tasks": ["7.2"] }
  ]
}
```
