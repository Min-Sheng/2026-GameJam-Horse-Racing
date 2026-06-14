# Requirements Document

## Introduction

在賽馬遊戲的下注階段，馬匹列表中為每匹馬顯示對應的狀態圖片。遊戲共有 8 匹馬，每匹馬有 8 種狀態，對應的 PNG 圖片已放置於 `Assets/HorseRacing/Art/Horses/Status/`。本功能將馬匹的隱藏加成值對應到狀態圖片，並在 UI 中以視覺方式呈現，讓玩家透過圖片獲得馬匹狀態的暗示。

## Glossary

- **Status_Image_System**：負責將馬匹狀態對應到正確圖片並提供給 UI 顯示的子系統
- **Horse_List_UI**：下注面板中顯示所有馬匹資訊的列表區域（目前由 GameUI.BuildBettingPanel 建立）
- **Horse_Status**：每匹馬在每場比賽中的狀態描述，由隱藏加成值（HiddenBonus 0..7）決定
- **Status_Config**：ScriptableObject 設定檔，儲存馬匹名稱與狀態名稱之間的對應關係及圖片資源參照
- **UIFactory**：程式化建立 uGUI/TMP 元件的靜態工具類別

## Requirements

### 需求 1：狀態圖片資源對應設定

**使用者故事：** 身為遊戲設計師，我希望透過 ScriptableObject 設定檔管理馬匹狀態圖片的對應關係，以便在不修改程式碼的情況下調整設定。

#### 驗收條件

1. THE Status_Config SHALL 定義 8 匹馬的名稱清單（囚犯、墓碑、石頭、貓利、輪椅、金魚、馬、Tardis），清單順序對應馬匹索引 0 至 7
2. THE Status_Config SHALL 定義 8 種狀態名稱清單（上場比賽剛結束、剛睡飽、嗨的飛起、心情很好、是上次冠軍、狀態很差、胃口不好、看起來很Chill），清單順序對應狀態索引 0 至 7
3. THE Status_Config SHALL 為每一組馬匹索引與狀態索引的組合（共 64 組）各提供一個可於 Inspector 編輯的 Sprite 欄位
4. THE Status_Config SHALL 提供以馬匹索引（0..7）和狀態索引（0..7）查詢對應 Sprite 的方法，回傳該組合所設定的 Sprite 參照
5. IF Status_Config 中某組合的 Sprite 欄位未指派資源，THEN THE Status_Config SHALL 回傳 null 而非拋出例外
6. IF 傳入的馬匹索引或狀態索引超出有效範圍（小於 0 或大於 7），THEN THE Status_Config SHALL 回傳 null 而非拋出例外

### 需求 2：隱藏加成值與狀態的對應邏輯

**使用者故事：** 身為系統開發者，我希望有明確的對應規則將每匹馬的隱藏加成值轉換為狀態索引，以便在下注面板中顯示正確的狀態圖片。

#### 驗收條件

1. THE Status_Config SHALL 在 Inspector 中以長度為 8 的有序陣列定義 HiddenBonus 值（0..7）到狀態索引（0..7）的對應關係，陣列的第 N 個元素代表 HiddenBonus=N 所對應的狀態索引
2. WHEN 一場新比賽開始時，THE Status_Image_System SHALL 讀取每匹馬被分配的 HiddenBonus 值，透過 Status_Config 的對應陣列取得狀態索引，並以該狀態索引查詢對應的 Sprite 作為該馬的狀態圖片
3. THE Status_Image_System SHALL 在整場比賽期間對同一匹馬回傳相同的狀態圖片，直到下一場比賽重新分配 HiddenBonus 為止
4. IF Horse 的 HiddenBonus 值超出 0..7 範圍，THEN THE Status_Image_System SHALL 回傳 null 而非拋出例外

### 需求 3：馬匹列表 UI 顯示狀態圖片

**使用者故事：** 身為玩家，我希望在下注面板的馬匹列表中看到每匹馬的狀態圖片，以便獲得關於馬匹狀態的視覺提示。

#### 驗收條件

1. WHEN 下注面板顯示時，THE Horse_List_UI SHALL 在每匹馬的列表行中顯示該馬對應的狀態圖片
2. THE Horse_List_UI SHALL 將狀態圖片放置於馬匹色塊（Chip）之後、文字資訊之前的位置
3. THE Horse_List_UI SHALL 使用 Unity Image 元件顯示狀態圖片，並將圖片類型設定為保持原始比例（preserveAspect = true）
4. THE Horse_List_UI SHALL 將狀態圖片的顯示尺寸限制為高度 48 像素，寬度依原圖比例自動計算
5. WHILE 當前 Round 尚未初始化（馬匹尚未被分配 HiddenBonus 值）時，THE Horse_List_UI SHALL 將狀態圖片元件設為隱藏（GameObject.SetActive(false)）
6. IF Status_Config 回傳的 Sprite 為 null，THEN THE Horse_List_UI SHALL 將該狀態圖片元件設為隱藏

### 需求 4：圖片載入機制

**使用者故事：** 身為系統開發者，我希望狀態圖片透過 ScriptableObject 的直接參照方式載入，以確保與專案現有架構一致。

#### 驗收條件

1. THE Status_Config SHALL 以序列化欄位方式直接持有全部 64 個（8 匹馬 × 8 種狀態）Sprite 資源參照，所有參照於編輯階段在 Inspector 中指定
2. WHEN Status_Image_System 收到查詢請求時，THE Status_Image_System SHALL 直接從 Status_Config 的序列化欄位取得 Sprite 參照，不得使用 Resources.Load、Addressables 或 AssetBundle 等執行期動態載入方式
3. IF 查詢的馬匹索引或狀態索引超出有效範圍（0..7），THEN THE Status_Image_System SHALL 回傳 null 且不拋出例外
4. IF Status_Image_System 被查詢時 Status_Config 參照尚未注入（為 null），THEN THE Status_Image_System SHALL 回傳 null 且不拋出例外

### 需求 5：與現有 UI 架構整合

**使用者故事：** 身為系統開發者，我希望狀態圖片功能遵循現有的程式化 UI 建構模式，以維持程式碼風格一致性。

#### 驗收條件

1. THE Horse_List_UI SHALL 使用 UIFactory.NewUIObject 建立狀態圖片的 GameObject，並使用 UIFactory.LE 設定其 LayoutElement 參數，不得使用 Inspector 手動建立或 Prefab 方式產生該元件
2. WHEN RefreshBetting 方法被呼叫時，THE Horse_List_UI SHALL 根據該馬匹當前的 HiddenBonus 值，從 Status_Config 取得對應的 Sprite 並指派給狀態圖片的 Image 元件；IF 取得的 Sprite 為 null，THEN THE Horse_List_UI SHALL 將該狀態圖片元件設為隱藏（GameObject.SetActive(false)）
3. THE Status_Config SHALL 作為 GameConfigDatabase 的序列化欄位提供給 UI 使用，與現有的 BettingConfig、TrackConfig 等 Config 以相同的 ScriptableObject 聚合方式注入
4. THE Horse_List_UI SHALL 保持現有馬匹列表行的 Button 元件點擊事件（ToggleHorse）與選中高亮（Image.color 切換為 AccentGreen）功能不受影響，加入狀態圖片後所有既有的使用者互動行為結果不變
5. THE Horse_List_UI SHALL 在建構馬匹列表行時，將狀態圖片元件建立於 HLayout 容器內既有的色塊（Chip）之後、文字元件之前的位置，使其遵循現有的 HorizontalLayoutGroup 排版邏輯
