import * as vscode from 'vscode';

export interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
}

export class CopilotService {
  /**
   * Retrieves the best available GitHub Copilot chat model.
   */
  public async getModel(preferredFamily?: string): Promise<vscode.LanguageModelChat | undefined> {
    const family = preferredFamily || 
      vscode.workspace.getConfiguration('coengine').get<string>('copilotFamily') || 
      'gpt-4o';

    try {
      // Try to find matching vendor 'copilot' and family
      const models = await vscode.lm.selectChatModels({
        vendor: 'copilot',
        family: family
      });

      if (models && models.length > 0) {
        return models[0];
      }

      // Fallback: any available copilot model
      const anyCopilot = await vscode.lm.selectChatModels({ vendor: 'copilot' });
      if (anyCopilot && anyCopilot.length > 0) {
        return anyCopilot[0];
      }

      // Fallback: any chat model registered in VS Code
      const anyModel = await vscode.lm.selectChatModels({});
      if (anyModel && anyModel.length > 0) {
        return anyModel[0];
      }
    } catch (err) {
      console.error('[CoEngine CopilotService] Error selecting model:', err);
    }

    return undefined;
  }

  /**
   * Streams chat completion chunks via GitHub Copilot language model API.
   */
  public async streamChat(
    messages: ChatMessage[],
    onChunk: (text: string) => void,
    cancellationToken?: vscode.CancellationToken
  ): Promise<string> {
    const model = await this.getModel();
    if (!model) {
      throw new Error(
        'GitHub Copilot Language Model is not available in VS Code. Please ensure GitHub Copilot is installed and active.'
      );
    }

    // Convert messages to VS Code LanguageModelChatMessage
    const vsMessages: vscode.LanguageModelChatMessage[] = [];

    for (const msg of messages) {
      if (msg.role === 'user') {
        vsMessages.push(vscode.LanguageModelChatMessage.User(msg.content));
      } else if (msg.role === 'assistant') {
        vsMessages.push(vscode.LanguageModelChatMessage.Assistant(msg.content));
      } else if (msg.role === 'system') {
        // In vscode.lm, system instructions are represented as User context or assistant guidance
        vsMessages.push(vscode.LanguageModelChatMessage.User(`[System Instructions]: ${msg.content}`));
      }
    }

    const token = cancellationToken || new vscode.CancellationTokenSource().token;
    const response = await model.sendRequest(vsMessages, {}, token);

    let fullText = '';
    for await (const fragment of response.text) {
      fullText += fragment;
      onChunk(fragment);
    }

    return fullText;
  }

  /**
   * One-shot prompt completion helper.
   */
  public async sendPrompt(
    systemPrompt: string,
    userPrompt: string,
    cancellationToken?: vscode.CancellationToken
  ): Promise<string> {
    const messages: ChatMessage[] = [
      { role: 'system', content: systemPrompt },
      { role: 'user', content: userPrompt }
    ];

    let accumulator = '';
    await this.streamChat(messages, (chunk) => {
      accumulator += chunk;
    }, cancellationToken);

    return accumulator;
  }
}
