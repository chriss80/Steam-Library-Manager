﻿using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Steam_Library_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class Main
    {
        public static Main FormAccessor;
        public Framework.AsyncObservableCollection<string> TaskManager_Logs = new Framework.AsyncObservableCollection<string>();
        //Framework.Network.Server SLMServer = new Framework.Network.Server();

        public Main()
        {
            InitializeComponent();

            UpdateBindings();
            MetroDialogOptions.ColorScheme = MetroDialogColorScheme.Accented;
        }

        void UpdateBindings()
        {
            FormAccessor = this;

            Properties.Settings.Default.SearchText = "";

            LibraryPanel.ItemsSource = Definitions.List.Libraries;
            TaskPanel.ItemsSource = Framework.TaskManager.TaskList;
            TaskManager_LogsView.ItemsSource = TaskManager_Logs;

            LibraryCleaner.ItemsSource = Definitions.List.LCItems;
        }

        private void MainForm_Loaded(object sender, RoutedEventArgs e)
        {
            Functions.SLM.OnLoad();

            GeneralSettingsGroupBox.DataContext = new Definitions.Settings();
            QuickSettings.DataContext = GeneralSettingsGroupBox.DataContext;

            if (Properties.Settings.Default.Global_StartTaskManagerOnStartup)
            {
                Framework.TaskManager.Start();
            }

            if (Properties.Settings.Default.Advanced_Logging)
            {
                Functions.Logger.StartLogger();
            }
        }

        private async void MainForm_ClosingAsync(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (e.Cancel) return;
            e.Cancel = true;

            var mySettings = new MetroDialogSettings()
            {
                AffirmativeButtonText = "Quit",
                NegativeButtonText = "Cancel",
                AnimateShow = true,
                AnimateHide = false
            };

            var result = await this.ShowMessageAsync("Quit application?",
                "Sure you want to quit application?",
                MessageDialogStyle.AffirmativeAndNegative, mySettings);

            if (result == MessageDialogResult.Affirmative)
            {
                Functions.SLM.OnClosing();
                Application.Current.Shutdown();
            }
        }

        private void LibraryGrid_Drop(object sender, DragEventArgs e)
        {
            try
            {
                Definitions.Library Library = (sender as Grid).DataContext as Definitions.Library;

                if (AppPanel.SelectedItems.Count == 0 || Library == null)
                {
                    return;
                }

                if (!Library.DirectoryInfo.Exists)
                {
                    return;
                }

                if (Definitions.SLM.CurrentSelectedLibrary.Type == Definitions.Enums.LibraryType.Steam || (Definitions.SLM.CurrentSelectedLibrary.Type == Definitions.Enums.LibraryType.SLM && Library.Type == Definitions.Enums.LibraryType.Steam))
                {
                    foreach (Definitions.AppInfo App in AppPanel.SelectedItems)
                    {
                        if (Library == App.Library)
                        {
                            continue;
                        }

                        if (Framework.TaskManager.TaskList.Count(x => x.App == App && x.TargetLibrary == Library) == 0)
                        {
                            Definitions.List.TaskInfo newTask = new Definitions.List.TaskInfo
                            {
                                App = App,
                                TargetLibrary = Library
                            };

                            Framework.TaskManager.AddTask(newTask);
                        }
                        else
                        {
                            MessageBox.Show($"This item is already tasked.\n\nGame: {App.AppName}\nTarget Library: {Library.DirectoryInfo.FullName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Functions.Logger.LogToFile(Functions.Logger.LogType.SLM, ex.ToString());
                MessageBox.Show(ex.ToString());
            }
        }

        private void LibraryGrid_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }

        private void LibraryPanel_Drop(object sender, DragEventArgs e)
        {
            string[] DroppedItems = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            if (DroppedItems == null)
            {
                return;
            }

            foreach (string DroppedItem in DroppedItems)
            {
                FileInfo Info = new FileInfo(DroppedItem);

                if (Info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!Functions.SLM.Library.IsLibraryExists(DroppedItem))
                    {
                        if (Directory.GetDirectoryRoot(DroppedItem) != DroppedItem)
                        {
                            Functions.SLM.Library.AddNew(Info.FullName);
                        }
                        else
                        {
                            MessageBox.Show("Libraries can not be created at root");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Library already exists at " + DroppedItem);
                    }
                }
            }
        }

        private void LibraryCMenuItem_Click(object sender, RoutedEventArgs e) => ((Definitions.Library)(sender as MenuItem).DataContext).ParseMenuItemAction((string)(sender as MenuItem).Tag);

        private void Gamelibrary_ContextMenuItem_Click(object sender, RoutedEventArgs e) => ((Definitions.AppInfo)(sender as MenuItem).DataContext).ParseMenuItemActionAsync((string)(sender as MenuItem).Tag);

        private void RightWindowCommands_SettingsButton_Click(object sender, RoutedEventArgs e) => TabItem_Settings.IsSelected = true;

        private void CheckForUpdates_Click(object sender, RoutedEventArgs e) => Functions.Updater.CheckForUpdates(true);

        private void LibraryGrid_MouseDown(object sender, SelectionChangedEventArgs e)
        {
            Definitions.SLM.CurrentSelectedLibrary = LibraryPanel.SelectedItem as Definitions.Library;

            if (Definitions.SLM.CurrentSelectedLibrary == null)
            {
                return;
            }

            if (Definitions.SLM.CurrentSelectedLibrary.Type == Definitions.Enums.LibraryType.SLM)
            {
                if (Directory.Exists(Definitions.SLM.CurrentSelectedLibrary.DirectoryInfo.FullName))
                {
                    Functions.SLM.Library.UpdateBackupLibrary(Definitions.SLM.CurrentSelectedLibrary);
                }
            }

            // Update games list from current selection
            Functions.App.UpdateAppPanel(Definitions.SLM.CurrentSelectedLibrary);
        }

        private void TaskManager_Buttons_Click(object sender, RoutedEventArgs e)
        {
            switch((sender as Button).Tag)
            {
                default:
                case "Start":
                    Framework.TaskManager.Start();
                    Button_StartTaskManager.IsEnabled = false;
                    Button_PauseTaskManager.IsEnabled = true;
                    Button_StopTaskManager.IsEnabled = true;
                    break;
                case "Pause":
                    Framework.TaskManager.Pause();
                    Button_PauseTaskManager.IsEnabled = false;
                    Button_StopTaskManager.IsEnabled = true;
                    break;
                case "Stop":
                    Framework.TaskManager.Stop();
                    Button_PauseTaskManager.IsEnabled = false;
                    Button_StopTaskManager.IsEnabled = false;
                    break;
                case "BackupUpdates":
                    Functions.Steam.Library.CheckForBackupUpdates();
                    break;
                case "ClearCompleted":
                    if (Framework.TaskManager.TaskList.Count == 0)
                    {
                        return;
                    }

                    foreach (Definitions.List.TaskInfo CurrentTask in Framework.TaskManager.TaskList.ToList())
                    {
                        if (CurrentTask.Completed)
                        {
                            Framework.TaskManager.TaskList.Remove(CurrentTask);
                        }
                    }
                    break;
            }
        }

        private void TaskManager_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                switch ((sender as MenuItem).Tag)
                {
                    default:
                    case "Remove":
                        if (TaskPanel.SelectedItems.Count == 0)
                        {
                            return;
                        }

                        List<Definitions.List.TaskInfo> SelectedItems = TaskPanel.SelectedItems.OfType<Definitions.List.TaskInfo>().ToList();

                        foreach (Definitions.List.TaskInfo CurrentTask in SelectedItems)
                        {
                            if (CurrentTask.Active && Framework.TaskManager.Status && !CurrentTask.Completed)
                            {
                                MessageBox.Show($"[{CurrentTask.App.AppName}] You can't remove an app from Task Manager which is currently being moved.\n\nPlease Stop the Task Manager first.");
                            }
                            else
                            {
                                Framework.TaskManager.RemoveTask(CurrentTask);
                                TaskPanel.Items.Remove(CurrentTask);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Functions.Logger.LogToFile(Functions.Logger.LogType.SLM, ex.ToString());
            }
        }

        private void Gamelibrary_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is Grid grid && e.LeftButton == MouseButtonState.Pressed)
                {
                    // Do drag & drop with our pictureBox
                    DragDrop.DoDragDrop(grid, grid.DataContext, DragDropEffects.Move);
                }
            }
            catch { }
        }

        private void GameSortingMethod_SelectionChanged(object sender, SelectionChangedEventArgs e) => Functions.App.UpdateAppPanel(Definitions.SLM.CurrentSelectedLibrary);

        private void LibraryCleaner_ContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LibraryCleaner.SelectedItems.Count == 0)
                {
                    return;
                }

                List<Definitions.List.JunkInfo> SelectedJunks = LibraryCleaner.SelectedItems.OfType<Definitions.List.JunkInfo>().ToList();

                foreach (Definitions.List.JunkInfo Junk in SelectedJunks)
                {
                    if ((string)(sender as MenuItem).Tag == "Explorer")
                    {
                        Process.Start(Junk.FSInfo.FullName);
                    }
                    else
                    {
                        if (Junk.FSInfo is FileInfo)
                        {
                            if (((FileInfo)Junk.FSInfo).Exists)
                            {
                                File.SetAttributes(((FileInfo)Junk.FSInfo).FullName, FileAttributes.Normal);
                                ((FileInfo)Junk.FSInfo).Delete();
                            }
                        }
                        else
                        {
                            if (((DirectoryInfo)Junk.FSInfo).Exists)
                            {
                                ((DirectoryInfo)Junk.FSInfo).Delete(true);
                            }
                        }

                        Definitions.List.LCItems.Remove(Junk);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Debug.WriteLine(ex);
                Functions.Logger.LogToFile(Functions.Logger.LogType.SLM, ex.ToString());
            }
        }

        // Library Cleaner Button actions
        private void LibraryCleaner_ButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((string)(sender as Button).Tag == "Refresh")
                {
                    foreach (Definitions.Library Library in Definitions.List.Libraries.Where(x => x.DirectoryInfo.Exists && (x.Type == Definitions.Enums.LibraryType.Steam || x.Type == Definitions.Enums.LibraryType.SLM)))
                    {
                        Library.Steam.UpdateJunks();
                    }
                }

                if (LibraryCleaner.Items.Count == 0)
                {
                    return;
                }

                if ((string)(sender as Button).Tag == "MoveAll")
                {
                    var TargetFolderBrowser = new System.Windows.Forms.FolderBrowserDialog();
                    System.Windows.Forms.DialogResult TargetFolderDialogResult = TargetFolderBrowser.ShowDialog();

                    if (TargetFolderDialogResult == System.Windows.Forms.DialogResult.OK)
                    {
                        if (Directory.GetDirectoryRoot(TargetFolderBrowser.SelectedPath) == TargetFolderBrowser.SelectedPath)
                        {
                            if (MessageBox.Show("Are you sure you like to move junks to root of disk?", "Root?", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                        
                        List<Definitions.List.JunkInfo> LibraryCleanerItems = LibraryCleaner.ItemsSource.OfType<Definitions.List.JunkInfo>().ToList();

                        foreach (Definitions.List.JunkInfo Junk in LibraryCleanerItems)
                        {
                            if (Junk.FSInfo is FileInfo)
                            {
                                if (((FileInfo)Junk.FSInfo).Exists)
                                {
                                    (Junk.FSInfo as FileInfo).CopyTo(Junk.FSInfo.Name, true);
                                }

                                File.SetAttributes(Junk.FSInfo.FullName, FileAttributes.Normal);
                                Junk.FSInfo.Delete();
                            }
                            else
                            {
                                if (((DirectoryInfo)Junk.FSInfo).Exists)
                                {
                                    foreach(FileInfo currentFile in (Junk.FSInfo as DirectoryInfo).EnumerateFileSystemInfos("*", SearchOption.AllDirectories).Where(x => x is FileInfo).ToList())
                                    {
                                        FileInfo newFile = new FileInfo(currentFile.FullName.Replace(Junk.Library.Steam.SteamAppsFolder.FullName, TargetFolderBrowser.SelectedPath));

                                        if (!newFile.Exists || (newFile.Length != currentFile.Length || newFile.LastWriteTime != currentFile.LastWriteTime))
                                        {
                                            if (!newFile.Directory.Exists)
                                            {
                                                newFile.Directory.Create();
                                            }

                                            currentFile.CopyTo(newFile.FullName, true);
                                        }
                                    }

                                    (Junk.FSInfo as DirectoryInfo).Delete(true);
                                }
                            }

                            Definitions.List.LCItems.Remove(Junk);
                        }
                    }
                }
                else if ((string)(sender as Button).Tag == "DeleteAll")
                {
                    if (MessageBox.Show("Saved Games may be located within these folders, are you sure you want to remove them?", "There might be saved games in these folders?!", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        List<Definitions.List.JunkInfo> LibraryCleanerItems = LibraryCleaner.ItemsSource.OfType<Definitions.List.JunkInfo>().ToList();

                        foreach (Definitions.List.JunkInfo Junk in LibraryCleanerItems)
                        {
                            if (Junk.FSInfo is FileInfo)
                            {
                                if (((FileInfo)Junk.FSInfo).Exists)
                                {
                                    File.SetAttributes(((FileInfo)Junk.FSInfo).FullName, FileAttributes.Normal);
                                    ((FileInfo)Junk.FSInfo).Delete();
                                }
                            }
                            else
                            {
                                if (((DirectoryInfo)Junk.FSInfo).Exists)
                                {
                                    ((DirectoryInfo)Junk.FSInfo).Delete(true);
                                }
                            }

                            Definitions.List.LCItems.Remove(Junk);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Functions.Logger.LogToFile(Functions.Logger.LogType.SLM, ex.ToString());
            }
        }

        private void ViewLogsButton(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Definitions.Directories.SLM.Log))
            {
                Process.Start(Definitions.Directories.SLM.Log);
            }
        }

        private void GetIPButton_Click(object sender, RoutedEventArgs e) => Functions.Network.UpdatePublicIP();

        private void GetPortButton_Click(object sender, RoutedEventArgs e) => Properties.Settings.Default.ListenPort = Functions.Network.GetAvailablePort();

        private void ToggleSLMServerButton_Click(object sender, RoutedEventArgs e)
        {
            //ToggleSLMServer.Content = "Stop Server";
            //SLMServer.StartServer();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Framework.Network.Client SLMClient = new Framework.Network.Client();

            SLMClient.ConnectToServer();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                {
                    if ((sender as Grid).DataContext as Definitions.List.TaskInfo is Definitions.List.TaskInfo)
                    {
                        if (((sender as Grid).DataContext as Definitions.List.TaskInfo).App.CommonFolder.Exists)
                        {
                            Process.Start(((sender as Grid).DataContext as Definitions.List.TaskInfo).App.CommonFolder.FullName);
                        }
                    }
                    else if (((sender as Grid).DataContext is Definitions.AppInfo))
                    {
                        if (((sender as Grid).DataContext as Definitions.AppInfo).CommonFolder.Exists)
                        {
                            Process.Start(((sender as Grid).DataContext as Definitions.AppInfo).CommonFolder.FullName);
                        }
                    }
                    else if (((sender as Grid).DataContext is Definitions.Library))
                    {
                        if (((sender as Grid).DataContext as Definitions.Library).Steam.SteamAppsFolder.Exists)
                        {
                            Process.Start(((sender as Grid).DataContext as Definitions.Library).Steam.SteamAppsFolder.FullName);
                        }
                    }
                    else if (((sender as Grid).DataContext is Definitions.List.JunkInfo))
                    {
                        if (((sender as Grid).DataContext as Definitions.List.JunkInfo).FSInfo.Exists)
                        {
                            Process.Start(((sender as Grid).DataContext as Definitions.List.JunkInfo).FSInfo.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Functions.Logger.LogToFile(Functions.Logger.LogType.SLM, ex.ToString());
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Definitions.SLM.CurrentSelectedLibrary != null)
            {
                Functions.App.UpdateAppPanel(Definitions.SLM.CurrentSelectedLibrary);
            }
        }

        private async void HeaderImageClearButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(Definitions.Directories.SLM.HeaderImage))
                {
                    Directory.Delete(Definitions.Directories.SLM.HeaderImage, true);
                }

                await this.ShowMessageAsync("Steam Library Manager", "Header Image Cache cleared.");
            }
            catch { }
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(Definitions.SLM.DonateButtonURL);
            }
            catch { }
        }
    }
}
