# Requirements Document

## Introduction

本文件定義賽馬投注模擬遊戲與 PRD 完整對齊所需的全部需求。遊戲為 Unity 2D 單人策略型賽馬投注遊戲（Unity 6000.4.11f1, URP），以純 C# 系統搭配 ScriptableObject 設定驅動。文件涵蓋 PRD §2–§14 所有系統，確保實作完整性並標識尚需補齊的缺口。

## Glossary

- **GameManager**: 遊戲流程狀態機（MonoBehaviour FSM），串接所有純 C# 系統並維護玩家狀態
- **Horse_System**: 馬匹生成系統，負責產生 8 匹馬並分配唯一隱藏加成
- **Odds_System**: 賠率計算系統，依排名與下注輪次計算動態賠率
- **MessageCard_System**: 消息卡系統，將隱藏加成轉化為模糊文字情報
- **Track_System**: 賽道系統，管理三種賽道與 8×3 偏好修正表
- **Analyst_System**: 分析師系統，產生真假混合的情報陳述
- **Event_System**: 隨機事件系統，每階段獨立判定觸發並套用速度修正
- **Race_Simulation_System**: 賽事模擬系統，三階段模擬計算最終名次
- **Betting_System**: 投注系統，管理六種投注類型與派彩判定
- **Shop_System**: 商店系統，販售防禦卡與持有數量管理
- **Settlement_System**: 結算系統，比對投注結果並更新資金
- **GameUI**: 程式化建構的全部 UI 畫面控制器
- **RaceView**: 賽事動畫控制器，驅動 8 匹馬的賽道動畫
- **PlayerState**: 玩家狀態模型，包含資金與防禦卡持有清單
- **RoundContext**: 單一回合的所有當局資料容器
- **IRandom**: 隨機數介面，支援依賴注入實現確定性測試
- **ScriptableObject**: Unity 資料容器，承載所有可調參數
- **GameConfigDatabase**: 主設定資料庫，彙整所有子設定的單一入口

## Requirements

### Requirement 1: 核心遊戲循環

**User Story:** As a 玩家, I want 遊戲依照固定流程進行每個回合, so that 我可以在結構化的回合中進行策略決策

#### Acceptance Criteria

1. THE GameManager SHALL 依序執行以下階段：MainMenu → Betting（含多輪下注） → Racing → Settlement → Shop → 下一回合
2. WHEN 玩家在主選單點擊開始, THE GameManager SHALL 進入第一回合的 Betting 階段
3. WHILE 處於 Betting 階段, THE GameManager SHALL 支援由 BettingConfig.bettingRounds 定義的下注輪次數量（預設 3 輪）
4. WHEN 最後一輪下注完成並確認, THE GameManager SHALL 進入 Racing 階段
5. WHEN 賽事動畫播放完畢, THE GameManager SHALL 自動進入 Settlement 階段執行結算
6. WHEN 結算完成且玩家選擇進入商店, THE GameManager SHALL 進入 Shop 階段
7. WHEN 玩家在商店階段選擇開始下一回合, THE GameManager SHALL 開始新回合

### Requirement 2: 遊戲結束條件

**User Story:** As a 玩家, I want 遊戲在適當條件下結束, so that 我的遊戲有明確的成敗結局

#### Acceptance Criteria

1. WHEN 玩家資金降至 0 或以下, THE GameManager SHALL 觸發遊戲結束並顯示「資金耗盡」
2. WHEN 玩家資金低於 GameConfig.minBetAmount 但大於 0, THE GameManager SHALL 觸發遊戲結束並顯示「資金不足以下注」
3. WHEN 已完成 GameConfig.totalRounds 回合（且 totalRounds > 0）, THE GameManager SHALL 觸發遊戲結束並顯示已完成回合數
4. WHEN 遊戲結束時玩家最終資金大於或等於 GameConfig.startingMoney, THE GameManager SHALL 判定玩家獲勝
5. WHEN 遊戲結束時玩家最終資金小於 GameConfig.startingMoney, THE GameManager SHALL 判定玩家落敗
6. WHEN 遊戲結束, THE GameUI SHALL 顯示遊戲結果畫面，包含結束原因、最終資金與盈虧

