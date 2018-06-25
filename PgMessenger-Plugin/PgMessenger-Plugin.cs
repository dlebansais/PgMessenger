using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
using TaskbarIconHost;

namespace PgMessenger
{
    public class PgMessengerPlugin : TaskbarIconHost.IPluginClient
    {
        #region Plugin
        public string Name
        {
            get { return PluginDetails.Name; }
        }

        public Guid Guid
        {
            get { return PluginDetails.Guid; }
        }

        public bool RequireElevated
        {
            get { return false; }
        }

        public bool HasClickHandler
        {
            get { return true; }
        }

        public void Initialize(bool isElevated, Dispatcher dispatcher, TaskbarIconHost.IPluginSettings settings, TaskbarIconHost.IPluginLogger logger)
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

            InitializeCommand("Restore Window",
                              isVisibleHandler: () => true,
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandRestoreWindow);

            InitializeCommand("Settings...",
                              isVisibleHandler: () => true,
                              isEnabledHandler: () => true,
                              isCheckedHandler: () => false,
                              commandHandler: OnCommandSettings);

            InitChatLog();
        }

        private void InitializeCommand(string header, Func<bool> isVisibleHandler, Func<bool> isEnabledHandler, Func<bool> isCheckedHandler, Action commandHandler)
        {
            ICommand Command = new RoutedUICommand();
            CommandList.Add(Command);
            MenuHeaderTable.Add(Command, header);
            MenuIsVisibleTable.Add(Command, isVisibleHandler);
            MenuIsEnabledTable.Add(Command, isEnabledHandler);
            MenuIsCheckedTable.Add(Command, isCheckedHandler);
            MenuHandlerTable.Add(Command, commandHandler);
        }

        public List<ICommand> CommandList { get; private set; } = new List<ICommand>();

        public bool GetIsMenuChanged(bool beforeMenuOpening)
        {
            return false;
        }

        public string GetMenuHeader(ICommand Command)
        {
            return MenuHeaderTable[Command];
        }

        public bool GetMenuIsVisible(ICommand Command)
        {
            return MenuIsVisibleTable[Command]();
        }

        public bool GetMenuIsEnabled(ICommand Command)
        {
            return MenuIsEnabledTable[Command]();
        }

        public bool GetMenuIsChecked(ICommand Command)
        {
            return MenuIsCheckedTable[Command]();
        }

        public Bitmap GetMenuIcon(ICommand Command)
        {
            return null;
        }

        public void OnMenuOpening()
        {
        }

        public void OnExecuteCommand(ICommand Command)
        {
            MenuHandlerTable[Command]();
        }

        public bool GetIsIconChanged()
        {
            return false;
        }

        public Icon Icon { get { return LoadEmbeddedResource<Icon>("Taskbar.ico", Logger); } }
        public Bitmap SelectionBitmap { get { return LoadEmbeddedResource<Bitmap>("PgMessenger.png", Logger); } }

        public void OnIconClicked()
        {
            MainPopup.IconClicked();
        }

        public bool GetIsToolTipChanged()
        {
            bool Result = IsToolTipChanged || MainPopup.GetIsToolTipChanged();
            IsToolTipChanged = false;

            return Result;
        }

        public string ToolTip
        {
            get
            {
                string Result = "Project: Gorgon Messenger";

                Result += "\r\n" + MainPopup.ConnectedUserCount.ToString() + " Connected, " + MainPopup.GuestUserCount.ToString() + " Guest(s)";
                if (CurrentChat != null)
                    if (CurrentChat.LoginName != null)
                        Result += "\r\n" + "You are online as " + CurrentChat.LoginName;
                    else
                        Result += "\r\n" + "You are not online";

                return Result;
            }
        }

        public void OnActivated()
        {
        }

        public void OnDeactivated()
        {
        }

        public bool CanClose(bool canClose)
        {
            return true;
        }

        public void BeginClose()
        {
            MainPopup.Close();
            SaveSettings();

            using (MainWindow Popup = MainPopup)
            {
                MainPopup = null;
            }

            using (ChatLog Chat = CurrentChat)
            {
                CurrentChat = null;
            }
        }

        public bool IsClosed
        {
            get { return true; }
        }

