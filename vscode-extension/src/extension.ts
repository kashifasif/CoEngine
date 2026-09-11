import * as vscode from 'vscode';
import { ApiClient } from './services/apiClient';
import { CopilotService } from './services/copilotService';
import { SpecsViewProvider } from './providers/specsViewProvider';
import { SpecChatParticipant } from './providers/specChatParticipant';
import { SpecsEditorPanel } from './panels/specsEditorPanel';
import { CopilotBridgeServer } from './services/copilotBridge';

export function activate(context: vscode.ExtensionContext) {
  console.log('[CoEngine] Extension activating...');

  const apiClient = new ApiClient();
  const copilotService = new CopilotService();

  // Start local OpenAI-compatible bridge to GitHub Copilot (http://127.0.0.1:5123)
  const copilotBridge = new CopilotBridgeServer(copilotService, context.extensionUri);
  copilotBridge.start().catch((err) => console.error('[CoEngine Bridge] Start error:', err));
  context.subscriptions.push({ dispose: () => copilotBridge.stop() });

  // 1. Register Sidebar Webview View Provider
  const specsViewProvider = new SpecsViewProvider(context.extensionUri, apiClient, copilotService);
  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider(
      SpecsViewProvider.viewType,
      specsViewProvider
    )
  );

  // 2. Register Copilot Chat Participant (@spec)
  const chatParticipant = new SpecChatParticipant(apiClient, copilotService);
  context.subscriptions.push(chatParticipant.register(context));

  // 3. Register Commands
  context.subscriptions.push(
    vscode.commands.registerCommand('coengine.refresh', () => {
      specsViewProvider.refresh();
      vscode.window.showInformationMessage('CoEngine specifications refreshed.');
    }),

    vscode.commands.registerCommand('coengine.openSidebar', () => {
      vscode.commands.executeCommand('workbench.view.extension.coengine-sidebar');
    }),

    vscode.commands.registerCommand('coengine.openEditor', () => {
      SpecsEditorPanel.render(context.extensionUri, apiClient, copilotService);
    }),

    vscode.commands.registerCommand('coengine.configure', () => {
      vscode.commands.executeCommand('workbench.action.openSettings', 'coengine');
    })
  );

  // 4. Register Status Bar item for 1-click launch from the bottom bar
  const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  statusBarItem.command = 'coengine.openEditor';
  statusBarItem.text = '$(layers) CoEngine';
  statusBarItem.tooltip = 'Click to open CoEngine Studio';
  statusBarItem.show();
  context.subscriptions.push(statusBarItem);

  console.log('[CoEngine] Extension activated successfully.');
}

export function deactivate() {
  console.log('[CoEngine] Extension deactivated.');
}
