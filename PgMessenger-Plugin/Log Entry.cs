using System;
using System.Collections.Generic;

namespace PgMessenger
{
    public class LogEntry
    {
        #region Init
        public LogEntry(DateTime logTime, ChannelType type, string author, string message, List<string> itemList)
        {
            LogTime = logTime;
            Type = type;
            Author = author;
            Message = message;
            ItemList = itemList;
        }
        #endregion

        #region Properties
        public DateTime LogTime { get; private set; }
        public ChannelType Type { get; private set; }
        public string Author { get; private set; }
        public string Message { get; private set; }
        public List<string> ItemList { get; private set; }
        #endregion

        #region Overrides
        public override string ToString()
        {
            string Result = LogTime.ToString() + " [" + Type.ToString() + "] " + Author + ": " + Message;
            foreach (string ItemName in ItemList)
                Result += " [" + ItemName + "]";

            return Result;
        }
        #endregion
    }
}
