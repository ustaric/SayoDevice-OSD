using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SayoOSD
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
        public List<LanguageProfile> Languages { get; set; } = new List<LanguageProfile>();
    }

    public static class LanguageManager
    {
        private static LocalizationData _data;
        private static string _filePath = "languages.json";
        
        // UI에서 사용할 키 목록 (기준)
        public static readonly List<string> Keys = new List<string>
        {
            "Title", "GrpDevice", "BtnScan", "BtnApply", "LblVid", "LblPid", "MsgSayoOnly",
            "GrpOsd", "LblOpacity", "LblTimeout", "LblMode", "ModeAuto", "ModeOn", "ModeOff",
            "ChkMoveOsd", "BtnResetSize",
            "GrpMap", "ColTime", "ColKey", "ColData", "MnuCopy",
            "ChkPauseLog", "LblLayer", "LblSlot", "LblName", "BtnRename",
            "LblTarget", "TargetNone", "LblSignal", "BtnAutoDetect", "BtnUnmap",
            "ChkEnableFileLog", "ChkStartWithWindows", "BtnSave", "BtnHide",
            "MsgNameChanged", "MsgSelectSlot", "MsgDetecting", "MsgSelectSignal",
            "MsgUnmapConfirm", "MsgUnmapped", "MsgPatternMapped", "MsgVidApplied", "MsgSettingsSaved",
            "TitleSelectSignal", "TitleUnmap"
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

            if (_data == null)
            {
                _data = defaults;
                Save();
            }
            else
            {
                // 기존 파일이 있더라도, 코드에 새로 추가된 키나 언어가 누락되어 있다면 병합합니다.
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

                if (changed) Save();
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

        private static LocalizationData GetDefaultData()
        {
            var data = new LocalizationData();

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
                { "GrpMap", "키 매핑 설정 (settings.json 직접 수정 권장)" },
                { "ColTime", "시간" }, { "ColKey", "키값(Hex)" }, { "ColData", "전체 데이터" }, { "MnuCopy", "복사" },
                { "ChkPauseLog", "로그 일시정지" },
                { "LblLayer", "레이어:" }, { "LblSlot", "슬롯:" }, { "LblName", "이름:" }, { "BtnRename", "이름변경" },
                { "LblTarget", "이동:" }, { "TargetNone", "이동 없음" },
                { "LblSignal", "선택한 신호:" }, { "BtnAutoDetect", "자동 감지" }, { "BtnUnmap", "매핑해제" },
                { "ChkEnableFileLog", "로그 파일 저장" }, { "ChkStartWithWindows", "윈도우 시작 시 자동 실행" },
                { "BtnSave", "설정 저장" }, { "BtnHide", "트레이로 숨기기" },
                { "MsgNameChanged", "이름이 변경되었습니다." },
                { "MsgSelectSlot", "매핑할 슬롯을 먼저 선택해주세요." },
                { "MsgDetecting", "감지 중..." },
                { "MsgSelectSignal", "목록에서 신호를 선택해주세요." },
                { "MsgUnmapConfirm", "매핑 정보를 초기화하시겠습니까?" },
                { "MsgUnmapped", "매핑이 해제되었습니다." },
                { "MsgPatternMapped", "패턴이 매핑되었습니다." },
                { "MsgVidApplied", "VID/PID가 적용되었습니다." },
                { "MsgSettingsSaved", "설정이 저장되었습니다." },
                { "TitleSelectSignal", "신호 선택 (키를 누르세요)" },
                { "TitleUnmap", "매핑 해제" }
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
                { "GrpMap", "Key Mapping (Edit settings.json recommended)" },
                { "ColTime", "Time" }, { "ColKey", "Key(Hex)" }, { "ColData", "Full Data" }, { "MnuCopy", "Copy" },
                { "ChkPauseLog", "Pause Log" },
                { "LblLayer", "Layer:" }, { "LblSlot", "Slot:" }, { "LblName", "Name:" }, { "BtnRename", "Rename" },
                { "LblTarget", "Target:" }, { "TargetNone", "No Move" },
                { "LblSignal", "Selected Signal:" }, { "BtnAutoDetect", "Auto Detect" }, { "BtnUnmap", "Unmap" },
                { "ChkEnableFileLog", "Save Log to File" }, { "ChkStartWithWindows", "Run on Startup" },
                { "BtnSave", "Save Settings" }, { "BtnHide", "Hide to Tray" },
                { "MsgNameChanged", "Name changed." },
                { "MsgSelectSlot", "Please select a slot to map." },
                { "MsgDetecting", "Detecting..." },
                { "MsgSelectSignal", "Please select a signal from the list." },
                { "MsgUnmapConfirm", "Reset mapping for this key?" },
                { "MsgUnmapped", "Unmapped." },
                { "MsgPatternMapped", "Pattern mapped." },
                { "MsgVidApplied", "VID/PID Applied." },
                { "MsgSettingsSaved", "Settings saved." },
                { "TitleSelectSignal", "Select Signal (Press Key)" },
                { "TitleUnmap", "Unmap" }
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
                { "GrpMap", "Mappage des touches" },
                { "ColTime", "Temps" }, { "ColKey", "Clé(Hex)" }, { "ColData", "Données" }, { "MnuCopy", "Copier" },
                { "ChkPauseLog", "Pause Log" },
                { "LblLayer", "Couche:" }, { "LblSlot", "Slot:" }, { "LblName", "Nom:" }, { "BtnRename", "Renommer" },
                { "LblTarget", "Cible:" }, { "TargetNone", "Aucun" },
                { "LblSignal", "Signal:" }, { "BtnAutoDetect", "Détection auto" }, { "BtnUnmap", "Démapper" },
                { "ChkEnableFileLog", "Enreg. fichier" }, { "ChkStartWithWindows", "Démarrer avec Windows" },
                { "BtnSave", "Enreg. paramètres" }, { "BtnHide", "Cacher" },
                { "MsgNameChanged", "Nom changé." },
                { "MsgSelectSlot", "Sélectionnez un slot." },
                { "MsgDetecting", "Détection..." },
                { "MsgSelectSignal", "Sélectionnez un signal." },
                { "MsgUnmapConfirm", "Réinitialiser ce mappage ?" },
                { "MsgUnmapped", "Démappé." },
                { "MsgPatternMapped", "Modèle mappé." },
                { "MsgVidApplied", "VID/PID Appliqué." },
                { "MsgSettingsSaved", "Paramètres enregistrés." },
                { "TitleSelectSignal", "Sélectionner le signal" },
                { "TitleUnmap", "Démapper" }
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
                { "GrpMap", "Mapeo de teclas" },
                { "ColTime", "Tiempo" }, { "ColKey", "Clave(Hex)" }, { "ColData", "Datos" }, { "MnuCopy", "Copiar" },
                { "ChkPauseLog", "Pausar registro" },
                { "LblLayer", "Capa:" }, { "LblSlot", "Ranura:" }, { "LblName", "Nombre:" }, { "BtnRename", "Renombrar" },
                { "LblTarget", "Destino:" }, { "TargetNone", "Ninguno" },
                { "LblSignal", "Señal:" }, { "BtnAutoDetect", "Detección auto" }, { "BtnUnmap", "Desasignar" },
                { "ChkEnableFileLog", "Guardar registro" }, { "ChkStartWithWindows", "Iniciar con Windows" },
                { "BtnSave", "Guardar config." }, { "BtnHide", "Ocultar" },
                { "MsgNameChanged", "Nombre cambiado." },
                { "MsgSelectSlot", "Seleccione una ranura." },
                { "MsgDetecting", "Detectando..." },
                { "MsgSelectSignal", "Seleccione una señal." },
                { "MsgUnmapConfirm", "¿Restablecer mapeo?" },
                { "MsgUnmapped", "Desasignado." },
                { "MsgPatternMapped", "Patrón mapeado." },
                { "MsgVidApplied", "VID/PID Aplicado." },
                { "MsgSettingsSaved", "Configuración guardada." },
                { "TitleSelectSignal", "Seleccionar señal" },
                { "TitleUnmap", "Desasignar" }
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
                { "GrpMap", "按键映射设置" },
                { "ColTime", "时间" }, { "ColKey", "键值(Hex)" }, { "ColData", "数据" }, { "MnuCopy", "复制" },
                { "ChkPauseLog", "暂停日志" },
                { "LblLayer", "层:" }, { "LblSlot", "槽:" }, { "LblName", "名称:" }, { "BtnRename", "重命名" },
                { "LblTarget", "目标:" }, { "TargetNone", "无" },
                { "LblSignal", "信号:" }, { "BtnAutoDetect", "自动检测" }, { "BtnUnmap", "取消映射" },
                { "ChkEnableFileLog", "保存日志文件" }, { "ChkStartWithWindows", "开机自启" },
                { "BtnSave", "保存设置" }, { "BtnHide", "隐藏到托盘" },
                { "MsgNameChanged", "名称已更改。" },
                { "MsgSelectSlot", "请先选择一个槽位。" },
                { "MsgDetecting", "检测中..." },
                { "MsgSelectSignal", "请从列表中选择信号。" },
                { "MsgUnmapConfirm", "确定要重置此按键映射吗？" },
                { "MsgUnmapped", "映射已取消。" },
                { "MsgPatternMapped", "模式已映射。" },
                { "MsgVidApplied", "VID/PID 已应用。" },
                { "MsgSettingsSaved", "设置已保存。" },
                { "TitleSelectSignal", "选择信号 (按键)" },
                { "TitleUnmap", "取消映射" }
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
                { "GrpMap", "Tastenbelegung" },
                { "ColTime", "Zeit" }, { "ColKey", "Taste(Hex)" }, { "ColData", "Daten" }, { "MnuCopy", "Kopieren" },
                { "ChkPauseLog", "Log pausieren" },
                { "LblLayer", "Ebene:" }, { "LblSlot", "Slot:" }, { "LblName", "Name:" }, { "BtnRename", "Umbenennen" },
                { "LblTarget", "Ziel:" }, { "TargetNone", "Keine" },
                { "LblSignal", "Signal:" }, { "BtnAutoDetect", "Auto-Erkennung" }, { "BtnUnmap", "Löschen" },
                { "ChkEnableFileLog", "Log speichern" }, { "ChkStartWithWindows", "Mit Windows starten" },
                { "BtnSave", "Speichern" }, { "BtnHide", "In Tray minimieren" },
                { "MsgNameChanged", "Name geändert." },
                { "MsgSelectSlot", "Bitte Slot wählen." },
                { "MsgDetecting", "Erkennen..." },
                { "MsgSelectSignal", "Bitte Signal wählen." },
                { "MsgUnmapConfirm", "Zuordnung zurücksetzen?" },
                { "MsgUnmapped", "Gelöscht." },
                { "MsgPatternMapped", "Zugeordnet." },
                { "MsgVidApplied", "VID/PID angewendet." },
                { "MsgSettingsSaved", "Gespeichert." },
                { "TitleSelectSignal", "Signal wählen" },
                { "TitleUnmap", "Löschen" }
            };
            data.Languages.Add(de);

            return data;
        }
    }
}