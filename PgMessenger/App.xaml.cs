using Microsoft.Win32;
using SchedulerTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TaskbarTools;

namespace PgMessenger
{
    public partial class App : Application, ITaskbarClient, IDisposable
    {
        #region Init
        static App()
        {
            InitSettings();
            LoadSettings();
            InitUpdate();
            LastReadIndex = -1;
        }

        public App()
        {
            // Ensure only one instance is running at a time.
            try
            {
                bool createdNew;
                InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "{2301E527-A27B-4D03-A758-C6D7E4AFB436}", out createdNew);
                if (!createdNew)
                {
                    InstanceEvent.Close();
                    InstanceEvent = null;
                    Shutdown();
                    return;
                }
            }
            catch
            {
                Shutdown();
                return;
            }

            Taskbar.UpdateLocation();
            Startup += OnStartup;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        public MainWindow MainPopup { get; private set; }
        private EventWaitHandle InstanceEvent;
        #endregion

        #region Properties
        public static string LogId { get; private set; }
        public static List<CharacterSetting> CharacterList { get; private set; } = new List<CharacterSetting>();
        public static bool IsGuildChatEnabled { get; private set; }
        public static string CustomLogFolder { get; private set; }
        public static bool EnableUpdates { get; private set; }

        public bool IsElevated
        {
            get
            {
                WindowsIdentity wi = WindowsIdentity.GetCurrent();
                if (wi != null)
                {
                    WindowsPrincipal wp = new WindowsPrincipal(wi);
                    if (wp != null)
                        return wp.IsInRole(WindowsBuiltInRole.Administrator);
                }

                return false;
            }
        }

        public string ToolTipText
        {
            get
            {
                string Result = "Project: Gorgon Messenger";

                if (MainPopup != null)
                {
                    Result += "\r\n" + MainPopup.ConnectedUserCount.ToString() + " Connected, " + MainPopup.GuestUserCount.ToString() + " Guest(s)";
                    if (CurrentChat != null)
                        if (CurrentChat.LoginName != null)
                            Result += "\r\n" + "You are online as " + CurrentChat.LoginName;
                        else
                            Result += "\r\n" + "You are not online";
                }

                return Result;
            }
        }
        #endregion

        #region Load at startup
        private void InstallLoad(bool Install)
        {
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (Install)
                Scheduler.AddTask(MainPopup.Title, ExeName);
            else
                Scheduler.RemoveTask(ExeName);
        }
        #endregion

        #region Taskbar Icon
        private void InitTaskbarIcon()
        {
            MenuHeaderTable = new Dictionary<ICommand, string>();
            LoadAtStartupCommand = InitMenuCommand("LoadAtStartupCommand", LoadAtStartupHeader);
            RestoreWindowCommand = InitMenuCommand("RestoreWindowCommand", "Restore Window");
            SettingsCommand = InitMenuCommand("SettingsCommand", "Settings...");
            ExitCommand = InitMenuCommand("ExitCommand", "Exit");

            System.Drawing.Icon Icon = LoadIcon("Taskbar.ico");
            ContextMenu ContextMenu = LoadContextMenu();

            TaskbarIcon = TaskbarIcon.Create(Icon, ToolTipText, ContextMenu, this);
            TaskbarIcon.MenuOpening += OnMenuOpening;
        }

        private ICommand InitMenuCommand(string CommandName, string Header)
        {
            ICommand Command = MainPopup.FindResource(CommandName) as ICommand;
            MenuHeaderTable.Add(Command, Header);
            return Command;
        }

        private System.Drawing.Icon LoadIcon(string IconName)
        {
            foreach (string ResourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (ResourceName.EndsWith(IconName))
                {
                    using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        System.Drawing.Icon Result = new System.Drawing.Icon(rs);
                        return Result;
                    }
                }

            return null;
        }

        private System.Drawing.Bitmap LoadBitmap(string IconName)
        {
            foreach (string ResourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (ResourceName.EndsWith(IconName))
                {
                    using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        System.Drawing.Bitmap Result = new System.Drawing.Bitmap(rs);
                        return Result;
                    }
                }

            return null;
        }

