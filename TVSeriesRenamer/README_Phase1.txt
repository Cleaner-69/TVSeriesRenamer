TVSeriesRenamer Phase 1 - Auto-detect and auto-select

This ZIP contains a full replacement MainForm.cs built from the last-known-good file supplied in chat.

Functional behaviour:
- After Rename & Move completes, the app runs auto-detect again against the remaining loaded files.
- When a series is selected, either automatically or manually, files matching that selected series are selected in the Files list.
- The selected files are shown in Match Preview -> Original Files.
- New Names shows: Pending match - click Match Selected Files.
- The user still controls execution by clicking Match Selected Files, then Rename & Move.

Important compile fix:
- Your screenshot shows MainForm_old.cs inside the project.
- If MainForm_old.cs contains another MainForm class and is included in the project, Visual Studio will compile it and you will get duplicate definitions.
- Exclude MainForm_old.cs from the project, or move it outside the project folder, before building.

Replace only:
- TVSeriesRenamer\MainForm.cs

Do not replace:
- MainForm.Designer.cs
- Program.cs

Suggested commit message:
feat: auto-detect and select next series workflow
