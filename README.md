# LedgerPay

A small digital wallet for Sri Lankan rupees. Customers top up, send money to each other and read their history, and every move is posted to a double-entry ledger that always balances.

## 1. Summary

LedgerPay is a .NET 10 API with a React app on SQL Server. It covers the whole of the Level 1 brief:

- Register and sign in. Registering opens a wallet with a random 12 digit number and a balance of 0.00.
- An operator tops up a wallet against a bank reference, and a bank reference can be used once.
- A customer sends money to another wallet by wallet number or mobile number, with an optional note of up to 140 characters. The sender pays a fee, shown before they confirm.
- Balance, and a paged history with date filters.
- Every money request carries an `Idempotency-Key`, so pressing confirm twice or retrying after a dropped connection moves the money once.
- An operator or an admin can freeze and unfreeze a wallet with a reason.

Beyond the brief it has refresh tokens with a sessions page, a back office for operators and admins (find a customer, read every transaction, an audit log, and restricting an operator account), rate limits, structured logs, a health check, and a Docker Compose file that starts the whole stack with one command.

Screens: landing page, sign in, register, wallet, send money, history, sessions, operator top-up, and the back office: an overview of what needs a look, customers, one customer (with freeze or unfreeze), every transaction, the audit log and the staff page (the last two for admins).

The data model is in [docs/erd.png](docs/erd.png) (source in [docs/erd.mmd](docs/erd.mmd)), and the API description is in [docs/openapi.json](docs/openapi.json).

## 2. Tech stack

| Part | Choice |
| --- | --- |
| API | .NET 10, ASP.NET Core controllers, EF Core 10 |
| Database | SQL Server 2022 (the brief asks for 2019 or newer) |
| Frontend | React 19, TypeScript 6 (strict), Vite 8, Tailwind CSS 4, shadcn/ui on Base UI |
| Data in the browser | TanStack Query, React Hook Form with zod, React Router 7 |
| Validation | FluentValidation on the API, zod in the browser |
| Tokens | JWT bearer (HS256) for access, a random token in a cookie for refresh |
| Logging | Serilog, JSON to the console |
| API docs | Microsoft.AspNetCore.OpenApi, Swagger UI |
| Tests | xUnit v3 with Testcontainers (a real SQL Server), Vitest with Testing Library |
| Packaging | Docker Compose, nginx in front of the built app |
| CI | GitHub Actions |

Exact versions are in `backend/Directory.Packages.props` and `frontend/package.json`.

## 3. Prerequisites

- Docker Desktop (or any Docker with Compose v2). Give it at least 4 GB of memory, because SQL Server will not start with less than 2 GB. The SQL Server image is built for x86-64, so on Apple Silicon turn on Rosetta in Docker Desktop.
- To run the tests or work outside Docker: the .NET 10 SDK (`backend/global.json` pins it) and Node 24 (`.nvmrc`) with npm.
- `openssl`, or any other way to make a random key, for the token signing key.

## 4. Configuration

Everything is read from a `.env` file at the root, which Git ignores.

```
cp .env.example .env
```

Fill in the seven values. Every password needs 10 to 128 characters with an upper case letter, a lower case letter, a digit and a symbol, and must not contain a single quote, a semicolon or a dollar sign, because they are written into SQL and connection strings.

| Variable | What it is |
| --- | --- |
| `MSSQL_SA_PASSWORD` | the `sa` account of the local SQL Server container. Only the container setup uses it |
| `DB_MIGRATOR_PASSWORD` | the login that owns the schema. The setup step uses it to create tables |
| `DB_API_PASSWORD` | the login the API connects with. It cannot change or delete ledger entries or audit logs |
| `JWT_SIGNING_KEY` | base64 of at least 32 random bytes, for example `openssl rand -base64 48` |
| `SEED_ADMIN_PASSWORD` | password of the seeded admin |
| `SEED_OPERATOR_PASSWORD` | password of the seeded operator |
| `SEED_CUSTOMER_PASSWORD` | password of the three seeded customers |

