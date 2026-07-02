# Banking API

ASP.NET Core Web API for credit card management and transaction processing with live FX conversion via the US Treasury Reporting Rates of Exchange API.

Built with **.NET 10**, **PostgreSQL** (Docker), and **Entity Framework Core**.

---

## Quick Start

```bash
git clone https://github.com/yspl72989/banking-api.git
cd banking-api
docker-compose up -d
cd BankingApi
dotnet run
```

- API: `http://localhost:5184` (or `https://localhost:7132`)
- Swagger: `/swagger`
- Tests: `dotnet test` (28 tests, no Docker required)

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker Desktop](https://www.docker.com/products/docker-desktop)

---

## Endpoints

| Req | Method | Path |
|---|---|---|
| 1 | `POST` | `/api/cards` |
| 2 | `POST` | `/api/cards/{cardId}/transactions` |
| 3 | `GET` | `/api/cards/{cardId}/transactions/{transactionId}?currency=AUD` |
| 4 | `GET` | `/api/cards/{cardId}/balance?currency=AUD` |

Currencies use **ISO 4217 codes** (`AUD`, `EUR`, `USD`). See Swagger for full request/response schemas.

---

## Key Assumptions

- Transactions stored in their original currency; balance/conversion uses FX
- No credit limit check on transaction creation (balance can go negative)
- Req 3 uses **historical** rates (on/before tx date, 6-month window); Req 4 uses **latest** rates
- FX fetched live per request (production would cache — rates change quarterly)
- Schema via `EnsureCreated()` on startup (production would use EF migrations)
