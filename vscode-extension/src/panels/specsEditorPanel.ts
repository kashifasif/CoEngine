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
      SpecsEditorPanel.currentPanel._panel.reveal(vscode.ViewColumn.One);
    } else {
      const panel = vscode.window.createWebviewPanel(
        'coengine.specsEditor',
        '⚡ CoEngine Studio',
        vscode.ViewColumn.One,
        {
          enableScripts: true,
          enableForms: true,
          retainContextWhenHidden: true,
          localResourceRoots: [extensionUri]
        }
      );

      panel.iconPath = vscode.Uri.joinPath(extensionUri, 'media', 'coengine-icon.svg');
      SpecsEditorPanel.currentPanel = new SpecsEditorPanel(panel, extensionUri, apiClient, copilotService);
    }
  }

  public dispose() {
    SpecsEditorPanel.currentPanel = undefined;
    this._panel.dispose();
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
    // Load local bundled Blazor Studio with target backend API and session token
    const iframeSrc = `http://127.0.0.1:5123/login?apiUrl=${encodeURIComponent(apiUrl)}&token=${token}`;

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
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
  </style>
</head>
<body>
  <iframe 
    id="coengineFrame"
    src="${iframeSrc}" 
    allow="clipboard-read; clipboard-write; microphone; camera"
  ></iframe>
</body>
</html>`;
  }
}