Nothing has a default. The API refuses to start without a signing key, and the seed step stops when a password is missing.

Other settings can be changed with environment variables if you need to: `Cors__AllowedOrigins__0` (the only origins allowed to call the API), `RateLimits__*`, `ForwardedHeaders__Enabled`, and `Sessions__CookieSecure`. The last one is `true` by default. Docker Compose sets it to `false` because the local stack is served over plain http, and it must stay `true` wherever the site is served over https.

The fee, the transfer limits and the wallet balance cap are not in code. They are rows of the `SystemSettings` table, seeded with the values from the brief, and a change applies to the next request.

## 5. Database setup

One command creates everything, including the database logins, the schema, the permissions and the seed data:

```
docker compose up -d --build --wait
```

Behind it are three steps that run once and can run again safely: `db-init` creates the two limited logins, `migrate` runs the DbTool `setup` command (EF Core migrations, then the permission script, then the seed), and the API starts after that.

Without Docker for the API, keep only the database in Compose and run the same DbTool by hand:

```
docker compose up -d db db-init
cd backend
dotnet run --project tools/LedgerPay.DbTool -- setup
```

with `ConnectionStrings__Migration`, `Seed__AdminPassword`, `Seed__OperatorPassword` and `Seed__CustomerPassword` set as environment variables or user secrets (the lines are in `.env.example`). `setup` is `migrate`, `permissions` and `seed` in that order, and each can be run alone. The seed is a DbTool command and not a SQL script because the passwords must be hashed by the same hasher the API uses, and they come from configuration and never from Git.

For anyone without the .NET SDK, [db/schema.sql](db/schema.sql) is the same schema as one idempotent script. Run it on an empty database with `sqlcmd -S <server> -d <database> -I -b -i db/schema.sql` (the `-I` flag matters). It does not contain the permissions or the seed data. It is generated from the migrations, and CI fails when the two differ.

`LedgerEntries` and `AuditLogs` reject `UPDATE` and `DELETE` with a trigger, and the API login has no right to do either.

## 6. How to run

With everything in Docker:

```
docker compose up -d --build --wait
```

| What | Where |
| --- | --- |
| The app | http://localhost:8080 |
| The API and Swagger | http://localhost:5100/swagger (also at http://localhost:8080/swagger) |
| Health check | http://localhost:5100/api/v1/health |
| SQL Server | `127.0.0.1,1433` |

The ports are bound to this machine only. `docker compose down -v` stops everything and removes the data.

For development, with the database in Docker and the API and the app on your machine:

```
dotnet run --project backend/src/LedgerPay.Api      # http://localhost:5100, needs ConnectionStrings__Api and Jwt__SigningKey
cd frontend && npm install && npm run dev            # http://localhost:5173, passes /api to port 5100
```

A first walk through, because the three customers start with a balance of 0.00:

1. Sign in as a customer (see the next section) and copy the wallet number from the wallet page.
2. Sign in as the operator and top up that wallet under Top up. Use any bank reference of 6 to 40 letters and digits.
3. Sign in as the customer again, open Send, and send money to another customer by wallet number or mobile number. The review step shows the amount, the fee and the total before anything moves.
4. Open History to read the statement with the balance after each line.

## 7. Seeded accounts

The brief asks for one user per back-office role and three customers. The passwords are the ones you put in `.env`, and they are not in this repository.

| Role | Name | Email | Mobile | Password from |
| --- | --- | --- | --- | --- |
| Admin | Chamara Rajapaksa | chamara.rajapaksa@example.com | +94719846203 | `SEED_ADMIN_PASSWORD` |
| Operator | Dilani Senanayake | dilani.senanayake@example.com | +94762751894 | `SEED_OPERATOR_PASSWORD` |
| Customer | Nimali Perera | nimali.perera@example.com | +94771284635 | `SEED_CUSTOMER_PASSWORD` |
| Customer | Kasun Jayawardena | kasun.jayawardena@example.com | +94712390581 | `SEED_CUSTOMER_PASSWORD` |
| Customer | Tharindu Fernando | tharindu.fernando@example.com | +94754106872 | `SEED_CUSTOMER_PASSWORD` |

