using UnityEngine;

namespace HorseRacing
{
    /// <summary>小地圖與事件卡片的動畫/佈局配置。</summary>
    [CreateAssetMenu(fileName = "RaceAnimConfig", menuName = "HorseRacing/Race Anim Config")]
    public class RaceAnimConfig : ScriptableObject
    {
        [Header("Minimap")]
        [Tooltip("佔畫面寬度比例")] public float minimapWidthPercent = 0.30f;
        [Tooltip("佔畫面高度比例")] public float minimapHeightPercent = 0.28f;
        [Tooltip("與畫面邊緣間距")] public float minimapMarginPx = 10f;
        [Tooltip("背景不透明度 (0.5~0.7)")] public float minimapBgAlpha = 0.6f;

        [Header("EventCard - Timing")]
        [Tooltip("彈出動畫時長（秒）")] public float cardPopupDuration = 0.25f;
        [Tooltip("中央停留時長（秒）")] public float cardHoldDuration = 0.4f;
        [Tooltip("歸檔動畫時長（秒）")] public float cardArchiveDuration = 0.35f;

        [Header("EventCard - Layout")]
        [Tooltip("卡片寬度（px）")] public float cardWidth = 150f;
        [Tooltip("卡片高度（px）")] public float cardHeight = 80f;
        [Tooltip("歸檔後縮放比例")] public float archivedScale = 1.0f;
        [Tooltip("堆疊最多可見數量")] public int maxVisibleArchived = 8;
        [Tooltip("堆疊與畫面邊緣間距")] public float stackMarginPx = 8f;
        [Tooltip("堆疊卡片間距")] public float stackSpacing = 6f;

        [Header("Horse Sprite Animation")]
        [Tooltip("Animation frame rate (frames per second), range 1-30")]
        [Range(1, 30)]
        public int spriteFrameRate = 8;
    }
}
