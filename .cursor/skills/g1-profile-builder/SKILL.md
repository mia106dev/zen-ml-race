---
name: g1-profile-builder
description: Builds and tunes ZenMLRace.Lightweight G1 profile JSON files from JRA data-analysis pages and race outputs. Use when creating or revising profile-2026-<raceKey>.json, adjusting weights/scores, or validating ranking reasons for races like takamatsu and haruten.
disable-model-invocation: true
---

# G1 Profile Builder

## Purpose

Create reproducible profile tuning for `ZenMLRace.Lightweight` using:

- JRA data-analysis page facts
- CLI output (`Top N Detailed Breakdown`)
- Existing profile conventions in this repository

## Scope

- Target files:
  - `Profiles/profile-2026-<raceKey>.json`
- Do not edit plan files.
- Prefer fact-based tuning over subjective narratives.

## Workflow

Use this checklist and update mentally while working:

```text
Profile Tuning Checklist
- [ ] Confirm raceKey and source page URL
- [ ] Freeze baseline (keep current profile + current CLI output as comparison)
- [ ] Extract factual tendencies (popularity, age, prep races, frame, etc.)
- [ ] Draft initial weights and score bands
- [ ] Create/update profile JSON file
- [ ] Apply only one minimal change set (single axis)
- [ ] Run tests/build and verify no regressions
- [ ] Review CLI reasons against baseline and refine if needed
```

## Tuning Rules

1. **Start from facts**
   - Convert page statements to scoring direction first (up/down), then magnitude.
2. **Limit one-step changes**
   - Change one axis at a time (weights or one score table) to keep causality visible.
   - Keep magnitude small (recommendation: weight delta <= `0.20`, band score delta <= `0.25` per step).
3. **Avoid overfitting**
   - Treat longshots as optional wins; prioritize stable top-group ranking.
4. **Prefer neutral defaults**
   - If evidence is weak, use neutral score (`0.0`) or neutral weight (`null` -> 1.0 behavior).
5. **Handle race-name variance**
   - Add common aliases (e.g., punctuation variants) for `preferredRaceNameScores`.
6. **Prioritize finish over popularity**
   - Treat popularity as a secondary signal.
   - If model behavior drifts, adjust scorer logic before over-tuning profile weights.
   - Preferred precedence in scorer behavior:
     - `preferredRaceNameScores` matched race > unmatched race
     - finish-position signal > popularity signal
7. **Preserve exploration**
   - Do not over-stabilize ranks.
   - Target "explainable fluctuation" instead of strict deterministic top-1 reproduction.

## Reproducibility Protocol (Required)

When tuning, always produce these artifacts mentally and in response text:

1. **Input facts set**
   - Explicitly list which JRA statements were used as evidence.
2. **Single change declaration**
   - State exactly one axis changed in this step (weights OR one scoring table OR scorer logic).
3. **Before/After comparison**
   - Compare against baseline CLI output:
     - score range
     - top group composition (e.g., top5 names)
     - per-reason contribution trend (`前走系評価`, `人気評価`)
4. **Rollback condition**
   - If top group quality worsens and explanation quality does not improve, revert that step.

## Scorer vs Profile Decision Rule

Choose the edit location by failure type:

- **Scorer change first** when:
  - precedence is wrong (`match > unmatch`, `finish > popularity`)
  - one signal class dominates globally across races
- **Profile change first** when:
  - race-specific evidence differs (prep race set, age trend, frame tendency)
  - only one raceKey is misaligned while others are stable

## Current Known Issues (Project Lessons)

- Score ranges can be too narrow, making rank separation weak.
- Favorite horses are often captured, but mid-range/longshot ordering is unstable.
- `preferredRaceNameScores` can become brittle if race-name coverage is sparse.
- Narrative wording may drift from implementation details (do not use "category" wording when scoring is race-name based).
- Lightweight model should prioritize stable top-group accuracy; longshot capture is a bonus.

## Required Review Checks After Tuning

After each profile revision, check these points from CLI output:

1. **Score spread check**
   - Confirm spread is not overly compressed.
   - If top-to-bottom spread is too small, increase discriminative bands (especially `lastFinishScores` / `lastPopularityScores`).
2. **Top group quality**
   - Verify winners/place horses are at least in upper group (not necessarily exact rank).
3. **Longshot handling sanity**
   - Longshots should not dominate top ranks without strong reason evidence.
4. **Reason-language consistency**
   - Ensure reporting text matches real implementation terms.
5. **Precedence consistency**
   - Verify output still reflects `preferredRaceName` match priority and `finish > popularity`.
6. **Suppression guard**
   - Confirm popularity is not zeroed out unnecessarily (secondary, but not dead signal).

## JSON Conventions

- Keep these sections:
  - `weights`: `popularityWeight`, `ageWeight`, `frameWeight`, `previousRaceWeight`
  - `scoring`: `frameScores`, `ageScores`, `preferredRaceNameScores`, `lastFinishScores`, `lastPopularityScores`
- Use explicit numeric decimals for readability (e.g., `1.25`, `0.35`).
- Keep profile keys stable; avoid ad-hoc new fields in Lightweight profiles.

## Validation Commands

Run after profile updates:

```bash
dotnet test "ZenMLRace.Lightweight.Tests/ZenMLRace.Lightweight.Tests.csproj"
dotnet build "ZenMLRace.Lightweight.Cli/ZenMLRace.Lightweight.Cli.csproj"
```

## Output Style for Recommendations

When presenting tuning suggestions:

- Show **what changed** (weights/tables)
- Show **why** (source fact mapping)
- Show **expected effect** (e.g., stronger favorite bias, reduced frame impact)
- Show **risk/trade-off** (e.g., weaker longshot sensitivity)

Template:

```markdown
- Change: `popularityWeight 1.10 -> 1.25`
- Reason: Source says top popularity buckets dominate.
- Expected effect: Raise high-confidence favorites; reduce upset exposure.
```

## Quick Race Heuristics (Initial Drafting)

- If source says popularity dominates:
  - Increase `popularityWeight` cautiously.
  - Never let popularity overwhelm finish-position signals.
- If source says older horses underperform:
  - Keep 4-5 high, 6 mild, 7+ negative in `ageScores`.
- If source says frame has weak signal:
  - Lower `frameWeight` and flatten `frameScores`.
- If source says specific prep races dominate:
  - Increase matching entries in `preferredRaceNameScores`.

## CLI Reporting Guidance

When improving CLI output for tuning work, prefer:

- **Two-layer view**
  - Core contenders (top group)
  - Watchlist (potential upset/edge candidates)
- **Structured reason totals**
  - Keep per-reason lines and show positive/negative subtotal.
- **Rank confidence cues**
  - Show score gap to next rank and to rank-1.
- **Stability signal**
  - Display score range/average and note when compression is high.

Recommended labels:

- `Core Picks` for stable top candidates
- `Watchlist` for non-core but noteworthy runners
- `Score Compression` when spread is narrow

