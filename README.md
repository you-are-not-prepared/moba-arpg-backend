# moba-backend

Backend for the MOBA (Unity project: `moba-test`). PostgreSQL database plus a C# API (ASP.NET Core, .NET 10), both run with Docker.

**Status:** phase 3 (game data). Accounts, settings sync, recorded matches (from the game client offline, from the game server online), match history and stats.

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

## API

JSON in and out. Errors always look like `{"error": "code", "message": "Text to show the player"}`.

| Endpoint | Auth | Does |
|---|---|---|
| `POST /auth/register` | – | `{email, username, password}` → `201 {token, expiresAt, profile}` |
| `POST /auth/login` | – | `{login, password}` (login = email **or** username) → `200 {token, expiresAt, profile}` |
| `POST /auth/logout` | ✔ | Ends this session → `204` |
| `GET /me` | ✔ | `{accountId, email, username, level, xp, mmr, settings}` |
| `PUT /me/settings` | ✔ | Replaces the settings with the JSON object in the body (max 16 KB) → `204` |
| `POST /matches` | ✔ | Start a match: `{mode, mapLayout, players: [{team, heroId, isBot, isLocal}]}` → `201 {matchId}` |
| `POST /matches/{id}/result` | ✔ | Finish it, once: `{winnerTeam, cheatsUsed, players: [{slot, level, kills, deaths, assists, creepScore, heroDamage, totalDamage, damageTaken, build}]}` → `204` |
| `GET /me/matches?limit=20` | ✔ | Finished matches, newest first, with every player's numbers (max 50) |
| `GET /me/matches/{id}` | ✔ | One match in full: also each player's damage (to heroes, total, taken) and final build |
| `GET /me/stats` | ✔ | Totals and per-hero matches/wins, without matches where cheats were used |
| `POST /me/join-ticket` | ✔ | `{ticket, expiresAt}`: proof of who you are, for joining a game server (valid 2 minutes) |
| `POST /server/join-tickets/redeem` | Server key | `{ticket}` → `{accountId, username}` |
| `POST /server/matches` | Server key | A finished game-server match: `{mode, mapLayout, durationSeconds, winnerTeam, cheatsUsed, players: [{team, heroId, isBot, accountId, level, kills, deaths, assists, creepScore, heroDamage, totalDamage, damageTaken, build}]}` → `201 {matchId}` |
| `GET /health` | – | `{status, database}` |

**Auth** means the header `Authorization: Bearer <token>`. **Server key** means the header `X-Game-Server-Key: <GAME_SERVER_KEY>`; only the game server has it.

**Rules**
- **Passwords:** 8–128 characters. They're stored only as PBKDF2 hashes (ASP.NET Core's `PasswordHasher`).
- **Usernames:** 3–24 letters, numbers and `_`. Usernames and emails are unique regardless of capitalisation.
- **Sessions:** last 30 days. The client keeps the token; the database stores only its SHA-256 hash.
- **Rate limit:** register and login allow 10 attempts per minute per IP address. After that the API answers `429`.
- **Wrong logins:** a wrong email, username or password all get the same answer, so nobody can find out which accounts exist.

Common error codes:
- `invalid_input`
- `email_taken`
- `username_taken`
- `invalid_credentials`
- `invalid_json`
- `too_large`
- `rate_limited`

### How matches are recorded

**Online (game server).** The game server runs the match, so it records it, and clients can't change the result:
1. A logged-in player gets a join ticket (`POST /me/join-ticket`) and hands it to the game server when joining. Their session token never leaves the game.
2. The game server redeems it (`/server/join-tickets/redeem`) to learn the account. Tickets are signed with a key that exists only in the running API, and they expire after 2 minutes.
3. When the match ends, the game server sends everything at once (`/server/matches`). Each account can play only one hero per match, and the same number checks apply.

Matches with no logged-in players aren't sent. Game-server matches have no `created_by`.

**Offline (PvE in the game client).** The client reports these matches itself. When it does, the server:
- creates the match itself, assigning its ID and start time;
- accepts exactly **one** result per match, and only from the account that started it;
- refuses results less than 15 seconds after the start, results that don't list every player, and impossible numbers;
- marks matches where the in-game cheat menu was used, which stay in the history but out of the stats.

A match that's left early never gets a result, so it never counts. This makes casual cheating hard, but a modified client could still lie about offline matches.

### Try it

```bash
curl -X POST localhost:8080/auth/register -H "Content-Type: application/json" \
  -d '{"email":"me@example.com","username":"Me","password":"something-long"}'
curl localhost:8080/me -H "Authorization: Bearer <token from the reply>"
```

### In the game

The Unity project's login screen (before the main menu) uses this API through `Scripts/Online/AccountService.cs`. While logged in, the options and keybinds are saved to the account, and logging in on another PC restores them. "Play offline" skips the login when the backend isn't running.

The game connects to `http://localhost:8080` by default. To use another server, set the PlayerPrefs key `moba.backend.url`. Plain `http://` only works in the Editor and in development builds; release builds need HTTPS (phase 4).

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
- The game-server key defaults to `local-dev-only-server-key` on both sides (this compose file and moba-test's), so recording works locally out of the box.
- **On any real server:** copy `.env.example` to `.env`, set a strong `DB_PASSWORD` and `GAME_SERVER_KEY` (the game server needs the same key), and never commit `.env` (git ignores it).
- The game client never connects to the database. It only talks to the API.

## Running the API without Docker (optional)

Start just the database with `docker compose up -d db`, then run the API from your IDE or with `dotnet run --project src/Moba.Api`. It uses the connection string in `appsettings.Development.json`.

## Roadmap

1. **Local setup** ✅
2. **Accounts** ✅ register/login, Unity login screen, profile and settings sync
3. **Game data** ✅ recorded match results, match history and stats
4. Online hosting: VPS with the same Compose setup, HTTPS, nightly backups
5. Later: Discord login, friends, MMR and matchmaking
