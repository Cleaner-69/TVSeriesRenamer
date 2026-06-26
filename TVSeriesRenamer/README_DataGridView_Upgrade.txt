TVSeriesRenamer DataGridView Preview Upgrade

Files in this package:
- MainForm.cs: updated
- MainForm.Designer.cs: unchanged from supplied file
- Program.cs: unchanged from supplied file

Implemented:
- Added runtime DataGridView preview grid. No Designer file edits required.
- Existing preview ListBoxes are hidden at runtime and retained as rollback-safe controls.
- Grid columns: Status, Original File, New File, Message.
- Pending rows show output-folder guidance before matching.
- Matched rows show OK, OVERWRITE, SKIP, ERROR, and related messages.
- Existing TVDB, auto-detect, overwrite, undo, and rename logic is preserved.
- Added defensive source-equals-target and missing-source guards in ApplyRenameAndMove.
- Fixed overwrite preview message text.

Deployment:
1. Replace MainForm.cs with the file from this package.
2. MainForm.Designer.cs and Program.cs are included only for convenience and are unchanged.
3. Ensure files like MainForm_old.cs are excluded from the project before building.

Suggested commit:
feat: add DataGridView-based rename preview queue
