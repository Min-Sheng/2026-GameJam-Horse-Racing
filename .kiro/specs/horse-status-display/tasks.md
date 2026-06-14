# Implementation Plan: Horse Status Display

## Overview

在下注面板中為每匹馬顯示狀態圖片。實作依循專案既有架構：Config ScriptableObject 持有資料、Systems 靜態類別處理純邏輯、UI 層以 UIFactory 程式化建構元件。任務按增量進展排列，每步皆建立在前一步之上，最終整合至 GameUI。

## Tasks

- [x] 1. 建立 HorseStatusConfig ScriptableObject 與 StatusImageSystem 靜態類別
  - [x] 1.1 建立 HorseStatusConfig.cs
    - 在 `Scripts/Config/` 建立 `HorseStatusConfig.cs`
    - 繼承 ScriptableObject，加上 `[CreateAssetMenu]` 屬性
    - 定義欄位：`string[] horseNames`（長度 8）、`string[] statusNames`（長度 8）、`int[] bonusToStatusMap`（長度 8）、`Sprite[] sprites`（長度 64）
    - 實作 `GetSprite(int horseIndex, int statusIndex)` 方法：索引超出 0..7 回傳 null，否則回傳 `sprites[horseIndex * 8 + statusIndex]`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 1.2 建立 StatusImageSystem.cs
    - 在 `Scripts/Systems/` 建立 `StatusImageSystem.cs`
    - 實作靜態方法 `GetStatusSprite(HorseStatusConfig config, int horseIndex, int hiddenBonus)`
    - config 為 null 時回傳 null；hiddenBonus 超出 0..7 時回傳 null
    - 正常路徑：從 `config.bonusToStatusMap[hiddenBonus]` 取得 statusIndex，再呼叫 `config.GetSprite(horseIndex, statusIndex)`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 4.2, 4.3, 4.4_

  - [x] 1.3 將 HorseStatusConfig 欄位加入 GameConfigDatabase
    - 在 `Scripts/Config/GameConfigDatabase.cs` 新增 `public HorseStatusConfig horseStatus;` 欄位
    - _Requirements: 5.3_

- [ ] 2. 屬性測試與單元測試
  - [ ]* 2.1 Property 1：Config sprite lookup 正確性
    - **Property 1: Config sprite lookup correctness**
    - **Validates: Requirements 1.4**
    - 在 `Tests/` 建立 `HorseStatusTests.cs`
    - 使用迴圈 100 次迭代，隨機生成 horseIndex∈[0,7] 和 statusIndex∈[0,7]
    - 以 `ScriptableObject.CreateInstance<HorseStatusConfig>()` 建立測試用 config，隨機填充 sprites 陣列（使用 `Sprite.Create` 或 null 標記）
    - 斷言 `config.GetSprite(horseIndex, statusIndex) == config.sprites[horseIndex * 8 + statusIndex]`

  - [ ]* 2.2 Property 2：完整資料流 pipeline
    - **Property 2: Full data flow pipeline**
    - **Validates: Requirements 2.2**
    - 100 次迭代，隨機 hiddenBonus∈[0,7]、horseIndex∈[0,7]、隨機 bonusToStatusMap（值域 [0,7]）
    - 斷言 `StatusImageSystem.GetStatusSprite(config, horseIndex, hiddenBonus) == config.sprites[horseIndex * 8 + config.bonusToStatusMap[hiddenBonus]]`

  - [ ]* 2.3 Property 3：超出範圍安全性
    - **Property 3: Out-of-range safety**
    - **Validates: Requirements 1.6, 2.4, 4.3**
    - 100 次迭代，隨機生成 horseIndex 和 hiddenBonus（包含負數和大於 7 的值）
    - 斷言 `GetSprite` 和 `GetStatusSprite` 對超出範圍的輸入回傳 null 且不拋出例外

  - [ ]* 2.4 Property 4：Null config 安全性
    - **Property 4: Null config safety**
    - **Validates: Requirements 4.4**
    - 100 次迭代，隨機 horseIndex 和 hiddenBonus
    - 斷言 `StatusImageSystem.GetStatusSprite(null, horseIndex, hiddenBonus)` 回傳 null 且不拋出例外

  - [ ]* 2.5 Property 5：冪等性（Determinism）
    - **Property 5: Determinism**
    - **Validates: Requirements 2.3**
    - 100 次迭代，隨機合法輸入
    - 呼叫 `GetStatusSprite` 兩次，斷言結果相同（參照相等）

