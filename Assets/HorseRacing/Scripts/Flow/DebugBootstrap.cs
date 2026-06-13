using System.Text;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>
    /// 無 UI 的流程驗證器（M3）：進入 Play Mode 後自動跑完一回合並輸出 Console。
    /// 正式 UI 完成後可停用此元件。
    /// </summary>
    [RequireComponent(typeof(GameManager))]
    public class DebugBootstrap : MonoBehaviour
    {
        public bool runOnStart = false;

        private GameManager _gm;

        private void Awake()
        {
            _gm = GetComponent<GameManager>();
            _gm.OnNotice += msg => Debug.Log("[賽馬] " + msg);
        }

        private void Start()
        {
            if (runOnStart) RunOneRound();
        }

        [ContextMenu("Run One Round")]
        public void RunOneRound()
        {
            if (_gm.config == null) { Debug.LogError("DebugBootstrap: GameManager.config 未設定"); return; }

            Debug.Log("====== 自動測試一回合 ======");
            _gm.StartNewRound();

            // 三輪：每輪在最被看好的馬上下注 100 獨贏，並確認進入下一輪
            for (int r = 0; r < _gm.BettingRounds; r++)
            {
                var fav = _gm.Round.CurrentOdds.Count > 0 ? _gm.Round.CurrentOdds[0] : null;
                if (fav != null)
                {
                    Debug.Log($"  第{r + 1}輪 最熱門：Horse {fav.HorseId} 賠率 {fav.WinOdds:0.00}");
                    _gm.PlaceBet(BetType.Win, 100, new[] { fav.HorseId });
                }
                if (_gm.IsLastBettingRound)
                    _gm.BuyAnalystReport(AnalystTier.Senior);

                _gm.ConfirmBettingRound(); // 最後一輪會自動開賽
            }

            // 開賽後進入 Racing；直接結算（動畫於 M5 接上）
            LogStandings();
            _gm.CompleteRaceAndSettle();

            Debug.Log($"  玩家資金：{_gm.Player.Money}");
            _gm.EnterShop();
            if (_gm.config.shop.availableCards.Count > 0)
                _gm.BuyProtectionCard(_gm.config.shop.availableCards[0]);
            Debug.Log($"  持有防禦卡：{_gm.Player.ProtectionCards.Count}");
            Debug.Log("====== 回合結束 ======");
        }

        private void LogStandings()
        {
            if (_gm.Round.Result == null) return;
            var sb = new StringBuilder("  名次（賽道 ").Append(_gm.config.track.GetTrackName(_gm.Round.Track)).Append("）：");
            foreach (var s in _gm.Round.Result.Standings)
                sb.Append($"\n    {s.Rank}. Horse {s.HorseId} (速度 {s.FinalSpeed})");
            Debug.Log(sb.ToString());
            if (_gm.Round.Result.Events.Count > 0)
            {
                var es = new StringBuilder("  事件：");
                foreach (var e in _gm.Round.Result.Events)
                    es.Append($"\n    Stage{e.Stage} Horse{e.HorseId} {e.EventName} {(e.Defended ? "(已防禦)" : e.SpeedModifier.ToString())}");
                Debug.Log(es.ToString());
            }
        }
    }
}
