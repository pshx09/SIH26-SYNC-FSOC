import asyncio
import websockets
import json

async def listen():
    try:
        async with websockets.connect('ws://127.0.0.1:8000/ws/dashboard') as ws:
            while True:
                msg = await ws.recv()
                data = json.loads(msg)
                if data.get('InputMode') is not None:
                    print('Keys:', list(data.keys()))
                    print('IsDetected:', data.get('IsDetected'))
                    break
    except Exception as e:
        print(f'Error: {e}')

asyncio.run(listen())
