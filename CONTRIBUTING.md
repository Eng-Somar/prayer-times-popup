# Contributing

Issues and pull requests are welcome in English or Arabic.

## Build and test

You need Windows 10 or 11 and nothing else. The scripts use the C# compiler included with Windows.

```powershell
powershell -ExecutionPolicy Bypass -File test.ps1
```

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

That compiler only supports **C# 5**, so `$"..."`, `?.`, `nameof` and expression-bodied members don't compile. The app has no third-party packages, and it should stay that way.

## Where things are

| Area | Files |
|---|---|
| Prayer times | `src/PrayCalc.cs`, `src/Hijri.cs`, `src/Geo.cs` |
| What to show and when (no UI, no state) | `src/Scheduler.cs` |
| Tray icon and windows | `src/TrayApp.cs`, `src/*Form.cs` |
| Shared drawing, themes and motion | `src/UiKit.cs`, `src/Themes.cs`, `src/Motion.cs` |
| English and Arabic text | `src/Lang.cs` |
| Tests | `tests/Tests.cs`, `tests/SolarReference.cs` |

## Changing prayer times

`tests/SolarReference.cs` is a second solar calculation that shares no code with the app. Keep it separate, because that is what makes it a useful check.

Don't change a method's angles or add fixed minutes without linking to an official timetable, and add dates from it to the tests.

## Adhkar text

Never edit `data/adhkar.tsv` by hand. Use `tools/build-adhkar-data.ps1`, and report mistakes in the source as an issue.

## License

Contributions are licensed under the GNU LGPL v3.0, like the rest of the project.
