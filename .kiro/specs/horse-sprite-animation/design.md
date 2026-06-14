# Design Document: Horse Sprite Animation

## Overview

This feature replaces the single shared `horseSprite` with per-character sprite sheet animations for a horse racing game in Unity. Each of the 8 race participants gets a unique sprite sheet that is sliced into animation frames. Frame 0 serves as a static icon in the Betting Panel, and the full frame sequence plays as a looping running animation in the Race View.

The design introduces a `HorseSpriteConfig` ScriptableObject that maps horse IDs (1–8) to sprite arrays, a frame-cycling animation system driven by elapsed time, and UI integration for both the betting panel icons and race view animation.

Key constraints:
- Zero per-frame heap allocation during animation
- Reuse existing Image components (no Instantiate/Destroy in the animation loop)
- Maintain 30 fps render performance with 8 simultaneous animations
- Graceful fallback when sprite data is missing or malformed

## Architecture

```mermaid
graph TD
    subgraph Data Layer
        A[HorseSpriteConfig ScriptableObject]
        B[RaceAnimConfig ScriptableObject]
    end

    subgraph UI Layer
        C[GameUI / Betting Panel]
        D[RaceView]
    end

    subgraph Art Assets
        E["Sprite Sheets (8x PNG, sliced)"]
    end

    E -->|Inspector assignment| A
    A -->|GetSprites(horseId)| C
    A -->|GetSprites(horseId)| D
    B -->|spriteFrameRate| D
    C -->|"frame 0 → Image.sprite"| F[Betting Row Icons]
    D -->|"frame[n] → Image.sprite"| G[Race Horse Images]
```

**Data flow:**
1. At edit time, sprite sheets are sliced and assigned to `HorseSpriteConfig` entries via Inspector.
2. At runtime, `GameUI.BuildBettingPanel()` queries `HorseSpriteConfig.GetSprites(horseId)[0]` for each row icon.
3. During racing, `RaceView` reads the frame rate from `RaceAnimConfig` and cycles through each horse's sprite array by computing `frameIndex = floor(elapsed * fps) % arrayLength`, assigning to the existing `Image.sprite` property each frame.

## Components and Interfaces

### HorseSpriteConfig (ScriptableObject)

**File:** `Assets/HorseRacing/Scripts/Config/HorseSpriteConfig.cs`

```csharp
namespace HorseRacing
{
    [CreateAssetMenu(fileName = "HorseSpriteConfig", menuName = "HorseRacing/Horse Sprite Config")]
    public class HorseSpriteConfig : ScriptableObject
    {
        [System.Serializable]
        public struct HorseSpriteEntry
        {
            public Sprite[] frames;
        }

        [Tooltip("8 entries, index 0 = Horse 1, index 7 = Horse 8")]
        public HorseSpriteEntry[] entries = new HorseSpriteEntry[8];

        [Tooltip("Fallback sprite array when an entry is null/empty")]
        public Sprite[] defaultFrames;

        /// <summary>
        /// Returns the sprite array for the given horse ID (1-based).
        /// Returns defaultFrames for out-of-range IDs or null/empty entries.
        /// </summary>
        public Sprite[] GetSprites(int horseId) { ... }
    }
}
```

**Key behaviors:**
- `GetSprites(horseId)`: Pure lookup function. Returns `entries[horseId - 1].frames` if valid (non-null, length > 0), else returns `defaultFrames`.
- Out-of-range IDs (< 1 or > 8) return `defaultFrames`.

### RaceAnimConfig Extension

**File:** `Assets/HorseRacing/Scripts/Config/RaceAnimConfig.cs` (extend existing)

Add a new field to the existing `RaceAnimConfig`:

```csharp
[Header("Horse Sprite Animation")]
[Tooltip("Animation frame rate (frames per second), range 1-30")]
[Range(1, 30)]
public int spriteFrameRate = 8;
```

### GameUI Modifications

**File:** `Assets/HorseRacing/Scripts/UI/GameUI.cs`

Changes:
- Add `public HorseSpriteConfig horseSpriteConfig;` serialized field.
- In `BuildBettingPanel()`, replace the solid-color chip `Image` with a sprite `Image` showing `horseSpriteConfig.GetSprites(horseId)[0]`, with `preserveAspect = true` and max size 52×52.
- Pass `horseSpriteConfig` to `RaceView.Init()`.

### RaceView Modifications

**File:** `Assets/HorseRacing/Scripts/UI/RaceView.cs`

Changes:
- Store a reference to `HorseSpriteConfig`.
- Store an `Image[]` array (length 8) referencing the horse sprite Image components created in `Build()`.
- Add per-horse animation state: `int[] _frameIndices` (pre-allocated), `bool[] _finished` (pre-allocated).
- In the animation loop (`Run` coroutine), compute `frameIndex = (int)(elapsed * fps) % frameCount` for each horse and assign `_horseImages[i].sprite = sprites[frameIndex]`.
- When a horse finishes, set `_finished[i] = true` and assign frame 0.
- If `sprites` is null or length < 2, skip animation for that horse (static fallback).
- Zero allocation: all arrays pre-allocated in `Build()`, frame index computed from elapsed time (no lists, no string ops in the loop).

### Betting Panel Icon Integration

The existing betting row creates a 28×28 solid-color "Chip" Image. This will be replaced with:
- An Image component with `preserveAspect = true`
- Size constraints: `LayoutElement` with `prefW: 52, prefH: 52, minH: 52`
- Sprite set to `horseSpriteConfig.GetSprites(horseId)[0]`
- Positioned as the first child in the HorizontalLayoutGroup (same position as the old chip)

## Data Models

### HorseSpriteConfig Asset

**File:** `Assets/HorseRacing/Data/HorseSpriteConfig.asset`

