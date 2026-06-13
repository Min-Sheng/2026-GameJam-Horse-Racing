using UnityEngine;

namespace HorseRacing.UI
{
    /// <summary>Temp debug: logs RaceView field dimensions each frame for 5 frames after activation.</summary>
    public class RaceViewDebug : MonoBehaviour
    {
        private int _frames = 0;
        private RectTransform _rt;

        private void OnEnable()
        {
            _frames = 0;
            _rt = (RectTransform)transform;
        }

        private void Update()
        {
            if (_frames < 5)
            {
                Debug.Log($"[RaceViewDebug] Frame {_frames}: rect.width={_rt.rect.width}, rect.height={_rt.rect.height}, sizeDelta={_rt.sizeDelta}, anchorMin={_rt.anchorMin}, anchorMax={_rt.anchorMax}");
                _frames++;
            }
        }
    }
}
