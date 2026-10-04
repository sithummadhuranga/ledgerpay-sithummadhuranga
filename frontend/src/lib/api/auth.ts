import { api } from './client'
import type { LoginRequest, LoginResponse, RegisterRequest, RegisterResponse } from './types'

export const login = (body: LoginRequest) => api.post<LoginResponse>('/auth/login', body)

export const register = (body: RegisterRequest) => api.post<RegisterResponse>('/auth/register', body)
