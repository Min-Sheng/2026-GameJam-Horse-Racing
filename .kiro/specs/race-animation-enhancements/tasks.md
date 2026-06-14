# Implementation Plan: Race Animation Enhancements

## Overview

本實作計畫將俯視小地圖（Minimap）與事件卡片系統（EventCard）以獨立 MonoBehaviour 加入現有 RaceView，分為配置模型建立、Minimap 實作、EventCard 實作、RaceView 整合四大階段。每個階段結束後有 checkpoint 確認品質。

## Tasks

- [x] 1. Create RaceAnimConfig ScriptableObject and data models
  - [x] 1.1 Create the RaceAnimConfig ScriptableObject class
    - Create file `Assets/HorseRacing/Scripts/Config/RaceAnimConfig.cs`
    - Define the ScriptableObject with `[CreateAssetMenu]` attribute
    - Include all minimap fields: `minimapWidthPercent`, `minimapHeightPercent`, `minimapMarginPx`, `minimapBgAlpha`
    - Include all EventCard timing fields: `cardPopupDuration`, `cardHoldDuration`, `cardArchiveDuration`
    - Include all EventCard layout fields: `cardWidth`, `cardHeight`, `archivedScale`, `maxVisibleArchived`, `stackMarginPx`, `stackSpacing`
    - Use default values as specified in the design document
    - _Requirements: 2.1, 2.2, 2.3, 3.3, 4.4, 4.6, 5.1, 5.2, 5.4_

  - [x] 1.2 Create the CardAnimState enum
    - Create file `Assets/HorseRacing/Scripts/UI/CardAnimState.cs`
    - Define enum with states: `Idle`, `PopupIn`, `Holding`, `Archiving`
    - _Requirements: 5.1, 5.2_

  - [x] 1.3 Create the RaceAnimConfig asset instance
    - Create `Assets/HorseRacing/Data/RaceAnimConfig.asset` ScriptableObject instance in the Data folder
    - Set default values matching design specifications
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Implement MinimapController
  - [x] 2.1 Create MinimapController MonoBehaviour with panel and layout setup
    - Create file `Assets/HorseRacing/Scripts/UI/MinimapController.cs`
    - Implement `Init(RectTransform parentRT)` that builds the minimap panel
    - Create semi-transparent background panel anchored to top-right corner with configurable margin (8px)
    - Set panel size to configurable percentage of parent (width 20%, height 15%)
    - Set background alpha to configurable value (0.6)
    - Create start line and finish line images at left and right boundaries
    - Implement `Show()` and `Hide()` methods
    - Use `UIFactory` patterns consistent with existing code
    - _Requirements: 1.1, 1.2, 1.6, 2.1, 2.2, 2.3_

  - [x] 2.2 Create 8 MinimapDot instances with correct colors and vertical layout
    - Create 8 dot Images within the minimap panel during `Init()`
    - Assign each dot the color from `UIFactory.HorseColors[i]` matching horse (i+1)
    - Position dots vertically by horse index: top-to-bottom ordered by ID
    - Use formula: `dotY = laneTop + (horseIndex / 7.0) * (laneBottom - laneTop)`
    - _Requirements: 1.3, 1.5, 2.4_

  - [x] 2.3 Implement UpdatePositions method for real-time dot movement
    - Implement `UpdatePositions(float[] progress)` method
    - Map each horse's progress (0.0–1.0) to horizontal position within panel
    - Use formula: `dotX = panelLeft + Clamp01(progress) * panelWidth`
    - When progress >= 1.0, clamp dot to finish line position (right boundary)
    - Add null/length guard: if `progress` is null or length < 8, skip update and log warning
    - _Requirements: 1.4, 1.7_

  - [x]* 2.4 Write property test for dot position mapping (Property 1)
    - **Property 1: Dot position is a linear mapping of clamped progress**
    - For any progress value and panel width, verify dot X = panelLeft + Clamp01(progress) * panelWidth
    - When progress >= 1.0, dot remains at right boundary
    - **Validates: Requirements 1.4, 1.7**

  - [x]* 2.5 Write property test for dot color assignment (Property 2)
    - **Property 2: Dot color matches horse color**
    - For any horse index i (0–7), verify dot color equals UIFactory.HorseColors[i]
    - **Validates: Requirements 1.5**

  - [x]* 2.6 Write property test for vertical dot ordering (Property 3)
    - **Property 3: Dots are vertically ordered by horse ID**
    - For any two horse indices i < j, verify dot for horse (i+1) has Y position above dot for horse (j+1)
    - **Validates: Requirements 2.4**