### Requirement 3: 馬匹系統

**User Story:** As a 玩家, I want 每場比賽有 8 匹馬且各有不同隱藏實力, so that 我需要透過情報推測實力差異

#### Acceptance Criteria

1. THE Horse_System SHALL 每回合產生正好 GameConfig.horseCount 匹馬（預設 8），編號為 1 至 N
2. THE Horse_System SHALL 為所有馬匹設定相同基礎速度 GameConfig.baseSpeed（預設 30）
3. WHEN 新回合開始, THE Horse_System SHALL 從 GameConfig.hiddenBonusPool 隨機分配唯一加成值給每匹馬
4. THE Horse_System SHALL 確保 hiddenBonusPool 中每個值僅出現一次（唯一排列）
5. THE GameUI SHALL 不向玩家直接顯示任何馬匹的 HiddenBonus 數值

### Requirement 4: 消息卡系統

**User Story:** As a 玩家, I want 分輪收到馬匹的模糊情報卡, so that 我可以推測馬匹隱藏實力

#### Acceptance Criteria

1. WHEN 新回合開始, THE MessageCard_System SHALL 從 8 匹馬中隨機抽取 3 匹不重複的馬產生消息卡
2. THE MessageCard_System SHALL 依據 MessageCardConfig 中的 bonus-to-description 對應表將隱藏加成轉換為模糊文字描述
3. WHEN 第 N 輪下注開始（N=0,1,2）, THE GameManager SHALL 揭露對應輪次的消息卡給玩家
4. THE MessageCardConfig SHALL 提供完整的加成值到描述文字對應表，且支援管理者自由編輯內容

### Requirement 5: 賠率系統

**User Story:** As a 玩家, I want 賠率反映馬匹實力排名且逐輪變差, so that 我有動力在早期輪次下注

#### Acceptance Criteria

1. THE Odds_System SHALL 依據 InitialScore（BaseSpeed + HiddenBonus）由高到低排名所有馬匹
2. WHEN 兩匹馬 InitialScore 相同, THE Odds_System SHALL 以馬號較小者排名在前（tie-break）
3. THE Odds_System SHALL 根據排名位置從 OddsConfig.baseRankOdds 陣列取得基礎賠率
4. THE Odds_System SHALL 將基礎賠率乘以 OddsConfig.roundPayoutMultiplier[round] 作為該輪的實際賠率
5. THE Odds_System SHALL 確保計算結果不低於 OddsConfig.minOdds 下限值
6. WHEN 下注輪次遞進, THE Odds_System SHALL 產生較低的賠率（逐輪變差）

### Requirement 6: 賽道系統

**User Story:** As a 玩家, I want 每場有隨機賽道影響結果, so that 賽道因素增加策略深度

#### Acceptance Criteria

1. THE Track_System SHALL 支援 TrackConfig.tracks 中定義的所有賽道類型（預設 Grass/Mud/Snow）
2. WHEN 新回合開始, THE GameManager SHALL 從可用賽道中隨機抽選一種，但在開賽前不向玩家揭露
3. WHEN 比賽開始, THE GameManager SHALL 公布本場賽道類型
4. THE Track_System SHALL 根據 TrackConfig.preferences 中的 8×3 偏好表為每匹馬套用對應的 TrackModifier
5. THE TrackConfig SHALL 由 ScriptableObject 管理所有賽道修正值，不得硬編碼

### Requirement 7: 分析師系統

**User Story:** As a 玩家, I want 購買付費分析師情報, so that 我可以獲得額外（但不完全可信）的馬匹資訊

#### Acceptance Criteria

