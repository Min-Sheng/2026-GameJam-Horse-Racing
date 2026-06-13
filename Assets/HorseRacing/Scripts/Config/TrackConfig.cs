using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 賽道設定（PRD §6）：每匹馬對三種賽道的速度修正表。
    /// preferences[i] 對應馬號 (i+1)。
    /// </summary>
    [CreateAssetMenu(fileName = "TrackConfig", menuName = "HorseRacing/Track Config")]
    public class TrackConfig : ScriptableObject
    {
        [System.Serializable]
        public class HorsePreference
        {
            public int grass;
            public int mud;
            public int snow;
        }

        [System.Serializable]
        public class TrackInfo
        {
            public TrackType type;
            public string displayName;
        }

        [Header("可用賽道（隨機抽選其一，開賽公布）")]
        public List<TrackInfo> tracks = new List<TrackInfo>();

        [Header("每匹馬的賽道偏好（index 0 = Horse 1）")]
        public List<HorsePreference> preferences = new List<HorsePreference>();

        public string GetTrackName(TrackType type)
        {
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].type == type) return tracks[i].displayName;
            return type.ToString();
        }

        public int GetModifier(int horseId, TrackType track)
        {
            int idx = horseId - 1;
            if (idx < 0 || idx >= preferences.Count) return 0;
            var p = preferences[idx];
            switch (track)
            {
                case TrackType.Grass: return p.grass;
                case TrackType.Mud: return p.mud;
                case TrackType.Snow: return p.snow;
                default: return 0;
            }
        }
    }
}