What each role can do:

| | Customer | Operator | Admin |
| --- | --- | --- | --- |
| Wallet, send money, history | yes | no | no |
| See and end their own sessions | yes | yes | yes |
| Top up a wallet | no | yes | no |
| Freeze or unfreeze a wallet | no | yes | yes |
| See what needs a look, find customers, open one | no | yes | yes |
| Read every transaction, or find one by reference | own only | yes | yes |
| Read the audit log | no | no | yes |
| Restrict or release an operator account | no | no | yes |

Top-up is for operators only and freeze is for operators and admins, as the brief says. The rest of the back office is an addition: the brief does not ask for it and does not forbid it.

A restricted operator cannot sign in, every session of theirs ends at once, and the token they hold stops working on its next call. Only an operator can be restricted: not an admin, not a customer, and nobody can restrict themselves. A customer's wallet is stopped by freezing it.

## 8. API docs

Swagger UI is served by the API at `/swagger`. The OpenAPI document is at `/openapi/v1.json`, and a copy is committed as [docs/openapi.json](docs/openapi.json). An integration test writes that file when `UPDATE_OPENAPI=1` is set and fails when the committed copy no longer matches what the API serves, so it cannot go stale:

```
cd backend
UPDATE_OPENAPI=1 dotnet test --project tests/LedgerPay.IntegrationTests --filter-class "*OpenApiDocumentTests"
```

All routes are under `/api/v1`.

| Method | Path | Who |
| --- | --- | --- |
| POST | `/auth/register`, `/auth/login` | anyone |
| POST | `/auth/refresh`, `/auth/logout` | anyone with the refresh cookie |
| GET | `/auth/sessions` | any signed-in user |
| DELETE | `/auth/sessions/{id}` | any signed-in user, own sessions only |
| GET | `/wallets/me` | customer |
| GET | `/wallets/lookup?phone=` or `?walletNumber=` | customer |
| GET | `/wallets/me/transactions` | customer |
| GET | `/transfers/quote?amount=` | customer |
| POST | `/transfers` (`Idempotency-Key`) | customer |
| GET | `/transactions/{reference}` | owner, operator or admin |
| POST | `/admin/topups` (`Idempotency-Key`) | operator |
| PATCH | `/admin/wallets/{walletNumber}/status` | operator or admin |
| GET | `/admin/users?search=&status=` | operator or admin |
| GET | `/admin/users/{walletNumber}` | operator or admin (the look is audited) |
| GET | `/admin/transactions?type=&status=&walletNumber=&from=&to=` | operator or admin |
| GET | `/admin/audit-logs?action=&actor=&from=&to=` | admin |
| GET | `/admin/staff` | admin |
| PATCH | `/admin/staff/restriction` | admin |
| GET | `/health` | anyone |

The quote route is an addition to the brief. The server works out the fee, so the browser never does arithmetic on money and there is one copy of the rounding rule.

Every error is an RFC 7807 Problem Details body with a stable `code` (for example `INSUFFICIENT_FUNDS`, `WALLET_FROZEN`, `IDEMPOTENCY_KEY_REUSED`) and a `traceId` that matches the `X-Correlation-Id` header and the audit log. A customer who asks for someone else's transaction gets the same 404 as for a reference that does not exist.

## 9. How to run the tests

```
cd backend && dotnet test        # unit tests and integration tests, 1012 in all
cd frontend && npm test          # Vitest, 244 tests
```

The backend integration tests need Docker. They start one SQL Server container for the whole run with Testcontainers, apply the real migrations, triggers and view, and connect as the same limited login the API ships with. They do not use the EF Core in-memory provider. The first run is slow because the image has to start, and under Rosetta it is slower still.

