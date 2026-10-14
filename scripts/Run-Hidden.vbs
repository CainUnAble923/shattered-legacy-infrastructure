' cc-P107: runs one PowerShell script with no window (for scheduled tasks that run every minute).
'     wscript.exe //B //Nologo Run-Hidden.vbs "<full path to a .ps1>"
' Only a .ps1 next to this file is run. ASCII only.
Dim fso, here, target
Set fso = CreateObject("Scripting.FileSystemObject")
here = fso.GetParentFolderName(WScript.ScriptFullName)
If WScript.Arguments.Count <> 1 Then WScript.Quit 2
target = WScript.Arguments(0)
If LCase(fso.GetParentFolderName(target)) <> LCase(here) Or LCase(fso.GetExtensionName(target)) <> "ps1" Then WScript.Quit 2
CreateObject("WScript.Shell").Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & target & """", 0, True
