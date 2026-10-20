# moba-backend

Backend for the MOBA (Unity project: `moba-test`). PostgreSQL database plus a C# API (ASP.NET Core, .NET 10), both run with Docker.

**Status:** phase 1 (local setup). The database schema exists; account endpoints arrive in phase 2.

## Run it

Needs [Docker Desktop](https://www.docker.com/products/docker-desktop/), and it must be running.

```bash
docker compose up -d --build
```

That's all. Check it at http://localhost:8080/health, which should return `{"status":"ok","database":"ok"}`.

| Command | What it does |
|---|---|
| `docker compose up -d --build` | Start (or rebuild after code changes) |
| `docker compose logs -f api` | Follow the API's log |
| `docker compose down` | Stop (the data is kept) |
| `docker compose down -v` | Stop **and delete all data** |

On the very first start the API log shows one `fail: ... __EFMigrationsHistory` line. That's EF Core checking for its history table before creating it, and it's harmless.

## Database

PostgreSQL 17 on `localhost:5432`, database `moba`, user `moba`. Connect with any client (DBeaver, pgAdmin, DataGrip) to browse the tables.

| Table | Holds |
|---|---|
| `accounts` | Email, username (both unique, case-insensitive), password hash |
| `sessions` | Logged-in devices (only token hashes are stored) |
| `player_profiles` | Level, XP, MMR, and settings (keybinds, HUD, loadouts) as JSON |
| `matches` | Mode, map layout, start/end time, winner |
| `match_players` | Each hero in a match: team, hero, K/D, CS (bots have no account) |
| `friendships` | Friend requests, friends, blocks |

The schema is defined in code (`src/Moba.Api/Data`) and applied automatically when the API starts.

### Changing the schema

1. Edit the classes in `src/Moba.Api/Data/Entities.cs` / `MobaDbContext.cs`.
2. Create a migration (the database must be running):
   ```bash
   dotnet tool restore
   dotnet ef migrations add DescribeTheChange --project src/Moba.Api --output-dir Data/Migrations
   ```
3. `docker compose up -d --build`: the API applies it on start.

## Passwords and secrets

- Locally, the database uses the password `local-dev-only`. That's fine because both ports only listen on `127.0.0.1`, so other machines can't reach them.
- **On any real server:** copy `.env.example` to `.env`, set a strong `DB_PASSWORD`, and never commit `.env` (git ignores it).
- The game client never connects to the database. It only talks to the API.

## Running the API without Docker (optional)

Start just the database with `docker compose up -d db`, then run the API from your IDE or with `dotnet run --project src/Moba.Api`. It uses the connection string in `appsettings.Development.json`.

## Roadmap

1. **Local setup** ✅
2. Accounts: register/login, Unity login screen, profile and settings sync
3. Game data: server-recorded match results, match history
4. Online hosting: VPS with the same Compose setup, HTTPS, nightly backups
5. Later: Discord login, friends, MMR and matchmaking
