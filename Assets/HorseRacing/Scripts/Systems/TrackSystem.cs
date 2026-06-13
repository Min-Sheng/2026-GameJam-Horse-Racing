using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>賽道系統（PRD §6）：隨機決定賽道並套用每匹馬的賽道修正。</summary>
    public static class TrackSystem
    {
        /// <summary>從設定的賽道清單隨機抽選一種（下注期間隱藏，開賽公布）。</summary>
        public static TrackType PickTrack(TrackConfig cfg, IRandom rng)
        {
            if (cfg.tracks != null && cfg.tracks.Count > 0)
                return cfg.tracks[rng.Next(cfg.tracks.Count)].type;
            // 後備：預設使用 Grass（避免 hard-code 賽道數量）
            return TrackType.Grass;
        }

        /// <summary>將賽道修正寫入每匹馬的 TrackModifier。</summary>
        public static void ApplyTrackModifiers(List<Horse> horses, TrackType track, TrackConfig cfg)
        {
            for (int i = 0; i < horses.Count; i++)
                horses[i].TrackModifier = cfg.GetModifier(horses[i].Id, track);
        }
    }
}
