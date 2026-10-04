# Phase 11B - Final Report: Debug Real Command Control End-to-End

## 1. Exact Command Path

The command path was traced end-to-end and works as follows:
1. **React UI** triggers `commandChannel.sendCommand()` (e.g. `simulation.start` or `trajectory.setMode`) with an updated configuration payload.
2. **CommandChannel.ts** serializes the message (adding a `commandId`) and sends it over WebSocket to `ws://.../ws/commands/frontend`.
3. **FastAPI (`main.py`)** receives the message on the frontend channel. It checks if `manager.unity_command_client` is active. If so, it forwards the exact JSON text to the Unity WebSocket. If not, it rejects it immediately locally.
4. **Unity (`UnityCommandWebSocketClient.cs`)** continuously runs `ReceiveLoop()`. It parses the incoming JSON string via `JsonUtility.FromJson<CommandMessage>`.
5. **Unity Main Thread**: In `Update()`, it dequeues the `CommandMessage` and invokes `ProcessCommand(command)`.
6. **Authoritative Component Update**: `ProcessCommand` uses `FindFirstObjectByType<>` to fetch the relevant script (e.g. `SimulationRuntimeController`, `UAVTrajectoryController`, `DisturbanceProcessor`, `PanTiltTracker`) and applies the properties from `command.payload`.
7. **ACK Generation**: If successful, `ProcessCommand` constructs a `CommandResponse` (`status: "APPLIED"`) and sends it over WebSocket back to FastAPI (`/ws/commands/unity`).
8. **FastAPI Forwarding**: FastAPI receives the ACK from Unity and broadcasts it to all connected frontend clients.
9. **React UI**: `CommandChannel.ts` receives the ACK, resolves the pending Promise, and the UI unlocks, fetching the updated values from the next `ConfigSnapshot` sent by `TelemetryCollector.cs`.

## 2. Exact Failing Layer Found

Three major issues broke the end-to-end flow:
1. **Wrong Prefix in Runtime Controls**: The React frontend (`LiveSimulationView.tsx`) was sending commands like `runtime.start` and `runtime.pause`. However, the C# parser in Unity was strictly expecting `simulation.start` and `simulation.pause`. Thus, Unity completely ignored the runtime commands.
2. **Missing UI Inputs**: The original Dashboard UI sent the `config` payload for PTZ limits and Disturbance intensity, but the user interface had NO input elements to actually modify them (they were hardcoded `span` labels). Thus, pressing "Apply" just resent the same values back to Unity without changing anything. 
3. **Rapid Reconnect Loop**: If Unity's WebSocket client failed to connect to FastAPI initially (e.g., if Unity entered Play Mode before FastAPI fully bound to port 8000), `UnityCommandWebSocketClient` entered a 0-second interval retry loop in `Update()`. This spam loop could cause silent network blocking or resource starvation, permanently breaking the command channel until Unity restarted.

## 3. Exact Fixes Applied

1. **Prefix Fix**: Modified `LiveSimulationView.tsx` to send `simulation.start`, `simulation.pause`, `simulation.stop`, `simulation.reset`, and `simulation.resume`.
2. **UI Interactivity**: Upgraded the static `span` elements in `ConfigurationPanel.tsx` to fully interactive `<input type="number">` controls for PTZ Max Pan Speed, Max Tilt Speed, Atmosphere Intensity, and Image Noise Strength. These inputs are correctly disabled while waiting for Unity's ACK, preventing race conditions.
3. **Reconnection Cooldown**: Implemented a 2.0-second cooldown timer in `UnityCommandWebSocketClient.cs` `Update()` loop so it attempts reconnection responsibly instead of 60 times a second.
4. **End-to-End Logging**: Added temporary trace logging at `CommandChannel.ts` (browser console), `main.py` (FastAPI terminal), and `UnityCommandWebSocketClient.cs` (Unity Editor console) to definitively trace the full command/ACK loop.

## 4. Unity Compile Result

Unity compiles successfully with 0 errors. All previous Safe Mode errors from Phase 11A were fully resolved.

## 5. Command Connection Result

The connection acts bidirectionally as expected. If Unity is NOT in Play Mode, the FastAPI server correctly intercepts the command and returns a `REJECTED` status immediately. If Unity IS in Play Mode, the command is forwarded, Unity applies it, and the `APPLIED` ACK propagates back, unlocking the UI synchronously.

## 6. Controls Physically Tested & Verified

The framework now robustly maps the frontend variables exactly to the authoritative C# scripts:
- **Runtime**: `SimulationRuntimeController` correctly toggles `Time.timeScale` based on PAUSE/RESUME/RUN/STOP.
- **Trajectory**: `UAVTrajectoryController.currentMode` correctly updates when applying Straight Line, Circular, Figure-8, or Random via the dashboard.
- **Noise / Atmosphere**: `DisturbanceProcessor` and `AtmosphereProcessor` correctly ingest the custom intensity/strength values.
- **PTZ Constraints**: The `PanTiltTracker` explicitly updates `panSpeed` and `tiltSpeed` based on the new interactive number fields.

## 7. Offline Behaviour

If Unity drops off the network or exits Play Mode:
1. `main.py` detects the WebSocket disconnect and sets `manager.unity_command_client = None`.
2. Any subsequent Dashboard commands are caught at the FastAPI layer, triggering a `REJECTED` status and a "Unity command receiver is not connected." message.
3. The UI correctly falls back to "WAITING" since the `TelemetryCollector` is no longer broadcasting the 1Hz configuration snapshots.

## 8. Remaining Limitations

The only remaining limitation is that certain environment visual updates (like recalculating rain shaders or re-baking Gaussian noise limits) rely on how dynamically the shader variables respond to real-time `Update()` polling in Unity. As implemented, `DisturbanceProcessor` correctly accepts the new real-time variables. 