1. THE Analyst_System SHALL 提供 Junior 與 Senior 兩種等級的分析師
2. THE Analyst_System SHALL 根據 AnalystConfig 中設定的正確率決定每則陳述為真或誤導
3. THE AnalystConfig SHALL 確保 seniorAccuracy > juniorAccuracy（N > M 規則）
4. WHEN 玩家購買分析師情報, THE Analyst_System SHALL 產生 AnalystConfig.statementsPerReport 則陳述
5. WHEN 玩家本回合已購買過情報, THE GameManager SHALL 拒絕重複購買並通知玩家
6. WHEN 玩家資金不足, THE GameManager SHALL 拒絕購買並通知「資金不足」
7. THE AnalystConfig SHALL 由 ScriptableObject 管理分析師價格與正確率

### Requirement 8: 隨機事件系統

**User Story:** As a 玩家, I want 比賽中有隨機事件影響結果, so that 比賽結果具有不確定性

#### Acceptance Criteria

1. THE Event_System SHALL 在比賽的每個階段（共 3 階段）獨立判定每個事件是否觸發
2. THE Event_System SHALL 根據 EventDefinition.triggerChance 機率判定觸發
3. WHEN 事件觸發, THE Event_System SHALL 依據 EventDefinition.target 選擇影響目標（單匹隨機或全部馬）
4. WHEN 事件命中目標馬, THE Event_System SHALL 套用 EventDefinition.speedModifier 至該馬的階段修正
5. THE EventDefinition SHALL 由 ScriptableObject 定義，支援管理者新增任意事件
6. THE EventDatabase SHALL 彙整所有可觸發事件，由 GameConfigDatabase 統一引用

### Requirement 9: 防禦卡系統

**User Story:** As a 玩家, I want 使用防禦卡抵擋負面事件, so that 我可以保護下注的馬匹

#### Acceptance Criteria

1. WHEN 負面事件命中目標馬且玩家持有對應防禦卡, THE Event_System SHALL 進行防禦判定
2. THE Event_System SHALL 根據 ProtectionCardDefinition.defendChance 機率決定防禦是否成功
3. WHEN 防禦判定觸發（無論成功或失敗）, THE Event_System SHALL 消耗該張防禦卡
4. WHEN 防禦成功, THE Event_System SHALL 完全抵銷該事件的速度修正
5. THE PlayerState SHALL 維護防禦卡持有清單，受 ShopConfig.maxHeldCards 上限約束（預設 3）

### Requirement 10: 賽事模擬系統

**User Story:** As a 玩家, I want 比賽依公式計算最終名次, so that 結果公平且可驗證

#### Acceptance Criteria

1. THE Race_Simulation_System SHALL 將比賽分為 3 個階段（Stage 1/2/3）依序模擬
2. THE Race_Simulation_System SHALL 以公式 FinalSpeed = BaseSpeed + HiddenBonus + TrackModifier + Σ(Stage1..3 EventModifiers) 計算每匹馬的最終速度
3. THE Race_Simulation_System SHALL 依 FinalSpeed 由高到低決定名次
4. WHEN 兩匹或多匹馬 FinalSpeed 相同, THE Race_Simulation_System SHALL 以馬號較小者勝出（tie-break）
5. THE Race_Simulation_System SHALL 產生包含所有馬匹名次、最終速度與事件紀錄的 RaceResult

### Requirement 11: 投注系統

**User Story:** As a 玩家, I want 支援六種不同投注類型, so that 我可以依策略選擇風險與報酬的組合

#### Acceptance Criteria

1. THE Betting_System SHALL 支援以下六種投注：Win（獨贏）、Place（位置）、Quinella（連贏）、Exacta（正連贏）、Trio（三重彩）、Trifecta（三連單）
2. WHEN 玩家下注 Win 且選定馬獲得第一名, THE Betting_System SHALL 判定中獎
3. WHEN 玩家下注 Place 且選定馬進入前三名, THE Betting_System SHALL 判定中獎
4. WHEN 玩家下注 Quinella 且選定的兩匹馬為前兩名（不分順序）, THE Betting_System SHALL 判定中獎
5. WHEN 玩家下注 Exacta 且選定的兩匹馬依正確順序為前兩名, THE Betting_System SHALL 判定中獎
6. WHEN 玩家下注 Trio 且選定的三匹馬為前三名（不分順序）, THE Betting_System SHALL 判定中獎
7. WHEN 玩家下注 Trifecta 且選定的三匹馬依正確順序為前三名, THE Betting_System SHALL 判定中獎
8. WHEN 玩家下注 Win, THE Betting_System SHALL 鎖定下注當下該馬的動態賠率作為派彩倍率
9. WHEN 玩家下注非 Win 類型, THE Betting_System SHALL 使用 BettingConfig 中該類型的固定 payoutMultiplier
10. THE BettingConfig SHALL 由 ScriptableObject 管理所有投注倍率，不得硬編碼

