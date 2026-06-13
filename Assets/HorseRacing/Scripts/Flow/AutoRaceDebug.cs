using System.Collections;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>Auto-triggers race for debugging. Attach alongside GameManager. Remove after debug.</summary>
    public class AutoRaceDebug : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            var gm = GetComponent<GameManager>();
            if (gm == null) yield break;
            gm.StartNewRound();
            yield return null;
            gm.ConfirmBettingRound();
            yield return null;
            gm.ConfirmBettingRound();
            yield return null;
            gm.ConfirmBettingRound();
        }
    }
}
