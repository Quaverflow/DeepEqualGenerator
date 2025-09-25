# DeepEqual — Rewritten Tests (Unified Graph + Extension Methods)

This suite rewrites tests to:
- Use a **single, rich object graph** (`Order`) to exercise most scenarios.
- Prefer the **extension methods** (`AreDeepEqual`, `GetDeepDiff`, `ComputeDeepDelta`, `ApplyDeepDelta`).
- Add **missing coverage**: dynamic/expando, many collection shapes (queues, stacks, sets), primitives, Date/Time, etc.

## How to wire up

1. Ensure your solution includes the DeepEqual generator + runtime (your existing projects).
2. Add these tests to your test project (or use the provided `.csproj` and adjust the analyzer `<ProjectReference>` path).
3. Build once so the generator emits the typed `DeepOps` API and the instance extensions for the `[DeepComparable]` roots.

> If `DeepOpsExtensions.ApplyDeepDelta` is generated with `ref this` for reference types, apply the CS8337 patch you already have.