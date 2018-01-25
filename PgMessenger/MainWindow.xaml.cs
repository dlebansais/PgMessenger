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
using System.Windows.Navigation;
using System.Windows.Threading;

namespace PgMessenger
{
    public partial class MainWindow : Window, INotifyPropertyChanged, IDisposable
    {
        #region Constants
        private const double DefaultZoom = 1.5;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        #endregion

        #region Init
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            InitLocation();
            InitSettings();
            InitLogEntries();
        }

        private void InitLocation()
        {
            Left = App.GetSettingDouble("HorizontalOffset", double.NaN);
            Top = App.GetSettingDouble("VerticalOffset", double.NaN);
            Width = App.GetSettingDouble("Width", double.NaN);
            Height = App.GetSettingDouble("Height", double.NaN);

            if (!double.IsNaN(Width) && !double.IsNaN(Height))
                SizeToContent = SizeToContent.Manual;
        }

        private void SaveLocation()
        {
            App.SetSettingDouble("HorizontalOffset", Left);
            App.SetSettingDouble("VerticalOffset", Top);
            App.SetSettingDouble("Width", Width);
            App.SetSettingDouble("Height", Height);
        }
        #endregion

        #region Properties
        public string CurrentVersion
        {
            get
            {
                try { return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion; }
                catch { return ""; }
            }
        }

        public bool IsUpdateAvailable
        {
            get { return App.IsUpdateAvailable(CurrentVersion); }
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
            _AutoScroll = App.GetSettingBool("AutoScroll", true);
            Topmost = App.GetSettingBool("Topmost", Topmost);
            _MessageZoom = App.GetSettingDouble("MessageZoom", DefaultZoom);
            if (!(_MessageZoom >= 1.0 && _MessageZoom <= 5.0))
                _MessageZoom = DefaultZoom;
            _HideSpoilers = App.GetSettingBool("HideSpoilers", true);
            _SelectedGuild = App.GetSettingInt("SelectedGuild", -1);
            DisplayGlobal = App.GetSettingBool("DisplayGlobal", true);
            DisplayHelp = App.GetSettingBool("DisplayHelp", true);
            DisplayTrade = App.GetSettingBool("DisplayTrade", true);
        }

        private void SaveSettings()
        {
            App.SetSettingBool("AutoScroll", AutoScroll);
            App.SetSettingBool("Topmost", Topmost);
            App.SetSettingDouble("MessageZoom", MessageZoom);
            App.SetSettingBool("HideSpoilers", HideSpoilers);
            if (SelectedGuild >= 0)
                App.SetSettingInt("SelectedGuild", SelectedGuild);
            App.SetSettingBool("DisplayGlobal", DisplayGlobal);
            App.SetSettingBool("DisplayHelp", DisplayHelp);
            App.SetSettingBool("DisplayTrade", DisplayTrade);
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

            App.DownloadLog(GuildName, ref LatestRegisteredUserCount, ref LatestConnectedUserCount, ref LatestGuestUserCount, GuildmateTable, ChatLineList);

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

            (Application.Current as App).UpdateToolTipText();

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
                if (App.ParseMessageInfo(ChatLineList[0], HideSpoilers, DisplayGlobal, DisplayHelp, DisplayTrade, GuildName, out LogEntry))
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
            (Application.Current as App).NotifyClientClosed(e);
        }

        private void OnLoadAtStartup(object sender, ExecutedRoutedEventArgs e)
        {
            (Application.Current as App).OnLoadAtStartup(sender, e);
        }

        private void OnRestoreWindow(object sender, ExecutedRoutedEventArgs e)
        {
            Left = 0;
            Top = 0;
            Width = double.NaN;
            Height = double.NaN;
            SizeToContent = SizeToContent.WidthAndHeight;

            if (!IsVisible)
                Show();
            App.SetForeground(this);
        }

        private void OnSettings(object sender, ExecutedRoutedEventArgs e)
        {
            (Application.Current as App).OnSettings();
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
            (Application.Current as App).OnClose();
        }

        private void OnExit(object sender, ExecutedRoutedEventArgs e)
        {
            (Application.Current as App).OnExit();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            bool CloseApplication = (MessageBox.Show("To use the dowloaded update you need to exit the application. Do it now?", "Downloading update", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
            Process UpdateProcess = Process.Start(PgMessenger.App.UpdateLink);
            UpdateProcess.WaitForExit(3000);

            if (CloseApplication)
                (Application.Current as App).OnExit();
        }
        #endregion

        #region Implementation of INotifyPropertyChanged
        /// <summary>
        ///     Implements the PropertyChanged event.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        internal void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameter is mandatory with [CallerMemberName]")]
        internal void NotifyThisPropertyChanged([CallerMemberName] string propertyName = "")
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
