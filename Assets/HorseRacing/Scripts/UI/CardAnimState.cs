namespace HorseRacing.UI
{
    /// <summary>事件卡片動畫狀態機。</summary>
    public enum CardAnimState
    {
        Idle,       // 無卡片顯示
        PopupIn,    // 正在彈出
        Holding,    // 中央停留
        Archiving   // 正在縮小歸檔
    }
}
