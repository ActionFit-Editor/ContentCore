---
name: content-core-help
description: Explain ActionFit Content Core, its installed skills, opaque state-store and flush contracts, two-slot PlayerPrefs fallback, idempotent reward service, project adapter expectations, tests, and safety limits. Use when a user asks how content persistence or reward contracts work or which package skill applies.
---

# ActionFit Content Core Help

Answer in the user's language. Explain the package without running an audit or tests, reading PlayerPrefs, content payloads, ledgers, transaction IDs, balances, or project storage, or changing project or release state unless the user separately requests an authorized operation.

1. Read `PACKAGE_SKILLS.md` first. Treat its generated package identity, complete related-skill table, `$skill-name` invocations, descriptions, and access boundaries as authoritative.
2. Resolve `Packages/com.actionfit.content-core`; otherwise use `Library/PackageCache/com.actionfit.content-core@*` without editing it. Read `package.json`, `README.md`, and `AI_GUIDE.md` when present.
3. Explain opaque `IContentStateStore` ownership, optional critical `IFlushableContentStateStore`, reversible content-key encoding, two-slot revision and SHA-256 envelope fallback, `ContentReward`, `IContentRewardService.IsAvailable`, durable `HasGranted`/`GrantOnce` idempotency, and PlayerPrefs default limitations.
4. Keep content DTO schemas and migrations, gameplay state machines, project currencies, analytics, cloud synchronization, UI, Firebase, and server-authoritative transactions in content packages or consuming-project adapters.
5. Identify `com.actionfit.content-core.Editor.Tests` and the package `README` menu under `Tools > Package > Content Core`.
6. State that help and audit do not read or mutate PlayerPrefs, ledgers, balances, reward receipts, content payloads, project storage, scenes, prefabs, packages, repositories, tags, or catalogs.
