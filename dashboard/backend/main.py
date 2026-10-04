from fastapi import FastAPI, WebSocket, WebSocketDisconnect, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from typing import List, Dict
import json
import asyncio
import os
from pydantic import BaseModel

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

class ConnectionManager:
    def __init__(self):
        self.telemetry_clients: List[WebSocket] = []
        self.simulation_clients: List[WebSocket] = []
        self.sensor_clients: List[WebSocket] = []
        
        self.latest_telemetry: str = None
        self.latest_simulation_frame: bytes = None
        self.latest_sensor_frame: bytes = None

    async def connect_telemetry(self, websocket: WebSocket):
        await websocket.accept()
        self.telemetry_clients.append(websocket)
        if self.latest_telemetry:
            await websocket.send_text(self.latest_telemetry)

    async def connect_simulation(self, websocket: WebSocket):
        await websocket.accept()
        self.simulation_clients.append(websocket)
        if self.latest_simulation_frame:
            await websocket.send_bytes(self.latest_simulation_frame)

    async def connect_sensor(self, websocket: WebSocket):
        await websocket.accept()
        self.sensor_clients.append(websocket)
        if self.latest_sensor_frame:
            await websocket.send_bytes(self.latest_sensor_frame)

    def disconnect_telemetry(self, websocket: WebSocket):
        if websocket in self.telemetry_clients:
            self.telemetry_clients.remove(websocket)

    def disconnect_simulation(self, websocket: WebSocket):
        if websocket in self.simulation_clients:
            self.simulation_clients.remove(websocket)

    def disconnect_sensor(self, websocket: WebSocket):
        if websocket in self.sensor_clients:
            self.sensor_clients.remove(websocket)

    async def broadcast_telemetry(self, message: str):
        self.latest_telemetry = message
        # Use gather to avoid slow clients blocking the loop
        tasks = []
        for client in self.telemetry_clients:
            tasks.append(client.send_text(message))
        if tasks:
            results = await asyncio.gather(*tasks, return_exceptions=True)
            for client, res in zip(self.telemetry_clients.copy(), results):
                if isinstance(res, Exception):
                    self.disconnect_telemetry(client)

    async def broadcast_simulation(self, frame: bytes):
        self.latest_simulation_frame = frame
        tasks = []
        for client in self.simulation_clients:
            tasks.append(client.send_bytes(frame))
        if tasks:
            results = await asyncio.gather(*tasks, return_exceptions=True)
            for client, res in zip(self.simulation_clients.copy(), results):
                if isinstance(res, Exception):
                    self.disconnect_simulation(client)

    async def broadcast_sensor(self, frame: bytes):
        self.latest_sensor_frame = frame
        tasks = []
        for client in self.sensor_clients:
            tasks.append(client.send_bytes(frame))
        if tasks:
            results = await asyncio.gather(*tasks, return_exceptions=True)
            for client, res in zip(self.sensor_clients.copy(), results):
                if isinstance(res, Exception):
                    self.disconnect_sensor(client)

manager = ConnectionManager()

@app.get("/")
async def root():
    return {"message": "FSOC Tracking Simulator Telemetry Bridge"}

@app.get("/health")
async def health():
    return {
        "status": "ok", 
        "telemetry_clients": len(manager.telemetry_clients),
        "simulation_clients": len(manager.simulation_clients),
        "sensor_clients": len(manager.sensor_clients)
    }

# ==========================================
# VIDEO UPLOAD
# ==========================================

@app.post("/upload_video")
async def upload_video(file: UploadFile = File(...)):
    # Save the file to a temporary location
    os.makedirs("temp_videos", exist_ok=True)
    file_path = os.path.join(os.path.abspath("temp_videos"), file.filename)
    
    with open(file_path, "wb") as buffer:
        content = await file.read()
        buffer.write(content)
        
    return {"filename": file.filename, "path": file_path}

# ==========================================
# TELEMETRY (JSON)
# ==========================================

