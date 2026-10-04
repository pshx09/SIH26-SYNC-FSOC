import asyncio
import websockets
import json
import time

async def test_command():
    async with websockets.connect("ws://127.0.0.1:8000/ws/commands/frontend") as ws:
        # Send a start command
        payload = {
            "commandId": "cmd_test_1",
            "commandType": "simulation.start",
            "payload": {},
            "timestamp": time.time()
        }
        await ws.send(json.dumps(payload))
        print("Sent command. Waiting for response...")
        
        try:
            response = await asyncio.wait_for(ws.recv(), timeout=5.0)
            print("Received response:", response)
        except asyncio.TimeoutError:
            print("Timed out waiting for response.")

asyncio.run(test_command())