The backend tests cover, among others: fee rounding below the minimum, in range and above the maximum; available balance; every transfer rule in order; balanced postings after a transfer; the same idempotency key moving money once, including ten requests at once; twenty parallel transfers from a wallet that can only afford some of them, where exactly that many succeed and the balance never goes negative; a failed transfer leaving no entries; a customer getting 404 for someone else's transaction; the append-only triggers; the database permissions; the lockout; and rotation and reuse of refresh tokens. The parallel tests that guard a lock were checked by taking the lock out and watching them fail.

The frontend tests cover, among others: the confirmation step showing the server's amount, fee and total; a protected route sending a visitor to sign in; the confirm button not firing twice; and a reload keeping the session.

Also in `frontend`: `npm run lint`, `npm run typecheck` and `npm run build`. CI runs all of this on every pull request, plus a check that `db/schema.sql` matches the migrations, a build of the Docker images, and `npm audit` at high.

## 10. Architecture

```
backend/
  src/
    LedgerPay.Domain/          rules and entities, no framework
    LedgerPay.Application/     services, request and response types, validators
    LedgerPay.Infrastructure/  EF Core, SQL scripts, password hashing, token creation, seeding
    LedgerPay.Api/             controllers, authorization policies, error handling, rate limits, logging
  tools/LedgerPay.DbTool/      migrate, permissions, seed
  tests/                       unit and integration tests
frontend/                      the React app, built into an nginx image
db/                            schema.sql and the script that creates the two SQL logins
docs/                          openapi.json and the ERD
```

References go one way: Domain knows nothing, Application knows Domain, Infrastructure implements what Application asks for, and Api wires them together. The fee, the transfer rules, the top-up rules and the ledger postings are plain functions over values in Domain, which is why their unit tests are short. A service in Application reads like the steps in the brief: validate, lock, check the rules, post, save, answer.

A request goes to a controller, which says who may call it and runs the validator. The controller calls a service, the service talks to the database, and it answers with a `ServiceResult` that holds a value or an error code. One place turns the code into a Problem Details response, from a single error catalogue.

The browser and the API share an origin: nginx serves the built app and passes `/api`, `/swagger` and `/openapi` to the API (Vite does the same in development). That is why the refresh cookie can be `SameSite=Strict`. CORS is still configured from an explicit list of origins, with no wildcard, and the API will not start with one.

In the app, one module makes every API call, attaches the token, and turns an error into a typed error with the code. One file maps each error code to a sentence for the user, and a test reads the backend's list of codes and fails when one has no sentence.

## 11. How the ledger and idempotency work

**Double entry.** Every transaction posts rows to `LedgerEntries` whose debits equal their credits, and entries are never changed or deleted.

- Top-up of X: debit the settlement float X, credit the customer's wallet account X.
- Transfer of X with fee F: debit the sender X + F, credit the receiver X, credit fee revenue F.

For example, sending LKR 5,000.00 costs the sender 5,025.00, because the fee is 0.5 percent, rounded half away from zero to 2 places and held between 10.00 and 250.00. The receiver gets 5,000.00 and 25.00 goes to fee revenue. Money is `decimal` in C# and `DECIMAL(18,2)` in SQL, never a float.

`Wallets.Balance` is a cached total that moves inside the same database transaction as the entries, so it always equals credits minus debits on the wallet's account. A wallet can never go below zero (a CHECK constraint backs the rule) or above LKR 2,000,000.00. The view `vw_WalletStatement` works out the running balance after each line with a window function, and the date filters are applied outside it, so the running balance is that of the whole wallet and not of the filtered rows.

**Rules.** A transfer is checked in a fixed order inside the locked section, so the answer is the same every time: recipient not found, sending to yourself, below the minimum, above the maximum, sender frozen, recipient frozen, balance does not cover the amount plus the fee, recipient over the cap. A top-up checks: wallet not found, wallet frozen, duplicate bank reference, balance cap.

