using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using SayoOSD.Managers;

namespace SayoOSD.Managers
{
    public class LanguageProfile
    {
        public string Code { get; set; } // 예: KO, EN
        public string Name { get; set; } // 예: 한국어, English
        public Dictionary<string, string> Strings { get; set; } = new Dictionary<string, string>();

        // 번역 완성도 계산 (기준 키 개수 대비)
        public int GetCompletionPercentage(int totalKeys)
        {
            if (totalKeys == 0) return 0;
            int count = Strings.Count(kv => !string.IsNullOrWhiteSpace(kv.Value));
            return (int)((double)count / totalKeys * 100);
        }
    }

    public class LocalizationData
    {
        public int Version { get; set; } = 0;
        public List<LanguageProfile> Languages { get; set; } = new List<LanguageProfile>();
    }

    public static class LanguageManager
    {
        public const int CurrentVersion = 2; // [수정] 버전 증가 (기존 파일 강제 갱신 유도)
        private static LocalizationData _data;
        private static string _filePath = Path.Combine(AppContext.BaseDirectory, "languages.json");
        
        // UI에서 사용할 키 목록 (기준)
        public static readonly List<string> Keys = new List<string>
        {
            "Title", "GrpDevice", "BtnScan", "BtnApply", "LblVid", "LblPid", "MsgSayoOnly",
            "GrpOsd", "LblOpacity", "LblTimeout", "LblMode", "ModeAuto", "ModeOn", "ModeOff",
            "ChkMoveOsd", "BtnResetSize",
            "GrpSystemSettings",
            "ChkVertical", "ChkSwapRows", "GrpAudio", "GrpMap", "ColTime", "ColKey", "ColData", "MnuCopy",
            "ChkPauseLog", "LblLayer", "LblSlot", "LblName", "BtnRename",
            "LblTarget", "TargetNone", "LblSignal", "BtnAutoDetect", "BtnManualDetect", "BtnUnmap",
            "ActionRun",
            "ActionTextMacro", "ActionAudioCycle",
            "ActionOsdCycle", "ModeBottom",
            "TitleInputText", "MsgEnterText",
            "ActionActiveVolUp", "ActionActiveVolDown", "LblVolumeStep",
            "CtxDeleteMacro", "TitleDeleteMacro", "MsgDeleteMacroConfirm",
            "HeaderMedia", "ActionMediaPlayPause", "ActionMediaNext", "ActionMediaPrev",
            "ActionVolUp", "ActionVolDown", "ActionVolMute",
            "HeaderLayerMove",
            "ChkEnableFileLog", "ChkStartWithWindows", "BtnSave", "BtnHide",
            "BtnOpenSettings", "MsgNameChanged", "MsgSelectSlot", "MsgDetecting", "MsgSelectSignal",
            "MsgUnmapConfirm", "MsgUnmapped", "MsgPatternMapped", "MsgVidApplied", "MsgSettingsSaved",
            "TitleSelectSignal", "TitleUnmap",
            "MsgSaved",
            "BtnLicense", "TitleLicense", "LicenseText"
            , "CtxOsdMode", "CtxMoveOsd", "CtxExit", "MsgDetectionDone",
            "GrpFunctions", "LblPath", "LblArgs", "LblIcon", "BtnBrowse", "BtnChange", 
            "BtnSavePanel", "BtnCancelPanel", "HeaderSystem", "HeaderAction",
            "MsgPatternMappedDetail", "MsgConfirmRunProgram", "TitleRunProgram", "MsgNeedMapping", "TitleNeedMapping",
            "MsgUnmapConfirmDetail", "MsgStartupRegistered", "TitleStartupRegistered", "MsgStartupFailed",
            "ChkUseClipboard", // [추가]
            "TabGeneral", "TabProfiles", "ActionProfileCycle",
            "MsgLanguageUpdated",
            "BtnAddProfile", "BtnDeleteProfile", "BtnExportProfile", "BtnImportProfile",
            "MsgProcessExists", "MsgDeleteProfileConfirm", "TitleDeleteProfile",
            "TitleExportProfile", "MsgExportSuccess", "TitleExportSuccess", "MsgExportFailed", "TitleError", "MsgSelectProfile",
            "TitleImportProfile", "MsgInvalidProfile", "MsgProfileExists", "TitleDuplicate", "MsgImportSuccess", "TitleImportSuccess", "MsgImportFailed",
            "MsgResetColorConfirm", "TitleResetColor",
            "GrpColor", "GrpFont", "LblTargetSettings", "ItemGlobal",
            "BtnBgColor", "BtnHighlightColor", "BtnBorderColor", "BtnResetColor", "BtnResetFont",
            "LblFallbackProfile", "LblFontFamily", "LblFontSize", "LblFontWeight",
            "ChkEnableAppProfiles", "HeaderProfileName", "HeaderProcessName",
            "ActionMicMute",
            "LblPaletteFontSize"
        };

        public static void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    _data = JsonSerializer.Deserialize<LocalizationData>(json);
                }
                catch { _data = null; }
            }

            // 기본값 데이터 생성 (코드에 정의된 최신 데이터)
            var defaults = GetDefaultData();
            bool versionUpdated = false;

            if (_data == null)
            {
                _data = defaults;
                Save();
            }
            else
            {
                // 기존 파일이 있더라도, 코드에 새로 추가된 키나 언어가 누락되어 있다면 병합합니다.
                // 버전 확인 및 백업
                if (_data.Version < CurrentVersion)
                {
                    try
                    {
                        string backupPath = _filePath + ".bak";
                        File.Copy(_filePath, backupPath, true);
                    }
                    catch { /* 백업 실패 시 무시 */ }

                    _data.Version = CurrentVersion;
                    versionUpdated = true;
                }

                bool changed = false;
                var defaultEn = defaults.Languages.FirstOrDefault(l => l.Code == "EN");

                foreach (var defLang in defaults.Languages)
                {
                    var existingLang = _data.Languages.FirstOrDefault(l => l.Code == defLang.Code);
                    if (existingLang == null)
                    {
                        _data.Languages.Add(defLang);
                        changed = true;
                    }
                    else
                    {
                        foreach (var kv in defLang.Strings)
                        {
                            // 1. 키가 아예 없으면 추가
                            if (!existingLang.Strings.ContainsKey(kv.Key))
                            {
                                existingLang.Strings[kv.Key] = kv.Value;
                                changed = true;
                            }
                            // [수정] "LblTarget" 키의 값이 구버전(이동/Target/Action)일 경우 "기능"으로 강제 업데이트
                            else if (kv.Key == "LblTarget")
                            {
                                bool update = (existingLang.Code == "KO" && (existingLang.Strings[kv.Key] == "이동:" || existingLang.Strings[kv.Key] == "동작:")) ||
                                              (existingLang.Code == "EN" && (existingLang.Strings[kv.Key] == "Target:" || existingLang.Strings[kv.Key] == "Action:")) ||
                                              (existingLang.Code == "FR" && (existingLang.Strings[kv.Key] == "Cible:" || existingLang.Strings[kv.Key] == "Action:")) ||
                                              (existingLang.Code == "ES" && (existingLang.Strings[kv.Key] == "Destino:" || existingLang.Strings[kv.Key] == "Acción:")) ||
                                              (existingLang.Code == "CN" && (existingLang.Strings[kv.Key] == "目标:" || existingLang.Strings[kv.Key] == "动作:")) ||
                                              (existingLang.Code == "DE" && (existingLang.Strings[kv.Key] == "Ziel:" || existingLang.Strings[kv.Key] == "Aktion:"));
                                
                                if (update) { existingLang.Strings[kv.Key] = kv.Value; changed = true; }
                            }
                            // [추가] 라이선스 버튼 텍스트 강제 업데이트 (HidSharp 추가 반영)
                            // 기존 파일에 "NAudio"만 있고 "HidSharp"가 없으면 새 값으로 덮어씀
                            else if (kv.Key == "BtnLicense")
                            {
                                if (!existingLang.Strings[kv.Key].Contains("HidSharp"))
                                {
                                    existingLang.Strings[kv.Key] = kv.Value;
                                    changed = true;
                                }
                            }
                            // [추가] 라이선스 본문 강제 업데이트 (HidSharp 누락 시)
                            else if (kv.Key == "LicenseText")
                            {
                                if (!existingLang.Strings[kv.Key].Contains("HidSharp"))
                                {
                                    existingLang.Strings[kv.Key] = kv.Value;
                                    changed = true;
                                }
                            }
                            // 2. 키는 있는데 값이 영어 기본값과 같고, 새로운 번역 데이터는 영어와 다르다면 업데이트 (번역 적용)
                            else if (defaultEn != null && defaultEn.Strings.ContainsKey(kv.Key))
                            {
                                string currentVal = existingLang.Strings[kv.Key];
                                string enVal = defaultEn.Strings[kv.Key];
                                string newVal = kv.Value;

                                if (currentVal == enVal && newVal != enVal)
                                {
                                    existingLang.Strings[kv.Key] = newVal;
                                    changed = true;
                                }
                            }
                        }
                    }
                }

                if (changed || versionUpdated) Save();

                if (versionUpdated)
                {
                    // 업데이트 알림 (기본 한국어 메시지 사용)
                    string msg = GetString("KO", "MsgLanguageUpdated");
                    System.Windows.MessageBox.Show(msg, "Language Update", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public static void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_data, options);
            File.WriteAllText(_filePath, json);
        }

        public static List<LanguageProfile> GetLanguages()
        {
            return _data.Languages;
        }

        public static string GetString(string langCode, string key)
        {
            if (_data == null) Load();

            var lang = _data.Languages.FirstOrDefault(l => l.Code == langCode);
            if (lang != null && lang.Strings.ContainsKey(key))
            {
                return lang.Strings[key];
            }
            // 해당 언어에 없으면 영어(EN)에서 찾고, 없으면 키 자체 반환
            var fallback = _data.Languages.FirstOrDefault(l => l.Code == "EN");
            if (fallback != null && fallback.Strings.ContainsKey(key)) return fallback.Strings[key];
            return key;
        }

        // NAudio 라이선스 전문 (공통 사용)
        private const string NAudioLicense = 
@"NAudio 2.2.1
Copyright 2020 Mark Heath

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the ""Software""), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

        // [추가] HidSharp 라이선스
        private const string HidSharpLicense =
