# TV Series Renamer v1.6 Test Build Notes

## Scope
This is a pre-release/test build for the `feature/rename-execution-safety` workstream. Do not publish as a GitHub release until the rename execution safety tests have passed.

## Changes included
- Bumped application version display and update metadata from v1.5 to v1.6.
- Added pre-flight validation before any rename/move operation starts.
- Added duplicate target detection within a batch.
- Moved overwrite confirmation to before the batch starts.
- Added rollback tracking for successful moves in a partially failed batch.
- Added overwrite backup/restore handling for targets that already exist.
- Added batch-level log markers for start, end, errors, rollback, and backup cleanup.

## Suggested commit message

git add TVSeriesRenamer/MainForm.cs TVSeriesRenamer/MainForm.Designer.cs TVSeriesRenamer/Program.cs TVSeriesRenamer/RELEASE_NOTES_v1.6.md
git commit -m "feat(rename): harden move execution with preflight validation and rollback"
git push --set-upstream origin feature/rename-execution-safety

## Test focus
- Happy path: several files rename/move successfully.
- Duplicate target: batch blocks before moving anything.
- Existing target: test overwrite all, skip all, and cancel.
- Missing source after preview: batch blocks or fails safely.
- Mid-batch failure: prompt offers rollback and restores successful moves.
- Undo after successful batch still restores moved files.
- Rename log contains BATCH START and BATCH END markers.
