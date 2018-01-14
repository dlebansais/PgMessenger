using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PgMessenger
{
    public class Guildmate : INotifyPropertyChanged
    {
        #region Init
        public Guildmate(string name, int connection)
        {
            Name = name;
            _Connection = connection;
        }
        #endregion

        #region Properties
        public string Name { get; private set; }
        public int Connection
        {
            get { return _Connection; }
            set
            {
                if (_Connection != value)
                {
                    _Connection = value;
                    NotifyThisPropertyChanged();
                    NotifyPropertyChanged(nameof(IsConnected));
                    NotifyPropertyChanged(nameof(IsReading));
                }
            }
        }
        public bool IsConnected
        {
            get { return _Connection > 1; }
        }
        public bool IsReading
        {
            get { return _Connection > 0; }
        }
        private int _Connection;
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

        #region Overrides
        public override string ToString()
        {
            if (IsConnected)
                return Name + " (connected)";
            else if (IsReading)
                return Name + " (reading)";
            else
                return Name;
        }
        #endregion
    }
}