@app.websocket("/ws/telemetry")
async def ws_telemetry_source(websocket: WebSocket):
    """Source connection from Unity for JSON Telemetry"""
    await websocket.accept()
    try:
        while True:
            data = await websocket.receive_text()
            # Broadcast to all dashboard clients asynchronously
            asyncio.create_task(manager.broadcast_telemetry(data))
    except WebSocketDisconnect:
        manager.latest_telemetry = None
        offline_msg = json.dumps({"RunState": "OFFLINE"})
        asyncio.create_task(manager.broadcast_telemetry(offline_msg))

@app.websocket("/ws/dashboard")
async def ws_telemetry_view(websocket: WebSocket):
    """Client connection for React dashboard JSON Telemetry"""
    await manager.connect_telemetry(websocket)
    try:
        while True:
            await websocket.receive_text() # Clients shouldn't send, but just in case keep alive
    except WebSocketDisconnect:
        manager.disconnect_telemetry(websocket)

# ==========================================
# VISUAL: 3D SIMULATION (BINARY)
# ==========================================

@app.websocket("/ws/simulation")
async def ws_simulation_source(websocket: WebSocket):
    """Source connection from Unity for Simulation frames"""
    await websocket.accept()
    try:
        while True:
            data = await websocket.receive_bytes()
            asyncio.create_task(manager.broadcast_simulation(data))
    except WebSocketDisconnect:
        manager.latest_simulation_frame = None

@app.websocket("/ws/simulation-view")
async def ws_simulation_view(websocket: WebSocket):
    """Client connection for React dashboard Simulation frames"""
    await manager.connect_simulation(websocket)
    try:
        while True:
            await websocket.receive_bytes()
    except WebSocketDisconnect:
        manager.disconnect_simulation(websocket)

# ==========================================
# VISUAL: SENSOR (BINARY)
# ==========================================

@app.websocket("/ws/sensor")
async def ws_sensor_source(websocket: WebSocket):
    """Source connection from Unity for Sensor frames"""
    await websocket.accept()
    try:
        while True:
            data = await websocket.receive_bytes()
            asyncio.create_task(manager.broadcast_sensor(data))
    except WebSocketDisconnect:
        manager.latest_sensor_frame = None

@app.websocket("/ws/sensor-view")
async def ws_sensor_view(websocket: WebSocket):
    """Client connection for React dashboard Sensor frames"""
    await manager.connect_sensor(websocket)
    try:
        while True:
            await websocket.receive_bytes()
    except WebSocketDisconnect:
        manager.disconnect_sensor(websocket)

# ==========================================
# COMMAND CHANNEL (JSON)
# ==========================================

@app.websocket("/ws/commands/unity")
async def ws_commands_unity(websocket: WebSocket):
    """Source connection from Unity for Command ACKs"""
    await websocket.accept()
    # Add to a dedicated unity command client list if needed,
    # or just broadcast ACKs back to frontend.
    manager.unity_command_client = websocket
    try:
        while True:
            data = await websocket.receive_text()
            print(f"[FASTAPI] Unity ACK received: {data}")
            # Forward ACK to frontend
            if hasattr(manager, 'frontend_command_clients'):
                for client in manager.frontend_command_clients:
                    await client.send_text(data)
    except WebSocketDisconnect:
        manager.unity_command_client = None

@app.websocket("/ws/commands/frontend")
async def ws_commands_frontend(websocket: WebSocket):
    """Client connection for React dashboard to send commands"""
    await websocket.accept()
    if not hasattr(manager, 'frontend_command_clients'):
        manager.frontend_command_clients = []
    manager.frontend_command_clients.append(websocket)
    try:
        while True:
            data = await websocket.receive_text()
            print(f"[FASTAPI] Frontend command received: {data}")
            # Route command to Unity
            if hasattr(manager, 'unity_command_client') and manager.unity_command_client is not None:
                print(f"[FASTAPI] Forwarding to Unity")
                await manager.unity_command_client.send_text(data)
            else:
                print(f"[FASTAPI] Unity not connected, rejecting")
                # Unity not connected, return REJECTED locally
                try:
                    payload = json.loads(data)
                    command_id = payload.get("commandId", "unknown")
                    await websocket.send_text(json.dumps({
                        "commandId": command_id,
                        "status": "REJECTED",
                        "message": "Unity command receiver is not connected."
                    }))
                except:
                    pass
    except WebSocketDisconnect:
        manager.frontend_command_clients.remove(websocket)
