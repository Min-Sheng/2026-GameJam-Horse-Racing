using UnityEngine;

namespace HorseRacing
{
    /// <summary>防禦卡定義（PRD §11）：以一定機率防禦特定事件。</summary>
    [CreateAssetMenu(fileName = "ProtectionCard", menuName = "HorseRacing/Protection Card")]
    public class ProtectionCardDefinition : ScriptableObject
    {
        public string cardName;
        [TextArea] public string description;

        [Tooltip("此卡防禦的目標事件")] public EventDefinition targetEvent;
        [Range(0f, 1f)]
        [Tooltip("防禦成功機率")] public float defendChance = 0.5f;
        [Tooltip("購買價格")] public long price = 150;
    }
}
