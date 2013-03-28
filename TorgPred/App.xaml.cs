using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;

namespace TorgPred
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Settinger setter = new Settinger();
            setter.WorkDir = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\TorgPred\WorkDir", "", null);
            setter.LastUser = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\TorgPred\UserTPSelected", "", null);
            setter.Report_date = DateTime.Now;
            FinishWayPointView finishpoint = new FinishWayPointView();
            finishpoint.setter = setter;
            MainWindow mainwindow = new MainWindow();
            mainwindow.setter = setter;
            mainwindow.lVersionInfo.Content = setter.Version;
            
            Settings settings = new Settings();
            settings.setter = setter;

            WayListMode waylistmode = new WayListMode();
            waylistmode.setter = setter;
            
            waylistmode.main_window = mainwindow;
            mainwindow.waylistmode_window = waylistmode;
            mainwindow.finishpoint_window = finishpoint;

            if ((setter.WorkDir == null || setter.LastUser == null) || setter.WayListSettings == null || !setter.WayListSettings.DataDefined ||
                !setter.IsWriteAccessEnabled(setter.WorkDir) ||
                (setter.WorkDir != null && (!File.Exists(setter.WorkDir + @"\TPs.csv") || !File.Exists(setter.WorkDir + @"\APs.csv") || !File.Exists(setter.WorkDir + @"\TP_list.csv"))))
            {
                settings.ShowDialog();
                CloseApp(setter.CloseApp);
                if (setter.LastUser != "" && setter.LastUser != null && setter.WorkDir != "" && setter.WorkDir != null && setter.WayListSettings.DataDefined)
                do
                {
                    setter.ShowSetter = false;
                    //
                    waylistmode.ShowDialog();
                    CloseApp(setter.CloseApp);
                    //
                    mainwindow.ShowDialog();
                    CloseApp(setter.CloseApp);
                    //
                    if (setter.ShowSetter)
                        settings.ShowDialog();
                } while (setter.ShowSetter == true);
            }
            else
            {
                do
                {
                    setter.ShowSetter = false;
                    //
                    waylistmode.ShowDialog();
                    CloseApp(setter.CloseApp);
                    //
                    mainwindow.LoadWaylist();
                    mainwindow.ShowDialog();
                    CloseApp(setter.CloseApp);
                    //
                    if (setter.ShowSetter)
                        settings.ShowDialog();
                } while (setter.ShowSetter == true);
            }

            CloseApp(true);
        
        }

        private void CloseApp(bool comm)
        {
            if (comm)
                this.Shutdown();
        }
    }
}