        public bool IsElevated { get; private set; }
        public Dispatcher Dispatcher { get; private set; }
        public TaskbarIconHost.IPluginSettings Settings { get; private set; }
        public TaskbarIconHost.IPluginLogger Logger { get; private set; }

        public static T LoadEmbeddedResource<T>(string resourceName, TaskbarIconHost.IPluginLogger logger)
        {
            // Loads an "Embedded Resource" of type T (ex: Bitmap for a PNG file).
            foreach (string ResourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (ResourceName.EndsWith(resourceName))
                {
                    using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        T Result = (T)Activator.CreateInstance(typeof(T), rs);
                        logger.AddLog($"Resource {resourceName} loaded");

                        return Result;
                    }
                }

            logger.AddLog($"Resource {resourceName} not found");
            return default(T);
        }

        public MainWindow MainPopup { get; private set; }
        private Dictionary<ICommand, string> MenuHeaderTable = new Dictionary<ICommand, string>();
        private Dictionary<ICommand, Func<bool>> MenuIsVisibleTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsEnabledTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsCheckedTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Action> MenuHandlerTable = new Dictionary<ICommand, Action>();
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
        private void OnCommandRestoreWindow()
        {
            MainPopup.OnCommandRestoreWindow();
        }

        private void OnCommandSettings()
        {
            SettingsWindow Dlg = new SettingsWindow(CharacterList, IsGuildChatEnabled, CustomLogFolder, EnableUpdates);
            Dlg.ShowDialog();

            IsGuildChatEnabled = Dlg.IsGuildChatEnabled;
            CustomLogFolder = Dlg.CustomLogFolder;
            EnableUpdates = Dlg.EnableUpdates;
            MainPopup.UpdateGuildList(CharacterList);
            CurrentChat.SetCustomLogFolder(CustomLogFolder);

            MainPopup.OnCommandSettings();
        }
        #endregion

        #region Chat Log
        private void InitChatLog()
        {
            CurrentChat = new ChatLog(this, CustomLogFolder);
            CurrentChat.LoginNameChanged += OnLoginNameChanged;
            CurrentChat.StartLogging();
        }

        private void OnLoginNameChanged(object sender, EventArgs e)
        {
            string LoginName = CurrentChat.LoginName;
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

        private ChatLog CurrentChat;
        #endregion

        #region Settings
        private void LoadSettings()
        {
            LogId = Settings.GetSettingString("LogId", null);
            if (LogId == null)
            {
                LogId = Guid.NewGuid().ToString();
                Settings.SetSettingString("LogId", LogId);
            }

            IsGuildChatEnabled = Settings.GetSettingBool("IsGuildChatEnabled", false);
            CustomLogFolder = Settings.GetSettingString("CustomLogFolder", "");
            EnableUpdates = Settings.GetSettingBool("EnableUpdates", true);

            for (int i = 0; i < 4; i++)
            {
                string Name = Settings.GetSettingString("CharacterName#" + i, "");
                string GuildName = Settings.GetSettingString("GuildName#" + i, "");
                bool IsAutoUpdated = Settings.GetSettingBool("IsAutoUpdated#" + i, false);
                string Password = Settings.GetSettingString("Password#" + i, "");
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
            Settings.SetSettingBool("IsGuildChatEnabled", IsGuildChatEnabled);
            Settings.SetSettingString("CustomLogFolder", CustomLogFolder);
            Settings.SetSettingBool("EnableUpdates", EnableUpdates);

            for (int i = 0; i < 4 && i < CharacterList.Count; i++)
            {
                Settings.SetSettingString("CharacterName#" + i, CharacterList[i].Name);
                Settings.SetSettingString("GuildName#" + i, CharacterList[i].GuildName);
                Settings.SetSettingBool("IsAutoUpdated#" + i, CharacterList[i].IsAutoUpdated);
                Settings.SetSettingString("Password#" + i, CharacterList[i].Password);
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
            ChannelType LogType = ChatLog.StringToChannelType(Channel);
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

        private string ConnectionAddress = "http://www.enu.numbatsoft.com/pgmessenger/";
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
                                            UpdateLink = "https://github.com/dlebansais/PgMessenger/releases/download/" + UpdateTagVersion + "/PgMessenger.zip";
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
    }
}
