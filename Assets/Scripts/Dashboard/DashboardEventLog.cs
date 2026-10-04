using System.Collections.Generic;
using UnityEngine;
using FSOC.Contracts;

namespace FSOC.Dashboard
{
    public class DashboardEventLog : MonoBehaviour
    {
        [Tooltip("Max events to store in history.")]
        public int maxEvents = 50;

        private List<string> _events = new List<string>();
        public System.Action OnLogUpdated;

        public void LogEvent(string message)
        {
            string timeStr = Time.time.ToString("F2");
            string formatted = $"[{timeStr}] {message}";
            _events.Add(formatted);

            if (_events.Count > maxEvents)
            {
                _events.RemoveAt(0);
            }

            OnLogUpdated?.Invoke();
        }

        public IReadOnlyList<string> GetEvents() => _events;
    }
}
