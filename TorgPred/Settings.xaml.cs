using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Controls = System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.Data;
using Excel = Microsoft.Office.Interop.Excel;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Globalization;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
            file_watcher.Created += new FileSystemEventHandler(WrkDirUpdated);
            file_watcher.Deleted += new FileSystemEventHandler(WrkDirUpdated);
            file_watcher.Renamed += new RenamedEventHandler(WrkDirUpdated);
        }

        private Settinger _setter;
        public Settinger setter
        {
            set
            {
                if (value != null)
                {
                    _setter = value;
                    setter.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(setter_PropertyChanged);
                }
            }
            get { return _setter; }
        }

        FileSystemWatcher file_watcher = new FileSystemWatcher();
        BrushConverter brushconverter = new BrushConverter();

        private delegate void DelegateWrkDirUpdater(FileSystemEventArgs args);
        private void WrkDirUpdated(object sender, FileSystemEventArgs e)
        {
            DelegateWrkDirUpdater _updatewrkdir = RefreshItems;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(_updatewrkdir, System.Windows.Threading.DispatcherPriority.Background, e);
        }

        private void RefreshTPs(string filename)
        {
            if (filename.ToUpper() == "Tps.csv".ToUpper())
            {
                ObservableCollection<string> tps = GetTorgPreds();
                cbTPs.ItemsSource = null;
                cbTPs.Items.Clear();
                cbTPs.ItemsSource = tps;
                cbTPs.IsEnabled = true;
            }
            ReCheckWrkDir(setter.WorkDir);
        }

        private void RefreshItems(FileSystemEventArgs args)
        {
            switch (args.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    ReCheckWrkDir(setter.WorkDir);
                    break;
                case WatcherChangeTypes.Created:
                    RefreshTPs(args.Name);
                    break;
                case WatcherChangeTypes.Renamed:
                    RefreshTPs(args.Name);
                    break;
            }
        }

        public void ReCheckWrkDir(string WorkDir)
        {
            bool folder_is_ok = true;

            //Проверка существования рабочей папки
            if (setter.WorkDir == null || setter.WorkDir == "" || !Directory.Exists(WorkDir) || !setter.IsWriteAccessEnabled(setter.WorkDir))
            {
                folder_is_ok = false;
                lWrkDr.Content = "Рабочая папка не определена!";
                lWrkDr.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                bOpenWrkDr.IsEnabled = false;
                cbTPs.IsEnabled = false;
            }
            else
            {
                bOpenWrkDr.IsEnabled = true;
                //Проверка существования списка торгпредов
                if (!File.Exists(WorkDir + @"\TPs.csv"))
                {
                    folder_is_ok = false;
                    lWrkDr.Content = "В рабочей папке нет ТоргПредов!";
                    lWrkDr.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                    cbTPs.IsEnabled = false;
                }

                //Проверка существования адресного плана
                if (!File.Exists(WorkDir + @"\APs.csv"))
                {
                    folder_is_ok = false;
                    lWrkDr.Content = "В рабочей папке нет адрес плана!";
                    lWrkDr.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                }

                //Проверка существования списка тарифов
                if (!File.Exists(WorkDir + @"\TP_list.csv"))
                {
                    folder_is_ok = false;
                    lWrkDr.Content = "Нет списка тарифных планов!";
                    lWrkDr.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);;
                }
            }

            //Если все хорошо, говорим об этом
            if (folder_is_ok)
            {
                lWrkDr.Content = "Рабочая папка определена!";
                lWrkDr.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            }

            ReCheckLastUser();
        }

        public void ReCheckLastUser()
        {
            bool user_is_ok = true;

            if (setter.LastUser == null || setter.LastUser.Replace(" ", "") == "")
            {
                lLastUser.Content = "Пользователь не определён!";
                lLastUser.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                user_is_ok = false;
            }

            ObservableCollection<string> tps = GetTorgPreds();
            cbTPs.ItemsSource = tps;

            if (tps == null)
            {
                lLastUser.Content = "Пользователь не определён!";
                lLastUser.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                cbTPs.IsEnabled = false;
                user_is_ok = false;
            }
            else
            {
                cbTPs.IsEnabled = true;
                string check = (from t in tps
                                where t == setter.LastUser
                                select t).FirstOrDefault();
                if (check == null)
                {
                    lLastUser.Content = "Пользователь не определён!";
                    lLastUser.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                    user_is_ok = false;
                }
            }

            if (user_is_ok)
            {
                lLastUser.Content = "Пользователь определён!";
                lLastUser.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                cbTPs.IsEnabled = true;
                for (int i = 0; i <= cbTPs.Items.Count - 1; i++)
                {
                    if (cbTPs.Items[i].ToString() == setter.LastUser)
                        cbTPs.SelectedIndex = i;
                }
            }
        }

        public ObservableCollection<string> GetTorgPreds()
        {
            ObservableCollection<string> result = new ObservableCollection<string>();
            FileInfo tps_xls_file = new FileInfo(setter.WorkDir + @"\TPs.csv");
            string line;
            if (Directory.Exists(setter.WorkDir) && tps_xls_file.Exists)
            {
                using (TextReader tr = new StreamReader(tps_xls_file.FullName, Encoding.Default))
                {
                    while ((line = tr.ReadLine()) != null)
                    {
                        string[] data = setter.CSVrow2StringArray(line);
                        if (data[0] != null && data[0].ToString().Trim() != "")
                        {
                            try
                            {
                                string cleaned_username = data[0].ToString().Trim();
                                foreach (char illegal in System.IO.Path.GetInvalidFileNameChars())
                                {
                                    cleaned_username = cleaned_username.Replace(illegal.ToString(), "");
                                }
                                result.Add(cleaned_username);
                            }
                            catch { }
                        }
                    }
                }
            }
            else
                return null;
            return result;
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReCheckWrkDir(setter.WorkDir);
            if (Directory.Exists(setter.WorkDir))
            {
                bOpenWrkDr.IsEnabled = true;
                file_watcher.Path = setter.WorkDir;
                file_watcher.Filter = "*.*";
                file_watcher.IncludeSubdirectories = false;
                file_watcher.EnableRaisingEvents = true;
            }

            gbWayListSettings.DataContext = setter.WayListSettings;
            
            //Вместо Binding
            tbCompany.Text = setter.WayListSettings.Company;
            tbOkud.Text = setter.WayListSettings.Okud;
            tbAutobrand.Text = setter.WayListSettings.Autobrand;
            tbAutoN.Text = setter.WayListSettings.AutoN;
            tbFIO.Text = setter.WayListSettings.FIO;
            tbPravaN.Text = setter.WayListSettings.PravaN;
            tbPravaClass.Text = setter.WayListSettings.PravaClass;
            tbRegN.Text = setter.WayListSettings.RegN;
            tbRegSeria.Text = setter.WayListSettings.RegSeria;
            tbRegN2.Text = setter.WayListSettings.RegN2;
            tbLicenseType.Text = setter.WayListSettings.LicenseType;
            tbOkpo.Text = setter.WayListSettings.Okpo;
            tbPurpose.Text = setter.WayListSettings.Purpose;
            tbFIOSign.Text = setter.WayListSettings.FIOSign;
            tbGazLimit.Text = setter.WayListSettings.GazLimit.ToString();
            
            setter.WayListSettings.PropertyChanged +=new System.ComponentModel.PropertyChangedEventHandler(setter_PropertyChanged);
        }

        private void setter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender.GetType()==typeof(Settinger) && e.PropertyName == "WorkDir")
            {
                ReCheckWrkDir(setter.WorkDir);
            }

            if (sender.GetType() == typeof(WayListSettings) && e.PropertyName != "StartEndPoints" && e.PropertyName != "WayListPoints" && e.PropertyName != "WayListDateModes")
            {
                setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
            }
        }

        private void bWrkDr_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выбрать папку, где будут храниться файлы.";
                dialog.ShowNewFolderButton = true;
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    setter.WorkDir = dialog.SelectedPath;
                    if (Directory.Exists(setter.WorkDir))
                    {
                        Registry.SetValue(@"HKEY_CURRENT_USER\Software\TorgPred\WorkDir", "", setter.WorkDir, RegistryValueKind.String);
                        bOpenWrkDr.IsEnabled = true;
                        file_watcher.Path = setter.WorkDir;
                        file_watcher.Filter = "*.*";
                        file_watcher.IncludeSubdirectories = false;
                        file_watcher.EnableRaisingEvents = true;
                        ReCheckWrkDir(setter.WorkDir);
                        ReCheckLastUser();
                    }
                }
            }
        }

        private void bOpenWrkDr_Click(object sender, RoutedEventArgs e)
        {
            setter.WorkDir = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\TorgPred\WorkDir", "", null);
            if (setter.WorkDir != null & Directory.Exists(setter.WorkDir))
            {
                var runExplorer = new System.Diagnostics.ProcessStartInfo();
                runExplorer.FileName = "explorer.exe";
                runExplorer.Arguments = setter.WorkDir;
                System.Diagnostics.Process.Start(runExplorer); 
            }
            
        }

        private void bGo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((string)lLastUser.Content == "Пользователь определён!" && (string)lWrkDr.Content == "Рабочая папка определена!" && setter.WayListSettings.DataDefined)
                {
                    setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                    this.Hide();
                }
                else
                {
                    if ((string)lLastUser.Content != "Пользователь определён!")
                        System.Windows.MessageBox.Show("Определите пользователя","Предупреждение",MessageBoxButton.OK,MessageBoxImage.Warning);
                    if ((string)lWrkDr.Content != "Рабочая папка определена!")
                        System.Windows.MessageBox.Show("Определите рабочую папку", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (!setter.WayListSettings.DataDefined)
                        System.Windows.MessageBox.Show("Определите настройки путевого листа", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch { }
        }

        private void LabelColor(string text, Controls.Label label)
        {
            if (text.Trim() != "")
                label.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            else

                label.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
        }

        private void tbCompany_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbCompany.Text, lCompany);
            setter.WayListSettings.Company = tbCompany.Text.Trim();
        }

        private void tbOkud_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbOkud.Text, lOkud);
            setter.WayListSettings.Okud = tbOkud.Text.Trim();
        }

        private void tbAutobrand_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbAutobrand.Text, lAutobrand);
            setter.WayListSettings.Autobrand = tbAutobrand.Text.Trim();
        }

        private void tbAutoN_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbAutoN.Text, lAutoN);
            setter.WayListSettings.AutoN = tbAutoN.Text.Trim();
        }

        private void tbFIO_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbFIO.Text, lFIO);
            setter.WayListSettings.FIO = tbFIO.Text.Trim();
        }

        private void tbPravaN_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbPravaN.Text, lPravaN);
            setter.WayListSettings.PravaN = tbPravaN.Text.Trim();
        }

        private void tbPravaClass_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbPravaClass.Text, lPravaClass);
            setter.WayListSettings.PravaClass = tbPravaClass.Text.Trim();
        }

        private void tbRegN_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbRegN.Text, lRegN);
            setter.WayListSettings.RegN = tbRegN.Text.Trim();
        }

        private void tbRegSeria_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbRegSeria.Text, lRegSeria);
            setter.WayListSettings.RegSeria = tbRegSeria.Text.Trim();
        }

        private void tbRegN2_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbRegN2.Text, lRegN2);
            setter.WayListSettings.RegN2 = tbRegN2.Text.Trim();
        }

        private void tbLicenseType_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbLicenseType.Text, lLicenseType);
            setter.WayListSettings.LicenseType = tbLicenseType.Text.Trim();
        }

        private void tbOkpo_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbOkpo.Text, lOkpo);
            setter.WayListSettings.Okpo = tbOkpo.Text.Trim();
        }

        private void tbPurpose_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbPurpose.Text, lPurpose);
            setter.WayListSettings.Purpose = tbPurpose.Text;
        }

        private void tbFIOSign_TextChanged(object sender, TextChangedEventArgs e)
        {
            LabelColor(tbFIOSign.Text, lFIOSign);
            setter.WayListSettings.FIOSign = tbFIOSign.Text;
        }

        private void tbGazLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (tbGazLimit.Text.Trim() == "")
                    tbGazLimit.Text = "0";
                decimal value = Decimal.Parse(tbGazLimit.Text.Trim());
                if (value > 0)
                    lGazLimit.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                else
                    lGazLimit.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                setter.WayListSettings.GazLimit = value;
            }
            catch { lGazLimit.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed); }
        }


        private void bCreateWayList_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void cbTPs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbTPs.SelectedValue != null && cbTPs.SelectedValue.ToString().Replace(" ", "") != "")
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\TorgPred\UserTPSelected", "", cbTPs.SelectedValue.ToString().Trim(), RegistryValueKind.String);
                setter.LastUser = cbTPs.SelectedValue.ToString().Trim();
                lLastUser.Content = "Пользователь определён!";
                lLastUser.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                ReCheckWrkDir(setter.WorkDir);
                if (setter.WayListSettings.FIOSign.Trim() == "")
                    setter.WayListSettings.FIOSign = setter.LastUser;
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
                e.Handled = true;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel=true;
        }

        private static bool IsTextAllowed(string text)
        {
            //Дробные цифры
            Regex regex = new Regex("[^0-9"+CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator+"-]+"); //regex that matches disallowed text
            //Целые числа
            //Regex regex = new Regex(@"^\d{1,}"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }

        private static bool IsDecimalAllowed(string text)
        {
            //Дробные цифры
            //Regex regex = new Regex("[^0-9]" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "-]+"); //regex that matches disallowed text
            //Regex regex = new Regex(@"^\d{,2}" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + @"{1}\d{,1}$"); //regex that matches disallowed text
            Regex regex = new Regex(@"^\d{0,2}(" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + @"\d{0,2})?$");
            //Целые числа
            //Regex regex = new Regex(@"^\d{1,}"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        private void DecimalMask_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDecimalAllowed((sender as System.Windows.Controls.TextBox).Text.Insert((sender as System.Windows.Controls.TextBox).SelectionStart, e.Text));
        }

        private void bMin_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void bMax_Click(object sender, RoutedEventArgs e)
        {

        }

        private void bClose_Click(object sender, RoutedEventArgs e)
        {
            if (setter != null)
            {
                setter.CloseApp = true;
                this.Hide();
            }
        }
    }
}