        private ContextMenu LoadContextMenu()
        {
            ContextMenu Result = new ContextMenu();

            MenuItem LoadAtStartup;
            string ExeName = Assembly.GetExecutingAssembly().Location;
            if (Scheduler.IsTaskActive(ExeName))
            {
                if (IsElevated)
                {
                    LoadAtStartup = LoadNotificationMenuItem(LoadAtStartupCommand);
                    LoadAtStartup.IsChecked = true;
                }
                else
                {
                    LoadAtStartup = LoadNotificationMenuItem(LoadAtStartupCommand, RemoveFromStartupHeader);
                    LoadAtStartup.Icon = LoadBitmap("UAC-16.png");
                }
            }
            else
            {
                LoadAtStartup = LoadNotificationMenuItem(LoadAtStartupCommand);

                if (!IsElevated)
                    LoadAtStartup.Icon = LoadBitmap("UAC-16.png");
            }

            MenuItem RestoreWindowMenu = LoadNotificationMenuItem(RestoreWindowCommand);
            MenuItem SettingsMenu = LoadNotificationMenuItem(SettingsCommand);
            MenuItem ExitMenu = LoadNotificationMenuItem(ExitCommand);

            AddContextMenu(Result, LoadAtStartup, true);
            AddContextMenuSeparator(Result);
            AddContextMenu(Result, RestoreWindowMenu, true);
            AddContextMenu(Result, SettingsMenu, true);
            AddContextMenuSeparator(Result);
            AddContextMenu(Result, ExitMenu, true);

            return Result;
        }

        private MenuItem LoadNotificationMenuItem(ICommand Command)
        {
            MenuItem Result = new MenuItem();
            Result.Header = MenuHeaderTable[Command];
            Result.Command = Command;
            Result.Icon = null;

            return Result;
        }

        private MenuItem LoadNotificationMenuItem(ICommand Command, string MenuHeader)
        {
            MenuItem Result = new MenuItem();
            Result.Header = MenuHeader;
            Result.Command = Command;
            Result.Icon = null;

            return Result;
        }

        private void AddContextMenu(ContextMenu Menu, MenuItem Item, bool IsVisible)
        {
            Item.Visibility = IsVisible ? Visibility.Visible : Visibility.Collapsed;
            Menu.Items.Add(Item);
        }

        private void AddContextMenuSeparator(ContextMenu Menu)
        {
            Menu.Items.Add(new Separator());
        }

        private void OnMenuOpening(object sender, EventArgs e)
        {
            TaskbarIcon SenderIcon = sender as TaskbarIcon;
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (IsElevated)
                SenderIcon.Check(LoadAtStartupCommand, Scheduler.IsTaskActive(ExeName));
            else
            {
                if (Scheduler.IsTaskActive(ExeName))
                    SenderIcon.SetText(LoadAtStartupCommand, RemoveFromStartupHeader);
                else
                    SenderIcon.SetText(LoadAtStartupCommand, LoadAtStartupHeader);
            }
        }

        private void CloseTaskbarIcon()
        {
        }

        public void UpdateToolTipText()
        {
            TaskbarIcon.UpdateToolTipText(ToolTipText);
        }

        private TaskbarIcon TaskbarIcon;
        private static readonly string LoadAtStartupHeader = "Load at startup";
        private static readonly string RemoveFromStartupHeader = "Remove from startup";
        private ICommand LoadAtStartupCommand;
        private ICommand RestoreWindowCommand;
        private ICommand SettingsCommand;
        private ICommand ExitCommand;
        private Dictionary<ICommand, string> MenuHeaderTable;
        #endregion

        #region Events
        private void OnStartup(object sender, StartupEventArgs e)
        {
            MainPopup = new MainWindow();
            MainPopup.UpdateGuildList(CharacterList);
            Exit += OnExit;

            InitChatLog();
            InitTaskbarIcon();
        }

        public void OnLoadAtStartup(object sender, ExecutedRoutedEventArgs e)
        {
            TaskbarIcon SenderIcon = e.Parameter as TaskbarIcon;

            if (IsElevated)
            {
                bool IsChecked;
                if (SenderIcon.ToggleChecked(e.Command, out IsChecked))
                    InstallLoad(IsChecked);
            }
            else
            {
                string ExeName = Assembly.GetExecutingAssembly().Location;

                if (Scheduler.IsTaskActive(ExeName))
                {
                    RemoveFromStartupWindow Dlg = new RemoveFromStartupWindow();
                    Dlg.ShowDialog();
                }
                else
                {
                    LoadAtStartupWindow Dlg = new LoadAtStartupWindow();
                    Dlg.ShowDialog();
                }
            }
        }

