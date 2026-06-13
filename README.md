# 🏇 賽馬投注模擬 (Horse Racing Betting Game)

2026 GameJam 賽馬投注策略遊戲 — Unity 2D 單人遊戲。玩家透過情報收集、賠率分析與策略下注，在多回合比賽中追求最大獲利。

## 遊戲概述

- **類型**：策略 / 模擬 / 賽馬投注
- **引擎**：Unity 6000.4.11f1 (URP)
- **平台**：PC (Windows/Mac)
- **語言**：繁體中文

### 核心玩法

每回合流程：**下注（3輪）→ 比賽 → 結算 → 商店 → 下一回合**

1. **情報收集**：每輪下注時收到一張消息卡，揭露一匹馬的模糊狀態
2. **賠率分析**：8 匹馬各有動態賠率，逐輪變差（早下注賠率較好）
3. **下注決策**：6 種投注玩法（獨贏、位置、連贏、正連贏、三重彩、三連單）
4. **賽事觀看**：2D 卷軸側視動畫，含隨機事件（打滑、絆倒、順風）
5. **防禦策略**：商店購買防禦卡，可抵消不利事件
6. **分析師**：最後一輪可付費購買初級/資深分析師的情報

### 勝負條件

- **獲勝**：完成指定回合數且資金 ≥ 起始資金
- **失敗**：資金歸零或不足以下注

---

## 環境需求

- **Unity**：6000.4.11f1（建議透過 Unity Hub 安裝）
- **Git LFS**：必須安裝（圖片資源使用 LFS 追蹤）

---

## 快速開始

### 1. Clone 專案

```bash
# 確保已安裝 Git LFS
git lfs install

# Clone（LFS 檔案會自動下載）
git clone https://github.com/Min-Sheng/2026-GameJam-Horse-Racing.git
```

### 2. 用 Unity Hub 開啟

1. 開啟 Unity Hub
2. 點選「Add」→ 選擇 clone 下來的資料夾
3. 確認 Unity 版本為 `6000.4.11f1`（如果沒有會提示安裝）
4. 開啟專案

### 3. 執行遊戲

1. Unity Editor 開啟後，預設場景為 `Assets/Scenes/SampleScene.unity`
2. 按下 ▶️ Play 即可遊玩

---

## 操作說明

| 畫面 | 操作 |
|------|------|
| 主選單 | 點擊「開始遊戲」 |
| 下注 | 選擇玩法 → 點選馬匹 → 設定金額 → 點「下注」 |
| 下注確認 | 點「確認，進入下一輪」（第3輪變為「開賽！」）|
| 分析師 | 第3輪可購買初級/資深情報 |
| 比賽 | 自動播放動畫，無需操作 |
| 結算 | 檢視結果後點「進入商店」 |
| 商店 | 購買防禦卡（最多持有3張），點「開始下一回合」 |

### 投注玩法

| 玩法 | 說明 | 選擇馬數 | 固定倍率 |
|------|------|---------|---------|
| 獨贏 | 選中第1名 | 1 | 依動態賠率 |
| 位置 | 選中前3名 | 1 | ×1.5 |
| 連贏 | 前2名不分順序 | 2 | ×5 |
| 正連贏 | 前2名按順序 | 2 | ×8 |
| 三重彩 | 前3名不分順序 | 3 | ×15 |
| 三連單 | 前3名按順序 | 3 | ×30 |

---

## 常見問題

### Q: 跳出 TMP Importer 視窗

專案已包含 TextMesh Pro Essential Resources，正常不會跳出。如果跳出，點「Import TMP Essentials」即可。

### Q: 賽道背景/馬匹顯示為白色方塊

圖片資源使用 Git LFS。請確認：
```bash
git lfs install
git lfs pull
```

### Q: 場景中沒有 GameManager

請開啟 `Assets/Scenes/SampleScene.unity`（不是其他場景）。

---

## 專案架構

```
Assets/HorseRacing/
├── Art/                    美術素材（馬匹、賽道圖片）
├── Data/                   ScriptableObject 設定資產（所有數值皆可調整）
├── Fonts/                  中文字型（微軟正黑體 SDF）
├── Scenes/                 遊戲場景
├── Scripts/
│   ├── Config/            ScriptableObject 定義（可調參數）
│   ├── Domain/            資料模型（Horse, Bet, RaceResult...）
│   ├── Systems/           純 C# 遊戲邏輯（無 MonoBehaviour）
│   ├── Flow/              GameManager 狀態機 + IRandom
│   └── UI/                程式化 UI 建構
└── Tests/                  NUnit 單元測試
```

### 設計原則

- **Config-driven**：所有數值在 ScriptableObject 中，零程式碼修改即可調參
- **純邏輯系統**：Systems 層完全不依賴 Unity MonoBehaviour，可獨立測試
- **確定性**：注入 IRandom 介面，測試可用 FakeRandom 保證結果可重現
- **UI 程式化**：無 Prefab，所有 UI 由程式碼建構

---

## 開發相關

### 執行測試

Unity Editor → Window → General → Test Runner → EditMode → Run All

### 新增可調參數

1. 在 `Scripts/Config/` 定義 ScriptableObject
2. 在 `Data/` 建立 `.asset`
3. 加入 `GameConfigDatabase.asset` 引用

---

## 授權

本專案為 2026 GameJam 作品。
