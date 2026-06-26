# TV Series Renamer v1.4

## Change summary
- Added configurable output naming format: S01E01, 1x01, or Custom.
- Added persistent naming settings in appsettings.json.
- Added custom naming pattern support using tokens: {series}, {season}, {season00}, {episode}, {episode00}, {code}, {title}.
- Added support for detecting source episode codes in both S01E01 and 1x01 style.
- Updated application version display from v1.3 to v1.4.

## Default behaviour
- Default format remains S01E01 to preserve existing behaviour.
- Custom default pattern is: {series} - {code} - {title}

## Suggested v1.3 booking commands
git status
git add .
git commit -m "chore(release): book current build as v1.3"
git tag -a v1.3 -m "Release v1.3"
git push origin main
git push origin v1.3

## Suggested v1.4 commit commands
git status
git add MainForm.cs MainForm.Designer.cs Program.cs
git commit -m "feat(naming): add configurable episode naming formats"
git tag -a v1.4 -m "Release v1.4"
git push origin main
git push origin v1.4
