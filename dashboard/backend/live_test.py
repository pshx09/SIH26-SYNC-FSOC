import asyncio
import websockets
import json

async def listen():
    try:
        async with websockets.connect('ws://127.0.0.1:8000/ws/dashboard') as ws:
            msg = await ws.recv()
            data = json.loads(msg)
            print('LIVE DATA SNAPSHOT:')
            print('Timestamp:', data.get('Timestamp'))
            print('IsDetected:', data.get('IsDetected'))
            print('TrackingState:', data.get('TrackingState'))
            print('RadialError:', data.get('RadialError'))
            print('PanVelocity:', data.get('PanVelocity'))
            print('TiltVelocity:', data.get('TiltVelocity'))
            print('MaxError:', data.get('MaxError'))
    except Exception as e:
        print(f'Error: {e}')

asyncio.run(listen())
