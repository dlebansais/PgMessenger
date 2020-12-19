namespace PgMessenger
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

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
