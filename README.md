# Keep Awake

A C# WinForms app that:

1. **Keeps the PC from going to sleep or hibernating while still letting the
   screensaver and the screen timeout work.**
2. **Relieves RAM and CPU pressure**, automatically once either one reaches **70%**.

## Building and running

Open `KeepAwake.sln` in Visual Studio, or from a terminal:

```
dotnet build KeepAwake.sln -c Release
bin\Release\net8.0-windows\KeepAwake.exe
```

Target framework is `net8.0-windows` (WinForms). There are no NuGet dependencies, so
it builds offline.

One SDK note: the .NET 8 SDK on this machine (8.0.415) fails before compiling with
a workload manifest error (`Microsoft.NET.Workload.Emscripten.net6` missing), so the
build uses the default newer SDK (11.0.100 here). There is deliberately no
`global.json` in this folder - if you add one, pin it to a newer SDK, not 8.0.415.

## The window

- **Start** blocks sleep, **Stop** allows it again. The elapsed counter shows how
  long sleep has been blocked.
- The RAM and CPU cards update once a second, with a bar that is blue below 70%,
  amber from 70% and red from 85%.
- `Auto-optimize RAM at 70%` and `Auto-optimize CPU at 70%` are ticked by default.
  Each is checked once a second and runs at most once every 60 seconds.
- `Optimize RAM and CPU now` runs both immediately, off the UI thread.
- The **gear icon** (in this window and on the toolbar) opens an options menu that
  starts or stops each part on its own - keep awake, RAM optimization and CPU
  optimization - and has an `Optimize now` action.
- When an optimization lowers a number, a green note says how much:
  `RAM reduced 6%   CPU reduced 12%`.

## The toolbar

The toolbar appears **only when the main window is minimized** — pressing Start
does not collapse anything. While minimized:

```
RAM 62%   CPU 14%
RAM reduced 6%              [ □ ]  [ ✕ ]
```

- It floats above ordinary windows, but it stops being topmost whenever the
  window in front is maximized or running fullscreen - so it never covers a
  fullscreen video or game. When that app goes away it comes back to the front.
- **Drag it anywhere**: the whole strip is a drag handle, including the text.
- It **remembers that spot**: the last dragged position is saved to
  `%APPDATA%\KeepAwake\toolbar.txt` and the strip reopens there next launch. A
  saved spot that no longer exists (monitor unplugged) is clamped back on screen.
- The **maximize button** (or double-clicking the strip) restores the main window; the toolbar
  then disappears again.
- **✕** closes the app entirely, which also releases the sleep block and stops
  the timer.

## Why the screensaver still works

`PowerKeeper` calls `SetThreadExecutionState` with:

| Flag                  | Meaning                      | Used |
| --------------------- | ---------------------------- | ---- |
| `ES_SYSTEM_REQUIRED`  | keep the system awake        | yes  |
| `ES_CONTINUOUS`       | keep that state until reset  | yes  |
| `ES_DISPLAY_REQUIRED` | keep the *display* awake     | **no** |

Because `ES_DISPLAY_REQUIRED` is never set, Windows can blank the screen, start
the screensaver and lock the session as usual. The state is re-asserted every 30
seconds while blocking, since other apps are allowed to clear it.

## RAM and CPU optimizers

`ResourceMonitor` is ported from the `MemoryPower` class in `MemoryOptimizerPro`.

- **RAM**: calls `EmptyWorkingSet` on every process it can open with a working set
  of 8 MB or more, pushing those pages to the standby list.
- **CPU**: CPU cannot be freed the way memory can, so it samples per-process CPU
  and drops busy **background** processes to `BelowNormal`. Anything with a
  visible window is left alone. The reported `CPU reduced X%` is how much less CPU
  those specific processes used afterwards, which only becomes a real number when
  something else is competing for the CPU.

Both need no admin rights. Elevated, protected and other users' processes cannot
be opened and are skipped silently, which is also why the standby-list purge and
the NVIDIA accounting reset from the original project are not included.

## Files

- `KeepAwake.sln`, `KeepAwake.csproj` - solution and project
- `Program.cs` - entry point
- `MainForm.cs` / `MainForm.Designer.cs` - window logic / all controls and layout
- `ToolbarForm.cs` / `ToolbarForm.Designer.cs` - toolbar logic / its controls
- `PowerKeeper.cs` - `SetThreadExecutionState` wrapper
- `ResourceMonitor.cs` - RAM and CPU readings plus both actions
- `powershell-legacy/` - the earlier PowerShell version, kept for reference

## Notes

- No installer, no admin rights, no registry writes, no network access.
- Closing the main window (or pressing ✕ on the toolbar) exits and restores normal
  sleep behaviour.
- If Windows is already asleep the app cannot wake it; it only prevents sleep from
  being requested while it runs.