        public void OnSettings()
        {
            SettingsWindow Dlg = new SettingsWindow(CharacterList, IsGuildChatEnabled, CustomLogFolder, EnableUpdates);
            Dlg.ShowDialog();

            IsGuildChatEnabled = Dlg.IsGuildChatEnabled;
            CustomLogFolder = Dlg.CustomLogFolder;
            EnableUpdates = Dlg.EnableUpdates;
            MainPopup.UpdateGuildList(CharacterList);
            CurrentChat.SetCustomLogFolder(CustomLogFolder);
        }

        public void OnClose()
        {
            MainPopup.Hide();
        }

        public void OnExit()
        {
            MainPopup.Close();
            Shutdown();
        }

        public void OnExit(object sender, ExitEventArgs e)
        {
            if (InstanceEvent != null)
            {
                InstanceEvent.Close();
                InstanceEvent = null;
            }

            CloseChat();
            CloseTaskbarIcon();
            SaveSettings();

            using (TaskbarIcon Icon = TaskbarIcon)
            {
                TaskbarIcon = null;
            }

            using (MainWindow Popup = MainPopup)
            {
                MainPopup = null;
            }
        }
        #endregion

        #region Settings
        private static void InitSettings()
        {
            try
            {
                RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software", true);
                Key = Key.CreateSubKey("Project Gorgon Tools");
                SettingKey = Key.CreateSubKey("PgMessenger");
            }
            catch
            {
            }
        }

        private static void LoadSettings()
        {
            LogId = GetSettingString("LogId", null);
            if (LogId == null)
            {
                LogId = Guid.NewGuid().ToString();
                SetSettingString("LogId", LogId);
            }

            IsGuildChatEnabled = GetSettingBool("IsGuildChatEnabled", false);
            CustomLogFolder = GetSettingString("CustomLogFolder", "");
            EnableUpdates = GetSettingBool("EnableUpdates", true);

            for (int i = 0; i < 4; i++)
            {
                string Name = GetSettingString("CharacterName#" + i, "");
                string GuildName = GetSettingString("GuildName#" + i, "");
                bool IsAutoUpdated = GetSettingBool("IsAutoUpdated#" + i, false);
                string Password = GetSettingString("Password#" + i, "");
                CharacterSetting NewCharacter = new CharacterSetting(Name, GuildName, IsAutoUpdated, Password);
                CharacterList.Add(NewCharacter);
            }
        }

        private static string GetGuildName(string CharacterName)
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

        private static void SaveSettings()
        {
            SetSettingBool("IsGuildChatEnabled", IsGuildChatEnabled);
            SetSettingString("CustomLogFolder", CustomLogFolder);
            SetSettingBool("EnableUpdates", EnableUpdates);

            for (int i = 0; i < 4 && i < CharacterList.Count; i++)
            {
                SetSettingString("CharacterName#" + i, CharacterList[i].Name);
                SetSettingString("GuildName#" + i, CharacterList[i].GuildName);
                SetSettingBool("IsAutoUpdated#" + i, CharacterList[i].IsAutoUpdated);
                SetSettingString("Password#" + i, CharacterList[i].Password);
            }
        }

        private static object GetSettingKey(string ValueName)
        {
            try
            {
                return SettingKey?.GetValue(ValueName);
            }
            catch
            {
                return null;
            }
        }

        private static void SetSettingKey(string ValueName, object Value, RegistryValueKind Kind)
        {
            try
            {
                SettingKey?.SetValue(ValueName, Value, Kind);
            }
            catch
            {
            }
        }

        private static void DeleteSetting(string ValueName)
        {
            try
            {
                SettingKey?.DeleteValue(ValueName, false);
            }
            catch
            {
            }
        }

        public static bool IsBoolKeySet(string ValueName)
        {
            int? Value = GetSettingKey(ValueName) as int?;
            return Value.HasValue;
        }

        public static bool GetSettingBool(string ValueName, bool Default)
        {
            int? Value = GetSettingKey(ValueName) as int?;
            return Value.HasValue ? (Value.Value != 0) : Default;
        }

        public static void SetSettingBool(string ValueName, bool Value)
        {
            SetSettingKey(ValueName, Value ? 1 : 0, RegistryValueKind.DWord);
        }

