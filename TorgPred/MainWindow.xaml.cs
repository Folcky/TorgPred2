using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.OleDb;
using System.Data;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Media;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.IO.Packaging;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Checksums;
using ICSharpCode.SharpZipLib.Zip;
using System.Threading;
using System.Reflection;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            stocks.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(stocks_CollectionChanged);
            uploads.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(uploads_CollectionChanged);
            dgStocks.ItemsSource = stocks;
            dgUploads.ItemsSource = uploads;
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
            get { return _setter;}
        }

        DateTimeFormatInfo MyCalendar = DateTimeFormatInfo.CurrentInfo;
        private string[] date_formats = { "dd.MM.yyyy HH:mm", "dd.MM.yyyy H:mm", "dd.MM.yyyy H:mm:ss", "dd.MM.yyyy" };
        private string aps_file;
        private string newaps_file;
        private string tariff_file;
        private string shortstock_file_mask = "остатки";
        private string stock_file_mask = "остатки_детально";
        private string stock_file_ext = "csv";
        private string stock_file_delimiter = ";";
        private string upload_file_mask = "отгрузки_детально";
        private string upload_file_ext = "csv";
        private string upload_file_delimiter = ";";
        private string newaps_file_mask = "новые_точки";
        private string newaps_file_ext = "csv";
        private string newaps_file_delimiter = ";";
        private const long BUFFER_SIZE = 4096;
        BrushConverter brushconverter = new BrushConverter();

        //DateTime Report_date;
        int ipsps_position=0;
        public ObservableCollection<IPandSPRecord> ipsps = new ObservableCollection<IPandSPRecord>();
        public ObservableCollection<string> tariffs
        {
            get;
            set;
        }
        public ObservableCollection<string> tariffs_for_edit
        {
            get;
            set;
        }

        public ObservableCollection<string> AddedDescriptions
        {
            get;
            set;
        }

        public ObservableCollection<UploadRecord> upload_week_data
        {
            get;
            set;
        }

        ObservableCollection<StockRecord> stocks = new ObservableCollection<StockRecord>();
        ObservableCollection<UploadRecord> uploads = new ObservableCollection<UploadRecord>();
        ObservableCollection<WayListPoint> waylistpoints = new ObservableCollection<WayListPoint>();
        ObservableCollection<string> comments = new ObservableCollection<string>();

        public WayListMode waylistmode_window{get;set;}
        public FinishWayPointView finishpoint_window{get;set;}


        DataSet aps_scheme = new DataSet();
        DataTable aps_table = new DataTable("APS");

        private void NAR(object o)
        {
            try
            {
                while (System.Runtime.InteropServices.Marshal.ReleaseComObject(o) > 0) ;
            }
            catch { }
            finally
            {
                o = null;
            }
        }

        protected void dgStocks_ReadOnly()
        {
            if (stocks != null && tariffs!=null)
            if (stocks.Count < tariffs.Count)
                dgStocks.CanUserAddRows = true;
            else
                dgStocks.CanUserAddRows = false;
        }

        protected void stocks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (cbIPs.SelectedValue != null && cbSPs.SelectedItem!=null)
            {
                string selectedip = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                IPandSPRecord selectedsp = (cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord;

                //Заполняем снова tariffs_for_edit
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    tariffs_for_edit.Clear();
                    foreach (string s in tariffs)
                        tariffs_for_edit.Add(s);
                }
                //Заполняем недостающие поля stocks для новых записей
                if (stocks.Count() > 0 && e.Action == NotifyCollectionChangedAction.Add)
                {
                    if (selectedsp != null && ipsps.Contains(selectedsp))
                    {
                        selectedsp.Has_StockData = true;
                        ColorcbIPItems();
                        ColorcbSPItems();
                    }

                    dgStocks_ReadOnly();
                    foreach (StockRecord s in e.NewItems)
                    {
                        if ((s.IP_name == null || s.IP_name == "") && cbIPs.SelectedValue != null)
                            s.IP_name = selectedip;
                        if ((s.SP_code == null || s.SP_code == "") && cbSPs.SelectedItem != null)
                        {
                            s.SP_code = selectedsp.SP_code;
                            s.SP_type = (from l in ipsps
                                         where l.SP_code.Trim() == s.SP_code.Trim()
                                         && l.IP_name.Trim() == s.IP_name.Trim()
                                         select l.SP_type).FirstOrDefault();
                        }
                        if (((DateTime)dpReportDate.SelectedDate).ToString("dd.MM.yyyy") != DateTime.Now.ToString("dd.MM.yyyy"))
                            s.Report_date = (DateTime)dpReportDate.SelectedDate;
                        else
                            s.Report_date = DateTime.Now;
                        if (s.TP_name == "" || s.TP_name == null)
                            s.TP_name = setter.LastUser;
                    }
                }

                if (stocks.Count() == 0 && e.Action == NotifyCollectionChangedAction.Remove)
                    UpdateStockFile(stocks, true);
            }
        }

        private delegate void DelegateCountUploads();
        protected void CountUploads()
        {
            try {
                int i = (from u in uploads
                        where u.ICC_id.Trim().Replace("\b","") != ""
                        select u.ICC_id).Distinct().Count();
                decimal sum = (from u in uploads
                               where u.ICC_id.Trim().Replace("\b", "") != ""
                               && u.Repeater==false
                         select u.SIM_price).Sum();
                tiUpload.Header = "Отгрузки " + i + "шт. " + sum + "руб.";
            }
            catch { tiUpload.Header = "Отгрузки"; }
        }

        private void uploadrecord_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == "ICC_id" || e.PropertyName == "SIM_price"
                    //&& (sender as UploadRecord).ICC_id.Trim() == ""
                    )
                {
                    DelegateCountUploads _updatecountuploads = CountUploads;
                    Application.Current.Dispatcher.BeginInvoke(_updatecountuploads, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch { }
        }


        protected void uploads_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (cbIPs.SelectedValue != null && cbSPs.SelectedItem != null)
            {
                string selectedip = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                IPandSPRecord selectedsp = (cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord;

                if (uploads.Count() > 0 && e.Action == NotifyCollectionChangedAction.Add)
                {
                    if (selectedsp != null && ipsps.Contains(selectedsp))
                    {
                        selectedsp.Has_UploadData = true;
                        ColorcbIPItems();
                        ColorcbSPItems();
                    }


                    if (uploads.Count() == 1 && e.Action == NotifyCollectionChangedAction.Add && e.NewItems.Count == 1)
                    {
                        bool _default_comm_flag = selectedsp.COMM_flag;
                        foreach (UploadRecord s in e.NewItems)
                        {
                            s.COMM_flag = _default_comm_flag;
                            //s.PropertyChanged += new PropertyChangedEventHandler(uploadrecord_PropertyChanged);
                        }
                    }
                    int max_index = uploads.Count() - e.NewItems.Count - 1;
                    bool _comm_flag = false;
                    decimal _sim_price = 0;
                    string _comment = "";
                    if (max_index >= 0)
                    {
                        _comm_flag = uploads.ElementAt(max_index).COMM_flag;
                        _sim_price = uploads.ElementAt(max_index).SIM_price;
                        _comment = uploads.ElementAt(max_index).COMMENT_text;
                        if ("Изменения не будут сохранены." == _comment.Trim() || _comment.ToUpper().Contains("ПОВТОР"))
                            _comment = "";
                    }
                    foreach (UploadRecord s in e.NewItems)
                    {
                        s.PropertyChanged += new PropertyChangedEventHandler(uploadrecord_PropertyChanged);
                        if (max_index >= 0)
                            s.COMM_flag = _comm_flag;
                        s.SIM_price = _sim_price;
                        s.COMMENT_text = _comment;
                        if ((s.IP_name == null || s.IP_name == "") && cbIPs.SelectedValue != null)
                            s.IP_name = (cbIPs.SelectedValue as ComboBoxItem).Content.ToString();
                        if ((s.SP_code == null || s.SP_code == "") && cbSPs.SelectedValue != null)
                            s.SP_code = selectedsp.SP_code;
                        if (((DateTime)dpReportDate.SelectedDate).ToString("dd.MM.yyyy") != DateTime.Now.ToString("dd.MM.yyyy"))
                        {
                            s.Report_date =
                                        new DateTime(
                                            ((DateTime)dpReportDate.SelectedDate).Year,
                                            ((DateTime)dpReportDate.SelectedDate).Month,
                                            ((DateTime)dpReportDate.SelectedDate).Day,
                                            DateTime.Now.Hour,
                                            DateTime.Now.Minute,
                                            DateTime.Now.Second);
                        }
                        else
                            s.Report_date = DateTime.Now;
                        if (s.TP_name == "" || s.TP_name == null)
                            s.TP_name = setter.LastUser;
                    }
                }
            }
        }

        private void TimeHandler(object sender, EventArgs e)
        {
            dpReportDate.SelectedDate = DateTime.Now;
        }


        private ObservableCollection<string> DistinctAddresses(string week_day)
        {
            WaylistDateMode datemode = setter.GetWaylistDateMode(setter.Report_date);
            bool get_all_sps = datemode != null ? datemode.FilterAPSwithTP : false;

            if (week_day.ToUpper() != "Ежедневно".ToUpper())
                return new ObservableCollection<string>(
                                (from i in ipsps
                                 where (i.Week_day.ToUpper() == week_day.ToUpper() || i.Week_day.ToUpper() == "Ежедневно".ToUpper()) &&
                                 (i.TP_name == setter.LastUser || get_all_sps)
                                 select i.SP_address).Distinct());
            else
                return new ObservableCollection<string>(
                                    (from s in ipsps
                                         where s.TP_name == setter.LastUser || get_all_sps
                                         select s.SP_address).Distinct()
                                     );

        }

        private ObservableCollection<ComboBoxItem> DistinctIPs(string week_day)
        {
            WaylistDateMode datemode = setter.GetWaylistDateMode(setter.Report_date);
            bool get_all_sps = datemode!=null ? datemode.FilterAPSwithTP : false;

            if (week_day.ToUpper() != "Ежедневно".ToUpper())
                return new ObservableCollection<ComboBoxItem>(
                                (from i in ipsps
                                 where (i.Week_day.ToUpper() == week_day.ToUpper() || i.Week_day.ToUpper() == "Ежедневно".ToUpper()) &&
                                 (i.TP_name == setter.LastUser || get_all_sps)
                                 select i.IP_name).Distinct().OrderBy(p => p)
                                 .Select(p => new ComboBoxItem() { Content = p }));
            else
                return new ObservableCollection<ComboBoxItem>(
                                        (from s in ipsps
                                        where s.TP_name == setter.LastUser || get_all_sps
                                        select s.IP_name)
                                        .Distinct().OrderBy(p => p)
                                        .Select(p => new ComboBoxItem() { Content = p })
                                        );
        }

        private void ColorcbIPItems()
        {
            foreach (object item in cbIPs.Items)
            {
                if (item.GetType() == typeof(ComboBoxItem))
                {
                    IPandSPRecord checker = ipsps.Where(p => p.IP_name == (item as ComboBoxItem).Content as string && (p.Has_StockData || p.Has_UploadData)).FirstOrDefault();
                    if (checker != null)
                    {
                        (item as ComboBoxItem).Background = (Brush)brushconverter.ConvertFromString(setter.CustomBack);
                        (item as ComboBoxItem).Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                    }
                }
            }
        }

        private void ColorcbSPItems()
        {
            foreach (object item in cbSPs.Items)
            {
                if (item.GetType() == typeof(ComboBoxItem))
                {
                    IPandSPRecord checker = (item as ComboBoxItem).DataContext as IPandSPRecord;
                    if (checker != null && (checker.Has_StockData || checker.Has_UploadData))
                    {
                        (item as ComboBoxItem).Background = (Brush)brushconverter.ConvertFromString(setter.CustomBack);
                        (item as ComboBoxItem).Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                    }
                }
            }
        }

        private void IPDefineItemsSource(string ru_week_day)
        {
            cbIPs.SelectionChanged -= new SelectionChangedEventHandler(cbIPs_SelectionChanged);
            cbIPs.ItemsSource = DistinctIPs(ru_week_day);
            cbIPs.SelectedIndex = -1;
            cbIPs.SelectionChanged += new SelectionChangedEventHandler(cbIPs_SelectionChanged);
            cbIPs.SelectedIndex = 0;
        }

        private void FilterIPSPbyWeekDay()
        {
            dpReportDate.FirstDayOfWeek=DayOfWeek.Monday;
            if ((bool)cbFilterIPSPbyWeekday.IsChecked)
            {
                switch (dpReportDate.SelectedDate.Value.DayOfWeek)
                {
                    case (DayOfWeek.Monday):
                        IPDefineItemsSource("Понедельник");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Понедельник");
                        break;
                    case (DayOfWeek.Tuesday):
                        IPDefineItemsSource("Вторник");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Вторник");
                        break;
                    case (DayOfWeek.Wednesday):
                        IPDefineItemsSource("Среда");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Среда");
                        break;
                    case (DayOfWeek.Thursday):
                        IPDefineItemsSource("Четверг");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Четверг");
                        break;
                    case (DayOfWeek.Friday):
                        IPDefineItemsSource("Пятница");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Пятница");
                        break;
                    case (DayOfWeek.Saturday):
                        IPDefineItemsSource("Суббота");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Суббота");
                        break;
                    case (DayOfWeek.Sunday):
                        IPDefineItemsSource("Воскресенье");
                        cbDistinctAddresses.ItemsSource = DistinctAddresses("Воскресенье");
                        break;
                }
            }
            else
            {
                IPDefineItemsSource("Ежедневно");
                cbDistinctAddresses.ItemsSource = DistinctAddresses("Ежедневно");
            }
            ColorcbIPItems();
        }

        private void LoadCommentCSV(string comment_file)
        {
            comments = new ObservableCollection<string>();
            if (comment_file != null && File.Exists(comment_file))
                try
                {
                    using (TextReader tr = new StreamReader(comment_file, Encoding.Default))
                    {
                        string line;
                        while ((line = tr.ReadLine()) != null)
                        {
                            string[] data = setter.CSVrow2StringArray(line);
                            if (data[0] != null && data[0].ToString().Trim() != "")
                            {
                                try
                                {
                                    comments.Add(data[0].ToString().Trim());
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
        }

        private void LoadApsCSV(string aps_file)
        {
            ipsps = new ObservableCollection<IPandSPRecord>();
            if (aps_file != null)
                try
                {
                    string line;
                    int sp_col_id = 11;
                    int sp_type_col_id = 12;
                    int ip_col_id = 13;
                    int tp_col_id = 14;
                    int weekday_col_id = 15;
                    int comm_flag_col_id = 16;
                    int[] spaddress_col_ids = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                    int addeddesc_col_id = 10;
                    if (File.Exists(aps_file))
                    {
                        using (TextReader tr = new StreamReader(aps_file, Encoding.Default))
                        {
                            line = tr.ReadLine();
                            while ((line = tr.ReadLine()) != null)
                            {
                                string[] data = setter.CSVrow2StringArray(line);
                                ipsps.Add(new IPandSPRecord
                                {
                                    IP_name = setter.CSVField2String(data[ip_col_id - 1]),
                                    SP_code = setter.CSVField2String(data[sp_col_id - 1]),
                                    SP_type = setter.CSVField2String(data[sp_type_col_id - 1]),
                                    COMM_flag = setter.CSVField2String(data[comm_flag_col_id - 1]).Trim() == "0" ? false : true,
                                    Week_day = setter.CSVField2String(data[weekday_col_id - 1]),

                                    TP_name = setter.CSVField2String(data[tp_col_id - 1]).Trim(),

                                    SP_region = setter.CSVField2String(data[spaddress_col_ids[0] - 1]).Trim(),
                                    SP_town = setter.CSVField2String(data[spaddress_col_ids[1] - 1]).Trim(),
                                    SP_town_type = setter.CSVField2String(data[spaddress_col_ids[2] - 1]).Trim(),
                                    SP_town_area = setter.CSVField2String(data[spaddress_col_ids[3] - 1]).Trim(),
                                    SP_subway = setter.CSVField2String(data[spaddress_col_ids[4] - 1]).Trim(),
                                    SP_street = setter.CSVField2String(data[spaddress_col_ids[5] - 1]).Trim(),
                                    SP_street_type = setter.CSVField2String(data[spaddress_col_ids[6] - 1]).Trim(),
                                    SP_house = setter.CSVField2String(data[spaddress_col_ids[7] - 1]).Trim(),
                                    SP_house_building = setter.CSVField2String(data[spaddress_col_ids[8] - 1]).Trim(),
                                    SP_added_description = setter.CSVField2String(data[addeddesc_col_id - 1]).Trim()
                                });
                            }
                        }
                    }
                }
                catch { }
        }

        private void LoadNewApsCSV(string newaps_file)
        {
            if (newaps_file != null)
                try
                {
                    string line;
                    int sp_col_id = 11;
                    int sp_type_col_id = 12;
                    int ip_col_id = 13;
                    int tp_col_id = 14;
                    int weekday_col_id = 15;
                    int comm_flag_col_id = 16;
                    int[] spaddress_col_ids = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
                    int addeddesc_col_id = 10;
                    if (File.Exists(aps_file))
                    {
                        using (TextReader tr = new StreamReader(newaps_file, Encoding.Default))
                        {
                            while ((line = tr.ReadLine()) != null)
                            {
                                string[] data = setter.CSVrow2StringArray(line);
                                try
                                {
                                    if (data[tp_col_id - 1].ToString() == setter.LastUser)
                                    {
                                        ipsps.Add(new IPandSPRecord
                                        {
                                            IP_name = setter.CSVField2String(data[ip_col_id - 1]),
                                            SP_code = setter.CSVField2String(data[sp_col_id - 1]),
                                            SP_type = setter.CSVField2String(data[sp_type_col_id - 1]),
                                            COMM_flag = setter.CSVField2String(data[comm_flag_col_id - 1]).Trim() == "0" ? false : true,
                                            Week_day = setter.CSVField2String(data[weekday_col_id - 1]),

                                            TP_name = setter.CSVField2String(data[tp_col_id - 1]).Trim(),

                                            SP_region = setter.CSVField2String(data[spaddress_col_ids[0] - 1]).Trim(),
                                            SP_town = setter.CSVField2String(data[spaddress_col_ids[1] - 1]).Trim(),
                                            SP_town_type = setter.CSVField2String(data[spaddress_col_ids[2] - 1]).Trim(),
                                            SP_town_area = setter.CSVField2String(data[spaddress_col_ids[3] - 1]).Trim(),
                                            SP_subway = setter.CSVField2String(data[spaddress_col_ids[4] - 1]).Trim(),
                                            SP_street = setter.CSVField2String(data[spaddress_col_ids[5] - 1]).Trim(),
                                            SP_street_type = setter.CSVField2String(data[spaddress_col_ids[6] - 1]).Trim(),
                                            SP_house = setter.CSVField2String(data[spaddress_col_ids[7] - 1]).Trim(),
                                            SP_house_building = setter.CSVField2String(data[spaddress_col_ids[8] - 1]).Trim(),
                                            SP_added_description = setter.CSVField2String(data[addeddesc_col_id - 1]).Trim(),
                                            New_sp = true
                                        });
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
        }

        private ObservableCollection<string> LoadTariffsCSV(string tariff_file)
        {
            ObservableCollection<string> result = new ObservableCollection<string>();
            string line;
            if (tariff_file != null)
                try
                {
                    if (File.Exists(tariff_file))
                    {
                        using (TextReader tr = new StreamReader(tariff_file, Encoding.Default))
                        {
                            while ((line = tr.ReadLine()) != null)
                            {
                                string[] data = setter.CSVrow2StringArray(line);
                                if (data[0] != null && data[0].ToString().Trim() != "")
                                    result.Add(data[0].ToString().Trim());
                            }
                        }
                    }
                }
                catch { }
            return result;
        }

        private void setter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "WayListMode")
            {
                setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                WayListLocker();
            }
            if (e.PropertyName == "LastUser")
            {
                lTP.Content = setter.LastUser;
                Window_Loaded(this, new RoutedEventArgs());
            }
            if (e.PropertyName == "Report_date")
            {
                dpReportDate.SelectedDate = setter.Report_date;
            }
            if (e.PropertyName == "FilterAPS")
            {
                FilterIPSPbyWeekDay();
            }
        }

        private void HasDataLoader()
        {
            var stockdataowners = (from l in GetStockData()
                                   where l.Report_date.Date == setter.Report_date.Date
                                   select new { IP_name = l.IP_name, SP_code = l.SP_code }).Distinct();

            var uploaddataowners = (from l in GetUploadData()
                                    where l.Report_date.Date == setter.Report_date.Date
                                    select new { IP_name = l.IP_name, SP_code = l.SP_code }).Distinct();

            foreach (IPandSPRecord ipsp in ipsps)
            {
                if (stockdataowners.Contains(new { IP_name = ipsp.IP_name, SP_code = ipsp.SP_code }))
                { 
                    ipsp.Has_StockData = true;
                }
                else
                    ipsp.Has_StockData = false;
                
                if (uploaddataowners.Contains(new { IP_name = ipsp.IP_name, SP_code = ipsp.SP_code }))
                {
                    ipsp.Has_UploadData = true;
                }
                else
                    ipsp.Has_UploadData = false;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;

            dpReportDate.SelectedDate = setter.Report_date;

            lTP.Content = setter.LastUser;

            LoadWaylist();

            //dgWayList.ItemsSource = waylistpoints;

            //Загружаем адресный план
            if (File.Exists(setter.WorkDir + @"\APs.csv"))
            {
                aps_file = setter.WorkDir + @"\APs.csv";
                LoadApsCSV(aps_file);
            }

            //Загружаем список новых точек
            if (File.Exists(setter.WorkDir + @"\NEW_APS.csv"))
            {
                newaps_file = setter.WorkDir + @"\NEW_APS.csv";
                LoadNewApsCSV(newaps_file);
            }
            
            //Загружаем комментарии
            if (File.Exists(setter.WorkDir + @"\Comments.csv"))
            {
                aps_file = setter.WorkDir + @"\Comments.csv";
                LoadCommentCSV(aps_file);
            }

            if (File.Exists(setter.WorkDir + @"\TP_list.csv"))
            {
                tariff_file = setter.WorkDir + @"\TP_list.csv";
                tariffs = LoadTariffsCSV(tariff_file);
                tariffs_for_edit = LoadTariffsCSV(tariff_file);
            }

            //TODO Исключить дублирование с кнопкой bFirst
            ipsps_position = 0;
            GetIPSPSData(ipsps_position);
            bPrevious.IsEnabled = false;
            bNext.IsEnabled = true;
            EnableButtons(ipsps_position);

            if(ipsps.Count==0)
            {
                stocks.Clear();
                uploads.Clear();
                MessageBox.Show("Для Вас в адресном плане нет данных!","Ошибка",MessageBoxButton.OK,MessageBoxImage.Stop);
                this.setter.ShowSetter = true;
                this.Hide();
            }

            HasDataLoader();

            WayListLocker();

            //Изменение вкладки вызывает сохранение файлов
            //tcOperations.SelectionChanged += new SelectionChangedEventHandler(tcOperations_SelectionChanged);

            this.DataContext = this;
            try
            {
                string filterAPS = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\TorgPred\FilterAPS", "", null);
                if (filterAPS==null) filterAPS="";
                if (filterAPS.Trim() == "1")
                    cbFilterIPSPbyWeekday.IsChecked = true;
                FilterIPSPbyWeekDay();
            }
            catch 
            {
                //Определить список ИПшников
                FilterIPSPbyWeekDay();
            }
            //Подписка на изменение свойств setter5
            //setter.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(setter_PropertyChanged);
            //setter.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(setter_PropertyChanged);
        }

        public void LoadWaylist()
        {
            List<UIElement> all_spw_objects = (from l in spWayListPoints.Children.Cast<UIElement>()
                     //where l.GetType() == typeof(WayListView)
                     select l).ToList();

            Button baddwp = (from l in all_spw_objects
                             where l.GetType() == typeof(Button)
                             select l).FirstOrDefault() as Button;

            foreach (object spwchild in all_spw_objects)
            {
                if (spwchild.GetType() == typeof(WayListView))
                    spWayListPoints.Children.Remove(spwchild as UIElement);
                if (spwchild.GetType() == typeof(Button))
                    spWayListPoints.Children.Remove(spwchild as UIElement);
            }

            waylistpoints = new ObservableCollection<WayListPoint>(from r in setter.WayListSettings.WayListPoints
                            where r.Report_date.Date == setter.Report_date.Date
                            select r);
            foreach (WayListPoint wlp in waylistpoints)
            {
                WayListView wlw = new WayListView() { WayListPointProperty = wlp, FontSize=13, setter=this.setter };
                wlw.WayListPointUpdated +=new PropertyChangedEventHandler(wlw_WayListPointUpdated);
                spWayListPoints.Children.Add(wlw);
            }

            if (baddwp != null)
                spWayListPoints.Children.Add(baddwp);
        }

        private void wlw_WayListPointUpdated(object sender, PropertyChangedEventArgs e)
        {
            setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
            if (e.PropertyName == "WayListPointProperty_deleted")
                LoadWaylist();
            WayListLocker();
        }

        private string GetDataFilename(string dir, string report_week, string mask, string ext)
        {
            return dir + String.Format(@"\{0}_{3}_{1}.{2}", setter.LastUser, report_week, ext, mask);
        }

        private bool UpdateStockFile(ObservableCollection<StockRecord> data, bool delete=false)
        {
            try
            {
                    ObservableCollection<StockRecord> result = new ObservableCollection<StockRecord>();
                    foreach (StockRecord data_item in data)
                    {
                        if (data_item.IP_name != null && data_item.IP_name.Trim() != "")
                            result.Add(data_item);
                    }

                    if (result.Count > 0 || delete)
                    {
                        string ip_string="";
                        string sp_code_string = "";
                        string report_date = "";
                        string report_week = "";

                        if (result.Count > 0)
                        {
                            ip_string = result.ElementAt(0).IP_name;
                            sp_code_string = result.ElementAt(0).SP_code;
                            report_date = result.ElementAt(0).Report_date.ToString("yyyyMMdd");
                            report_week = MyCalendar.Calendar.GetWeekOfYear(result.ElementAt(0).Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
                        }
                        else
                            if (delete)
                            { 
                                if(cbIPs.SelectedValue!=null)
                                    ip_string = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                                if (cbSPs.SelectedItem != null && (cbSPs.SelectedItem as ComboBoxItem).DataContext!=null)
                                    sp_code_string = ((cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord).SP_code;
                                report_date = setter.Report_date.ToString("yyyyMMdd");
                                report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
                            }

                            foreach (StockRecord item in GetStockData().Where(p => !(ip_string == p.IP_name && sp_code_string == p.SP_code && p.Report_date.ToString("yyyyMMdd") == report_date)))
                            {
                                result.Add(item);
                            }

                            string filename_stock = GetDataFilename(setter.WorkDir, report_week, stock_file_mask, stock_file_ext);

                            result = new ObservableCollection<StockRecord>(
                                                                            from st in result
                                                                            where (st.Tariff_name != null && st.Tariff_name.Trim() != "")
                                                                            group st by new { st.IP_name, st.SP_code, st.Tariff_name, report = st.Report_date.ToString("yyyyMMdd") } into grp
                                                                            select grp.First());

                            using (FileStream fs = new FileStream(filename_stock, FileMode.Create))
                            {
                                using (StreamWriter w = new StreamWriter(fs, Encoding.Default))
                                {
                                    w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}",
                                            stock_file_delimiter,
                                            "Торговая точка",
                                            "Субдилер",
                                            "Тарифный план",
                                            "Количество SIM",
                                            "ДатаВремя",
                                            "Торговый представитель",
                                            "Тип ТТ",
                                            "Комментарий"));
                                    foreach (StockRecord stock_record in result)
                                    {
                                        w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}",
                                            stock_file_delimiter,
                                            stock_record.SP_code,
                                            stock_record.IP_name,
                                            stock_record.Tariff_name,
                                            stock_record.Sim_num,
                                            stock_record.Report_date.ToString("dd.MM.yyyy HH:mm"),
                                            stock_record.TP_name,
                                            stock_record.SP_type,
                                            (setter.String2CSVField(stock_record.COMMENT_text))
                                            ));
                                    }
                                }
                            }
                    }
                    Microsoft.Win32.SystemEvents.TimeChanged += new EventHandler(TimeHandler);
                    return true;
            }
            catch (Exception usf)
            {
                //MessageBox.Show(usf.Message, "Сохранение остатков", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private ObservableCollection<StockRecord> GetStockData()
        {
            ObservableCollection<StockRecord> result = new ObservableCollection<StockRecord>();

            string report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
            string report_date = setter.Report_date.ToString("yyyyMMdd");

            string filename_stock = GetDataFilename(setter.WorkDir, report_week, stock_file_mask, stock_file_ext);
            string line;
            if (File.Exists(filename_stock))
            {
                using (TextReader tr = new StreamReader(filename_stock, Encoding.Default))
                {
                    while ((line = tr.ReadLine()) != null)
                    {
                        string[] data = setter.CSVrow2StringArray(line);
                        try
                        {
                            DateTime data_date = DateTime.ParseExact(data.ElementAt(4), date_formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
                            result.Add(new StockRecord()
                            {
                                SP_code = data.ElementAt(0),
                                IP_name = data.ElementAt(1),
                                Tariff_name = data.ElementAt(2),
                                Sim_num = Convert.ToInt16(data.ElementAt(3)),
                                Report_date = data_date,
                                TP_name = data.ElementAt(5),
                                SP_type = data.Count() >= 7 ? data.ElementAt(6) : "",
                                COMMENT_text = data.Count() >= 8 ? data.ElementAt(7).Replace("\"", "") : ""
                            });
                        }
                        catch { }
                    }
                }
            }
            return result;
        }

        private void LoadStockFile(object ip, object sp_code)
        {
            if (ip != null)
            {
                string ip_string = ip == null ? "" : ip.ToString();
                string sp_code_string = sp_code == null ? "" : (sp_code as IPandSPRecord).SP_code.ToString();

                string report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
                string report_date = setter.Report_date.ToString("yyyyMMdd");

                //Когда списки ИП становятся пустыми, надо очистить
                if (ip_string == "")
                    stocks.Clear();

                if (ip != null && report_week != null)
                {
                    UpdateStockFile(stocks);
                    stocks.Clear();

                    stocks.CollectionChanged -= new System.Collections.Specialized.NotifyCollectionChangedEventHandler(stocks_CollectionChanged);
                    foreach (StockRecord item in GetStockData().Where(p => (p.IP_name == ip_string && p.SP_code == sp_code_string && p.Report_date.ToString("yyyyMMdd") == report_date)))
                    {
                        stocks.Add(item);
                    }
                    stocks.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(stocks_CollectionChanged);
                    dgStocks_ReadOnly();
                    Scroll2End(dgStocks);
                }
            }
        }

        private bool UpdateUploadFile(ObservableCollection<UploadRecord> data)
        {
            try
            {
                ObservableCollection<UploadRecord> result = new ObservableCollection<UploadRecord>(data);

                if (result.Count > 0)
                {
                    UploadRecord ipsp_excluder = result.ElementAt(0);
                    //Distinct список недель
                    string report_week = MyCalendar.Calendar.GetWeekOfYear(result.ElementAt(0).Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
                    foreach (UploadRecord item in GetUploadData().Where(p => !(ipsp_excluder.IP_name == p.IP_name && ipsp_excluder.SP_code == p.SP_code && p.Report_date.ToString("yyyyMMdd") == ipsp_excluder.Report_date.ToString("yyyyMMdd"))))
                    {
                        result.Add(item);
                    }

                    string filename_upload = GetDataFilename(setter.WorkDir, report_week, upload_file_mask, upload_file_ext);

                    var query = from n in result
                                where !n.Repeater
                                group n by new { n.ICC_id } into g
                                select new { ICC_ID = g.Key.ICC_id, Report_date = g.Max(t => t.Report_date) };

                    result = new ObservableCollection<UploadRecord>(
                                                        from st in result
                                                        from q in query
                                                        where st.ICC_id != null && st.ICC_id.Trim().Replace("\b", "") != "" &&
                                                        st.ICC_id == q.ICC_ID && st.Report_date == q.Report_date
                                                        group st by new { st.ICC_id, st.IP_name } into grp
                                                        select grp.First()
                                                        );

                    using (FileStream fs = new FileStream(filename_upload, FileMode.Create))
                    {
                        using (StreamWriter w = new StreamWriter(fs, Encoding.Default))
                        {
                            w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}",
                                    upload_file_delimiter,
                                    "Номер SIM",
                                    "Субдилер",
                                    "Торговая точка",
                                    "ДатаВремя",
                                    "Комиссия",
                                    "Стоимость",
                                    "Торговый представитель",
                                    "Комментарий"
                                    ));
                            foreach (UploadRecord upload_record in result)
                            {
                                try
                                {
                                    if (upload_record.IP_name.Trim() != "" && upload_record.ICC_id.Trim() != "")
                                        w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}",
                                            upload_file_delimiter,
                                            upload_record.ICC_id.Trim().Replace("\b", ""),
                                            upload_record.IP_name,
                                            upload_record.SP_code,
                                            upload_record.Report_date.ToString("dd.MM.yyyy HH:mm:ss"),
                                            upload_record.COMM_flag == false ? 0 : 1,
                                            upload_record.SIM_price,
                                            upload_record.TP_name,
                                            (setter.String2CSVField(upload_record.COMMENT_text))
                                            ));
                                }
                                catch { }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception usf)
            {
                MessageBox.Show(usf.Message, "Сохранение отгрузок", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private ObservableCollection<UploadRecord> GetUploadData()
        {
            ObservableCollection<UploadRecord> result = new ObservableCollection<UploadRecord>();

            string report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
            string report_date = setter.Report_date.ToString("yyyyMMdd");

            string filename_upload = GetDataFilename(setter.WorkDir, report_week, upload_file_mask, upload_file_ext);
            string line;
            if (File.Exists(filename_upload))
            {
                using (TextReader tr = new StreamReader(filename_upload, Encoding.Default))
                {
                    while ((line = tr.ReadLine()) != null)
                    {
                        string[] data = setter.CSVrow2StringArray(line);
                        try
                        {
                            if (data.ElementAt(0).Trim().Replace("\b", "") != "")
                            {
                                UploadRecord upload = new UploadRecord()
                                {
                                    ICC_id = data.ElementAt(0).Trim().Replace("\b", ""),
                                    IP_name = data.ElementAt(1),
                                    SP_code = data.ElementAt(2),
                                    Report_date = DateTime.ParseExact(data.ElementAt(3), date_formats, CultureInfo.InvariantCulture, DateTimeStyles.None),
                                    COMM_flag = data.ElementAt(4).Trim() == "0" ? false : true,
                                    SIM_price = Convert.ToDecimal(data.ElementAt(5)),
                                    TP_name = data.ElementAt(6),
                                    COMMENT_text = data.ElementAt(7).Replace("\"", ""),
                                };
                                result.Add(upload);
                            }
                        }
                        catch { }
                    }
                }
            }
            return result;
        }

        private void LoadUploadFile(object ip, object sp_code)
        {
            string ip_string = ip == null ? "" : ip.ToString();
            string sp_code_string = sp_code == null ? "" : (sp_code as IPandSPRecord).SP_code.ToString();

            string report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
            string report_date = setter.Report_date.ToString("yyyyMMdd");

            //Когда списки ИП становятся пустыми, надо очистить
            if (ip_string == "")
                uploads.Clear();

            if (ip_string != "" && ip != null && report_week != null)
            {
                UpdateUploadFile(uploads);
                uploads.Clear();

                upload_week_data = GetUploadData();

                uploads.CollectionChanged -= new System.Collections.Specialized.NotifyCollectionChangedEventHandler(uploads_CollectionChanged);
                foreach (UploadRecord item in upload_week_data.Where(p => (p.IP_name == ip_string && p.SP_code == sp_code_string && p.Report_date.ToString("yyyyMMdd") == report_date)))
                {
                    uploads.Add(item);
                    item.PropertyChanged+=new PropertyChangedEventHandler(uploadrecord_PropertyChanged);
                }
                uploads.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler(uploads_CollectionChanged);
                Scroll2End(dgUploads);
            }
        }

        private void Scroll2End(DataGrid sender)
        {
            if (sender.Items.Count > 0 && sender.ItemsSource.Cast<object>().Count() > 0 && VisualTreeHelper.GetChildrenCount(sender)>0)
            {
                var border = VisualTreeHelper.GetChild(sender, 0) as Decorator;
                if (border != null)
                {
                    var scroll = border.Child as ScrollViewer;
                    if (scroll != null) scroll.ScrollToEnd();
                }
            }
        }

        private void cbIPs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cbIPs.SelectedValue != null)
                {
                    string selected_ip = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                    ObservableCollection<IPandSPRecord> distinctSPs = new ObservableCollection<IPandSPRecord>(
                        (from sp in ipsps
                         where sp.IP_name.Trim() == selected_ip.Trim()
                         && ((bool)cbFilterIPSPbyWeekday.IsChecked && 
                         (sp.Week_day.ToLower().Trim()=="ежедневно" || sp.Week_day.ToUpper() == GetRuWeekDay(((DateTime)dpReportDate.SelectedDate).DayOfWeek).ToUpper()) ||
                         !(bool)cbFilterIPSPbyWeekday.IsChecked)
                         select sp)
                         .GroupBy(p => p.SP_code)
                         .Select(g => g.First())
                        );
                    cbSPs.SelectionChanged -=new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                    cbSPs.ItemsSource = distinctSPs.Select(p => new ComboBoxItem() {Content=p.SP_code, DataContext = p });
                    cbSPs.SelectionChanged += new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                    cbSPaddresses.ItemsSource = distinctSPs;
                    cbSPs.SelectedIndex = 0;
                    IPandSPRecord selectedsp = ((cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord);
                    ColorcbSPItems();


                    cbDistinctAddresses.SelectionChanged -=new SelectionChangedEventHandler(cbDistinctAddresses_SelectionChanged);
                    cbAddedDescriptions.SelectionChanged -=new SelectionChangedEventHandler(cbAddedDescriptions_SelectionChanged);
                    cbDistinctAddresses.SelectedValue = ((cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord).SP_address;
                    ObservableCollection<string> distinctADs = new ObservableCollection<string>(
                        (from sp in ipsps
                         where sp.SP_address == selectedsp.SP_address
                         && ((bool)cbFilterIPSPbyWeekday.IsChecked &&
                         (sp.Week_day.ToLower().Trim() == "ежедневно" || sp.Week_day.ToUpper() == GetRuWeekDay(((DateTime)dpReportDate.SelectedDate).DayOfWeek).ToUpper()) ||
                         !(bool)cbFilterIPSPbyWeekday.IsChecked)
                         select sp.SP_added_description).OrderBy(p => p).Distinct()
                        );
                    cbAddedDescriptions.ItemsSource = distinctADs;
                    cbAddedDescriptions.SelectedValue = selectedsp.SP_added_description;
                    cbAddedDescriptions.SelectionChanged += new SelectionChangedEventHandler(cbAddedDescriptions_SelectionChanged);
                    cbDistinctAddresses.SelectionChanged += new SelectionChangedEventHandler(cbDistinctAddresses_SelectionChanged);

                    switch (distinctSPs.Count())
                    {
                        case (0):
                            lTTsList.Content = "Точек нет:";
                            break;
                        case (1):
                            lTTsList.Content = "1 торговая точка:";
                            break;
                        case (2):
                            lTTsList.Content = "2 торговые точки:";
                            break;
                        case (3):
                            lTTsList.Content = "3 торговые точки:";
                            break;
                        case (4):
                            lTTsList.Content = "4 торговые точки:";
                            break;
                        default:
                            lTTsList.Content = distinctSPs.Count()+" торговых точек:";
                            break;
                    }
                }
                else
                {
                    cbSPs.SelectionChanged -= new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                    cbSPs.ItemsSource = null;
                    cbSPs.SelectionChanged += new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                    cbSPaddresses.ItemsSource = null;
                }
            }
            catch { }
        }

        private bool WLPPresented(WayListPointTypes waypointtype, bool datadefined_required)
        {

            WayListPoint cwlp;

            if (datadefined_required)
                cwlp = (from l in setter.WayListSettings.WayListPoints
                        where l.Report_date.Date == setter.Report_date.Date
                        && l.Point_type == waypointtype
                        && l.DataDefined
                        select l).FirstOrDefault();
            else
                cwlp = (from l in setter.WayListSettings.WayListPoints
                        where l.Report_date.Date == setter.Report_date.Date
                        && l.Point_type == waypointtype
                        select l).FirstOrDefault();
            if (cwlp != null)
                return true;
            return false;
        }

        private bool CommonWLPwithIPSPPresented()
        {
            bool result = false;
            if (cbIPs.SelectedValue != null 
                && cbSPs.SelectedItem != null
                )
            {
                string selectedip = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                IPandSPRecord selectedsp = (cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord;

                string _ip_name = selectedip;
                string _sp_code = cbSPs.SelectedItem != null ? selectedsp.SP_code : "";
                WayListPoint cwlp = (from l in setter.WayListSettings.WayListPoints
                                     where l.Report_date.Date == setter.Report_date.Date
                                     && l.Point_type == WayListPointTypes.Common
                                     && l.IP_name == _ip_name
                                     && l.SP_code == _sp_code
                                     && l.DataDefined
                                     select l).FirstOrDefault();
                if (cwlp != null)
                    result = true;
            }
            return result;
        }

        private void WayListLocker()
        {
            switch (setter.WayListMode)
            {
                case (WayListModeSet.Market):
                    lWayListMode.Content = "Рынок";
                    if (WLPPresented(WayListPointTypes.Common, false))
                    {

                        dgStocks.IsReadOnly = false;
                        dgUploads.IsReadOnly = false;
                        bAddWayListPoint.Visibility = Visibility.Hidden;
                        lAdvice.Content = "Транзитная точка присутствует в путевом листе";
                        lAdvice.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                    }
                    else
                    {
                        dgStocks.IsReadOnly = true;
                        dgUploads.IsReadOnly = true;
                        bAddWayListPoint.Visibility = Visibility.Visible;
                        lAdvice.Content = "Добавьте транзитную точку в путевой лист!";
                        lAdvice.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                    }
                    tiWayListDoc.Visibility = Visibility.Visible;
                    break;
                case (WayListModeSet.OnTransport):
                    lWayListMode.Content = "На транспорте";
                    if (CommonWLPwithIPSPPresented())
                    {
                        dgStocks.IsReadOnly = false;
                        dgUploads.IsReadOnly = false;
                        lAdvice.Content = "Транзитная точка присутствует в путевом листе";
                        lAdvice.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                    }
                    else
                    {
                        dgStocks.IsReadOnly = true;
                        dgUploads.IsReadOnly = true;
                        lAdvice.Content = "Добавьте транзитную точку в путевой лист!";
                        lAdvice.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                    }
                    tiWayListDoc.Visibility = Visibility.Visible;
                    bAddWayListPoint.Visibility = Visibility.Visible;
                    break;
                case (WayListModeSet.Pedestrian):
                    lWayListMode.Content = "Пешеход";
                    dgStocks.IsReadOnly = false;
                    dgUploads.IsReadOnly = false;
                    tiWayListDoc.Visibility = Visibility.Hidden;
                    tcOperations.SelectedItem = tiStock;
                    bAddWayListPoint.Visibility = Visibility.Hidden;
                    lAdvice.Content = "Заполнение путевого листа не требуется";
                    lAdvice.Foreground = (Brush)brushconverter.ConvertFromString(setter.CustomGreen);
                    break;
            }
            if (cbIPs.SelectedItem != null && (cbIPs.SelectedItem as ComboBoxItem).Content.ToString().ToUpper().Trim() == "ЮТК".ToUpper())
            {
                dgStocks.IsReadOnly = false;
                dgUploads.IsReadOnly = false;
                tiStock.Visibility = Visibility.Hidden;
                if (tcOperations.SelectedItem == tiStock)
                    tcOperations.SelectedItem = tiUpload;
            }
            else
                tiStock.Visibility = Visibility.Visible;

            if (ipsps.Count() == 0)
            {
                dgStocks.IsReadOnly = true;
                dgUploads.IsReadOnly = true;
            }

        }

        private void cbSPs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IPandSPRecord selectedsp = ((cbSPs.SelectedValue as ComboBoxItem).DataContext as IPandSPRecord);

                lSPType.Content = selectedsp.SP_type;
                lCommFlag.Content = selectedsp.COMM_flag == true ? "В комиссию" : "Без комиссии";
                lCommFlag.Foreground = selectedsp.COMM_flag == true ? (Brush)brushconverter.ConvertFromString(setter.CustomGreen) : (Brush)brushconverter.ConvertFromString(setter.CustomRed);
                cbSPaddresses.SelectionChanged -=new SelectionChangedEventHandler(cbSPaddresses_SelectionChanged);
                cbSPaddresses.SelectedItem = selectedsp; 
                cbSPaddresses.SelectionChanged += new SelectionChangedEventHandler(cbSPaddresses_SelectionChanged);
                WayListLocker();
                LoadStockFile((cbIPs.SelectedItem as ComboBoxItem).Content, selectedsp);
                LoadUploadFile((cbIPs.SelectedItem as ComboBoxItem).Content, selectedsp);
                ipsps_position = ipsps.IndexOf(selectedsp);
                EnableButtons(ipsps_position);
                GetIPSPSData(ipsps_position);
                CountUploads();
            }
            catch { }
        }

         private void tcOperations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //e.Handled = true;
            //if (e.Source is TabControl) //if this event fired from TabControl then enter
            //{
            //    if (tiStock.IsSelected)
            //        LoadStockFile(cbIPs.SelectedValue, cbSPs.SelectedValue);
            //    if (tiUpload.IsSelected)
            //        LoadUploadFile(cbIPs.SelectedValue, cbSPs.SelectedValue);
            //}

            //int i = 1;
        }

        private void cbSPaddresses_TextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                foreach (ComboBoxItem i in cbSPaddresses.Items)
                {
                    if (i.Content.ToString().ToUpper().Contains(e.Text.ToUpper()))
                    {
                        cbSPaddresses.SelectedItem = i;
                        break;
                    }
                }
                e.Handled = true;
            }
            catch { }
        }

        private void cbSPaddresses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IPandSPRecord selectedsp = cbSPaddresses.SelectedItem as IPandSPRecord;
                cbSPs.SelectedItem = cbSPs.Items.Cast<ComboBoxItem>().First(o => (o.DataContext as IPandSPRecord).SP_code == selectedsp.SP_code); 
            }
            catch { }
        }

        private void cbDistinctAddresses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                //IPandSPRecord selectedsp = cbSPaddresses.SelectedValue as IPandSPRecord;
                //cbSPs.SelectedItem = cbSPs.Items.Cast<ComboBoxItem>().First(o => (o.DataContext as IPandSPRecord).SP_code == selectedsp.SP_code); 
                ObservableCollection<string> distinctADs = new ObservableCollection<string>(
                        (from sp in ipsps
                         where sp.SP_address == (string)cbDistinctAddresses.SelectedValue
                         && ((bool)cbFilterIPSPbyWeekday.IsChecked &&
                         (sp.Week_day.ToLower().Trim() == "ежедневно" || sp.Week_day.ToUpper() == GetRuWeekDay(((DateTime)dpReportDate.SelectedDate).DayOfWeek).ToUpper()) ||
                         !(bool)cbFilterIPSPbyWeekday.IsChecked)
                         select sp.SP_added_description).OrderBy(p => p).Distinct()
                        );
                cbAddedDescriptions.ItemsSource = distinctADs;
                cbAddedDescriptions.SelectedValue = ((cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord).SP_added_description;
            }
            catch { }
        }

        private void cbAddedDescriptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                IPandSPRecord sp_to_select = (from sp in ipsps
                                              where sp.SP_address == (string)cbDistinctAddresses.SelectedValue
                                              && sp.SP_added_description == (string)cbAddedDescriptions.SelectedValue
                                              select sp).FirstOrDefault();
                FocusOnIPSP(sp_to_select);
            }
            catch { }
        }

        private void rTopLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void dgcbStockTP_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            GridColumnFastEdit(cell, e);
        }

        private void DataGridCell_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            GridColumnFastEdit(cell, e);
        }

        private void dgcbStockTP_KeyUp(object sender, KeyEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            if (e.Key == Key.Space)
            {
                GridColumnFastEdit(cell, e);
            }
        }

        private void dgtcComment_KeyUp(object sender, KeyEventArgs e)
        {
            DataGridCell cell = sender as DataGridCell;
            if (cell.Column.Header.ToString() == "Комментарий" && comments.Count > 0)
            {
                switch (e.Key)
                {
                    case Key.D1:
                        if (cell.Content.GetType() == typeof(TextBox))
                            (cell.Content as TextBox).Text = comments.ElementAt(0);
                        break;
                    case Key.D2:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 1)
                            (cell.Content as TextBox).Text = comments.ElementAt(1);
                        break;
                    case Key.D3:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 2)
                            (cell.Content as TextBox).Text = comments.ElementAt(2);
                        break;
                    case Key.D4:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 3)
                            (cell.Content as TextBox).Text = comments.ElementAt(3);
                        break;
                    case Key.D5:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 4)
                            (cell.Content as TextBox).Text = comments.ElementAt(4);
                        break;
                    case Key.D6:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 5)
                            (cell.Content as TextBox).Text = comments.ElementAt(5);
                        break;
                    case Key.D7:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 6)
                            (cell.Content as TextBox).Text = comments.ElementAt(6);
                        break;
                    case Key.D8:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 7)
                            (cell.Content as TextBox).Text = comments.ElementAt(7);
                        break;
                    case Key.D9:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 8)
                            (cell.Content as TextBox).Text = comments.ElementAt(8);
                        break;
                    case Key.D0:
                        if (cell.Content.GetType() == typeof(TextBox) && comments.Count > 9)
                            (cell.Content as TextBox).Text = comments.ElementAt(9);
                        break;
                }

            }
        }

        private static void GridColumnFastEdit(DataGridCell cell, RoutedEventArgs e)
        {
            if (cell == null || cell.IsEditing || cell.IsReadOnly)
                return;

            DataGrid dataGrid = FindVisualParent<DataGrid>(cell);

            foreach (StockRecord sr_presented in (FindVisualParent<Window>(cell).DataContext as MainWindow).stocks)
            {
                (FindVisualParent<Window>(cell).DataContext as MainWindow).tariffs_for_edit.Remove(sr_presented.Tariff_name);
            }
            if (cell.DataContext.GetType()==typeof(StockRecord) && (cell.DataContext as StockRecord).Tariff_name != null && (cell.DataContext as StockRecord).Tariff_name != "")
                (FindVisualParent<Window>(cell).DataContext as MainWindow).tariffs_for_edit.Add((cell.DataContext as StockRecord).Tariff_name);

            //TODO
            //Динамически изменять состав допустимых элементов в dgComboBoxColumn не получилось
            //Написать на StackOverFlow
            //IEnumerable<string> tariffs_updated = from all_tariffs in dataGrid.DataContext as ObservableCollection<string>
            //                                      where !(from inputed_stocks in dataGrid.ItemsSource as ObservableCollection<StockRecord>
            //                                                  select inputed_stocks.Tariff_name).Contains(all_tariffs)
            //                                      select all_tariffs;
            //dataGrid.ItemsSource
            //(dataGrid.Columns[0] as DataGridComboBoxColumn).ItemsSource = tariffs_updated;

            if (dataGrid == null)
                return;
            
            if (!cell.IsFocused)
            {
                cell.Focus();
            }

            if (cell.Content is CheckBox)
            {
                if (dataGrid.SelectionUnit != DataGridSelectionUnit.FullRow)
                {
                    if (!cell.IsSelected)
                        cell.IsSelected = true;
                }
                else
                {
                    DataGridRow row = FindVisualParent<DataGridRow>(cell);
                    if (row != null && !row.IsSelected)
                    {
                        row.IsSelected = true;
                    }
                }
            }
            else
            {
                try
                {
                    ComboBox cb = cell.Content as ComboBox;
                    if (cb != null)
                    {
                        dataGrid.BeginEdit(e);
                        cell.Dispatcher.Invoke(
                         DispatcherPriority.Background,
                         new Action(delegate { }));
                        cb.IsDropDownOpen = false;
                    }
                }
                catch { }
            }
        }


        private static T FindVisualParent<T>(UIElement element) where T : UIElement
        {
            UIElement parent = element;
            while (parent != null)
            {
                T correctlyTyped = parent as T;
                if (correctlyTyped != null)
                {
                    return correctlyTyped;
                }

                parent = VisualTreeHelper.GetParent(parent) as UIElement;
            }
            return null;
        }

        private SolidColorBrush highlightBrush = new SolidColorBrush(Colors.Orange);
        private SolidColorBrush normalBrush = new SolidColorBrush(Colors.White);
        private void dgStocks_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                StockRecord product = (StockRecord)e.Row.DataContext;
                if (product.Sim_num > 0)
                {
                    e.Row.Background = highlightBrush;
                }
                else
                {
                    // Restore the default white background. This ensures that used,
                    // formatted DataGrid objects are reset to their original appearance.
                    e.Row.Background = normalBrush;
                }
            }
            catch { }
        }

        private void dgUploads_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                UploadRecord sim = (UploadRecord)e.Row.DataContext;
                if (sim.Repeater)
                {
                    e.Row.Background = highlightBrush;
                }
                else
                {
                    e.Row.Background = normalBrush;
                }
            }
            catch { }
        }

        private void datagrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            //Никаких красных рамок в поле Количество
            try
            {
                if ((e.Column.Header.ToString() == "Стоимость" || e.Column.Header.ToString() == "Количество")
                    && e.EditingElement.GetType() == typeof(TextBox)
                    && (e.EditingElement as TextBox).Text.Trim() == "")
                    (e.EditingElement as TextBox).Text = "0";
                else if (e.Column.Header.ToString() == "ICC"
                    && e.EditingElement.GetType() == typeof(TextBox) && e.Row.DataContext.GetType() == typeof(UploadRecord))
                {
                    UploadRecord ICC_current = e.Row.DataContext as UploadRecord;
                    IEnumerable<UploadRecord> ICC_repeaters = (from h in upload_week_data
                                                               where h.ICC_id == (e.EditingElement as TextBox).Text.Trim()
                                                                     && h != e.Row.DataContext
                                                               select h)
                                                               .Union(
                                                              from h in uploads
                                                              where h.ICC_id == (e.EditingElement as TextBox).Text.Trim()
                                                                && h != e.Row.DataContext
                                                                && h.Repeater == false
                                                              select h);
                    UploadRecord ICC_exist = (from h in ICC_repeaters
                                              where !(h.IP_name == ICC_current.IP_name
                                                    && h.SP_code == ICC_current.SP_code
                                                    && h.Report_date.ToString("yyyyMMdd") == ICC_current.Report_date.ToString("yyyyMMdd"))
                                              select h).FirstOrDefault();

                    UploadRecord ICC_repeater = (from h in ICC_repeaters
                                                 where (h.IP_name == ICC_current.IP_name
                                                     && h.SP_code == ICC_current.SP_code
                                                     && h.Report_date.ToString("yyyyMMdd") == ICC_current.Report_date.ToString("yyyyMMdd"))
                                                 select h).FirstOrDefault();
                    if (ICC_repeaters.Count()==0)
                        ICC_current.Repeater = false;
                    if (ICC_repeater != null)
                    {
                        ICC_current.Repeater = true;
                        ICC_current.SIM_price = ICC_repeater.SIM_price;
                        ICC_current.COMMENT_text = ICC_repeater.COMMENT_text;
                        ICC_current.COMM_flag = ICC_repeater.COMM_flag;
                        //e.Row.Background = Brushes.SaddleBrown;
                        //e.Row.Foreground = Brushes.White;
                    }
                    else
                    if (ICC_exist != null)
                    {
                        string rcomment = "Повтор " + ICC_exist.IP_name + " на " + ICC_exist.SP_code + " от " + ICC_exist.Report_date.ToString("yyyy.MM.dd");
                        string add_rcomment = "";
                        if (ICC_exist.Report_date > ICC_current.Report_date)
                        {
                            add_rcomment = "Изменения не будут сохранены.";
                            ICC_current.COMMENT_text = add_rcomment;
                            e.Row.Background = Brushes.Red;
                        }
                        else
                        {
                            ICC_current.COMMENT_text =ICC_current.COMMENT_text+"-" +rcomment;
                            e.Row.Background = Brushes.Purple;
                            e.Row.Foreground = Brushes.White;
                        }

                        MessageBox.Show("ICC " + ICC_exist.ICC_id + " является повтором!" + Environment.NewLine +
                            rcomment + Environment.NewLine + add_rcomment, "Повтор отгрузки.", MessageBoxButton.OK, MessageBoxImage.Warning);
                        
                    }
                }
            }
            catch { }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                UpdateStockFile(stocks);
                UpdateUploadFile(uploads);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\TorgPred\FilterAPS", "", cbFilterIPSPbyWeekday.IsChecked == true ? "1" : "0", RegistryValueKind.String);
            }
            catch { }
        }

        private string GetRuWeekDay(DayOfWeek dw)
        {
            string result="";
            switch (dw)
            {
                case (DayOfWeek.Monday):
                    result="Понедельник";
                    break;
                case (DayOfWeek.Tuesday):
                    result = "Вторник";
                    break;
                case (DayOfWeek.Wednesday):
                    result = "Среда";
                    break;
                case (DayOfWeek.Thursday):
                    result = "Четверг";
                    break;
                case (DayOfWeek.Friday):
                    result = "Пятница";
                    break;
                case (DayOfWeek.Saturday):
                    result = "Суббота";
                    break;
                case (DayOfWeek.Sunday):
                    result = "Воскресенье";
                    break;
                default:
                    result = "Не определено";
                    break;
            }
            return result;
        }

        private void dpReportDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpReportDate.SelectedDate != null)
                lWeekDay.Content = GetRuWeekDay(((DateTime)dpReportDate.SelectedDate).DayOfWeek);

            if (dpReportDate.SelectedDate != null && cbIPs.SelectedItem!=null)
            {
                //IPandSPRecord selectedsp = (cbSPs.SelectedItem as ComboBoxItem).DataContext as IPandSPRecord;

                setter.Report_date = (DateTime)dpReportDate.SelectedDate;
                WaylistDateMode datemode = (from l in setter.WayListSettings.WayListDateModes
                                            where l.Report_date.Date == setter.Report_date.Date
                                            select l).FirstOrDefault();
                if (datemode != null)
                    setter.WayListMode = datemode.Date_WayListMode;
                //LoadStockFile((cbIPs.SelectedItem as ComboBoxItem).Content, selectedsp);
                //LoadUploadFile((cbIPs.SelectedItem as ComboBoxItem).Content, selectedsp);
                HasDataLoader();
                FilterIPSPbyWeekDay();

                if (datemode==null || (setter.WayListMode != WayListModeSet.Pedestrian && !WLPPresented(WayListPointTypes.Start, false)))
                {
                    if (waylistmode_window != null && waylistmode_window.Visibility==Visibility.Hidden)
                    {
                        waylistmode_window.Reload();
                        waylistmode_window.ShowDialog();
                    }
                }

                LoadWaylist();
                WayListLocker();

                miGenReport.Header = "Создать отчет на " + setter.Report_date.ToString("dd.MM.yyyy");
            }
        }

        private void cbFilterIPSPbyWeekday_Click(object sender, RoutedEventArgs e)
        {
            FilterIPSPbyWeekDay();
        }

        private void miChangeTP_Click(object sender, RoutedEventArgs e)
        {
            setter.ShowSetter = true;
            this.Hide();
        }

        private void miChangeAD_Click(object sender, RoutedEventArgs e)
        {
            switch (miChangeAD.Header as string)
            {
                case ("Адрес по точке"):
                    cbDistinctAddresses.Visibility = Visibility.Hidden;
                    cbAddedDescriptions.Visibility = Visibility.Hidden;
                    cbSPaddresses.Visibility = Visibility.Visible;
                    miChangeAD.Header = "Адрес по рынку";
                    break;
                case ("Адрес по рынку"):
                    cbDistinctAddresses.Visibility = Visibility.Visible;
                    cbAddedDescriptions.Visibility = Visibility.Visible;
                    cbSPaddresses.Visibility = Visibility.Hidden;
                    miChangeAD.Header = "Адрес по точке";
                    break;
            }
            
        }

        private void miOpenWrkDr_Click(object sender, RoutedEventArgs e)
        {
            var runExplorer = new System.Diagnostics.ProcessStartInfo();
            runExplorer.FileName = "explorer.exe";
            runExplorer.Arguments = setter.WorkDir;
            System.Diagnostics.Process.Start(runExplorer); 
        }


        private void button2_Click(object sender, RoutedEventArgs e)
        {
            //Пример Binding programmically
            aps_scheme.Tables.Add(aps_table);
            aps_table.Columns.Add("IP_name", typeof(string));
            foreach (IPandSPRecord ipsps_record in ipsps)
            {
                DataRow aps_row = aps_table.NewRow();
                aps_row["IP_name"] = ipsps_record.IP_name;
                aps_table.Rows.Add(aps_row);
            }
            Binding b = new Binding();
            b.Source = aps_table;
            b.Path = new PropertyPath("IP_name");
            tbIP_name.SetBinding(TextBox.TextProperty, b);
        }

        private IPandSPRecord GetIPSPSData(string ip_name, string sp_code)
        {
            ip_name = ip_name == null ? "" : ip_name;
            sp_code = sp_code == null ? "" : sp_code;
            return (from l in ipsps
                    where l.IP_name == ip_name && l.SP_code == sp_code
                    select l).FirstOrDefault();
        }

        private void GetIPSPSData(int ipsps_position)
        {
            try
            {
                if (ipsps_position >= 0)
                {
                    tbIP_name.Text = ipsps.ElementAt(ipsps_position).IP_name;
                    tbSP_code.Text = ipsps.ElementAt(ipsps_position).SP_code;
                    cbCOMM_flag.Text = ipsps.ElementAt(ipsps_position).COMM_flag == true ? "Да" : "Нет";
                    tbSP_type.Text = ipsps.ElementAt(ipsps_position).SP_type;
                    cbSP_region.Text = ipsps.ElementAt(ipsps_position).SP_region;
                    tbSP_town.Text = ipsps.ElementAt(ipsps_position).SP_town;
                    tbSP_town_type.Text = ipsps.ElementAt(ipsps_position).SP_town_type;
                    tbSP_town_area.Text = ipsps.ElementAt(ipsps_position).SP_town_area;
                    cbSP_subway.Text = ipsps.ElementAt(ipsps_position).SP_subway;
                    tbSP_street.Text = ipsps.ElementAt(ipsps_position).SP_street;
                    tbSP_street_type.Text = ipsps.ElementAt(ipsps_position).SP_street_type;
                    tbSP_house.Text = ipsps.ElementAt(ipsps_position).SP_house;
                    tbSP_house_building.Text = ipsps.ElementAt(ipsps_position).SP_house_building;
                    tbSP_added_description.Text = ipsps.ElementAt(ipsps_position).SP_added_description;

                    if (!ipsps.ElementAt(ipsps_position).New_sp)
                    {
                        EnableEditing(false);
                    }
                    else
                    {
                        EnableEditing(true);
                    }
                }
                if (ipsps_position == -1)
                {
                    tbIP_name.Text = "";
                    tbSP_code.Text = "";
                    cbCOMM_flag.Text = "";
                    tbSP_type.Text = "";
                    cbSP_region.Text = "";
                    tbSP_town.Text = "";
                    tbSP_town_type.Text = "";
                    tbSP_town_area.Text = "";
                    cbSP_subway.Text = "";
                    tbSP_street.Text = "";
                    tbSP_street_type.Text = "";
                    tbSP_house.Text = "";
                    tbSP_house_building.Text = "";
                    tbSP_added_description.Text = "";
                }
            }
            catch{}
        }

        private void EnableEditing(bool enable)
        {
            if (!enable)
            {
                tbIP_name.IsReadOnly = true;
                tbSP_code.IsReadOnly = true;
                cbCOMM_flag.IsReadOnly = true;
                tbSP_type.IsReadOnly = true;
                cbSP_region.IsReadOnly = true;
                tbSP_town.IsReadOnly = true;
                tbSP_town_type.IsReadOnly = true;
                tbSP_town_area.IsReadOnly = true;
                cbSP_subway.IsReadOnly = true;
                tbSP_street.IsReadOnly = true;
                tbSP_street_type.IsReadOnly = true;
                tbSP_house.IsReadOnly = true;
                tbSP_house_building.IsReadOnly = true;
                tbSP_added_description.IsReadOnly = true;
            }
            else
            {
                tbIP_name.IsReadOnly = false;
                //tbSP_code.IsReadOnly = false;
                cbCOMM_flag.IsReadOnly = false;
                tbSP_type.IsReadOnly = false;
                cbSP_region.IsReadOnly = false;
                tbSP_town.IsReadOnly = false;
                tbSP_town_type.IsReadOnly = false;
                tbSP_town_area.IsReadOnly = false;
                cbSP_subway.IsReadOnly = false;
                tbSP_street.IsReadOnly = false;
                tbSP_street_type.IsReadOnly = false;
                tbSP_house.IsReadOnly = false;
                tbSP_house_building.IsReadOnly = false;
                tbSP_added_description.IsReadOnly = false;
            }
        }

        private void EnableButtons(int ipsps_position)
        {
            try
            {
                if (ipsps.ElementAt(ipsps_position).New_sp)
                    bSave.IsEnabled = true;
                else
                    bSave.IsEnabled = false;

                if (ipsps_position == ipsps.Count - 1)
                {
                    bLast.IsEnabled = false;
                    bNext.IsEnabled = false;
                }
                else
                    if (ipsps_position >= 0 && ipsps_position < ipsps.Count - 1)
                    {
                        bLast.IsEnabled = true;
                        bNext.IsEnabled = true;
                    }

                if (ipsps_position == 0)
                {
                    bFirst.IsEnabled = false;
                    bPrevious.IsEnabled = false;
                }
                else
                    if (ipsps_position > 0 && ipsps_position <= ipsps.Count - 1)
                    {
                        bFirst.IsEnabled = true;
                        bPrevious.IsEnabled = true;
                    }
                lSPCount.Content = (ipsps_position + 1).ToString() + " из " + ipsps.Count.ToString();
            }
            catch { }
        }

        private void bFirst_Click(object sender, RoutedEventArgs e)
        {
            ipsps_position = 0;
            GetIPSPSData(ipsps_position);
            EnableButtons(ipsps_position);
        }

        private void bNext_Click(object sender, RoutedEventArgs e)
        {
            ipsps_position = ipsps_position + 1;
            if (ipsps_position < ipsps.Count)
                GetIPSPSData(ipsps_position);
            else
                ipsps_position = ipsps.Count - 1;
            EnableButtons(ipsps_position);
        }

        private void bLast_Click(object sender, RoutedEventArgs e)
        {
            ipsps_position = ipsps.Count-1;
            GetIPSPSData(ipsps_position);
            EnableButtons(ipsps_position);
        }

        private void bPrevious_Click(object sender, RoutedEventArgs e)
        {
            ipsps_position = ipsps_position - 1;
            if (ipsps_position >= 0)
                GetIPSPSData(ipsps_position);
            else
                ipsps_position = 0;
            EnableButtons(ipsps_position);
        }

        private void bNewTT_Click(object sender, RoutedEventArgs e)
        {
            if (((string)bNewTT.Content) == "Новая точка")
            {
                bPrevious.IsEnabled = false;
                bNext.IsEnabled = false;
                bFirst.IsEnabled = false;
                bLast.IsEnabled = false;
                bSave.IsEnabled = true;
                ipsps_position = -1;
                GetIPSPSData(ipsps_position);
                bNewTT.Content = "Отменить";
                tbSP_code.Text = "Новая" + (from i in ipsps
                                            where i.New_sp == true
                                            select i).Count();
                EnableEditing(true);
            }
            else
                if (((string)bNewTT.Content) == "Отменить")
                {
                    ipsps_position = ipsps.Count - 1;
                    GetIPSPSData(ipsps_position);
                    bPrevious.IsEnabled = true;
                    bNext.IsEnabled = false;
                    EnableButtons(ipsps_position);
                    CheckForSave();
                    bNewTT.Content = "Новая точка";
                }
        }

        private bool CheckForSave()
        {
            bool result = true;
            if (tbIP_name.Text.Trim() == "")
            {
                lIP_name.Foreground = Brushes.Red;
                result = false;
            }
            else
                lIP_name.Foreground = Brushes.Black;
            if ( tbSP_code.Text.Trim() == "")
            {
                lSP_code.Foreground = Brushes.Red;
                result = false;
            }
            else
                lSP_code.Foreground = Brushes.Black;
            return result;
        }

        private void SaveNewAPS()
        { 
            string filename_newaps = setter.WorkDir + String.Format(@"\NEW_APS.{0}", newaps_file_ext);

                using (FileStream fs = new FileStream(filename_newaps, FileMode.Create))
                {
                    using (StreamWriter w = new StreamWriter(fs, Encoding.Default))
                    {
                        w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}{0}{11}{0}{12}{0}{13}{0}{14}{0}{15}{0}{16}",
                                    newaps_file_delimiter,
                                    "Регион",
                                    "Населенный пункт",
                                    "Тип населенного пункта",
                                    "Округ/ Направление/ Район",
                                    "Станция метро",
                                    "Адрес пункта продаж",
                                    "Тип улицы",
                                    "Номер дома",
                                    "Номер строения",
                                    "Дополнительное описание месторасположения точки",
                                    "Код точки",
                                    "Тип точки",
                                    "Субдилер",
                                    "ТП",
                                    "День недели",
                                    "Признак комиссии"
                                    ));
                        foreach (IPandSPRecord newaps_record in ipsps)
                        {
                            if (newaps_record.New_sp)
                            {
                                w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}{0}{11}{0}{12}{0}{13}{0}{14}{0}{15}{0}{16}",
                                    newaps_file_delimiter,
                                    setter.String2CSVField(newaps_record.SP_region),
                                    setter.String2CSVField(newaps_record.SP_town),
                                    setter.String2CSVField(newaps_record.SP_town_type),
                                    setter.String2CSVField(newaps_record.SP_town_area),
                                    setter.String2CSVField(newaps_record.SP_subway),
                                    setter.String2CSVField(newaps_record.SP_street),
                                    setter.String2CSVField(newaps_record.SP_street_type),
                                    setter.String2CSVField(newaps_record.SP_house),
                                    setter.String2CSVField(newaps_record.SP_house_building),
                                    setter.String2CSVField(newaps_record.SP_added_description),
                                    setter.String2CSVField(newaps_record.SP_code),
                                    setter.String2CSVField(newaps_record.SP_type),
                                    setter.String2CSVField(newaps_record.IP_name),
                                    setter.String2CSVField(setter.LastUser),
                                    newaps_record.Week_day,
                                    newaps_record.COMM_flag == true ? 1 : 0
                                    ));
                            }
                        }
                    }
                }
        }


        private void FocusOnIPSP(IPandSPRecord sp_to_select)
        {
            try
            {
                cbIPs.SelectionChanged -= new SelectionChangedEventHandler(cbIPs_SelectionChanged);
                cbSPaddresses.SelectionChanged -= new SelectionChangedEventHandler(cbSPaddresses_SelectionChanged);
                cbIPs.SelectedItem = cbIPs.Items.Cast<ComboBoxItem>().First(o => o.Content as string == sp_to_select.IP_name); 

                ObservableCollection<IPandSPRecord> distinctSPs = new ObservableCollection<IPandSPRecord>(
                        (from sp in ipsps
                         where sp.IP_name.Trim() == ((cbIPs.SelectedValue as ComboBoxItem).Content as string).Trim()
                         && ((bool)cbFilterIPSPbyWeekday.IsChecked &&
                         (sp.Week_day.ToLower().Trim() == "ежедневно" || sp.Week_day.ToUpper() == GetRuWeekDay(((DateTime)dpReportDate.SelectedDate).DayOfWeek).ToUpper()) ||
                         !(bool)cbFilterIPSPbyWeekday.IsChecked)
                         select sp)
                         .GroupBy(p => p.SP_code)
                         .Select(g => g.First())
                        );
                cbSPs.SelectionChanged -= new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                cbSPs.ItemsSource = distinctSPs.Select(p => new ComboBoxItem() { Content = p.SP_code, DataContext = p });
                cbSPs.SelectionChanged += new SelectionChangedEventHandler(cbSPs_SelectionChanged);
                cbSPs.SelectedItem = cbSPs.Items.Cast<ComboBoxItem>().First(o => (o.DataContext as IPandSPRecord).SP_code == sp_to_select.SP_code); 
                cbSPaddresses.ItemsSource = distinctSPs;
                cbSPaddresses.SelectedItem = sp_to_select;
                cbSPaddresses.SelectionChanged += new SelectionChangedEventHandler(cbSPaddresses_SelectionChanged);
                cbIPs.SelectionChanged += new SelectionChangedEventHandler(cbIPs_SelectionChanged);
            }
            catch { }
        }

        private void bSave_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Сохранить данные по новой точке?", "Запрос на сохранение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                //Редактирование новой точки
                if (CheckForSave() && ipsps_position != -1)
                {
                    try
                    {
                        ipsps.ElementAt(ipsps_position).IP_name = tbIP_name.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_code = tbSP_code.Text.Trim();
                        ipsps.ElementAt(ipsps_position).COMM_flag = cbCOMM_flag.Text.Trim().ToUpper() == "Да".ToUpper() || cbCOMM_flag.Text.Trim() == "1" ? true : false;
                        ipsps.ElementAt(ipsps_position).SP_type = tbSP_type.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_region = cbSP_region.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_town = tbSP_town.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_town_type = tbSP_town_type.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_town_area = tbSP_town_area.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_subway = cbSP_subway.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_street = tbSP_street.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_street_type = tbSP_street_type.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_house = tbSP_house.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_house_building = tbSP_house_building.Text.Trim();
                        ipsps.ElementAt(ipsps_position).SP_added_description = tbSP_added_description.Text.Trim();
                        ipsps.ElementAt(ipsps_position).Week_day = "ежедневно";

                        SaveNewAPS();
                        //FilterIPSPbyWeekDay();
                        FocusOnIPSP(ipsps.ElementAt(ipsps_position));
                        //ipsps_position = ipsps.Count - 1;
                        //bNewTT.Content = "Новая точка";
                        
                    }
                    catch { }
                }
                //Сохранение новой точки
                if (CheckForSave() && ipsps_position == -1)
                {
                    try
                    {
                        bPrevious.IsEnabled = true;
                        bNext.IsEnabled = true;
                        bFirst.IsEnabled = true;
                        bLast.IsEnabled = true;

                        IPandSPRecord new_TT = new IPandSPRecord()
                        {
                            New_sp = true,
                            IP_name = tbIP_name.Text.Trim(),
                            SP_code = tbSP_code.Text.Trim(),
                            COMM_flag = cbCOMM_flag.Text.Trim().ToUpper() == "Да".ToUpper() || cbCOMM_flag.Text.Trim().ToUpper() == "1" ? true : false,
                            SP_type = tbSP_type.Text.Trim(),
                            SP_region = cbSP_region.Text.Trim(),
                            SP_town = tbSP_town.Text.Trim(),
                            SP_town_type = tbSP_town_type.Text.Trim(),
                            SP_town_area = tbSP_town_area.Text.Trim(),
                            SP_subway = cbSP_subway.Text.Trim(),
                            Week_day = "ежедневно",
                            SP_street = tbSP_street.Text.Trim(),
                            SP_street_type = tbSP_street_type.Text.Trim(),
                            SP_house = tbSP_house.Text.Trim(),
                            SP_house_building = tbSP_house_building.Text.Trim(),
                            SP_added_description = tbSP_added_description.Text.Trim(),
                            TP_name = setter.LastUser
                        };
                        ipsps.Add(new_TT);
                        SaveNewAPS();
                        FilterIPSPbyWeekDay();
                        ipsps_position = ipsps.Count - 1;
                        bPrevious.IsEnabled = true;
                        bNext.IsEnabled = false;
                        EnableButtons(ipsps_position);
                        bNewTT.Content = "Новая точка";
                        FocusOnIPSP(new_TT);
                    }
                    catch { }
                }
            }

        }

        private void bCancel_Click(object sender, RoutedEventArgs e)
        {
            ipsps_position = ipsps.Count - 1;
            GetIPSPSData(ipsps_position);
            bPrevious.IsEnabled = true;
            bNext.IsEnabled = false;
            EnableButtons(ipsps_position);
            CheckForSave();
        }

        private void dgtcComment_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Right && comments.Count > 0)
                {
                    dgUploads.UnselectAllCells();
                    DataGridCell cell = sender as DataGridCell;
                    cell.IsSelected = true;
                    ContextMenu menu = new ContextMenu();
                    for (int i = 1; i <= comments.Count; i++)
                    {
                        MenuItem comment = new MenuItem() { Header = String.Format("{0}. {1}", i, comments.ElementAt(i - 1)) };
                        comment.Click += new RoutedEventHandler(comment_Click);
                        menu.Items.Add(comment);
                    }
                    menu.IsOpen = true;
                }
                else
                    if (comments.Count == 0)
                        MessageBox.Show("Файла комментариев нет. Создайте файл Comments.csv в рабочей папке", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { }
        }

        private void comment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender != null && sender.GetType() == typeof(MenuItem))
                {
                    string comment = (sender as MenuItem).Header.ToString();
                    comment = comment.Substring(comment.IndexOf(".") + 1).Trim();
                    (dgUploads.SelectedCells[0].Item as TorgPred.UploadRecord).COMMENT_text = comment;
                }
            }
            catch { }
        }

        private void dgtcPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void dgtcICC_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Trim().Replace("\b", "") == "" || e.Text.Trim().Replace("\b", "") == ";";
        }

        private static bool IsTextAllowed(string text)
        {
            //Дробные цифры
            //Regex regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            Regex regex = new Regex("[^0-9-]+"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }

        private FileInfo CreateStockReport(string filename, string report_name)
        {
            FileInfo filename_info;
            FileInfo result_file_info = null;
            if (File.Exists(filename) && (new FileInfo(report_name)).Directory.Exists)
            {
                filename_info = new FileInfo(filename);
                string line;
                ObservableCollection<StockRecord> result = new ObservableCollection<StockRecord>();
                using (TextReader tr = new StreamReader(filename, Encoding.Default))
                {
                    while ((line = tr.ReadLine()) != null)
                    {
                        string[] file_record = setter.CSVrow2StringArray(line); 
                        try
                        {
                            if (Convert.ToInt64(DateTime.ParseExact(file_record.ElementAt(4), date_formats, CultureInfo.InvariantCulture, DateTimeStyles.None).ToString("yyyyMMdd")) <= Convert.ToInt64(setter.Report_date.ToString("yyyyMMdd")))
                                result.Add(new StockRecord()
                                {
                                    SP_code = file_record.ElementAt(0),
                                    IP_name = file_record.ElementAt(1),
                                    Tariff_name = file_record.ElementAt(2),
                                    Sim_num = Convert.ToInt16(file_record.ElementAt(3)),
                                    Report_date = DateTime.ParseExact(file_record.ElementAt(4), date_formats, CultureInfo.InvariantCulture, DateTimeStyles.None),
                                    TP_name = file_record.ElementAt(5),
                                    SP_type = file_record.Count() == 7 ? file_record.ElementAt(6) : "",
                                    COMMENT_text = file_record.Count() == 8 ? file_record.ElementAt(7) : ""
                                });
                        }
                        catch { }
                    }
                }

                ObservableCollection<StockRecord> result_max = new ObservableCollection<StockRecord>(
                    from l in result
                    group l by new { l.IP_name, l.SP_code } into g
                    select g.OrderByDescending(t => t.Report_date).First()
                    );
                result = new ObservableCollection<StockRecord>(
                    from r in result 
                    from rm in result_max
                    where r.IP_name==rm.IP_name && r.SP_code==rm.SP_code && r.Report_date.Date==rm.Report_date.Date
                    select r
                    );
                if (result.Count() > 0)
                {
                    string result_filename = report_name;
                    using (FileStream fs = new FileStream(result_filename, FileMode.Create))
                    {
                        using (StreamWriter w = new StreamWriter(fs, Encoding.Default))
                        {
                            w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}",
                                    stock_file_delimiter,
                                    "Торговая точка",
                                    "Субдилер",
                                    "Тарифный план",
                                    "Количество SIM",
                                    "ДатаВремя",
                                    "Торговый представитель",
                                    "Тип ТТ",
                                    "Комментарий",
                                    "Доп.описание"));
                            foreach (StockRecord stock_record in result)
                            {
                                w.WriteLine(String.Format("{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}",
                                    stock_file_delimiter,
                                    stock_record.SP_code,
                                    stock_record.IP_name,
                                    stock_record.Tariff_name,
                                    stock_record.Sim_num,
                                    stock_record.Report_date.ToString("dd.MM.yyyy HH:mm"),
                                    stock_record.TP_name,
                                    stock_record.SP_type,
                                    setter.String2CSVField(stock_record.COMMENT_text),
                                    GetIPSPSData(stock_record.IP_name, stock_record.SP_code) !=null ? setter.String2CSVField(GetIPSPSData(stock_record.IP_name, stock_record.SP_code).SP_added_description) : ""));
                            }
                        }
                    }
                    if (File.Exists(result_filename))
                        result_file_info = new FileInfo(result_filename);
                }
            }
            return result_file_info;
        }
        
        //Восстановление шаблона путевого листа
        private bool RestoreResource(string resource, string destination)
        {
        	try
        	{
	            string[] ir = Assembly.GetExecutingAssembly().GetManifestResourceNames();
	            Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
	    		FileStream resourceFile = new FileStream(destination, FileMode.Create);
	    		byte[] b = new byte[s.Length + 1];
				s.Read(b, 0, Convert.ToInt32(s.Length));
				resourceFile.Write(b, 0, Convert.ToInt32(b.Length - 1));
				resourceFile.Flush();
				resourceFile.Close();
				return true;
        	}
        	catch
        	{
        		return false;
        	}
        }

        //TODO Format cell
        //private void FormatCell(object cell, string fontname, int fontsize, bool fontbold)
        //{
        //    if (typeof(System.__ComObject) == cell.GetType())
        //    {
        //        //cell.Font.Name = "Calibri";
        //        //cell.Font.Size = 14;
        //        //cell.Font.Bold = true;
        //    }
        //}

        private void GenerateRecMoney(string filename_recmoney)
        {
            try
            {
                ObservableCollection<UploadRecord> guploads = GetUploadData();
                var query = guploads
                    .Where(c => (c.Report_date.Date <= setter.Report_date && c.SIM_price > 0))
                    .GroupBy(c => new { c.IP_name, c.Report_date.Date })
                    .Select(g => new
                    {
                        CustId = g.Key,
                        Comm = g.Where(c => c.COMM_flag == true).Sum(c => c.SIM_price),
                        WComm = g.Where(c => c.COMM_flag == false).Sum(c => c.SIM_price)
                    });
                decimal CommSum = (from q in query
                                   select q.Comm).Sum();
                decimal WCommSum = (from q in query
                                   select q.WComm).Sum();


                if (File.Exists("RecMoney.xlsx") == false)
                    RestoreResource("TorgPred.Resources.RecMoney.xlsx", filename_recmoney);
                else
                    File.Copy("RecMoney.xlsx", filename_recmoney, true);

                Excel.Workbook workbook;
                Excel.Sheets worksheets;
                Excel._Worksheet worksheet;
                //Excel.Range range;
                //Если Excel не запущен запускаем
                if (setter.Excel == null)
                    setter.Excel = new Microsoft.Office.Interop.Excel.Application();
                workbook = setter.Excel.Workbooks.Open(filename_recmoney, 0, false, 5, "", "", true, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, false, false);
                //(filename_waylist, AddToMru: false);
                //(filename_waylist, 0, false, 5, "", "", true, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, false, false);
                worksheets = workbook.Worksheets;
                worksheet = (Microsoft.Office.Interop.Excel.Worksheet)worksheets.get_Item(1);

                //Заполняем
                int recnum = 0;
                foreach (var item in query)
                {
                    worksheet.Cells[recnum + 4, 1] = recnum + 1;
                    worksheet.Cells[recnum + 4, 2] = item.CustId.IP_name;
                    worksheet.Cells[recnum + 4, 3] = item.CustId.Date;
                    worksheet.Cells[recnum + 4, 4] = item.Comm;
                    worksheet.Cells[recnum + 4, 5] = item.WComm;
                    recnum++;
                }

                //Merge ячеек
                string mergerange1 = String.Format("A{0}:B{1}", recnum + 4, recnum + 4);
                worksheet.Range[mergerange1].Merge();//Всего
                string mergerange2 = String.Format("A{0}:B{1}", recnum + 5, recnum + 5);
                worksheet.Range[mergerange2].Merge();//Итого к сдаче
                string mergerange3 = String.Format("D{0}:E{1}", recnum + 5, recnum + 5);
                worksheet.Range[mergerange3].Merge();//Comm+WComm
                string mergerange4 = String.Format("A{0}:B{1}", recnum + 8, recnum + 8);
                worksheet.Range[mergerange4].Merge();//Сдал
                string mergerange5 = String.Format("A{0}:B{1}", recnum + 10, recnum + 10);
                worksheet.Range[mergerange5].Merge();//Принял
               

                //Итог
                worksheet.Cells[recnum + 4, 1] = "Всего:";
                worksheet.Cells[recnum + 4, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
                worksheet.Cells[recnum + 4, 4] = CommSum;
                worksheet.Cells[recnum + 4, 5] = WCommSum;
                worksheet.Cells[recnum + 5, 1] = "Итого к сдаче:";
                worksheet.Cells[recnum + 5, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
                worksheet.Cells[recnum + 5, 3] = setter.Report_date.Date;
                worksheet.Cells[recnum + 5, 4] = CommSum + WCommSum;
                worksheet.Cells[recnum + 5, 4].HorizontalAlignment = Excel.XlHAlign.xlHAlignRight;
                worksheet.Cells[recnum + 8, 1] = "Сдал:";
                worksheet.Cells[recnum + 8, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                worksheet.Cells[recnum + 8, 4] = "Подпись";
                worksheet.Cells[recnum + 10, 1] = "Принял:";
                worksheet.Cells[recnum + 10, 1].HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;
                worksheet.Cells[recnum + 10, 4] = "Подпись";

                //Форматирование
                string datarange = String.Format("A{0}:E{1}", 4, recnum+4);
                worksheet.Range[datarange].Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                string itogorange=String.Format("A{0}:E{1}", recnum + 4, recnum + 10);
                worksheet.Range[itogorange].Font.Name = "Calibri";
                worksheet.Range[itogorange].Font.Size = 14;
                worksheet.Range[itogorange].Font.Bold = true;
                string itogorange1 = String.Format("A{0}:E{1}", recnum + 4, recnum + 5);
                worksheet.Range[itogorange1].Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                worksheet.Cells[recnum + 5, 3].Font.Size = 12;
                worksheet.Cells[recnum + 8, 3].Borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;
                worksheet.Cells[recnum + 10, 3].Borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;
                worksheet.Cells[recnum + 8, 5].Borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;
                worksheet.Cells[recnum + 10, 5].Borders[Excel.XlBordersIndex.xlEdgeBottom].LineStyle = Excel.XlLineStyle.xlContinuous;

                //Сохраняем
                workbook.Save();
                workbook.Close();
                workbook = null;
                //TODO Excel не закрывается
                setter.Excel.Quit();
            }
            catch (Exception create_recmoney_error) { MessageBox.Show("Неизвестная ошибка" + Environment.NewLine + create_recmoney_error.Message, "Ошибка " + "в процессе создания отчета сданных средств", MessageBoxButton.OK, MessageBoxImage.Stop); }
        }

        private void GenerateWayList(string filename_waylist)
        { 
            try
            {
            	if(File.Exists("WayListTemplate3.xlsx")==false)
            		RestoreResource("TorgPred.Resources.WayListTemplate3.xlsx", filename_waylist);
            	else
            		File.Copy("WayListTemplate3.xlsx", filename_waylist, true);
            		
			    //Создание отчета путевого листа
			    if(File.Exists(filename_waylist))
			    {
	                Excel.Workbook workbook;
	                Excel.Sheets worksheets;
	                Excel._Worksheet worksheet;
	                //Excel.Range range;
	                //Если Excel не запущен запускаем
	                if (setter.Excel == null)
	                    setter.Excel = new Microsoft.Office.Interop.Excel.Application();
	                workbook = setter.Excel.Workbooks.Open(filename_waylist, 0, false, 5, "", "", true, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, false, false);
	                	//(filename_waylist, AddToMru: false);
	                    //(filename_waylist, 0, false, 5, "", "", true, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "\t", false, false, 0, true, false, false);
	                worksheets = workbook.Worksheets;
	                worksheet = (Microsoft.Office.Interop.Excel.Worksheet)worksheets.get_Item(1);
	                //Заполняем первый лист
	                worksheet.Cells[5, 4] = setter.Report_date.ToLongDateString();
	                worksheet.Cells[7, 2] = setter.WayListSettings.Company;
	                worksheet.Cells[7, 9] = setter.WayListSettings.Okud;
	                worksheet.Cells[8, 9] = setter.WayListSettings.Okpo;
	                worksheet.Cells[9, 3] = setter.WayListSettings.Autobrand;
	                worksheet.Cells[11, 4] = setter.WayListSettings.AutoN;
	                worksheet.Cells[12, 2] = setter.WayListSettings.FIO;
	                worksheet.Cells[14, 3] = setter.WayListSettings.PravaN;
	                worksheet.Cells[14, 6] = setter.WayListSettings.PravaClass;
	                worksheet.Cells[16, 3] = setter.WayListSettings.LicenseType;
	                worksheet.Cells[19, 3] = setter.WayListSettings.RegN;
	                worksheet.Cells[19, 5] = setter.WayListSettings.RegSeria;
	                worksheet.Cells[19, 7] = setter.WayListSettings.RegN2;
	                worksheet.Cells[29, 3] = (from l in waylistpoints
	                                             where l.Report_date.Date==setter.Report_date.Date
	                                             && l.Point_type==WayListPointTypes.Start
	                                             select l.Point_address).FirstOrDefault();
	                worksheet.Cells[24, 3] = setter.WayListSettings.Purpose;
	                worksheet.Cells[31, 9] = setter.LastUser;
	                worksheet.Cells[34, 5] = setter.WayListSettings.FIOSign;
	                worksheet.Cells[36, 5] = setter.WayListSettings.FIOSign;
                    worksheet.Cells[42, 5] = setter.LastUser;
	                worksheet.Cells[46, 5] = setter.WayListSettings.FIOSign;
	                //TODO: Так не работает!
	                //Excel.Range rng = worksheet.Range["I43"];
	                //rng.Formula = String.Format(@"=ROUND(R45*{0}/100;1)", setter.WayListSettings.GazLimit).Replace(",", ".");
	                //и так не работает
	                //worksheet.Cells[43, 9].Formula = String.Format("=(ROUND(R45*{0}/100;1))", setter.WayListSettings.GazLimit).Replace(",",".");
	                //Так работает
	                //rng.Formula = "=Sum(2,2)";
	                //Так работает
	                worksheet.Cells[43, 9] = String.Format(@"=R45*{0}/100", setter.WayListSettings.GazLimit).Replace(",", ".");
	                //Заполняем второй лист
	                int start_row = 6;
	                int start_column = 12;
	                for (int i = 0; i <= waylistpoints.Count-2; i++)
	                {
	                    if (waylistpoints.ElementAt(i).Point_type == WayListPointTypes.Start)
	                    {
	                        worksheet.Cells[41, 9] = waylistpoints.ElementAt(i).Gaznumber_onpoint;
	                        worksheet.Cells[24, 9] = waylistpoints.ElementAt(i).Speedmeter;
	                    }
	                    if (waylistpoints.ElementAt(i + 1).Point_type == WayListPointTypes.Finish)
	                    {
	                        worksheet.Cells[39, 9] = waylistpoints.ElementAt(i + 1).Gaznumber_buyed;
	                        worksheet.Cells[42, 9] = waylistpoints.ElementAt(i + 1).Gaznumber_onpoint;
	                        worksheet.Cells[45, 6] = waylistpoints.ElementAt(i+1).Speedmeter;
	                    }
	                    worksheet.Cells[start_row, 10] = i + 1;
	                    worksheet.Cells[start_row, start_column] = waylistpoints.ElementAt(i).Point_address;
	                    worksheet.Cells[start_row, start_column + 1] = waylistpoints.ElementAt(i + 1).Point_address;
	                    worksheet.Cells[start_row, start_column + 2] = waylistpoints.ElementAt(i).Point_leave.Hour + ":" + waylistpoints.ElementAt(i).Point_leave.Minute;
	                    worksheet.Cells[start_row, start_column + 3] = waylistpoints.ElementAt(i + 1).Point_enter.Hour + ":" + waylistpoints.ElementAt(i + 1).Point_enter.Minute;
	                    worksheet.Cells[start_row, start_column + 4] = waylistpoints.ElementAt(i + 1).Speedmeter - waylistpoints.ElementAt(i).Speedmeter;
	                    start_row = start_row + 2;
	                 }
	
	                workbook.Save();
	                workbook.Close();
	                workbook = null;
	                //TODO Excel не закрывается
	                setter.Excel.Quit();
			    }
			    else
			    {
			    	System.Windows.MessageBox.Show("Шаблон путевого листа не найден. Восстановить не удалось", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
			    }
            }
            catch (Exception create_waylist_error) { MessageBox.Show("Неизвестная ошибка" + Environment.NewLine + create_waylist_error.Message, "Ошибка " + "в процессе создания путевого листа", MessageBoxButton.OK, MessageBoxImage.Stop); }
        }

        private bool ConvertCSV2Excel(string source_file, string destination_file)
        {
            if (!File.Exists(source_file))
                return false;
            //Если Excel не запущен запускаем
            if (setter.Excel == null)
                setter.Excel = new Microsoft.Office.Interop.Excel.Application();
            Excel.Workbook workbook;
            Excel.Worksheet worksheet;
            Excel.Range range;
            workbook = setter.Excel.Workbooks.Add(1);
            worksheet = (Excel.Worksheet)workbook.Sheets[1];
            range = worksheet.get_Range("$A$1");
            ImportCSV(source_file, worksheet,
                            range, new int[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, true);
            //TODO Исправить расскоментировать
            workbook.SaveAs(destination_file,
                                Excel.XlFileFormat.xlWorkbookDefault, null, null,
                                null, null, Excel.XlSaveAsAccessMode.xlNoChange,
                                null, null, null, null, null);
            workbook.Close();
            setter.Excel.Quit();
            return true;
        }

        private void GenReport_DoWork(object sender, DoWorkEventArgs e)
        {
            //Определяемся куда будем сохранять
            string report_folder = String.Format(setter.WorkDir + @"\Отчет_{0}", setter.Report_date.ToString("dd.MM.yyyy"));
            string report_zip_file = String.Format(setter.WorkDir + @"\{0}_отчет_{1}.zip", setter.LastUser, setter.Report_date.ToString("dd.MM.yyyy"));
            //Определяемся куда будем архивировать
            string report_week = MyCalendar.Calendar.GetWeekOfYear(setter.Report_date, MyCalendar.CalendarWeekRule, MyCalendar.FirstDayOfWeek).ToString();
            //Пара отчетов по остаткам
            string filename_stockdetail = GetDataFilename(setter.WorkDir, report_week, stock_file_mask, stock_file_ext);
            string filename_stockdetail_report = GetDataFilename(report_folder, report_week, stock_file_mask, "xlsx");
            string filename_stock_temp = GetDataFilename(System.IO.Path.GetTempPath(), report_week, shortstock_file_mask, stock_file_ext);
            string filename_stock_report = GetDataFilename(report_folder, report_week, shortstock_file_mask, "xlsx");
            //Отчет по отгрузкам
            string filename_uploaddetail = GetDataFilename(setter.WorkDir, report_week, upload_file_mask, upload_file_ext);
            string filename_uploaddetail_report = GetDataFilename(report_folder, report_week, upload_file_mask, "xlsx");
            //Путевой лист
            string filename_waylist = GetDataFilename(report_folder, setter.Report_date.ToString("dd.MM.yyyy"), "путевой_лист", "xlsx");
            string filename_newaps = GetDataFilename(report_folder, report_week, newaps_file_mask, "xlsx");
            //Отчет по сданным денежным средствам
            string filename_recmoney = GetDataFilename(report_folder, setter.Report_date.ToString("dd.MM.yyyy"), "Денежные_средства", "xlsx");

            string p = Environment.NewLine;

            //Создаем если надо директорию для отчетов
            try { Directory.Delete(report_folder, true); }
            catch { }
            Directory.CreateDirectory(report_folder);

            //Удаляем если можем архив с отчетами
            if (File.Exists(report_zip_file))
                File.Delete(report_zip_file);

            //Если есть директория для отчетов начинаем создавать отчеты
            if (Directory.Exists(report_folder))
            {
                //Путевой лист
                try
                {
                    if (setter.WayListMode != WayListModeSet.Pedestrian)
                    {
                        GenerateWayList(filename_waylist);
                        (sender as BackgroundWorker).ReportProgress(30, "Создание путевого листа...");
                    }
                }
                catch
                {
                    MessageBox.Show(@"Ошибка создания путевого листа." +
                        p + filename_waylist, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
                //Отчет по новым точкам
                try
                {
                    if (File.Exists(setter.WorkDir + @"\NEw_APS.csv"))
                    {
                        ConvertCSV2Excel(setter.WorkDir + @"\NEw_APS.csv", filename_newaps);
                        (sender as BackgroundWorker).ReportProgress(40, "Новые точки...");
                    }
                }
                catch
                {
                    MessageBox.Show(@"Ошибка создания отчета новых ТТ." +
                        p + filename_newaps, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
                //Детальный по остаткам
                try
                {
                    ConvertCSV2Excel(filename_stockdetail, filename_stockdetail_report);
                    (sender as BackgroundWorker).ReportProgress(30, "Остатки детально...");
                }
                catch
                {
                    MessageBox.Show(@"Ошибка создания отчета по детальным остаткам." +
                        p + filename_stockdetail + p + filename_stockdetail_report, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
                //Последние остатки
                //FileInfo stock_info = null;
                //try
                //{
                //    stock_info = CreateStockReport(filename_stockdetail, filename_stock_temp);
                //    if (stock_info != null)
                //    {
                //        ConvertCSV2Excel(stock_info.FullName, filename_stock_report);
                //        (sender as BackgroundWorker).ReportProgress(30, "Остатки на конец недели...");
                //    }
                //    else
                //        MessageBox.Show(@"Похоже, что остатки не снимались" +
                //            p + p + filename_stockdetail + p + filename_stock_temp + p + filename_stock_report, "", MessageBoxButton.OK, MessageBoxImage.Information);
                //}
                //catch
                //{
                //    MessageBox.Show(@"Ошибка создания отчета по остаткам на конец недели." +
                //    p + filename_stockdetail + p + filename_stock_temp + p + filename_stock_report, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                //}
                //Детальный по отгрузкам
                try
                {
                    ConvertCSV2Excel(filename_uploaddetail, filename_uploaddetail_report);
                    (sender as BackgroundWorker).ReportProgress(60, "Отгрузки детально...");
                }
                catch
                {
                    MessageBox.Show(@"Ошибка создания отчета по отгрузкам." +
                        p + filename_uploaddetail + p + filename_uploaddetail_report, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
                //Денежные средства
                try
                {
                    GenerateRecMoney(filename_recmoney);
                    (sender as BackgroundWorker).ReportProgress(80, "Создание отчета сданных средств...");
                }
                catch
                {
                    MessageBox.Show(@"Ошибка создания отчета сданных денежных средств." +
                        p + filename_recmoney, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Stop);
                }

                //Упаковка отчетов
                List<string> files2zip = new List<string>();
                //Детальный отчет по отчетную дату Остатки
                if (File.Exists(filename_stockdetail_report))
                    files2zip.Add(filename_stockdetail_report);
                //Отчет на отчетную дату Остатки
                //if (stock_info != null && File.Exists(filename_stock_report))
                //{
                //    stock_info.Delete();
                //    files2zip.Add(filename_stock_report);
                //}
                //Детальный отчет по отчетную дату Отгрузки
                if (File.Exists(filename_uploaddetail_report))
                    files2zip.Add(filename_uploaddetail_report);
                //Новые ТТ
                if (File.Exists(filename_newaps))
                    files2zip.Add(filename_newaps);
                //Путевой лист
                if (File.Exists(filename_waylist))
                    files2zip.Add(filename_waylist);
                //Сданные средства
                if (File.Exists(filename_recmoney))
                    files2zip.Add(filename_recmoney);

                if (File.Exists("ICSharpCode.SharpZipLib.dll") == false)
                    RestoreResource("TorgPred.Resources.ICSharpCode.SharpZipLib.dll", "ICSharpCode.SharpZipLib.dll");

                if (File.Exists("ICSharpCode.SharpZipLib.dll") && files2zip.Count > 0)
                {
                    WriteZipFile(files2zip, report_zip_file, 5);
                    (sender as BackgroundWorker).ReportProgress(100, "Упаковка отчетов...");
                }
                else
                {
                    if (files2zip.Count == 0)
                        MessageBox.Show("Отчеты не созданы. Проверьте наличие отчетов!", "Ошибка создания отчета!", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Не найден архиватор. Восстановление не удалось.", "Ошибка создания отчета!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private void GenReport_ReportProgress(object sender, ProgressChangedEventArgs e)
        {
            tblGenReportStatus.Text = tblGenReportStatus.Text + Environment.NewLine + e.UserState as string;
            tblGenReportStatus.Height = tblGenReportStatus.Height + 30;
            bGenReportStatus.Height = bGenReportStatus.Height + 30;
        }

        private void GenReport_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                string report_zip_file = String.Format(setter.WorkDir + @"\{0}_отчет_{1}.zip", setter.LastUser, setter.Report_date.ToString("dd.MM.yyyy"));
                gGenReport.Visibility = Visibility.Hidden;
                if (File.Exists(report_zip_file) && MessageBox.Show("Открыть рабочую папку?", "Проверьте отчет", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var runExplorer = new System.Diagnostics.ProcessStartInfo();
                    runExplorer.FileName = "explorer.exe";
                    runExplorer.Arguments = setter.WorkDir;
                    System.Diagnostics.Process.Start(runExplorer);
                }
            }
            catch { gGenReport.Visibility = Visibility.Hidden; }
        }

        private void miGenReport_Click(object sender, RoutedEventArgs e)
        {
            if (FailWLPExist() == true || (setter.WayListMode != WayListModeSet.Pedestrian && (!WLPPresented(WayListPointTypes.Common,true) || !WLPPresented(WayListPointTypes.Start,true))))
            {
                MessageBox.Show("Есть незаполненные данные по путевому листу!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            else
                if (setter.WayListMode != WayListModeSet.Pedestrian && !WLPPresented(WayListPointTypes.Finish,true))
                {
                    finishpoint_window.Reload();
                    finishpoint_window.ShowDialog();
                    if (!finishpoint_window.Cancel)
                    {
                        LoadWaylist();
                        WayListLocker();
                        miGenReport_Click(this, new RoutedEventArgs());
                    }
                }
                else
                {
                    try
                    {
                        if (MessageBox.Show("Сформировать отчет?", "", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            UpdateStockFile(stocks);
                            UpdateUploadFile(uploads);

                            tblGenReportStatus.Text = "Идет построение отчета...";
                            tblGenReportStatus.Height = 30;
                            bGenReportStatus.Height = 70;

                            gGenReport.Visibility = Visibility.Visible;

                            BackgroundWorker genreportworker = new BackgroundWorker();
                            genreportworker.WorkerReportsProgress = true;
                            genreportworker.DoWork += new DoWorkEventHandler(GenReport_DoWork);
                            genreportworker.ProgressChanged += new ProgressChangedEventHandler(GenReport_ReportProgress);
                            genreportworker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(GenReport_Completed);
                            genreportworker.RunWorkerAsync();
                        }
                    }
                    catch (Exception genreport)
                    {
                        gGenReport.Visibility = Visibility.Hidden;
                        MessageBox.Show("Неизвестная ошибка" + Environment.NewLine + genreport.Message, "Ошибка " + "GenReport", MessageBoxButton.OK, MessageBoxImage.Stop);
                    }
                }
        }


        private static void WriteZipFile(List<string> filesToZip, string path, int compression)
        {
            if (compression < 0 || compression > 9)
                throw new ArgumentException("Invalid compression rate.");

            if (!Directory.Exists(new FileInfo(path).Directory.ToString()))
                throw new ArgumentException("The Path does not exist.");

            foreach (string c in filesToZip)
                if (!File.Exists(c))
                    throw new ArgumentException(string.Format("The File{0}does not exist!", c));


            Crc32 crc32 = new Crc32();
            ZipOutputStream stream = new ZipOutputStream(File.Create(path));
            stream.SetLevel(compression);

            for (int i = 0; i < filesToZip.Count; i++)
            {
                ZipEntry entry = new ZipEntry(System.IO.Path.GetFileName(filesToZip[i]));
                entry.DateTime = DateTime.Now;

                using (FileStream fs = File.OpenRead(filesToZip[i]))
                {
                    byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                    entry.Size = fs.Length;
                    fs.Close();
                    crc32.Reset();
                    crc32.Update(buffer);
                    entry.Crc = crc32.Value;
                    stream.PutNextEntry(entry);
                    stream.Write(buffer, 0, buffer.Length);
                }
            }
            stream.Finish();
            stream.Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            UpdateStockFile(stocks);
            UpdateUploadFile(uploads);
        }

        private bool FailWLPExist()
        {
            if (setter != null)
            {
                WayListPoint first_failed_wlp = (from wlps in setter.WayListSettings.WayListPoints
                                                 where
                                                 ( wlps.Point_type == WayListPointTypes.Common &&
                                                   wlps.Report_date.Date == setter.Report_date.Date &&
                                                   (!wlps.DataDefined || wlps.Point_leave.Hour + wlps.Point_leave.Minute==0))
                                                 ||
                                                 ( wlps.Point_type == WayListPointTypes.Start &&
                                                   wlps.Report_date.Date == setter.Report_date.Date &&
                                                   !wlps.DataDefined)
                                                 ||
                                                 ( wlps.Point_type == WayListPointTypes.Finish &&
                                                   wlps.Report_date.Date == setter.Report_date.Date &&
                                                   !wlps.DataDefined )
                                                 select wlps).FirstOrDefault();
                if ( first_failed_wlp != null )
                    return true;
            }
            return false;
        }

        private void bAddWayListPoint_Click(object sender, RoutedEventArgs e)
        {
            if (cbIPs.SelectedValue != null && cbSPs.SelectedValue != null)
            {
                string selectedip = (cbIPs.SelectedValue as ComboBoxItem).Content as string;
                IPandSPRecord selectedsp = ((cbSPs.SelectedValue as ComboBoxItem).DataContext as IPandSPRecord);

                if (WLPPresented(WayListPointTypes.Start, false))
                {
                    WayListPoint cwlp = (from wlps in setter.WayListSettings.WayListPoints
                                         where wlps.IP_name == selectedip
                                         && wlps.SP_code == selectedsp.SP_code
                                         && wlps.Report_date.Date == setter.Report_date.Date
                                         && wlps.Point_type == WayListPointTypes.Common
                                         select wlps).FirstOrDefault();
                    if (WLPPresented(WayListPointTypes.Finish, false))
                        MessageBox.Show("Конечная точка уже присутствует в путевом листе", "Добавление точки невозможно", MessageBoxButton.OK, MessageBoxImage.Stop);
                    else
                        if (cwlp != null)
                            MessageBox.Show(String.Format("Точка уже присутствует в путевом листе.\n{0}\n{1}", selectedip, selectedsp.SP_code), "Добавление точки невозможно", MessageBoxButton.OK, MessageBoxImage.Stop);
                        else
                        {
                            if (FailWLPExist() == false)
                            {
                                int hour = DateTime.Now.Hour < 10 ? 10 : DateTime.Now.Hour;
                                hour = hour >= 19 ? 19 : hour;
                                int minute = hour < 10 || hour >= 19 ? 0 : DateTime.Now.Minute;
                                cwlp = new WayListPoint()
                                {
                                    Report_date = setter.Report_date,
                                    IP_name = selectedip,
                                    SP_code = selectedsp.SP_code,
                                    Point_address = (cbSPaddresses.SelectedValue as IPandSPRecord).SP_address,
                                    Point_enter = new DateTime(
                                        setter.Report_date.Year,
                                        setter.Report_date.Month,
                                        setter.Report_date.Day,
                                        hour,
                                        minute,
                                        0),
                                    Point_type = WayListPointTypes.Common
                                };
                                DateTime _point_leave = cwlp.Point_enter.Add(new TimeSpan(0, 10, 0));
                                hour = _point_leave.Hour < 10 ? 10 : _point_leave.Hour;
                                hour = hour >= 19 ? 19 : hour;
                                minute = hour < 10 || hour >= 19 ? 0 : _point_leave.Minute;
                                cwlp.Point_leave = new DateTime(
                                    setter.Report_date.Year,
                                    setter.Report_date.Month,
                                    setter.Report_date.Day,
                                    hour,
                                    minute,
                                    0);
                                setter.WayListSettings.WayListPoints.Add(cwlp);
                                LoadWaylist();
                                setter.WayListSettings.Save(setter.WorkDir + @"\WayList.xml");
                            }
                            else
                            {
                                MessageBox.Show("Есть незаполненные данные по путевому листу!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Stop);
                            }
                        }
                }
                else
                {
                    if (MessageBox.Show("Начальная точка путевого листа не задана!\nДобавить начальную точку?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.Yes)
                        if (waylistmode_window != null)
                        {
                            waylistmode_window.Reload();
                            waylistmode_window.ShowDialog();
                        }
                }
                WayListLocker();
            }
        }

        private void bMin_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void bMax_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState != WindowState.Maximized)
                this.WindowState = WindowState.Maximized;
            else
                this.WindowState = WindowState.Normal;
        }

        private void bClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        public void ImportCSV(string importFileName, Excel.Worksheet destinationSheet,
                            Excel.Range destinationRange, int[] columnDataTypes, bool autoFitColumns)
        {
            destinationSheet.QueryTables.Add(
                "TEXT;" + System.IO.Path.GetFullPath(importFileName),
            destinationRange, Type.Missing);
            destinationSheet.QueryTables[1].Name = System.IO.Path.GetFileNameWithoutExtension(importFileName);
            destinationSheet.QueryTables[1].FieldNames = true;
            destinationSheet.QueryTables[1].RowNumbers = false;
            
            destinationSheet.QueryTables[1].FillAdjacentFormulas = false;
            destinationSheet.QueryTables[1].PreserveFormatting = true;
            destinationSheet.QueryTables[1].RefreshOnFileOpen = false;
            destinationSheet.QueryTables[1].RefreshStyle = Excel.XlCellInsertionMode.xlInsertDeleteCells;
            destinationSheet.QueryTables[1].SavePassword = false;
            destinationSheet.QueryTables[1].SaveData = true;
            destinationSheet.QueryTables[1].AdjustColumnWidth = true;
            destinationSheet.QueryTables[1].RefreshPeriod = 0;
            destinationSheet.QueryTables[1].TextFilePromptOnRefresh = false;
            destinationSheet.QueryTables[1].TextFilePlatform = 1251;
            destinationSheet.QueryTables[1].TextFileStartRow = 1;
            destinationSheet.QueryTables[1].TextFileParseType = Excel.XlTextParsingType.xlDelimited;
            destinationSheet.QueryTables[1].TextFileTextQualifier = Excel.XlTextQualifier.xlTextQualifierDoubleQuote;
            destinationSheet.QueryTables[1].TextFileConsecutiveDelimiter = false;
            destinationSheet.QueryTables[1].TextFileTabDelimiter = false;
            destinationSheet.QueryTables[1].TextFileSemicolonDelimiter = true;
            destinationSheet.QueryTables[1].TextFileCommaDelimiter = false;
            destinationSheet.QueryTables[1].TextFileSpaceDelimiter = false;
            destinationSheet.QueryTables[1].TextFileColumnDataTypes = columnDataTypes;

            //Logger.GetInstance().WriteLog("Importing data...");
            destinationSheet.QueryTables[1].Refresh(false);

            if (autoFitColumns == true)
                destinationSheet.QueryTables[1].Destination.EntireColumn.AutoFit();

            // cleanup
            //this.ActiveSheet.QueryTables[1].Delete();
        }

        private void datagrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(sender.GetType() == typeof(DataGrid))
                if ((sender as DataGrid).IsReadOnly && setter.WayListMode != WayListModeSet.Pedestrian)
                {
                    MessageBox.Show("Введите транзитную точку в путевой лист!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
        }

        private void bSaveData_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateStockFile(stocks) && UpdateUploadFile(uploads))
                MessageBox.Show("Сохранение данных остатков и отгрузок прошло успешно","Сохранение данных", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void bDeleteStockRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender.GetType() == typeof(Button) && (sender as Button).DataContext != null && (sender as Button).DataContext.GetType() == typeof(StockRecord))
                stocks.Remove((sender as Button).DataContext as StockRecord);
        }
    }
}



