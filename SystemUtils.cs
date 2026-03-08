using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;

namespace SONA.Services
{
    public class InstalledProgram
    {
        public string DisplayName { get; set; } = "";
        public string DisplayVersion { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public string QuietUninstallString { get; set; } = "";
    }

    public static class SystemUtils
    {
        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        public static List<InstalledProgram> GetInstalledPrograms()
        {
            var programs = new List<InstalledProgram>();
            string[] registryKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var keyPath in registryKeys)
            {
                foreach (var baseKey in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using (var key = baseKey.OpenSubKey(keyPath))
                        {
                            if (key == null) continue;
                            foreach (var subkeyName in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using (var subkey = key.OpenSubKey(subkeyName))
                                    {
                                        if (subkey == null) continue;
                                        var name = subkey.GetValue("DisplayName") as string;
                                        var uninstall = subkey.GetValue("UninstallString") as string;
                                        var systemComponent = subkey.GetValue("SystemComponent") as int?;
                                        
                                        if (systemComponent == 1) continue; // Skip true system components
                                        
                                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(uninstall))
                                        {
                                            programs.Add(new InstalledProgram
                                            {
                                                DisplayName = name.Trim(),
                                                DisplayVersion = (subkey.GetValue("DisplayVersion") as string) ?? "",
                                                Publisher = (subkey.GetValue("Publisher") as string) ?? "",
                                                UninstallString = uninstall.Trim(),
                                                QuietUninstallString = (subkey.GetValue("QuietUninstallString") as string) ?? ""
                                            });
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }

            return programs.GroupBy(p => p.DisplayName)
                           .Select(g => g.First())
                           .OrderBy(p => p.DisplayName)
                           .ToList();
        }

        public static bool UninstallProgram(InstalledProgram program, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                string cmd = !string.IsNullOrWhiteSpace(program.QuietUninstallString)
                    ? program.QuietUninstallString
                    : program.UninstallString;

                if (string.IsNullOrWhiteSpace(cmd))
                {
                    errorMsg = "No uninstall string found.";
                    return false;
                }

                bool isMsi = cmd.StartsWith("MsiExec.exe", StringComparison.OrdinalIgnoreCase) || cmd.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase);
                if (isMsi && !cmd.Contains("/quiet", StringComparison.OrdinalIgnoreCase))
                {
                    cmd += " /quiet /norestart";
                }

                string fileName = cmd;
                string args = "";

                if (cmd.StartsWith("\""))
                {
                    int endQuote = cmd.IndexOf("\"", 1);
                    if (endQuote > 0)
                    {
                        fileName = cmd.Substring(1, endQuote - 1);
                        args = cmd.Substring(endQuote + 1).Trim();
                    }
                }
                else
                {
                    int space = cmd.IndexOf(" ");
                    if (space > 0)
                    {
                        fileName = cmd.Substring(0, space);
                        args = cmd.Substring(space + 1).Trim();
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        proc.WaitForExit();
                        return proc.ExitCode == 0 || proc.ExitCode == 3010; // 3010 is pending reboot for MSI
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }
    }
}
