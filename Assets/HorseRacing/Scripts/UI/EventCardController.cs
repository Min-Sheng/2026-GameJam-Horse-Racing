using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing.UI
{
    /// <summary>事件卡片控制器，管理卡片彈出、歸檔、堆疊。</summary>
    public class EventCardController : MonoBehaviour
    {
        // --- 內部狀態 ---
        private Queue<StageEventLog> _eventQueue;
        private EventCardView _activeCard;
        private RectTransform _stackPanel;
        private List<EventCardView> _archivedCards;
        private bool _isAnimating;
        private CardAnimState _state;
        private int _animStartFrame; // 動畫開始時的幀數，用於判斷是否為同一幀批次

        // --- 配置 ---
        private RaceAnimConfig _config;
        private RectTransform _parentRT;
        private RectTransform _activeCardArea;

        // --- 預設配置值（當 RaceAnimConfig 為 null 時使用） ---
        private const float DefaultCardWidth = 150f;
        private const float DefaultCardHeight = 80f;
        private const float DefaultArchivedScale = 1.0f;
        private const int DefaultMaxVisibleArchived = 8;
        private const float DefaultStackMarginPx = 8f;
        private const float DefaultStackSpacing = 6f;
        private const float DefaultCardPopupDuration = 0.25f;
        private const float DefaultCardHoldDuration = 0.4f;
        private const float DefaultCardArchiveDuration = 0.35f;

        // --- 配置取值屬性 ---
        private float CardWidth => _config != null ? _config.cardWidth : DefaultCardWidth;
        private float CardHeight => _config != null ? _config.cardHeight : DefaultCardHeight;
        private float ArchivedScale => _config != null ? _config.archivedScale : DefaultArchivedScale;
        private int MaxVisibleArchived => _config != null ? _config.maxVisibleArchived : DefaultMaxVisibleArchived;
        private float StackMarginPx => _config != null ? _config.stackMarginPx : DefaultStackMarginPx;
        private float StackSpacing => _config != null ? _config.stackSpacing : DefaultStackSpacing;
        private float CardPopupDuration => _config != null ? _config.cardPopupDuration : DefaultCardPopupDuration;
        private float CardHoldDuration => _config != null ? _config.cardHoldDuration : DefaultCardHoldDuration;
        private float CardArchiveDuration => _config != null ? _config.cardArchiveDuration : DefaultCardArchiveDuration;

        /// <summary>
        /// 初始化事件卡片控制器。
        /// </summary>
        /// <param name="parentRT">父節點 RectTransform（通常為 RaceView 根節點）。</param>
        /// <param name="config">動畫/佈局配置，可為 null（使用預設值）。</param>
        public void Init(RectTransform parentRT, RaceAnimConfig config = null)
        {
            _parentRT = parentRT;
            _config = config;

            _eventQueue = new Queue<StageEventLog>();
            _archivedCards = new List<EventCardView>();
            _isAnimating = false;
            _state = CardAnimState.Idle;
            _animStartFrame = -1;

            // 建立 active card 區域（置中於 parent）
            var activeAreaGo = UIFactory.NewUIObject("EventCard_ActiveArea", parentRT);
            _activeCardArea = (RectTransform)activeAreaGo.transform;
            _activeCardArea.anchorMin = new Vector2(0.5f, 0.5f);
            _activeCardArea.anchorMax = new Vector2(0.5f, 0.5f);
            _activeCardArea.pivot = new Vector2(0.5f, 0.5f);
            _activeCardArea.anchoredPosition = Vector2.zero;
            _activeCardArea.sizeDelta = new Vector2(CardWidth, CardHeight);

            // 建立堆疊面板（左上角，水平並排）
            var stackGo = UIFactory.NewUIObject("EventCard_Stack", parentRT);
            _stackPanel = (RectTransform)stackGo.transform;
            _stackPanel.anchorMin = new Vector2(0f, 1f);
            _stackPanel.anchorMax = new Vector2(0f, 1f);
            _stackPanel.pivot = new Vector2(0f, 1f);
            _stackPanel.anchoredPosition = new Vector2(StackMarginPx, -StackMarginPx);
            // 堆疊面板大小：水平方向容納多張卡片
            _stackPanel.sizeDelta = new Vector2(1200f, CardHeight * ArchivedScale + 20f);
        }

        /// <summary>顯示事件卡片系統。</summary>
        public void Show()
        {
            if (_activeCardArea != null)
                _activeCardArea.gameObject.SetActive(true);
            if (_stackPanel != null)
                _stackPanel.gameObject.SetActive(true);
        }

        /// <summary>隱藏事件卡片系統。</summary>
        public void Hide()
        {
            if (_activeCardArea != null)
                _activeCardArea.gameObject.SetActive(false);
            if (_stackPanel != null)
                _stackPanel.gameObject.SetActive(false);
        }

        /// <summary>清除所有卡片與佇列（賽事結束時呼叫）。</summary>
        public void Clear()
        {
            // 停止所有動畫 coroutine
            StopAllCoroutines();

            // 清空佇列
            _eventQueue.Clear();

            // 銷毀當前卡片
            if (_activeCard != null)
            {
                Destroy(_activeCard.Root.gameObject);
                _activeCard = null;
            }

            // 銷毀所有歸檔卡片
            foreach (var card in _archivedCards)
            {
                if (card != null && card.Root != null)
                    Destroy(card.Root.gameObject);
            }
            _archivedCards.Clear();

            _isAnimating = false;
            _state = CardAnimState.Idle;
        }

        /// <summary>
        /// 將事件加入顯示佇列。依序處理，前一張完成/中斷後才顯示下一張。
        /// 若當前卡片正在動畫中（且非同一幀批次），立即歸檔後再處理新事件。
        /// </summary>
        /// <param name="eventLog">賽事事件紀錄。</param>
        public void EnqueueEvent(StageEventLog eventLog)
        {
            if (eventLog == null) return;

            _eventQueue.Enqueue(eventLog);

            // 若有卡片正在動畫中且動畫已跨幀（非同一批次），中斷並強制歸檔
            // 同一幀內批次入隊的事件不觸發中斷，保持 FIFO 依序顯示
            if (_isAnimating && _activeCard != null && Time.frameCount > _animStartFrame)
            {
                ForceArchiveCurrent();
            }

            // 若閒置，開始處理佇列
            if (!_isAnimating)
            {
                ProcessNextInQueue();
            }
        }

        /// <summary>強制歸檔當前卡片（用於新事件中斷）。</summary>
        public void ForceArchiveCurrent()
        {
            // 停止當前動畫
            StopAllCoroutines();

            if (_activeCard != null)
            {
                // 直接將卡片移至歸檔位置
                ArchiveCardImmediate(_activeCard);
                _activeCard = null;
            }

            _isAnimating = false;
            _state = CardAnimState.Idle;
        }

        /// <summary>開始處理佇列中下一個事件。</summary>
        private void ProcessNextInQueue()
        {
            if (_eventQueue.Count == 0)
            {
                _isAnimating = false;
                _state = CardAnimState.Idle;
                return;
            }

            var nextEvent = _eventQueue.Dequeue();
            _isAnimating = true;
            _animStartFrame = Time.frameCount;
            StartCoroutine(AnimateCard(nextEvent));
        }

        /// <summary>
        /// 卡片動畫 coroutine（彈出 → 停留 → 歸檔）。
        /// </summary>
        private IEnumerator AnimateCard(StageEventLog eventLog)
        {
            // 建立新卡片
            _activeCard = new EventCardView(_activeCardArea, CardWidth, CardHeight);
            _activeCard.Bind(eventLog);
            _activeCard.SetScale(0f);

            // --- PopupIn 階段 ---
            _state = CardAnimState.PopupIn;
            float elapsed = 0f;
            float popupDur = CardPopupDuration;
            while (elapsed < popupDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / popupDur);
                _activeCard.SetScale(t);
                yield return null;
            }
            _activeCard.SetScale(1f);

            // --- Holding 階段 ---
            _state = CardAnimState.Holding;
            yield return new WaitForSeconds(CardHoldDuration);

            // --- Archiving 階段 ---
            _state = CardAnimState.Archiving;
            elapsed = 0f;
            float archiveDur = CardArchiveDuration;
            Vector2 startPos = _activeCard.Root.anchoredPosition;
            Vector2 targetPos = GetNextArchivePosition();
            float startScale = 1f;
            float endScale = ArchivedScale;

            while (elapsed < archiveDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / archiveDur);
                _activeCard.SetScale(Mathf.Lerp(startScale, endScale, t));
                _activeCard.Root.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // 歸檔完成
            _activeCard.SetScale(endScale);
            _activeCard.Root.anchoredPosition = targetPos;
            ArchiveCardFinalize(_activeCard);
            _activeCard = null;

            // 處理下一個
            ProcessNextInQueue();
        }

        /// <summary>立即歸檔卡片（無動畫）。</summary>
        private void ArchiveCardImmediate(EventCardView card)
        {
            card.SetScale(ArchivedScale);
            card.Root.SetParent(_stackPanel, false);

            // 水平並排：依序向右排列
            float xOffset = _archivedCards.Count * (CardWidth * ArchivedScale + StackSpacing);
            card.Root.anchorMin = new Vector2(0f, 1f);
            card.Root.anchorMax = new Vector2(0f, 1f);
            card.Root.pivot = new Vector2(0f, 1f);
            card.Root.anchoredPosition = new Vector2(xOffset, 0f);

            _archivedCards.Add(card);
            EnforceStackOverflow();
        }

        /// <summary>歸檔卡片最終化（動畫完成後呼叫）。</summary>
        private void ArchiveCardFinalize(EventCardView card)
        {
            card.Root.SetParent(_stackPanel, false);

            // 水平並排：依序向右排列
            float xOffset = _archivedCards.Count * (CardWidth * ArchivedScale + StackSpacing);
            card.Root.anchorMin = new Vector2(0f, 1f);
            card.Root.anchorMax = new Vector2(0f, 1f);
            card.Root.pivot = new Vector2(0f, 1f);
            card.Root.anchoredPosition = new Vector2(xOffset, 0f);

            _archivedCards.Add(card);
            EnforceStackOverflow();
        }

        /// <summary>計算下一張歸檔卡片的目標位置（相對於 activeCardArea）。</summary>
        private Vector2 GetNextArchivePosition()
        {
            // 水平方向：向左上角移動，xOffset 為堆疊中的水平位置
            float xOffset = _archivedCards.Count * (CardWidth * ArchivedScale + StackSpacing);
            return new Vector2(-CardWidth * 0.5f + xOffset, CardHeight * 0.5f);
        }

        /// <summary>管理堆疊溢出：隱藏超出最大可見數量的舊卡片。</summary>
        private void EnforceStackOverflow()
        {
            int count = _archivedCards.Count;
            if (count <= MaxVisibleArchived) return;

            // 隱藏超出上限的最舊卡片
            int hideCount = count - MaxVisibleArchived;
            for (int i = 0; i < count; i++)
            {
                if (_archivedCards[i] != null && _archivedCards[i].Root != null)
                {
                    _archivedCards[i].Root.gameObject.SetActive(i >= hideCount);
                }
            }
        }
    }
}
