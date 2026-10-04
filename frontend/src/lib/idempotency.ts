// A new Idempotency-Key for one attempt to move money. The server answers the same key and the same details with
// the same result, so a retry after a lost connection cannot send the money twice. A key is made when the user
// reaches the confirmation, kept for retries, and replaced as soon as the details change or the money has moved.
export const newIdempotencyKey = () => crypto.randomUUID()
