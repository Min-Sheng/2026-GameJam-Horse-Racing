using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>商店設定（PRD §11）。</summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "HorseRacing/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        [Tooltip("商店販售的防禦卡")] public List<ProtectionCardDefinition> availableCards = new List<ProtectionCardDefinition>();
        [Tooltip("玩家最多同時持有的防禦卡數量")] public int maxHeldCards = 3;
    }
}