        public static int GetSettingInt(string ValueName, int Default)
        {
            int? Value = GetSettingKey(ValueName) as int?;
            return Value.HasValue ? Value.Value : Default;
        }

        public static void SetSettingInt(string ValueName, int Value)
        {
            SetSettingKey(ValueName, Value, RegistryValueKind.DWord);
        }

        public static double GetSettingDouble(string ValueName, double Default)
        {
            byte[] Value = GetSettingKey(ValueName) as byte[];
            if (Value != null && Value.Length == 8)
                return BitConverter.ToDouble(Value, 0);
            else
                return Default;
        }

        public static void SetSettingDouble(string ValueName, double Value)
        {
            SetSettingKey(ValueName, BitConverter.GetBytes(Value), RegistryValueKind.Binary);
        }

        public static string GetSettingString(string ValueName, string Default)
        {
            string Value = GetSettingKey(ValueName) as string;
            return Value != null ? Value : Default;
        }

        public static void SetSettingString(string ValueName, string Value)
        {
            if (Value == null)
                DeleteSetting(ValueName);
            else
                SetSettingKey(ValueName, Value, RegistryValueKind.String);
        }

        private static RegistryKey SettingKey = null;
        #endregion

        #region Chat Log
        private void InitChatLog()
        {
            CurrentChat = new ChatLog(CustomLogFolder);
            CurrentChat.LoginNameChanged += OnLoginNameChanged;
            CurrentChat.StartLogging();
        }

        private void OnLoginNameChanged(object sender, EventArgs e)
        {
            string LoginName = CurrentChat.LoginName;
            MainPopup.LoginName = LoginName;
            TaskbarIcon.UpdateToolTipText(ToolTipText);
        }

        private void CloseChat()
        {
            CurrentChat.StopLogging();
        }

        public static void UpdatePassword(string LoginName, string Password)
        {
            foreach (CharacterSetting Character in CharacterList)
                if (Character.Name == LoginName)
                {
                    if (Character.IsAutoUpdated)
                        Character.Password = Password;
                    break;
                }
        }

        public static string GetPasswordByLoginName(string LoginName)
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

        public static string GetPasswordByGuildName(string GuildName)
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

        #region Upload & Download
        public static void UploadLog(string LoginName, string Channel, string Message, string Hash)
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

        public static void DownloadLog(string GuildName, ref int RegisteredUserCount, ref int ConnectedUserCount, ref int GuestUserCount, Dictionary<string, int> GuildmateTable, List<string> ChatLineList)
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

        public static void SendKeepAlive()
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

        public static void ParseUserInfo(string Line, ref int RegisteredUserCount, ref int ConnectedUserCount, ref int GuestUserCount, Dictionary<string, int> GuildmateTable)
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

        public static bool ParseMessageInfo(string Line, bool HideSpoilers, bool DisplayGlobal, bool DisplayHelp, bool DisplayTrade, string GuildName, out LogEntry LogEntry)
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

                for(;;)
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
                for (;;)
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

        private static string ConnectionAddress = "http://www.enu.numbatsoft.com/pgmessenger/";
        private static readonly HttpClient ConnectionClient = new HttpClient();
        private static int LastReadIndex;
        #endregion

        #region Update
        public static void InitUpdate()
        {
            UpdateLink = null;
        }

        public static bool IsUpdateAvailable(string CurrentVersion)
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

        public static string UpdateLink { get; private set; }
        #endregion

        #region Implementation of ITaskbarClient
        public event EventHandler ClientClosed;

        public bool IsOpen { get { return MainPopup.IsVisible; } }
        public IInputElement Input { get { return MainPopup; } }

        public void Open()
        {
            MainPopup.Show();
        }

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

        public static void SetForeground(Window Window)
        {
            HwndSource source = (HwndSource)HwndSource.FromVisual(Window);
            IntPtr WindowHandle = source.Handle;
            SetForegroundWindow(WindowHandle);
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
            if (InstanceEvent != null)
            {
                InstanceEvent.Close();
                InstanceEvent = null;
            }

            using (TaskbarIcon ToRemove = TaskbarIcon)
            {
                TaskbarIcon = null;
            }

            using (ChatLog Chat = CurrentChat)
            {
                CurrentChat = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~App()
        {
            Dispose(false);
        }
        #endregion
    }
}
