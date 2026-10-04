import { api } from './client'
import type { TransferReceipt, TransferRequest } from './types'

export const sendMoney = (body: TransferRequest, idempotencyKey: string) =>
  api.post<TransferReceipt>('/transfers', body, { idempotencyKey })
