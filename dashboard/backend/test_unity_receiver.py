import asyncio
import websockets

async def listen_commands():
    async with websockets.connect("ws://127.0.0.1:8000/ws/commands/unity") as ws:
        print("Connected as Unity. Listening for commands...")
        while True:
            try:
                data = await ws.recv()
                print("Received command:", data)
                # Send fake ACK
                import json
                try:
                    payload = json.loads(data)
                    ack = {
                        "commandId": payload.get("commandId", ""),
                        "status": "APPLIED",
                        "message": "Fake ACK",
                        "stateRevision": 2
                    }
                    await ws.send(json.dumps(ack))
                    print("Sent ACK")
                except:
                    pass
            except websockets.exceptions.ConnectionClosed:
                print("Connection closed")
                break

asyncio.run(listen_commands())
