import * as http from 'http';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { CopilotService } from './copilotService';

export class CopilotBridgeServer {
  private server?: http.Server;
  private readonly port = 5123;

  constructor(
    private readonly copilotService: CopilotService,
    private readonly extensionUri?: vscode.Uri
  ) {}

  public start(): Promise<void> {
    return new Promise((resolve) => {
      this.server = http.createServer(async (req, res) => {
        // Enable CORS
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

        if (req.method === 'OPTIONS') {
          res.writeHead(204);
          res.end();
          return;
        }

        const url = req.url || '';

        // Health check or models list
        if (req.method === 'GET' && (url === '/v1/models' || url === '/models')) {
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.end(JSON.stringify({
            object: 'list',
            data: [
              { id: 'gpt-4o', object: 'model' },
              { id: 'claude-3.5-sonnet', object: 'model' },
              { id: 'o1-mini', object: 'model' }
            ]
          }));
          return;
        }

        // Chat completions endpoint
        if (req.method === 'POST' && (url.includes('/chat/completions') || url.includes('/v1/chat/completions'))) {
          let body = '';
          req.on('data', (chunk) => { body += chunk; });
          req.on('end', async () => {
            try {
              const payload = JSON.parse(body || '{}');
              const messages: { role: string; content: string }[] = payload.messages || [];
              const isStream = payload.stream === true;
              const preferredFamily = payload.model || 'gpt-4o';

              const model = await this.copilotService.getModel(preferredFamily);
              if (!model) {
                res.writeHead(503, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({
                  error: {
                    message: 'GitHub Copilot Language Model is not available in VS Code.',
                    type: 'service_unavailable'
                  }
                }));
                return;
              }

              // Map messages to VS Code LanguageModelChatMessage
              const vsMessages: vscode.LanguageModelChatMessage[] = [];
              for (const m of messages) {
                if (m.role === 'user') {
                  vsMessages.push(vscode.LanguageModelChatMessage.User(m.content || ''));
                } else if (m.role === 'assistant') {
                  vsMessages.push(vscode.LanguageModelChatMessage.Assistant(m.content || ''));
                } else {
                  // System instructions
                  vsMessages.push(vscode.LanguageModelChatMessage.User(`[System Instructions]: ${m.content || ''}`));
                }
              }

              const tokenSource = new vscode.CancellationTokenSource();
              const response = await model.sendRequest(vsMessages, {}, tokenSource.token);

              if (isStream) {
                res.writeHead(200, {
                  'Content-Type': 'text/event-stream',
                  'Cache-Control': 'no-cache',
                  'Connection': 'keep-alive'
                });

                const id = 'chatcmpl-' + Date.now();
                for await (const chunk of response.text) {
                  const sseData = JSON.stringify({
                    id,
                    object: 'chat.completion.chunk',
                    created: Math.floor(Date.now() / 1000),
                    model: preferredFamily,
                    choices: [
                      {
                        delta: { content: chunk },
                        index: 0,
                        finish_reason: null
                      }
                    ]
                  });
                  res.write(`data: ${sseData}\n\n`);
                }

                res.write('data: [DONE]\n\n');
                res.end();
              } else {
                let fullText = '';
                for await (const chunk of response.text) {
                  fullText += chunk;
                }

                res.writeHead(200, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({
                  id: 'chatcmpl-' + Date.now(),
                  object: 'chat.completion',
                  created: Math.floor(Date.now() / 1000),
                  model: preferredFamily,
                  choices: [
                    {
                      index: 0,
                      message: {
                        role: 'assistant',
                        content: fullText
                      },
                      finish_reason: 'stop'
                    }
                  ]
                }));
              }
            } catch (err: any) {
              console.error('[CopilotBridge] Error processing request:', err);
              if (!res.headersSent) {
                res.writeHead(500, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ error: { message: err.message || 'Internal Bridge Error' } }));
              }
            }
          });
          return;
        }

        // Static file serving for bundled Blazor WASM
        if (req.method === 'GET' && this.extensionUri) {
          let blazorRoot = path.join(this.extensionUri.fsPath, 'dist', 'blazor', 'wwwroot');
          if (fs.existsSync(path.join(blazorRoot, 'wwwroot', 'index.html'))) {
            blazorRoot = path.join(blazorRoot, 'wwwroot');
          }
          if (fs.existsSync(blazorRoot)) {
            this.serveStatic(req, res, blazorRoot, url);
            return;
          }
        }

        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ error: { message: 'Not found' } }));
      });

      this.server.listen(this.port, '127.0.0.1', () => {
        console.log(`[CoEngine CopilotBridge] Listening on http://127.0.0.1:${this.port}`);
        resolve();
      });

      this.server.on('error', (err) => {
        console.warn(`[CoEngine CopilotBridge] Server error (may already be running):`, err.message);
        resolve();
      });
    });
  }

  private serveStatic(req: http.IncomingMessage, res: http.ServerResponse, rootDir: string, reqUrl: string) {
    let cleanPath = reqUrl.split('?')[0].split('#')[0];
    if (cleanPath === '/' || cleanPath === '') {
      cleanPath = '/index.html';
    }

    let filePath = path.join(rootDir, cleanPath);
    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      filePath = path.join(rootDir, 'index.html');
    }

    if (!fs.existsSync(filePath)) {
      res.writeHead(404, { 'Content-Type': 'text/plain' });
      res.end('Blazor Studio assets not found.');
      return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const mimeTypes: Record<string, string> = {
      '.html': 'text/html; charset=utf-8',
      '.js': 'application/javascript; charset=utf-8',
      '.mjs': 'application/javascript; charset=utf-8',
      '.css': 'text/css; charset=utf-8',
      '.json': 'application/json; charset=utf-8',
      '.wasm': 'application/wasm',
      '.dat': 'application/octet-stream',
      '.blat': 'application/octet-stream',
      '.dll': 'application/octet-stream',
      '.png': 'image/png',
      '.svg': 'image/svg+xml',
      '.ico': 'image/x-icon',
      '.woff': 'font/woff',
      '.woff2': 'font/woff2'
    };

    const contentType = mimeTypes[ext] || 'application/octet-stream';
    res.writeHead(200, {
      'Content-Type': contentType,
      'Cache-Control': 'no-store, no-cache, must-revalidate, max-age=0',
      'Pragma': 'no-cache',
      'Expires': '0',
      'Surrogate-Control': 'no-store'
    });

    const stream = fs.createReadStream(filePath);
    stream.pipe(res);
  }

  public stop() {
    if (this.server) {
      this.server.close();
      this.server = undefined;
    }
  }
}
