import asyncio
import websockets
import json
import time

async def run_audit():
    print("=== FINAL PRE-REACT TELEMETRY AUDIT ===")
    uri = "ws://127.0.0.1:8000/ws/dashboard"
    
    states_captured = {
        0: None, # Searching
        1: None, # Acquiring
        2: None, # Locked (stationary/initial)
        "moving_locked": None # Locked with velocity > 0 or error changing
    }

    try:
        async with websockets.connect(uri) as ws:
            print("[Audit] Connected to dashboard WebSocket")
            
            start_time = time.time()
            last_locked_error = None

            while time.time() - start_time < 35:
                try:
                    msg = await asyncio.wait_for(ws.recv(), timeout=1.0)
                    data = json.loads(msg)
                    state = data.get("TrackingState")
                    
                    if state == 0 and states_captured[0] is None:
                        states_captured[0] = data
                        print("[Audit] Captured SEARCHING snapshot")
                        
                    elif state == 1 and states_captured[1] is None:
                        states_captured[1] = data
                        print("[Audit] Captured ACQUIRING snapshot")
                        
                    elif state == 2:
                        if states_captured[2] is None:
                            states_captured[2] = data
                            last_locked_error = data.get("ErrorX", 0)
                            print("[Audit] Captured initial LOCKED snapshot")
                        elif states_captured["moving_locked"] is None:
                            # Consider it 'moving' if error has changed or velocity is non-zero
                            if data.get("ErrorX", 0) != last_locked_error or data.get("PanVelocity", 0) != 0:
                                states_captured["moving_locked"] = data
                                print("[Audit] Captured moving LOCKED snapshot")
                                
                    if all(v is not None for v in states_captured.values()):
                        break
                except asyncio.TimeoutError:
                    continue

            print("\n--- CAPTURED DATA ---")
            for k, v in states_captured.items():
                if v:
                    state_name = "SEARCHING" if k == 0 else "ACQUIRING" if k == 1 else "LOCKED (initial)" if k == 2 else "LOCKED (moving)"
                    print(f"\n[{state_name}]")
                    # Print requested fields
                    print(f"TrackingState: {v.get('TrackingState')}")
                    print(f"DetectionConfidence: {v.get('DetectionConfidence')}")
                    print(f"CentroidX/Y: {v.get('CentroidX')}, {v.get('CentroidY')}")
                    print(f"ErrorX/Y: {v.get('ErrorX')}, {v.get('ErrorY')}")
                    print(f"FPS: {v.get('FPS')}")
                    print(f"PanAngle: {v.get('PanAngle')} | TiltAngle: {v.get('TiltAngle')}")
                    print(f"PanVelocity: {v.get('PanVelocity')} | TiltVelocity: {v.get('TiltVelocity')}")
                    print(f"MeanError: {v.get('MeanError')} | RMSE: {v.get('RMSE')}")
                    print(f"LockRetention: {v.get('LockRetention')} | TargetLossRate: {v.get('TargetLossRate')}")
                    print(f"TrajectoryScenario: {v.get('TrajectoryScenario')}")

    except Exception as e:
        print(f"Audit failed: {e}")

if __name__ == "__main__":
    asyncio.run(run_audit())
