using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
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

            if (!GuildList.Contains(OldSelectedGuild))
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
        }

        private void SaveSettings()
        {
            App.SetSettingBool("AutoScroll", AutoScroll);
            App.SetSettingBool("Topmost", Topmost);
            App.SetSettingDouble("MessageZoom", MessageZoom);
            App.SetSettingBool("HideSpoilers", HideSpoilers);
            App.SetSettingInt("SelectedGuild", SelectedGuild);
        }
        #endregion

        #region Log entries
        private void InitLogEntries()
        {
            LastReadIndex = -1;
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
            List<LogEntry> LogEntryList = new List<LogEntry>();

            App.DownloadLog(HideSpoilers, GuildName, ref LastReadIndex, ref LatestRegisteredUserCount, ref LatestConnectedUserCount, ref LatestGuestUserCount, GuildmateTable, LogEntryList);

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

            foreach (LogEntry Entry in LogEntryList)
                GlobalMessageList.Add(Entry);

            if (AutoScroll)
                scrollMessages.ScrollToBottom();

            (Application.Current as App).UpdateToolTipText();
        }

        private Timer LogTimer;
        private int LastReadIndex;
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
            (Application.Current as App).OnSettings(sender, e);
        }

        private void OnClose(object sender, ExecutedRoutedEventArgs e)
        {
            (Application.Current as App).OnClose(sender, e);
        }

        private void OnExit(object sender, ExecutedRoutedEventArgs e)
        {
            (Application.Current as App).OnExit(sender, e);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
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