| Field | Type | Description |
|-------|------|-------------|
| entries | HorseSpriteEntry[8] | Each entry holds a Sprite[] of animation frames for one horse |
| defaultFrames | Sprite[] | Fallback frames (typically the original horse_ sprite) |

**Entry assignment (ID → sprite sheet):**

| Horse ID | Index | Sprite Sheet |
|----------|-------|-------------|
| 1 | 0 | horse_.png |
| 2 | 1 | cat.png |
| 3 | 2 | goldfish.png |
| 4 | 3 | grandma.png |
| 5 | 4 | spongebob.png |
| 6 | 5 | tardis.png |
| 7 | 6 | thief.png |
| 8 | 7 | tombstone.png |

### RaceAnimConfig Extension

| Field | Type | Default | Range | Description |
|-------|------|---------|-------|-------------|
| spriteFrameRate | int | 8 | 1–30 | Frames per second for horse running animation |

### Animation State (Runtime, pre-allocated in RaceView)

| Field | Type | Description |
|-------|------|-------------|
| _horseImages | Image[8] | References to existing horse Image components |
| _horseSpriteArrays | Sprite[8][] | Cached sprite arrays from config at race start |
| _finished | bool[8] | Whether each horse has crossed the finish line |

No per-frame allocations — frame index is computed inline from `elapsed * fps`.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Config lookup always returns a valid non-empty sprite array

*For any* horse ID (including values outside [1,8]) and *for any* config state (where some entries may be null or empty), `GetSprites(horseId)` SHALL always return a non-null array with length ≥ 1. For IDs in [1,8] with a valid entry, it returns `entries[id-1].frames`; otherwise it returns `defaultFrames`.

**Validates: Requirements 2.3, 2.4, 2.5**

### Property 2: Betting icon matches frame 0 from config

*For any* horse ID in [1,8] and *for any* valid `HorseSpriteConfig`, the sprite displayed as the icon in that horse's betting row SHALL equal `GetSprites(horseId)[0]`.

**Validates: Requirements 3.1, 3.5**

### Property 3: Frame index calculation is correct

*For any* horse ID in [1,8], *for any* frame rate in [1,30], *for any* sprite array of length ≥ 2, and *for any* non-negative elapsed time, the displayed frame index SHALL equal `floor(elapsed × frameRate) % arrayLength`, and the displayed sprite SHALL equal `GetSprites(horseId)[frameIndex]`.

**Validates: Requirements 4.1, 4.4**

### Property 4: Frame rate is clamped to valid range

*For any* integer value assigned to `spriteFrameRate`, the effective frame rate used in animation SHALL equal `Clamp(value, 1, 30)`.

**Validates: Requirements 4.2**

### Property 5: Null sprite entries never cause exceptions

*For any* sprite array containing null entries at arbitrary positions, the frame update logic SHALL never throw an exception, and SHALL retain the last valid sprite that was displayed.

**Validates: Requirements 5.5**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| `HorseSpriteConfig` not assigned on GameUI | Log error at `Start()`, fall back to existing `horseSprite` for all horses |
| Sprite array entry is null or empty | `GetSprites()` returns `defaultFrames`; UI displays fallback |
| Horse ID out of range [1,8] | `GetSprites()` returns `defaultFrames` |
| Individual sprite within array is null | Skip `Image.sprite` assignment for that frame; retain previous sprite |
| `spriteFrameRate` set to 0 or negative in Inspector | `[Range(1,30)]` attribute prevents invalid values in Inspector; code clamps at runtime |
| `defaultFrames` is null or empty | Log warning at `OnValidate()`; behave as if no config exists (use legacy `horseSprite`) |
| Sprite sheet has < 2 frames after slicing | Editor validation logs a warning; race animation falls back to static display |

## Testing Strategy

### Unit Tests (Example-Based)

- Verify `HorseSpriteConfig` asset has exactly 8 entries
- Verify each entry has ≥ 2 frames for animation
- Verify betting row icon Image has `preserveAspect = true` and correct size constraints
- Verify finished horse shows frame 0
- Verify bounce/bob motion is still applied during animation
- Verify no `Instantiate`/`Destroy` calls during the animation loop

### Property-Based Tests

Property-based testing is appropriate for this feature because the core logic involves pure functions (config lookup, frame index calculation, clamping) with clear input/output behavior and meaningful input variation.

**Library:** [FsCheck](https://github.com/fscheck/FsCheck) for C# / NUnit integration (or Unity Test Framework with custom generators).

**Configuration:**
- Minimum 100 iterations per property test
- Each test tagged with: **Feature: horse-sprite-animation, Property {number}: {property_text}**

| Property | Test Description | Generators |
|----------|-----------------|------------|
| 1 | GetSprites returns valid array for any ID and config state | Random int for horseId; random config with some null/empty entries |
| 2 | Betting icon equals GetSprites(id)[0] | Random valid configs with distinct frame arrays |
| 3 | Frame index = floor(elapsed * fps) % length | Random fps ∈ [1,30], random array length ∈ [2,20], random elapsed ∈ [0,60] |
| 4 | Effective fps = Clamp(input, 1, 30) | Random int values including negatives, 0, large values |
| 5 | Null entries in sprite array don't throw | Random arrays with null entries at random positions |

### Integration Tests

- Load the actual `HorseSpriteConfig.asset` and verify all 8 sprite sheets are correctly assigned
- Run the race animation for a few seconds in Play Mode and verify no GC allocations in the frame update path (Unity Profiler API)
- Verify 30 fps is maintained with 8 simultaneous animations on target hardware

### Manual Testing

- Visual inspection that each horse displays its unique character in both Betting Panel and Race View
- Verify animation looks smooth at 8 fps default
- Verify finish behavior (horse stops animating, shows resting pose)
