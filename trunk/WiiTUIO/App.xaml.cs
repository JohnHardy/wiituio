using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

using Hardcodet.Wpf.TaskbarNotification;

namespace WiiTUIO
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected TaskbarIcon tb;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Initialise the Tray Icon
            tb = (TaskbarIcon)FindResource("tbNotifyIcon");
            tb.ShowBalloonTip("WiiTUIO", "WiiTUIO is running", BalloonIcon.Info);
            base.OnStartup(e);
        }

        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            tb.Dispose();
            Application.Current.Shutdown(0);
        }
    }
}
