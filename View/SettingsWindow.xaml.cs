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
        private bool _isUpdatingUi = false; // [추가] UI 업데이트 중 이벤트 발생 방지

        public SettingsWindow(AppSettings settings, RawInputReceiver rawInput, OsdWindow osd)
        {
            InitializeComponent();
            _settings = settings;
            _rawInput = rawInput;
            _osd = osd;
            this.DataContext = _settings; // [추가] 데이터 바인딩 컨텍스트 설정

            // 초기값 로드
            TxtVid.Text = _settings.DeviceVid;
            TxtPid.Text = _settings.DevicePid;
            SldOpacity.Value = _settings.OsdOpacity;
            TxtTimeout.Text = _settings.OsdTimeout.ToString();
            CboMode.SelectedIndex = _settings.OsdMode;
            ChkMoveOsd.IsChecked = _osd.ResizeMode == ResizeMode.CanResize;
            ChkEnableFileLog.IsChecked = _settings.EnableFileLog;
            SldVolumeStep.Value = _settings.VolumeStep; // [추가] 볼륨 설정 로드
            SldPaletteFontSize.Value = _settings.PaletteFontSize; // [추가] 팔레트 폰트 크기 로드
            
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

            // [추가] 폰트 목록 초기화
            var koKr = System.Windows.Markup.XmlLanguage.GetLanguage("ko-kr");
            foreach (var fontFamily in System.Windows.Media.Fonts.SystemFontFamilies.OrderBy(f => f.Source))
            {
                string displayName = fontFamily.Source;
                if (fontFamily.FamilyNames.ContainsKey(koKr))
                {
                    displayName = fontFamily.FamilyNames[koKr];
                }
                CboFontFamily.Items.Add(new ComboBoxItem { Content = displayName, Tag = fontFamily.Source });
            }
            
            // [추가] 폰트 굵기 초기화
            CboFontWeight.Items.Add("Normal");
            CboFontWeight.Items.Add("Bold");
            CboFontWeight.Items.Add("ExtraBold");
            CboFontWeight.Items.Add("Thin");
            CboFontWeight.Items.Add("Light");

            // [수정] 설정 대상 콤보박스 초기화 (프로필 포함)
            RefreshTargetList();

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

        // [추가] 설정 대상 목록 갱신 (전역 + 레이어 + 프로필)
        private void RefreshTargetList()
        {
            // 기존 선택 상태 저장
            object selectedTag = (CboTarget.SelectedItem as ComboBoxItem)?.Tag;

            CboTarget.Items.Clear();

            // 1. 전역 설정
            CboTarget.Items.Add(new ComboBoxItem { Content = LanguageManager.GetString(_settings.Language, "ItemGlobal"), Tag = "Global" });

            // 2. 하드웨어 레이어
            for (int i = 0; i < 5; i++)
            {
                CboTarget.Items.Add(new ComboBoxItem { Content = $"Layer {i}", Tag = i });
            }

            // 3. 앱 프로필 (가상 레이어)
            foreach (var profile in _settings.AppProfiles)
            {
                CboTarget.Items.Add(new ComboBoxItem { Content = $"[App] {profile.Name}", Tag = profile });
            }

            // 선택 상태 복구
            if (selectedTag != null)
            {
                var itemToSelect = CboTarget.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag.Equals(selectedTag) || i.Tag == selectedTag);
                if (itemToSelect != null) CboTarget.SelectedItem = itemToSelect;
                else CboTarget.SelectedIndex = 0;
            }
            else
                CboTarget.SelectedIndex = 0;
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

        private void SldPaletteFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings != null)
            {
                _settings.PaletteFontSize = e.NewValue;
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

        // [추가] 프로필 추가 버튼 핸들러
        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Executables (*.exe)|*.exe" };
            if (dlg.ShowDialog() == true)
            {
                string exeName = System.IO.Path.GetFileName(dlg.FileName);
                string profileName = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
                
                if (_settings.AppProfiles.Any(p => p.ProcessName.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgProcessExists"));
                    return;
                }

                var newProfile = new AppProfile { Name = profileName, ProcessName = exeName, ExecutablePath = dlg.FileName };
                _settings.AppProfiles.Add(newProfile);
                
                RefreshTargetList(); // [추가] 콤보박스 갱신
                RefreshProfileList();
                AppSettings.Save(_settings);
            }
        }

        // [추가] 프로필 삭제 버튼 핸들러
        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (DgProfiles.SelectedItem is AppProfile profile)
            {
                string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgDeleteProfileConfirm"), profile.Name);
                string title = LanguageManager.GetString(_settings.Language, "TitleDeleteProfile");

                if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _settings.AppProfiles.Remove(profile);
                    RefreshTargetList(); // [추가] 콤보박스 갱신
                    RefreshProfileList();
                    AppSettings.Save(_settings);
                }
            }
        }

        // [추가] 프로필 내보내기 (Export)
        private void BtnExportProfile_Click(object sender, RoutedEventArgs e)
        {
            if (DgProfiles.SelectedItem is AppProfile profile)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = LanguageManager.GetString(_settings.Language, "TitleExportProfile"),
                    FileName = profile.Name,
                    DefaultExt = ".json",
                    Filter = "JSON Files (*.json)|*.json"
                };

                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        string jsonString = System.Text.Json.JsonSerializer.Serialize(profile, options);
                        System.IO.File.WriteAllText(dlg.FileName, jsonString);
                        
                        string msg = LanguageManager.GetString(_settings.Language, "MsgExportSuccess");
                        string title = LanguageManager.GetString(_settings.Language, "TitleExportSuccess");
                        System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgExportFailed"), ex.Message);
                        string title = LanguageManager.GetString(_settings.Language, "TitleError");
                        System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgSelectProfile"));
            }
        }

        // [추가] 프로필 가져오기 (Import)
        private void BtnImportProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = LanguageManager.GetString(_settings.Language, "TitleImportProfile"),
                DefaultExt = ".json",
                Filter = "JSON Files (*.json)|*.json"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string jsonString = System.IO.File.ReadAllText(dlg.FileName);
                    var newProfile = System.Text.Json.JsonSerializer.Deserialize<AppProfile>(jsonString);

                    if (newProfile == null)
                    {
                        System.Windows.MessageBox.Show(LanguageManager.GetString(_settings.Language, "MsgInvalidProfile"));
                        return;
                    }

                    // 중복 이름 확인
                    var existing = _settings.AppProfiles.FirstOrDefault(p => p.Name == newProfile.Name);
                    if (existing != null)
                    {
                        string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgProfileExists"), newProfile.Name);
                        string title = LanguageManager.GetString(_settings.Language, "TitleDuplicate");
                        if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            // 기존 프로필 교체
                            int index = _settings.AppProfiles.IndexOf(existing);
                            _settings.AppProfiles[index] = newProfile;
                        }
                        else
                        {
                            return; // 취소
                        }
                    }
                    else
                    {
                        _settings.AppProfiles.Add(newProfile);
                    }

                    RefreshTargetList(); // [추가] 콤보박스 갱신
                    RefreshProfileList();
                    AppSettings.Save(_settings);
                    
                    string msgSuccess = LanguageManager.GetString(_settings.Language, "MsgImportSuccess");
                    string titleSuccess = LanguageManager.GetString(_settings.Language, "TitleImportSuccess");
                    System.Windows.MessageBox.Show(msgSuccess, titleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgImportFailed"), ex.Message);
                    string title = LanguageManager.GetString(_settings.Language, "TitleError");
                    System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // [추가] 프로필 리스트 UI 갱신 (List<T>는 변경 알림이 없으므로 수동 갱신 필요)
        private void RefreshProfileList()
        {
            DgProfiles.Items.Refresh();
            // ComboBox 갱신을 위해 ItemsSource 재설정
            var currentFallback = _settings.FallbackProfileId;
            CboFallback.ItemsSource = null;
            CboFallback.ItemsSource = _settings.AppProfiles;
            _settings.FallbackProfileId = currentFallback;
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
            if (LblPaletteFontSize != null) LblPaletteFontSize.Text = LanguageManager.GetString(lang, "LblPaletteFontSize");
            if (_chkVertical != null) _chkVertical.Content = LanguageManager.GetString(lang, "ChkVertical");
            if (_chkSwapRows != null) _chkSwapRows.Content = LanguageManager.GetString(lang, "ChkSwapRows");
            
            if (_btnLicense != null) _btnLicense.Content = LanguageManager.GetString(lang, "BtnLicense");

            // [추가] 탭 헤더 번역 (키가 없으면 기본값 유지)
            if (TabGeneral != null) TabGeneral.Header = LanguageManager.GetString(lang, "TabGeneral") ?? "일반 설정";
            if (TabProfiles != null) TabProfiles.Header = LanguageManager.GetString(lang, "TabProfiles") ?? "앱 프로필";

            // [추가] 프로필 버튼 번역
            if (BtnAddProfile != null) BtnAddProfile.Content = LanguageManager.GetString(lang, "BtnAddProfile");
            if (BtnDeleteProfile != null) BtnDeleteProfile.Content = LanguageManager.GetString(lang, "BtnDeleteProfile");
            if (BtnExportProfile != null) BtnExportProfile.Content = LanguageManager.GetString(lang, "BtnExportProfile");
            if (BtnImportProfile != null) BtnImportProfile.Content = LanguageManager.GetString(lang, "BtnImportProfile");

            // [추가] OSD 색상/폰트 설정 및 기타 UI 번역
            if (this.FindName("LblOsdColor") is TextBlock lblOsdColor) lblOsdColor.Text = LanguageManager.GetString(lang, "GrpColor");
            if (this.FindName("LblOsdFont") is TextBlock lblOsdFont) lblOsdFont.Text = LanguageManager.GetString(lang, "GrpFont");
            if (this.FindName("LblTargetSettings") is TextBlock lblTargetSettings) lblTargetSettings.Text = LanguageManager.GetString(lang, "LblTargetSettings");
            if (BtnGlobalBgColor != null) BtnGlobalBgColor.Content = LanguageManager.GetString(lang, "BtnBgColor");
            if (BtnGlobalHighlightColor != null) BtnGlobalHighlightColor.Content = LanguageManager.GetString(lang, "BtnHighlightColor");
            if (BtnGlobalBorderColor != null) BtnGlobalBorderColor.Content = LanguageManager.GetString(lang, "BtnBorderColor");
            if (BtnGlobalResetColor != null) BtnGlobalResetColor.Content = LanguageManager.GetString(lang, "BtnResetColor");
            if (BtnFontReset != null) BtnFontReset.Content = LanguageManager.GetString(lang, "BtnResetFont");
            if (this.FindName("LblFallbackProfile") is TextBlock lblFallbackProfile) lblFallbackProfile.Text = LanguageManager.GetString(lang, "LblFallbackProfile");
            if (this.FindName("LblFontFamily") is TextBlock lblFontFamily) lblFontFamily.Text = LanguageManager.GetString(lang, "LblFontFamily");
            if (this.FindName("LblFontSize") is TextBlock lblFontSize) lblFontSize.Text = LanguageManager.GetString(lang, "LblFontSize");
            if (this.FindName("LblFontWeight") is TextBlock lblFontWeight) lblFontWeight.Text = LanguageManager.GetString(lang, "LblFontWeight");

            // [추가] 앱 프로필 관련 UI 번역
            if (this.FindName("ChkEnableAppProfiles") is System.Windows.Controls.CheckBox chkEnableAppProfiles) chkEnableAppProfiles.Content = LanguageManager.GetString(lang, "ChkEnableAppProfiles");
            
            if (this.FindName("DgProfiles") is DataGrid dgProfiles && dgProfiles.Columns.Count >= 2)
            {
                dgProfiles.Columns[0].Header = LanguageManager.GetString(lang, "HeaderProfileName");
                dgProfiles.Columns[1].Header = LanguageManager.GetString(lang, "HeaderProcessName");
            }
            
            RefreshTargetList(); // 콤보박스 아이템 텍스트 갱신

            // [추가] OSD 모드 콤보박스 아이템 번역 적용
            if (CboMode != null && CboMode.Items.Count >= 3)
            {
                if (CboMode.Items[0] is ComboBoxItem item0) item0.Content = LanguageManager.GetString(lang, "ModeAuto");
                if (CboMode.Items[1] is ComboBoxItem item1) item1.Content = LanguageManager.GetString(lang, "ModeOn");
                if (CboMode.Items[2] is ComboBoxItem item2) item2.Content = LanguageManager.GetString(lang, "ModeOff");
            }
        }

        // [추가] 색상 선택 헬퍼 (Windows Forms ColorDialog 사용)
        private string PickColor(string currentHex)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            if (!string.IsNullOrEmpty(currentHex))
            {
                try
                {
                    var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(currentHex);
                    dlg.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
                }
                catch { }
            }

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Hex 문자열로 변환 (#AARRGGBB)
                return $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            }
            return null;
        }

        // [수정] 설정 대상 변경 시 폰트 UI 값 갱신
        private void CboTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;

            _isUpdatingUi = true; // 이벤트 루프 방지

            string family = null;
            double? size = null;
            string weight = null;

            if (selectedItem.Tag is string s && s == "Global")
            {
                family = _settings.OsdFontFamily;
                size = _settings.OsdFontSize;
                weight = _settings.OsdFontWeight;
            }
            else if (selectedItem.Tag is int layerIdx)
            {
                var style = _settings.LayerStyles[layerIdx];
                family = style.FontFamily;
                size = style.FontSize;
                weight = style.FontWeight;
            }
            else if (selectedItem.Tag is AppProfile profile)
            {
                family = profile.CustomOsdFontFamily;
                size = profile.CustomOsdFontSize;
                weight = profile.CustomOsdFontWeight;
            }

            // UI 반영
            if (family != null)
                CboFontFamily.SelectedItem = CboFontFamily.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == family);
            else
                CboFontFamily.SelectedItem = null;

            TxtFontSize.Text = size.HasValue ? size.Value.ToString() : "";
            CboFontWeight.SelectedItem = weight;

            _isUpdatingUi = false;
        }

        // [추가] 폰트 설정 변경 핸들러
        private void UpdateFontSettings()
        {
            if (_isUpdatingUi || !(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;

            string family = (CboFontFamily.SelectedItem as ComboBoxItem)?.Tag as string;
            string weight = CboFontWeight.SelectedItem as string;
            double? size = null;
            if (double.TryParse(TxtFontSize.Text, out double d)) size = d;

            if (selectedItem.Tag is string s && s == "Global")
            {
                if (family != null) _settings.OsdFontFamily = family;
                if (size.HasValue) _settings.OsdFontSize = size.Value;
                if (weight != null) _settings.OsdFontWeight = weight;
            }
            else if (selectedItem.Tag is int layerIdx)
            {
                var style = _settings.LayerStyles[layerIdx];
                style.FontFamily = family;
                style.FontSize = size;
                style.FontWeight = weight;
            }
            else if (selectedItem.Tag is AppProfile profile)
            {
                profile.CustomOsdFontFamily = family;
                profile.CustomOsdFontSize = size;
                profile.CustomOsdFontWeight = weight;
            }

            _osd.UpdateSettings(_settings);
            AppSettings.Save(_settings);
        }

        private void CboFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFontSettings();
        private void CboFontWeight_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFontSettings();
        private void TxtFontSize_TextChanged(object sender, TextChangedEventArgs e) => UpdateFontSettings();

        private void BtnFontReset_Click(object sender, RoutedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;
            
            _isUpdatingUi = true;
            // UI 초기화 (이벤트 발생 안함)
            if (selectedItem.Tag is string s && s == "Global")
            {
                CboFontFamily.SelectedItem = CboFontFamily.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == "Segoe UI");
                TxtFontSize.Text = "12";
                CboFontWeight.SelectedItem = "Normal";
            }
            else
            {
                CboFontFamily.SelectedItem = null;
                TxtFontSize.Text = "";
                CboFontWeight.SelectedItem = null;
            }
            _isUpdatingUi = false;
            UpdateFontSettings(); // 강제 저장
        }

        // [수정] 색상 변경 핸들러 (전역/레이어 공용)
        private void BtnGlobalBgColor_Click(object sender, RoutedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;
            
            string currentHex = null;
            if (selectedItem.Tag is string s && s == "Global") currentHex = _settings.OsdBackgroundColor;
            else if (selectedItem.Tag is int layerIdx) currentHex = _settings.LayerStyles[layerIdx].BackgroundColor;
            else if (selectedItem.Tag is AppProfile profile) currentHex = profile.CustomOsdBackgroundColor;

            string color = PickColor(currentHex);
            if (color != null)
            {
                if (selectedItem.Tag is string str && str == "Global") 
                    _settings.OsdBackgroundColor = color;
                else if (selectedItem.Tag is int layerIdx) 
                    _settings.LayerStyles[layerIdx].BackgroundColor = color;
                else if (selectedItem.Tag is AppProfile profile) 
                {
                    profile.CustomOsdBackgroundColor = color;
                }

                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }

        private void BtnGlobalHighlightColor_Click(object sender, RoutedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;

            string currentHex = null;
            if (selectedItem.Tag is string s && s == "Global") currentHex = _settings.OsdHighlightColor;
            else if (selectedItem.Tag is int layerIdx) currentHex = _settings.LayerStyles[layerIdx].HighlightColor;
            else if (selectedItem.Tag is AppProfile profile) currentHex = profile.CustomOsdHighlightColor;

            string color = PickColor(currentHex);
            if (color != null)
            {
                if (selectedItem.Tag is string str && str == "Global") 
                    _settings.OsdHighlightColor = color;
                else if (selectedItem.Tag is int layerIdx) 
                    _settings.LayerStyles[layerIdx].HighlightColor = color;
                else if (selectedItem.Tag is AppProfile profile)
                {
                    profile.CustomOsdHighlightColor = color;
                }

                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }

        private void BtnGlobalBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;

            string currentHex = null;
            if (selectedItem.Tag is string s && s == "Global") currentHex = _settings.OsdBorderColor;
            else if (selectedItem.Tag is int layerIdx) currentHex = _settings.LayerStyles[layerIdx].BorderColor;
            else if (selectedItem.Tag is AppProfile profile) currentHex = profile.CustomOsdBorderColor;

            string color = PickColor(currentHex);
            if (color != null)
            {
                if (selectedItem.Tag is string str && str == "Global") 
                    _settings.OsdBorderColor = color;
                else if (selectedItem.Tag is int layerIdx) 
                    _settings.LayerStyles[layerIdx].BorderColor = color;
                else if (selectedItem.Tag is AppProfile profile)
                {
                    profile.CustomOsdBorderColor = color;
                }

                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }

        // [수정] 색상 초기화 (전역/레이어 공용)
        private void BtnGlobalResetColor_Click(object sender, RoutedEventArgs e)
        {
            if (!(CboTarget.SelectedItem is ComboBoxItem selectedItem)) return;

            string targetName = selectedItem.Content.ToString();

            string msg = string.Format(LanguageManager.GetString(_settings.Language, "MsgResetColorConfirm"), targetName);
            string title = LanguageManager.GetString(_settings.Language, "TitleResetColor");

            if (System.Windows.MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (selectedItem.Tag is string s && s == "Global")
                {
                    _settings.OsdBackgroundColor = "#32FFFFFF";
                    _settings.OsdHighlightColor = "#FF007ACC";
                    _settings.OsdBorderColor = "#FFFFFF00";
                }
                else if (selectedItem.Tag is int layerIdx)
                {
                    var style = _settings.LayerStyles[layerIdx];
                    style.BackgroundColor = null;
                    style.HighlightColor = null;
                    style.BorderColor = null;
                }
                else if (selectedItem.Tag is AppProfile profile)
                {
                    profile.CustomOsdBackgroundColor = null;
                    profile.CustomOsdHighlightColor = null;
                    profile.CustomOsdBorderColor = null;
                }

                _osd.UpdateSettings(_settings);
                AppSettings.Save(_settings);
            }
        }
    }
}