@"

--------------------------------------------------

HidSharp 2.6.4
Copyright © 2012-2021 James F. Bellinger
License: Apache License 2.0
URL: https://www.nuget.org/packages/HidSharp/2.6.4/License

Licensed under the Apache License, Version 2.0 (the ""License"");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an ""AS IS"" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.


                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      ""License"" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      ""Licensor"" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      ""Legal Entity"" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      ""control"" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      ""You"" (or ""Your"") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      ""Source"" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      ""Object"" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      ""Work"" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      ""Derivative Works"" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      ""Contribution"" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, ""submitted""
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as ""Not a Contribution.""

      ""Contributor"" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a ""NOTICE"" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an ""AS IS"" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS";

        private static LocalizationData GetDefaultData()
        {
            var data = new LocalizationData { Version = CurrentVersion };

            // 1. 한국어 (KO)
            var ko = new LanguageProfile { Code = "KO", Name = "한국어" };
            ko.Strings = new Dictionary<string, string>
            {
                { "Title", "SayoDevice OSD 설정" },
                { "GrpDevice", "장치 설정 (장치 관리자에서 확인)" },
                { "BtnScan", "검색" }, { "BtnApply", "적용" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* SayoDevice만 감지합니다." },
                { "GrpOsd", "OSD 설정" },
                { "LblOpacity", "투명도:" }, { "LblTimeout", "표시 시간(초):" }, { "LblMode", "표시 모드:" },
                { "ModeAuto", "자동 (입력 시 표시)" }, { "ModeOn", "항상 켜기" }, { "ModeOff", "항상 끄기" },
                { "ChkMoveOsd", "OSD 위치 이동 허용" }, { "BtnResetSize", "크기 초기화" },
                { "ChkVertical", "세로 모드 (6x2)" }, { "ChkSwapRows", "1번줄/7번줄 위치 교체" },
                { "GrpSystemSettings", "시스템 설정" },
                { "GrpAudio", "오디오 설정" },
                { "GrpMap", "키 매핑 설정 (settings.json 직접 수정 권장)" },
                { "ColTime", "시간" }, { "ColKey", "키값(Hex)" }, { "ColData", "전체 데이터" }, { "MnuCopy", "복사" },
                { "ChkPauseLog", "로그 일시정지" },
                { "LblLayer", "레이어:" }, { "LblSlot", "슬롯:" }, { "LblName", "이름:" }, { "BtnRename", "이름변경" },
                { "LblTarget", "기능:" }, { "TargetNone", "기능 없음" }, { "BtnManualDetect", "수동 감지 (목록)" },
                { "ActionRun", "프로그램 연결..." },
                { "ActionTextMacro", "텍스트 매크로 (자동 입력)" }, { "ActionAudioCycle", "오디오 장치 전환 (순차)" },
                { "ActionOsdCycle", "OSD 표시 모드 변경 (순환)" }, { "ModeBottom", "제일 아래 (바탕화면)" },
                { "TitleInputText", "텍스트 입력" }, { "MsgEnterText", "매크로로 입력할 텍스트를 입력하세요.\n(예: 안녕하세요{ENTER})" },
                { "ActionActiveVolUp", "활성 창 볼륨 증가" },
                { "ActionActiveVolDown", "활성 창 볼륨 감소" },
                { "LblVolumeStep", "볼륨 조절 단위:" },
                { "CtxDeleteMacro", "상용구 삭제 (모든 키에서 해제)" }, { "TitleDeleteMacro", "상용구 삭제" }, { "MsgDeleteMacroConfirm", "'{0}' 상용구를 삭제하시겠습니까?\n이 상용구를 사용하는 모든 키의 매핑이 해제됩니다." },
                { "HeaderMedia", "미디어 제어" },
                { "ActionMediaPlayPause", "재생 / 일시정지" }, { "ActionMediaNext", "다음 곡" }, { "ActionMediaPrev", "이전 곡" },
                { "ActionVolUp", "볼륨 증가" }, { "ActionVolDown", "볼륨 감소" }, { "ActionVolMute", "음소거 (시스템)" },
                { "HeaderLayerMove", "레이어 이동 선택" },
                { "LblSignal", "선택한 신호:" }, { "BtnAutoDetect", "자동 감지" }, { "BtnUnmap", "매핑해제" },
                { "ChkEnableFileLog", "로그 파일 저장" }, { "ChkStartWithWindows", "윈도우 시작 시 자동 실행" },
                { "BtnSave", "설정 저장" }, { "BtnHide", "트레이로 숨기기" }, { "BtnOpenSettings", "설정" },
                { "MsgNameChanged", "이름이 변경되었습니다." },
                { "MsgSelectSlot", "매핑할 슬롯을 먼저 선택해주세요." },
                { "MsgDetecting", "감지 중..." },
                { "MsgSelectSignal", "목록에서 신호를 선택해주세요." },
                { "MsgUnmapConfirm", "매핑 정보를 초기화하시겠습니까?" },
                { "MsgUnmapped", "매핑이 해제되었습니다." },
                { "MsgPatternMapped", "패턴이 매핑되었습니다." },
                { "MsgVidApplied", "VID/PID가 적용되었습니다." },
                { "MsgSettingsSaved", "설정이 저장되었습니다." },
                { "MsgSaved", "저장됨" },
                { "TitleSelectSignal", "신호 선택 (키를 누르세요)" },
                { "TitleUnmap", "매핑 해제" },
                { "BtnLicense", "오픈소스 라이선스 (NAudio, HidSharp)" },
                { "TitleLicense", "오픈소스 라이선스 정보" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "OSD 표시 모드" },
                { "CtxMoveOsd", "OSD 이동" },
                { "CtxExit", "종료" },
                { "MsgDetectionDone", "감지 완료 (선택하세요)" },
                { "GrpFunctions", "기능 팔레트" }, { "LblPath", "파일 경로:" }, { "LblArgs", "실행 인수:" },
                { "LblIcon", "아이콘:" }, { "BtnBrowse", "찾아보기..." }, { "BtnChange", "변경" },
                { "BtnSavePanel", "저장" }, { "BtnCancelPanel", "취소" },
                { "HeaderSystem", "시스템 기능" }, { "HeaderAction", "단축키 / 실행" },
                { "MsgPatternMappedDetail", "Key {0}에 패턴이 매핑되었습니다.\n패턴: {1}" },
                { "MsgConfirmRunProgram", "Key {0}에 '{1}'을(를) 등록하시겠습니까?" }, { "TitleRunProgram", "프로그램 등록" },
                { "MsgNeedMapping", "이 키는 아직 하드웨어 버튼과 연결되지 않았습니다.\n지금 매핑하시겠습니까?" }, { "TitleNeedMapping", "매핑 필요" },
                { "MsgUnmapConfirmDetail", "Key {0} (Layer {1})의 매핑 정보를 초기화하시겠습니까?" },
                { "MsgStartupRegistered", "관리자 권한으로 자동 실행이 등록되었습니다.\n윈도우 로그인 시 백그라운드에서 자동으로 실행됩니다." }, { "TitleStartupRegistered", "자동 실행 등록" },
                { "MsgStartupFailed", "자동 실행 설정 실패: {0}" },
                { "ChkUseClipboard", "클립보드에 복사하기" },
                { "TabGeneral", "일반 설정" },
                { "TabProfiles", "앱 프로필" },
                { "ActionProfileCycle", "프로필 순환" },
                { "MsgLanguageUpdated", "언어 파일이 최신 버전으로 업데이트되었습니다.\n사용자 변경 사항은 유지됩니다." },
                { "BtnAddProfile", "추가" }, { "BtnDeleteProfile", "삭제" }, { "BtnExportProfile", "내보내기" }, { "BtnImportProfile", "가져오기" },
                { "MsgProcessExists", "이미 등록된 프로세스입니다." }, { "MsgDeleteProfileConfirm", "'{0}' 프로필을 삭제하시겠습니까?" }, { "TitleDeleteProfile", "삭제 확인" },
                { "TitleExportProfile", "프로필 내보내기" }, { "MsgExportSuccess", "프로필을 성공적으로 내보냈습니다." }, { "TitleExportSuccess", "내보내기 완료" }, { "MsgExportFailed", "프로필 내보내기 실패: {0}" }, { "TitleError", "오류" }, { "MsgSelectProfile", "내보낼 프로필을 선택해주세요." },
                { "TitleImportProfile", "프로필 가져오기" }, { "MsgInvalidProfile", "잘못된 프로필 파일 형식입니다." }, { "MsgProfileExists", "'{0}' 프로필이 이미 존재합니다.\n덮어쓰시겠습니까?" }, { "TitleDuplicate", "중복 확인" }, { "MsgImportSuccess", "프로필을 성공적으로 가져왔습니다." }, { "TitleImportSuccess", "가져오기 완료" }, { "MsgImportFailed", "프로필 가져오기 실패: {0}" },
                { "MsgResetColorConfirm", "'{0}'의 OSD 색상을 초기화하시겠습니까?" }, { "TitleResetColor", "색상 초기화" },
                { "GrpColor", "OSD 색상" }, { "GrpFont", "OSD 폰트" }, { "LblTargetSettings", "설정 대상:" }, { "ItemGlobal", "전역 설정 (Global)" },
                { "BtnBgColor", "배경색" }, { "BtnHighlightColor", "강조색" }, { "BtnBorderColor", "테두리색" }, { "BtnResetColor", "초기화" }, { "BtnResetFont", "초기화" },
                { "LblFallbackProfile", "기본 프로필 (Fallback):" }, { "LblFontFamily", "글꼴:" }, { "LblFontSize", "크기:" }, { "LblFontWeight", "굵기:" },
                { "ChkEnableAppProfiles", "가상 레이어(앱 프로필) 기능 활성화" }, { "HeaderProfileName", "프로필 이름" }, { "HeaderProcessName", "연결된 프로세스 (.exe)" },
                { "ActionMicMute", "마이크 음소거 (토글)" },
                { "LblPaletteFontSize", "기능 팔레트 폰트 크기:" }
            };
            data.Languages.Add(ko);

            // 2. English (EN)
            var en = new LanguageProfile { Code = "EN", Name = "English" };
            en.Strings = new Dictionary<string, string>
            {
                { "Title", "SayoDevice OSD Settings" },
                { "GrpDevice", "Device Settings (Check Device Manager)" },
                { "BtnScan", "Scan" }, { "BtnApply", "Apply" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* Detects SayoDevice only." },
                { "GrpOsd", "OSD Settings" },
                { "LblOpacity", "Opacity:" }, { "LblTimeout", "Timeout(s):" }, { "LblMode", "Mode:" },
                { "ModeAuto", "Auto (On Input)" }, { "ModeOn", "Always On" }, { "ModeOff", "Always Off" },
                { "ChkMoveOsd", "Allow OSD Move" }, { "BtnResetSize", "Reset Size" },
                { "ChkVertical", "Vertical Mode (6x2)" }, { "ChkSwapRows", "Swap Rows (1st/7th)" },
                { "GrpSystemSettings", "System Settings" },
                { "GrpAudio", "Audio Settings" },
                { "GrpMap", "Key Mapping (Edit settings.json recommended)" },
                { "ColTime", "Time" }, { "ColKey", "Key(Hex)" }, { "ColData", "Full Data" }, { "MnuCopy", "Copy" },
                { "ChkPauseLog", "Pause Log" },
                { "LblLayer", "Layer:" }, { "LblSlot", "Slot:" }, { "LblName", "Name:" }, { "BtnRename", "Rename" },
                { "LblTarget", "Function:" }, { "TargetNone", "None" }, { "BtnManualDetect", "Manual Detect (List)" },
                { "ActionRun", "Run Program..." },
                { "ActionTextMacro", "Text Macro (Auto Input)" }, { "ActionAudioCycle", "Audio Device Cycle" },
                { "ActionOsdCycle", "Cycle OSD Mode" }, { "ModeBottom", "Bottommost" },
                { "TitleInputText", "Input Text Macro" }, { "MsgEnterText", "Enter text for macro.\n(e.g. Hello{ENTER})" },
                { "ActionActiveVolUp", "Active Window Vol +" },
                { "ActionActiveVolDown", "Active Window Vol -" },
                { "LblVolumeStep", "Volume Step:" },
                { "CtxDeleteMacro", "Delete Macro (Unmap all)" }, { "TitleDeleteMacro", "Delete Macro" }, { "MsgDeleteMacroConfirm", "Delete macro '{0}'?\nThis will unmap it from all keys." },
                { "HeaderMedia", "Media Control" },
                { "ActionMediaPlayPause", "Play / Pause" }, { "ActionMediaNext", "Next Track" }, { "ActionMediaPrev", "Prev Track" },
                { "ActionVolUp", "Volume Up" }, { "ActionVolDown", "Volume Down" }, { "ActionVolMute", "Mute (System)" },
                { "HeaderLayerMove", "Select Layer" },
                { "LblSignal", "Selected Signal:" }, { "BtnAutoDetect", "Auto Detect" }, { "BtnUnmap", "Unmap" },
                { "ChkEnableFileLog", "Save Log to File" }, { "ChkStartWithWindows", "Run on Startup" },
                { "BtnSave", "Save Settings" }, { "BtnHide", "Hide to Tray" }, { "BtnOpenSettings", "Settings" },
                { "MsgNameChanged", "Name changed." },
                { "MsgSelectSlot", "Please select a slot to map." },
                { "MsgDetecting", "Detecting..." },
                { "MsgSelectSignal", "Please select a signal from the list." },
                { "MsgUnmapConfirm", "Reset mapping for this key?" },
                { "MsgUnmapped", "Unmapped." },
                { "MsgPatternMapped", "Pattern mapped." },
                { "MsgVidApplied", "VID/PID Applied." },
                { "MsgSettingsSaved", "Settings saved." },
                { "MsgSaved", "Saved" },
                { "TitleSelectSignal", "Select Signal (Press Key)" },
                { "TitleUnmap", "Unmap" },
                { "BtnLicense", "Open Source Licenses (NAudio, HidSharp)" },
                { "TitleLicense", "Open Source Licenses" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "OSD Display Mode" },
                { "CtxMoveOsd", "Move OSD" },
                { "CtxExit", "Exit" },
                { "MsgDetectionDone", "Detection Complete (Select)" },
                { "GrpFunctions", "Function Palette" }, { "LblPath", "File Path:" }, { "LblArgs", "Arguments:" },
                { "LblIcon", "Icon:" }, { "BtnBrowse", "Browse..." }, { "BtnChange", "Change" },
                { "BtnSavePanel", "Save" }, { "BtnCancelPanel", "Cancel" },
                { "HeaderSystem", "System" }, { "HeaderAction", "Shortcuts / Run" },
                { "MsgPatternMappedDetail", "Pattern mapped to Key {0}.\nPattern: {1}" },
                { "MsgConfirmRunProgram", "Register '{1}' to Key {0}?" }, { "TitleRunProgram", "Register Program" },
                { "MsgNeedMapping", "This key is not mapped to a hardware button yet.\nMap it now?" }, { "TitleNeedMapping", "Mapping Required" },
                { "MsgUnmapConfirmDetail", "Reset mapping for Key {0} (Layer {1})?" },
                { "MsgStartupRegistered", "Startup registered with admin rights.\nIt will run in background on login." }, { "TitleStartupRegistered", "Startup Registered" },
                { "MsgStartupFailed", "Startup setup failed: {0}" },
                { "ChkUseClipboard", "Copy to Clipboard" },
                { "TabGeneral", "General" },
                { "TabProfiles", "App Profiles" },
                { "ActionProfileCycle", "Cycle Profiles" },
                { "MsgLanguageUpdated", "Language file updated to the latest version.\nYour changes are preserved." },
                { "BtnAddProfile", "Add" }, { "BtnDeleteProfile", "Delete" }, { "BtnExportProfile", "Export" }, { "BtnImportProfile", "Import" },
                { "MsgProcessExists", "Process already registered." }, { "MsgDeleteProfileConfirm", "Delete profile '{0}'?" }, { "TitleDeleteProfile", "Delete Confirmation" },
                { "TitleExportProfile", "Export Profile" }, { "MsgExportSuccess", "Profile exported successfully." }, { "TitleExportSuccess", "Export Complete" }, { "MsgExportFailed", "Export failed: {0}" }, { "TitleError", "Error" }, { "MsgSelectProfile", "Please select a profile to export." },
                { "TitleImportProfile", "Import Profile" }, { "MsgInvalidProfile", "Invalid profile file format." }, { "MsgProfileExists", "Profile '{0}' already exists.\nOverwrite?" }, { "TitleDuplicate", "Duplicate Check" }, { "MsgImportSuccess", "Profile imported successfully." }, { "TitleImportSuccess", "Import Complete" }, { "MsgImportFailed", "Import failed: {0}" },
                { "MsgResetColorConfirm", "Reset OSD colors for '{0}'?" }, { "TitleResetColor", "Reset Colors" },
                { "GrpColor", "OSD Colors" }, { "GrpFont", "OSD Fonts" }, { "LblTargetSettings", "Target:" }, { "ItemGlobal", "Global Settings" },
                { "BtnBgColor", "Background" }, { "BtnHighlightColor", "Highlight" }, { "BtnBorderColor", "Border" }, { "BtnResetColor", "Reset" }, { "BtnResetFont", "Reset" },
                { "LblFallbackProfile", "Default Profile (Fallback):" }, { "LblFontFamily", "Font:" }, { "LblFontSize", "Size:" }, { "LblFontWeight", "Weight:" },
                { "ChkEnableAppProfiles", "Enable App Profiles (Virtual Layer)" }, { "HeaderProfileName", "Profile Name" }, { "HeaderProcessName", "Process Name (.exe)" },
                { "ActionMicMute", "Mic Mute (Toggle)" },
                { "LblPaletteFontSize", "Palette Font Size:" }
            };
            data.Languages.Add(en);

            // 3. French (FR)
            var fr = new LanguageProfile { Code = "FR", Name = "Français" };
            fr.Strings = new Dictionary<string, string>
            {
                { "Title", "Paramètres OSD SayoDevice" },
                { "GrpDevice", "Paramètres du périphérique" },
                { "BtnScan", "Scanner" }, { "BtnApply", "Appliquer" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* Détecte uniquement SayoDevice." },
                { "GrpOsd", "Paramètres OSD" },
                { "LblOpacity", "Opacité:" }, { "LblTimeout", "Temps(s):" }, { "LblMode", "Mode:" },
                { "ModeAuto", "Auto (Sur entrée)" }, { "ModeOn", "Toujours activé" }, { "ModeOff", "Toujours désactivé" },
                { "ChkMoveOsd", "Déplacer l'OSD" }, { "BtnResetSize", "Réinit. taille" },
                { "ChkVertical", "Mode Vertical" }, { "ChkSwapRows", "Échanger les rangées" },
                { "GrpSystemSettings", "Paramètres Système" },
                { "GrpAudio", "Paramètres Audio" },
                { "GrpMap", "Mappage des touches" },
                { "ColTime", "Temps" }, { "ColKey", "Clé(Hex)" }, { "ColData", "Données" }, { "MnuCopy", "Copier" },
                { "ChkPauseLog", "Pause Log" },
                { "LblLayer", "Couche:" }, { "LblSlot", "Slot:" }, { "LblName", "Nom:" }, { "BtnRename", "Renommer" },
                { "LblTarget", "Fonction:" }, { "TargetNone", "Aucun" }, { "BtnManualDetect", "Détection manuelle" },
                { "ActionRun", "Lancer le programme..." },
                { "ActionTextMacro", "Macro texte (Entrée auto)" }, { "ActionAudioCycle", "Cycle périphérique audio" },
                { "TitleInputText", "Entrer la macro texte" }, { "MsgEnterText", "Entrez le texte pour la macro.\n(ex: Bonjour{ENTER})" },
                { "CtxDeleteMacro", "Supprimer la macro (Démapper tout)" }, { "TitleDeleteMacro", "Supprimer la macro" }, { "MsgDeleteMacroConfirm", "Supprimer la macro '{0}' ?\nCela la démappera de toutes les touches." },
                { "ActionActiveVolUp", "Vol Fenêtre Active +" },
                { "ActionActiveVolDown", "Vol Fenêtre Active -" },
                { "LblVolumeStep", "Pas de volume:" },
                { "HeaderMedia", "Contrôle Média" },
                { "ActionMediaPlayPause", "Lecture / Pause" }, { "ActionMediaNext", "Piste suivante" }, { "ActionMediaPrev", "Piste précédente" },
                { "ActionVolUp", "Volume +" }, { "ActionVolDown", "Volume -" }, { "ActionVolMute", "Muet (Système)" },
                { "HeaderLayerMove", "Choisir la couche" },
                { "ActionOsdCycle", "Changer mode OSD" }, { "ModeBottom", "Arrière-plan" },
                { "LblSignal", "Signal:" }, { "BtnAutoDetect", "Détection auto" }, { "BtnUnmap", "Démapper" },
                { "ChkEnableFileLog", "Enreg. fichier" }, { "ChkStartWithWindows", "Démarrer avec Windows" },
                { "BtnSave", "Enreg. paramètres" }, { "BtnHide", "Cacher" }, { "BtnOpenSettings", "Paramètres" },
                { "MsgNameChanged", "Nom changé." },
                { "MsgSelectSlot", "Sélectionnez un slot." },
                { "MsgDetecting", "Détection..." },
                { "MsgSelectSignal", "Sélectionnez un signal." },
                { "MsgUnmapConfirm", "Réinitialiser ce mappage ?" },
                { "MsgUnmapped", "Démappé." },
                { "MsgPatternMapped", "Modèle mappé." },
                { "MsgVidApplied", "VID/PID Appliqué." },
                { "MsgSettingsSaved", "Paramètres enregistrés." },
                { "MsgSaved", "Enregistré" },
                { "TitleSelectSignal", "Sélectionner le signal" },
                { "TitleUnmap", "Démapper" },
                { "BtnLicense", "Licences Open Source (NAudio, HidSharp)" },
                { "TitleLicense", "Licences Open Source" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "Mode d'affichage OSD" },
                { "CtxMoveOsd", "Déplacer l'OSD" },
                { "CtxExit", "Quitter" },
                { "MsgDetectionDone", "Détection terminée (Sélectionner)" },
                { "GrpFunctions", "Fonctions" }, { "LblPath", "Chemin:" }, { "LblArgs", "Arguments:" },
                { "LblIcon", "Icône:" }, { "BtnBrowse", "Parcourir..." }, { "BtnChange", "Changer" },
                { "BtnSavePanel", "Enregistrer" }, { "BtnCancelPanel", "Annuler" },
                { "HeaderSystem", "Système" }, { "HeaderAction", "Raccourcis / Exécuter" },
                { "MsgPatternMappedDetail", "Modèle mappé sur la touche {0}.\nModèle : {1}" },
                { "MsgConfirmRunProgram", "Enregistrer '{1}' sur la touche {0} ?" }, { "TitleRunProgram", "Enregistrer le programme" },
                { "MsgNeedMapping", "Cette touche n'est pas encore mappée.\nLa mapper maintenant ?" }, { "TitleNeedMapping", "Mappage requis" },
                { "MsgUnmapConfirmDetail", "Réinitialiser le mappage pour la touche {0} (Couche {1}) ?" },
                { "MsgStartupRegistered", "Démarrage enregistré avec droits admin.\nS'exécutera en arrière-plan." }, { "TitleStartupRegistered", "Démarrage enregistré" },
                { "MsgStartupFailed", "Échec config démarrage : {0}" },
                { "ChkUseClipboard", "Copier dans le presse-papiers" },
                { "TabGeneral", "Général" },
                { "TabProfiles", "Profils d'application" },
                { "ActionProfileCycle", "Cycle de profils" },
                { "MsgLanguageUpdated", "Fichier de langue mis à jour.\nVos modifications sont conservées." },
                { "BtnAddProfile", "Ajouter" }, { "BtnDeleteProfile", "Supprimer" }, { "BtnExportProfile", "Exporter" }, { "BtnImportProfile", "Importer" },
                { "MsgProcessExists", "Processus déjà enregistré." }, { "MsgDeleteProfileConfirm", "Supprimer le profil '{0}' ?" }, { "TitleDeleteProfile", "Confirmation de suppression" },
                { "TitleExportProfile", "Exporter le profil" }, { "MsgExportSuccess", "Profil exporté avec succès." }, { "TitleExportSuccess", "Exportation terminée" }, { "MsgExportFailed", "Échec de l'exportation : {0}" }, { "TitleError", "Erreur" }, { "MsgSelectProfile", "Veuillez sélectionner un profil à exporter." },
                { "TitleImportProfile", "Importer le profil" }, { "MsgInvalidProfile", "Format de fichier de profil invalide." }, { "MsgProfileExists", "Le profil '{0}' existe déjà.\nÉcraser ?" }, { "TitleDuplicate", "Vérification de doublon" }, { "MsgImportSuccess", "Profil importé avec succès." }, { "TitleImportSuccess", "Importation terminée" }, { "MsgImportFailed", "Échec de l'importation : {0}" },
                { "MsgResetColorConfirm", "Réinitialiser les couleurs OSD pour '{0}' ?" }, { "TitleResetColor", "Réinitialiser les couleurs" },
                { "GrpColor", "Couleurs OSD" }, { "GrpFont", "Polices OSD" }, { "LblTargetSettings", "Cible :" }, { "ItemGlobal", "Paramètres globaux" },
                { "BtnBgColor", "Arrière-plan" }, { "BtnHighlightColor", "Surbrillance" }, { "BtnBorderColor", "Bordure" }, { "BtnResetColor", "Réinit." }, { "BtnResetFont", "Réinit." },
                { "LblFallbackProfile", "Profil par défaut :" }, { "LblFontFamily", "Police :" }, { "LblFontSize", "Taille :" }, { "LblFontWeight", "Graisse :" },
                { "ChkEnableAppProfiles", "Activer les profils d'application" }, { "HeaderProfileName", "Nom du profil" }, { "HeaderProcessName", "Nom du processus (.exe)" },
                { "ActionMicMute", "Muet Micro (Bascule)" },
                { "LblPaletteFontSize", "Taille police palette :" }
            };
            data.Languages.Add(fr);

            // 4. Spanish (ES)
            var es = new LanguageProfile { Code = "ES", Name = "Español" };
            es.Strings = new Dictionary<string, string>
            {
                { "Title", "Configuración OSD SayoDevice" },
                { "GrpDevice", "Configuración del dispositivo" },
                { "BtnScan", "Escanear" }, { "BtnApply", "Aplicar" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* Solo detecta SayoDevice." },
                { "GrpOsd", "Configuración OSD" },
                { "LblOpacity", "Opacidad:" }, { "LblTimeout", "Tiempo(s):" }, { "LblMode", "Modo:" },
                { "ModeAuto", "Auto (Al entrar)" }, { "ModeOn", "Siempre encendido" }, { "ModeOff", "Siempre apagado" },
                { "ChkMoveOsd", "Mover OSD" }, { "BtnResetSize", "Restablecer tamaño" },
                { "ChkVertical", "Modo Vertical" }, { "ChkSwapRows", "Intercambiar filas" },
                { "GrpSystemSettings", "Configuración del Sistema" },
                { "GrpAudio", "Configuración de Audio" },
                { "GrpMap", "Mapeo de teclas" },
                { "ColTime", "Tiempo" }, { "ColKey", "Clave(Hex)" }, { "ColData", "Datos" }, { "MnuCopy", "Copiar" },
                { "ChkPauseLog", "Pausar registro" },
                { "LblLayer", "Capa:" }, { "LblSlot", "Ranura:" }, { "LblName", "Nombre:" }, { "BtnRename", "Renombrar" },
                { "LblTarget", "Función:" }, { "TargetNone", "Ninguno" }, { "BtnManualDetect", "Detección manual" },
                { "ActionRun", "Ejecutar programa..." },
                { "ActionTextMacro", "Macro de texto (Auto)" }, { "ActionAudioCycle", "Ciclo de dispositivo de audio" },
                { "TitleInputText", "Ingresar macro de texto" }, { "MsgEnterText", "Ingrese el texto para la macro.\n(ej: Hola{ENTER})" },
                { "CtxDeleteMacro", "Eliminar macro (Desasignar todo)" }, { "TitleDeleteMacro", "Eliminar macro" }, { "MsgDeleteMacroConfirm", "¿Eliminar la macro '{0}'?\nSe desasignará de todas las teclas." },
                { "ActionActiveVolUp", "Vol Ventana Activa +" },
                { "ActionActiveVolDown", "Vol Ventana Activa -" },
                { "LblVolumeStep", "Paso de volumen:" },
                { "HeaderMedia", "Control Multimedia" },
                { "ActionMediaPlayPause", "Reproducir / Pausa" }, { "ActionMediaNext", "Siguiente pista" }, { "ActionMediaPrev", "Pista anterior" },
                { "ActionVolUp", "Subir volumen" }, { "ActionVolDown", "Bajar volumen" }, { "ActionVolMute", "Silenciar (Sistema)" },
                { "HeaderLayerMove", "Seleccionar capa" },
                { "ActionOsdCycle", "Cambiar modo OSD" }, { "ModeBottom", "Fondo" },
                { "LblSignal", "Señal:" }, { "BtnAutoDetect", "Detección auto" }, { "BtnUnmap", "Desasignar" },
                { "ChkEnableFileLog", "Guardar registro" }, { "ChkStartWithWindows", "Iniciar con Windows" },
                { "BtnSave", "Guardar config." }, { "BtnHide", "Ocultar" }, { "BtnOpenSettings", "Configuración" },
                { "MsgNameChanged", "Nombre cambiado." },
                { "MsgSelectSlot", "Seleccione una ranura." },
                { "MsgDetecting", "Detectando..." },
                { "MsgSelectSignal", "Seleccione una señal." },
                { "MsgUnmapConfirm", "¿Restablecer mapeo?" },
                { "MsgUnmapped", "Desasignado." },
                { "MsgPatternMapped", "Patrón mapeado." },
                { "MsgVidApplied", "VID/PID Aplicado." },
                { "MsgSettingsSaved", "Configuración guardada." },
                { "MsgSaved", "Guardado" },
                { "TitleSelectSignal", "Seleccionar señal" },
                { "TitleUnmap", "Desasignar" },
                { "BtnLicense", "Licencias de código abierto (NAudio, HidSharp)" },
                { "TitleLicense", "Licencias de código abierto" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "Modo de visualización OSD" },
                { "CtxMoveOsd", "Mover OSD" },
                { "CtxExit", "Salir" },
                { "MsgDetectionDone", "Detección completa (Seleccionar)" },
                { "GrpFunctions", "Funciones" }, { "LblPath", "Ruta:" }, { "LblArgs", "Argumentos:" },
                { "LblIcon", "Icono:" }, { "BtnBrowse", "Examinar..." }, { "BtnChange", "Cambiar" },
                { "BtnSavePanel", "Guardar" }, { "BtnCancelPanel", "Cancelar" },
                { "HeaderSystem", "Sistema" }, { "HeaderAction", "Atajos / Ejecutar" },
                { "MsgPatternMappedDetail", "Patrón asignado a Tecla {0}.\nPatrón: {1}" },
                { "MsgConfirmRunProgram", "¿Registrar '{1}' en Tecla {0}?" }, { "TitleRunProgram", "Registrar Programa" },
                { "MsgNeedMapping", "Esta tecla no está asignada aún.\n¿Asignarla ahora?" }, { "TitleNeedMapping", "Asignación Requerida" },
                { "MsgUnmapConfirmDetail", "¿Restablecer asignación para Tecla {0} (Capa {1})?" },
                { "MsgStartupRegistered", "Inicio registrado con permisos de admin.\nSe ejecutará en segundo plano." }, { "TitleStartupRegistered", "Inicio Registrado" },
                { "MsgStartupFailed", "Fallo config inicio: {0}" },
                { "ChkUseClipboard", "Copiar al portapapeles" },
                { "TabGeneral", "General" },
                { "TabProfiles", "Perfiles de aplicación" },
                { "ActionProfileCycle", "Ciclo de perfiles" },
                { "MsgLanguageUpdated", "Archivo de idioma actualizado.\nSus cambios se conservan." },
                { "BtnAddProfile", "Añadir" }, { "BtnDeleteProfile", "Eliminar" }, { "BtnExportProfile", "Exportar" }, { "BtnImportProfile", "Importar" },
                { "MsgProcessExists", "Proceso ya registrado." }, { "MsgDeleteProfileConfirm", "¿Eliminar perfil '{0}'?" }, { "TitleDeleteProfile", "Confirmación de eliminación" },
                { "TitleExportProfile", "Exportar perfil" }, { "MsgExportSuccess", "Perfil exportado con éxito." }, { "TitleExportSuccess", "Exportación completa" }, { "MsgExportFailed", "Fallo al exportar: {0}" }, { "TitleError", "Error" }, { "MsgSelectProfile", "Seleccione un perfil para exportar." },
                { "TitleImportProfile", "Importar perfil" }, { "MsgInvalidProfile", "Formato de archivo de perfil no válido." }, { "MsgProfileExists", "El perfil '{0}' ya existe.\n¿Sobrescribir?" }, { "TitleDuplicate", "Verificación de duplicados" }, { "MsgImportSuccess", "Perfil importado con éxito." }, { "TitleImportSuccess", "Importación completa" }, { "MsgImportFailed", "Fallo al importar: {0}" },
                { "MsgResetColorConfirm", "¿Restablecer colores OSD para '{0}'?" }, { "TitleResetColor", "Restablecer colores" },
                { "GrpColor", "Colores OSD" }, { "GrpFont", "Fuentes OSD" }, { "LblTargetSettings", "Objetivo:" }, { "ItemGlobal", "Configuración global" },
                { "BtnBgColor", "Fondo" }, { "BtnHighlightColor", "Resaltado" }, { "BtnBorderColor", "Borde" }, { "BtnResetColor", "Restablecer" }, { "BtnResetFont", "Restablecer" },
                { "LblFallbackProfile", "Perfil predeterminado:" }, { "LblFontFamily", "Fuente:" }, { "LblFontSize", "Tamaño:" }, { "LblFontWeight", "Peso:" },
                { "ChkEnableAppProfiles", "Habilitar perfiles de aplicación" }, { "HeaderProfileName", "Nombre del perfil" }, { "HeaderProcessName", "Nombre del proceso (.exe)" },
                { "ActionMicMute", "Silenciar Micro (Alternar)" },
                { "LblPaletteFontSize", "Tamaño fuente paleta:" }
            };
            data.Languages.Add(es);

            // 5. Chinese (CN)
            var cn = new LanguageProfile { Code = "CN", Name = "中文" };
            cn.Strings = new Dictionary<string, string>
            {
                { "Title", "SayoDevice OSD 设置" },
                { "GrpDevice", "设备设置 (查看设备管理器)" },
                { "BtnScan", "扫描" }, { "BtnApply", "应用" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* 仅检测 SayoDevice" },
                { "GrpOsd", "OSD 设置" },
                { "LblOpacity", "透明度:" }, { "LblTimeout", "显示时间(秒):" }, { "LblMode", "显示模式:" },
                { "ModeAuto", "自动 (输入时)" }, { "ModeOn", "常开" }, { "ModeOff", "常关" },
                { "ChkMoveOsd", "允许移动 OSD" }, { "BtnResetSize", "重置大小" },
                { "ChkVertical", "垂直模式" }, { "ChkSwapRows", "交换行 (1/7)" },
                { "GrpSystemSettings", "系统设置" },
                { "GrpAudio", "音频设置" },
                { "GrpMap", "按键映射设置" },
                { "ColTime", "时间" }, { "ColKey", "键值(Hex)" }, { "ColData", "数据" }, { "MnuCopy", "复制" },
                { "ChkPauseLog", "暂停日志" },
                { "LblLayer", "层:" }, { "LblSlot", "槽:" }, { "LblName", "名称:" }, { "BtnRename", "重命名" },
                { "LblTarget", "功能:" }, { "TargetNone", "无" }, { "BtnManualDetect", "手动检测" },
                { "ActionRun", "运行程序..." },
                { "ActionTextMacro", "文本宏 (自动输入)" }, { "ActionAudioCycle", "切换音频设备" },
                { "TitleInputText", "输入文本宏" }, { "MsgEnterText", "输入宏文本。\n(例如: 你好{ENTER})" },
                { "CtxDeleteMacro", "删除宏 (全部取消)" }, { "TitleDeleteMacro", "删除宏" }, { "MsgDeleteMacroConfirm", "删除宏 '{0}'?\n这将取消所有按键的映射。" },
                { "ActionActiveVolUp", "活动窗口音量 +" },
                { "ActionActiveVolDown", "活动窗口音量 -" },
                { "LblVolumeStep", "音量步长:" },
                { "HeaderMedia", "媒体控制" },
                { "ActionMediaPlayPause", "播放 / 暂停" }, { "ActionMediaNext", "下一首" }, { "ActionMediaPrev", "上一首" },
                { "ActionVolUp", "音量 +" }, { "ActionVolDown", "音量 -" }, { "ActionVolMute", "静音 (系统)" },
                { "HeaderLayerMove", "选择层级" },
                { "ActionOsdCycle", "切换 OSD 模式" }, { "ModeBottom", "最底层" },
                { "LblSignal", "信号:" }, { "BtnAutoDetect", "自动检测" }, { "BtnUnmap", "取消映射" },
                { "ChkEnableFileLog", "保存日志文件" }, { "ChkStartWithWindows", "开机自启" },
                { "BtnSave", "保存设置" }, { "BtnHide", "隐藏到托盘" }, { "BtnOpenSettings", "设置" },
                { "MsgNameChanged", "名称已更改。" },
                { "MsgSelectSlot", "请先选择一个槽位。" },
                { "MsgDetecting", "检测中..." },
                { "MsgSelectSignal", "请从列表中选择信号。" },
                { "MsgUnmapConfirm", "确定要重置此按键映射吗？" },
                { "MsgUnmapped", "映射已取消。" },
                { "MsgPatternMapped", "模式已映射。" },
                { "MsgVidApplied", "VID/PID 已应用。" },
                { "MsgSettingsSaved", "设置已保存。" },
                { "MsgSaved", "已保存" },
                { "TitleSelectSignal", "选择信号 (按键)" },
                { "TitleUnmap", "取消映射" },
                { "BtnLicense", "开源许可证 (NAudio, HidSharp)" },
                { "TitleLicense", "开源许可证" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "OSD 显示模式" },
                { "CtxMoveOsd", "移动 OSD" },
                { "CtxExit", "退出" },
                { "MsgDetectionDone", "检测完成 (请选择)" },
                { "GrpFunctions", "功能" }, { "LblPath", "路径:" }, { "LblArgs", "参数:" },
                { "LblIcon", "图标:" }, { "BtnBrowse", "浏览..." }, { "BtnChange", "更改" },
                { "BtnSavePanel", "保存" }, { "BtnCancelPanel", "取消" },
                { "HeaderSystem", "系统" }, { "HeaderAction", "快捷键 / 运行" },
                { "MsgPatternMappedDetail", "模式已映射到按键 {0}。\n模式: {1}" },
                { "MsgConfirmRunProgram", "将 '{1}' 注册到按键 {0}？" }, { "TitleRunProgram", "注册程序" },
                { "MsgNeedMapping", "此按键尚未映射到硬件按钮。\n现在映射吗？" }, { "TitleNeedMapping", "需要映射" },
                { "MsgUnmapConfirmDetail", "重置按键 {0} (层 {1}) 的映射？" },
                { "MsgStartupRegistered", "已使用管理员权限注册启动。\n登录时将在后台运行。" }, { "TitleStartupRegistered", "启动已注册" },
                { "MsgStartupFailed", "启动设置失败: {0}" },
                { "ChkUseClipboard", "复制到剪贴板" },
                { "TabGeneral", "常规设置" },
                { "TabProfiles", "应用配置文件" },
                { "ActionProfileCycle", "循环切换配置文件" },
                { "MsgLanguageUpdated", "语言文件已更新至最新版本。\n您的更改已保留。" },
                { "BtnAddProfile", "添加" }, { "BtnDeleteProfile", "删除" }, { "BtnExportProfile", "导出" }, { "BtnImportProfile", "导入" },
                { "MsgProcessExists", "进程已注册。" }, { "MsgDeleteProfileConfirm", "删除配置文件 '{0}'?" }, { "TitleDeleteProfile", "删除确认" },
                { "TitleExportProfile", "导出配置文件" }, { "MsgExportSuccess", "配置文件导出成功。" }, { "TitleExportSuccess", "导出完成" }, { "MsgExportFailed", "导出失败: {0}" }, { "TitleError", "错误" }, { "MsgSelectProfile", "请选择要导出的配置文件。" },
                { "TitleImportProfile", "导入配置文件" }, { "MsgInvalidProfile", "无效的配置文件格式。" }, { "MsgProfileExists", "配置文件 '{0}' 已存在。\n覆盖吗？" }, { "TitleDuplicate", "重复检查" }, { "MsgImportSuccess", "配置文件导入成功。" }, { "TitleImportSuccess", "导入完成" }, { "MsgImportFailed", "导入失败: {0}" },
                { "MsgResetColorConfirm", "重置 '{0}' 的 OSD 颜色？" }, { "TitleResetColor", "重置颜色" },
                { "GrpColor", "OSD 颜色" }, { "GrpFont", "OSD 字体" }, { "LblTargetSettings", "目标:" }, { "ItemGlobal", "全局设置" },
                { "BtnBgColor", "背景色" }, { "BtnHighlightColor", "高亮色" }, { "BtnBorderColor", "边框色" }, { "BtnResetColor", "重置" }, { "BtnResetFont", "重置" },
                { "LblFallbackProfile", "默认配置文件:" }, { "LblFontFamily", "字体:" }, { "LblFontSize", "大小:" }, { "LblFontWeight", "粗细:" },
                { "ChkEnableAppProfiles", "启用应用配置文件" }, { "HeaderProfileName", "配置文件名称" }, { "HeaderProcessName", "进程名称 (.exe)" },
                { "ActionMicMute", "麦克风静音 (切换)" },
                { "LblPaletteFontSize", "功能面板字体大小:" }
            };
            data.Languages.Add(cn);

            // 6. German (DE)
            var de = new LanguageProfile { Code = "DE", Name = "Deutsch" };
            de.Strings = new Dictionary<string, string>
            {
                { "Title", "SayoDevice OSD-Einstellungen" },
                { "GrpDevice", "Geräteeinstellungen" },
                { "BtnScan", "Scannen" }, { "BtnApply", "Anwenden" },
                { "LblVid", "VID:" }, { "LblPid", "PID:" }, { "MsgSayoOnly", "* Nur SayoDevice" },
                { "GrpOsd", "OSD-Einstellungen" },
                { "LblOpacity", "Deckkraft:" }, { "LblTimeout", "Zeit(s):" }, { "LblMode", "Modus:" },
                { "ModeAuto", "Auto (Bei Eingabe)" }, { "ModeOn", "Immer an" }, { "ModeOff", "Immer aus" },
                { "ChkMoveOsd", "OSD verschieben" }, { "BtnResetSize", "Größe zurücksetzen" },
                { "ChkVertical", "Vertikaler Modus" }, { "ChkSwapRows", "Zeilen tauschen" },
                { "GrpSystemSettings", "Systemeinstellungen" },
                { "GrpAudio", "Audioeinstellungen" },
                { "GrpMap", "Tastenbelegung" },
                { "ColTime", "Zeit" }, { "ColKey", "Taste(Hex)" }, { "ColData", "Daten" }, { "MnuCopy", "Kopieren" },
                { "ChkPauseLog", "Log pausieren" },
                { "LblLayer", "Ebene:" }, { "LblSlot", "Slot:" }, { "LblName", "Name:" }, { "BtnRename", "Umbenennen" },
                { "LblTarget", "Funktion:" }, { "TargetNone", "Keine" }, { "BtnManualDetect", "Manuelle Erkennung" },
                { "ActionRun", "Programm ausführen..." },
                { "ActionTextMacro", "Textmakro (Auto-Eingabe)" }, { "ActionAudioCycle", "Audiogerät wechseln" },
                { "TitleInputText", "Textmakro eingeben" }, { "MsgEnterText", "Text für Makro eingeben.\n(z.B. Hallo{ENTER})" },
                { "CtxDeleteMacro", "Makro löschen (Alle aufheben)" }, { "TitleDeleteMacro", "Makro löschen" }, { "MsgDeleteMacroConfirm", "Makro '{0}' löschen?\nDies hebt die Zuordnung von allen Tasten auf." },
                { "ActionActiveVolUp", "Aktives Fenster Vol +" },
                { "ActionActiveVolDown", "Aktives Fenster Vol -" },
                { "LblVolumeStep", "Lautstärkeschritt:" },
                { "HeaderMedia", "Mediensteuerung" },
                { "ActionMediaPlayPause", "Wiedergabe / Pause" }, { "ActionMediaNext", "Nächster Titel" }, { "ActionMediaPrev", "Vorheriger Titel" },
                { "ActionVolUp", "Lautstärke +" }, { "ActionVolDown", "Lautstärke -" }, { "ActionVolMute", "Stummschalten (System)" },
                { "HeaderLayerMove", "Ebene wählen" },
                { "ActionOsdCycle", "OSD-Modus wechseln" }, { "ModeBottom", "Hintergrund" },
                { "LblSignal", "Signal:" }, { "BtnAutoDetect", "Auto-Erkennung" }, { "BtnUnmap", "Löschen" },
                { "ChkEnableFileLog", "Log speichern" }, { "ChkStartWithWindows", "Mit Windows starten" },
                { "BtnSave", "Speichern" }, { "BtnHide", "In Tray minimieren" }, { "BtnOpenSettings", "Einstellungen" },
                { "MsgNameChanged", "Name geändert." },
                { "MsgSelectSlot", "Bitte Slot wählen." },
                { "MsgDetecting", "Erkennen..." },
                { "MsgSelectSignal", "Bitte Signal wählen." },
                { "MsgUnmapConfirm", "Zuordnung zurücksetzen?" },
                { "MsgUnmapped", "Gelöscht." },
                { "MsgPatternMapped", "Zugeordnet." },
                { "MsgVidApplied", "VID/PID angewendet." },
                { "MsgSettingsSaved", "Gespeichert." },
                { "MsgSaved", "Gespeichert" },
                { "TitleSelectSignal", "Signal wählen" },
                { "TitleUnmap", "Löschen" },
                { "BtnLicense", "Open-Source-Lizenzen (NAudio, HidSharp)" },
                { "TitleLicense", "Open-Source-Lizenzen" },
                { "LicenseText", NAudioLicense + HidSharpLicense },
                { "CtxOsdMode", "OSD-Anzeigemodus" },
                { "CtxMoveOsd", "OSD verschieben" },
                { "CtxExit", "Beenden" },
                { "MsgDetectionDone", "Erkennung abgeschlossen (Auswählen)" },
                { "GrpFunctions", "Funktionen" }, { "LblPath", "Pfad:" }, { "LblArgs", "Argumente:" },
                { "LblIcon", "Icon:" }, { "BtnBrowse", "Durchsuchen..." }, { "BtnChange", "Ändern" },
                { "BtnSavePanel", "Speichern" }, { "BtnCancelPanel", "Abbrechen" },
                { "HeaderSystem", "System" }, { "HeaderAction", "Shortcuts / Ausführen" },
                { "MsgPatternMappedDetail", "Muster auf Taste {0} gemappt.\nMuster: {1}" },
                { "MsgConfirmRunProgram", "'{1}' auf Taste {0} registrieren?" }, { "TitleRunProgram", "Programm registrieren" },
                { "MsgNeedMapping", "Diese Taste ist noch nicht zugewiesen.\nJetzt zuweisen?" }, { "TitleNeedMapping", "Zuweisung erforderlich" },
                { "MsgUnmapConfirmDetail", "Zuordnung für Taste {0} (Ebene {1}) zurücksetzen?" },
                { "MsgStartupRegistered", "Autostart mit Admin-Rechten registriert.\nLäuft beim Login im Hintergrund." }, { "TitleStartupRegistered", "Autostart registriert" },
                { "MsgStartupFailed", "Autostart-Fehler: {0}" },
                { "ChkUseClipboard", "In Zwischenablage kopieren" },
                { "TabGeneral", "Allgemein" },
                { "TabProfiles", "App-Profile" },
                { "ActionProfileCycle", "Profile durchwechseln" },
                { "MsgLanguageUpdated", "Sprachdatei auf die neueste Version aktualisiert.\nIhre Änderungen bleiben erhalten." },
                { "BtnAddProfile", "Hinzufügen" }, { "BtnDeleteProfile", "Löschen" }, { "BtnExportProfile", "Exportieren" }, { "BtnImportProfile", "Importieren" },
                { "MsgProcessExists", "Prozess bereits registriert." }, { "MsgDeleteProfileConfirm", "Profil '{0}' löschen?" }, { "TitleDeleteProfile", "Löschbestätigung" },
                { "TitleExportProfile", "Profil exportieren" }, { "MsgExportSuccess", "Profil erfolgreich exportiert." }, { "TitleExportSuccess", "Export abgeschlossen" }, { "MsgExportFailed", "Export fehlgeschlagen: {0}" }, { "TitleError", "Fehler" }, { "MsgSelectProfile", "Bitte wählen Sie ein Profil zum Exportieren." },
                { "TitleImportProfile", "Profil importieren" }, { "MsgInvalidProfile", "Ungültiges Profil-Dateiformat." }, { "MsgProfileExists", "Profil '{0}' existiert bereits.\nÜberschreiben?" }, { "TitleDuplicate", "Duplikatprüfung" }, { "MsgImportSuccess", "Profil erfolgreich importiert." }, { "TitleImportSuccess", "Import abgeschlossen" }, { "MsgImportFailed", "Import fehlgeschlagen: {0}" },
                { "MsgResetColorConfirm", "OSD-Farben für '{0}' zurücksetzen?" }, { "TitleResetColor", "Farben zurücksetzen" },
                { "GrpColor", "OSD-Farben" }, { "GrpFont", "OSD-Schriftarten" }, { "LblTargetSettings", "Ziel:" }, { "ItemGlobal", "Globale Einstellungen" },
                { "BtnBgColor", "Hintergrund" }, { "BtnHighlightColor", "Hervorhebung" }, { "BtnBorderColor", "Rand" }, { "BtnResetColor", "Zurücksetzen" }, { "BtnResetFont", "Zurücksetzen" },
                { "LblFallbackProfile", "Standardprofil:" }, { "LblFontFamily", "Schriftart:" }, { "LblFontSize", "Größe:" }, { "LblFontWeight", "Gewicht:" },
                { "ChkEnableAppProfiles", "App-Profile aktivieren" }, { "HeaderProfileName", "Profilname" }, { "HeaderProcessName", "Prozessname (.exe)" },
                { "ActionMicMute", "Mikrofon stumm (Umschalten)" },
                { "LblPaletteFontSize", "Paletten-Schriftgröße:" }
            };
            data.Languages.Add(de);

            return data;
        }
    }
}