# AOT size check

This tool measures the file size of the Native AOT publish output of
[`tests/SizeTrackingApp.AOT`](../../tests/SizeTrackingApp.AOT) and
compares it against the per-platform baselines committed in
[`tests/SizeTrackingApp.AOT/aot-size-baselines.json`](../../tests/SizeTrackingApp.AOT/aot-size-baselines.json).

The check is bidirectional — it fails on both **growth** and significant
**shrinkage** so the baseline window can be ratcheted tighter over time
as PolyType gets leaner.

## Running locally

```bash
# Publish + check for the current platform (uses Release).
make test-aot-size

# Publish + overwrite the current platform's baseline entry, then commit.
make update-aot-size-baseline
```

`update-aot-size-baseline` only touches the entry for the RID of the
machine you're on. To refresh every platform's baseline at once, see
the CI flow below.

## CI flow (updating every platform at once)

`.github/workflows/build.yml` runs `make test-aot-size` on every leg of
the matrix (`ubuntu-latest`, `windows-latest`, `macos-latest`, Release
configuration). Each leg prints a single block to its job log:

```
=== AOT-SIZE-RESULT-BEGIN ===
{
  "rid": "win-x64",
  "app_name": "SizeTrackingApp.AOT",
  "measured_path": "…/publish/SizeTrackingApp.AOT.exe",
  "size_bytes": 2543104,
  "expected_bytes": 2543104,
  "delta_bytes": 0,
  "tolerance_bytes": 524288,
  "verdict": "PASS"
}
=== AOT-SIZE-RESULT-END ===
```

`verdict` is one of `PASS`, `FAIL`, or `SKIP` (no baseline for this RID).

To refresh baselines for every platform after a CI run:

1. Open each matrix leg's "Check AOT size" step log and copy the
   `rid` and `size_bytes` values from the `AOT-SIZE-RESULT` block.
2. Update the corresponding `platforms.<rid>.size_bytes` entries in
   `tests/SizeTrackingApp.AOT/aot-size-baselines.json`.
3. Commit.

There are only three matrix legs, so a single agent or contributor can
do this in seconds. No CI artifacts or aggregator job are involved.

## Tolerance

Defined in `aot-size-baselines.json` itself:

```json
"tolerance_bytes": 524288,
"tolerance_percent": 3.0
```

The applied tolerance is `max(tolerance_bytes, tolerance_percent% of baseline)`,
applied in both directions.

## When the test fails

1. Inspect the failing PR for the source of the regression.
2. If it's intentional (e.g. a deliberate feature add), rerun CI to be
   sure the new size is reproducible, then update
   `aot-size-baselines.json` with the per-leg `size_bytes` values from
   the run.
3. If the change is a regression that should be fixed, address the
   underlying issue rather than updating the baseline.
