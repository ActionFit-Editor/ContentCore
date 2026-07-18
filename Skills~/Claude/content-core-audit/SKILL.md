---
name: content-core-audit
description: Audit ActionFit Content Core source and project adapters for opaque state ownership, two-slot revision and hash fallback, flush durability, reward-ledger reload, checked aggregation, idempotent grants, and atomic adapter expectations without reading or changing stored data. Use when reviewing persistence or reward package changes and integrations.
---

# Audit ActionFit Content Core

Keep the audit read-only and source-only. Never read, resolve, print, hash, copy, normalize, migrate, or change PlayerPrefs values, content payloads, reward ledgers, transaction IDs, balances, cloud records, or project storage. Do not start Unity or change repository, project, package, or release state.

1. Read repository instructions so project routing and safety rules apply before inspection.
2. From the repository root, capture `git status --short --untracked-files=all` as the audit baseline and preserve every pre-existing change.
3. Resolve `Packages/com.actionfit.content-core`; otherwise use `Library/PackageCache/com.actionfit.content-core@*` without editing it. Read `package.json`, `README.md`, and `AI_GUIDE.md`.
4. Use `rg` and read-only inspection to trace `IContentStateStore`, `IFlushableContentStateStore`, `PlayerPrefsContentStateStore`, `ContentReward`, `IContentRewardService`, `PlayerPrefsContentRewardService`, consuming-project adapters when present, asmdefs, and deterministic tests. Inspect definitions and call sites, never runtime values.
5. Verify and report source evidence for these contracts:
   - State stores handle opaque content-owned JSON, while content packages own DTO schema and migration. `Flush` is explicit and project adapters make requested critical transitions durable.
   - UTF-16 content IDs map to reversible hexadecimal key segments, so distinct IDs do not collide through lossy sanitization.
   - Each content ID owns two envelopes with schema, original content ID, positive monotonic revision, payload, and SHA-256 hash. Reads choose the highest valid revision, and saves replace a missing, invalid, or older slot while preserving the previous valid slot.
   - Hash or envelope failure never exposes corrupted payload, deletion removes both slots, revision overflow is rejected, and `Save`, `Delete`, and `Flush` persist before returning.
   - The reward service reloads its schema-versioned ledger for every public operation, rejects malformed existing data, validates stable IDs and positive amounts, sums duplicate rewards with checked arithmetic, and mutates only after validation succeeds.
   - `GrantOnce` returns true only for the first durable transaction, duplicate calls return false without balance mutation, and sequential service instances observe recorded grants.
   - Production adapters expose `IsAvailable` only when reward mutation and the transaction receipt are atomically durable; local PlayerPrefs defaults do not claim server authority, authentication, or multi-device synchronization.
6. Inspect `com.actionfit.content-core.Editor.Tests` coverage for newest valid revision selection, corrupted-newest fallback, both-slot deletion, duplicate transaction idempotency, malformed ledger rejection, checked aggregation, and balance accumulation. Verify tests use isolated keys and report missing evidence without executing them.
7. Capture the same Git status command again and compare it with the baseline. If state changed during the audit, report the paths and do not claim a no-change result.
8. Return findings grouped as passed contracts, risks, missing evidence, and recommended validation.
