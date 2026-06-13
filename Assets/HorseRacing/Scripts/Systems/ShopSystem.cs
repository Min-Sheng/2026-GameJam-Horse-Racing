namespace HorseRacing
{
    /// <summary>商店系統（PRD §11）：購買防禦卡，受最大持有數與資金限制。</summary>
    public static class ShopSystem
    {
        public static bool CanBuy(PlayerState player, ProtectionCardDefinition card, ShopConfig shop)
        {
            if (player == null || card == null || shop == null) return false;
            if (player.ProtectionCards.Count >= shop.maxHeldCards) return false;
            if (player.Money < card.price) return false;
            return true;
        }

        /// <summary>購買成功則扣款並加入持有清單，回傳是否成功。</summary>
        public static bool Buy(PlayerState player, ProtectionCardDefinition card, ShopConfig shop)
        {
            if (!CanBuy(player, card, shop)) return false;
            player.Money -= card.price;
            player.ProtectionCards.Add(card);
            return true;
        }
    }
}
