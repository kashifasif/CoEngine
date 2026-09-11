import * as vscode from 'vscode';
import { ApiClient } from '../services/apiClient';
import { CopilotService } from '../services/copilotService';
import { SpecsEditorPanel } from '../panels/specsEditorPanel';

export class SpecsViewProvider implements vscode.WebviewViewProvider {
  public static readonly viewType = 'coengine.specsView';
  private _view?: vscode.WebviewView;

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly apiClient: ApiClient,
    private readonly copilotService: CopilotService
  ) {}

  public resolveWebviewView(
    webviewView: vscode.WebviewView,
    context: vscode.WebviewViewResolveContext,
    _token: vscode.CancellationToken
  ) {
    this._view = webviewView;

    // 1. Immediately open the full Blazor Studio editor panel in the center window
    SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);

    // 2. Immediately close the sidebar so zero sidebar content is visible
    vscode.commands.executeCommand('workbench.action.closeSidebar');
    setTimeout(() => {
      vscode.commands.executeCommand('workbench.action.closeSidebar');
    }, 150);

    webviewView.onDidChangeVisibility(() => {
      if (webviewView.visible) {
        SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);
        vscode.commands.executeCommand('workbench.action.closeSidebar');
        setTimeout(() => {
          vscode.commands.executeCommand('workbench.action.closeSidebar');
        }, 150);
      }
    });

    webviewView.webview.options = {
      enableScripts: false,
      localResourceRoots: [this.extensionUri]
    };

    // Completely empty HTML body — no sidebar content whatsoever
    webviewView.webview.html = '<!DOCTYPE html><html><body style="margin:0;padding:0;background:transparent;"></body></html>';
  }

  public refresh() {
    SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);
  }
}