### Requirement 12: 下注規則

**User Story:** As a 玩家, I want 下注受資金與金額限制, so that 遊戲保持合理的資金管理

#### Acceptance Criteria

1. WHEN 玩家下注金額低於 GameConfig.minBetAmount, THE GameManager SHALL 拒絕下注並通知最低金額限制
2. WHEN 玩家下注金額超過當前持有資金, THE GameManager SHALL 拒絕下注並通知「資金不足」
3. WHEN 下注成功, THE GameManager SHALL 立即從玩家資金扣除下注金額
4. WHILE 處於 Betting 階段, THE GameManager SHALL 允許玩家在同一輪次內進行多筆下注
5. IF 玩家未選擇馬匹即嘗試下注, THEN THE GameManager SHALL 拒絕該筆下注

### Requirement 13: 商店系統

**User Story:** As a 玩家, I want 在賽後商店購買防禦卡, so that 我可以為下一場比賽準備防禦手段

#### Acceptance Criteria

1. WHILE 處於 Shop 階段, THE GameUI SHALL 顯示 ShopConfig.availableCards 中所有可購買的防禦卡
2. WHEN 玩家購買防禦卡, THE Shop_System SHALL 從玩家資金扣除 ProtectionCardDefinition.price
3. WHEN 玩家購買成功, THE Shop_System SHALL 將防禦卡加入 PlayerState.ProtectionCards
4. WHEN 玩家持有防禦卡數量已達 ShopConfig.maxHeldCards, THE Shop_System SHALL 拒絕購買
5. WHEN 玩家資金不足以支付防禦卡價格, THE Shop_System SHALL 拒絕購買

### Requirement 14: 結算系統

**User Story:** As a 玩家, I want 比賽結束後看到完整結算結果, so that 我了解投注成敗與資金變化

#### Acceptance Criteria

1. WHEN 比賽結束, THE Settlement_System SHALL 比對所有投注與 RaceResult 判定每筆中獎或落敗
2. WHEN 投注中獎, THE Settlement_System SHALL 以 Bet.Amount × Bet.PayoutMultiplier 計算派彩金額
3. THE Settlement_System SHALL 將所有中獎派彩加回玩家資金
4. THE Settlement_System SHALL 產生包含 TotalStaked、TotalPayout、Net 盈虧的 SettlementResult
5. THE GameUI SHALL 顯示完整名次、各馬最終速度、投注結果與資金變化

### Requirement 15: 主畫面 UI

**User Story:** As a 玩家, I want 在主畫面看到遊戲基本資訊, so that 我了解遊戲設定後可以開始

#### Acceptance Criteria

1. THE GameUI SHALL 在主畫面顯示遊戲標題與簡要說明
2. THE GameUI SHALL 在主畫面顯示起始資金與總回合數資訊
3. WHEN 玩家點擊開始遊戲按鈕, THE GameManager SHALL 進入第一回合

### Requirement 16: 下注畫面 UI

**User Story:** As a 玩家, I want 在下注畫面看到所有決策所需資訊, so that 我可以做出明智的投注決策

#### Acceptance Criteria

