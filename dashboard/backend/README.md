# FSOC Telemetry Bridge

This is a standalone local FastAPI server that acts as a bridge between the Unity tracking simulator and the React dashboard.

## Architecture

- **Unity** publishes telemetry snapshots via WebSocket to `/ws/telemetry`.
- **Browser Clients (React)** subscribe via WebSocket to `/ws/dashboard`.
- The server stores only the latest snapshot in memory and broadcasts it to all connected dashboard clients as soon as it arrives.
- If a browser connects mid-session, it immediately receives the latest snapshot.

## Prerequisites
- Python 3.9+

## Setup

1. Create a Python virtual environment:
   ```bash
   python -m venv venv
   ```
2. Activate the virtual environment:
   - **Windows:** `venv\Scripts\activate`
   - **Mac/Linux:** `source venv/bin/activate`
3. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

## Running the Server

Start the FastAPI server using Uvicorn:
```bash
uvicorn main:app --host 127.0.0.1 --port 8000
```
*(Add `--reload` if you are developing and want auto-reload on file changes).*

## Endpoints

- **`GET /`**: Returns a basic JSON message confirming the server is running.
- **`GET /health`**: Returns the server status, whether Unity is connected, and the number of dashboard clients.
- **`ws://127.0.0.1:8000/ws/telemetry`**: WebSocket endpoint for Unity (Publisher).
- **`ws://127.0.0.1:8000/ws/dashboard`**: WebSocket endpoint for Browser/React clients (Subscribers).

## Example Payload from Unity
```json
{
    "Timestamp": 124.5,
    "DetectorType": 2,
    "DetectionConfidence": 1.0,
    "CentroidX": 320.5,
    "CentroidY": 240.0,
    "TrackingState": 2,
    "ErrorX": 0.5,
    "ErrorY": -1.2,
    "RMSE": 1.5,
    "FPS": 60.0
}
```
*(See `UnityTelemetryWebSocketPublisher.cs` and `TelemetrySnapshot.cs` for the full schema).*
