import * as vscode from 'vscode';
import { ApiClient, ProjectDto, SpecDto } from '../services/apiClient';
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

    // Directly open the Blazor App in the center window whenever user clicks the side icon
    SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);
    setTimeout(() => {
      vscode.commands.executeCommand('workbench.action.closeSidebar');
    }, 150);

    webviewView.onDidChangeVisibility(() => {
      if (webviewView.visible) {
        SpecsEditorPanel.render(this.extensionUri, this.apiClient, this.copilotService);
        setTimeout(() => {
          vscode.commands.executeCommand('workbench.action.closeSidebar');
        }, 150);
      }
    });

    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [this.extensionUri]
    };

    webviewView.webview.html = this._getHtmlForWebview(webviewView.webview);

    webviewView.webview.onDidReceiveMessage(async (data) => {
      switch (data.type) {
        case 'loadProjects': {
          try {
            const projects = await this.apiClient.getProjects();
            webviewView.webview.postMessage({ type: 'projectsLoaded', projects });
          } catch (err: any) {
            webviewView.webview.postMessage({ type: 'error', message: err.message });
          }
          break;
        }

        case 'loadSpecs': {
          try {
            const specs = await this.apiClient.getProjectSpecs(data.projectId);
            webviewView.webview.postMessage({ type: 'specsLoaded', specs });
          } catch (err: any) {
            webviewView.webview.postMessage({ type: 'error', message: err.message });
          }
          break;
        }

        case 'askCopilot': {
          try {
            const { projectId, prompt } = data;
            const contextRes = await this.apiClient.getDevQaContext(projectId, prompt);

            const systemPrompt = contextRes.systemPrompt;
            const messages = [
              { role: 'system' as const, content: systemPrompt },
              { role: 'user' as const, content: prompt }
            ];

            await this.copilotService.streamChat(messages, (chunk) => {
              webviewView.webview.postMessage({ type: 'chatChunk', chunk });
            });

            webviewView.webview.postMessage({ type: 'chatDone' });
          } catch (err: any) {
            webviewView.webview.postMessage({ type: 'error', message: err.message });
          }
          break;
        }

        case 'createDraftSpec': {
          try {
            const { projectId, prompt } = data;
            // Structure using GitHub Copilot
            const systemPrompt = 
              `You are an expert technical product architect. Given a requirement or brainstorming thought, convert it into structured JSON.\n` +
              `Return strictly valid JSON with this schema:\n` +
              `{\n` +
              `  "title": "Clear Spec Title",\n` +
              `  "description": "Concise summary",\n` +
              `  "acceptanceCriteria": ["AC 1", "AC 2"],\n` +
              `  "scopeTags": ["api", "security"]\n` +
              `}`;

            const rawJson = await this.copilotService.sendPrompt(systemPrompt, prompt);
            
            // Clean markdown code fence if copilot wrapped in ```json
            const cleanedJson = rawJson.replace(/```json/g, '').replace(/```/g, '').trim();
            const parsed = JSON.parse(cleanedJson);

            const created = await this.apiClient.createSpec(
              projectId,
              parsed.title,
              parsed.description,
              parsed.acceptanceCriteria || [],
              parsed.scopeTags || []
            );

            webviewView.webview.postMessage({ type: 'specCreated', spec: created });
            vscode.window.showInformationMessage(`Spec "${created.title}" published to CoEngine!`);
          } catch (err: any) {
            webviewView.webview.postMessage({ type: 'error', message: err.message });
          }
          break;
        }

        case 'openSettings': {
          vscode.commands.executeCommand('workbench.action.openSettings', 'coengine');
          break;
        }

        case 'openEditor': {
          vscode.commands.executeCommand('coengine.openEditor');
          break;
        }
      }
    });
  }

  public refresh() {
    if (this._view) {
      this._view.webview.postMessage({ type: 'refresh' });
    }
  }

  private _getHtmlForWebview(webview: vscode.Webview): string {
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>CoEngine Specs</title>
  <style>
    :root {
      --primary: #6366f1;
      --primary-hover: #4f46e5;
    }
    body {
      padding: 12px;
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      color: var(--vscode-foreground);
      background-color: var(--vscode-sideBar-background);
      margin: 0;
      box-sizing: border-box;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 12px;
      padding-bottom: 8px;
      border-bottom: 1px solid var(--vscode-sideBarSectionHeader-border);
    }
    .title {
      font-weight: 600;
      font-size: 13px;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .badge {
      font-size: 10px;
      background: var(--primary);
      color: #fff;
      padding: 2px 6px;
      border-radius: 9999px;
      font-weight: 500;
    }
    select, input, textarea {
      width: 100%;
      box-sizing: border-box;
      padding: 6px 8px;
      margin-bottom: 8px;
      background-color: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border, #3c3c3c);
      border-radius: 4px;
      font-family: inherit;
      font-size: 12px;
    }
    button {
      width: 100%;
      padding: 6px 12px;
      border-radius: 4px;
      border: none;
      background-color: var(--vscode-button-background);
      color: var(--vscode-button-foreground);
      font-weight: 500;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      margin-bottom: 8px;
    }
    button:hover {
      background-color: var(--vscode-button-hoverBackground);
    }
    button.secondary {
      background-color: var(--vscode-button-secondaryBackground);
      color: var(--vscode-button-secondaryForeground);
    }
    button.secondary:hover {
      background-color: var(--vscode-button-secondaryHoverBackground);
    }
    .tabs {
      display: flex;
      gap: 4px;
      margin-bottom: 10px;
      border-bottom: 1px solid var(--vscode-sideBarSectionHeader-border);
    }
    .tab {
      padding: 6px 10px;
      cursor: pointer;
      font-size: 11px;
      font-weight: 500;
      border-bottom: 2px solid transparent;
      color: var(--vscode-descriptionForeground);
    }
    .tab.active {
      color: var(--vscode-foreground);
      border-bottom-color: var(--primary);
    }
    .card {
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-widget-border, #2d2d2d);
      border-radius: 6px;
      padding: 10px;
      margin-bottom: 8px;
    }
    .card-title {
      font-weight: 600;
      font-size: 12px;
      margin-bottom: 4px;
      display: flex;
      justify-content: space-between;
    }
    .card-desc {
      font-size: 11px;
      color: var(--vscode-descriptionForeground);
      margin-bottom: 6px;
      line-height: 1.4;
    }
    .tag {
      font-size: 9px;
      padding: 1px 5px;
      border-radius: 3px;
      background: var(--vscode-badge-background);
      color: var(--vscode-badge-foreground);
      display: inline-block;
      margin-right: 4px;
    }
    .ac-item {
      font-size: 11px;
      padding: 2px 0;
      color: var(--vscode-foreground);
    }
    .chat-box {
      max-height: 220px;
      overflow-y: auto;
      background: var(--vscode-editor-background);
      border: 1px solid var(--vscode-widget-border);
      border-radius: 6px;
      padding: 8px;
      font-size: 11px;
      line-height: 1.4;
      white-space: pre-wrap;
      margin-bottom: 8px;
      display: none;
    }
    .status {
      font-size: 11px;
      color: var(--vscode-descriptionForeground);
      margin-top: 4px;
    }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">⚡ CoEngine <span class="badge">Copilot</span></div>
    <a href="#" id="btnSettings" style="color:var(--vscode-textLink-foreground); font-size:11px; text-decoration:none;">Settings</a>
  </div>

  <button id="btnOpenEditor" style="background: linear-gradient(135deg, #6366f1, #8b5cf6); color:white; font-weight:600; padding:9px 12px; margin-bottom:12px; border-radius:6px; width:100%; border:none; cursor:pointer; font-size:12px; display:flex; align-items:center; justify-content:center; gap:6px;">
    ⤢ Open Full Studio in Center Window
  </button>

  <label style="font-size:11px; font-weight:500;">Project:</label>
  <select id="projectSelect">
    <option value="">Loading projects...</option>
  </select>

  <div class="tabs">
    <div class="tab active" data-tab="specsTab">Specifications</div>
    <div class="tab" data-tab="ideateTab">Ideate with Copilot</div>
  </div>

  <div id="specsTab">
    <button id="btnRefresh" class="secondary">🔄 Refresh Specs</button>
    <div id="specsList">
      <div class="status">Select a project to view specifications.</div>
    </div>
  </div>

  <div id="ideateTab" style="display:none;">
    <textarea id="promptInput" rows="3" placeholder="Describe requirement or ask question..."></textarea>
    <button id="btnAsk">💬 Ask Copilot (pgvector Grounded)</button>
    <button id="btnStructure" class="secondary">✨ Structure & Create Spec</button>
    <div id="chatBox" class="chat-box"></div>
  </div>

  <script>
    const vscode = acquireVsCodeApi();

    const projectSelect = document.getElementById('projectSelect');
    const specsList = document.getElementById('specsList');
    const promptInput = document.getElementById('promptInput');
    const chatBox = document.getElementById('chatBox');
    const btnAsk = document.getElementById('btnAsk');
    const btnStructure = document.getElementById('btnStructure');
    const btnOpenEditor = document.getElementById('btnOpenEditor');
    const btnRefresh = document.getElementById('btnRefresh');
    const btnSettings = document.getElementById('btnSettings');

    btnOpenEditor.addEventListener('click', () => {
      vscode.postMessage({ type: 'openEditor' });
    });

    // Tab switching
    document.querySelectorAll('.tab').forEach(tab => {
      tab.addEventListener('click', () => {
        document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
        tab.classList.add('active');
        const target = tab.dataset.tab;
        document.getElementById('specsTab').style.display = target === 'specsTab' ? 'block' : 'none';
        document.getElementById('ideateTab').style.display = target === 'ideateTab' ? 'block' : 'none';
      });
    });

    btnSettings.addEventListener('click', (e) => {
      e.preventDefault();
      vscode.postMessage({ type: 'openSettings' });
    });

    btnRefresh.addEventListener('click', () => {
      const projectId = projectSelect.value;
      if (projectId) {
        vscode.postMessage({ type: 'loadSpecs', projectId });
      } else {
        vscode.postMessage({ type: 'loadProjects' });
      }
    });

    projectSelect.addEventListener('change', () => {
      const projectId = projectSelect.value;
      if (projectId) {
        specsList.innerHTML = '<div class="status">Loading specifications...</div>';
        vscode.postMessage({ type: 'loadSpecs', projectId });
      }
    });

    btnAsk.addEventListener('click', () => {
      const prompt = promptInput.value.trim();
      const projectId = projectSelect.value;
      if (!projectId) {
        alert('Please select a project first.');
        return;
      }
      if (!prompt) return;

      chatBox.style.display = 'block';
      chatBox.innerText = 'Analyzing with GitHub Copilot and pgvector...\\n';
      vscode.postMessage({ type: 'askCopilot', projectId, prompt });
    });

    btnStructure.addEventListener('click', () => {
      const prompt = promptInput.value.trim();
      const projectId = projectSelect.value;
      if (!projectId) {
        alert('Please select a project first.');
        return;
      }
      if (!prompt) return;

      chatBox.style.display = 'block';
      chatBox.innerText = 'Converting brainstorm into structured spec with Copilot...\\n';
      vscode.postMessage({ type: 'createDraftSpec', projectId, prompt });
    });

    // Handle messages from extension
    window.addEventListener('message', (event) => {
      const msg = event.data;
      switch (msg.type) {
        case 'projectsLoaded': {
          const projects = msg.projects || [];
          if (projects.length === 0) {
            projectSelect.innerHTML = '<option value="">No projects found</option>';
            return;
          }
          projectSelect.innerHTML = projects.map(p => 
            \`<option value="\${p.Id || p.id}">\${p.Name || p.name}</option>\`
          ).join('');

          // Trigger specs load for first project
          const firstId = projects[0].Id || projects[0].id;
          vscode.postMessage({ type: 'loadSpecs', projectId: firstId });
          break;
        }

        case 'specsLoaded': {
          const specs = msg.specs || [];
          if (specs.length === 0) {
            specsList.innerHTML = '<div class="status">No specifications found in this project.</div>';
            return;
          }
          specsList.innerHTML = specs.map(s => {
            const tags = (s.scopeTags || []).map(t => \`<span class="tag">\${t.tagName}</span>\`).join('');
            const acs = (s.acceptanceCriteria || []).map(a => \`<div class="ac-item">• \${a.text}</div>\`).join('');
            return \`
              <div class="card" style="cursor:pointer;" onclick="vscode.postMessage({ type: 'openEditor' })" title="Click to open in center window">
                <div class="card-title">
                  <span>\${s.title}</span>
                  <span style="font-size:10px; color:var(--primary)">v\${s.versionNumber || 1}</span>
                </div>
                <div class="card-desc">\${s.description || 'No description'}</div>
                <div style="margin-bottom:6px;">\${tags}</div>
                \${acs ? \`<div style="border-top:1px dashed var(--vscode-widget-border); padding-top:4px;">\${acs}</div>\` : ''}
              </div>
            \`;
          }).join('');
          break;
        }

        case 'chatChunk': {
          chatBox.innerText += msg.chunk;
          chatBox.scrollTop = chatBox.scrollHeight;
          break;
        }

        case 'chatDone': {
          chatBox.innerText += '\\n\\n[Complete]';
          break;
        }

        case 'specCreated': {
          chatBox.innerText = \`✅ Spec "\${msg.spec.title}" created successfully!\\nRefreshing specs list...\`;
          const projectId = projectSelect.value;
          if (projectId) {
            vscode.postMessage({ type: 'loadSpecs', projectId });
          }
          break;
        }

        case 'refresh': {
          vscode.postMessage({ type: 'loadProjects' });
          break;
        }

        case 'error': {
          alert('CoEngine Error: ' + msg.message);
          break;
        }
      }
    });

    // Initial load
    vscode.postMessage({ type: 'loadProjects' });
  </script>
</body>
</html>`;
  }
}