**Failed attempts.** A request with a bad format is refused with 400 before any work. A business rule that refuses a transfer is recorded as a `Transactions` row with status `Failed` and a failure code, with an audit entry, and posts nothing. The sender sees it in their history with the reason.

**Concurrency.** One database transaction covers each money move. It locks both wallets with `UPDLOCK`, always in ascending wallet id order, so two transfers in opposite directions cannot deadlock. A `rowversion` column on wallets is a second guard. The execution strategy retries the whole transaction after a short database fault, and the code inside is safe to run twice.

**Idempotency.** A money `POST` needs an `Idempotency-Key` header (at most 100 characters from `A-Z a-z 0-9 _ -`). Inside the money transaction the service first inserts a row keyed by user, key and endpoint, with a hash of the request. Then:

- The same user, key, endpoint and payload again: the stored status and body come back, with an `Idempotent-Replayed: true` header, and nothing is posted a second time.
- The same key with a different payload: 409 `IDEMPOTENCY_KEY_REUSED`.
- Two identical requests at the same moment: the second waits on the unique index until the first commits, then replays its answer.
- A request refused by a business rule is stored under its key like a success is, so a retry gets the same refusal and a new attempt needs a new key. The browser makes a new key after the server refuses a request and keeps the same one after a lost connection.

The hash is taken from the validated request in a canonical form, so spacing and property order do not matter. This is a service and not HTTP middleware, because the answer has to be stored in the same transaction as the money.

## 12. Assumptions

- The brief was followed as written. The covering email described a different product, a news portal, and the PDF describes LedgerPay.
- Top-up is for operators only and an admin cannot do it, because the brief's endpoint table lists it for the operator alone.
- Freeze and unfreeze need a reason of 3 to 250 characters. The reason is for staff and is never shown to the customer. Setting a wallet to the state it is already in is a 409.
- Customers register themselves. The operator and the admin exist only through the seed.
- A wallet number is 12 random digits and not in order, so it cannot be guessed from another. A transaction reference is random and readable.
- Lookup shows the wallet number, a masked name (N*** P***) and whether the wallet is active. Nothing else about the holder.
- There is no minimum top-up beyond "above 0", because the brief sets none. A transfer is at least LKR 100.00.
- The history date filters are whole days in UTC, both ends included, and the screen says so. Times in the table are shown in the reader's time zone.
- A transfer refused by a rule appears in the sender's history as Failed. A refused top-up is the operator's attempt and does not appear in a customer's history.
- Emails are stored in lower case. Names may hold Unicode letters, spaces, hyphens and apostrophes.
- Amounts with more than two decimals are rejected, never rounded.

## 13. Security notes