- [x] 3. Checkpoint - Minimap implementation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement EventCardView
  - [x] 4.1 Create EventCardView class with UI structure and data binding
    - Create file `Assets/HorseRacing/Scripts/UI/EventCardView.cs`
    - Define fields: `Root` (RectTransform), `EventNameText`, `HorseIdText`, `HorseColorIndicator`, `SpeedModifierText`, `DefendedLabel`
    - Implement `Bind(StageEventLog log)` method:
      - Set event name text from `log.EventName`
      - Set horse ID text with color from `UIFactory.HorseColors[log.HorseId - 1]` (default white if out of range)
      - Set speed modifier text (e.g., "-2" or "+1")
      - If `log.Defended == true`: show defense label, display speed modifier as "0"
    - Implement `SetScale(float scale)` to uniformly scale the card root
    - Build card UI using `UIFactory` patterns (background panel, text elements)
    - _Requirements: 3.2, 3.4, 4.4_

  - [x]* 4.2 Write property test for event card field display (Property 4)
    - **Property 4: Event card displays all required fields**
    - For any StageEventLog, verify EventCardView contains event name, horse ID with correct color, and speed modifier
    - **Validates: Requirements 3.2**

  - [x]* 4.3 Write property test for defended events (Property 5)
    - **Property 5: Defended events display defense indicator and zero modifier**
    - For any StageEventLog where Defended == true, verify defense label is shown and speed modifier is "0"
    - **Validates: Requirements 3.4**

- [x] 5. Implement EventCardController
  - [x] 5.1 Create EventCardController MonoBehaviour with queue and stack management
    - Create file `Assets/HorseRacing/Scripts/UI/EventCardController.cs`
    - Implement `Init(RectTransform parentRT)` that sets up:
      - The event queue (`Queue<StageEventLog>`)
      - The active card area (centered in parent)
      - The stack panel (anchored top-left with configurable margin)
      - The archived cards list
    - Implement `Show()`, `Hide()`, `Clear()` methods
    - Implement `EnqueueEvent(StageEventLog eventLog)` that adds to queue and starts processing if idle
    - Implement `ForceArchiveCurrent()` for immediate archival on interrupt
    - _Requirements: 3.1, 3.5, 4.2, 4.5, 5.5_

  - [x] 5.2 Implement card popup animation coroutine
    - Implement popup animation: scale from 0 to 1.0 over `cardPopupDuration` (0.25s)
    - Position card at horizontal and vertical center of parent
    - Use Coroutine + Lerp approach (no DOTween dependency)
    - Transition to Holding state after popup completes
    - _Requirements: 3.1, 5.1_

  - [x] 5.3 Implement card hold and archive animation coroutine
    - Hold card at center for `cardHoldDuration` (0.5s)
    - Implement archive animation: smoothly scale down and move to stack target position over `cardArchiveDuration` (0.4s)
    - Final scale matches `archivedScale` (0.35)
    - Total popup + hold + archive duration must be <= 1.0s
    - After archive, add card to stack and process next in queue
    - _Requirements: 3.3, 4.1, 5.2, 5.4_

  - [x] 5.4 Implement EventCard_Stack layout and overflow management
    - Position stack panel at top-left with `stackMarginPx` margin
    - Arrange archived cards vertically top-to-bottom, newest at bottom
    - Apply `stackSpacing` between cards
    - When archived count exceeds `maxVisibleArchived` (8), hide oldest cards and keep most recent visible
    - Cards in stack display at `archivedScale` with visible event name and horse ID
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 5.5 Implement event interruption logic
    - When a new event is enqueued while a card is animating (PopupIn, Holding, or Archiving):
      - Immediately complete current card animation
      - Archive current card to stack
      - Begin new card popup
    - Use `ForceArchiveCurrent()` to handle interruption
    - Ensure non-blocking: fire-and-forget coroutine does not pause race loop
    - _Requirements: 5.3, 5.5_

  - [x]* 5.6 Write property test for FIFO event order (Property 6)
    - **Property 6: Event cards are displayed in StageEventLog list order**
    - For any list of StageEventLog entries enqueued in order, verify they display in FIFO sequence
    - **Validates: Requirements 3.5**

  - [x]* 5.7 Write property test for stack overflow (Property 7)
    - **Property 7: Stack overflow hides oldest cards**
    - For n archived cards > maxVisibleArchived, verify only the most recent maxVisibleArchived are visible
    - **Validates: Requirements 4.6**

  - [x]* 5.8 Write property test for event interruption (Property 8)
    - **Property 8: New event interrupts current card animation**
    - For any card in animation state, when new event enqueued, verify current card is immediately archived before new card begins
    - **Validates: Requirements 5.5**

