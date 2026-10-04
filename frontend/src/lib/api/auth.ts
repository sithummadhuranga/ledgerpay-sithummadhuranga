import { api } from './client'
import type { LoginRequest, LoginResponse, RegisterRequest, RegisterResponse, SessionInfo } from './types'

export const login = (body: LoginRequest) => api.post<LoginResponse>('/auth/login', body)

export const register = (body: RegisterRequest) => api.post<RegisterResponse>('/auth/register', body)

// The refresh token is in a cookie the page cannot read, so these calls carry no body and no bearer token.
export const logout = () => api.post<void>('/auth/logout', undefined, { anonymous: true })

export const listSessions = (signal?: AbortSignal) => api.get<SessionInfo[]>('/auth/sessions', { signal })

export const endSessionById = (id: string) => api.delete<void>(`/auth/sessions/${encodeURIComponent(id)}`)
