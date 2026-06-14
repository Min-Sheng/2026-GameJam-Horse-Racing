using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 馬匹狀態圖片設定檔。
    /// 管理 8 匹馬 × 8 種狀態的 Sprite 對應，以及 HiddenBonus 到狀態索引的對應表。
    /// </summary>
    [CreateAssetMenu(fileName = "HorseStatusConfig", menuName = "HorseRacing/Horse Status Config")]
    public class HorseStatusConfig : ScriptableObject
    {
        [Header("馬匹名稱（索引 0..7）")]
        [Tooltip("8 匹馬的顯示名稱，順序對應馬匹索引")]
        public string[] horseNames = new string[8];

        [Header("狀態名稱（索引 0..7）")]
        [Tooltip("8 種狀態的顯示名稱，順序對應狀態索引")]
        public string[] statusNames = new string[8];

        [Header("HiddenBonus → 狀態索引對應（長度 8）")]
        [Tooltip("陣列第 N 個元素 = HiddenBonus=N 對應的狀態索引")]
        public int[] bonusToStatusMap = new int[8];

        [Header("狀態圖片（8×8 = 64 張）")]
        [Tooltip("一維陣列，索引 = horseIndex * 8 + statusIndex")]
        public Sprite[] sprites = new Sprite[64];

        /// <summary>
        /// 以馬匹索引與狀態索引查詢 Sprite。
        /// 任何索引超出範圍或 sprite 未指派時回傳 null。
        /// </summary>
        public Sprite GetSprite(int horseIndex, int statusIndex)
        {
            if (horseIndex < 0 || horseIndex > 7) return null;
            if (statusIndex < 0 || statusIndex > 7) return null;
            return sprites[horseIndex * 8 + statusIndex];
        }
    }
}
