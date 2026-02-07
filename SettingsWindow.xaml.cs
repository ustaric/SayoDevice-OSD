using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SayoOSD
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private RawInputReceiver _rawInput;
        private OsdWindow _osd;

        public SettingsWindow(AppSettings settings, RawInputReceiver rawInput, OsdWindow osd)
        {
            InitializeComponent();
            _settings = settings;
            _rawInput = rawInput;
            _osd = osd;

            // 초기값 로드
            TxtVid.Text = _settings.DeviceVid;
            TxtPid.Text = _settings.DevicePid;
            SldOpacity.Value = _settings.OsdOpacity;
            TxtTimeout.Text = _settings.OsdTimeout.ToString();
            CboMode.SelectedIndex = _settings.OsdMode;
            ChkMoveOsd.IsChecked = _osd.ResizeMode == ResizeMode.CanResize;

            UpdateLanguage();
            RefreshDeviceList();
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList();
        }

        private void RefreshDeviceList()
        {
            if (_rawInput == null) return;
            string vid = TxtVid.Text;
            var devices = _rawInput.GetAvailableDevices(vid);
            CboDevices.ItemsSource = devices;
            if (devices.Count > 0)
            {
                var current = devices.FirstOrDefault(d => d.Pid.Equals(TxtPid.Text, StringComparison.OrdinalIgnoreCase));
                if (current != null) CboDevices.SelectedItem = current;
                else CboDevices.SelectedIndex = 0;
            }
        }

        private void CboDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboDevices.SelectedItem is DeviceInfo info)
            {
                TxtPid.Text = info.Pid;
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            _settings.DeviceVid = TxtVid.Text;
            _settings.DevicePid = TxtPid.Text;
            _rawInput.UpdateTargetDevice(_settings.DeviceVid, _settings.DevicePid);
            AppSettings.Save(_settings);
            System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgVidApplied"));
        }

        private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings != null && _osd != null)
            {
                _settings.OsdOpacity = e.NewValue;
                _osd.Opacity = e.NewValue;
                AppSettings.Save(_settings);
            }
        }

        private void TxtTimeout_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settings != null && int.TryParse(TxtTimeout.Text, out int timeout))
            {
                _settings.OsdTimeout = timeout;
                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }

        private void CboMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings != null && _osd != null)
            {
                _settings.OsdMode = CboMode.SelectedIndex;
                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }

        private void ChkMoveOsd_CheckedChanged(object sender, RoutedEventArgs e)
        {
            _osd.SetMoveMode(ChkMoveOsd.IsChecked == true);
        }

        private void BtnResetSize_Click(object sender, RoutedEventArgs e)
        {
            _osd.ResetSize();
            AppSettings.Save(_settings);
        }

        public void UpdateLanguage()
        {
            string lang = _settings.Language;
            this.Title = LanguageManager.GetString(lang, "Title") + " - Settings";
            GrpDevice.Header = LanguageManager.GetString(lang, "GrpDevice");
            LblVid.Text = LanguageManager.GetString(lang, "LblVid");
            LblPid.Text = LanguageManager.GetString(lang, "LblPid");
            BtnScan.Content = LanguageManager.GetString(lang, "BtnScan");
            BtnApply.Content = LanguageManager.GetString(lang, "BtnApply");
            MsgSayoOnly.Text = LanguageManager.GetString(lang, "MsgSayoOnly");
            GrpOsd.Header = LanguageManager.GetString(lang, "GrpOsd");
            LblOpacity.Text = LanguageManager.GetString(lang, "LblOpacity");
            LblTimeout.Text = LanguageManager.GetString(lang, "LblTimeout");
            LblMode.Text = LanguageManager.GetString(lang, "LblMode");
            ChkMoveOsd.Content = LanguageManager.GetString(lang, "ChkMoveOsd");
            BtnResetSize.Content = LanguageManager.GetString(lang, "BtnResetSize");
        }
    }
}