using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for WayListMode.xaml
    /// </summary>
    public partial class WayListMode : Window
    {
        public WayListMode()
        {
            InitializeComponent();
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

        BrushConverter brushconverter = new BrushConverter();
        public MainWindow main_window { get; set; }


        private void SetDateWayListMode(DateTime report_date, WayListModeSet mode_value)
        {
            WaylistDateMode datemode = setter.GetWaylistDateMode(report_date.Date);

            if (datemode == null)
            {
                datemode = new WaylistDateMode();
                datemode.Report_date = report_date;
                datemode.Date_WayListMode = mode_value;
                setter.WayListSettings.WayListDateModes.Add(datemode);
            }
            else
            {
                datemode.Date_WayListMode = mode_value;
            }
        }

        private void SetWayListDateModeFilterAPS(DateTime report_date, Boolean value)
        {
            WaylistDateMode datemode = setter.GetWaylistDateMode(report_date.Date);

            if (datemode == null)
            {
                datemode = new WaylistDateMode();
                datemode.Report_date = report_date;
                datemode.FilterAPSwithTP = value;
                setter.WayListSettings.WayListDateModes.Add(datemode);
            }
            else
            {
                datemode.FilterAPSwithTP = value;
            }
        }

        private void rbPedestrian_Checked(object sender, RoutedEventArgs e)
        {
            WayListModeSet value = WayListModeSet.Pedestrian;

            lStartPoint.Visibility = Visibility.Hidden;
            cbStartPoint.Visibility = Visibility.Hidden;
            lSpeedMeter.Visibility = Visibility.Hidden;
            tbSpeedMeter.Visibility = Visibility.Hidden;
            tbGaznumber_onpoint.Visibility = Visibility.Hidden;
            lGaznumber_onpoint.Visibility = Visibility.Hidden;
            bGo.IsEnabled = true;
            setter.WayListMode = value;
            SetDateWayListMode(setter.Report_date, value);
        }

        private void rbMarket_Checked(object sender, RoutedEventArgs e)
        {
            WayListModeSet value = WayListModeSet.Market;
            lStartPoint.Visibility = Visibility.Visible;
            cbStartPoint.Visibility = Visibility.Visible;
            lSpeedMeter.Visibility = Visibility.Visible;
            tbSpeedMeter.Visibility = Visibility.Visible;
            tbGaznumber_onpoint.Visibility = Visibility.Visible;
            lGaznumber_onpoint.Visibility = Visibility.Visible;
            bGo.IsEnabled = true;
            setter.WayListMode = value;
            SetDateWayListMode(setter.Report_date, value);
        }

        private void rbTransport_Checked(object sender, RoutedEventArgs e)
        {
            WayListModeSet value = WayListModeSet.OnTransport;
            lStartPoint.Visibility = Visibility.Visible;
            cbStartPoint.Visibility = Visibility.Visible;
            lSpeedMeter.Visibility = Visibility.Visible;
            tbSpeedMeter.Visibility = Visibility.Visible;
            tbGaznumber_onpoint.Visibility = Visibility.Visible;
            lGaznumber_onpoint.Visibility = Visibility.Visible;
            bGo.IsEnabled = true;
            setter.WayListMode = value;
            SetDateWayListMode(setter.Report_date, value);
        }

        private void bGo_Click(object sender, RoutedEventArgs e)
        {
            if (cbStartPoint.Text.Trim() != "" || setter.WayListMode == WayListModeSet.Pedestrian)
            {
                if (setter.WayListMode != WayListModeSet.Pedestrian)
                {
                    setter.WayListSettings.StartEndPoints.Add(cbStartPoint.Text);
                    WayListPoint startpoint = (from sp in setter.WayListSettings.WayListPoints
                                               where sp.Point_type == WayListPointTypes.Start
                                               && sp.Report_date.Date == setter.Report_date.Date
                                               select sp).FirstOrDefault();
                    if (startpoint == null)
                    {
                        startpoint = new WayListPoint()
                        {
                            Point_address = cbStartPoint.Text,
                            Report_date = setter.Report_date,
                            Point_type = WayListPointTypes.Start,
                            Speedmeter = tbSpeedMeter.Text.Trim() == "" ? 0 : Convert.ToInt64(tbSpeedMeter.Text),
                            Gaznumber_onpoint = tbGaznumber_onpoint.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text),
                            Point_leave = new DateTime(setter.Report_date.Year, setter.Report_date.Month, setter.Report_date.Day, DateTime.Now.Hour < 10 ? 10 : DateTime.Now.Hour, DateTime.Now.Hour < 10 ? 0 : DateTime.Now.Minute, 0)
                        };
                        setter.WayListSettings.WayListPoints.Add(startpoint);
                    }
                    else
                    {
                        startpoint.Point_address = cbStartPoint.Text;
                        startpoint.Speedmeter = tbSpeedMeter.Text.Trim() == "" ? 0 : Convert.ToInt64(tbSpeedMeter.Text);
                        startpoint.Gaznumber_onpoint = tbGaznumber_onpoint.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text);
                        if (startpoint.Point_leave.Date != setter.Report_date.Date)
                            startpoint.Point_leave = new DateTime(setter.Report_date.Year, setter.Report_date.Month, setter.Report_date.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);
                    }
                    setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                }
                else
                    setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                this.Hide();
            }
            else
                MessageBox.Show("Заполните адрес подачи транспорта", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
                e.Handled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private WayListPoint GetWayListPoint(DateTime report_date)
        {
            return (from sp in setter.WayListSettings.WayListPoints
                    where sp.Point_type == WayListPointTypes.Start
                    && sp.Report_date.Date == report_date.Date
                    select sp).FirstOrDefault();
        }

        private void InitWayListForm(DateTime report_date)
        {
            gbWayListInit.Header = "Режим путевого листа на " + setter.Report_date.ToString("dd.MM.yyyy");

            WaylistDateMode datemode = setter.GetWaylistDateMode(setter.Report_date);
            if (datemode == null)
            {
                rbMarket.IsChecked = false;
                rbPedestrian.IsChecked = false;
                rbTransport.IsChecked = false;
                bGo.IsEnabled = false;
                cbFilterAPSwithTP.IsChecked = false;
                lStartPoint.Visibility = Visibility.Hidden;
                cbStartPoint.Visibility = Visibility.Hidden;
                lSpeedMeter.Visibility = Visibility.Hidden;
                tbSpeedMeter.Visibility = Visibility.Hidden;
                tbGaznumber_onpoint.Visibility = Visibility.Hidden;
                lGaznumber_onpoint.Visibility = Visibility.Hidden;
            }
            else
            {
                switch (datemode.Date_WayListMode)
                {
                    case (WayListModeSet.Market):
                        rbMarket.IsChecked = true;
                        break;
                    case (WayListModeSet.OnTransport):
                        rbTransport.IsChecked = true;
                        break;
                    case (WayListModeSet.Pedestrian):
                        rbPedestrian.IsChecked = true;
                        break;
                    default:
                        rbPedestrian.IsChecked = true;
                        break;
                }
                cbFilterAPSwithTP.IsChecked = datemode.FilterAPSwithTP;
            }
            WayListPoint startpoint = GetWayListPoint(setter.Report_date);
            cbStartPoint.ItemsSource = setter.WayListSettings.StartEndPoints;
            if (startpoint != null)
            {
                if (setter.WayListSettings.StartEndPoints.Contains(startpoint.Point_address))
                    cbStartPoint.SelectedItem = startpoint.Point_address;
                tbSpeedMeter.Text = startpoint.Speedmeter==0 ? "" : startpoint.Speedmeter.ToString();
                tbGaznumber_onpoint.Text = startpoint.Gaznumber_onpoint == 0 ? "" : startpoint.Gaznumber_onpoint.ToString();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dtReport_date.SelectedDateChanged -= new EventHandler<SelectionChangedEventArgs>(dtReport_date_SelectedDateChanged);
            dtReport_date.SelectedDate = setter.Report_date;
            dtReport_date.SelectedDateChanged += new EventHandler<SelectionChangedEventArgs>(dtReport_date_SelectedDateChanged);
            InitWayListForm(setter.Report_date);
            ColorDecimalLabel(tbGaznumber_onpoint, lGaznumber_onpoint);
            ColorIntLabel(tbSpeedMeter, lSpeedMeter);
        }

        public void Reload()
        {
            dtReport_date.SelectedDateChanged -= new EventHandler<SelectionChangedEventArgs>(dtReport_date_SelectedDateChanged);
            dtReport_date.SelectedDate = setter.Report_date;
            dtReport_date.SelectedDateChanged += new EventHandler<SelectionChangedEventArgs>(dtReport_date_SelectedDateChanged);
            InitWayListForm(setter.Report_date);
        }

        public void setter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Report_date")
                Reload();
        }

        private void dtReport_date_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dtReport_date.SelectedDate != null)
                setter.Report_date = (DateTime)dtReport_date.SelectedDate;
            InitWayListForm(setter.Report_date);
        }

        private static bool IsTextAllowed(string text)
        {
            //Дробные цифры
            //Regex regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            //Целые числа
            Regex regex = new Regex("[^0-9-]+"); //regex that matches disallowed text
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

        private void tbSpeedMeter_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void DecimalMask_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDecimalAllowed((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void bMin_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void bClose_Click(object sender, RoutedEventArgs e)
        {
            if (setter != null)
            {
                if (main_window.Visibility != Visibility.Visible)
                    setter.CloseApp = true;
                this.Hide();
            }
        }

        private void tbGaznumber_onpoint_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorDecimalLabel(tbGaznumber_onpoint, lGaznumber_onpoint);
        }

        private void ColorIntLabel(TextBox textbox, Label label)
        {
            Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
            Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);

            if (textbox.Text == "" || !IsTextAllowed(textbox.Text) || Convert.ToInt64(textbox.Text) == 0)
                label.Foreground = red;
            else
                label.Foreground = green;
        }
        
        private void ColorDecimalLabel(TextBox textbox, Label label)
        {
            Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
            Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);

            if (textbox.Text == "" || !IsDecimalAllowed(textbox.Text))
                label.Foreground = red;
            else
                label.Foreground = green;
        }

        private void tbSpeedMeter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorIntLabel(tbSpeedMeter, lSpeedMeter);
        }

        private void cbStartPoint_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                LabelColor(cbStartPoint.SelectedValue.ToString(), lStartPoint);
            }
            catch { }
        }

        private void LabelColor(string text, Label label)
        {
            if (text.Trim() != "")
                label.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            else
                label.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
        }

        private void TextBoxSelectAll_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender.GetType() == typeof(TextBox))
                    (sender as TextBox).SelectAll();
            }
            catch { }
        }

        private void cbFilterAPSwithTP_Click(object sender, RoutedEventArgs e)
        {
            SetWayListDateModeFilterAPS(setter.Report_date, (bool)cbFilterAPSwithTP.IsChecked);
            setter.FilterAPS = (bool)cbFilterAPSwithTP.IsChecked;
        }
    }
}