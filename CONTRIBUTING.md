# Contributing

How we use issues, branches and pull requests in this repo. For setting up and running the API, see the [README](README.md).

The game repo ([you-are-not-prepared/moba-arpg-game](https://github.com/you-are-not-prepared/moba-arpg-game)) follows the same rules.

---

## Branches

| Branch | What it is | How changes get in |
|---|---|---|
| `main` | Stable. Every merge is a release that the game's playtest builds use. | PR from `dev` (or a `hotfix/*` branch) |
| `dev` | Where finished work comes together. | PR from a work branch |
| work branches | One task each, short-lived (days, not weeks). | You push to them directly |

Never commit directly to `main` or `dev`.

## Issues and the project board

All issues in the game, backend and ideas repos are tracked on the org project board **[moba-arpg](https://github.com/orgs/you-are-not-prepared/projects/1)**. Each issue has one status:

| Status | Meaning | Who moves it there |
|---|---|---|
| **Backlog** | Logged, but nobody has agreed to do it yet. | Automatic when the issue is added |
| **Ready** | Agreed on and clear enough to start. Nobody is on it yet. | A maintainer |
| **In progress** | Someone is working on it, including while its PR into `dev` is open. | You, when you assign yourself and start the branch |
| **Ready for QA** | Merged into `dev`, waiting to be tested in a build. | Whoever merges the PR |
| **Done** | Tested and released to `main`. | Automatic when the issue closes |
| **Cancelled** | Closed without being done: a duplicate, not planned or invalid. | A maintainer, closing it as "not planned" |

Use the **`blocked`** label (not a status) when an issue is waiting on something else. Say in a comment what it's waiting on, and remove the label once it can continue.

Issues don't close by themselves when a PR is merged into `dev`, because GitHub only closes them from PRs into `main`. They close at release instead (see [Releases](#releases)). If an issue fails QA, move it back to **In progress** and fix it on a new branch.

## Naming work branches

`type/<issue>-short-name`: lowercase, words separated by dashes, with the issue number right after the prefix.

| Prefix | For | Example |
|---|---|---|
| `feature/` | New endpoints and features | `feature/12-friends-list` |
| `fix/` | Bug fixes | `fix/42-settings-sync-overwrite` |
| `chore/` | Upgrades, cleanup, tooling | `chore/23-upgrade-ef-core` |
| `hotfix/` | Urgent fix to `main` (maintainers only) | `hotfix/60-login-500` |

## Workflow

1. Pick an issue in **Ready**, assign yourself, and move it to **In progress**. Start your branch from an up-to-date `dev`.
2. Commit as often as you like and push your branch. **Start every commit message with the issue number:** `#42 Add friends list endpoint`. Every task needs an issue, so if there isn't one yet, create it first.
3. Before opening a PR, merge the latest `dev` into your branch.
4. Open a PR into `dev`. **Start its title with the issue number too** (`#42 Add friends list endpoint`): the squash commit on `dev` takes its message from the title. In the description, say what changed and how you tested it, and mention any new migration. Don't write `Closes #42`: it does nothing in a PR into `dev`.
5. A maintainer reviews it. Once it's approved, it is **squash merged**: the whole branch becomes one commit on `dev`. The branch is then deleted, and the issue moves to **Ready for QA**.

### Releases

When `dev` is ready, a maintainer opens a PR from `dev` into `main` and merges it with a **merge commit**. The merge on `main` is tagged (`v0.4.0`, `v0.5.0`, …).

In the release PR's description, list each issue in **Ready for QA** that passed testing as `Closes #42`. Merging the release closes those issues, and the board moves them to **Done**. Issues that failed go back to **In progress** and wait for the next release.

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
