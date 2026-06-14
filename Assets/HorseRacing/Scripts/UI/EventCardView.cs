using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        /// <summary>
        /// 建立事件卡片 UI 結構。
        /// </summary>
        /// <param name="parent">父節點 Transform。</param>
        /// <param name="width">卡片寬度（px）。</param>
        /// <param name="height">卡片高度（px）。</param>
        public EventCardView(Transform parent, float width, float height)
        {
            // 根節點
            var rootGo = UIFactory.NewUIObject("EventCard", parent);
            Root = (RectTransform)rootGo.transform;
            Root.sizeDelta = new Vector2(width, height);

            // 背景面板
            var bg = rootGo.AddComponent<Image>();
            bg.color = UIFactory.Card;
            bg.raycastTarget = false;

            // 垂直佈局
            UIFactory.VLayout(rootGo, spacing: 4, padding: 10, align: TextAnchor.MiddleCenter,
                cChildW: true, cChildH: false, fChildW: true, fChildH: false);

            // 事件名稱
            EventNameText = UIFactory.Text(rootGo.transform, "", 20,
                TextAlignmentOptions.Center, UIFactory.TextMain);
            UIFactory.LE(EventNameText.gameObject, prefH: 28);

            // 馬匹資訊列（水平）
            var horseRow = UIFactory.NewUIObject("HorseRow", rootGo.transform);
            UIFactory.HLayout(horseRow, spacing: 6, padding: 0, align: TextAnchor.MiddleCenter,
                cChildW: false, cChildH: true, fChildW: false, fChildH: false);
            UIFactory.LE(horseRow, prefH: 24);

            // 馬匹顏色指示器
            HorseColorIndicator = UIFactory.Rect(horseRow.transform, "ColorIndicator", Color.white);
            HorseColorIndicator.raycastTarget = false;
            var ciRT = HorseColorIndicator.rectTransform;
            ciRT.sizeDelta = new Vector2(18, 18);
            UIFactory.LE(HorseColorIndicator.gameObject, prefW: 18, prefH: 18);

            // 馬匹編號文字
            HorseIdText = UIFactory.Text(horseRow.transform, "", 18,
                TextAlignmentOptions.MidlineLeft, UIFactory.TextMain);
            UIFactory.LE(HorseIdText.gameObject, prefW: 80, prefH: 24);

            // 速度修正值
            SpeedModifierText = UIFactory.Text(rootGo.transform, "", 22,
                TextAlignmentOptions.Center, UIFactory.TextMain);
            UIFactory.LE(SpeedModifierText.gameObject, prefH: 28);

            // 防禦成功標示（預設隱藏）
            DefendedLabel = UIFactory.Text(rootGo.transform, "防禦成功", 16,
                TextAlignmentOptions.Center, UIFactory.AccentGreen);
            UIFactory.LE(DefendedLabel.gameObject, prefH: 22);
            DefendedLabel.gameObject.SetActive(false);
        }

        /// <summary>
        /// 綁定事件資料至卡片 UI。
        /// </summary>
        /// <param name="log">賽事事件紀錄。</param>
        public void Bind(StageEventLog log)
        {
            // 事件名稱
            EventNameText.text = log.EventName;

            // 馬匹顏色（若 HorseId 超出範圍則使用白色）
            int colorIndex = log.HorseId - 1;
            Color horseColor = (colorIndex >= 0 && colorIndex < UIFactory.HorseColors.Length)
                ? UIFactory.HorseColors[colorIndex]
                : Color.white;

            HorseColorIndicator.color = horseColor;
            HorseIdText.text = $"Horse {log.HorseId}";
            HorseIdText.color = horseColor;

            // 防禦判定
            if (log.Defended)
            {
                SpeedModifierText.text = "0";
                SpeedModifierText.color = UIFactory.TextDim;
                DefendedLabel.gameObject.SetActive(true);
            }
            else
            {
                int mod = log.SpeedModifier;
                SpeedModifierText.text = mod >= 0 ? $"+{mod}" : $"{mod}";
                SpeedModifierText.color = mod >= 0 ? UIFactory.AccentGreen : UIFactory.AccentRed;
                DefendedLabel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 統一縮放卡片根節點。
        /// </summary>
        /// <param name="scale">縮放比例。</param>
        public void SetScale(float scale)
        {
            Root.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
