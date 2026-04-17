# Contributing

## Flow

1. Branch off `main`.
2. Small commits, Conventional Commits: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `ci:`.
3. Open PR → CI must be green.
4. Squash-merge.

## Pre-push

Run `/ship` (or manually):

```bash
dotnet format --verify-no-changes
dotnet test
dotnet list package --vulnerable
pwsh scripts/scan-secrets.ps1
```

## Major changes

- Bumping a major dep, changing auth, changing deploy topology → add a `DECISIONS.md` entry (ADR) in the same PR.

## Style

- .editorconfig is authoritative.
- No comments that restate the code. Explain WHY when non-obvious.
- No hand-written docstrings on trivial methods.
