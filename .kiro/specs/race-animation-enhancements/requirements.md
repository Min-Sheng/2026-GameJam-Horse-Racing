# Requirements Document

## Introduction

本功能為賽馬動畫頁面新增兩項視覺增強：俯視小地圖與事件卡片系統。俯視小地圖以鳥瞰視角顯示各匹馬的即時位置，協助玩家掌握全局；事件卡片系統以動畫卡片的方式呈現賽事事件，取代現行底部文字顯示，提升閱讀體驗與視覺衝擊感。

## Glossary

- **RaceView**：賽道動畫頁面，負責以 2D 捲軸側視視角呈現賽馬動畫的 UI 元件
- **Minimap**：俯視小地圖元件，以鳥瞰視角在畫面右上角顯示各匹馬的相對位置
- **Minimap_Dot**：小地圖中代表各匹馬的圓點標記
- **EventCard**：事件卡片元件，以卡片形式呈現單一賽事事件的文字描述
- **EventCard_Stack**：事件卡片堆疊區域，位於畫面左上角，存放縮小後的歷史事件卡片
- **StageEventLog**：賽事事件紀錄資料結構，包含階段、馬號、事件名稱、速度修正值與防禦狀態
- **RaceResult**：賽事結果資料結構，包含所有 StageEventLog 紀錄與排名資訊

## Requirements

### Requirement 1: 俯視小地圖顯示

**User Story:** 身為玩家，我希望在賽馬動畫頁面看到俯視小地圖，以便在賽事進行中即時掌握所有馬匹的相對位置與整體賽況。

#### Acceptance Criteria

1. WHILE 賽馬動畫播放中，THE Minimap SHALL 顯示於畫面右上角，並於動畫結束後隨賽道畫面一同隱藏
2. THE Minimap SHALL 以矩形區域呈現，代表賽道的俯視視角，左邊界對應起點、右邊界對應終點
3. THE Minimap SHALL 為每匹參賽馬匹（共 8 匹）顯示一個對應的 Minimap_Dot
4. WHILE 賽馬動畫播放中，THE Minimap_Dot SHALL 於每個渲染幀根據各匹馬的當前進度（0.0 至 1.0）更新水平位置，其中進度 0.0 對應 Minimap 左邊界、進度 1.0 對應 Minimap 右邊界
5. THE Minimap_Dot SHALL 使用與主畫面馬匹編號相同的顏色標示，以便玩家辨識
6. THE Minimap SHALL 顯示起點與終點的標示線，分別位於矩形區域的左邊界與右邊界
7. WHEN 某匹馬通過終點時，THE 對應的 Minimap_Dot SHALL 停留在終點標示線位置不再移動

### Requirement 2: 俯視小地圖佈局

**User Story:** 身為玩家，我希望小地圖不會遮擋重要的賽事資訊，以便同時觀看比賽動畫與小地圖。

#### Acceptance Criteria

1. THE Minimap SHALL 佔據畫面寬度的 15%–25% 且高度的 10%–20%
2. THE Minimap SHALL 定位於畫面右上角，與畫面上方及右方邊緣各保持 8 像素的固定間距
3. THE Minimap SHALL 以背景不透明度 50%–70% 的半透明方式呈現，使下方的賽道動畫仍可透視辨認
4. THE Minimap_Dot SHALL 沿垂直方向依馬匹編號由上而下排列於各自的車道位置
5. IF EventCard 彈出或動畫播放中，THEN THE Minimap SHALL 維持顯示且不被 EventCard 遮蔽

### Requirement 3: 事件卡片彈出顯示

**User Story:** 身為玩家，我希望賽事事件以醒目的卡片方式在畫面中央彈出，以便在賽事關鍵時刻立即注意到發生的事件。

#### Acceptance Criteria

1. WHEN 賽事事件觸發時，THE EventCard SHALL 在畫面視窗水平與垂直中央彈出顯示
2. THE EventCard SHALL 包含事件名稱、受影響馬匹編號（以馬匹對應顏色標示）、以及該事件造成的速度修正值（例如「-2」或「+1」）的文字描述
3. WHEN EventCard 彈出完成後，THE EventCard SHALL 在畫面中央持續顯示 0.5 秒（±0.1 秒），再進入歸檔流程
4. IF 事件已被防禦卡成功抵擋，THEN THE EventCard SHALL 顯示防禦成功的文字標示，且速度修正值顯示為 0
5. WHEN 多個事件於同一階段觸發時，THE RaceView SHALL 依據 StageEventLog 列表順序逐一顯示各 EventCard，前一張卡片完成歸檔動畫後才顯示下一張，每張卡片遵循相同的顯示流程

### Requirement 4: 事件卡片縮小歸檔

**User Story:** 身為玩家，我希望已呈現的事件卡片縮小後整齊排列在左上角，以便回顧本場比賽已發生的所有事件。

#### Acceptance Criteria

1. WHEN EventCard 在畫面中央顯示 0.5 秒後，THE EventCard SHALL 以動畫方式縮小並移動至 EventCard_Stack 區域
2. THE EventCard_Stack SHALL 位於畫面左上角，與畫面上方及左方邊緣保持固定間距
3. THE EventCard_Stack SHALL 將多張已歸檔的 EventCard 以垂直方向由上至下排列，最新事件置於最下方
4. THE EventCard SHALL 在縮小歸檔狀態下以不小於原始尺寸 30% 的比例顯示，且須完整顯示事件名稱文字與受影響的馬匹編號
5. WHILE 賽馬動畫播放中，THE EventCard_Stack SHALL 持續顯示所有已歸檔的事件卡片
6. IF 已歸檔的 EventCard 數量超過 EventCard_Stack 可見區域能容納的上限（最多顯示 8 張），THEN THE EventCard_Stack SHALL 隱藏最早的卡片並保留最近的卡片可見

### Requirement 5: 事件卡片動畫效果

**User Story:** 身為玩家，我希望事件卡片的出現與縮小過程有流暢的動畫效果，以提升觀賽體驗的視覺品質。

#### Acceptance Criteria

1. WHEN EventCard 彈出時，THE EventCard SHALL 以縮放動畫從初始縮放比例 0 放大至縮放比例 1.0 呈現於畫面中央，彈出動畫時長為 0.3 秒以內
2. WHEN EventCard 歸檔時，THE EventCard SHALL 以連續不跳幀的縮小與位移動畫過渡至 EventCard_Stack 的目標位置，歸檔動畫時長為 0.5 秒以內，結束時 EventCard 的縮放比例與 EventCard_Stack 中已歸檔卡片的顯示尺寸一致
3. WHILE EventCard 動畫播放中，THE RaceView SHALL 持續更新賽馬動畫，動畫播放不暫停比賽進行
4. THE EventCard 的單次彈出動畫加上歸檔動畫的總時長（含畫面中央停留時間）SHALL 控制在 1.0 秒以內
5. IF 新的賽事事件於前一張 EventCard 動畫尚未完成時觸發，THEN THE RaceView SHALL 將前一張 EventCard 立即完成動畫並歸檔至 EventCard_Stack，再開始顯示新的 EventCard
