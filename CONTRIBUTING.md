# Contributing

How we use branches and pull requests in this repo. For setting up and running the API, see the [README](README.md).

The game repo ([you-are-not-prepared/moba-arpg-game](https://github.com/you-are-not-prepared/moba-arpg-game)) follows the same rules.

---

## Branches

| Branch | What it is | How changes get in |
|---|---|---|
| `main` | Stable. Every merge is a release that the game's playtest builds use. | PR from `dev` (or a `hotfix/*` branch) |
| `dev` | Where finished work comes together. | PR from a work branch |
| work branches | One task each, short-lived (days, not weeks). | You push to them directly |

Never commit directly to `main` or `dev`.

## Naming work branches

`type/short-name`, lowercase, words separated by dashes. Add the issue number if there is one.

| Prefix | For | Example |
|---|---|---|
| `feature/` | New endpoints and features | `feature/friends-list` |
| `fix/` | Bug fixes | `fix/settings-sync-overwrite` |
| `chore/` | Upgrades, cleanup, tooling | `chore/upgrade-ef-core` |
| `hotfix/` | Urgent fix to `main` (maintainers only) | `hotfix/login-500` |

## Workflow

1. Start from an up-to-date `dev`.
2. Commit as often as you like and push your branch. If the task has an issue, start each commit message with its number: `#42 Add friends list endpoint`.
3. Before opening a PR, merge the latest `dev` into your branch.
4. Open a PR into `dev`. In the description, say what changed and how you tested it, and mention any new migration.
5. A maintainer reviews it. Once it's approved, it is **squash merged**: the whole branch becomes one commit on `dev`. The branch is then deleted.

### Releases

When `dev` is ready, a maintainer opens a PR from `dev` into `main` and merges it with a **merge commit**. The merge on `main` is tagged (`v0.4.0`, `v0.5.0`, …).

### Versioning

Tags follow `vMAJOR.MINOR.PATCH`:

- **Major:** big or breaking changes, such as API changes that older game versions can't handle. While the project is in early development it stays at `0`.
- **Minor:** a release from `dev` with new features (`v0.4.0` → `v0.5.0`).
- **Patch:** bug fixes only, usually a hotfix (`v0.5.0` → `v0.5.1`).

When you bump a number, the numbers after it reset to 0. The backend's version is independent of the game's.

### Hotfixes

Branch `hotfix/*` from `main`, open a PR into `main`, then merge `main` back into `dev` right away so the fix isn't lost.

## Database migrations

Two branches that each add a migration can conflict, because EF Core's model snapshot (`src/Moba.Api/Data/Migrations/MobaDbContextModelSnapshot.cs`) changes in both.

- **At most one migration per PR.** If you changed the model several times on your branch, remove your migrations and create one again before opening the PR.
- **If `dev` got a new migration while you worked:** merge `dev` into your branch, remove your migration (`dotnet ef migrations remove`), and create it again on top. See the README for the `migrations add` command.
- Never edit a migration that is already on `dev` or `main`. Add a new one instead.

## Changes that touch both repos

Use the same branch name in both repos, open one PR in each and link them to each other. The backend PR is merged first.
