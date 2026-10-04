import { COMMAND_WS_URL } from './backendUrls';

export interface CommandPayload {
    [key: string]: any;
}

export interface CommandMessage {
    commandId: string;
    commandType: string;
    payload: CommandPayload;
    timestamp: number;
}

export interface CommandResponse {
    commandId: string;
    status: 'APPLIED' | 'REJECTED' | 'ERROR';
    message: string;
    stateRevision: number;
    downloadFileName?: string;
    downloadContent?: string;
}

// type CommandAckHandler = (response: CommandResponse) => void;

class CommandChannel {
    private ws: WebSocket | null = null;
    private isConnecting = false;
    private pendingCommands: Map<string, {
        resolve: (value: any) => void,
        reject: (reason?: any) => void,
        timer: any
    }> = new Map();
    
    private nextId = 1;

    public connect(url: string = COMMAND_WS_URL) {
        if (this.ws || this.isConnecting) return;
        this.isConnecting = true;

        this.ws = new WebSocket(url);

        this.ws.onopen = () => {
            this.isConnecting = false;
            console.log('[CommandChannel] Connected to backend');
        };

        this.ws.onmessage = (event) => {
            try {
                const response = JSON.parse(event.data) as CommandResponse;
                
                if (this.pendingCommands.has(response.commandId)) {
                    console.log(`[WEB] ACK received for ${response.commandId}: ${response.status}`);
                    const handler = this.pendingCommands.get(response.commandId)!;
                    clearTimeout(handler.timer);
                    this.pendingCommands.delete(response.commandId);
                    
                    if (response.status === 'APPLIED') {
                        handler.resolve(response);
                    } else {
                        handler.reject(new Error(response.message || response.status));
                    }
                }
            } catch (e) {
                console.error('Error parsing command response:', e);
            }
        };

        this.ws.onclose = () => {
            this.ws = null;
            this.isConnecting = false;
            setTimeout(() => this.connect(url), 2000);
            
            // Reject all pending commands
            for (const [, handler] of this.pendingCommands.entries()) {
                clearTimeout(handler.timer);
                handler.reject(new Error('WebSocket closed'));
            }
            this.pendingCommands.clear();
        };

        this.ws.onerror = (error) => {
            console.error('[CommandChannel] WebSocket error:', error);
        };
    }

    public getStatus(): boolean {
        return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
    }

    public async sendCommand(commandType: string, payload: CommandPayload): Promise<CommandResponse> {
        if (!this.getStatus()) {
            throw new Error('Command channel not connected');
        }

        const commandId = `cmd_${Date.now()}_${this.nextId++}`;
        
        const message: CommandMessage = {
            commandId,
            commandType,
            payload,
            timestamp: Date.now() / 1000.0 // seconds
        };

        return new Promise<CommandResponse>((resolve, reject) => {
            const timer = setTimeout(() => {
                this.pendingCommands.delete(commandId);
                reject(new Error('Command timeout (5s)'));
            }, 5000);

            this.pendingCommands.set(commandId, { resolve, reject, timer });
            console.log(`[WEB] command sent: ${commandType} (${commandId})`);
            this.ws!.send(JSON.stringify(message));
        });
    }
}

export const commandChannel = new CommandChannel();