- **Passwords** are hashed with the ASP.NET Core Identity hasher (PBKDF2). The policy (10 to 128 characters with upper, lower, digit and symbol) is enforced on the server, and the maximum length stops a long password from burning CPU.
- **Sign-in** locks an account for 15 minutes after 5 wrong passwords. The lock is checked before the password, so a locked account answers the same whether or not the password was right. An unknown email costs the same work as a known one. Register does say when an email is taken, so the lock adds no new leak.
- **Access tokens** are HS256 JWTs that last 15 minutes. The server checks issuer, audience, lifetime and signature, accepts only that algorithm, and allows 30 seconds of clock slack. The key comes from configuration and is at least 32 bytes. For an operator or an admin the API also reads the user row on each call, so a restricted account loses access at once and not when the token runs out.
- **Refresh tokens** are 256 random bits. Only their SHA-256 is stored. Every use replaces the token, and a replaced token that comes back after a 10 second grace ends the whole session. A session lasts 7 days from its last use and 30 days at most. The token travels in an `HttpOnly`, `SameSite=Strict` cookie that is only sent to `/api/v1/auth`, and `Secure` wherever the site uses https. Refresh, sign out and ending a session also refuse a request that began on another site. The access token is kept in the page's memory and never in browser storage.
- **Authorization** is a set of named policies in one file, mapped from the three roles, and every route says who may call it. The user id always comes from the token and never from the body, the route or a header. A customer cannot reach another wallet by changing a number or a reference.
- **Database**: the API connects as a limited login that is not `sa`. It has no `DELETE` anywhere, no `UPDATE` on the ledger or audit tables, and `UPDATE` on only five columns of the users table (the failed sign-in count, the lock and the restriction) and two columns of the refresh token table. A separate login owns the schema. All queries are parameterised.
- **Rate limits** count sign-in and register per address, lookup and money routes per user, refresh per address, and the back-office lists and searches per staff member (120 a minute). The counters are in memory, so they are not shared between API instances. Behind a proxy the API only trusts the forwarded address when that is switched on.
- **Audit log** records register, sign-in (and failures), lockout, refresh, reuse, sign-out, ending a session, top-up, transfer (and failures), freeze and unfreeze, restricting an operator and lifting it, and a staff member opening a customer's page, with the address and the correlation id. Money events are written in the same transaction as the money. No entry holds a password, a token or an email.
- **Customer data for staff**: the customer list shows an email and a mobile number cut down (n***@example.com), and the whole values are only on the page of one customer, which is written to the audit log once for a visit. Only admins can read the audit log.
- **Errors and logs**: one Problem Details shape, no stack traces. Logs are structured, carry the route pattern and not the path, and never hold passwords, tokens, request bodies, emails or phone numbers.
- **Headers**: responses under `/api` are `no-store`, with `nosniff`, `no-referrer` and `DENY` framing. nginx sends a content security policy for the app.
- **Secrets**: `.env` and user secrets only, never Git. `.env.example` has empty values. Seed passwords come from configuration.
- **The local Compose stack** is plain http on `localhost`, with `TrustServerCertificate=True` on the database connection and the refresh cookie not marked `Secure`. That is fine for a laptop and not for a host. A real deployment needs https, a real certificate and `Sessions__CookieSecure` left at `true`.

## 14. Limitations

- Idempotency keys are never deleted. There is no expiry job.
- Refresh token rows and their audit entries are kept for ever, and the API login cannot delete them.
- Ending a session does not cancel the access token it already issued, which keeps working for up to 15 minutes. After a sign out, another open tab shows the signed-in screen until its token runs out.
- If the server cannot be reached when someone signs out, the screen signs out but the cookie stays valid until it expires or the session is ended from the sessions page.
- A locked account keeps its existing sessions. Lockout applies to sign-in only.
- There is no cap on how many sessions one user can have, and the sessions page shows the newest 50.
- There is no password change or reset, no email verification and no two-factor sign-in.
- Restricting a customer's sign-in is not built. A frozen wallet stops the money, and that is the brief's way to stop a customer.
- Customer tokens are not checked for a restriction on each call. Only operator and admin tokens are, because only operators can be restricted.
- The list of staff is not paged. The customer search and the audit filter scan their tables, which is fine at this size and not for millions of rows.
- Fees, limits and the balance cap are changed by editing `SystemSettings`. There is no screen for it.
- No holds or approvals for large transfers, no reversals, no withdrawals, no CSV statement. The brief marks these as bonus.
- LKR only.
- The app is not deployed anywhere. It runs with Docker Compose. CI builds the Docker images, so a host can run them, but there is no hosted copy.

## 15. Time spent

About 30 focused hours, from Friday 2 October to Sunday 4 October 2026. The first commit is on the Friday evening and the history shows the rest.

That is about twice the 12 to 16 hours the brief suggests. The extra time went on things the brief calls bonus or does not ask for: refresh tokens and the sessions page, rate limits, structured logging, the Docker Compose stack and CI, the back office screens, a redesign of the app after the first version looked generic, and a tests-first approach to the money code, with checks that each important test fails when the code it guards is broken.
