# Task 21 Repeatability, Read-Only, and Performance Validation

**Date:** 2026-09-03

**Commit under test:** `01a2d264c9948c0ab8e722a6ef6aefea2d3a5d13`

**Unity:** `6000.5.10f1`

**Unity Localization:** `1.5.12`

## Scope

This validation covers the current EditMode test suite after Task 20:

- Repeatability of the complete test outcome;
- Absence of source-tree modifications caused by testing;
- Baseline wall-clock and Unity-reported test duration.

The validation uses the same project, Unity version, test platform, and
batchmode command for all runs.

## Command

```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform editmode `
  -testResults Logs/task-21-repeat-N-results.xml `
  -logFile Logs/task-21-repeat-N.log
```

## Results

| Run | Total | Passed | Failed | Skipped | Unity duration | Wall-clock |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 169 | 168 | 0 | 1 | 25.37 s | 29.76 s |
| 2 | 169 | 168 | 0 | 1 | 25.27 s | 31.37 s |
| 3 | 169 | 168 | 0 | 1 | 25.25 s | 31.02 s |

The skipped test is the graphics-capable EditorWindow smoke test, which
requires a non-batchmode graphical Unity Editor:

`LocalizationAuditorWindowReportSmokeTests.GraphicsEditorWindowRendersLongReportAndDisabledLocateButton`

## Repeatability

Each result XML was normalized to the ordered set of test full names and
outcomes. All three runs produced the same signature:

`15a8adc6a7d820e48ae9e0ff03162644464f72e449de75b95093cdbd6d819c88`

Therefore the test membership and pass/fail/skip outcome were identical across
all three runs.

## Read-Only Check

After the three runs:

- `git status --short --branch` was clean;
- `git diff --check` reported no whitespace errors;
- The only generated project file, `ProjectSettings/SceneTemplateSettings.json`,
  was removed after validation;
- No tracked production or test source file was modified by the runs.

The existing tests independently verify serialized Scene, Prefab, String Table,
and TMP Font Asset bytes where those resources are exercised.

## Performance Baseline

The current full EditMode suite completes in approximately 25.25–25.37 seconds
inside Unity and 29.76–31.37 seconds wall-clock in this local environment.
This is a baseline measurement, not a pass/fail performance budget, because
 the product decision document does not define a numeric time limit.

The measurement includes Unity test setup and fixture lifecycle overhead. It is
not a benchmark of a single production-project scan.

## Conclusion

Task 21 validation passed for repeatability and test-run cleanliness. The
project has a reproducible EditMode baseline, but real-project scan
performance and a numeric acceptance threshold remain part of Task 22 and
future validation.
