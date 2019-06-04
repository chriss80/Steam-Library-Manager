﻿using MahApps.Metro.Controls.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Steam_Library_Manager.Definitions
{
    public class OriginAppInfo : AppBase
    {
        public string[] Locales { get; set; }

        public string InstalledLocale { get; set; }
        public FileInfo TouchupFile { get; set; }
        public string InstallationParameter { get; set; }
        public string UpdateParameter { get; set; }
        public string RepairParameter { get; set; }
        public Version AppVersion { get; set; }

        public OriginAppInfo(Library library, string appName, int appId, DirectoryInfo installationDirectory, Version appVersion, string[] locales, string installedLocale, string touchupFile, string installationParameter, string updateParameter = null, string repairParameter = null)
        {
            Library = library;
            AppName = appName;
            AppId = appId;
            Locales = locales;
            InstalledLocale = installedLocale;
            InstallationDirectory = installationDirectory;
            TouchupFile = new FileInfo(installationDirectory.FullName + touchupFile);
            InstallationParameter = installationParameter;
            UpdateParameter = updateParameter;
            RepairParameter = repairParameter;
            AppVersion = appVersion;
            SizeOnDisk = Functions.FileSystem.GetDirectorySize(InstallationDirectory, true);
            LastUpdated = InstallationDirectory.LastWriteTimeUtc;
            IsCompacted = CompactStatus().Result;
        }

        public async void ParseMenuItemActionAsync(string action)
        {
            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "disk":
                        InstallationDirectory.Refresh();

                        if (InstallationDirectory.Exists)
                        {
                            Process.Start(InstallationDirectory.FullName);
                        }

                        break;

                    case "compact":
                        if (Functions.TaskManager.TaskList.Count(x => x.OriginApp == this && x.TargetLibrary == Library && x.TaskType == Enums.TaskType.Compact) == 0)
                        {
                            Functions.TaskManager.AddTask(new List.TaskInfo
                            {
                                OriginApp = this,
                                TargetLibrary = Library,
                                TaskType = Enums.TaskType.Compact
                            });
                        }
                        break;

                    case "install":

                        await InstallAsync().ConfigureAwait(false);

                        break;

                    case "repair":

                        await InstallAsync(true).ConfigureAwait(false);

                        break;

                    case "deleteappfiles":
                        await Task.Run(async () => await DeleteFilesAsync()).ConfigureAwait(false);

                        Library.Origin.Apps.Remove(this);
                        if (SLM.CurrentSelectedLibrary == Library)
                            Functions.App.UpdateAppPanel(Library);

                        break;

                    case "deleteappfilestm":
                        Functions.TaskManager.AddTask(new List.TaskInfo
                        {
                            OriginApp = this,
                            TargetLibrary = Library,
                            TaskType = Enums.TaskType.Delete
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public async Task InstallAsync(bool repair = false)
        {
            try
            {
                TouchupFile.Refresh();

                if (TouchupFile.Exists && !string.IsNullOrEmpty(InstallationParameter))
                {
                    if (repair && string.IsNullOrEmpty(RepairParameter))
                    {
                        return;
                    }

                    await Main.FormAccessor.AppView.AppPanel.Dispatcher.Invoke(async delegate
                    {
                        var progressInformationMessage = await Main.FormAccessor.ShowProgressAsync(Functions.SLM.Translate(nameof(Properties.Resources.PleaseWait)), Framework.StringFormat.Format(Functions.SLM.Translate(nameof(Properties.Resources.OriginInstallation_Start)), new { AppName })).ConfigureAwait(true);
                        progressInformationMessage.SetIndeterminate();

                        var process = Process.Start(TouchupFile.FullName, ((repair) ? RepairParameter : InstallationParameter).Replace("{locale}", InstalledLocale).Replace("{installLocation}", InstallationDirectory.FullName));

                        Debug.WriteLine(InstallationParameter.Replace("{locale}", InstalledLocale).Replace("{installLocation}", InstallationDirectory.FullName));

                        progressInformationMessage.SetMessage(Framework.StringFormat.Format(Functions.SLM.Translate(nameof(Properties.Resources.OriginInstallation_Ongoing)), new { AppName }));

                        while (!process.HasExited)
                        {
                            await Task.Delay(100).ConfigureAwait(false);
                        }

                        await progressInformationMessage.CloseAsync().ConfigureAwait(false);

                        var installLog = File.ReadAllLines(Path.Combine(InstallationDirectory.FullName, "__Installer", "InstallLog.txt")).Reverse().ToList();
                        if (installLog.Any(x => x.IndexOf("Installer finished with exit code:", StringComparison.OrdinalIgnoreCase) != -1))
                        {
                            var installerResult = installLog.FirstOrDefault(x => x.IndexOf("Installer finished with exit code:", StringComparison.OrdinalIgnoreCase) != -1);

                            await Main.FormAccessor.ShowMessageAsync(Functions.SLM.Translate(nameof(Properties.Resources.OriginInstallation)), Framework.StringFormat.Format(Functions.SLM.Translate(nameof(Properties.Resources.OriginInstallation_Completed)), new { installerResult })).ConfigureAwait(true);
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }
}