# Releasing DbfDataReader

A release is made by publishing a GitHub release. That creates a `vX.Y.Z` tag, and the
tag triggers the [Release workflow](.github/workflows/release.yml), which builds, tests,
packs and pushes the package to MyGet and NuGet. Nothing is published from a developer
machine.

## How the version is set

The package version comes from the git tag, via [MinVer](https://github.com/adamralph/minver)
(`MinVerTagPrefix` is `v`). There is no version number to edit in the repository.

- A commit tagged `v2.3.0` builds as `2.3.0`.
- Commits after a tag build as the next patch pre-release, e.g. `2.3.1-alpha.0.4`.

Choose the number with [semantic versioning](https://semver.org):

| Bump | When |
|---|---|
| Major | A breaking change to the public API or to behaviour callers rely on |
| Minor | New features that leave existing code working |
| Patch | Bug fixes only |

## Before releasing

1. Everything for the release is merged to `main`.
2. The [CI workflow](.github/workflows/ci.yml) is green on the latest `main` commit:

   ```
   gh run list --branch main --workflow CI --limit 1
   ```

3. The README describes any new features.
4. Review what is going out, to write the notes and settle the version number:

   ```
   git fetch --tags
   git log --oneline v2.2.0..origin/main      # use the previous release's tag
   ```

## Creating the release

1. Write the release notes to a file. Follow the previous releases
   (`gh release view v2.2.0`):

   - a one-line summary of the release;
   - a `##` section per feature, referencing its issue or pull request, with a short
     example where it helps;
   - a `## Compatibility notes` section for anything that could affect existing code,
     including changes too small to warrant a major version;
   - a closing line: `**Full changelog**: https://github.com/yellowfeather/DbfDataReader/compare/v2.2.0...v2.3.0`

2. Publish the release, which also creates the tag on `main`:

   ```
   gh release create v2.3.0 --target main --title v2.3.0 --notes-file notes.md
   ```

   The tag must be `v` followed by `major.minor.patch`. The same can be done from the
   GitHub web UI with **Draft a new release**.

3. Watch the Release workflow:

   ```
   gh run watch $(gh run list --workflow Release --limit 1 --json databaseId --jq '.[0].databaseId')
   ```

   It runs the test suite, then pushes the `.nupkg` to MyGet and to NuGet, and attaches
   the packages to the workflow run as an artifact. It needs the `MYGET_URL`,
   `MYGET_API_KEY` and `NUGET_API_KEY` repository secrets.

4. Check that the version is listed on
   [nuget.org/packages/DbfDataReader](https://www.nuget.org/packages/DbfDataReader).
   NuGet can take several minutes to validate and index a new version.

## After releasing

The benchmarks project measures the *published* package, so it is updated once the new
version is available on NuGet. In one pull request (see #305 for an example):

1. Set the default `DbfDataReaderVersion` in
   [`test/DbfDataReader.Benchmarks/DbfDataReader.Benchmarks.csproj`](test/DbfDataReader.Benchmarks/DbfDataReader.Benchmarks.csproj)
   to the new version.
2. Run the benchmarks and add a section for the version to [`benchmarks.md`](benchmarks.md).
   The [benchmarks README](test/DbfDataReader.Benchmarks/README.md) has the commands;
   check the `// DbfDataReader under benchmark:` line in the output to confirm which
   version was measured.

## If something goes wrong

- **The workflow fails before pushing** (build or tests): fix the problem on `main`,
  then delete the release and its tag and create them again on the fixed commit:

  ```
  gh release delete v2.3.0 --cleanup-tag
  ```

- **A package was pushed but is faulty**: a version on NuGet cannot be replaced.
  Unlist it on nuget.org and release a new patch version with the fix.
