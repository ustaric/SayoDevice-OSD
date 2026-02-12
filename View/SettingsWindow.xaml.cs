using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SayoOSD.Models;
using SayoOSD.Services;
using SayoOSD.Managers;
using SayoOSD.Views;
using System.Security.Principal;

namespace SayoOSD.Views
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;
        private RawInputReceiver _rawInput;
        private OsdWindow _osd;
        private System.Windows.Controls.Button _btnLicense; // 라이선스 버튼 동적 추가
        private System.Windows.Controls.CheckBox _chkVertical; // [추가] 세로 모드 체크박스
        private System.Windows.Controls.CheckBox _chkSwapRows; // [추가] 줄 교체 체크박스

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
            ChkEnableFileLog.IsChecked = _settings.EnableFileLog;
            SldVolumeStep.Value = _settings.VolumeStep; // [추가] 볼륨 설정 로드
            
            // [추가] 언어 콤보박스 구성
            CboLanguage.Items.Clear();
            foreach (var lang in LanguageManager.GetLanguages())
            {
                int percent = lang.GetCompletionPercentage(LanguageManager.Keys.Count);
                var item = new System.Windows.Controls.ComboBoxItem();
                item.Content = $"{lang.Name} ({percent}%)";
                item.Tag = lang.Code;
                CboLanguage.Items.Add(item);
                
                if (lang.Code == _settings.Language) CboLanguage.SelectedItem = item;
            }

            // [추가] 윈도우 시작 시 자동 실행 여부 확인
            try
            {
                ChkStartWithWindows.IsChecked = StartupManager.IsStartupTaskEnabled();
            }
            catch
            {
                ChkStartWithWindows.IsChecked = false;
            }

            // [추가] 라이선스 버튼 생성 및 설정
            _btnLicense = new System.Windows.Controls.Button();
            _btnLicense.Click += BtnLicense_Click;
            _btnLicense.Margin = new Thickness(0, 10, 0, 0);
            _btnLicense.Height = 24;

            // [추가] 세로 모드 및 줄 교체 체크박스 생성
            _chkVertical = new System.Windows.Controls.CheckBox { Margin = new Thickness(0, 5, 0, 0) };
            _chkVertical.IsChecked = _settings.OsdVertical;
            _chkVertical.Checked += (s, e) => ApplyLayoutSettings();
            _chkVertical.Unchecked += (s, e) => ApplyLayoutSettings();

            _chkSwapRows = new System.Windows.Controls.CheckBox { Margin = new Thickness(0, 5, 0, 0) };
            _chkSwapRows.IsChecked = _settings.OsdSwapRows;
            _chkSwapRows.Checked += (s, e) => ApplyLayoutSettings();
            _chkSwapRows.Unchecked += (s, e) => ApplyLayoutSettings();

            // UI 로드 후 컨트롤 동적 추가
            this.Loaded += (s, e) => {
                if (GrpOsd.Parent is System.Windows.Controls.Panel parent && !parent.Children.Contains(_btnLicense))
                {
                    parent.Children.Add(_btnLicense);
                }

                // [수정] 세로 모드/줄 교체 옵션을 OSD 설정 그룹의 맨 아래에 추가 (줄바꿈 효과)
                // GrpOsd.Content가 메인 패널(StackPanel 등)이라고 가정하고 맨 뒤에 추가
                if (GrpOsd.Content is System.Windows.Controls.Panel osdMainPanel)
                {
                    if (!osdMainPanel.Children.Contains(_chkVertical))
                    {
                        osdMainPanel.Children.Add(_chkVertical);
                        osdMainPanel.Children.Add(_chkSwapRows);
                    }
                }
            };

            UpdateLanguage();
            RefreshDeviceList();
        }

        // [추가] 레이아웃 설정 즉시 적용
        private void ApplyLayoutSettings()
        {
            _settings.OsdVertical = _chkVertical.IsChecked == true;
            _settings.OsdSwapRows = _chkSwapRows.IsChecked == true;
            _osd.UpdateSettings(_settings); // OSD 갱신 (UpdateLayoutOrientation 호출됨)
            AppSettings.Save(_settings);
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
            if (_settings != null && double.TryParse(TxtTimeout.Text, out double timeout))
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

        private void ChkEnableFileLog_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_settings != null)
            {
                _settings.EnableFileLog = ChkEnableFileLog.IsChecked == true;
                AppSettings.Save(_settings);
                LogManager.Enabled = _settings.EnableFileLog;
            }
        }

        private void ChkStartWithWindows_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // 창이 로드되는 중(생성자)에는 이벤트 무시
            if (!this.IsLoaded) return;

            try
            {
                bool enable = ChkStartWithWindows.IsChecked == true;
                StartupManager.SetStartup(enable);

                // 관리자 권한으로 실행 시 사용자에게 알림
                bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                if (enable && isAdmin)
                {
                    string msg = LanguageManager.GetString(_settings.Language, "MsgStartupRegistered");
                    string title = LanguageManager.GetString(_settings.Language, "TitleStartupRegistered");
                    System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                string msgFail = string.Format(LanguageManager.GetString(_settings.Language, "MsgStartupFailed"), ex.Message);
                System.Windows.MessageBox.Show(msgFail);
                // 실패 시 체크 상태 복구 (이벤트 재발생 방지 위해 로직 주의 필요하나 여기선 단순 처리)
                if (sender is System.Windows.Controls.CheckBox chk) 
                {
                    // 이벤트 핸들러 잠시 제거 후 복구하는 것이 안전하지만, 간단히 예외 메시지만 띄움
                }
            }
        }

        private void SldVolumeStep_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings != null)
            {
                _settings.VolumeStep = (int)e.NewValue;
                AppSettings.Save(_settings);
            }
        }

        private void CboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settings == null || CboLanguage == null || !IsLoaded) return;

            if (CboLanguage.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                string lang = item.Tag.ToString();
                if (_settings.Language != lang)
                {
                    _settings.Language = lang;
                    AppSettings.Save(_settings); // 메인 윈도우의 OnSettingsSaved 이벤트 발생
                    UpdateLanguage(); // 설정 창 UI 즉시 갱신
                }
            }
        }

        // [추가] 라이선스 버튼 클릭 이벤트
        private void BtnLicense_Click(object sender, RoutedEventArgs e)
        {
            string license = LanguageManager.GetString(_settings.Language, "LicenseText");
            string title = LanguageManager.GetString(_settings.Language, "TitleLicense");
            
            // [수정] MessageBox 대신 스크롤 가능한 별도 윈도우(Dialog) 생성
            // 표준적인 약관/라이선스 표시 방식입니다.
            var viewer = new Window
            {
                Title = title,
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                WindowStyle = WindowStyle.ToolWindow, // 최소화/최대화 버튼 없는 대화상자 스타일
                Content = new System.Windows.Controls.TextBox
                {
                    Text = license,
                    IsReadOnly = true, // 읽기 전용
                    TextWrapping = TextWrapping.Wrap, // 자동 줄바꿈
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, // 세로 스크롤바 자동 표시
                    Padding = new Thickness(10),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, Monospace"), // 고정폭 글꼴 추천
                    FontSize = 12
                }
            };

            viewer.ShowDialog(); // 모달 창으로 열기 (닫기 전까지 부모 창 제어 불가)
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
            GrpAudio.Header = LanguageManager.GetString(lang, "GrpAudio"); // [추가]
            GrpSystem.Header = LanguageManager.GetString(lang, "GrpSystemSettings");
            ChkEnableFileLog.Content = LanguageManager.GetString(lang, "ChkEnableFileLog");
            LblVolumeStep.Text = LanguageManager.GetString(lang, "LblVolumeStep");
            ChkStartWithWindows.Content = LanguageManager.GetString(lang, "ChkStartWithWindows");
            if (_chkVertical != null) _chkVertical.Content = LanguageManager.GetString(lang, "ChkVertical");
            if (_chkSwapRows != null) _chkSwapRows.Content = LanguageManager.GetString(lang, "ChkSwapRows");
            
            if (_btnLicense != null) _btnLicense.Content = LanguageManager.GetString(lang, "BtnLicense");

            // [추가] OSD 모드 콤보박스 아이템 번역 적용
            if (CboMode != null && CboMode.Items.Count >= 3)
            {
                if (CboMode.Items[0] is ComboBoxItem item0) item0.Content = LanguageManager.GetString(lang, "ModeAuto");
                if (CboMode.Items[1] is ComboBoxItem item1) item1.Content = LanguageManager.GetString(lang, "ModeOn");
                if (CboMode.Items[2] is ComboBoxItem item2) item2.Content = LanguageManager.GetString(lang, "ModeOff");
            }
        }
    }
}