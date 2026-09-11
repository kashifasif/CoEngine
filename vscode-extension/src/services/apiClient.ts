import * as vscode from 'vscode';

export interface ProjectDto {
  id: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
}

export interface SpecDto {
  id: string;
  projectId: string;
  title: string;
  description: string;
  status: string;
  versionNumber: number;
  updatedAt: string;
  acceptanceCriteria: { id: string; text: string; isCompleted: boolean }[];
  scopeTags: { id: string; tagName: string }[];
}

export interface DevQaContextResponse {
  projectId: string;
  projectName: string;
  projectDescription: string;
  groundedContext: string;
  systemPrompt: string;
  vectorMatches: {
    title: string;
    content: string;
    similarityScore: number;
    docType: string;
  }[];
}

export class ApiClient {
  private get baseUrl(): string {
    const config = vscode.workspace.getConfiguration('coengine');
    return (config.get<string>('apiUrl') || 'http://localhost:5005').replace(/\/$/, '');
  }

  private get authHeader(): Record<string, string> {
    const config = vscode.workspace.getConfiguration('coengine');
    const token = config.get<string>('sessionToken') || '';
    if (token) {
      return {
        'Authorization': `Bearer ${token}`,
        'X-Session-Token': token
      };
    }
    return {};
  }

  private async fetchJson<T>(path: string, options: RequestInit = {}): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    const headers = {
      'Content-Type': 'application/json',
      ...this.authHeader,
      ...(options.headers as Record<string, string> || {})
    };

    const res = await fetch(url, {
      ...options,
      headers
    });

    if (!res.ok) {
      const errText = await res.text();
      throw new Error(`API Error [${res.status}]: ${errText || res.statusText}`);
    }

    return (await res.json()) as T;
  }

  public async getProjects(): Promise<ProjectDto[]> {
    return this.fetchJson<ProjectDto[]>('/api/projects');
  }

  public async getProjectSpecs(projectId: string): Promise<SpecDto[]> {
    return this.fetchJson<SpecDto[]>(`/api/projects/${projectId}/specs`);
  }

  public async getDevQaContext(projectId: string, userQuery: string): Promise<DevQaContextResponse> {
    return this.fetchJson<DevQaContextResponse>('/api/specs/query/context', {
      method: 'POST',
      body: JSON.stringify({
        projectId,
        roleMode: 'qa_mode',
        messages: [{ role: 'user', content: userQuery }]
      })
    });
  }

  public async createSpec(
    projectId: string,
    title: string,
    description: string,
    acceptanceCriteria: string[],
    scopeTags: string[]
  ): Promise<SpecDto> {
    return this.fetchJson<SpecDto>(`/api/projects/${projectId}/specs`, {
      method: 'POST',
      body: JSON.stringify({
        title,
        description,
        acceptanceCriteria,
        scopeTags
      })
    });
  }

  public async getCurrentUser(): Promise<any> {
    return this.fetchJson<any>('/api/auth/me');
  }
}
