import * as vscode from 'vscode';
import { ApiClient } from '../services/apiClient';
import { CopilotService } from '../services/copilotService';

export class SpecsEditorPanel {
  public static currentPanel: SpecsEditorPanel | undefined;
  private readonly _panel: vscode.WebviewPanel;
  private readonly _extensionUri: vscode.Uri;
  private _disposables: vscode.Disposable[] = [];

  private constructor(
    panel: vscode.WebviewPanel,
    extensionUri: vscode.Uri,
    private readonly apiClient: ApiClient,
    private readonly copilotService: CopilotService
  ) {
    this._panel = panel;
    this._extensionUri = extensionUri;

    this._panel.onDidDispose(() => this.dispose(), null, this._disposables);
    this._panel.webview.options = {
      enableScripts: true,
      enableForms: true,
      localResourceRoots: [this._extensionUri]
    };

    this._panel.webview.html = this._getWebviewContent();
  }

  public static render(
    extensionUri: vscode.Uri,
    apiClient: ApiClient,
    copilotService: CopilotService
  ) {
    if (SpecsEditorPanel.currentPanel) {
      // Force dispose old panel to destroy any stale webview iframe cache completely
      try {
        SpecsEditorPanel.currentPanel._panel.dispose();
      } catch {}
      SpecsEditorPanel.currentPanel = undefined;
    }

    const panel = vscode.window.createWebviewPanel(
      'coengine.specsEditor',
      '⚡ CoEngine Studio',
      vscode.ViewColumn.One,
      {
        enableScripts: true,
        enableForms: true,
        retainContextWhenHidden: false,
        localResourceRoots: [extensionUri]
      }
    );

    panel.iconPath = vscode.Uri.joinPath(extensionUri, 'media', 'coengine-icon.svg');
    SpecsEditorPanel.currentPanel = new SpecsEditorPanel(panel, extensionUri, apiClient, copilotService);
  }

  public reload() {
    this._panel.webview.html = this._getWebviewContent();
  }

  public dispose() {
    SpecsEditorPanel.currentPanel = undefined;
    try {
      this._panel.dispose();
    } catch {}
    while (this._disposables.length) {
      const x = this._disposables.pop();
      if (x) {
        x.dispose();
      }
    }
  }

  private _getWebviewContent(): string {
    const config = vscode.workspace.getConfiguration('coengine');
    const apiUrl = (config.get<string>('apiUrl') || 'http://localhost:5005').replace(/\/$/, '');
    const token = config.get<string>('sessionToken') || '01a04ed5-a013-78a6-ad60-6542fa3f3b41';
    const timestamp = Date.now();
    // Load local bundled Blazor Studio with target backend API and session token + cache-busting timestamp
    const iframeSrc = `http://127.0.0.1:5123/login?apiUrl=${encodeURIComponent(apiUrl)}&token=${token}&_t=${timestamp}`;

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Cache-Control" content="no-cache, no-store, must-revalidate" />
  <meta http-equiv="Pragma" content="no-cache" />
  <meta http-equiv="Expires" content="0" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>CoEngine Studio</title>
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: 100vw;
      height: 100vh;
      overflow: hidden;
      background-color: var(--vscode-editor-background);
    }
    iframe {
      width: 100%;
      height: 100%;
      border: none;
      display: block;
    }
    .coengine-reload-bar {
      position: fixed;
      bottom: 12px;
      right: 14px;
      z-index: 999999;
      opacity: 0.8;
      transition: opacity 0.2s;
    }
    .coengine-reload-bar:hover {
      opacity: 1;
    }
    .coengine-reload-btn {
      background: #003824;
      color: #ffffff;
      border: 1px solid rgba(255, 255, 255, 0.25);
      border-radius: 8px;
      padding: 5px 10px;
      font-size: 11px;
      font-weight: 500;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 5px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.3);
    }
    .coengine-reload-btn:hover {
      background: #004d32;
    }
  </style>
</head>
<body>
  <div class="coengine-reload-bar">
    <button class="coengine-reload-btn" onclick="forceReload()" title="Force reload latest files without cache">
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38l5.67-5.67"/>
      </svg>
      <span>Reload Studio</span>
    </button>
  </div>
  <iframe 
    id="coengineFrame"
    src="${iframeSrc}" 
    allow="clipboard-read; clipboard-write; microphone; camera"
  ></iframe>
  <script>
    function forceReload() {
      var frame = document.getElementById('coengineFrame');
      if (frame) {
        var base = frame.src.split('&_t=')[0];
        frame.src = base + '&_t=' + Date.now();
      }
    }
  </script>
</body>
</html>`;
  }
}

export class SpecsEditorSerializer implements vscode.WebviewPanelSerializer {
  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly apiClient: ApiClient,
    private readonly copilotService: CopilotService
  ) {}

  async deserializeWebviewPanel(webviewPanel: vscode.WebviewPanel, _state: unknown) {
    webviewPanel.dispose();
    SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);
  }
}
