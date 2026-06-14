# Design Document: Race Animation Enhancements

## Overview

本設計為賽馬動畫頁面（`RaceView`）新增兩個 UI 子系統：

1. **MinimapController** — 俯視小地圖，以鳥瞰矩形區域在畫面右上角即時顯示 8 匹馬的相對位置。
2. **EventCardController** — 事件卡片系統，以動畫卡片取代現行底部文字事件顯示，包含彈出、停留、歸檔流程以及左上角歷史卡片堆疊。

兩個子系統與現有 `RaceView` 解耦為獨立 MonoBehaviour，由 `RaceView` 在賽事循環中驅動更新，不阻塞主動畫 coroutine。

## Architecture

```mermaid
graph TD
    subgraph RaceView
        RunCoroutine[Run Coroutine<br/>主動畫迴圈]
    end

    subgraph MinimapController
        MinimapPanel[Minimap Panel<br/>半透明背景]
        StartLine[Start Line]
        FinishLine[Finish Line]
        Dots[MinimapDot x8]
    end

    subgraph EventCardController
        CardQueue[Event Queue<br/>FIFO]
        ActiveCard[Active EventCard<br/>彈出/停留/歸檔]
        CardStack[EventCard_Stack<br/>歷史卡片]
    end

    RunCoroutine -->|UpdateProgress(float[])| MinimapController
    RunCoroutine -->|EnqueueEvents(StageEventLog[])| EventCardController
    EventCardController -->|Non-blocking coroutine| ActiveCard
    ActiveCard -->|Archive| CardStack
```

### 設計決策

| 決策 | 選項 | 結果 | 理由 |
|------|------|------|------|
| 子系統耦合方式 | A) 直接整合進 RaceView / B) 獨立 MonoBehaviour | B | 減少 RaceView 膨脹、方便獨立測試與未來擴充 |
| 動畫實作 | A) DOTween / B) Unity Coroutine + Lerp | B | 專案無 DOTween 依賴，保持一致性 |
| 卡片歸檔動畫 | A) 阻塞式等待 / B) Fire-and-forget coroutine | B | 不阻塞比賽迴圈，符合需求 5.3 |
| Minimap dot 位置計算 | A) 每幀由 RaceView 傳入 progress[] / B) dot 自行查詢 | A | 明確的數據流方向，易於測試 |

## Components and Interfaces

### MinimapController

```csharp
namespace HorseRacing.UI
{
    /// <summary>俯視小地圖控制器，管理 minimap 面板與 8 個 dot。</summary>
    public class MinimapController : MonoBehaviour
    {
        // --- 設定（由 RaceView 初始化） ---
        public void Init(RectTransform parentRT);
        public void Show();
        public void Hide();

        /// <summary>
        /// 更新所有馬匹在小地圖上的位置。
        /// progress[i] 為 horse (i+1) 的進度 0.0~1.0+。
        /// </summary>
        public void UpdatePositions(float[] progress);
    }
}
```

**內部結構：**
- `_panel` (RectTransform) — 半透明背景矩形，anchored 右上角
- `_dots[8]` (Image) — 各馬匹圓點，顏色取自 `UIFactory.HorseColors`
- `_startLine`, `_finishLine` (Image) — 起/終點標示線

**位置映射公式：**
```
dotX = panelLeft + Clamp01(progress) * panelWidth
dotY = laneTop + (horseIndex / 7.0) * (laneBottom - laneTop)
```

### EventCardController

```csharp
namespace HorseRacing.UI
{
    /// <summary>事件卡片控制器，管理卡片彈出、歸檔、堆疊。</summary>
    public class EventCardController : MonoBehaviour
    {
        public void Init(RectTransform parentRT);
        public void Show();
        public void Hide();

        /// <summary>
        /// 將事件加入顯示佇列。依序處理，前一張完成/中斷後才顯示下一張。
        /// </summary>
        public void EnqueueEvent(StageEventLog eventLog);

        /// <summary>強制歸檔當前卡片（用於新事件中斷）。</summary>
        public void ForceArchiveCurrent();

        /// <summary>清除所有卡片與佇列（賽事結束時呼叫）。</summary>
        public void Clear();
    }
}
```

