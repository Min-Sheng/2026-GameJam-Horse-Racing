using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// Maps each horse ID (1–8) to its corresponding sprite animation frames.
    /// Used by both the Betting Panel (frame 0 as icon) and Race View (full animation).
    /// </summary>
    [CreateAssetMenu(fileName = "HorseSpriteConfig", menuName = "HorseRacing/Horse Sprite Config")]
    public class HorseSpriteConfig : ScriptableObject
    {
        [System.Serializable]
        public struct HorseSpriteEntry
        {
            public Sprite[] frames;
        }

        [Tooltip("8 entries, index 0 = Horse 1, index 7 = Horse 8")]
        public HorseSpriteEntry[] entries = new HorseSpriteEntry[8];

        [Tooltip("Fallback sprite array when an entry is null/empty")]
        public Sprite[] defaultFrames;

        /// <summary>
        /// Returns the sprite array for the given horse ID (1-based).
        /// Returns defaultFrames for out-of-range IDs or null/empty entries.
        /// </summary>
        public Sprite[] GetSprites(int horseId)
        {
            if (horseId < 1 || horseId > 8)
                return defaultFrames;

            var entry = entries[horseId - 1];
            if (entry.frames != null && entry.frames.Length > 0)
                return entry.frames;

            return defaultFrames;
        }

        private void OnValidate()
        {
            if (defaultFrames == null || defaultFrames.Length == 0)
            {
                Debug.LogWarning($"[{name}] defaultFrames is null or empty. " +
                    "Horses with missing sprite entries will have no fallback.", this);
            }
        }
    }
}
