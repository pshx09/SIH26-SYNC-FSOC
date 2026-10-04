using System;
using UnityEngine;
using FSOC.Contracts;

namespace FSOC.Dashboard
{
    public class TelemetryBus : MonoBehaviour
    {
        private TelemetrySnapshot _latestSnapshot;
        public Action<TelemetrySnapshot> OnTelemetryUpdated;

        public void Publish(TelemetrySnapshot snapshot)
        {
            _latestSnapshot = snapshot;
            OnTelemetryUpdated?.Invoke(snapshot);
        }

        public TelemetrySnapshot GetLatest() => _latestSnapshot;
    }
}
