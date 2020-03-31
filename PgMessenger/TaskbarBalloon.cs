﻿namespace TaskbarTools
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// This class provides an API to display notifications to the user.
    /// </summary>
    public static class TaskbarBalloon
    {
        #region Init
        static TaskbarBalloon()
        {
            DefaultDelay = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Gets the default delay before a balloon closes.
        /// </summary>
        public static TimeSpan DefaultDelay { get; }
        #endregion

        #region Client Interface
        /// <summary>
        /// Display a notification in a taskbar balloon.
        /// </summary>
        /// <param name="text">The text to show.</param>
        /// <param name="delay">The delay, in milliseconds.</param>
        public static void Show(string text, TimeSpan delay)
        {
            NotifyIcon Notification = new NotifyIcon() { Visible = true, Icon = SystemIcons.Shield, Text = text, BalloonTipText = text };
#pragma warning disable CA2000 // Dispose objects before losing scope
            BallonPrivateData Data = new BallonPrivateData(Notification);
#pragma warning restore CA2000 // Dispose objects before losing scope
            Show(delay, Notification, Data);
        }

        /// <summary>
        /// Display a notification in a taskbar balloon.
        /// </summary>
        /// <param name="text">The text to show.</param>
        /// <param name="delay">The delay, in milliseconds.</param>
        /// <param name="clickHandler">Handler for the click event.</param>
        /// <param name="clickData">Handler data for the click event.</param>
        public static void Show(string text, TimeSpan delay, Action<object> clickHandler, object clickData)
        {
            NotifyIcon Notification = new NotifyIcon() { Visible = true, Icon = SystemIcons.Shield, Text = text, BalloonTipText = text };
#pragma warning disable CA2000 // Dispose objects before losing scope
            BallonPrivateData Data = new BallonPrivateData(Notification, clickHandler, clickData);
#pragma warning restore CA2000 // Dispose objects before losing scope
            Show(delay, Notification, Data);
        }
        #endregion

        #region Implementation
        private static void Show(TimeSpan delay, NotifyIcon notification, BallonPrivateData data)
        {
            try
            {
                notification.Tag = data;
                notification.BalloonTipClosed += new EventHandler(OnClosed);
                notification.BalloonTipClicked += new EventHandler(OnClicked);
                notification.Click += new EventHandler(OnClicked);
                notification.MouseClick += new MouseEventHandler(OnMouseClicked);
                notification.ShowBalloonTip((int)(delay.TotalMilliseconds));
                DisplayedBalloonList.Add(data);
            }
            catch
            {
            }
        }

        private static List<BallonPrivateData> DisplayedBalloonList = new List<BallonPrivateData>();

        private static void OnClosed(object sender, EventArgs e)
        {
            BallonCloseHandler(sender);
        }

        private static void OnClicked(object sender, EventArgs e)
        {
            BallonClickHandler(sender);
            BallonCloseHandler(sender);
        }

        private static void OnMouseClicked(object sender, MouseEventArgs e)
        {
            BallonClickHandler(sender);
            BallonCloseHandler(sender);
        }

        private static void BallonClickHandler(object sender)
        {
            if (sender is NotifyIcon Notification && Notification.Tag is BallonPrivateData Data)
                if (Data.GetClickHandler(out Action<object> ClickHandler, out object ClickData))
                    ClickHandler.Invoke(ClickData);
        }

        private static void BallonCloseHandler(object sender)
        {
            if (sender is NotifyIcon Notification)
            {
                Notification.Visible = false;

                if (Notification.Tag is BallonPrivateData Data)
                {
                    Notification.Tag = null;
                    DisplayedBalloonList.Remove(Data);
                    Data.Closed();
                    using BallonPrivateData CloseData = Data;
                }

                using NotifyIcon CloseNotification = Notification;
            }
        }

        private class BallonPrivateData : IDisposable
        {
            public BallonPrivateData(NotifyIcon notification)
            {
                Notification = notification;
                ClickData = this;
            }

            public BallonPrivateData(NotifyIcon notification, Action<object> clickHandler, object clickData)
                : this(notification)
            {
                ClickHandler = clickHandler;
                ClickData = clickData;
            }

            public NotifyIcon? Notification { get; private set; }
            public Action<object>? ClickHandler { get; private set; }
            public object ClickData { get; }
            public bool IsClosed { get; private set; }

            public void Closed()
            {
                IsClosed = true;
            }

            public bool GetClickHandler(out Action<object> clickHandler, out object clickData)
            {
                clickData = ClickData;

                if (ClickHandler != null)
                {
                    clickHandler = ClickHandler;
                    ClickHandler = null;
                    return true;
                }
                else
                {
                    clickHandler = new Action<object>((object data) => { });
                    return false;
                }
            }

            protected virtual void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    DisposeNow();
            }

            private void DisposeNow()
            {
                Notification = null;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~BallonPrivateData()
            {
                Dispose(false);
            }
        }
        #endregion
    }
}