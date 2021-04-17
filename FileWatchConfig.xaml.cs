using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DVDOrders.Objects;
using DVDOrders.Services;

namespace DVDOrders
{
    /// <summary>
    /// Interaction logic for FileWatchConfig.xaml
    /// </summary>
    public partial class FileWatchConfig : UserControl
    {
        private readonly MainWindow _main;
        private int _robot;
        public string Identifier { get; private set; }
        public FileWatchConfig(MainWindow main)
        {
            _main = main;
            InitializeComponent();

            Identifier = Hash.StringHash(HashType.SHA512, DateTime.Now.Ticks + "");
            IdentLabel.Content = Identifier;
            WatcherType.ItemsSource = Enum.GetValues(typeof(WatcherType)).Cast<WatcherType>();
            ChangeType.ItemsSource = Enum.GetValues(typeof(WatcherChangeTypes)).Cast<WatcherChangeTypes>();
            FilterType.ItemsSource = Enum.GetValues(typeof(NotifyFilters)).Cast<NotifyFilters>();
            EmailType.ItemsSource = Enum.GetValues(typeof(EmailType)).Cast<EmailType>();
            EmailRecipient.ItemsSource = Enum.GetValues(typeof(EmailRecipients)).Cast<EmailRecipients>();

            SaveBtn.IsEnabled = true;
        }

        public WatcherConfig GetWatcherConfig()
        {
            EmailRecipients email = EmailRecipients.Everyone;
            if (EmailRecipient.SelectedItem != null) email = (EmailRecipients) EmailRecipient.SelectedItem;
            return new WatcherConfig(Identifier,(WatcherType) WatcherType.SelectedItem,false,ContentCompare.IsChecked??false,(NotifyFilters) FilterType.SelectedItem, FileFilter.Text, (WatcherChangeTypes)ChangeType.SelectedItem,(EmailType) EmailType.SelectedItem, Subfolder.IsChecked ?? false, _robot, email);
        }

        public FileWatchConfig(MainWindow main, string id, EmailType type, NotifyFilters filters,
            WatcherChangeTypes change, WatcherType watcher, string file, bool content,
            EmailRecipients watcherEmailRecipients, bool recurse)
        {
            _main = main;
            InitializeComponent();
            WatcherType.ItemsSource = Enum.GetValues(typeof(WatcherType)).Cast<WatcherType>();
            ChangeType.ItemsSource = Enum.GetValues(typeof(WatcherChangeTypes)).Cast<WatcherChangeTypes>();
            FilterType.ItemsSource = Enum.GetValues(typeof(NotifyFilters)).Cast<NotifyFilters>();
            EmailType.ItemsSource = Enum.GetValues(typeof(EmailType)).Cast<EmailType>();
            EmailRecipient.ItemsSource = Enum.GetValues(typeof(EmailRecipients)).Cast<EmailRecipients>();

            Identifier = id;
            ContentCompare.IsChecked = content;
            EmailType.SelectedItem = type;
            FilterType.SelectedItem = filters;
            ChangeType.SelectedItem = change;
            WatcherType.SelectedItem = watcher;
            EmailRecipient.SelectedItem = watcherEmailRecipients;
            FileFilter.Text = file;
            IdentLabel.Content = id;
            Subfolder.IsChecked = recurse;
            CheckExists();
        }

        private void CheckExists()
        {
            if (_main.WatcherExists(Identifier))
            {
                DeleteBtn.IsEnabled = true;
                SaveBtn.IsEnabled = true;
                StartBtn.IsEnabled = true;
                StopBtn.IsEnabled = true;
            }
            else
            {
                DeleteBtn.IsEnabled = false;
                SaveBtn.IsEnabled = true;
                StartBtn.IsEnabled = false;
                StopBtn.IsEnabled = false;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((StackPanel)this.Parent).Children.Remove(this);
            _main.RemoveWatcherConfig(Identifier);
        }

        private void Button_Stop_Click(object sender, RoutedEventArgs e)
        {
            _main.StopWatcher(Identifier);
        }

        private void Button_Start_Click(object sender, RoutedEventArgs e)
        {
            _main.StartWatcher(Identifier);
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            DeleteBtn.IsEnabled = true;
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;
            WatcherConfig cfg = GetWatcherConfig();
            Settings.AddModWatcherConfig(cfg);
            _main.AddWatcherLabel(cfg);
        }

        public void SetRobot(int parse)
        {
            _robot = parse;
        }
    }
}
