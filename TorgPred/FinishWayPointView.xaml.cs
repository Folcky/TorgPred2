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
using System.Globalization;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for FinishWayPointView.xaml
    /// </summary>
    public partial class FinishWayPointView : Window
    {
        public FinishWayPointView()
        {
            InitializeComponent();
        }

        public Settinger setter;
        private Boolean cancel = true;
        public Boolean Cancel { get { return cancel; } set { cancel = value; } }

        private void tbSpeedMeter_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void DecimalMask_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDecimalAllowed((sender as TextBox).Text.Insert((sender as TextBox).SelectionStart, e.Text));
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
            Regex regex = new Regex("[^0-9-]+"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }

        private void bReady_Click(object sender, RoutedEventArgs e)
        {
            if (cbFinishPoint.Text.Trim() != "")
            {
                tbSpeedMeter.Text = tbSpeedMeter.Text.Trim() == "" ? "0" : tbSpeedMeter.Text.Trim();
                tbGaznumber_buyed.Text = tbGaznumber_buyed.Text.Trim() == "" ? "0" : tbGaznumber_buyed.Text.Trim();
                tbGaznumber_onpoint.Text = tbGaznumber_onpoint.Text.Trim() == "" ? "0" : tbGaznumber_onpoint.Text.Trim();
                setter.WayListSettings.StartEndPoints.Add(cbFinishPoint.Text);
                WayListPoint finishpoint = (from sp in setter.WayListSettings.WayListPoints
                                            where sp.Point_type == WayListPointTypes.Finish
                                            && sp.Report_date.Date == setter.Report_date.Date
                                            select sp).FirstOrDefault();
                if (finishpoint == null)
                {
                    finishpoint = new WayListPoint()
                    {
                        Point_address = cbFinishPoint.Text,
                        Report_date = setter.Report_date,
                        Point_type = WayListPointTypes.Finish,
                        Speedmeter = Convert.ToInt64(tbSpeedMeter.Text),
                        Gaznumber_buyed = Convert.ToDecimal(tbGaznumber_buyed.Text == "," ? "0" : tbGaznumber_buyed.Text),
                        Gaznumber_onpoint = Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text),
                        Point_enter = new DateTime(
                            setter.Report_date.Year, 
                            setter.Report_date.Month, 
                            setter.Report_date.Day, 
                            DateTime.Now.Hour > 19 ? 19 : DateTime.Now.Hour, 
                            DateTime.Now.Hour >= 19 ? 0 : DateTime.Now.Minute, 
                            0),
                    };
                    setter.WayListSettings.WayListPoints.Add(finishpoint);
                }
                else
                {
                    finishpoint.Point_address = cbFinishPoint.Text;
                    finishpoint.Speedmeter = Convert.ToInt16(tbSpeedMeter.Text);
                    finishpoint.Gaznumber_buyed = Convert.ToDecimal(tbGaznumber_buyed.Text == "," ? "0" : tbGaznumber_buyed.Text);
                    finishpoint.Gaznumber_onpoint = Convert.ToDecimal(tbGaznumber_onpoint.Text == "," ? "0" : tbGaznumber_onpoint.Text);
                    finishpoint.Point_enter = new DateTime(
                        setter.Report_date.Year, 
                        setter.Report_date.Month, 
                        setter.Report_date.Day, 
                        DateTime.Now.Hour > 19 ? 19 : DateTime.Now.Hour, 
                        DateTime.Now.Hour >= 19 ? 0 : DateTime.Now.Minute, 
                        0);
                }

                setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                this.Cancel = false;
                this.Hide();
            }
            else
                MessageBox.Show("Заполните адрес возврата транспорта","Предупреждение", MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        
        public void Reload()
        {
            InitWayListForm(setter.Report_date);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //e.Cancel = true;
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.F4)
                e.Handled = true;
        }

        private void InitWayListForm(DateTime report_date)
        {
            gbWayListInit.Header = "Конечная точка путевого листа " + setter.Report_date.ToString("dd.MM.yyyy");

            WayListPoint finishpoint = GetWayListPoint(setter.Report_date);
            cbFinishPoint.ItemsSource = setter.WayListSettings.StartEndPoints;
            if (finishpoint != null)
            {
                if (setter.WayListSettings.StartEndPoints.Contains(finishpoint.Point_address))
                    cbFinishPoint.SelectedItem = finishpoint.Point_address;
                tbSpeedMeter.Text = finishpoint.Speedmeter.ToString();
                tbGaznumber_buyed.Text = finishpoint.Gaznumber_buyed.ToString();
                tbGaznumber_onpoint.Text = finishpoint.Gaznumber_onpoint.ToString();
            }
        }
        private WayListPoint GetWayListPoint(DateTime report_date)
        {
            return (from sp in setter.WayListSettings.WayListPoints
                    where sp.Point_type == WayListPointTypes.Finish
                    && sp.Report_date.Date == report_date.Date
                    select sp).FirstOrDefault();
        }

        private void bClose_Click(object sender, RoutedEventArgs e)
        {
            this.Cancel = true;
            this.Hide();
        }
    }
}
