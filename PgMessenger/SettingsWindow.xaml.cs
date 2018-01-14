using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PgMessenger
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        #region Init
        public SettingsWindow(List<CharacterSetting> characterList, bool isGuildChatEnabled)
        {
            CharacterList = characterList;
            IsGuildChatEnabled = isGuildChatEnabled;
            OpenedSettings = this;

            InitializeComponent();
            DataContext = this;
        }
        #endregion

        #region Properties
        public static SettingsWindow OpenedSettings { get; private set; }
        public List<CharacterSetting> CharacterList { get; private set; }
        public bool IsGuildChatEnabled { get; set; }
        #endregion

        #region Events
        private void OnClose(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            OpenedSettings = null;
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
    }
}
