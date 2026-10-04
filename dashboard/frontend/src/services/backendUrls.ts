export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:8000';

export const COMMAND_WS_URL =
  import.meta.env.VITE_COMMAND_WS_URL ||
  'ws://127.0.0.1:8000/ws/commands/frontend';

export function backendApiUrl(path: string): string {
  return `${API_BASE_URL.replace(/\/$/, '')}${path}`;
}

export function backendWebSocketUrl(path: string): string {
  const websocketBaseUrl = API_BASE_URL.replace(/^http/, 'ws').replace(/\/$/, '');
  return `${websocketBaseUrl}${path}`;
}
