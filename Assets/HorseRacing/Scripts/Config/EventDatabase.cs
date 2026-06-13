using System.Collections.Generic;
using UnityEngine;

namespace HorseRacing
{
    /// <summary>所有可用隨機事件的集合（PRD §8）。</summary>
    [CreateAssetMenu(fileName = "EventDatabase", menuName = "HorseRacing/Event Database")]
    public class EventDatabase : ScriptableObject
    {
        public List<EventDefinition> events = new List<EventDefinition>();

        public EventDefinition FindByName(string eventName)
        {
            for (int i = 0; i < events.Count; i++)
                if (events[i] != null && events[i].eventName == eventName) return events[i];
            return null;
        }
    }
}