- [x] 3. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. 整合至 GameUI 下注面板
  - [x] 4.1 在 GameUI.BuildBettingPanel 中建立狀態圖片 Image 元件
    - 在馬匹列的 Chip 之後、文字之前，使用 `UIFactory.NewUIObject("StatusImg", row.transform)` 建立 GameObject
    - 加入 `Image` 元件，設定 `preserveAspect = true`、`raycastTarget = false`
    - 使用 `UIFactory.LE(statusGo, prefH: 48, prefW: 48)` 設定 LayoutElement
    - 預設 `SetActive(false)`
    - 將 Image 參照存入 `_horseStatusImages` 清單（`List<Image>`）
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 5.1, 5.5_

  - [x] 4.2 在 GameUI.RefreshBetting 中更新狀態圖片
    - 迭代 8 匹馬，從 `_gm.Round.Horses[i].HiddenBonus` 取得 hiddenBonus
    - 呼叫 `StatusImageSystem.GetStatusSprite(Cfg.horseStatus, i, hiddenBonus)` 取得 Sprite
    - Sprite 非 null → 設定 `_horseStatusImages[i].sprite = sprite` 並 `SetActive(true)`
    - Sprite 為 null → `SetActive(false)`
    - Round 尚未初始化時所有狀態圖片保持隱藏
    - _Requirements: 3.1, 3.5, 3.6, 5.2, 5.4_

- [x] 5. 建立 HorseStatusConfig.asset 並指派圖片資源
  - [x] 5.1 建立 HorseStatusConfig.asset 資料檔
    - 在 `Assets/HorseRacing/Data/` 建立 `HorseStatusConfig.asset`
    - 設定 horseNames：囚犯、墓碑、石頭、貓利、輪椅、金魚、馬、Tardis
    - 設定 statusNames：上場比賽剛結束、剛睡飽、嗨的飛起、心情很好、是上次冠軍、狀態很差、胃口不好、看起來很Chill
    - 設定 bonusToStatusMap 預設值 `{0, 1, 2, 3, 4, 5, 6, 7}`
    - 從 `Assets/HorseRacing/Art/Horses/Status/` 指派 64 張 Sprite
    - _Requirements: 1.1, 1.2, 1.3, 4.1_

  - [x] 5.2 將 HorseStatusConfig.asset 連結至 GameConfigDatabase.asset
    - 在 `GameConfigDatabase.asset` 的 Inspector 中將 `horseStatus` 欄位指向新建立的 `HorseStatusConfig.asset`
    - _Requirements: 5.3_

- [x] 6. 整合驗證
  - [ ]* 6.1 撰寫整合測試驗證完整 pipeline
    - 在 `HorseStatusTests.cs` 中新增整合測試
    - 建立完整 config（含已知 bonusToStatusMap 和 sprites），模擬 RefreshBetting 邏輯
    - 驗證給定已知 HiddenBonus 分配時，每匹馬取得正確的 Sprite
    - 驗證 GameConfigDatabase 的 horseStatus 欄位可正常存取
    - _Requirements: 2.2, 5.3_

  - [x] 6.2 編譯與全測試驗證
    - 使用 Unity MCP `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)` 確認專案編譯成功
    - 使用 `run_tests(mode="EditMode", assembly_names=["HorseRacing.Tests"])` 執行全部 EditMode 測試
    - 確認既有測試未受影響，新增測試全部通過
    - _Requirements: 5.4_

- [x] 7. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- 圖片資源已存在於 `Assets/HorseRacing/Art/Horses/Status/`（64 張 PNG），僅需在 .asset 中指派參照
- 使用 Unity MCP 工具進行編譯驗證與測試執行
- ConfigFactory 中可新增 `HorseStatus()` 輔助方法以簡化測試設定

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "5.1"] },
    { "id": 5, "tasks": ["5.2", "6.1"] },
    { "id": 6, "tasks": ["6.2"] }
  ]
}
```