**內部結構：**
- `_eventQueue` (Queue&lt;StageEventLog&gt;) — 待顯示事件 FIFO
- `_activeCard` (GameObject) — 當前顯示的卡片實例
- `_stackPanel` (RectTransform) — 左上角歸檔堆疊容器
- `_archivedCards` (List&lt;GameObject&gt;) — 已歸檔的卡片列表
- `_isAnimating` (bool) — 是否有卡片正在動畫中

### EventCardView (Prefab-like structure)

```csharp
namespace HorseRacing.UI
{
    /// <summary>單張事件卡片的 UI 結構與資料綁定。</summary>
    public class EventCardView
    {
        public RectTransform Root;
        public TextMeshProUGUI EventNameText;
        public TextMeshProUGUI HorseIdText;
        public Image HorseColorIndicator;
        public TextMeshProUGUI SpeedModifierText;
        public TextMeshProUGUI DefendedLabel;

        public void Bind(StageEventLog log);
        public void SetScale(float scale);
    }
}
```

### RaceView 整合修改

```csharp
// 新增欄位
private MinimapController _minimap;
private EventCardController _eventCards;

// 在 Build() 中初始化子系統
_minimap = gameObject.AddComponent<MinimapController>();
_minimap.Init(_self);

_eventCards = gameObject.AddComponent<EventCardController>();
_eventCards.Init(_self);

// 在 Run() 主迴圈中每幀呼叫：
_minimap.UpdatePositions(currentProgress);

// 事件觸發時（替換原本的 _eventText 寫法）：
foreach (var e in stageEvents)
    _eventCards.EnqueueEvent(e);
```

## Data Models

### 現有模型（不修改）

| 類型 | 用途 |
|------|------|
| `StageEventLog` | 事件紀錄，含 Stage, HorseId, EventName, SpeedModifier, Defended |
| `RaceResult` | 賽事結果，含 Events 列表與 RankToHorseId |

### 新增配置模型

```csharp
/// <summary>小地圖與事件卡片的動畫/佈局配置。</summary>
[CreateAssetMenu(fileName = "RaceAnimConfig", menuName = "HorseRacing/RaceAnimConfig")]
public class RaceAnimConfig : ScriptableObject
{
    [Header("Minimap")]
    public float minimapWidthPercent = 0.20f;   // 佔畫面寬度比例 (0.15~0.25)
    public float minimapHeightPercent = 0.15f;  // 佔畫面高度比例 (0.10~0.20)
    public float minimapMarginPx = 8f;          // 與畫面邊緣間距
    public float minimapBgAlpha = 0.6f;         // 背景不透明度 (0.5~0.7)

    [Header("EventCard - Timing")]
    public float cardPopupDuration = 0.25f;     // 彈出動畫時長（秒）
    public float cardHoldDuration = 0.5f;       // 中央停留時長（秒）
    public float cardArchiveDuration = 0.4f;    // 歸檔動畫時長（秒）

    [Header("EventCard - Layout")]
    public float cardWidth = 320f;              // 卡片寬度（px）
    public float cardHeight = 160f;             // 卡片高度（px）
    public float archivedScale = 0.35f;         // 歸檔後縮放比例 (>=0.3)
    public int maxVisibleArchived = 8;          // 堆疊最多可見數量
    public float stackMarginPx = 12f;           // 堆疊與畫面邊緣間距
    public float stackSpacing = 4f;             // 堆疊卡片間距
}
```

### 執行期狀態

