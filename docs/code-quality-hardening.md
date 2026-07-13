# Code Quality Hardening Plan

Tracks the work from @daiplusplus's review on
[PR #285](https://github.com/yellowfeather/DbfDataReader/pull/285#issuecomment-4952711673):
enable nullable reference types, SDK-style multitargeting, exception
documentation, and full Roslyn/FxCop analysis for a library with downstream
consumers.

**Status legend:** ✅ done · 🚧 in progress · ⬜ not started

---

## Phase 0 — Infrastructure (✅ done)

Groundwork so warnings surface without blocking the build.

- [x] Add `Directory.Build.props` — analyzers repo-wide (`AnalysisMode=All`,
      `AnalysisLevel=latest`, `EnforceCodeStyleInBuild`); `Nullable=enable` and
      `GenerateDocumentationFile=true` scoped to the `DbfDataReader` library.
- [x] Add `.editorconfig` — formatting conventions; `CS1591` silenced (gradual
      doc effort); style diagnostics as `suggestion`; `CA1707` silenced under
      `test/**` (underscore test names are intentional).
- [x] Fix empty/incomplete exception classes for **CA1032** —
      `DbfFileFormatException`, `CdxException`, `SqlParseException` now expose the
      standard constructors.
- [x] Confirm baseline: solution builds with **0 errors**; ~442 warnings on the
      library (CA1032 cleared).

Already in place before the review (no action needed):
- [x] SDK-style `.csproj` with multitargeting (`net10.0;netstandard2.1`).

---

## Phase 1 — Nullable reference types (⬜ not started)

~305 nullable warnings on the library, mostly on the public API surface. Do this
before the analyzer pass — it collapses most of the CA1062 count. Work
file-group by file-group so each PR is reviewable.

Warning inventory (per-build, `net10.0`):

| Rule | Count | Meaning |
|------|-------|---------|
| CS8603 | 104 | Possible null reference return |
| CS8625 | 94 | Cannot convert null literal to non-nullable reference |
| CS8618 | 58 | Non-nullable field/property uninitialized on exit from ctor |
| CS8765 | 28 | Nullability of override/interface parameter mismatch |
| CS8604 | 20 | Possible null reference argument |
| CS8600 | 14 | Converting null literal or possible null to non-nullable |
| CS8601 | 6 | Possible null reference assignment |
| CS8602 | 2 | Dereference of a possibly null reference |

Suggested order:
- [ ] Core read path — `DbfTable`, `DbfRecord`, `DbfHeader`, `DbfColumn`, memo readers
- [ ] Value types — `DbfValue*`
- [ ] ADO.NET surface — `DbfDbConnection`, `DbfDbCommand`, `DbfDataReader`, parameter/connection-string types
- [ ] Query engine — `Query/*` (parser, planner, translator, evaluator, readers)
- [ ] CDX index — `Cdx/*`
- [ ] Remove now-redundant manual null-checks flagged as dead by CA1508

---

## Phase 2 — Analyzer (CA) cleanup (⬜ not started)

~137 analyzer warnings. Expect CA1062 to shrink dramatically after Phase 1.
Group by theme; decide keep-vs-suppress per rule in `.editorconfig`.

| Rule | Count | Theme | Likely action |
|------|-------|-------|---------------|
| CA1062 | 36 | Validate args non-null | Mostly resolved by nullable; `ThrowIfNull` the rest |
| CA1510/CA1512 | 20 | Use `ArgumentNullException.ThrowIfNull` / `ThrowIf*` | Mechanical fix |
| CA1307/CA1305 | 18 | Culture / `StringComparison` on string ops | Fix — correctness for locale-sensitive data |
| CA1859 | 14 | Use concrete types for perf | Fix where hot; judgement call |
| CA1051 | 8 | Do not declare visible instance fields | Review |
| CA2249 | 6 | Prefer `string.Contains` over `IndexOf` | Mechanical |
| CA2201 | 6 | Do not raise reserved exception types (`IndexOutOfRangeException`) | Fix |
| CA1010 | 6 | Collections should implement generic interface | Review |
| CA2100 | 2 | Review SQL string for injection | Review/annotate |
| Others | ~30 | CA1720, CA1065, CA1002, CA2227, CA1870, CA1865, CA1861, CA1819, CA1724, CA1721, CA1508, CA1028, CA1008 | Triage individually |

- [ ] Mechanical fixes (CA1510/CA1512/CA2249/CA1865)
- [ ] Correctness fixes (CA1307/CA1305/CA2201/CA2100)
- [ ] API/design review (CA1051/CA1002/CA1010/CA1819/CA2227/CA1724/CA1721)
- [ ] Perf fixes where they matter (CA1859/CA1861/CA1870)
- [ ] Record deliberate suppressions with justification in `.editorconfig`

---

## Phase 3 — Exception documentation (⬜ not started)

Recommendation #4: `/// <exception>` comments so consumers know what to catch.
`GenerateDocumentationFile` is already on; `CS1591` is currently silenced.

- [ ] Document thrown exceptions on all public entry points (constructors,
      `Read`/`ReadAsync`, `Open`, query execution, connection-string parsing)
- [ ] Add `<summary>` docs to the public API
- [ ] Re-enable `CS1591` once the public surface is documented

---

## Phase 4 — Enforce in CI (⬜ not started)

Lock the gains in so regressions can't creep back.

- [ ] Set `TreatWarningsAsErrors=true` for rules that are already clean
      (per-rule via `.editorconfig`, or globally once Phases 1–2 land)
- [ ] Confirm the CI build fails on new warnings

---

## Out of scope / optional

- **`readonly struct` refinement types** (recommendation #3) — large effort, the
  reviewer flagged it as tedious. Revisit as a separate initiative if desired,
  e.g. for field offsets / record positions / encoding identifiers.
