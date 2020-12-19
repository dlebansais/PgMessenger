namespace PgMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Navigation;
    using System.Windows.Threading;
    using RegistryTools;
    using ResourceTools;
    using TaskbarIconHost;

    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        #region Constants
        private const double DefaultZoom = 1.5;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        #endregion

        #region Init
        PgMessengerPlugin Plugin;
        public MainWindow(PgMessengerPlugin plugin)
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                Plugin = plugin;
                Settings = Plugin.Settings;
                LastClosedTime = DateTime.MinValue;

                ResourceLoader.LoadIcon("main.ico", string.Empty, out ImageSource MainIcon);
                Icon = MainIcon;

                InitLocation();
                InitSettings();
                InitLogEntries();
            }
            catch
            {
            }
        }

        private void InitLocation()
        {
            Left = Settings.GetDouble("HorizontalOffset", double.NaN);
            Top = Settings.GetDouble("VerticalOffset", double.NaN);
            Width = Settings.GetDouble("Width", double.NaN);
            Height = Settings.GetDouble("Height", double.NaN);

            if (!double.IsNaN(Width) && !double.IsNaN(Height))
                SizeToContent = SizeToContent.Manual;
        }

        private void SaveLocation()
        {
            Settings.SetDouble("HorizontalOffset", Left);
            Settings.SetDouble("VerticalOffset", Top);
            Settings.SetDouble("Width", Width);
            Settings.SetDouble("Height", Height);
        }

        public Settings Settings { get; private set; }
        #endregion

        #region Properties
        public string CurrentVersion
        {
            get
            {
                try
                {
                    Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
                    AssemblyFileVersionAttribute FileVersion = CurrentAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                    return FileVersion.Version;
                }
                catch
                {
                    return "";
                }
            }
        }

        public bool IsUpdateAvailable
        {
            get { return Plugin.IsUpdateAvailable(CurrentVersion); }
        }

        public int RegisteredUserCount
        {
            get { return _RegisteredUserCount; }
            set
            {
                if (_RegisteredUserCount != value)
                {
                    _RegisteredUserCount = value;
                    NotifyThisPropertyChanged();
                }
            }
        }
        private int _RegisteredUserCount;

        public int ConnectedUserCount
        {
            get { return _ConnectedUserCount; }
            set
            {
                if (_ConnectedUserCount != value)
                {
                    _ConnectedUserCount = value;
                    NotifyThisPropertyChanged();
                    IsToolTipChanged = true;
                }
            }
        }
        private int _ConnectedUserCount;

        public int GuestUserCount
        {
            get { return _GuestUserCount; }
            set
            {
                if (_GuestUserCount != value)
                {
                    _GuestUserCount = value;
                    NotifyThisPropertyChanged();
                    IsToolTipChanged = true;
                }
            }
        }
        private int _GuestUserCount;

        public string LoginName
        {
            get { return _LoginName; }
            set
            {
                if (_LoginName != value)
                {
                    _LoginName = value;
                    NotifyThisPropertyChanged();
                }
            }
        }
        private string _LoginName;

        public bool AutoScroll
        {
            get { return _AutoScroll; }
            set
            {
                if (_AutoScroll != value)
                {
                    _AutoScroll = value;
                    NotifyThisPropertyChanged();

                    if (_AutoScroll)
                        scrollMessages.ScrollToBottom();
                }
            }
        }
        private bool _AutoScroll;

        public double MessageZoom
        {
            get { return _MessageZoom; }
            set
            {
                if (_MessageZoom != value)
                {
                    _MessageZoom = value;
                    NotifyThisPropertyChanged();
                }
            }
        }
        private double _MessageZoom;

        public bool HideSpoilers
        {
            get { return _HideSpoilers; }
            set
            {
                if (_HideSpoilers != value)
                {
                    _HideSpoilers = value;
                    NotifyThisPropertyChanged();
                }
            }
        }
        private bool _HideSpoilers;

        public bool DisplayGlobal { get; set; }
        public bool DisplayHelp { get; set; }
        public bool DisplayTrade { get; set; }

        public int SelectedGuild
        {
            get { return _SelectedGuild; }
            set
            {
                if (_SelectedGuild != value)
                {
                    _SelectedGuild = value;
                    NotifyThisPropertyChanged();
                }
            }
        }
        private int _SelectedGuild;

        public void UpdateGuildList(List<CharacterSetting> CharacterList)
        {
            string OldSelectedGuild = ((SelectedGuild >= 0 && SelectedGuild < GuildList.Count) ? GuildList[SelectedGuild] : null);

            List <string> ToRemove = new List<string>();

            foreach (string GuildName in GuildList)
            {
                bool IsFound = false;
                foreach (CharacterSetting Character in CharacterList)
                    if (Character.GuildName == GuildName)
                    {
                        IsFound = true;
                        break;
                    }
                if (!IsFound)
                    ToRemove.Add(GuildName);
            }

            foreach (CharacterSetting Character in CharacterList)
                if (Character.GuildName.Length > 0 && !GuildList.Contains(Character.GuildName))
                    GuildList.Add(Character.GuildName);

            foreach (string GuildName in ToRemove)
                GuildList.Remove(GuildName);

            if (OldSelectedGuild != null && !GuildList.Contains(OldSelectedGuild))
                SelectedGuild = -1;
            else if (GuildList.Count == 1 && SelectedGuild == -1)
                SelectedGuild = 0;
        }

        public ObservableCollection<Guildmate> GuildmateList { get; private set; } = new ObservableCollection<Guildmate>();
        public ObservableCollection<LogEntry> GlobalMessageList { get; private set; } = new ObservableCollection<LogEntry>();
        public ObservableCollection<string> GuildList { get; private set; } = new ObservableCollection<string>();
        #endregion

        #region Settings
        private void InitSettings()
        {
            _AutoScroll = Settings.GetBool("AutoScroll", true);
            Topmost = Settings.GetBool("Topmost", Topmost);
            _MessageZoom = Settings.GetDouble("MessageZoom", DefaultZoom);
            if (!(_MessageZoom >= 1.0 && _MessageZoom <= 5.0))
                _MessageZoom = DefaultZoom;
            _HideSpoilers = Settings.GetBool("HideSpoilers", true);
            _SelectedGuild = Settings.GetInt("SelectedGuild", -1);
            DisplayGlobal = Settings.GetBool("DisplayGlobal", true);
            DisplayHelp = Settings.GetBool("DisplayHelp", true);
            DisplayTrade = Settings.GetBool("DisplayTrade", true);
        }

        private void SaveSettings()
        {
            Settings.SetBool("AutoScroll", AutoScroll);
            Settings.SetBool("Topmost", Topmost);
            Settings.SetDouble("MessageZoom", MessageZoom);
            Settings.SetBool("HideSpoilers", HideSpoilers);
            if (SelectedGuild >= 0)
                Settings.SetInt("SelectedGuild", SelectedGuild);
            Settings.SetBool("DisplayGlobal", DisplayGlobal);
            Settings.SetBool("DisplayHelp", DisplayHelp);
            Settings.SetBool("DisplayTrade", DisplayTrade);
        }
        #endregion

        #region Log entries
        private void InitLogEntries()
        {
            _RegisteredUserCount = 0;
            _ConnectedUserCount = 0;
            _GuestUserCount = 0;
            LogTimer = new Timer(new TimerCallback(LogTimerCallback));
            LogTimer.Change(PollInterval, PollInterval);
        }

        private void LogTimerCallback(object Parameter)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new LogTimerHandler(OnLogTimer));
        }

        private delegate void LogTimerHandler();
        private void OnLogTimer()
        {
            string GuildName = SelectedGuild >= 0 && SelectedGuild < GuildList.Count ? GuildList[SelectedGuild] : "";
            int LatestRegisteredUserCount = RegisteredUserCount;
            int LatestConnectedUserCount = ConnectedUserCount;
            int LatestGuestUserCount = GuestUserCount;
            Dictionary<string, int> GuildmateTable = new Dictionary<string, int>();
            List<string> ChatLineList = new List<string>();

            Plugin.DownloadLog(GuildName, ref LatestRegisteredUserCount, ref LatestConnectedUserCount, ref LatestGuestUserCount, GuildmateTable, ChatLineList);

            RegisteredUserCount = LatestRegisteredUserCount;
            ConnectedUserCount = LatestConnectedUserCount;
            GuestUserCount = LatestGuestUserCount;

            List<Guildmate> ToRemove = new List<Guildmate>();
            foreach (Guildmate Guildmate in GuildmateList)
                if (!GuildmateTable.ContainsKey(Guildmate.Name))
                    ToRemove.Add(Guildmate);
                else
                    Guildmate.Connection = GuildmateTable[Guildmate.Name];

            foreach (KeyValuePair<string, int> Entry in GuildmateTable)
            {
                bool IsFound = false;
                foreach (Guildmate Guildmate in GuildmateList)
                    if (Guildmate.Name == Entry.Key)
                    {
                        IsFound = true;
                        break;
                    }

                if (!IsFound)
                    GuildmateList.Add(new Guildmate(Entry.Key, Entry.Value));
            }

            foreach (Guildmate Guildmate in ToRemove)
                GuildmateList.Remove(Guildmate);

            IsToolTipChanged = true;

            if (ChatLineList.Count > 0)
                Dispatcher.BeginInvoke(new ParseMessageInfoHandler(OnParseMessageInfo), ChatLineList, GuildName);
        }

        private delegate void ParseMessageInfoHandler(List<string> ChatLineList, string GuildName);
        private void OnParseMessageInfo(List<string> ChatLineList, string GuildName)
        {
            for (int i = 0; i < 4; i++)
            {
                if (ChatLineList.Count == 0)
                {
                    if (AutoScroll)
                        scrollMessages.ScrollToBottom();
                    return;
                }

                LogEntry LogEntry;
                if (Plugin.ParseMessageInfo(ChatLineList[0], HideSpoilers, DisplayGlobal, DisplayHelp, DisplayTrade, GuildName, out LogEntry))
                    GlobalMessageList.Add(LogEntry);

                ChatLineList.RemoveAt(0);
            }

            Dispatcher.BeginInvoke(new ParseMessageInfoHandler(OnParseMessageInfo), ChatLineList, GuildName);
        }

        private Timer LogTimer;
        #endregion

        #region Events
        private void OnClosed(object sender, EventArgs e)
        {
            SaveLocation();
            SaveSettings();
        }

        public void IconClicked()
        {
            if (!IsVisible)
            {
                // We rely on time to avoid a flickering popup.
                if ((DateTime.UtcNow - LastClosedTime).TotalSeconds >= 1.0)
                    Show();
                else
                    LastClosedTime = DateTime.MinValue;
            }
            else
                Plugin.SetForeground(this);
        }

        public bool GetIsToolTipChanged()
        {
            bool Result = IsToolTipChanged;
            IsToolTipChanged = false;

            return Result;
        }

        private bool IsToolTipChanged;

        public void OnCommandShowWindow()
        {
            if (!IsVisible)
                Show();
            Plugin.SetForeground(this);
        }

        public void OnCommandRestoreWindow()
        {
            OnCommandShowWindow();
        }

        public void OnCommandSettings()
        {
            NotifyPropertyChanged(nameof(IsUpdateAvailable));
        }

        private void OnClearAll(object sender, ExecutedRoutedEventArgs e)
        {
            GlobalMessageList.Clear();
        }

        private void OnClearAllButLastHour(object sender, ExecutedRoutedEventArgs e)
        {
            DateTime Now = DateTime.UtcNow;
            while (GlobalMessageList.Count > 0 && GlobalMessageList[0].LogTime < Now)
                GlobalMessageList.RemoveAt(0);
        }

        private void OnClose(object sender, ExecutedRoutedEventArgs e)
        {
            SaveLocation();
            Hide();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            bool CloseApplication = (MessageBox.Show("To use the dowloaded update you need to exit the application. Do it now?", "Downloading update", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
            Process UpdateProcess = Process.Start(Plugin.UpdateLink);
            UpdateProcess.WaitForExit(3000);

            if (CloseApplication)
                Application.Current.Shutdown();
        }

        private DateTime LastClosedTime;
        #endregion

        #region Implementation of INotifyPropertyChanged
        /// <summary>
        /// Implements the PropertyChanged event.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Invoke handlers of the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Invoke handlers of the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected void NotifyThisPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Implementation of IDisposable
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
                DisposeNow();
        }

        private void DisposeNow()
        {
            if (LogTimer != null)
            {
                LogTimer.Dispose();
                LogTimer = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MainWindow()
        {
            Dispose(false);
        }
        #endregion
    }
}
