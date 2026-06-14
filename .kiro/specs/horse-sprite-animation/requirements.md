# Requirements Document

## Introduction

本功能為賽馬遊戲加入多角色 sprite 支援。目前所有 8 匹馬都使用同一張 `horseSprite` 圖片，無法區分角色外觀。此次改動將：

1. 在下注畫面（Betting Panel）中，每匹馬的欄位顯示各自 sprite sheet 的第 0 幀作為圖示（icon）。
2. 在賽馬動畫階段（Race View），每匹馬使用各自的逐幀 sprite sheet 播放奔跑動畫。

可用的 8 個角色 sprite sheet 位於 `Assets/HorseRacing/Art/Horses/`：
- horse_.png（原始馬匹，逐幀動畫）
- cat.png
- goldfish.png
- grandma.png
- spongebob.png
- tardis.png
- thief.png
- tombstone.png

## Glossary

- **Sprite_Sheet**: 一張包含多個動畫幀的 PNG 圖片，由 Unity Sprite Editor 切割為多個子 Sprite
- **Frame**: Sprite Sheet 中的單一子 Sprite，用於動畫播放的一個畫面
- **Horse_Sprite_Config**: 一個 ScriptableObject 資料結構，儲存每匹馬對應的 Sprite 陣列參照
- **Betting_Panel**: 下注階段的 UI 面板，顯示 8 匹馬的資訊與下注控制項
- **Race_View**: 賽馬動畫播放的 UI 元件，負責捲軸視角下的馬匹位移與顯示
- **Frame_Rate**: 動畫每秒播放的幀數，控制動畫速度
- **Icon_Sprite**: 每匹馬在下注欄位中顯示的靜態圖示，取自 Sprite Sheet 的第 0 幀

## Requirements

### Requirement 1: Sprite Sheet 匯入與切割

**User Story:** As a developer, I want each character sprite sheet to be properly sliced into individual frames in Unity, so that each frame can be used independently for icons and animation.

#### Acceptance Criteria

1. THE Sprite_Sheet SHALL be imported with sprite mode set to "Multiple" in the Unity TextureImporter settings, with `alphaIsTransparency` enabled
2. WHEN the Sprite_Sheet is sliced, THE Sprite_Sheet SHALL produce at least 2 individually addressable Frame sub-sprites, each named using the pattern `{sheetName}_{index}` (e.g., `horse__0`, `horse__1`), accessible via `AssetDatabase.LoadAllAssetsAtPath` or direct Inspector reference
3. THE Sprite_Sheet SHALL retain full alpha channel transparency for non-rectangular character shapes, with `alphaUsage` set to 1 (From Texture) in the texture importer
4. WHEN the Sprite_Sheet is sliced, each Frame sub-sprite SHALL have a non-zero width and height rect that contains visible pixel data without clipping the character artwork
5. IF a Sprite_Sheet contains fewer than 2 identifiable animation frames after slicing, THEN the Unity Console SHALL display a warning indicating the sprite sheet may not be sliced correctly

### Requirement 2: Horse Sprite 配置資料結構

**User Story:** As a developer, I want a centralized configuration that maps each horse ID (1–8) to its corresponding sprite array, so that both the betting UI and race animation can reference the correct sprites.

#### Acceptance Criteria

1. THE Horse_Sprite_Config SHALL store an ordered array of exactly 8 entries, where each entry contains a reference to a sprite array with at least 1 sprite (all frames for that horse)
2. THE Horse_Sprite_Config SHALL be a ScriptableObject accessible from the Unity Inspector
3. WHEN a horse ID (1–8) is queried, THE Horse_Sprite_Config SHALL return the sprite array at index (horseID − 1) for that horse
4. IF a sprite array entry is null or contains zero sprites, THEN THE Horse_Sprite_Config SHALL return an Inspector-assignable default sprite array containing at least 1 sprite
5. IF a horse ID outside the range 1–8 is queried, THEN THE Horse_Sprite_Config SHALL return the default sprite array

### Requirement 3: 下注畫面馬匹圖示

**User Story:** As a player, I want to see each horse's unique character icon in the betting panel, so that I can visually distinguish which horse I am betting on.

#### Acceptance Criteria

1. WHEN the Betting_Panel is displayed, THE Betting_Panel SHALL show the Frame at index 0 of each horse's sprite array as the Icon_Sprite in that horse's betting row
2. THE Icon_Sprite SHALL be displayed at a fixed size that fits within the betting row height (52px row height), with a maximum width of 52px
3. THE Icon_Sprite SHALL preserve the aspect ratio of the source sprite using Unity Image component's "PreserveAspect" option
4. WHEN an Icon_Sprite is displayed, THE Betting_Panel SHALL position the Icon_Sprite to the left of the horse information text, replacing the current solid-color chip indicator
5. FOR ALL 8 horses, each betting row SHALL display a visually distinct Icon_Sprite corresponding to that horse's assigned character
6. IF the sprite array for a horse is null or empty, THEN THE Betting_Panel SHALL display the default sprite from Horse_Sprite_Config at index 0 as a fallback icon

### Requirement 4: 賽馬動畫逐幀播放

**User Story:** As a player, I want to see each horse animate with a running motion during the race, so that the race feels lively and each character is visually distinct.

#### Acceptance Criteria

1. WHILE the Race_View is playing, THE Race_View SHALL cycle through each horse's sprite frames sequentially from index 0 to the last index, looping back to index 0 after the final frame, at the configured Frame_Rate to create a running animation
2. THE Frame_Rate SHALL be configurable via the RaceAnimConfig ScriptableObject, with a default value of 8 frames per second and a valid range of 1 to 30 frames per second
3. WHEN a horse crosses the finish line, THE Race_View SHALL stop the frame animation for that horse and display the frame at index 0 as the resting pose
4. THE Race_View SHALL display each horse using its own sprite array as defined in Horse_Sprite_Config, replacing the single shared horseSprite, where each array contains at least 2 frames
5. IF a horse's sprite array in Horse_Sprite_Config is null or contains fewer than 2 frames, THEN THE Race_View SHALL fall back to displaying the default shared horseSprite as a static image for that horse
6. THE Race_View SHALL preserve the aspect ratio of each frame sprite during animation playback
7. WHILE the Race_View is playing, THE Race_View SHALL apply the same bounce (bob) motion to animated sprites as currently applied to the static horse sprites

### Requirement 5: 效能與記憶體管理

**User Story:** As a developer, I want sprite assets to be managed efficiently, so that the game runs smoothly without excessive memory usage.

#### Acceptance Criteria

1. THE Horse_Sprite_Config SHALL load sprite references via Unity's asset serialization (Inspector assignment), avoiding runtime file I/O
2. WHILE the Race_View is playing, THE Race_View SHALL reuse the 8 pre-existing horse Image components for frame updates, without instantiating or destroying GameObjects during the animation loop
3. THE Race_View SHALL update sprite frames by assigning new sprites to existing Image.sprite property rather than destroying and recreating UI elements
4. WHILE 8 匹馬同時播放動畫期間，THE Race_View SHALL 維持每秒不低於 30 幀的渲染幀率，且動畫迴圈中每幀不產生新的堆積記憶體配置（zero per-frame heap allocation）
5. IF Horse_Sprite_Config 中任一馬匹的 sprite 參考為 null，THEN THE Race_View SHALL 跳過該馬匹的幀更新並保留其最後一次有效顯示的 sprite，不拋出例外
