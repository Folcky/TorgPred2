using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Xml.Serialization;
using System.IO;
using System.Collections.ObjectModel;
using Excel = Microsoft.Office.Interop.Excel;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Reflection;

namespace TorgPred
{
    public enum WayListModeSet { Pedestrian, Market, OnTransport };
    public enum WayListPointTypes { Start, Common, Finish };

    public class Settinger : INotifyPropertyChanged
    {
        public Settinger()
        {
            this.User = System.Security.Principal.WindowsIdentity.GetCurrent();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private string _last_user;
        private string _work_dir;
        private bool _show_setter=false;
        private bool _closeapp = false;
        public readonly string CustomGreen = "#FFB3FF80";
        public readonly string CustomRed = "#FFFF8989";
        public readonly string CustomBack = "#FF61615C";
        private DateTime _report_date=DateTime.Now;
        private WayListSettings _waylistsettings;
        private WayListModeSet _waylistmode = WayListModeSet.Pedestrian;
        public WayListModeSet WayListMode { get { return _waylistmode; } set { _waylistmode = value; NotifyPropertyChanged("WayListMode"); } }
        public string LastUser { get { return _last_user; } set { _last_user = value; NotifyPropertyChanged("LastUser"); } }

        private bool _filteraps = false;
        public bool FilterAPS { get { return _filteraps; } set { _filteraps = value; NotifyPropertyChanged("FilterAPS"); } }

        public string Version
        {
            get
            {
                try
                {
                    return typeof(Settinger).Assembly.FullName.Substring(typeof(Settinger).Assembly.FullName.IndexOf("Version") + 8, typeof(Settinger).Assembly.FullName.Substring(typeof(Settinger).Assembly.FullName.IndexOf("Version") + 8).IndexOf(","));
                }
                catch { return ""; }

            }
        }


        public string WorkDir
        {
            get
            {
                return _work_dir;
            }
            set
            {
                _work_dir = value;
                NotifyPropertyChanged("WorkDir");
                this.WayListSettings = WayListSettings.Load(_work_dir + @"\WayList.xml");
            }
        }
        public bool ShowSetter { get { return _show_setter; } set { _show_setter = value; NotifyPropertyChanged("ShowSetter"); } }
        public bool CloseApp { get { return _closeapp; } set { _closeapp = value; NotifyPropertyChanged("CloseApp"); } }
        public DateTime Report_date { get { return _report_date; } set { _report_date = value; NotifyPropertyChanged("Report_date"); } }
        public WayListSettings WayListSettings { get { return _waylistsettings; } set { _waylistsettings = value; NotifyPropertyChanged("WayListSettings"); } }

        private Excel.Application _excel=null;
        public Excel.Application Excel { get { return _excel; } set { _excel = value; NotifyPropertyChanged("WayListSettings"); } }

        private WindowsPrincipal _principal;
        public WindowsPrincipal Principal
        {
            get { return _principal; }
            set
            {
                if (value != null && value.GetType() == typeof(WindowsPrincipal))
                    _principal = value;
            }
        }

        private WindowsIdentity _user;
        public WindowsIdentity User
        {
            get { return _user; }
            set
            {
                if (value != null && value.GetType() == typeof(WindowsIdentity))
                {
                    _user = value;
                    this.Principal = new WindowsPrincipal(value);
                }
            }
        }

        public void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
        public Boolean IsWriteAccessEnabled(string dir)
        {
            if (Directory.Exists(dir))
            {
                DirectoryInfo dirinfo = new DirectoryInfo(dir);
                bool read = CheckListAccess(this.User, this.Principal, dirinfo, FileSystemRights.Read);
                bool write = CheckListAccess(this.User, this.Principal, dirinfo, FileSystemRights.Write);
                bool list = CheckListAccess(this.User, this.Principal, dirinfo, FileSystemRights.ListDirectory);

                return read && write && list;
            }
            else
                return false;
        }
        private bool CheckListAccess(WindowsIdentity user, WindowsPrincipal principal, DirectoryInfo directory, FileSystemRights right)
        {
            // These are set to true if either the allow read or deny read access rights are set
            bool allowList = false;
            bool denyList = false;

            try
            {
                // Get the collection of authorization rules that apply to the current directory
                AuthorizationRuleCollection acl = directory.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));

                for (int x = 0; x < acl.Count; x++)
                {
                    FileSystemAccessRule currentRule = (FileSystemAccessRule)acl[x];
                    // If the current rule applies to the current user
                    if (user.User.Equals(currentRule.IdentityReference) || principal.IsInRole((SecurityIdentifier)currentRule.IdentityReference))
                    {
                        if
                        (currentRule.AccessControlType.Equals(AccessControlType.Deny))
                        {
                            if ((currentRule.FileSystemRights & right) == right)
                            {
                                denyList = true;
                            }
                        }
                        else if
                        (currentRule.AccessControlType.Equals(AccessControlType.Allow))
                        {
                            if ((currentRule.FileSystemRights & right) == right)
                            {
                                allowList = true;
                            }
                        }
                    }
                }
            }
            catch { return false; }

