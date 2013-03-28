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
using System.ComponentModel;

namespace TorgPred
{
    /// <summary>
    /// Interaction logic for TimeControl.xaml
    /// </summary>
    public partial class TimeControl : UserControl, INotifyPropertyChanged
    {
        public TimeControl()
        {
            InitializeComponent();
        }

        public TimeSpan Value
        {
            get { return (TimeSpan)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(TimeSpan), typeof(TimeControl),
        new UIPropertyMetadata(DateTime.Now.TimeOfDay, new PropertyChangedCallback(OnValueChanged)));
        //my edits
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(info));
        }
        public DateTime TimeValue
        {
            get { return (DateTime)GetValue(TimeProperty); }
            set { SetValue(TimeProperty, value); }
        }

        public static readonly DependencyProperty TimeProperty =
           DependencyProperty.Register("TimeValue", typeof(DateTime), typeof(TimeControl),
           new UIPropertyMetadata(DateTime.Now, new PropertyChangedCallback(OnTimeChangedTimeVal)));

        private static void OnTimeChangedTimeVal(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeControl control = obj as TimeControl;
            control.Value = new TimeSpan(control.TimeValue.Hour, control.TimeValue.Minute, control.TimeValue.Second);
        }
        //

        private static void OnValueChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeControl control = obj as TimeControl;
            control.Hours = ((TimeSpan)e.NewValue).Hours;
            control.Minutes = ((TimeSpan)e.NewValue).Minutes;
            control.Seconds = ((TimeSpan)e.NewValue).Seconds;
            control.TimeValue = new DateTime(2012, 1, 1, control.Hours, control.Minutes, control.Seconds);
        }

        public int Hours
        {
            get { return (int)GetValue(HoursProperty); }
            set { 
                SetValue(HoursProperty, value);
                //my edits
                NotifyPropertyChanged("Hours");
                NotifyPropertyChanged("Value");
                /////
            }
        }
        public static readonly DependencyProperty HoursProperty =
        DependencyProperty.Register("Hours", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Minutes
        {
            get { return (int)GetValue(MinutesProperty); }
            set { 
                SetValue(MinutesProperty, value);
                //my edits
                NotifyPropertyChanged("Minutes");
                NotifyPropertyChanged("Value");
                /////////
            }
        }
        public static readonly DependencyProperty MinutesProperty =
        DependencyProperty.Register("Minutes", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));

        public int Seconds
        {
            get { return (int)GetValue(SecondsProperty); }
            set { SetValue(SecondsProperty, value); }
        }

        public static readonly DependencyProperty SecondsProperty =
        DependencyProperty.Register("Seconds", typeof(int), typeof(TimeControl),
        new UIPropertyMetadata(0, new PropertyChangedCallback(OnTimeChanged)));


        private static void OnTimeChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            TimeControl control = obj as TimeControl;
            control.Value = new TimeSpan(control.Hours, control.Minutes, control.Seconds);
        }

        private void Down(object sender, KeyEventArgs args)
        {
            switch (((Grid)sender).Name)
            {
                case "sec":
                    if (args.Key == Key.Up)
                        this.Seconds++;
                    if (args.Key == Key.Down)
                        this.Seconds--;
                    break;

                case "min":
                    if (args.Key == Key.Up)
                        this.Minutes++;
                    if (args.Key == Key.Down)
                        this.Minutes--;
                    break;

                case "hour":
                    if (args.Key == Key.Up)
                        this.Hours++;
                    if (args.Key == Key.Down)
                        this.Hours--;
                    break;
            }
        }

    } 
}
