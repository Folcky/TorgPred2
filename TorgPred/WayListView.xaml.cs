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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Globalization;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for WayListView.xaml
    /// </summary>
    public partial class WayListView : UserControl//,INotifyPropertyChanged
    {
        BrushConverter brushconverter = new BrushConverter();
        public event PropertyChangedEventHandler WayListPointUpdated;
        public void NotifyWayListPointUpdated(String info)
        {
            if (WayListPointUpdated != null)
                WayListPointUpdated(this, new PropertyChangedEventArgs(info));
        }
        private Settinger _setter = null;
        public Settinger setter
        {
            get
            {
                return _setter;
            }
            set
            {
                _setter = value;
                ColorLabels();
            }
        }
        private WayListPoint _waylistproperty=null;
        public WayListPoint WayListPointProperty
        {
            get
            {
                return _waylistproperty;
            }
            set
            {
                _waylistproperty = value;
                UpdateForm();
                WayListPointProperty.PropertyChanged += new PropertyChangedEventHandler(WayListPointProperty_PropertyChanged);
            }
        }

        public WayListView()
        {
            InitializeComponent();
        }

        private void WayListPointProperty_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Speedmeter")
                tbSpeedMeter.Text = WayListPointProperty.Speedmeter==0 ? "" : WayListPointProperty.Speedmeter.ToString();
            if (e.PropertyName == "Gaznumber_onpoint")
                tbGaznumber_onpoint.Text = WayListPointProperty.Gaznumber_onpoint==0 ? "" : WayListPointProperty.Gaznumber_onpoint.ToString();
            if (e.PropertyName == "Gaznumber_buyed")
                tbGaznumber_buyed.Text = WayListPointProperty.Gaznumber_buyed==0 ? "" : WayListPointProperty.Gaznumber_buyed.ToString();
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

        private static bool IsTextAllowed(string text)
        {
            //Дробные цифры
            //Regex regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            //Целые числа
            Regex regex = new Regex(@"^\d{1,}"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }
        
        private static bool IsTimeAllowed(string text)
        {
            Regex regex0 = new Regex(@"\:");
            Regex regex1 = new Regex(@"\d");
            
            if (regex0.IsMatch(text) || regex1.IsMatch(text))
                return true;
            return false;
        }

        private static bool IsTimeCorrect(string text)
        {
            //Только в формате 00:00
            Regex regex = new Regex(@"^\d{1,2}\:\d{1,2}$"); //regex that matches disallowed text
            return regex.IsMatch(text);
        }

        private void UpdateForm()
        {
            tbPointAddress.Text = WayListPointProperty.Point_address;
            tbPoint_leave.Text = WayListPointProperty.Point_leave.Hour + ":" + WayListPointProperty.Point_leave.Minute;
            tbPoint_enter.Text = WayListPointProperty.Point_enter.Hour + ":" + WayListPointProperty.Point_enter.Minute;
            tbSpeedMeter.Text = WayListPointProperty.Speedmeter==0 ? "" : WayListPointProperty.Speedmeter.ToString();
            tbGaznumber_buyed.Text = WayListPointProperty.Gaznumber_buyed == 0 ? "" : WayListPointProperty.Gaznumber_buyed.ToString();
            tbGaznumber_onpoint.Text = WayListPointProperty.Gaznumber_onpoint == 0 ? "" : WayListPointProperty.Gaznumber_onpoint.ToString();
            ColorLabels();
            switch (WayListPointProperty.Point_type)
            {
                case (WayListPointTypes.Start):
                    lPoint_leave.Visibility = Visibility.Visible;
                    tbPoint_leave.Visibility = Visibility.Visible;
                    lPoint_enter.Visibility = Visibility.Hidden;
                    tbPoint_enter.Visibility = Visibility.Hidden;
                    lPoint_enterStar.Visibility = Visibility.Hidden;
                    bDelete.Visibility = Visibility.Hidden;
                    lPoint_type.Content = "Начало:";
                    //gbWayListPoint.Header = "Адрес подачи транспорта  ----->";
                    lGaznumber_buyed.Visibility = Visibility.Hidden;
                    tbGaznumber_buyed.Visibility = Visibility.Hidden;
                    lGaznumber_buyedStar.Visibility = Visibility.Hidden;
                    break;
                case (WayListPointTypes.Common):
                    lPoint_leave.Visibility = Visibility.Visible;
                    tbPoint_leave.Visibility = Visibility.Visible;
                    lPoint_enter.Visibility = Visibility.Visible;
                    tbPoint_enter.Visibility = Visibility.Visible;
                    lPoint_type.Content = "Транзит:";
                    //gbWayListPoint.Header = "Адрес транзитной точки  <----->";
                    //spHoster.Children.Remove(spGaznumber);
                    lGaznumber_buyed.Visibility = Visibility.Hidden;
                    tbGaznumber_buyed.Visibility = Visibility.Hidden;
                    lGaznumber_buyedStar.Visibility = Visibility.Hidden;
                    lGaznumber_onpoint.Visibility = Visibility.Hidden;
                    tbGaznumber_onpoint.Visibility = Visibility.Hidden;
                    lGaznumber_onpointStar.Visibility = Visibility.Hidden;
                    break;
                case (WayListPointTypes.Finish):
                    lPoint_leave.Visibility = Visibility.Hidden;
                    tbPoint_leave.Visibility = Visibility.Hidden;
                    lPoint_leaveStar.Visibility = Visibility.Hidden;
                    lPoint_enter.Visibility = Visibility.Visible;
                    tbPoint_enter.Visibility = Visibility.Visible;
                    lPoint_type.Content = "Конец:";
                    //gbWayListPoint.Header = "Адрес возврата транспорта  <-----";
                    break;
            }
        }

        private void tbSpeedMeter_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void DecimalMask_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDecimalAllowed((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
        }

        private void ColorTimeLabel(TextBox textbox, Label label)
        {
            Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
            Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            red = Brushes.Red;
            green = Brushes.Green;

            if (!IsTimeCorrect(textbox.Text))
                label.Foreground = red;
            else
            {
                int hour = Convert.ToInt16(textbox.Text.Substring(0, textbox.Text.IndexOf(":")).Trim());
                int minute = Convert.ToInt16(textbox.Text.Substring(textbox.Text.IndexOf(":") + 1).Trim());
                if (hour + minute != 0)
                    label.Foreground = green;
                else
                    label.Foreground = red;
            }
        }

        private void ColorDecimalLabel(TextBox textbox, Label label)
        {
            Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
            Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            red = Brushes.Red;
            green = Brushes.Green;

            if (textbox.Text == "" || !IsDecimalAllowed(textbox.Text))
                label.Foreground = red;
            else
                label.Foreground = green;
        }

        private void ColorIntLabel(TextBox textbox, Label label)
        {
            Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
            Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
            red = Brushes.Red;
            green = Brushes.Green;

            if (textbox.Text == "" || !IsTextAllowed(textbox.Text) || Convert.ToInt64(textbox.Text) == 0)
                label.Foreground = red;
                else
                label.Foreground = green;
        }

        private void ColorLabels()
        {
            if (setter != null)
            {
                Brush red = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                Brush green = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                red = Brushes.Red;
                green = Brushes.Green;
                //lPointAddress.Foreground = green;
                lGaznumber_buyedStar.Foreground = green;

                ColorDecimalLabel(tbGaznumber_onpoint, lGaznumber_onpointStar);
                ColorIntLabel(tbSpeedMeter, lSpeedMeterStar);

                ColorTimeLabel(tbPoint_enter, lPoint_enterStar);
                ColorTimeLabel(tbPoint_leave, lPoint_leaveStar);
            }
        }

        private bool CheckTimeForSave(TextBox textbox, DateTime Property)
        {
            bool result = false;
            if (IsTimeCorrect(textbox.Text))
            {
                int hour = Convert.ToInt16(textbox.Text.Substring(0, textbox.Text.IndexOf(":")).Trim());
                int minute = Convert.ToInt16(textbox.Text.Substring(textbox.Text.IndexOf(":") + 1).Trim());
                if (hour > 23)
                {
                    hour = 0;
                    textbox.Text = hour + ":" + minute;
                }
                if (minute > 59)
                {
                    minute = 59;
                    textbox.Text = hour + ":" + minute;
                }
                if (hour + minute != 0)
                    if (Property.Hour != hour || Property.Minute != minute)
                        return true;
            }
            return result;
        }

        private void CheckForSave()
        {
            bool save_is_needed = false;
            if (setter != null)
            {
                try
                {
                    if (WayListPointProperty.Point_address.Trim() != tbPointAddress.Text.Trim())
                        save_is_needed = true;
                    if (WayListPointProperty.Gaznumber_onpoint != (tbGaznumber_onpoint.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text)))
                        save_is_needed = true;
                    if (WayListPointProperty.Gaznumber_buyed != (tbGaznumber_buyed.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_buyed.Text == "," ? "0" : tbGaznumber_buyed.Text)))
                        save_is_needed = true;
                    if (WayListPointProperty.Speedmeter != (tbSpeedMeter.Text.Trim() == "" ? 0 : Convert.ToInt64(tbSpeedMeter.Text)))
                        save_is_needed = true;
                    if (CheckTimeForSave(tbPoint_enter, WayListPointProperty.Point_enter))
                        save_is_needed = true;
                    if (CheckTimeForSave(tbPoint_leave, WayListPointProperty.Point_leave))
                        save_is_needed = true;
                }
                catch { }
                if (save_is_needed)
                    bSave.Visibility = Visibility.Visible;
                else
                    bSave.Visibility = Visibility.Hidden;
            }
            else
                bSave.Visibility = Visibility.Hidden;
        }

        private void tbSpeedMeter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorLabels();
            CheckForSave();
        }

        private void bSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WayListPointProperty.Point_address = tbPointAddress.Text.Trim();
                WayListPointProperty.Speedmeter = tbSpeedMeter.Text.Trim()=="" ? 0 : Convert.ToInt64(tbSpeedMeter.Text);
                int hour = Convert.ToInt16(tbPoint_enter.Text.Substring(0, tbPoint_enter.Text.IndexOf(":")).Trim());
                int minute = Convert.ToInt16(tbPoint_enter.Text.Substring(tbPoint_enter.Text.IndexOf(":") + 1).Trim());
                WayListPointProperty.Point_enter = new DateTime(setter.Report_date.Year, setter.Report_date.Month, setter.Report_date.Day, hour, minute, 0);

                hour = Convert.ToInt16(tbPoint_leave.Text.Substring(0, tbPoint_leave.Text.IndexOf(":")).Trim());
                minute = Convert.ToInt16(tbPoint_leave.Text.Substring(tbPoint_leave.Text.IndexOf(":") + 1).Trim());
                WayListPointProperty.Point_leave = new DateTime(setter.Report_date.Year, setter.Report_date.Month, setter.Report_date.Day, hour, minute, 0);

                WayListPointProperty.Gaznumber_buyed = tbGaznumber_buyed.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_buyed.Text == "," ? "0" : tbGaznumber_buyed.Text);
                WayListPointProperty.Gaznumber_onpoint = tbGaznumber_onpoint.Text.Trim() == "" ? 0 : Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text);

                CheckForSave();
                NotifyWayListPointUpdated("WayListPointProperty");
            }
            catch { }
        }

        private void tbPoint_enter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorLabels();
            CheckForSave();
        }

        private void tbPoint_enter_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTimeAllowed(e.Text);
        }

        private void tbPoint_leave_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorLabels();
            CheckForSave();
        }

        private void tbPoint_leave_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTimeAllowed(e.Text);
        }

        private void bDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Действительно хотите удалить точку из путевого листа?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                setter.WayListSettings.WayListPoints.Remove(WayListPointProperty);
                NotifyWayListPointUpdated("WayListPointProperty_deleted");
            }
        }

        private void tbPointAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckForSave();
        }

        private void tbGaznumber_buyed_TextChanged(object sender, TextChangedEventArgs e)
        {
            ColorLabels();
            CheckForSave();
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

    }
}