            if (allowList & !denyList)
                return true;
            else
                return false;
        }

        private bool CheckListAccess(WindowsIdentity user, WindowsPrincipal principal, FileInfo directory, FileSystemRights right)
        {
            // These are set to true if either the allow read or deny read access rights are set
            bool allowList = false;
            bool denyList = false;

            try
            {
                // Get the collection of authorization rules that apply to the current directory
                AuthorizationRuleCollection acl = directory.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));

                for (int x = 0; x < acl.Count; x++)
                {
                    FileSystemAccessRule currentRule = (FileSystemAccessRule)acl[x];
                    // If the current rule applies to the current user
                    if (user.User.Equals(currentRule.IdentityReference) || principal.IsInRole((SecurityIdentifier)currentRule.IdentityReference))
                    {
                        if
                        (currentRule.AccessControlType.Equals(AccessControlType.Deny))
                        {
                            if ((currentRule.FileSystemRights & right) == right)
                            {
                                denyList = true;
                            }
                        }
                        else if
                        (currentRule.AccessControlType.Equals(AccessControlType.Allow))
                        {
                            if ((currentRule.FileSystemRights & right) == right)
                            {
                                allowList = true;
                            }
                        }
                    }
                }
            }
            catch { return false; }

            if (allowList & !denyList)
                return true;
            else
                return false;
        }

        public string[] CSVrow2StringArray(string row)
        {
            try
            {
                return Regex.Matches(row, "(?<=^|;)(\"(?:[^\"]|\"\")*\"|[^;]*)")
                                                    .Cast<Match>()
                                                    .Select(m => m.Groups[0].Value)
                                                    .ToArray();
            }
            catch { return null; }
        }

        public string String2CSVField(string field)
        {
            try
            {
                return !field.Contains(";") ? field : @"""" + field + @"""";
            }
            catch { return ""; }
        }

        public string CSVField2String(string field)
        {
            //На самом деле надо просто убить двойные кавычки
            try
            {
                return field.Replace(@"""","");
            }
            catch { return ""; }
        }

        public WaylistDateMode GetWaylistDateMode(DateTime report_date)
        {
            try
            {
                return (from sp in this.WayListSettings.WayListDateModes
                        where sp.Report_date.Date == report_date.Date
                        select sp).FirstOrDefault();
            }
            catch { return null; }
        }
    }

    public class StockRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string _ip_name = "";
        private string _sp_code = "";
        private string _sp_type = "";
        private string _tariff_name = "";
        private long _sim_num = 0;
        private string _comment_text = "";

        private DateTime _report_date;
        private string _tp_name = "";

        public string IP_name { get { return _ip_name; } set { _ip_name = value; NotifyPropertyChanged("IP_name"); } }
        public string SP_code { get { return _sp_code; } set { _sp_code = value; NotifyPropertyChanged("SP_code"); } }
        public string SP_type { get { return _sp_type; } set { _sp_type = value; NotifyPropertyChanged("SP_type"); } }
        public string COMMENT_text { get { return _comment_text; } set { _comment_text = value; NotifyPropertyChanged("COMMENT_text"); } }
        public string Tariff_name
        {
            get { return _tariff_name; }
            set
            {
                if (value != null && value != "")
                {
                    _tariff_name = value;
                    NotifyPropertyChanged("Tariff_name");
                }
            }
        }
        public long? Sim_num { get { return _sim_num; } set { _sim_num = (value == null ? 0 : (long)value); NotifyPropertyChanged("Sim_num"); } }
        public DateTime Report_date { get { return _report_date; } set { _report_date = value; NotifyPropertyChanged("Report_date"); } }
        public string TP_name { get { return _tp_name; } set { _tp_name = value; NotifyPropertyChanged("TP_name"); } }
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }
    public class IPandSPRecord
    {
        private string _ip_name = "";
        private string _sp_code = "";
        private string _sp_type = "";
        private string _week_day = "";
        private bool _comm_flag;
        private bool _new_sp;
        private bool _has_stockdata=false;
        private bool _has_uploaddata = false;
        public string IP_name { get { return _ip_name; } set { _ip_name = value; } }
        public string SP_code { get { return _sp_code; } set { _sp_code = value; } }
        public string SP_type { get { return _sp_type; } set { _sp_type = value; } }
        public string Week_day { get { return _week_day; } set { _week_day = value; } }
        public bool COMM_flag { get { return _comm_flag; } set { _comm_flag = value; } }
        public bool New_sp { get { return _new_sp; } set { _new_sp = value; } }
        public bool Has_StockData { get { return _has_stockdata; } set { _has_stockdata = value; } }
        public bool Has_UploadData { get { return _has_uploaddata; } set { _has_uploaddata = value; } }

        public string SP_address 
        { 
            get 
            {
                return SP_region + " " + SP_town + " " + SP_town_type + " " + SP_town_area + " " + SP_subway + " " + SP_street + " " + SP_street_type + " " + SP_house + " " + SP_house_building; 
            } 
        }

        private string _sp_region="";
        private string _sp_town = "";
        private string _sp_town_type = "";
        private string _sp_town_area = "";
        private string _sp_subway = "";
        private string _sp_street = "";
        private string _sp_street_type = "";
        private string _sp_house = "";
        private string _sp_house_building = "";
        private string _sp_added_description = "";

        public string SP_region { get { return _sp_region; } set { _sp_region = value != null ? value : ""; } }
        public string SP_town { get { return _sp_town; } set { _sp_town = value != null ? value : ""; } }
        public string SP_town_type { get { return _sp_town_type; } set { _sp_town_type = value != null ? value : ""; } }
        public string SP_town_area { get { return _sp_town_area; } set { _sp_town_area = value != null ? value : ""; } }
        public string SP_subway { get { return _sp_subway; } set { _sp_subway = value != null ? value : ""; } }
        public string SP_street { get { return _sp_street; } set { _sp_street = value != null ? value : ""; } }
        public string SP_street_type { get { return _sp_street_type; } set { _sp_street_type = value != null ? value : ""; } }
        public string SP_house { get { return _sp_house; } set { _sp_house = value != null ? value : ""; } }
        public string SP_house_building { get { return _sp_house_building; } set { _sp_house_building = value != null ? value : ""; } }
        public string SP_added_description { get { return _sp_added_description; } set { _sp_added_description = value != null ? value : ""; } }

        private string _tp_name="";
        public string TP_name { get { return _tp_name; } set { _tp_name = value != null ? value : ""; } }
    }
    public class UploadRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string _icc_id = "";
        private string _ip_name = "";
        private string _sp_code = "";
        private DateTime _report_date;
        private bool _comm_flag;
        private decimal _sim_price;
        private string _tp_name = "";
        private string _comment_text = "";
        //Повтор в текущем дне нужен для калькулятора отгрузок в шапке вкладки
        private bool _repeater = false;

        public string ICC_id 
        { 
            get 
            { 
                return _icc_id; 
            } 
            set 
            {
                if (value != null)
                {
                    _icc_id = value.Trim().Replace("\b", "").Replace(";", "");
                    NotifyPropertyChanged("ICC_id");
                }
            } 
        }
        public string IP_name { get { return _ip_name; } set { _ip_name = value; NotifyPropertyChanged("IP_name"); } }
        public string SP_code { get { return _sp_code; } set { _sp_code = value; NotifyPropertyChanged("SP_code"); } }
        public DateTime Report_date { get { return _report_date; } set { _report_date = value; NotifyPropertyChanged("Report_date"); } }
        public bool COMM_flag { get { return _comm_flag; } set { _comm_flag = value; NotifyPropertyChanged("COMM_flag"); } }
        public decimal SIM_price { get { return _sim_price; } set { _sim_price = value; NotifyPropertyChanged("SIM_price"); } }
        public string TP_name { get { return _tp_name; } set { _tp_name = value; NotifyPropertyChanged("TP_name"); } }
        public string COMMENT_text { get { return _comment_text; } set { _comment_text = value; NotifyPropertyChanged("COMMENT_text"); } }
        public bool Repeater { get { return _repeater; } set { _repeater = value; NotifyPropertyChanged("Repeater"); } }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }


    public class WaylistDateMode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private DateTime _report_date;
        [XmlElement("Report_date")]
        public DateTime Report_date { get { return _report_date; } set { _report_date = value; NotifyPropertyChanged("Report_date"); } }
        private WayListModeSet _date_waylistmode;
        [XmlElement("Date_WayListMode")]
        public WayListModeSet Date_WayListMode { get { return _date_waylistmode; } set { _date_waylistmode = value; NotifyPropertyChanged("Date_WayListMode"); } }
        private Boolean _filterAPSwithTP=false;
        [XmlElement("FilterAPSwithTP")]
        public Boolean FilterAPSwithTP { get { return _filterAPSwithTP; } set { _filterAPSwithTP = value; NotifyPropertyChanged("FilterAPSwithTP"); } }
        
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
    }

    public class WayListPoint : INotifyPropertyChanged
    {
        private bool _datadefined;
        public bool DataDefined { get { return _datadefined; } set { _datadefined = value; } }

        public event PropertyChangedEventHandler PropertyChanged;
        private string _ip_name="";
        [XmlElement("IP_name")]
        public string IP_name { get { return _ip_name; } set { _ip_name = value; CheckAllDefined(); NotifyPropertyChanged("IP_name"); } }
        private string _sp_code="";
        [XmlElement("SP_code")]
        public string SP_code { get { return _sp_code; } set { _sp_code = value; CheckAllDefined(); NotifyPropertyChanged("SP_code"); } }
        private DateTime _report_date;
        [XmlElement("Report_date")]
        public DateTime Report_date { get { return _report_date; } set { _report_date = value; CheckAllDefined(); NotifyPropertyChanged("Report_date"); } }
        private string _point_address;
        [XmlElement("Point_address")]
        public string Point_address { get { return _point_address; } set { _point_address = value; CheckAllDefined(); NotifyPropertyChanged("Point_address"); } }
        private DateTime _point_leave;
        [XmlElement("Point_leave")]
        public DateTime Point_leave { get { return _point_leave; } set { _point_leave = value; CheckAllDefined(); NotifyPropertyChanged("Point_leave"); } }
        private DateTime _point_enter;
        [XmlElement("Point_enter")]
        public DateTime Point_enter { get { return _point_enter; } set { _point_enter = value; CheckAllDefined(); NotifyPropertyChanged("Point_enter"); } }
        private long _speedmeter=0;
        [XmlElement("Speedmeter")]
        public long Speedmeter { get { return _speedmeter; } set { _speedmeter = value; CheckAllDefined(); NotifyPropertyChanged("Speedmeter"); } }
        private decimal _gaznumber_buyed = 0;
        [XmlElement("Gaznumber_buyed")]
        public decimal Gaznumber_buyed { get { return _gaznumber_buyed; } set { _gaznumber_buyed = value; CheckAllDefined(); NotifyPropertyChanged("Gaznumber_buyed"); } }
        private decimal _gaznumber_onpoint = 0;
        [XmlElement("Gaznumber_onpoint")]
        public decimal Gaznumber_onpoint { get { return _gaznumber_onpoint; } set { _gaznumber_onpoint = value; CheckAllDefined(); NotifyPropertyChanged("Gaznumber_onpoint"); } }
        private WayListPointTypes _point_type;
        [XmlElement("Point_type")]
        public WayListPointTypes Point_type { get { return _point_type; } set { _point_type = value; CheckAllDefined(); NotifyPropertyChanged("Speedmeter"); } }

        private void CheckAllDefined()
        {
            _datadefined = false;
            switch (Point_type)
            {
                case (WayListPointTypes.Start):
                    if (Point_leave != null && Report_date != null && Speedmeter > 0 && Report_date.Date == Point_leave.Date)
                        if ((Point_leave.Hour + Point_leave.Minute) != 0)
                            if (Gaznumber_onpoint != 0)
                                DataDefined = true;
                    break;
                case (WayListPointTypes.Common):
                    if (Point_enter != null && Report_date != null && Speedmeter > 0 && Report_date.Date == Point_enter.Date)
                        if ((Point_enter.Hour + Point_enter.Minute) != 0 
                            //&& (Point_leave.Hour + Point_leave.Minute) != 0
                            )
                            DataDefined = true;
                    break;
                case (WayListPointTypes.Finish):
                    if (Point_enter != null && Report_date != null && Speedmeter > 0 && Report_date.Date == Point_enter.Date)
                        if ((Point_enter.Hour + Point_enter.Minute) != 0)
                            if (Gaznumber_onpoint != 0)
                                DataDefined = true;
                    break;
                default:
                    DataDefined = false;
                    break;
            }
        }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
    }

    [XmlRoot("WayListSettings")]
    public class WayListSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        // здесь вместо XmlElement можно XmlAttribute. Формат сохранения будет другой. Проверьте.
        private string _company;
        [XmlElement("Company")]
        public string Company { get { return _company; } set { _company = value; CheckAllDefined(); NotifyPropertyChanged("Company"); } }
        private string _okud;
        [XmlElement("Okud")]
        public string Okud { get { return _okud; } set { _okud = value; CheckAllDefined(); NotifyPropertyChanged("Okud"); } }
        private string _okpo;
        [XmlElement("Okpo")]
        public string Okpo { get { return _okpo; } set { _okpo = value;  CheckAllDefined(); NotifyPropertyChanged("Okpo");} }
        private string _atobrand;
        [XmlElement("Autobrand")]
        public string Autobrand { get { return _atobrand; } set { _atobrand = value;  CheckAllDefined(); NotifyPropertyChanged("Autobrand");} }
        private string _auton;
        [XmlElement("AutoN")]
        public string AutoN { get { return _auton; } set { _auton = value;  CheckAllDefined(); NotifyPropertyChanged("AutoN");} }
        private string _fio;
        [XmlElement("FIO")]
        public string FIO { get { return _fio; } set { _fio = value;  CheckAllDefined(); NotifyPropertyChanged("FIO");} }
        private string _pravan;
        [XmlElement("PravaN")]
        public string PravaN { get { return _pravan; } set { _pravan = value;  CheckAllDefined(); NotifyPropertyChanged("PravaN");} }
        private string _pravaclass;
        [XmlElement("PravaClass")]
        public string PravaClass { get { return _pravaclass; } set { _pravaclass = value;  CheckAllDefined(); NotifyPropertyChanged("PravaClass");} }
        private string _licensetype;
        [XmlElement("LicenseType")]
        public string LicenseType { get { return _licensetype; } set { _licensetype = value;  CheckAllDefined();NotifyPropertyChanged("LicenseType"); } }
        private string _regn;
        [XmlElement("RegN")]
        public string RegN { get { return _regn; } set { _regn = value; CheckAllDefined();  NotifyPropertyChanged("RegN");} }
        private string _regseria;
        [XmlElement("RegSeria")]
        public string RegSeria { get { return _regseria; } set { _regseria = value;  CheckAllDefined(); NotifyPropertyChanged("RegSeria");} }
        private string _regn2;
        [XmlElement("RegN2")]
        public string RegN2 { get { return _regn2; } set { _regn2 = value;  CheckAllDefined();NotifyPropertyChanged("RegN2"); } }
        private string _purpose;
        [XmlElement("Purpose")]
        public string Purpose { get { return _purpose; } set { _purpose = value;  CheckAllDefined(); NotifyPropertyChanged("Purpose");} }
        private string _fiosign="";
        [XmlElement("FIOSign")]
        public string FIOSign { get { return _fiosign; } set { _fiosign = value;  CheckAllDefined();NotifyPropertyChanged("FIOSign"); } }
        private decimal _gazlimit = 0;
        [XmlElement("GazLimit")]
        public decimal GazLimit { get { return _gazlimit; } set { _gazlimit = value; CheckAllDefined(); NotifyPropertyChanged("GazLimit"); } }

        private List<string> _startendpoints = new List<string>();
        [XmlArray("StartEndPoints")]
        public List<string> StartEndPoints{ get { return _startendpoints; } set { _startendpoints = value;  NotifyPropertyChanged("StartEndPoints"); } }

        private ObservableCollection<WayListPoint> _waylistpoints = new ObservableCollection<WayListPoint>();
        [XmlArray("WayListPoints")]
        public ObservableCollection<WayListPoint> WayListPoints { get { return _waylistpoints; } set { _waylistpoints = value; NotifyPropertyChanged("WayListPoints"); } }

        private ObservableCollection<WaylistDateMode> _waylistdatemodes = new ObservableCollection<WaylistDateMode>();
        [XmlArray("WayListDateModes")]
        public ObservableCollection<WaylistDateMode> WayListDateModes { get { return _waylistdatemodes; } set { _waylistdatemodes = value; NotifyPropertyChanged("WayListDateModes"); } }

        private bool _datadefined;
        public bool DataDefined { get { return _datadefined; } set { _datadefined = value; } }
        private void CheckAllDefined()
        {
            _datadefined = false;
            if (Company != null && Company.Trim() != "" &&
                Okud != null && Okud.Trim() != "" &&
                Okpo != null && Okpo.Trim() != "" &&
                Autobrand != null && Autobrand.Trim() != "" &&
                AutoN != null && AutoN.Trim() != "" &&
                FIO != null && FIO.Trim() != "" &&
                PravaN != null && PravaN.Trim() != "" &&
                PravaClass != null && PravaClass.Trim() != "" &&
                LicenseType != null && LicenseType.Trim() != "" &&
                RegN != null && RegN.Trim() != "" &&
                RegSeria != null && RegSeria.Trim() != "" &&
                RegN2 != null && RegN2.Trim() != "" &&
                Purpose != null && Purpose.Trim() != "" &&
                FIOSign != null && FIOSign.Trim() != "" &&
                GazLimit > 0)
                DataDefined = true;
            else
                DataDefined = false;
        }

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }

        public void Save(string waylistsettings_file)
        {
            FileInfo waylister = new FileInfo(waylistsettings_file);

            if (!waylister.IsReadOnly || !waylister.Exists)
            {
                if (StartEndPoints.Count > 1)
                    StartEndPoints = (from p in StartEndPoints
                                      where p != null
                                      group p by p.Trim() into groups
                                      select groups.First()).ToList();
                XmlSerializer s = new XmlSerializer(typeof(WayListSettings));
                StreamWriter w = new StreamWriter(waylistsettings_file);
                s.Serialize(w, this);
                w.Flush();
                w.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Файл " + waylistsettings_file + " только для чтения." + Environment.NewLine + "Снимите флажок 'только для чтения' с рабочей папки!", "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Stop);
                App.Current.Shutdown();
            }
        }

        public static WayListSettings Load(string waylistsettings_file)
        {
            WayListSettings waylistsettings = new WayListSettings();
            if (File.Exists(waylistsettings_file))
            {
                try
                {
                    XmlSerializer s = new XmlSerializer(typeof(WayListSettings));
                    TextReader r = new StreamReader(waylistsettings_file);
                    waylistsettings = (WayListSettings)s.Deserialize(r);
                    r.Close();
                }
                catch { }
            }
            return waylistsettings;
        }
    }
}
