import asyncio
import websockets
import json

async def simulate_unity():
    uri = "ws://127.0.0.1:8000/ws/telemetry"
    try:
        async with websockets.connect(uri) as websocket:
            print("[Unity Test] Connected to /ws/telemetry")
            test_payload = {
                "Timestamp": 1.0,
                "TrackingState": 2,
                "ErrorX": 5.0,
                "ErrorY": -3.0
            }
            await websocket.send(json.dumps(test_payload))
            print(f"[Unity Test] Sent payload: {test_payload}")
            await asyncio.sleep(0.5) # keep connection open briefly
    except Exception as e:
        print(f"[Unity Test] Failed: {e}")

async def simulate_dashboard():
    uri = "ws://127.0.0.1:8000/ws/dashboard"
    try:
        async with websockets.connect(uri) as websocket:
            print("[Dashboard Test] Connected to /ws/dashboard")
            response = await websocket.recv()
            print(f"[Dashboard Test] Received snapshot: {response}")
    except Exception as e:
        print(f"[Dashboard Test] Failed: {e}")

async def test_bridge():
    # Start the unity publisher which sends a message and stays open
    unity_task = asyncio.create_task(simulate_unity())
    
    # Wait a tiny bit for the message to be received by the server
    await asyncio.sleep(0.2)
    
    # Dashboard connects, should receive the latest snapshot immediately
    dash_task = asyncio.create_task(simulate_dashboard())
    
    await asyncio.gather(unity_task, dash_task)
    print("[Test Completed]")

if __name__ == "__main__":
    asyncio.run(test_bridge())
