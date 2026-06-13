using System.Collections.Generic;

namespace HorseRacing
{
    /// <summary>
    /// 隨機源抽象。系統層注入此介面，使單元測試能以固定 seed 做確定性驗證
    /// （PRD §14：每模組需可獨立測試）。
    /// </summary>
    public interface IRandom
    {
        /// <summary>0 .. maxExclusive-1</summary>
        int Next(int maxExclusive);
        /// <summary>minInclusive .. maxExclusive-1</summary>
        int Range(int minInclusive, int maxExclusive);
        /// <summary>0.0 .. 1.0</summary>
        float Value();
        /// <summary>Fisher–Yates 原地洗牌。</summary>
        void Shuffle<T>(IList<T> list);
    }

    /// <summary>以 System.Random 實作的隨機源，可指定 seed。</summary>
    public class SystemRandom : IRandom
    {
        private readonly System.Random _r;

        public SystemRandom() { _r = new System.Random(); }
        public SystemRandom(int seed) { _r = new System.Random(seed); }

        public int Next(int maxExclusive) => _r.Next(maxExclusive);
        public int Range(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        public float Value() => (float)_r.NextDouble();

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _r.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
