# TV Series Renamer v1.5 Test Build Notes

## Scope
This is a pre-release/test build for the `feature/ui-improvements` workstream. Do not publish as a GitHub release until tester feedback has been reviewed and accepted.

## Changes included
- Bumped application version display and update metadata from v1.4 to v1.5.
- Added preview summary indicator with ready/warning/error counts.
- Improved preview grid row colouring for OK, pending, warning, overwrite, and error statuses.
- Added preview row tooltips so long statuses/messages are easier to inspect.
- Added double-click behaviour on matched preview rows to open the source file location in Windows Explorer.
- Added persistence for the last valid output folder in `appsettings.json`.
- Added auto-resize refresh for preview grid columns after matching.

## Branch plan
If you want the branch name aligned to the target release:

```bash
git branch -m feature/ui-improvements feature/v1.5-ui-improvements
git push --set-upstream origin feature/v1.5-ui-improvements
```

If you prefer to keep the current branch name, that is also acceptable while v1.5 remains unpublished.

## Suggested commit message

```bash
git add TVSeriesRenamer/MainForm.cs TVSeriesRenamer/MainForm.Designer.cs TVSeriesRenamer/Program.cs TVSeriesRenamer/RELEASE_NOTES_v1.5.md
git commit -m "feat(ui): improve preview visibility and persist output folder"
```

## Test focus
- Confirm app title and version label show v1.5.
- Confirm output folder is restored after restart when the folder still exists.
- Confirm preview summary updates after selected files are matched.
- Confirm row colours remain readable in light/dark Windows themes.
- Confirm double-clicking a matched preview row opens Windows Explorer at the relevant file.
- Confirm existing rename/move/undo behaviour is unchanged.
