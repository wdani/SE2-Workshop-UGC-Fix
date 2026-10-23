# SE2 Workshop UGC Fix v0.2.1 — Build fix

- Fixed `CS8604` in `tests/MetadataSafetyTests/Program.cs` by extracting a non-null `candidateRaw` with an explicit exception if the test premise is violated.
- Keeps `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and all existing C# safety tests.
- No modifications to Steam metadata detection, Steamworks actions, backups, user-consent requirements, or the existing GUI repair behavior.
- Windows build and real Steam runtime tests remain to be performed on a Windows PC.

## Build

Double-click `BUILD-EXE.cmd` in the extracted `se2-ugc-fix-gui` folder. On success, the release artifact is `Release/SE2-Workshop-UGC-Fix-v0.2.1-Windows-x64.zip`.
