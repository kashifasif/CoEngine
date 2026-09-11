import * as vscode from 'vscode';
import { ApiClient } from '../services/apiClient';
import { CopilotService } from '../services/copilotService';

export class SpecChatParticipant {
  constructor(
    private readonly apiClient: ApiClient,
    private readonly copilotService: CopilotService
  ) {}

  public register(context: vscode.ExtensionContext): vscode.Disposable {
    const handler: vscode.ChatRequestHandler = async (
      request: vscode.ChatRequest,
      chatContext: vscode.ChatContext,
      stream: vscode.ChatResponseStream,
      token: vscode.CancellationToken
    ) => {
      try {
        const command = request.command;
        const prompt = request.prompt.trim();

        // 1. Fetch available projects to determine context
        const projects = await this.apiClient.getProjects();
        if (!projects || projects.length === 0) {
          stream.markdown('⚠️ **No projects found in CoEngine.** Please create a project or check your API connection at `http://localhost:5005`.');
          return;
        }

        const activeProject = projects[0]; // Active or primary project

        // Handle @spec list
        if (command === 'list') {
          stream.markdown(`### 📋 Published Specifications for **${activeProject.name}**\n\n`);
          const specs = await this.apiClient.getProjectSpecs(activeProject.id);

          if (!specs || specs.length === 0) {
            stream.markdown('_No specifications created for this project yet._');
            return;
          }

          for (const spec of specs) {
            const statusEmoji = spec.status === 'published' ? '🟢' : '🟡';
            stream.markdown(`- ${statusEmoji} **${spec.title}** (v${spec.versionNumber || 1}) - _${spec.status}_\n`);
            if (spec.description) {
              stream.markdown(`  > ${spec.description}\n`);
            }
          }
          return;
        }

        // Handle @spec check (Validate selected code against acceptance criteria)
        if (command === 'check') {
          const editor = vscode.window.activeTextEditor;
          const selectedText = editor ? editor.document.getText(editor.selection) || editor.document.getText() : '';

          if (!selectedText.trim()) {
            stream.markdown('⚠️ **Please select or open code in an active editor tab to check against specifications.**');
            return;
          }

          stream.progress('Fetching matching project specifications and acceptance criteria...');

          const queryText = prompt || 'Check implementation criteria against published specifications';
          const contextRes = await this.apiClient.getDevQaContext(activeProject.id, queryText);

          stream.progress('Analyzing code with GitHub Copilot against published specs...');

          const systemPrompt = 
            `You are a strict QA & Software Architecture Validator for Project: '${contextRes.projectName}'.\n` +
            `Project Specifications & Acceptance Criteria:\n${contextRes.groundedContext}\n\n` +
            `Task: Review the provided code snippet against these acceptance criteria.\n` +
            `Output:\n` +
            `1. ✅ Met Criteria (with specific references)\n` +
            `2. ❌ Missing / Broken Criteria or Gaps\n` +
            `3. ⚠️ Edge cases or potential bugs\n` +
            `Be concise, actionable, and strictly grounded in the specs provided.`;

          const userPrompt = `Review this code against the project specifications:\n\`\`\`\n${selectedText.slice(0, 4000)}\n\`\`\``;

          const model = await this.copilotService.getModel();
          if (!model) {
            stream.markdown('❌ GitHub Copilot Language Model is not accessible. Please ensure Copilot is active.');
            return;
          }

          const messages = [
            vscode.LanguageModelChatMessage.User(`[System Instructions]: ${systemPrompt}`),
            vscode.LanguageModelChatMessage.User(userPrompt)
          ];

          const response = await model.sendRequest(messages, {}, token);
          for await (const chunk of response.text) {
            stream.markdown(chunk);
          }
          return;
        }

        // Default or @spec ask: Grounded Question-Answering via pgvector + Copilot
        const userQuery = prompt || 'Summarize the core requirements of this project.';

        stream.progress('Searching specifications in pgvector store...');
        const contextRes = await this.apiClient.getDevQaContext(activeProject.id, userQuery);

        if (contextRes.vectorMatches && contextRes.vectorMatches.length > 0) {
          stream.markdown(`> 🔍 *Grounded with **${contextRes.vectorMatches.length}** relevant vector matches from PostgreSQL*\n\n`);
        }

        stream.progress('Synthesizing answer with GitHub Copilot...');

        const model = await this.copilotService.getModel();
        if (!model) {
          stream.markdown('❌ GitHub Copilot Language Model is not accessible. Please ensure Copilot is active.');
          return;
        }

        const messages = [
          vscode.LanguageModelChatMessage.User(`[System Instructions]: ${contextRes.systemPrompt}`),
          vscode.LanguageModelChatMessage.User(userQuery)
        ];

        const response = await model.sendRequest(messages, {}, token);
        for await (const chunk of response.text) {
          stream.markdown(chunk);
        }

      } catch (err: any) {
        stream.markdown(`\n\n❌ **Error communicating with CoEngine API / Copilot:**\n\`\`\`\n${err.message || err}\n\`\`\``);
      }
    };

    const participant = vscode.chat.createChatParticipant('coengine.spec', handler);
    participant.iconPath = vscode.Uri.joinPath(context.extensionUri, 'media', 'coengine-icon.svg');
    return participant;
  }
}
