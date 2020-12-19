namespace PgMessenger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Threading;
    using RegistryTools;
    using ResourceTools;
    using TaskbarIconHost;
    using Tracing;
    using PgChatParser;
    using System.Collections.ObjectModel;
    using TaskbarTools;

    /// <summary>
    /// Represents a plugin that displays Project: Gorgon global chat.
    /// </summary>
    public class PgMessengerPlugin : IPluginClient, IDisposable
    {
        #region Plugin
        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name
        {
            get { return "PgMessenger"; }
        }

        /// <summary>
        /// Gets the plugin unique ID.
        /// </summary>
        public Guid Guid
        {
            get { return new Guid("{2301E527-A27B-4D03-A758-C6D7E4AFB436}"); }
        }

        /// <summary>
        /// Gets the plugin assembly name.
        /// </summary>
        public string AssemblyName { get; } = "PgMessenger-Plugin";

        /// <summary>
        ///  Gets a value indicating whether the plugin require elevated (administrator) mode to operate.
        /// </summary>
        public bool RequireElevated
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the plugin want to handle clicks on the taskbar icon.
        /// </summary>
        public bool HasClickHandler
        {
            get { return true; }
        }

        /// <summary>
        /// Called once at startup, to initialize the plugin.
        /// </summary>
        /// <param name="isElevated">True if the caller is executing in administrator mode.</param>
        /// <param name="dispatcher">A dispatcher that can be used to synchronize with the UI.</param>
        /// <param name="settings">An interface to read and write settings in the registry.</param>
        /// <param name="logger">An interface to log events asynchronously.</param>
        public void Initialize(bool isElevated, Dispatcher dispatcher, Settings settings, ITracer logger)
        {
            IsElevated = isElevated;
            Dispatcher = dispatcher;
            Settings = settings;
            Logger = logger;

            InitUpdate();
            LastReadIndex = -1;
            LoadSettings();

            MainPopup = new MainWindow(this);
            MainPopup.UpdateGuildList(CharacterList);

            InitializeCommand("Show",
                              isVisibleHandler: () => true,
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandShowWindow);

            InitializeCommand("RestoreWindow",
                              isVisibleHandler: () => true,
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandRestoreWindow);

            InitializeCommand("Settings",
                              isVisibleHandler: () => true,
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandSettings);

            InitializeCommand(isVisibleHandler: () => IsLinkMenuVisible(0),
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandClick0);

            InitializeCommand(isVisibleHandler: () => IsLinkMenuVisible(1),
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandClick1);

            InitializeCommand(isVisibleHandler: () => IsLinkMenuVisible(2),
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandClick2);

            InitChatLog(dispatcher);
        }

        private void InitializeCommand(Func<bool> isVisibleHandler, Func<bool> isEnabledHandler, Func<bool> isCheckedHandler, Action commandHandler)
        {
            RoutedUICommand Command = new RoutedUICommand();
            Command.Text = "-";

            InitializeCommand(Command, isVisibleHandler, isEnabledHandler, isCheckedHandler, commandHandler);
        }

        private void InitializeCommand(string header, Func<bool> isVisibleHandler, Func<bool> isEnabledHandler, Func<bool> isCheckedHandler, Action commandHandler)
        {
            string LocalizedText = Properties.Resources.ResourceManager.GetString(header, CultureInfo.CurrentCulture)!;
            RoutedUICommand Command = new RoutedUICommand(LocalizedText, header, GetType());

            InitializeCommand(Command, isVisibleHandler, isEnabledHandler, isCheckedHandler, commandHandler);
        }

        private void InitializeCommand(RoutedUICommand command, Func<bool> isVisibleHandler, Func<bool> isEnabledHandler, Func<bool> isCheckedHandler, Action commandHandler)
        {
            CommandList.Add(command);
            MenuHeaderTable.Add(command, command.Text);
            MenuIsVisibleTable.Add(command, isVisibleHandler);
            MenuIsEnabledTable.Add(command, isEnabledHandler);
            MenuIsCheckedTable.Add(command, isCheckedHandler);
            MenuHandlerTable.Add(command, commandHandler);
        }

        /// <summary>
        /// Gets the list of commands that the plugin can receive when an item is clicked in the context menu.
        /// </summary>
        public List<ICommand> CommandList { get; private set; } = new List<ICommand>();

        /// <summary>
        /// Reads a flag indicating if the state of a menu item has changed. The flag should be reset upon return until another change occurs.
        /// </summary>
        /// <param name="beforeMenuOpening">True if this function is called right before the context menu is opened by the user; otherwise, false.</param>
        /// <returns>True if a menu item state has changed since the last call; otherwise, false.</returns>
        public bool GetIsMenuChanged(bool beforeMenuOpening)
        {
            bool Result = IsMenuChanged;
            IsMenuChanged = false;

            if (Result)
            {
                foreach (KeyValuePair<ICommand, Action> Entry in MenuHandlerTable)
                {
                    if (Entry.Value == OnCommandClick0)
                        SetClickHeader(Entry.Key, 0);
                    if (Entry.Value == OnCommandClick1)
                        SetClickHeader(Entry.Key, 1);
                    if (Entry.Value == OnCommandClick2)
                        SetClickHeader(Entry.Key, 2);
                }
            }

            return Result;
        }

        private void SetClickHeader(ICommand command, int index)
        {
            if (index >= LinkList.Count)
                return;

            string Header = TruncateWithEllipsis(LinkList[index]);
            MenuHeaderTable[command] = Header;
        }

        private static string TruncateWithEllipsis(string s)
        {
            if (s.Length < 23)
                return s;

            string TruncatedString = s.Substring(0, 10) + "..." + s.Substring(s.Length - 10, 10);

            return TruncatedString;
        }

        /// <summary>
        /// Reads the text of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>The menu text.</returns>
        public string GetMenuHeader(ICommand command)
        {
            return MenuHeaderTable[command];
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item should be visible to the user, false if it should be hidden.</returns>
        public bool GetMenuIsVisible(ICommand command)
        {
            return MenuIsVisibleTable[command]();
        }

        private bool IsLinkMenuVisible(int index)
        {
            return LinkList.Count > index;
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item should appear enabled, false if it should be disabled.</returns>
        public bool GetMenuIsEnabled(ICommand command)
        {
            return MenuIsEnabledTable[command]();
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item is checked, false otherwise.</returns>
        public bool GetMenuIsChecked(ICommand command)
        {
            return MenuIsCheckedTable[command]();
        }

        /// <summary>
        /// Reads the icon of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>The icon to display with the menu text, null if none.</returns>
        public Bitmap? GetMenuIcon(ICommand command)
        {
            return null;
        }

        /// <summary>
        /// This method is called before the menu is displayed, but after changes in the menu have been evaluated.
        /// </summary>
        public void OnMenuOpening()
        {
        }

        /// <summary>
        /// Requests for command to be executed.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        public void OnExecuteCommand(ICommand command)
        {
            MenuHandlerTable[command]();
        }

        /// <summary>
        /// Reads a flag indicating if the plugin icon, that might reflect the state of the plugin, has changed.
        /// </summary>
        /// <returns>True if the icon has changed since the last call, false otherwise.</returns>
        public bool GetIsIconChanged()
        {
            return false;
        }

        /// <summary>
        /// Gets the icon displayed in the taskbar.
        /// </summary>
        public Icon Icon
        {
            get
            {
                ResourceLoader.LoadIcon("Taskbar.ico", string.Empty, out Icon Result);
                return Result;
            }
        }

        /// <summary>
        /// Gets the bitmap displayed in the preferred plugin menu.
        /// </summary>
        public Bitmap SelectionBitmap
        {
            get
            {
                ResourceLoader.LoadBitmap("PgMessenger.png", string.Empty, out Bitmap Result);
                return Result;
            }
        }

        /// <summary>
        /// Requests for the main plugin operation to be executed.
        /// </summary>
        public void OnIconClicked()
        {
            MainPopup?.IconClicked();
        }

        /// <summary>
        /// Reads a flag indicating if the plugin tooltip, that might reflect the state of the plugin, has changed.
        /// </summary>
        /// <returns>True if the tooltip has changed since the last call, false otherwise.</returns>
        public bool GetIsToolTipChanged()
        {
            bool Result = false;

            if (MainPopup != null)
            {
                Result = IsToolTipChanged || MainPopup.GetIsToolTipChanged();
                IsToolTipChanged = false;
            }

            return Result;
        }

        /// <summary>
        /// Gets the free text that indicate the state of the plugin.
        /// </summary>
        public string ToolTip
        {
            get
            {
                string Result = "Project: Gorgon Messenger";

                Result += "\r\n" + MainPopup.ConnectedUserCount.ToString() + " Connected, " + MainPopup.GuestUserCount.ToString() + " Guest(s)";
                if (CurrentChat != null)
                    if (LoginName != null)
                        Result += "\r\n" + "You are online as " + LoginName;
                    else
                        Result += "\r\n" + "You are not online";

                return Result;
            }
        }

        /// <summary>
        /// Called when the taskbar is getting the application focus.
        /// </summary>
        public void OnActivated()
        {
        }

        /// <summary>
        /// Called when the taskbar is loosing the application focus.
        /// </summary>
        public void OnDeactivated()
        {
        }

        /// <summary>
        /// Requests to close and terminate a plugin.
        /// </summary>
        /// <param name="canClose">True if no plugin called before this one has returned false, false if one of them has.</param>
        /// <returns>True if the plugin can be safely terminated, false if the request is denied.</returns>
        public bool CanClose(bool canClose)
        {
            return true;
        }

        /// <summary>
        /// Requests to begin closing the plugin.
        /// </summary>
        public void BeginClose()
        {
            MainPopup?.Close();
            SaveSettings();

            using (MainWindow Popup = MainPopup)
            {
                MainPopup = null;
            }

            using (Parser Chat = CurrentChat)
            {
                CurrentChat = null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the plugin is closed.
        /// </summary>
        public bool IsClosed
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the caller is executing in administrator mode.
        /// </summary>
        public bool IsElevated { get; private set; }

        /// <summary>
        /// Gets a dispatcher that can be used to synchronize with the UI.
        /// </summary>
        public Dispatcher Dispatcher { get; private set; } = null!;

        /// <summary>
        /// Gets an interface to read and write settings in the registry.
        /// </summary>
        public Settings Settings { get; private set; } = null!;

        /// <summary>
        /// Gets an interface to log events asynchronously.
        /// </summary>
        public ITracer Logger { get; private set; } = null!;

        private void AddLog(string message)
        {
            Logger.Write(Category.Information, message);
        }

        /// <summary>
        /// Gets the main window popup.
        /// </summary>
        public MainWindow MainPopup { get; private set; }

        private Dictionary<ICommand, string> MenuHeaderTable = new Dictionary<ICommand, string>();
        private Dictionary<ICommand, Func<bool>> MenuIsVisibleTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsEnabledTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsCheckedTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Action> MenuHandlerTable = new Dictionary<ICommand, Action>();
        private bool IsIconChanged;
        private bool IsMenuChanged;
        private bool IsToolTipChanged;
        #endregion

        #region Properties
        public string LogId { get; private set; }
        public List<CharacterSetting> CharacterList { get; private set; } = new List<CharacterSetting>();
        public bool IsGuildChatEnabled { get; private set; }
        public string CustomLogFolder { get; private set; }
        public bool EnableUpdates { get; private set; }
        #endregion

        #region Command Handlers
        private void OnCommandShowWindow()
        {
            MainPopup?.OnCommandShowWindow();
        }

        private void OnCommandRestoreWindow()
        {
            MainPopup?.OnCommandRestoreWindow();
        }

        private void OnCommandSettings()
        {
            SettingsWindow Dlg = new SettingsWindow(CharacterList, IsGuildChatEnabled, CustomLogFolder, EnableUpdates);
            Dlg.ShowDialog();

            IsGuildChatEnabled = Dlg.IsGuildChatEnabled;
            CustomLogFolder = Dlg.CustomLogFolder;
            EnableUpdates = Dlg.EnableUpdates;
            MainPopup?.UpdateGuildList(CharacterList);

            MainPopup?.OnCommandSettings();
        }

        private void OnCommandClick0()
        {
            OnCommandClick(0);
        }

        private void OnCommandClick1()
        {
            OnCommandClick(1);
        }

        private void OnCommandClick2()
        {
            OnCommandClick(2);
        }

        private void OnCommandClick(int index)
        {
            if (index >= LinkList.Count)
                return;

            string Link = LinkList[index];
            LaunchBrowser(Link);
        }
        #endregion

        #region Chat Log
        public static void LaunchBrowser(string link)
        {
            try
            {
                Process.Start(link);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                link = link.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {link}") { CreateNoWindow = true });
            }
        }

        private void InitChatLog(Dispatcher dispatcher)
        {
            CurrentChat = new Parser();
            CurrentChat.NewLine += OnNewLine;
            /*CurrentChat = new ChatLog(this, CustomLogFolder, dispatcher);
            CurrentChat.StartLogging();*/

            LinkList.CollectionChanged += OnLinkListChanged;
        }

        public string LoginName { get; private set; }

        void OnNewLine(Parser sender, DateTime logTime, string logLine)
        {
            string Line = logLine;

            string LogInPattern = "**************************************** Logged In As ";
            if (Line.StartsWith(LogInPattern))
            {
                LoginName = Line.Substring(LogInPattern.Length);
                OnLoginNameChanged();
                return;
            }

            string LogOutPattern = "**************************************** Logged Out";
            if (Line == LogOutPattern)
            {
                LoginName = null;
                OnLoginNameChanged();
                return;
            }

            if (Line[0] != '[')
                return;

            int Index = Line.IndexOf(']');
            if (Index <= 0)
                return;
            string Channel = Line.Substring(1, Index - 1);
            string Message = Line.Substring(Index + 1).Trim();
            ChannelType Type = StringToChannelType(Channel);

            ParseLink(Message);

            switch (Type)
            {
                case ChannelType.Global:
                case ChannelType.Trade:
                case ChannelType.Help:
                case ChannelType.Guild:
                case ChannelType.Nearby:
                case ChannelType.NPCChatter:
                case ChannelType.Status:
                case ChannelType.Error:
                    AddToLog(logTime, Type, Message);
                    break;
            }
        }

        private void AddToLog(DateTime LogTime, ChannelType Type, string Message)
        {
            Message = Message.Replace('\n', '\t');
            string Hash = "";

            if (Type == ChannelType.Guild)
            {
                if (!IsGuildChatEnabled)
                    return;

                if (string.IsNullOrEmpty(LoginName))
                    return;

                if (Message.StartsWith("(SYSTEM)") || Message.StartsWith("-SYSTEM-"))
                {
                    if (Message[Message.Length - 1] == '"')
                        Message = Message.Substring(0, Message.Length - 1);

                    string PasswordPattern = "PgMessenger:";
                    int PasswordStart = Message.IndexOf(PasswordPattern);

                    if (PasswordStart >= 0)
                    {
                        int i = PasswordStart + PasswordPattern.Length;
                        while (i < Message.Length && char.IsWhiteSpace(Message[i]))
                            i++;

                        string Password = "";
                        while (i < Message.Length && !char.IsWhiteSpace(Message[i]) && i != '\r' && i != '\n')
                            Password += Message[i++].ToString();

                        UpdatePassword(LoginName, Password);
                    }

                    return;
                }
                else
                {
                    string Password = GetPasswordByLoginName(LoginName);
                    if (Password == null)
                        return;

                    try
                    {
                        Hash = MD5Hash.GetHashString(Message);
                        Message = Encryption.AESThenHMAC.SimpleEncryptWithPassword(Message, Password, new byte[0]);
                    }
                    catch
                    {
                        Message = null;
                    }

                    if (Message == null)
                        return;
                }
            }
            else if (Type == ChannelType.Global)
            {
                if (Message.StartsWith("(SYSTEM) [Announcement]: Updating your character") ||
                    Message.StartsWith("(SYSTEM) [Announcement]: Done upgrading your character"))
                    return;
            }

            if (Type == ChannelType.Global || Type == ChannelType.Help || Type == ChannelType.Trade || Type == ChannelType.Guild)
                UploadLog(LoginName != null ? LoginName : "", Type.ToString(), Message, Hash);
        }

        private void ParseLink(string message)
        {
            int Index = message.IndexOf("http://", StringComparison.InvariantCulture);
            if (Index < 0)
                Index = message.IndexOf("https://", StringComparison.InvariantCulture);
            if (Index < 0)
                return;

            int LastIndex = message.IndexOf(" ", Index, StringComparison.InvariantCulture);
            if (LastIndex < 0)
                LastIndex = message.Length;

            string Link = message.Substring(Index, LastIndex - Index);

            if (!LinkList.Contains(Link))
            {
                while (LinkList.Count >= 3)
                    LinkList.RemoveAt(0);
                LinkList.Add(Link);

                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(ShowBalloon), Link);
            }
        }

        private void ShowBalloon(string text)
        {
            TaskbarBalloon.Show(text, TimeSpan.FromSeconds(15), OnClicked, text);
        }

        private void OnClicked(object data)
        {
            LaunchBrowser(data as string);
        }

        public ObservableCollection<string> LinkList { get; } = new ObservableCollection<string>();

        private void OnLinkListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            IsMenuChanged = true;
        }

        private void OnLoginNameChanged()
        {
            MainPopup.LoginName = LoginName;
            IsToolTipChanged = true;
        }

        private void CloseChat()
        {
            CurrentChat.StopLogging();
        }

        public void UpdatePassword(string LoginName, string Password)
        {
            foreach (CharacterSetting Character in CharacterList)
                if (Character.Name == LoginName)
                {
                    if (Character.IsAutoUpdated)
                        Character.Password = Password;
                    break;
                }
        }

        public string GetPasswordByLoginName(string LoginName)
        {
            foreach (CharacterSetting Character in CharacterList)
                if (Character.Name == LoginName && !string.IsNullOrEmpty(Character.Password))
                {
                    string Password = Character.Password;
                    while (Password.Length < 12)
                        Password += "*";

                    return Password;
                }

            return null;
        }

        public string GetPasswordByGuildName(string GuildName)
        {
            foreach (CharacterSetting Character in CharacterList)
                if (Character.GuildName == GuildName && !string.IsNullOrEmpty(Character.Password))
                {
                    string Password = Character.Password;
                    while (Password.Length < 12)
                        Password += "*";

                    return Password;
                }

            return null;
        }

        private Parser CurrentChat;
        #endregion

        #region Settings
        private void LoadSettings()
        {
            LogId = Settings.GetString("LogId", null);
            if (LogId == null)
            {
                LogId = Guid.NewGuid().ToString();
                Settings.SetString("LogId", LogId);
            }

            IsGuildChatEnabled = Settings.GetBool("IsGuildChatEnabled", false);
            CustomLogFolder = Settings.GetString("CustomLogFolder", "");
            EnableUpdates = Settings.GetBool("EnableUpdates", true);

            for (int i = 0; i < 4; i++)
            {
                string Name = Settings.GetString("CharacterName#" + i, "");
                string GuildName = Settings.GetString("GuildName#" + i, "");
                bool IsAutoUpdated = Settings.GetBool("IsAutoUpdated#" + i, false);
                string Password = Settings.GetString("Password#" + i, "");
                CharacterSetting NewCharacter = new CharacterSetting(Name, GuildName, IsAutoUpdated, Password);
                CharacterList.Add(NewCharacter);
            }
        }

        private string GetGuildName(string CharacterName)
        {
            string GuildName = "";

            foreach (CharacterSetting Character in CharacterList)
                if (Character.Name == CharacterName)
                {
                    GuildName = Character.GuildName;
                    break;
                }

            return GuildName;
        }

        private void SaveSettings()
        {
            Settings.SetBool("IsGuildChatEnabled", IsGuildChatEnabled);
            Settings.SetString("CustomLogFolder", CustomLogFolder);
            Settings.SetBool("EnableUpdates", EnableUpdates);

            for (int i = 0; i < 4 && i < CharacterList.Count; i++)
            {
                Settings.SetString("CharacterName#" + i, CharacterList[i].Name);
                Settings.SetString("GuildName#" + i, CharacterList[i].GuildName);
                Settings.SetBool("IsAutoUpdated#" + i, CharacterList[i].IsAutoUpdated);
                Settings.SetString("Password#" + i, CharacterList[i].Password);
            }
        }
        #endregion

        #region Upload & Download
        public void UploadLog(string LoginName, string Channel, string Message, string Hash)
        {
            Dictionary<string, string> Values = new Dictionary<string, string>();
            Values.Add("id", LogId);
            Values.Add("name", LoginName);
            Values.Add("channel", Channel);
            Values.Add("guildname", GetGuildName(LoginName));
            Values.Add("message", Message);
            Values.Add("hash", Hash);

            try
            {
                FormUrlEncodedContent Content = new FormUrlEncodedContent(Values);

                Task<HttpResponseMessage> PostTask = ConnectionClient.PostAsync(ConnectionAddress + "upload_form.php", Content);
                if (PostTask.Wait(2000))
                {
                    HttpResponseMessage Response = PostTask.Result;

                    Task<string> ReadTask = Response.Content.ReadAsStringAsync();
                    if (ReadTask.Wait(2000))
                    {
                        string Result = ReadTask.Result;
                    }
                }
            }
            catch
            {
            }
        }

        public void DownloadLog(string GuildName, ref int RegisteredUserCount, ref int ConnectedUserCount, ref int GuestUserCount, Dictionary<string, int> GuildmateTable, List<string> ChatLineList)
        {
            Dictionary<string, string> Values = new Dictionary<string, string>();
            Values.Add("id", LogId);
            Values.Add("guildname", GuildName);
            Values.Add("index", LastReadIndex.ToString());

            try
            {
                FormUrlEncodedContent Content = new FormUrlEncodedContent(Values);

                Task<HttpResponseMessage> PostTask = ConnectionClient.PostAsync(ConnectionAddress + "download_form.php", Content);
                if (PostTask.Wait(2000))
                {
                    HttpResponseMessage Response = PostTask.Result;

                    Task<string> ReadTask = Response.Content.ReadAsStringAsync();
                    if (ReadTask.Wait(2000))
                    {
                        string Result = ReadTask.Result;
                        string[] Lines = Result.Split('\n');

                        bool IsUserInfoParsed = false;
                        for (int i = 0; i < Lines.Length; i++)
                        {
                            string Line = Lines[i];
                            if (Line.Length < 1 || Line[0] != '*')
                                continue;
                            Line = Line.Substring(1);

                            if (!IsUserInfoParsed)
                            {
                                IsUserInfoParsed = true;
                                ParseUserInfo(Line, ref RegisteredUserCount, ref ConnectedUserCount, ref GuestUserCount, GuildmateTable);
                            }
                            else
                                ChatLineList.Add(Line);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public void SendKeepAlive()
        {
            Dictionary<string, string> Values = new Dictionary<string, string>();
            Values.Add("id", LogId);

            FormUrlEncodedContent Content = new FormUrlEncodedContent(Values);

            try
            {
                Task<HttpResponseMessage> PostTask = ConnectionClient.PostAsync(ConnectionAddress + "keep_alive_form.php", Content);
                if (PostTask.Wait(2000))
                {
                    HttpResponseMessage Response = PostTask.Result;

                    Task<string> ReadTask = Response.Content.ReadAsStringAsync();
                    if (ReadTask.Wait(2000))
                    {
                        string Result = ReadTask.Result;
                    }
                }
            }
            catch
            {
            }
        }

        public void ParseUserInfo(string Line, ref int RegisteredUserCount, ref int ConnectedUserCount, ref int GuestUserCount, Dictionary<string, int> GuildmateTable)
        {
            string[] Parts = Line.Split('/');
            if (Parts.Length >= 4)
            {
                int registered_user_count;
                if (int.TryParse(Parts[0], out registered_user_count))
                    RegisteredUserCount = registered_user_count;

                int connected_user_count;
                if (int.TryParse(Parts[1], out connected_user_count))
                    ConnectedUserCount = connected_user_count;

                int guest_user_count;
                if (int.TryParse(Parts[2], out guest_user_count))
                    GuestUserCount = guest_user_count;

                string[] Guildmates = Parts[3].Split(';');
                foreach (string Guildmate in Guildmates)
                {
                    string[] State = Guildmate.Split('=');
                    if (State.Length == 2)
                    {
                        string GuildmateName = State[0];
                        int Connection = 0;
                        int.TryParse(State[1], out Connection);

                        if (GuildmateName.Length > 0 && !GuildmateTable.ContainsKey(GuildmateName))
                            GuildmateTable.Add(GuildmateName, Connection);
                    }
                }
            }
        }

        public bool ParseMessageInfo(string Line, bool HideSpoilers, bool DisplayGlobal, bool DisplayHelp, bool DisplayTrade, string GuildName, out LogEntry LogEntry)
        {
            LogEntry = null;

            string[] Parts = Line.Split('/');
            if (Parts.Length < 9)
                return false;

            int LineIndex;
            if (!int.TryParse(Parts[0], out LineIndex))
                return false;

            if (LastReadIndex <= LineIndex)
                LastReadIndex = LineIndex + 1;

            int Year, Month, Day, Hour, Minute, Second;
            if (!int.TryParse(Parts[1], out Year) ||
                !int.TryParse(Parts[2], out Month) ||
                !int.TryParse(Parts[3], out Day) ||
                !int.TryParse(Parts[4], out Hour) ||
                !int.TryParse(Parts[5], out Minute) ||
                !int.TryParse(Parts[6], out Second))
                return false;

            DateTime LogTime = new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Utc);
            LogTime = LogTime.ToLocalTime();

            string Channel = Parts[7];
            if (Channel.Length < 2)
                return false;

            Channel = Channel[0].ToString().ToUpper() + Channel.Substring(1);
            ChannelType LogType = StringToChannelType(Channel);
            if (LogType == ChannelType.Other ||
                (LogType == ChannelType.Global && !DisplayGlobal) ||
                (LogType == ChannelType.Help && !DisplayHelp) ||
                (LogType == ChannelType.Trade && !DisplayTrade))
                return false;

            string Message = "";
            for (int j = 8; j < Parts.Length; j++)
            {
                if (j > 8)
                    Message += "/";
                Message += Parts[j];
            }

            if (LogType == ChannelType.Guild)
            {
                string Password = GetPasswordByGuildName(GuildName);
                if (Password == null)
                    return false;

                try
                {
                    Message = Encryption.AESThenHMAC.SimpleDecryptWithPassword(Message, Password);
                }
                catch
                {
                    Message = null;
                }

                if (Message == null)
                    return false;
            }

            string Author;
            int AuthorIndex = Message.IndexOf(':');
            if (AuthorIndex >= 0)
            {
                Author = Message.Substring(0, AuthorIndex);
                Message = Message.Substring(AuthorIndex + 1);
            }
            else
                Author = "";

            List<string> ItemList = new List<string>();

            int ItemIndex = Message.IndexOf('\t');
            if (ItemIndex >= 0)
            {
                string MessageEnd = Message.Substring(ItemIndex + 1);
                Message = Message.Substring(0, ItemIndex);

                for (; ; )
                {
                    if (MessageEnd.Length < 3 || MessageEnd[0] != '[')
                        break;

                    int ItemEndIndex = MessageEnd.IndexOf(']');
                    if (ItemEndIndex < 0)
                        break;

                    string ItemName = MessageEnd.Substring(1, ItemEndIndex - 1);
                    ItemList.Add("[" + ItemName + "]");

                    MessageEnd = MessageEnd.Substring(ItemEndIndex + 1).Trim();
                }
            }

            if (HideSpoilers)
            {
                for (; ; )
                {
                    int SpoilerStartIndex = Message.IndexOf("[[[");
                    if (SpoilerStartIndex < 0)
                        break;

                    int SpoilerEndIndex = Message.IndexOf("]]]", SpoilerStartIndex);
                    if (SpoilerEndIndex < 0)
                        break;

                    Message = Message.Substring(0, SpoilerStartIndex) + "\t" + Message.Substring(SpoilerEndIndex + 3);
                }

                Message = Message.Replace("\t", "[[[Spoiler]]]");
            }

            LogEntry = new LogEntry(LogTime, LogType, Author, Message, ItemList);
            //Debug.Print("Entry added: " + LogTime + ", " + LogType + ", " + Message);
            return true;
        }

        public static ChannelType StringToChannelType(string Channel)
        {
            ChannelType LogType;
            if (Channel == "Global")
                LogType = ChannelType.Global;
            else if (Channel == "Trade")
                LogType = ChannelType.Trade;
            else if (Channel == "Help")
                LogType = ChannelType.Help;
            else if (Channel == "Guild")
                LogType = ChannelType.Guild;
            else if (Channel == "Nearby")
                LogType = ChannelType.Nearby;
            else if (Channel == "NPC Chatter")
                LogType = ChannelType.NPCChatter;
            else if (Channel == "Status")
                LogType = ChannelType.Status;
            else if (Channel == "Error")
                LogType = ChannelType.Error;
            else
                LogType = ChannelType.Other;

            return LogType;
        }

        private string ConnectionAddress = "https://www.numbatsoft.com/pgmessenger/";
        private readonly HttpClient ConnectionClient = new HttpClient();
        private int LastReadIndex;
        #endregion

        #region Update
        public void InitUpdate()
        {
            UpdateLink = null;
        }

        public bool IsUpdateAvailable(string CurrentVersion)
        {
            if (!EnableUpdates)
                return false;

            string ReleasePageAddress = "https://github.com/dlebansais/PgMessenger/releases";

            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                HttpWebRequest Request = WebRequest.Create(ReleasePageAddress) as HttpWebRequest;
                using (WebResponse Response = Request.GetResponse())
                {
                    using (Stream ResponseStream = Response.GetResponseStream())
                    {
                        using (StreamReader Reader = new StreamReader(ResponseStream, Encoding.ASCII))
                        {
                            string Content = Reader.ReadToEnd();

                            string Pattern = @"<a href=""/dlebansais/PgMessenger/releases/tag/";
                            int Index = Content.IndexOf(Pattern);
                            if (Index >= 0)
                            {
                                string UpdateTagVersion = Content.Substring(Index + Pattern.Length, 20);
                                int EndIndex = UpdateTagVersion.IndexOf('"');
                                if (EndIndex > 0)
                                {
                                    UpdateTagVersion = UpdateTagVersion.Substring(0, EndIndex);

                                    string UpdateVersion;

                                    if (UpdateTagVersion.ToLower().StartsWith("v"))
                                        UpdateVersion = UpdateTagVersion.Substring(1);
                                    else
                                        UpdateVersion = UpdateTagVersion;

                                    string[] UpdateSplit = UpdateVersion.Split('.');
                                    string[] CurrentSplit = CurrentVersion.Split('.');
                                    for (int i = 0; i < UpdateSplit.Length && i < CurrentSplit.Length; i++)
                                    {
                                        int UpdateValue, CurrentValue;
                                        if (!int.TryParse(UpdateSplit[i], out UpdateValue) || !int.TryParse(CurrentSplit[i], out CurrentValue))
                                            break;
                                        else if (UpdateValue < CurrentValue)
                                            break;
                                        else if (UpdateValue > CurrentValue)
                                        {
                                            UpdateLink = "https://github.com/dlebansais/PgMessenger/releases/download/" + UpdateTagVersion + "/PgMessenger.exe";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }

            return UpdateLink != null;
        }

        public string UpdateLink { get; private set; }
        #endregion

        #region Implementation of ITaskbarClient
        public event EventHandler ClientClosed;

        public bool IsOpen { get { return MainPopup.IsVisible; } }
        public IInputElement Input { get { return MainPopup; } }

        public void Open()
        {
            MainPopup.Show();
        }
        /*
        public void OnMenuOpened(System.Windows.Forms.ToolStripItemCollection Items, IReadOnlyDictionary<System.Windows.Forms.ToolStripMenuItem, ICommand> CommandTable)
        {
            bool BringSettingsWindowToForeground = false;

            foreach (object Item in Items)
            {
                System.Windows.Forms.ToolStripMenuItem AsMenuItem;
                if ((AsMenuItem = Item as System.Windows.Forms.ToolStripMenuItem) != null)
                    if (CommandTable.ContainsKey(AsMenuItem))
                    {
                        ICommand Command = CommandTable[AsMenuItem];
                        if (Command == SettingsCommand || Command == ExitCommand)
                        {
                            if (SettingsWindow.OpenedSettings != null)
                            {
                                AsMenuItem.Enabled = false;
                                BringSettingsWindowToForeground = true;
                            }
                            else
                                AsMenuItem.Enabled = true;
                        }
                    }
            }

            if (BringSettingsWindowToForeground)
                SetForeground(SettingsWindow.OpenedSettings);
        }
        */
        public void SetForeground()
        {
            SetForeground(MainPopup);
        }

        public void NotifyClientClosed(EventArgs e)
        {
            ClientClosed?.Invoke(this, e);
        }
        #endregion

        #region Window Handle Management
        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        const int SWP_NOMOVE = 0x0002;
        const int SWP_NOSIZE = 0x0001;
        const int SWP_SHOWWINDOW = 0x0040;
        const int SWP_NOACTIVATE = 0x0010;
        const int HWND_TOPMOST = -1;

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        public void SetForeground(Window Window)
        {
            HwndSource source = (HwndSource)HwndSource.FromVisual(Window);
            IntPtr WindowHandle = source.Handle;
            SetForegroundWindow(WindowHandle);
        }
        #endregion

        #region Implementation of IDisposable
        /// <summary>
        /// Called when an object should release its resources.
        /// </summary>
        /// <param name="isDisposing">Indicates if resources must be disposed now.</param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (isDisposing)
                    DisposeNow();
            }
        }

        /// <summary>
        /// Called when an object should release its resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PgMoonPlugin"/> class.
        /// </summary>
        ~PgMessengerPlugin()
        {
            Dispose(false);
        }

        /// <summary>
        /// True after <see cref="Dispose(bool)"/> has been invoked.
        /// </summary>
        private bool IsDisposed;

        /// <summary>
        /// Disposes of every reference that must be cleaned up.
        /// </summary>
        private void DisposeNow()
        {
            using (Settings)
            {
            }
        }
        #endregion
    }
}
