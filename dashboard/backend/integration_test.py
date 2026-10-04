import asyncio
import websockets
import json
import time

async def run_integration_test():
    print("=== REAL UNITY TO FASTAPI INTEGRATION TEST ===")
    
    # 1. Connect to dashboard
    dash_ws_uri = "ws://127.0.0.1:8000/ws/dashboard"
    telemetry_ws_uri = "ws://127.0.0.1:8000/ws/telemetry"

    # We will connect as a dashboard client to observe what FastAPI forwards from Unity.
    try:
        async with websockets.connect(dash_ws_uri) as dash_ws:
            print("[Dashboard] Connected to /ws/dashboard")
            
            # Wait for Unity to connect and send data
            print("[Dashboard] Waiting for REAL Unity telemetry...")
            
            snapshots = []
            
            # Capture 10 snapshots to observe changes
            for i in range(10):
                msg = await dash_ws.recv()
                data = json.loads(msg)
                snapshots.append(data)
                if i == 0:
                    print(f"\n[Dashboard] First payload captured: \n{json.dumps(data, indent=2)}\n")
            
            print("[Dashboard] Captured 10 payloads.")
            
            # Verify changes over time (timestamp should increment)
            ts_start = snapshots[0]["Timestamp"]
            ts_end = snapshots[-1]["Timestamp"]
            print(f"[Verification] Timestamp changed: {ts_start} -> {ts_end}")
            if ts_end > ts_start:
                print("[Verification] Timestamp is incrementing. PASS")
            else:
                print("[Verification] Timestamp NOT incrementing. FAIL")
            
            # Now test malformed JSON on the publisher endpoint
            # We connect as a rogue publisher and send bad json
            print("\n[Rogue Publisher] Connecting to /ws/telemetry to send malformed JSON...")
            try:
                async with websockets.connect(telemetry_ws_uri) as rogue_ws:
                    await rogue_ws.send("MALFORMED JSON { NOT VALID }")
                    print("[Rogue Publisher] Sent malformed JSON.")
                    await asyncio.sleep(1) # give server time to process
            except Exception as e:
                print(f"[Rogue Publisher] Error: {e}")
            
            # Check if dashboard connection is still alive and receiving
            print("[Dashboard] Waiting to ensure server is still alive after malformed JSON...")
            msg = await asyncio.wait_for(dash_ws.recv(), timeout=5.0)
            if msg:
                print("[Verification] Server survived malformed JSON and is still forwarding. PASS")
            
            # Verify latest-snapshot behavior
            print("\n[Latest-Snapshot Test] Connecting a new dashboard client...")
            async with websockets.connect(dash_ws_uri) as new_dash_ws:
                # Should immediately get a message without waiting for the next Unity tick
                msg = await asyncio.wait_for(new_dash_ws.recv(), timeout=1.0)
                if msg:
                    print("[Verification] New client received immediate snapshot. PASS")
                
            print("\n=== INTEGRATION TEST COMPLETE ===")
            
    except Exception as e:
        print(f"Integration test failed: {e}")

if __name__ == "__main__":
    asyncio.run(run_integration_test())
