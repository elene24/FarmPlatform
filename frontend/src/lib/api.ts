import { config } from '../config'
import i18n from '../i18n'

/**
 * Thin fetch wrapper for the FarmPlatform API.
 *
 * Auth model: the session lives in an HttpOnly cookie set by the API, so JavaScript never sees it.
 * Every request is sent with credentials. State-changing requests carry a CSRF token in the
 * X-XSRF-TOKEN header; the token is fetched from /api/v1/auth/csrf, kept only in memory, and must be
 * refreshed after login/logout because it is bound to the signed-in user (call resetCsrfToken()).
 */

export interface ProblemDetails {
  status?: number
  title?: string
  code?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.title ?? `Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS'])
let csrfToken: string | null = null
let csrfRequest: Promise<string> | null = null

export function resetCsrfToken(): void {
  csrfToken = null
  csrfRequest = null
}

async function getCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken
  csrfRequest ??= rawFetch<{ token: string }>('GET', '/api/v1/auth/csrf').then((r) => {
    csrfToken = r.token
    return r.token
  })
  try {
    return await csrfRequest
  } finally {
    csrfRequest = null
  }
}

async function rawFetch<T>(method: string, path: string, body?: unknown, extraHeaders?: HeadersInit): Promise<T> {
  const headers = new Headers(extraHeaders)
  headers.set('Accept', 'application/json')
  headers.set('Accept-Language', i18n.language)
  if (body !== undefined && !(body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(`${config.apiBaseUrl}${path}`, {
      method,
      headers,
      credentials: 'include',
      body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, { code: 'network' })
  }

  if (!response.ok) {
    let problem: ProblemDetails = { status: response.status }
    try {
      problem = { ...problem, ...((await response.json()) as ProblemDetails) }
    } catch {
      // Non-JSON error body.
    }
    if (!problem.code) {
      problem.code =
        response.status === 429 ? 'too_many_requests' : response.status >= 500 ? 'server.error' : undefined
    }
    throw new ApiError(response.status, problem)
  }

  // 201/202/204 responses may have no body.
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  if (SAFE_METHODS.has(method)) return rawFetch<T>(method, path, body)

  const send = async () => rawFetch<T>(method, path, body, { 'X-XSRF-TOKEN': await getCsrfToken() })
  try {
    return await send()
  } catch (error) {
    // Token may be stale (e.g. session changed in another tab): refresh once and retry.
    if (error instanceof ApiError && error.problem.code === 'security.csrf_invalid') {
      resetCsrfToken()
      return send()
    }
    throw error
  }
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  delete: <T>(path: string) => request<T>('DELETE', path),
}
