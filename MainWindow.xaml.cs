using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DVDOrders.Objects;
using DVDOrders.Services;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Path = System.IO.Path;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DVDOrders
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly FileSystemWatcher _queueFile;

        private readonly HTTPServer _httpServer;

        private readonly ObservableCollection<QueueEntry> _entries = new ObservableCollection<QueueEntry>();

        private readonly ObservableCollection<QueueEntry> _completed = new ObservableCollection<QueueEntry>();

        private readonly ObservableCollection<Task> _tasks = new ObservableCollection<Task>();

        private readonly Dictionary<long, Stopwatch> _taskStopWatch = new Dictionary<long, Stopwatch>();

        private readonly Dictionary<long, CancellationTokenSource> _taskTokens = new Dictionary<long, CancellationTokenSource>();

        private readonly Dictionary<string, FileSystemWatcher> _fileSystemWatchers = new Dictionary<string, FileSystemWatcher>();

        private readonly AdvancedTimer _polling = new AdvancedTimer();

        public MainWindow()
        {
            InitializeComponent();


            Settings.Create();

            if (Directory.Exists(Settings.DvdQueueFolder))
            {
                _queueFile = new FileSystemWatcher(Settings.DvdQueueFolder, "*.queue");

                _queueFile.Created += CreateQueueFile;

                _queueFile.EnableRaisingEvents = true;
            }

            DefaultEmailAddress.Text = Settings.EmailAddress;

            LabelFileFormat.Text = Settings.FormatLabelContent;

            DVDQueueField.Text = Settings.DvdQueueFolder;

            EmailServerEmail.Text = Settings.EmailServerAddress ?? "admin@contoso.com";

            EmailServerSite.Text = Settings.EmailServerSite ?? "smtp.office365.com";

            EmailServerPort.Text = Settings.EmailServerPort + "";

            EmailServerDomain.Text = Settings.EmailServerDomain ?? "microsoft";

            EmailServerUsr.Text = Settings.EmailServerUsername ?? "admin@contoso.com";

            EmailServerPwd.Password = Settings.EmailServerPassword ?? "password";

            EmailServerSSL.IsChecked = Settings.EmailServerSsl;

            CheckFilterAgainstNewName.IsChecked = Settings.FileFilterRename;

            QueueEmailRecipient.ItemsSource = Enum.GetValues(typeof(EmailRecipients)).Cast<EmailRecipients>();

            QueueEmailRecipient.SelectedItem = Settings.QueueRecipient;

            if (File.Exists("queue.xml")) _entries = (ObservableCollection<QueueEntry>)_entries.GetType().GetXmlObject(Extensions.ReadXmlToString("queue.xml"));

            foreach (var robot in Settings.Robots())
            {
                robotList.Items.Add(new ListBoxItem() { Content = robot.Name, Tag = robot.Id });
            }

            foreach (var watcher in Settings.GetWatcherConfigs())
            {
                var robot = Settings.Robot(watcher.Robot);

                if(robot == null) continue;

                try
                {
                    AddWatcherToCollection(watcher, robot);
                }
                catch (Exception e)
                {
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                        ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": " + e.Message + "\r\n"));
                }
                
            }

            PollSliderInterval.Value = Settings.PollInterval;

            TaskSliderMax.Value = Settings.MaxTasks;

            try
            {
                _httpServer = new HTTPServer(5600, new List<string>() {$"http://+:{5600}/", $"http://*:{5600}/"});
                _httpServer.EventDictionary.Add("/api/queue", QueueItem);
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                    ErrorTextBox.Text += "[" + DateTime.Now +
                                         "][HTTP] Must run as administrator to use the HTTP queue api.\r\n"));
            }

            _polling.Interval = (int)Settings.PollInterval;

            _polling.Elapsed += PollingOnElapsed;

            _polling.Start();

            DataGrid.ItemsSource = _entries;

            CompletedGrid.ItemsSource = _completed;

            TaskGrid.ItemsSource = _tasks;

            if (File.Exists("log.txt")) ErrorTextBox.Text = File.ReadAllText("log.txt");
        }

        private void AddWatcherToCollection(WatcherConfig watcher, RobotConfig robot)
        {
            switch (watcher.WatcherType)
            {
                case WatcherType.Encoder:
                    if (!Directory.Exists(robot.Encoder))
                        throw new Exception("Watch folder doesn't exist.");
                    break;
                case WatcherType.Robot:
                    if (!Directory.Exists(robot.JobLocation))
                        throw new Exception("Watch folder doesn't exist.");
                    break;
                default:
                    throw new Exception("Watcher Type doesn't exist.");
            }

            var fs = watcher.WatcherType == WatcherType.Robot ? new FileSystemWatcher(robot.JobLocation, watcher.FileFilters) : new FileSystemWatcher(robot.Encoder, watcher.FileFilters);

            fs.EnableRaisingEvents = watcher.Enabled;

            fs.NotifyFilter = watcher.NotifyFilters;

            fs.IncludeSubdirectories = watcher.Subfolder;

            if (watcher.ChangeType == WatcherChangeTypes.All)
            {
                fs.Changed += (s, e) => OnFsChanged(watcher.Id, e);
                fs.Created += (s, e) => OnFsChanged(watcher.Id, e);
                fs.Deleted += (s, e) => OnFsChanged(watcher.Id, e);
                fs.Renamed += (s, e) => OnFsRenamed(watcher.Id, e);
            }
            else if (watcher.ChangeType != WatcherChangeTypes.Renamed)
            {
                fs.Changed += (s, e) => OnFsChanged(watcher.Id, e);
                fs.Created += (s, e) => OnFsChanged(watcher.Id, e);
                fs.Deleted += (s, e) => OnFsChanged(watcher.Id, e);
            }
            else
            {
                fs.Renamed += (s, e) => OnFsRenamed(watcher.Id, e);
            }


            fs.Error += (s, e) => OnFsError(watcher.Id, e);

            _fileSystemWatchers.Add(watcher.Id, fs);

            WatcherList.Items.Add(new Label() { Tag = watcher.Id, Content = "Robot ID : " + watcher.Robot + " | " + watcher.ChangeType + " | " + watcher.WatcherType + " | " + watcher.NotifyFilters + " | " + watcher.FileFilters + " | " + watcher.EmailType, Background = _fileSystemWatchers[watcher.Id].EnableRaisingEvents ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red), Foreground = new SolidColorBrush(Colors.White) });
        }

        private async void CreateQueueFile(object sender, FileSystemEventArgs e)
        {
            
            try
            {
                while (IsFileLocked(new FileInfo(e.FullPath)))
                {
                    await Task.Delay(500);
                }

                var lines = File.ReadAllLines(e.FullPath);

                for (var i = 0; i < lines.Length; i++)
                {
                    lines[i] = lines[i].Replace("\"","");
                }

                var id = Hash.StringHash(HashType.SHA1, DateTime.Now.Ticks.ToString());

                var source = lines[0];

                var lines1 = lines[1];

                var lines2 = lines[2];

                var lines3 = lines[3];

                var copies = int.Parse(lines[4]);

                var robot = int.Parse(lines[5]);

                var email = string.IsNullOrEmpty(lines[6]) ? Settings.EmailAddress : lines[6];

                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    var entry = new QueueEntry(id, robot, EmailType.Queued, source, lines1, lines2, lines3, copies, email);

                    _entries.Add(entry);
                }));

            }
            catch (Exception ex)
            {
                // discard result to prevent hangups and invoke the dispatcher to log the event error
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] Failed to queue entry for the following reason: "+ex.Message+"\r\n"));
            }

        }

        private void OnFsError(string watcher, ErrorEventArgs errorEventArgs)
        {
            // discard result to prevent hangups and invoke the dispatcher to log the event error
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": " + errorEventArgs.GetException().Message + "\r\n"));
        }

        private async void OnFsRenamed(string watcher, RenamedEventArgs renamedEventArgs)
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": File Rename Occured.\r\n"));

            var config = Settings.GetWatcherConfig(watcher);

            var extensionless = Path.GetFileNameWithoutExtension(renamedEventArgs.OldFullPath);

            var watcherExtensionless = Path.GetFileNameWithoutExtension(renamedEventArgs.FullPath);


            if (Settings.FileFilterRename &&
                !LikeOperator.LikeString(renamedEventArgs.Name, config.FileFilters, CompareMethod.Text))
            {
                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                         ": Change detected didn't match current completed jobs using content name comparison.\r\n"));
                return;
            }

            if (config.ContentCompare)
            {

                if (_completed.Any(T => T.Source.Contains(watcherExtensionless)))
                {

                    //|| T.Source.Contains(extensionless)
                    var item = _completed.FirstOrDefault(T => T.Source.Contains(watcherExtensionless) );
                    if (item != default)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                        {

                            if (item.Status == EmailType.Completed) return;
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                 ": Change detected matched a job (first or default) setting status and sending notification.\r\n";

                            item.Status = config.EmailType;
                            CompletedGrid.Items.Refresh();
                            await SendNotification(item, config.EmailType, config.EmailRecipients);
                        })); 
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Change detected didn't match current completed jobs.\r\n"));
                    }
                }
                else
                {
                    var foundContentMatch = false;

                    QueueEntry item = default;

                    while (IsFileLocked(new FileInfo(renamedEventArgs.FullPath)))
                    {

                    }
                    foreach (var line in File.ReadAllLines(renamedEventArgs.FullPath))
                    {
                        foreach (var entry in _completed)
                        {
                            var queueExtensionless = Path.GetFileNameWithoutExtension(entry.Source);
                            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Comparing content line: "+line+" with "+queueExtensionless+".\r\n"));
                            foundContentMatch = line.Contains(queueExtensionless);

                            if (!foundContentMatch) continue;
                            item = entry;
                            break;

                        }
                        if (foundContentMatch)
                        {
                            break;
                        }
                    }

                    if (foundContentMatch)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                        {
                            if (item.Status == EmailType.Completed) return;
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                 ": Change detected matched a job (first or default) setting status and sending notification.\r\n";
                            item.Status = config.EmailType;
                            CompletedGrid.Items.Refresh();
                            await SendNotification(item, config.EmailType, config.EmailRecipients);
                        }));
                    }
                    else
                    {

                        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Change detected didn't match current completed jobs using content name comparison.\r\n"));
                    }

                }
            }
            else
            {
                ////send notification and set status || T.Source.Contains(extensionless))
                var item = _completed.FirstOrDefault(T => T.Source.Contains(watcherExtensionless) );
                if (item != default)
                {
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                    {
                        if (item.Status == EmailType.Completed) return;
                        ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                             ": Change detected matched a job (first or default) setting status and sending notification.\r\n";
                        item.Status = config.EmailType;
                        CompletedGrid.Items.Refresh();
                        await SendNotification(item, config.EmailType, config.EmailRecipients);
                    })); 
                }
                else
                {
                    _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Change detected didn't match current completed jobs.\r\n"));

                }
            }
        }
        protected virtual bool IsFileLocked(FileInfo file)
        {
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
                stream.Close();
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }
        private async void OnFsChanged(string watcher, FileSystemEventArgs fileSystemEventArgs)
        {
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": File/Directory Change" + fileSystemEventArgs.ChangeType + ".\r\n"));
            var config = Settings.GetWatcherConfig(watcher);

            var watcherExtensionless = Path.GetFileNameWithoutExtension(fileSystemEventArgs.FullPath);

            if (config.ChangeType == fileSystemEventArgs.ChangeType || config.ChangeType == WatcherChangeTypes.All)
            {
                if (config.ChangeType != WatcherChangeTypes.Deleted && config.ContentCompare)
                {


                    if (_completed.Any(T => T.Source.Contains(watcherExtensionless)))
                    {
                        var item = _completed.FirstOrDefault(T => T.Source.Contains(watcherExtensionless));
                        if (item != default)
                        {
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                            {

                                if (item.Status == EmailType.Completed) return;
                                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                     ": Change detected matched a job (first or default) setting status and sending notification.\r\n";

                                item.Status = config.EmailType;
                                CompletedGrid.Items.Refresh();
                                await SendNotification(item, config.EmailType, config.EmailRecipients);
                            }));
                        }
                        else
                        {
                            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                     ": Change detected didn't match current completed jobs.\r\n"));
                        }
                    }
                    else
                    {
                        var foundContentMatch = false;
                        while (IsFileLocked(new FileInfo(fileSystemEventArgs.FullPath)))
                        {

                        }
                        QueueEntry item = default;
                        foreach (var line in File.ReadAllLines(fileSystemEventArgs.FullPath))
                        {
                            foreach (var entry in _completed)
                            {
                                var extensionless = Path.GetFileNameWithoutExtension(entry.Source);
                                foundContentMatch = line.Contains(extensionless);

                                if (!foundContentMatch) continue;
                                item = entry;
                                break;

                            }

                        }

                        if (foundContentMatch)
                        {

                            if (item.Status == EmailType.Completed) return;
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                            {
                                ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                     ": Change detected matched a job (first or default) setting status and sending notification.\r\n";
                                item.Status = config.EmailType;
                                CompletedGrid.Items.Refresh();
                                await SendNotification(item, config.EmailType, config.EmailRecipients);
                            }));
                        }
                        else
                        {
                            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Change detected didn't match current completed jobs using content name comparison.\r\n"));
                        }

                    }


                }
                else
                {
                    var item = _completed.FirstOrDefault(T => T.Source.Contains(watcherExtensionless));
                    if (item != default)
                    {
                        await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                        {

                            if (item.Status == EmailType.Completed) return;
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher +
                                                 ": Change detected matched a job (first or default) setting status and sending notification.\r\n";

                            item.Status = config.EmailType;
                            CompletedGrid.Items.Refresh();
                            await SendNotification(item, config.EmailType, config.EmailRecipients);
                        }));
                    }
                    else
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            ErrorTextBox.Text += "[" + DateTime.Now + "][WATCHER] ID#" + watcher + ": Change detected didn't match current completed jobs.\r\n"));
                    }
                }
            }
        }

        private async Task SendNotification(QueueEntry item, EmailType configEmailType, EmailRecipients queueRecipient)
        {
            if (await EmailService.SendMessage(configEmailType, item, queueRecipient, ErrorTextBox))
            {

               _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Queue ID#" + item.ID + ": Success Sending Notification [" + configEmailType + "].\r\n"));
               
            }
            else
            {
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Queue ID#" + item.ID + ": Failed Sending Notification [" + configEmailType + "].\r\n"));
            }
        }

        private async void PollingOnElapsed(object sender, EventArgs e)
        {
            try
            {
                // ReSharper disable once IdentifierTypo
                var pollstopwatch = new Stopwatch();

                pollstopwatch.Start();

                if (_entries.Count > 0 && _tasks.Count < Settings.MaxTasks)
                {
                    var entry = _entries.FirstOrDefault(T => T.Status == EmailType.Queued);

                    if (entry != default)
                    {
                        entry.Status = EmailType.Ready;

                        if (!File.Exists(entry.Source))
                        {
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            {
                                _entries.Remove(entry);
                                entry.Status = EmailType.NotFound;
                                _completed.Add(entry);
                                DataGrid.Items.Refresh();
                                ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] Task Error: Entry ID#" + entry.ID + " Failed to be transferred source file doesn't exist.\r\n";
                            }));
                        }
                        else if (!Settings.RobotExists(entry.Robot))
                        {
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            {
                                _entries.Remove(entry);
                                entry.Status = EmailType.Error;
                                _completed.Add(entry);
                                DataGrid.Items.Refresh();
                                ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] Task Error: Entry ID#" + entry.ID + " Failed to be transferred robot doesn't exist.\r\n";
                            }));
                        }
                        else
                        {
                            var tokenSource = new CancellationTokenSource();

                            var stopwatch = new Stopwatch();

                            var task = Task.Run(() => RunQueuedEntry(entry, stopwatch), tokenSource.Token);

                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                            {
                                ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Polling Log: Task Created ID#" +
                                                     task.Id + " " + entry.Source + "\r\n";
                                entry.TaskId = task.Id;
                                _tasks.Add(task);
                                _taskTokens.Add(task.Id, tokenSource);
                                _taskStopWatch.Add(task.Id, stopwatch);
                                ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Polling Log: Entry Created ID#" + entry.ID + " " + entry.Source + "\r\n";
                            }));


                        }
                    }
                }

                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    var completedEntries = new List<QueueEntry>();
                    completedEntries.AddRange(_completed.Where(T => T.Status == EmailType.Completed).ToArray());
                    foreach (var completedEntry in completedEntries)
                    {
                        ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Polling Operation: Removing completed order ["+completedEntry.Source+"]\r\n";
                        _completed.Remove(completedEntry);
                        CompletedGrid.Items.Refresh();
                    }
                }));

                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {

                    var completedTasks = new List<Task>();
                    completedTasks.AddRange(_tasks.Where(T => T.IsCanceled || T.IsCompleted || T.IsCompletedSuccessfully || T.IsFaulted).ToArray());
                    if (completedTasks.Count > 0)
                    {
                        foreach (var completedTask in completedTasks)
                        {
                            try
                            {
                                completedTask.Dispose();
                                if (_taskTokens.ContainsKey(completedTask.Id))
                                {
                                    _taskTokens[completedTask.Id].Dispose();
                                    _taskTokens.Remove(completedTask.Id);
                                }

                                if (_taskStopWatch.ContainsKey(completedTask.Id))
                                {
                                    _taskStopWatch[completedTask.Id].Stop();
                                    _taskStopWatch.Remove(completedTask.Id);
                                }

                            }
                            catch (Exception es)
                            {
                                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Polling Error: " + es.Message + "\r\n"));
                            }
                            finally
                            {
                                _tasks.Remove(completedTask);
                            }
                            

                        }
                    }


                }));

                pollstopwatch.Stop();

                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => TaskCount.Content = "Tasks: " + _tasks.Count + " / " + Settings.MaxTasks + " Poll Cycle: " + pollstopwatch.ElapsedMilliseconds + " ms"));
            }
            catch (Exception exception)
            {
                if (Application.Current == null) return;
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Polling Error: " + exception.Message + "\r\n"));

            }

        }
        private async Task RunQueuedEntry(QueueEntry entry, Stopwatch stopwatch)
        {
            try
            {
                stopwatch.Start();

                using (var webClient = new WebClient())
                {

                    webClient.DownloadProgressChanged += (s, e) => DownloadProgress(entry, stopwatch, e);

                    webClient.DownloadFileCompleted += (s, e) => DownloadCompleted(entry);

                    var fi = new FileInfo(entry.Source);

                    var robot = Settings.Robot(entry.Robot);

                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                    {
                        entry.Status = EmailType.Transferring;
                        DataGrid.Items.Refresh();
                        ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] File IO: Entry ID#" + entry.ID + " writing label file to robot label location.\r\n";
                        File.WriteAllText(Path.Combine(robot.LabelLocation, Path.GetFileNameWithoutExtension(fi.FullName) + ".txt"), Settings.GetLabelFormatted(entry));

                        if(Settings.QueueRecipient != EmailRecipients.None)
                            await SendNotification(entry, entry.Status, Settings.QueueRecipient);
                    }));


                    await webClient.DownloadFileTaskAsync(entry.Source,
                        Path.Combine(robot.Encoder, fi.Name));
                }

                stopwatch.Stop();
            }
            catch (OperationCanceledException exception)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    _entries.Remove(entry);

                    entry.Status = EmailType.Error;

                    _completed.Add(entry);

                    DataGrid.Items.Refresh();

                    ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] Operation Error: Entry ID#" + entry.ID + " " +
                                         exception.Message + "\r\n";
                }));
            }
            catch (Exception e)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] Task Error: Entry ID#" + entry.ID + " " +
                                         e.Message + "\r\n";
                    _entries.Remove(entry);

                    entry.Status = EmailType.Error;

                    _completed.Add(entry);

                    DataGrid.Items.Refresh();
                }));
            }
            finally
            {
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => entry.TaskId = -1));
            }
        }
        private async void DownloadCompleted(QueueEntry entry)
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action( () =>
            {
                if (_entries.Contains(entry))
                {
                    _entries.Remove(entry);

                    entry.Status = EmailType.Transferred;

                    _completed.Add(entry);

                }
                DataGrid.Items.Refresh();

                ErrorTextBox.Text += "[" + DateTime.Now + "][ENTRY] Task Completed: Entry ID#" + entry.ID + " Successfully transferred source file to encode location.\r\n";
            }));

            await SendNotification(entry, entry.Status, Settings.QueueRecipient);
        }
        private void DownloadProgress(QueueEntry entry, Stopwatch stopwatch, DownloadProgressChangedEventArgs e)
        {
            try
            {
                _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!(entry.Progress < 100)) return;
                    entry.TimeSpan = stopwatch.Elapsed;
                    entry.Progress = e.ProgressPercentage;
                    DataGrid.Items.Refresh();
                }));
            }
            catch
            {
                // Ignored
            }
        }
        private void QueueItem(HttpListenerContext obj)
        {
            try
            {

                if (obj.Request.HttpMethod == "POST")
                {
                    var encoding = obj.Request.ContentEncoding;

                    var request = obj.Request;

                    var text = "";

                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding);

                    text = reader.ReadToEnd();

                    var post = HttpUtility.ParseQueryString(text, encoding);

                    var email = post.Get("email");

                    try
                    {
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var entry = new QueueEntry(Hash.StringHash(HashType.SHA1, DateTime.Now.Ticks.ToString()), int.Parse(post.Get("robot")), EmailType.Queued, post.Get("source").Remove('"'), post.Get("line1"), post.Get("line2"), post.Get("line3"), int.Parse(post.Get("copies")), string.IsNullOrEmpty(email) ? Settings.EmailAddress : email);
                            _entries.Add(entry);
                        });
                    }
                    catch
                    {
                        // Ignore
                    }
                    
                    var json = JsonSerializer.Serialize(post);

                    DataResponse.SendMimeData(HTTPServer.MimeTypeFinder(".json"), obj, Converter.ToStream(json));
                }
                else
                {
                    var queue = "{" +
                                "\"queue\":[";
                    foreach (var queueEntry in _entries)
                    {
                        queue += "{" +
                                 $"\"ID\":\"{queueEntry.ID}\"," +
                                 $"\"Robot\":\"{queueEntry.Robot}\"," +
                                 $"\"EmailAddress\":\"{queueEntry.EmailAddress}\"," +
                                 $"\"Line1\":\"{queueEntry.Line1}\"," +
                                 $"\"Line2\":\"{queueEntry.Line2}\"," +
                                 $"\"Line3\":\"{queueEntry.Line3}\"," +
                                 $"\"CopyCount\":\"{queueEntry.CopyCount}\"," +
                                 $"\"Progress\":\"{queueEntry.Progress}\"," +
                                 $"\"Status\":\"{queueEntry.Status}\"," +
                                 $"\"Source\":\"{queueEntry.Source.Replace('\\', '/')}\"" +
                                 "},";
                    }

                    queue = queue.TrimEnd(',');
                    queue += "]}";
                    //Send Response
                    DataResponse.SendMimeData(HTTPServer.MimeTypeFinder(".json"), obj, Converter.ToStream(queue));
                }


            }
            catch (OperationCanceledException e)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ErrorTextBox.Text += "[" + DateTime.Now + "][HTTP] Operation Error: " + e.Message + "\r\n";
                });
            }
            catch (Exception ex)
            {
                _ = Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ErrorTextBox.Text += "[" + DateTime.Now + "][HTTP] Task Error: " + ex.Message + "\r\n";
                });
            }

        }
        private void robotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var item = (ListBoxItem)robotList.SelectedItem;

                if (item == null) return;
            
                var robot = Settings.Robot((int)item.Tag);

                robotStackID.Text = robot.Id + "";

                robotStackEID.Text = robot.Encoder + "";

                robotStackJob.Text = robot.JobLocation + "";

                robotStackName.Text = robot.Name + "";

                robotStackLbl.Text = robot.LabelLocation + "";

                WatcherFilters.Children.Clear();

                //Create File Watchers

                foreach (var watcher in Settings.GetWatcherConfigs(robot.Id))
                {
                    WatcherFilters.Children.Add(new FileWatchConfig(this, watcher.Id, watcher.EmailType, watcher.NotifyFilters, watcher.ChangeType, watcher.WatcherType, watcher.FileFilters, watcher.ContentCompare, watcher.EmailRecipients, watcher.Subfolder));
                }
            }
            catch (Exception exception)
            {
                ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Operation Error: Failed to get Robot. "+exception.Message+"\r\n";
            }
        }
        private void Delete_Robot_Click(object sender, RoutedEventArgs e)
        {
            if (!Settings.DeleteRobot(int.Parse(robotStackID.Text))) return;

            var robotIndex = -1;

            for (var i = 0; i < robotList.Items.Count; i++)
            {
                var tag = (robotList.Items[i] as ListBoxItem)?.Tag;
                if (tag == null || (int)tag != int.Parse(robotStackID.Text)) continue;
                robotIndex = i;
                break;
            }

            if(robotIndex == -1) return;

            robotList.Items.RemoveAt(robotIndex);

            foreach (var watcher in Settings.GetWatcherConfigs(int.Parse(robotStackID.Text)))
            {
                _fileSystemWatchers.Remove(watcher.Id);
                Settings.RemoveWatcherConfig(watcher.Id);

            }

            Clear_RobotSelection_Click(null, null);
        }
        private void Add_Robot_Click(object sender, RoutedEventArgs e)
        {
            robotList.SelectedItem = null;

            var robot = Settings.Robot(int.Parse(robotStackID.Text));

            if (robot == null)
            {
                try
                {
                    robot = new RobotConfig(int.Parse(robotStackID.Text), robotStackEID.Text, robotStackName.Text, robotStackJob.Text, robotStackLbl.Text);

                    Settings.AddRobot(robot);

                    robotList.Items.Add(new ListBoxItem() { Content = robotStackName.Text, Tag = int.Parse(robotStackID.Text) });

                    //Create File Watchers

                    Clear_RobotSelection_Click(null, null);
                }
                catch
                {
                    MessageBox.Show("[" + DateTime.Now +
                                    "][SYSTEM] Operation Error: Failed to create Robot using current values, double check values are correct.\r\n");
                    ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Operation Error: Failed to create Robot using current values, double check values are correct.\r\n";
                }

            }
            else
            {
                if (Settings.ModifyRobot(robot, new RobotConfig(int.Parse(robotStackID.Text), robotStackEID.Text, robotStackName.Text, robotStackJob.Text, robotStackLbl.Text)))
                {

                    var robotIndex = -1;

                    for (var i = 0; i < robotList.Items.Count; i++)
                    {
                        var tag = (robotList.Items[i] as ListBoxItem)?.Tag;
                        if (tag == null || (int)tag != int.Parse(robotStackID.Text)) continue;
                        robotIndex = i;
                        break;
                    }


                    if (robotIndex != -1)
                    {
                        robotList.Items.RemoveAt(robotIndex);
                        robotList.Items.Add(new ListBoxItem()
                        { Content = robotStackName.Text, Tag = int.Parse(robotStackID.Text) });
                    }


                    Clear_RobotSelection_Click(null, null);
                }
            }

            Settings.Save();
        }
        private void RobotStack_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            AddRobotBtn.IsEnabled = Directory.Exists(robotStackJob.Text) && Directory.Exists(robotStackEID.Text);
        }
        private void Clear_RobotSelection_Click(object sender, RoutedEventArgs e)
        {
            robotList.SelectedItem = null;

            robotStackID.Text = string.Empty;

            robotStackEID.Text = string.Empty;

            robotStackJob.Text = string.Empty;

            robotStackName.Text = string.Empty;

            robotStackLbl.Text = string.Empty;

            AddRobotBtn.IsEnabled = false;

            WatcherFilters.IsEnabled = false;

            AddFSWRow.IsEnabled = false;

            WatcherFilters.Children.Clear();
        }
        private void RobotStackID_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var idSet = !string.IsNullOrEmpty(robotStackID.Text);
            var idSetExist = (idSet && Settings.Robot(int.Parse(robotStackID.Text)) != null);


            DelRobotBtn.IsEnabled = idSet;
            AddFSWRow.IsEnabled = idSetExist;
            WatcherFilters.IsEnabled = idSetExist;
        }
        private void Click_WatcherRefresh(object sender, RoutedEventArgs e)
        {
            var remove = new List<Label>();
            foreach (Label watcherListItem in WatcherList.Items)
            {
                var id = (string)watcherListItem.Tag;
                if(Settings.GetWatcherConfig(id) == null) remove.Add(watcherListItem);
                watcherListItem.Background = _fileSystemWatchers[id].EnableRaisingEvents
                    ? new SolidColorBrush(Colors.Green)
                    : new SolidColorBrush(Colors.Red);
            }

            foreach (var label in remove)
            {
                WatcherList.Items.Remove(label);
            }
        }
        private void stop_stopwatch_task(object sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedCells == null) return;
            try
            {
                _taskStopWatch[((Task)TaskGrid.SelectedItem).Id].Stop();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
        private void start_stopwatch_task(object sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedCells == null) return;
            try
            {
                _taskStopWatch[((Task)TaskGrid.SelectedItem).Id].Start();
            }
            catch (Exception exception)
            {
                ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Operation Error: Failed to start task Stopwatch. "+ exception.Message +"\r\n";
            }
        }
        private void cancel_task(object sender, RoutedEventArgs e)
        {
            if (TaskGrid.SelectedCells == null) return;
            try
            {
                _taskTokens[((Task)TaskGrid.SelectedItem).Id].Cancel();
            }
            catch (Exception exception)
            {
                ErrorTextBox.Text += "[" + DateTime.Now + "][SYSTEM] Operation Error: Failed to cancel task. " + exception.Message + "\r\n";
            }
        }
        private async void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                Task_Max_CountLbl.Content = "Tasks (" + TaskSliderMax.Value + ")";
                Settings.SetMaxTasks(TaskSliderMax.Value);
            }));

        }
        private void ErrorTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (File.Exists("log.txt") && File.ReadAllBytes("log.txt").Length > 1024 * 10) File.Delete("log.txt");
            File.WriteAllText("log.txt", ErrorTextBox.Text);

            ErrorTextBox.ScrollToEnd();
        }
        private async void Interval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                Polling_CountLbl.Content = "Interval (" + PollSliderInterval.Value + ")";
                Settings.SetPollInterval(PollSliderInterval.Value);
                if (!_polling.IsRunning) return;
                _polling.Stop();
                _polling.Interval = (int) Settings.PollInterval;
                _polling.Start();
            }));
        }
        private void Add_FSW_Row_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(robotStackID.Text) ||
                    !Settings.RobotExists(int.Parse(robotStackID.Text))) throw new Exception();
                var conf = new FileWatchConfig(this);
                conf.SetRobot(int.Parse(robotStackID.Text));
                WatcherFilters.Children.Add(conf);
            }
            catch
            {
                MessageBox.Show("Robot doesn't exist or another error occured.");
            }
            
        }
        public bool WatcherExists(string identifier)
        {
            return _fileSystemWatchers.Keys.Any(T => T == identifier);
        }
        public void StopWatcher(string identifier)
        {
            if (_fileSystemWatchers.ContainsKey(identifier))
            {
                Settings.SetWatcherConfigEnabled(identifier, false);
                _fileSystemWatchers[identifier].EnableRaisingEvents = false;
            }

        }
        public void StartWatcher(string identifier)
        {
            if (_fileSystemWatchers.ContainsKey(identifier))
            {
                Settings.SetWatcherConfigEnabled(identifier, true);
                _fileSystemWatchers[identifier].EnableRaisingEvents = true;
            }
        }
        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            File.Delete("queue.xml");

            _polling.Stop();
            var watches = new long[_taskStopWatch.Count];
            _taskStopWatch.Keys.CopyTo(watches, 0);

            foreach (var watch in watches)
            {
                _taskStopWatch[watch].Stop();
                _taskStopWatch.Remove(watch);
            }

            foreach (var task in _tasks)
            {
                try
                {
                    _taskTokens[task.Id].Cancel();
                    task.Wait(_taskTokens[task.Id].Token);
                    task.Dispose();

                }
                catch
                {
                    // ignored
                }
            }

            _tasks.Clear();

            var watchers = new string[_fileSystemWatchers.Count];
            _fileSystemWatchers.Keys.CopyTo(watchers, 0);
            foreach (var watch in watchers)
            {
                try
                {
                    _fileSystemWatchers[watch].EnableRaisingEvents = false;
                    _fileSystemWatchers[watch].Dispose();
                    _fileSystemWatchers.Remove(watch);
                }
                catch
                {
                    // ignored
                }
            }


            try
            {
                _httpServer?.Stop();
                _httpServer?.Dispose();
            }
            catch
            {
                // ignored

            }

            _entries.Where(T => T.Status == EmailType.Queued).ToList().GetXmlString().SaveXmlToFile("queue.xml");
        }
        private void SaveEmailAddress_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetDefaultEmailAddress(DefaultEmailAddress.Text);
        }

        private void SaveLabelContent_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetDefaultLabelContent(LabelFileFormat.Text);
        }

        private void Save_EmailSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetEmailServer(EmailServerSite.Text, EmailServerDomain.Text, EmailServerUsr.Text, EmailServerPwd.Password, EmailServerEmail.Text, int.Parse(EmailServerPort.Text), EmailServerSSL.IsChecked ?? false);
            Settings.Save();
        }

        private void EmailServerPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _ = int.Parse(EmailServerPort.Text);
            }
            catch
            {
                MessageBox.Show("Must be a integer.");
            }
        }

        private void CheckFilterAgainstNewName_OnChecked(object sender, RoutedEventArgs e)
        {
            Settings.SetCheckFileFilterRename(CheckFilterAgainstNewName.IsChecked);
        }

        private void Save_DVDQueue_Click(object sender, RoutedEventArgs e)
        {
            Settings.SetDvdQueueFolder(DVDQueueField.Text);
        }

        public void RemoveWatcherConfig(string identifier)
        {
            Settings.RemoveWatcherConfig(identifier);
        }

        public void AddWatcherLabel(WatcherConfig watcher)
        {
            var robot = Settings.Robot(watcher.Robot);

            if(robot != null) AddWatcherToCollection(watcher, robot);
        }

        private void Save_QueueRecipient(object sender, RoutedEventArgs e)
        {
            Settings.SetQueueRecipient((EmailRecipients) QueueEmailRecipient.SelectedItem);
        }
    }
}
