export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errorCode?: string,
    public traceReference?: string
  ) {
    super(message);
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api/react${path}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (response.status === 401) {
    window.dispatchEvent(new CustomEvent('smartpos:unauthorized'));
  }

  if (!response.ok) {
    let body: any = null;
    try { body = await response.json(); } catch { /* no JSON body */ }
    throw new ApiError(
      response.status,
      body?.message ?? (response.status === 401 ? 'Invalid email or password.' : `Request failed (${response.status}).`),
      body?.errorCode,
      body?.traceReference
    );
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body)
    }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' })
};
