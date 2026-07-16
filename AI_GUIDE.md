# AI Guide - ActionFit Content Core

This file is shipped inside the UPM package so an AI assistant can preserve the package-neutral persistence and reward boundaries in any consuming project.

## Package Identity

- Package ID: `com.actionfit.content-core`
- Display name: ActionFit Content Core
- Repository: `https://github.com/ActionFit-Editor/ContentCore.git`
- Current package version at generation time: `0.2.1`
- Unity version: `6000.2`
- Runtime dependencies: none

## Purpose

ActionFit Content Core defines small synchronous contracts for content state persistence and idempotent reward grants. It includes PlayerPrefs defaults so a content package can run without project adapters while allowing production projects to replace persistence and reward mutation independently.

The package does not own content state DTOs, migrations, gameplay state machines, project currencies, analytics, cloud sync, UI, animation, Firebase, or server-authoritative transactions.

## Project Router Registration

This package should be listed in `Packages/com.actionfit.custompackagemanager/PACKAGE_AI_GUIDE_ROUTER.md`.

Requested router entry:

- `Packages/com.actionfit.content-core/AI_GUIDE.md` - Content Core owns package-neutral content state persistence and idempotent local reward contracts with PlayerPrefs defaults.

Read this file when:

- changing files under `Packages/com.actionfit.content-core/`
- implementing `IContentStateStore`, optional `IFlushableContentStateStore`, or `IContentRewardService`
- changing PlayerPrefs slot, envelope, revision, hash, or reward ledger behavior
- integrating a packaged content state machine with project persistence or rewards
- preparing a release for `com.actionfit.content-core`

## Runtime Contracts

- `IContentStateStore.TryLoad`, `Save`, and `Delete` operate on an opaque content-owned JSON string. The content package owns serialization versioning and migration.
- A project store may buffer ordinary `Save` calls. Implement `IFlushableContentStateStore` when a content engine must explicitly make critical transitions durable; `PlayerPrefsContentStateStore` implements it.
- `ContentReward` contains only a stable non-empty reward ID and a positive `long` amount.
- `IContentRewardService.IsAvailable` must be `true` only when the adapter can safely execute and recover grants. Content engines must check it before recording a pending transaction.
- `IContentRewardService.HasGranted` and `GrantOnce` use a stable non-empty transaction ID. `GrantOnce` returns `true` only for the first durable grant and `false` for an already recorded transaction.
- Project implementations must make state durable when `Flush` is requested and must atomically couple reward balance mutation with transaction completion.

## PlayerPrefs State Store

`PlayerPrefsContentStateStore` maps every UTF-16 content ID to a reversible hexadecimal key segment. Do not replace this with lossy sanitization.

Each content ID owns two slots. A valid envelope contains schema version, the original content ID, a positive monotonic revision, the payload, and a SHA-256 hash over those fields. Reads validate both slots and return the payload with the highest valid revision. Saves write the next revision into a missing, invalid, or older slot so the previous valid revision remains available. Deletes remove both slots. `Save`, `Delete`, and `Flush` call `PlayerPrefs.Save()` before returning.

Do not make hash failure fall back to the corrupted payload. Do not delete the older slot after a successful save. The two-slot design provides local torn/corrupt-write fallback, not encryption, authentication, cloud conflict resolution, or rollback protection against a malicious client.

## PlayerPrefs Reward Ledger

`PlayerPrefsContentRewardService` stores all granted transaction IDs and local reward balances in one schema-versioned JSON value. It reloads the ledger for each public operation so sequential service instances see one another's grants. Duplicate rewards in one request are summed before mutation, and all arithmetic is checked before the ledger is changed.

An existing malformed ledger must throw `InvalidOperationException`; treating it as empty can grant the same transaction twice. A duplicate transaction must return `false` without changing balances. The default service is a local functional implementation, not a secure production economy.

## Adapter Rules

- Keep gameplay and presentation assemblies dependent on these interfaces, not on `PlayerPrefs` directly.
- Production state stores should preserve content-owned versioned JSON or provide an explicit migration boundary.
- Buffer high-frequency state writes only when the engine can request `IFlushableContentStateStore.Flush` at critical boundaries.
- Production reward services must make reward mutation and transaction receipt durable as one idempotent operation.
- Return `IContentRewardService.IsAvailable == false` until that atomic reward contract is implemented; do not enter pending reward state first.
- Use globally stable content IDs, reward IDs, and transaction IDs. Do not use localized labels or session-only indices.
- Call the PlayerPrefs implementations from the Unity main thread.

## Testing

Run the EditMode assembly `com.actionfit.content-core.Editor.Tests`. Preserve coverage for newest valid revision selection, corrupted newest-slot fallback, deletion of both slots, duplicate transaction idempotency, and balance accumulation.

Do not use project `DataStore`, reward managers, Firebase, public endpoints, or shared production PlayerPrefs keys in package tests. Use a unique key prefix or ledger key per test and delete it during teardown.

## Package Tools Menu

- Unity menu root: `Tools/Package/Content Core/`.
- `README`: opens the installed package README.
- This package has no settings ScriptableObject and therefore exposes no `Setting SO` menu.

## Release Notes

- Publishing is manual through Custom Package Manager.
- Before reusing a version, check remote Git tags. Published tags are immutable.
- Update `package.json`, this guide's current version, README, tests, and `Editor/PackageInfo/ActionFitPackageInfo_SO.asset` together for behavior changes.
- The package repository should include this guide so consuming projects receive the same durability and idempotency rules.
