namespace PgMessenger
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using ResourceTools;
    using TaskbarIconHost;

    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        #region Init
        public SettingsWindow(List<CharacterSetting> characterList, bool isGuildChatEnabled, string customLogFolder, bool enableUpdates)
        {
            CharacterList = characterList;
            IsGuildChatEnabled = isGuildChatEnabled;
            CustomLogFolder = customLogFolder;
            EnableUpdates = enableUpdates;
            OpenedSettings = this;

            InitializeComponent();
            DataContext = this;

            ResourceLoader.LoadIcon("main.ico", string.Empty, out ImageSource ResultIcon);
            Icon = ResultIcon;
        }
        #endregion

        #region Properties
        public static SettingsWindow? OpenedSettings { get; private set; }
        public List<CharacterSetting> CharacterList { get; private set; }
        public bool IsGuildChatEnabled { get; set; }
        public string CustomLogFolder { get; set; }
        public bool EnableUpdates { get; set; }
        #endregion

        #region Events
        private void OnClose(object sender, ExecutedRoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CustomLogFolder))
                if (!Directory.Exists(CustomLogFolder))
                    if (MessageBox.Show("The folder " + CustomLogFolder + " doesn't seem to exist. Close anyway?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                        return;

            Close();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            OpenedSettings = null;
        }
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
    }
}
