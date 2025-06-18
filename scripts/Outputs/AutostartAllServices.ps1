New-NetFirewallRule: D:\BuildWorkspace\scripts\AutostartAllServices.ps1:22
Line |
  22 |  … ection Inbound -Protocol TCP -LocalPort 22 -Action Allow -ErrorAction
     |                                                             ~~~~~~~~~~~~
     | Missing an argument for parameter 'ErrorAction'. Specify a parameter of type
     | 'System.Management.Automation.ActionPreference' and try again.
SilentlyContinue: D:\BuildWorkspace\scripts\AutostartAllServices.ps1:23
Line |
  23 |    SilentlyContinue
     |    ~~~~~~~~~~~~~~~~
     | The term 'SilentlyContinue' is not recognized as a name of a cmdlet, function, script file, or executable
     | program. Check the spelling of the name, or if a path was included, verify that the path is correct and try
     | again.
WARNING: PowerShell remoting has been enabled only for PowerShell 6+ configurations and does not affect Windows PowerShell remoting configurations. Run this cmdlet in Windows PowerShell to affect all PowerShell remoting configurations.
WinRM is already set up to receive requests on this computer.
WinRM has been updated for remote management.
WinRM firewall exception enabled.
Configured LocalAccountTokenFilterPolicy to grant administrative rights remotely to local users.

🎉 Auto-start configuration complete!
🔄 Restart DRAGON to activate all services
PS D:\BuildWorkspace\scripts>