```csharp
/// <summary>事件卡片動畫狀態機。</summary>
public enum CardAnimState
{
    Idle,       // 無卡片顯示
    PopupIn,    // 正在彈出
    Holding,    // 中央停留
    Archiving   // 正在縮小歸檔
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Dot position is a linear mapping of clamped progress

*For any* progress value and any minimap width, the dot's X position SHALL equal `panelLeft + Clamp01(progress) * panelWidth`. When progress >= 1.0, the dot remains at the right boundary (finish line).

**Validates: Requirements 1.4, 1.7**

### Property 2: Dot color matches horse color

*For any* horse index i (0–7), the minimap dot for horse (i+1) SHALL have its color equal to `UIFactory.HorseColors[i]`.

**Validates: Requirements 1.5**

### Property 3: Dots are vertically ordered by horse ID

*For any* two horse indices i < j, the dot for horse (i+1) SHALL have a Y position above (or equal to) the dot for horse (j+1) within the minimap panel.

**Validates: Requirements 2.4**

### Property 4: Event card displays all required fields

*For any* `StageEventLog` instance, the rendered `EventCardView` SHALL contain the event name text, the horse ID with color matching `UIFactory.HorseColors[horseId-1]`, and the speed modifier value as text.

**Validates: Requirements 3.2**

### Property 5: Defended events display defense indicator and zero modifier

*For any* `StageEventLog` where `Defended == true`, the `EventCardView` SHALL display a defense success label and show speed modifier as "0".

**Validates: Requirements 3.4**

### Property 6: Event cards are displayed in StageEventLog list order

*For any* list of `StageEventLog` entries enqueued in order, the `EventCardController` SHALL display them in the same sequence (FIFO).

**Validates: Requirements 3.5**

### Property 7: Stack overflow hides oldest cards

*For any* number of archived cards n > `maxVisibleArchived`, only the most recent `maxVisibleArchived` cards SHALL be visible, and all older cards SHALL be hidden.

**Validates: Requirements 4.6**

### Property 8: New event interrupts current card animation

*For any* card currently in animation state (PopupIn, Holding, or Archiving), when a new event is enqueued, the current card SHALL be immediately archived to the stack before the new card begins its popup animation.

**Validates: Requirements 5.5**

## Error Handling

| 情境 | 處理方式 |
|------|----------|
| `progress[]` 為 null 或長度不足 8 | `MinimapController.UpdatePositions` 忽略該幀更新，輸出 warning log |
| `StageEventLog` 中 HorseId 超出 1–8 範圍 | `EventCardView.Bind` 使用預設白色，輸出 warning log |
| 歸檔動畫中 parentRT 尺寸為 0（未佈局） | 跳過動畫，直接設定最終位置/縮放 |
| `RaceAnimConfig` 為 null（未設定） | 使用程式內硬編碼的預設值（與 ScriptableObject default 一致） |
| EventCard 佇列於比賽結束時仍有殘餘 | `Clear()` 立即銷毀所有卡片、清空佇列 |
| 多個事件同時入隊但前一張動畫已完成 | 正常 FIFO 處理，無需中斷 |

## Testing Strategy

### Property-Based Testing

本功能的核心邏輯（座標映射、顏色對應、佇列排序、堆疊溢出管理）適合 property-based testing，因為：
- 輸入空間大（progress 為連續浮點數、事件組合多樣）
- 存在明確的 universal invariants
- 可在不依賴 Unity runtime 的情況下測試純邏輯

**PBT 框架**: [FsCheck](https://github.com/fscheck/FsCheck) for C# (NUnit integration)  
**最低迭代次數**: 每個 property test 100 次  
**標記格式**: `// Feature: race-animation-enhancements, Property {N}: {description}`

### Unit Tests (Example-Based)

| 測試目標 | 覆蓋需求 |
|----------|----------|
| Minimap 面板尺寸在 15%–25% 寬、10%–20% 高 | 2.1 |
| Minimap 定位右上角，margin 8px | 2.2 |
| Minimap 背景 alpha 在 0.5–0.7 | 2.3 |
| Minimap 有起點/終點標示線 | 1.6 |
| Minimap 產生 8 個 dot | 1.3 |
| EventCard 彈出位於畫面中央 | 3.1 |
| EventCard 停留 0.5 秒 (±0.1s) | 3.3 |
| EventCard 歸檔後 scale >= 0.3 | 4.4 |
| EventCard_Stack 位於左上角 | 4.2 |
| 彈出+歸檔總時長 <= 1.0 秒 | 5.4 |
| 卡片動畫不阻塞 RaceView 主迴圈 | 5.3 |

### Integration Tests

| 測試目標 | 覆蓋需求 |
|----------|----------|
| Minimap 於 Racing phase 顯示、結束後隱藏 | 1.1 |
| Minimap 不被 EventCard 遮蔽 (z-order) | 2.5 |
| EventCard_Stack 於比賽期間持續顯示 | 4.5 |
| 多事件同階段觸發時依序顯示完整流程 | 3.5, 5.5 |
