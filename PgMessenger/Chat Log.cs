using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace PgMessenger
{
    public class ChatLog : IDisposable
    {
        #region Constants
        private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(5);
        #endregion

        #region Init
        public ChatLog()
        {
            Guid localLowId = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
            string ChatLogPath = GetKnownFolderPath(localLowId);
            LogFolder = Path.Combine(ChatLogPath, @"Elder Game\Project Gorgon\ChatLogs");
        }

        private string GetKnownFolderPath(Guid knownFolderId)
        {
            IntPtr pszPath = IntPtr.Zero;
            try
            {
                int hr = SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pszPath);
                if (hr >= 0)
                    return Marshal.PtrToStringAuto(pszPath);
                throw Marshal.GetExceptionForHR(hr);
            }
            finally
            {
                if (pszPath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pszPath);
            }
        }

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);
        #endregion

        #region Properties
        public string LogFolder { get; private set; }
        public bool IsStarting { get; private set; }
        public string LoginName { get; private set; }
        public DateTime LastLogCheck { get; private set; }
        public Stopwatch KeepAlive { get; private set; }
        #endregion

        #region Client Interface
        public void StartLogging()
        {
            IsStarting = true;
            LastLogCheck = DateTime.Now;
            KeepAlive = new Stopwatch();
            WatcherTimer = new Timer(new TimerCallback(WatcherTimerCallback));
            WatcherTimer.Change(PollDelay, Timeout.InfiniteTimeSpan);
        }

        private void WatcherTimerCallback(object Parameter)
        {
            DateTime Now = DateTime.Now;
            if (LastLogCheck.Day != Now.Day)
            {
                LastLogCheck = Now;
                Disconnect();
            }

            if (LogStream == null)
                TryConnecting();

            if (LogStream != null)
                OnChanged();

            WatcherTimer?.Change(PollDelay, Timeout.InfiniteTimeSpan);
        }

        public void TryConnecting()
        {
            DateTime Now = DateTime.Now;
            string LogFile = "Chat-" + (Now.Year % 100).ToString() + "-" + Now.Month.ToString("D2") + "-" + Now.Day.ToString("D2") + ".log";
            string LogFilePath = Path.Combine(LogFolder, LogFile);

            if (File.Exists(LogFilePath))
            {
                try
                {
                    LogStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write);
                    if (IsStarting)
                    {
                        IsStarting = false;
                        LogStream.Seek(0, SeekOrigin.End);
                        KeepAlive.Start();
                    }
                }
                catch
                {
                    LogStream = null;
                }
            }
        }

        private void OnChanged()
        {
            long OldPosition = LogStream.Position;
            long NewPosition = LogStream.Length;

            if (NewPosition > OldPosition)
            {
                int Length = (int)(NewPosition - OldPosition);
                byte[] Content = new byte[Length];
                LogStream.Read(Content, 0, Length);

                char[] LineContent = new char[Content.Length];
                for (int i = 0; i < Length; i++)
                    LineContent[i] = (char)Content[i];

                string ExtractedLines = new string(LineContent);

                string[] Lines = ExtractedLines.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                for (int i = 0; i < Lines.Length; i++)
                {
                    string Line = Lines[i];

                    while (Line.Length > 0 && (Line[Line.Length - 1] == '\r' || Line[Line.Length - 1] == '\n'))
                        Line = Line.Substring(0, Line.Length - 1);

                    Lines[i] = Line;
                }

                for (int i = 0; i < Lines.Length; i++)
                    LogString(Lines[i]);

                KeepAlive.Restart();
            }

            else if (KeepAlive.Elapsed > KeepAliveTimeout)
            {
                if (LoginName != null)
                    App.SendKeepAlive();

                KeepAlive.Restart();
            }
        }

        private void LogString(string Line)
        {
            if (Line.Length <= 20 || Line[17] != '\t')
                return;

            int Year, Month, Day, Hour, Minute, Second;
            if (!int.TryParse(Line.Substring(0, 2), out Year) ||
                !int.TryParse(Line.Substring(3, 2), out Month) ||
                !int.TryParse(Line.Substring(6, 2), out Day) ||
                !int.TryParse(Line.Substring(9, 2), out Hour) ||
                !int.TryParse(Line.Substring(12, 2), out Minute) ||
                !int.TryParse(Line.Substring(15, 2), out Second))
                return;

            DateTime LogTime = new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Local);

            Line = Line.Substring(18);

            string LogInPattern = "**************************************** Logged In As ";
            if (Line.StartsWith(LogInPattern))
            {
                LoginName = Line.Substring(LogInPattern.Length);
                NotifyLoginNameChanged();
                return;
            }

            string LogOutPattern = "**************************************** Logged Out";
            if (Line == LogOutPattern)
            {
                LoginName = null;
                NotifyLoginNameChanged();
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
                    AddToLog(LogTime, Type, Message);
                    break;
            }
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

        private void AddToLog(DateTime LogTime, ChannelType Type, string Message)
        {
            Message = Message.Replace('\n', '\t');

            if (Type == ChannelType.Guild)
            {
                if (!App.IsGuildChatEnabled)
                    return;

                if (string.IsNullOrEmpty(LoginName))
                    return;

                if (Message.StartsWith("(SYSTEM)"))
                {
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

                        App.UpdatePassword(LoginName, Password);
                    }

                    return;
                }
                else
                {
                    string Password = App.GetPasswordByLoginName(LoginName);
                    if (Password == null)
                        return;

                    try
                    {
                        Message = Encryption.AESThenHMAC.SimpleEncryptWithPassword(Message, Password);
                    }
                    catch
                    {
                        Message = null;
                    }

                    if (Message == null)
                        return;
                }
            }

            if (Type == ChannelType.Global || Type == ChannelType.Help || Type == ChannelType.Trade || Type == ChannelType.Guild)
                App.UploadLog(LoginName != null ? LoginName : "", Type.ToString(), Message);
        }

        public void StopLogging()
        {
            if (WatcherTimer != null)
            {
                WatcherTimer.Dispose();
                WatcherTimer = null;
            }

            Disconnect();
        }

        public void Disconnect()
        {
            if (LogStream != null)
                using (FileStream fs = LogStream) { }
        }

        public event EventHandler LoginNameChanged;

        private void NotifyLoginNameChanged()
        {
            LoginNameChanged?.Invoke(this, EventArgs.Empty);
        }

        private FileStream LogStream;
        private Timer WatcherTimer;
        #endregion

        #region Implementation of IDisposable
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
                DisposeNow();
        }

        private void DisposeNow()
        {
            StopLogging();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ChatLog()
        {
            Dispose(false);
        }
        #endregion
    }
}
