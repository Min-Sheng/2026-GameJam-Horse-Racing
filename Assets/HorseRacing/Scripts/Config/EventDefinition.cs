using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 隨機事件定義（PRD §8）。管理者可新增任意事件資產。
    /// </summary>
    [CreateAssetMenu(fileName = "Event", menuName = "HorseRacing/Event Definition")]
    public class EventDefinition : ScriptableObject
    {
        [Tooltip("事件識別名稱（防禦卡以此比對）")] public string eventName;
        [Tooltip("顯示名稱")] public string displayName;
        [TextArea] public string description;

        [Range(0f, 1f)]
        [Tooltip("每階段觸發機率")] public float triggerChance = 0.1f;

        [Tooltip("命中時的速度修正值（可正可負）")] public int speedModifier = -2;

        [Tooltip("影響目標")] public EventTarget target = EventTarget.RandomSingleHorse;
    }
}
