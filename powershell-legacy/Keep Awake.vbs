' Keep Awake launcher.
' Runs the PowerShell app with window style 0, so no console window is ever
' created or flashed. Double-click this file to start the app.

Option Explicit

Dim shell, fso, here, command
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

here = fso.GetParentFolderName(WScript.ScriptFullName)

command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & here & "\KeepAwake.ps1"""

shell.Run command, 0, False
