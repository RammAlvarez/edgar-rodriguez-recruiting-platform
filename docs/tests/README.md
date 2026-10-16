# Test runs and defect register

## Runs

| Run | Slice | Result |
|---|---|---|
| [2026-10-16-01](2026-10-16-01-s0-foundation.md) | S0 Foundation | pass after fixing D-001, D-002 |

## Defects

| ID | Found in | Description | Status |
|---|---|---|---|
| D-001 | 2026-10-16-01 | After a successful sign-in the admin app stayed on *Entrar*: the session reported "signed-out" until `/v1/me` answered, so the guard bounced the user back. | fixed — the session goes to "loading" as soon as the provider reports a new user (`libs/ui/src/session.ts`) |
| D-002 | 2026-10-16-01 | Material Symbols showed as text (e.g. "da", "wo"): the theme set the icon font's variation settings but not its `font-family`. | fixed — full icon class in `libs/ui/styles/_platform-theme.scss` |
