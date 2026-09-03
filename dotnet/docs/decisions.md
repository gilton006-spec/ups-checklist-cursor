# Decisions

These records describe **what the code does now**, not a target architecture.

## ADR 1 — Modular monolith, two projects

Keep Core (rules + PDF) and Web (HTTP + SMTP). Extra projects, repositories, and a database would not match a no-login shift form with no server-side archive.

## ADR 2 — Shared password, not user accounts

Operators asked for a single site password. It is stored in hosting secrets, compared in lowercase, and issued as a 12-hour cookie. It does not identify a person and is not UPS SSO.

## ADR 3 — Tab sessionStorage drafts

Approved for this prototype: survive refresh, die when the tab closes. No server retention. Photos may be dropped under quota.

## ADR 4 — Best-effort duplicate email window

In-memory SHA-256 of the payload, ~45 seconds, single process. Released if send fails. Not reliable across Fly machines or restarts. A durable store would need explicit approval.

## ADR 5 — Forwarded headers only on Fly

`FLY_APP_NAME` enables trusting `X-Forwarded-*` because Fly is the TLS terminator. Local and test hosts do not trust arbitrary proxies.

## ADR 6 — No Playwright in CI yet

Critical journeys are covered by HTTP integration tests, Core validators, and Node helper tests. A browser suite would add a new runtime and CI image; it is not wired until that cost is accepted.

## ADR 7 — SMTP behind an interface; recipient is server-configured

`IHandoverEmailSender` is implemented with MailKit in Web. Tests use a fake. Clients cannot pass a destination address. Transport success is reported as accepted, never as confirmed delivery.

## ADR 8 — Antiforgery on all report POSTs

Checklist download/email now match scanners: browser-issued token plus cookie. That is request-origin protection. It is not a substitute for the site password.

## ADR 9 — Static workbook files stay public

Login wraps HTML and APIs. CSS, JS, and `/workbook/` diagrams stay anonymous so the login page and cached assets work. Diagrams are operational artwork, not employee PII.