1. WHILE 處於 Betting 階段, THE GameUI SHALL 顯示所有 8 匹馬及其當前賠率
2. WHILE 處於 Betting 階段, THE GameUI SHALL 顯示已揭露的消息卡資訊於對應馬匹旁
3. THE GameUI SHALL 顯示當前下注輪次（第 N/3 輪）
4. THE GameUI SHALL 提供六種投注類型選擇介面
5. THE GameUI SHALL 提供下注金額設定介面
6. THE GameUI SHALL 顯示本回合已進行的所有下注摘要
7. WHILE 處於最後一輪下注, THE GameUI SHALL 顯示分析師情報購買選項
8. WHEN 玩家已購買分析師情報, THE GameUI SHALL 顯示分析師產生的陳述內容

### Requirement 17: 賽事畫面 UI

**User Story:** As a 玩家, I want 看到賽事動畫, so that 我可以視覺化觀看比賽過程

#### Acceptance Criteria

1. WHILE 處於 Racing 階段, THE RaceView SHALL 顯示 8 匹馬由左至右的賽道動畫
2. THE RaceView SHALL 確保馬匹到達終點的順序與 RaceResult.Standings 名次一致
3. THE RaceView SHALL 顯示當前賽道類型的視覺背景
4. WHEN 賽事動畫播放完畢, THE RaceView SHALL 通知 GameUI 進入結算流程

### Requirement 18: 結果與商店畫面 UI

**User Story:** As a 玩家, I want 清楚看到比賽結果與商店介面, so that 我了解盈虧並可購買道具

#### Acceptance Criteria

1. WHILE 處於 Settlement 階段, THE GameUI SHALL 顯示賽道名稱、完整名次與各馬最終速度
2. WHILE 處於 Settlement 階段, THE GameUI SHALL 顯示每筆投注結果（中獎/未中）與派彩金額
3. THE GameUI SHALL 顯示本回合總投注、總派彩與淨盈虧
4. THE GameUI SHALL 提供進入商店的按鈕
5. WHILE 處於 Shop 階段, THE GameUI SHALL 顯示每張防禦卡的名稱、價格、防禦對象與成功率
6. WHILE 處於 Shop 階段, THE GameUI SHALL 顯示玩家目前持有的防禦卡數量與明細
7. THE GameUI SHALL 提供開始下一回合的按鈕

### Requirement 19: 技術架構需求

**User Story:** As a 開發者, I want 系統遵循模組化與設定驅動原則, so that 遊戲數值可以零程式碼調整

#### Acceptance Criteria

1. THE GameConfigDatabase SHALL 彙整所有子設定（game/messageCards/odds/track/events/analyst/betting/shop）為單一入口
2. THE GameManager SHALL 僅透過 GameConfigDatabase 存取所有遊戲數值，不得硬編碼任何遊戲參數
3. THE Systems SHALL 全部為純 C# 靜態類別，不依賴 MonoBehaviour，可在 EditMode 測試中獨立驗證
4. THE Systems SHALL 全部接受 IRandom 介面注入，確保確定性測試可行
5. THE GameManager SHALL 不引用任何 UI 型別，僅透過事件（OnStateChanged/OnNotice）通知 UI 層
6. THE 架構 SHALL 維持單向依賴：HorseRacing.Core ← HorseRacing.UI，UI 層僅讀取 Core 狀態

### Requirement 20: 多回合整合流程

**User Story:** As a 玩家, I want 遊戲在多回合間正確維護狀態, so that 資金與道具正確累積

#### Acceptance Criteria

1. WHEN 新回合開始, THE GameManager SHALL 保留玩家資金與防禦卡持有狀態（跨回合持續）
2. WHEN 新回合開始, THE GameManager SHALL 產生全新的 RoundContext（馬匹、賠率、消息卡、賽道）
3. THE GameManager SHALL 正確遞增回合計數器（RoundNumber）
4. WHEN 防禦卡在比賽中被消耗, THE PlayerState SHALL 在後續回合中反映該消耗結果
5. FOR ALL 有效的多回合遊戲序列, THE GameManager SHALL 維持資金公式一致性：最終資金 = 起始資金 - Σ(所有下注) + Σ(所有派彩) - Σ(所有購買)