- [x] 6. Checkpoint - EventCard implementation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integrate subsystems into RaceView
  - [x] 7.1 Add MinimapController and EventCardController initialization to RaceView.Build()
    - Add `_minimap` and `_eventCards` private fields to `RaceView`
    - In `Build()`, add `MinimapController` component and call `Init(_self)`
    - In `Build()`, add `EventCardController` component and call `Init(_self)`
    - Add `RaceAnimConfig` reference field (serialized or loaded from Resources)
    - Pass config to both controllers during initialization
    - _Requirements: 1.1, 3.1_

  - [x] 7.2 Integrate MinimapController.UpdatePositions into Run() main loop
    - Calculate `currentProgress[i]` for each horse based on `elapsed * horseRate[i]`
    - Call `_minimap.UpdatePositions(currentProgress)` every frame in the Run() coroutine
    - Call `_minimap.Show()` at race start, `_minimap.Hide()` at race end
    - _Requirements: 1.1, 1.4_

  - [x] 7.3 Replace event text display with EventCardController.EnqueueEvent
    - Remove or disable the existing `_eventText` bottom text for stage events
    - At each stage event trigger point, iterate `stageEvents` and call `_eventCards.EnqueueEvent(e)` for each
    - Call `_eventCards.Show()` at race start, `_eventCards.Clear()` and `_eventCards.Hide()` at race end
    - Ensure event cards don't overlap or z-conflict with minimap (set sibling order)
    - _Requirements: 2.5, 3.1, 3.5, 5.3_

  - [x]* 7.4 Write unit tests for RaceView integration
    - Test that MinimapController receives correct progress values
    - Test that EventCardController receives events in correct order
    - Test that minimap shows/hides with race lifecycle
    - Test that event cards don't block race animation coroutine
    - _Requirements: 1.1, 2.5, 5.3_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The project uses Unity Coroutine + Lerp for animations (no DOTween dependency)
- All UI construction follows existing `UIFactory` patterns
- PBT framework: FsCheck for C# with NUnit integration

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "4.1"] },
    { "id": 2, "tasks": ["2.1", "4.2", "4.3"] },
    { "id": 3, "tasks": ["2.2", "5.1"] },
    { "id": 4, "tasks": ["2.3", "5.2"] },
    { "id": 5, "tasks": ["2.4", "2.5", "2.6", "5.3"] },
    { "id": 6, "tasks": ["5.4", "5.5"] },
    { "id": 7, "tasks": ["5.6", "5.7", "5.8"] },
    { "id": 8, "tasks": ["7.1"] },
    { "id": 9, "tasks": ["7.2", "7.3"] },
    { "id": 10, "tasks": ["7.4"] }
  ]
}
```
