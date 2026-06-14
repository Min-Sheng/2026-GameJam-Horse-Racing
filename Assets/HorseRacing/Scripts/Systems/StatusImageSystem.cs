using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 狀態圖片系統：將馬匹的 HiddenBonus 轉換為對應的狀態 Sprite。
    /// 純靜態邏輯，無狀態、無副作用。
    /// </summary>
    public static class StatusImageSystem
    {
        /// <summary>
        /// 取得指定馬匹在當前局的狀態 Sprite。
        /// </summary>
        /// <param name="config">狀態設定檔（可為 null）</param>
        /// <param name="horseIndex">馬匹索引 0..7</param>
        /// <param name="hiddenBonus">該馬被分配的 HiddenBonus 值</param>
        /// <returns>對應的 Sprite；任何參數無效時回傳 null</returns>
        public static Sprite GetStatusSprite(HorseStatusConfig config, int horseIndex, int hiddenBonus)
        {
            if (config == null) return null;
            if (hiddenBonus < 0 || hiddenBonus > 7) return null;
            int statusIndex = config.bonusToStatusMap[hiddenBonus];
            return config.GetSprite(horseIndex, statusIndex);
        }
    }
}
