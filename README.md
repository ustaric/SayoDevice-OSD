# SayoDevice OSD (Unofficial)

<!-- Language Links -->
<div align="center">
  <a href="#english">English</a> | <a href="#korean">한국어</a> | <a href="#french">Français</a> | <a href="#spanish">Español</a> | <a href="#chinese">中文</a> | <a href="#german">Deutsch</a>
</div>

---

<a id="english"></a>
## English

> **Initial Release Beta**
> Currently supports **SayoDevice 2x6v RGB** model only.

An **unofficial** OSD (On-Screen Display) utility for SayoDevice keypad users.
It detects button input signals and visually displays the current key and layer information on the screen.

### Key Features

*   **OSD Display**: Overlays a window showing the name of the pressed key.
*   **Layer Support**: Supports 5 layers (Fn0 ~ Fn4), allowing different key names per layer.
*   **Customization**: Adjustable OSD position, size, opacity, display duration, and background density.
*   **Auto Detect**: Easily map key signals via 'Auto Detect' without complex setup.
*   **RawInput**: Uses Windows native features to detect devices without extra drivers.
*   **Tray Icon**: Runs in the background with a system tray icon.

### Localization

*   **Supported Languages**: English, Korean, French, Spanish, Chinese, German.
    *   *Note: Translations are AI-generated and may be unnatural.*
*   **Custom Translation**: You can modify the `languages.json` file to correct translations. **The program prioritizes the content of `languages.json` over internal defaults**, so your changes will be applied immediately upon restart.

### How to Use

1.  **Run**: Execute `SayoOSD.exe`.
2.  **Connect Device**: Automatically finds SayoDevice (VID: 8089, PID: 000B). If not recognized, modify VID/PID in settings and click 'Scan'.
3.  **Key Mapping**:
    1.  **Preparation**: Select the **Layer** and **Slot (Key 1~12)** to map.
    2.  **Set Name**: Enter the desired key name.
    3.  **Layer Switch Setting (Important)**: If this button changes layers, select the **target layer (Fn 0 ~ Fn 4)** in the **'Target Layer'** combo box. This ensures the OSD automatically switches to that layer when pressed.
    4.  **Detect & Register**: Click **[Auto Detect]** and press the key to register the signal. (Saved automatically.)
    *   *Note: To rename an existing key, enter the name and click **[Rename]** to save.*
4.  **Unmap**:
    *   To reset a key, select the slot and click **[Unmap]**.
5.  **Layer Settings**:
    *   Select a layer from the top combo box to set names. Last used layer is saved on exit.

### Settings Save & Reset

*   **Auto Save**: Settings are saved to `settings.json`.
*   **Reset**: Delete `settings.json` and restart the program to reset.

### Disclaimer

*   **Unofficial Software**:
    This program is not affiliated with SayoDevice and is an **unofficial tool**.
*   **Liability**:
    **The user assumes all responsibility for the use of this program.**
    (Use at your own risk.)

### License

This project follows the **MIT License**.

---

<a id="korean"></a>
## 한국어

> **초기 릴리즈 베타 버전 (Initial Release Beta)**
> 현재 **SayoDevice 2x6v RGB** 모델만 지원합니다.

SayoDevice 키패드 사용자를 위한 **비공식** OSD(On-Screen Display) 유틸리티입니다.
키패드의 버튼 입력 신호를 감지하여 화면에 현재 눌린 키와 레이어 정보를 시각적으로 표시해줍니다.

## 주요 기능

*   **OSD 표시**: 키 입력 시 화면에 오버레이 창을 띄워 현재 눌린 키의 이름을 보여줍니다.
*   **레이어 지원**: 5개의 레이어(Fn0 ~ Fn4)를 지원하며, 레이어별로 키 이름을 다르게 설정할 수 있습니다.
*   **커스터마이징**: OSD 창의 위치, 크기, 투명도, 표시 시간, 배경 농도를 자유롭게 조절할 수 있습니다.
*   **자동 감지**: 복잡한 설정 없이 '자동 감지' 기능을 통해 키 신호를 쉽게 매핑할 수 있습니다.
*   **RawInput 방식**: 별도의 드라이버 설치 없이 윈도우 기본 기능을 사용하여 장치를 감지합니다.
*   **트레이 아이콘**: 시스템 트레이에서 백그라운드로 가볍게 동작합니다.

## 다국어 지원 및 번역 수정

*   **지원 언어**: 한국어, 영어, 프랑스어, 스페인어, 중국어, 독일어.
    *   *참고: 번역은 AI 자동 번역을 사용하였으므로 표현이 다소 어색할 수 있습니다.*
*   **번역 수정**: `languages.json` 파일을 메모장으로 열어 직접 수정할 수 있습니다. **프로그램은 내부 기본값보다 `languages.json` 파일의 내용을 최우선으로 적용**하므로, 오역이 있다면 자유롭게 고쳐서 사용하세요.

## 사용 방법

1.  **실행**: `SayoOSD.exe`를 실행합니다.
2.  **장치 연결**: 프로그램이 자동으로 SayoDevice(VID: 8089, PID: 000B)를 찾습니다. 인식이 안 될 경우 설정에서 VID/PID를 수정하고 '검색'을 누르세요.
3.  **키 매핑**:
    1.  **준비**: 매핑할 **레이어**와 **슬롯(Key 1~12)**을 먼저 선택합니다.
    2.  **이름 설정**: 원하는 키 이름을 입력합니다.
    3.  **레이어 이동 설정 (중요)**: 만약 이 버튼이 **레이어를 변경하는 키**라면, **'이동'** 콤보박스에서 **전환될 레이어(Fn 0 ~ Fn 4)**를 선택해주세요. 이렇게 설정해야 키를 눌렀을 때 OSD도 해당 레이어로 자동 전환됩니다.
    4.  **감지 및 등록**: **[자동 감지]** 버튼을 누르고 키패드를 눌러 신호를 등록합니다. (이때 자동으로 저장됩니다.)
    *   *참고: 이미 등록된 키의 이름만 변경할 경우, 이름을 입력하고 **[이름변경]** 버튼을 눌러야 저장됩니다.*
4.  **매핑 해제**:
    *   잘못 등록된 키를 초기화하려면 슬롯을 선택하고 **[매핑해제]** 버튼을 누르세요.
5.  **레이어 설정**:
    *   프로그램 상단 콤보박스에서 레이어를 선택하여 각 레이어별 키 이름을 설정할 수 있습니다.
    *   프로그램 종료 시 마지막으로 사용한 레이어 위치가 저장됩니다.

## 설정 저장 및 초기화

*   **자동 저장**: 모든 설정(키 매핑, OSD 위치/크기, 레이어 정보 등)은 프로그램 실행 폴더 내의 `settings.json` 파일에 자동으로 저장됩니다.
*   **초기화**: 설정을 초기화하려면 프로그램을 종료한 후 `settings.json` 파일을 삭제하고 다시 실행하세요. 파일이 없으면 초기 상태로 시작됩니다.

## 주의사항 (Disclaimer)

*   **비공식 소프트웨어 (Unofficial Software)**:
    이 프로그램은 SayoDevice 제조사와 관련이 없으며, 사용자가 편의를 위해 개발한 **비공식 도구**입니다. 공식 소프트웨어가 아닙니다.

*   **책임 면책**:
    **이 프로그램 사용으로 인한 책임은 사용자에게 있습니다.**
    개발자는 이 프로그램의 사용으로 인해 발생하는 기기 오작동, 데이터 손실, 또는 기타 문제에 대해 어떠한 책임도 지지 않습니다. (Use at your own risk.)

## 라이선스

이 프로젝트는 **MIT 라이선스**를 따릅니다. 자유롭게 수정하고 배포하셔도 됩니다.

---

<a id="french"></a>
## Français

> **Version Bêta Initiale**
> Actuellement compatible uniquement avec le modèle **SayoDevice 2x6v RGB**.

Un utilitaire OSD (On-Screen Display) **non officiel** pour les utilisateurs de claviers SayoDevice.
Il détecte les signaux d'entrée des boutons et affiche visuellement la touche actuelle et les informations de couche à l'écran.

### Fonctionnalités Principales

*   **Affichage OSD** : Affiche une fenêtre superposée indiquant le nom de la touche pressée.
*   **Support des Couches** : Supporte 5 couches (Fn0 ~ Fn4), permettant des noms de touches différents par couche.
*   **Personnalisation** : Position, taille, opacité, durée d'affichage et densité de fond de l'OSD ajustables.
*   **Détection Auto** : Mappez facilement les signaux de touches via 'Détection auto' sans configuration complexe.
*   **RawInput** : Utilise les fonctionnalités natives de Windows pour détecter les périphériques sans pilotes supplémentaires.
*   **Icône de la Zone de Notification** : Fonctionne en arrière-plan avec une icône dans la barre des tâches.

### Localisation

*   **Langues Supportées** : Anglais, Coréen, Français, Espagnol, Chinois, Allemand.
    *   *Note : Les traductions sont générées par IA et peuvent ne pas être naturelles.*
*   **Traduction Personnalisée** : Vous pouvez modifier le fichier `languages.json` pour corriger les traductions. **Le programme priorise le contenu de `languages.json` sur les défauts internes**, vos modifications seront donc appliquées immédiatement après redémarrage.

### Comment Utiliser

1.  **Lancer** : Exécutez `SayoOSD.exe`.
2.  **Connecter le Périphérique** : Trouve automatiquement SayoDevice (VID: 8089, PID: 000B). Si non reconnu, modifiez VID/PID dans les paramètres et cliquez sur 'Scanner'.
3.  **Mappage des Touches** :
    1.  **Préparation** : Sélectionnez la **Couche** et le **Slot (Touche 1~12)** à mapper.
    2.  **Définir le Nom** : Entrez le nom de touche souhaité.
    3.  **Réglage Changement de Couche (Important)** : Si ce bouton change de couche, sélectionnez la **couche cible (Fn 0 ~ Fn 4)** dans la liste déroulante **'Cible'**. Cela assure que l'OSD bascule automatiquement vers cette couche lorsqu'il est pressé.
    4.  **Détecter & Enregistrer** : Cliquez sur **[Détection auto]** et appuyez sur la touche pour enregistrer le signal. (Sauvegardé automatiquement.)
    *   *Note : Pour renommer une touche existante, entrez le nom et cliquez sur **[Renommer]** pour sauvegarder.*
4.  **Démapper** :
    *   Pour réinitialiser une touche, sélectionnez le slot et cliquez sur **[Démapper]**.
5.  **Paramètres de Couche** :
    *   Sélectionnez une couche dans la liste déroulante supérieure pour définir les noms. La dernière couche utilisée est sauvegardée à la sortie.

### Sauvegarde & Réinitialisation

*   **Sauvegarde Auto** : Les paramètres sont sauvegardés dans `settings.json`.
*   **Réinitialiser** : Supprimez `settings.json` et redémarrez le programme pour réinitialiser.

### Avertissement

*   **Logiciel Non Officiel** : Ce programme n'est pas affilié à SayoDevice et est un **outil non officiel**.
*   **Responsabilité** : **L'utilisateur assume toute responsabilité pour l'utilisation de ce programme.** (Utilisation à vos propres risques.)

### Licence

Ce projet suit la **Licence MIT**.

---

<a id="spanish"></a>
## Español

> **Versión Beta Inicial**
> Actualmente solo soporta el modelo **SayoDevice 2x6v RGB**.

Una utilidad OSD (On-Screen Display) **no oficial** para usuarios de teclados SayoDevice.
Detecta las señales de entrada de los botones y muestra visualmente la tecla actual y la información de la capa en la pantalla.

### Características Principales

*   **Pantalla OSD**: Muestra una ventana superpuesta con el nombre de la tecla presionada.
*   **Soporte de Capas**: Soporta 5 capas (Fn0 ~ Fn4), permitiendo diferentes nombres de teclas por capa.
*   **Personalización**: Posición, tamaño, opacidad, duración y densidad de fondo del OSD ajustables.
*   **Detección Auto**: Asigne fácilmente señales de teclas mediante 'Detección auto' sin configuración compleja.
*   **RawInput**: Utiliza funciones nativas de Windows para detectar dispositivos sin controladores adicionales.
*   **Icono de Bandeja**: Se ejecuta en segundo plano con un icono en la bandeja del sistema.

### Localización

*   **Idiomas Soportados**: Inglés, Coreano, Francés, Español, Chino, Alemán.
    *   *Nota: Las traducciones son generadas por IA y pueden no ser naturales.*
*   **Traducción Personalizada**: Puede modificar el archivo `languages.json` para corregir traducciones. **El programa prioriza el contenido de `languages.json` sobre los valores internos**, por lo que sus cambios se aplicarán inmediatamente al reiniciar.

### Cómo Usar

1.  **Ejecutar**: Ejecute `SayoOSD.exe`.
2.  **Conectar Dispositivo**: Encuentra automáticamente SayoDevice (VID: 8089, PID: 000B). Si no se reconoce, modifique VID/PID en la configuración y haga clic en 'Escanear'.
3.  **Mapeo de Teclas**:
    1.  **Preparación**: Seleccione la **Capa** y la **Ranura (Tecla 1~12)** para mapear.
    2.  **Establecer Nombre**: Ingrese el nombre de tecla deseado.
    3.  **Configuración de Cambio de Capa (Importante)**: Si este botón cambia capas, seleccione la **capa destino (Fn 0 ~ Fn 4)** en el cuadro combinado **'Destino'**. Esto asegura que el OSD cambie automáticamente a esa capa cuando se presione.
    4.  **Detectar y Registrar**: Haga clic en **[Detección auto]** y presione la tecla para registrar la señal. (Guardado automático.)
    *   *Nota: Para renombrar una tecla existente, ingrese el nombre y haga clic en **[Renombrar]** para guardar.*
4.  **Desasignar**:
    *   Para restablecer una tecla, seleccione la ranura y haga clic en **[Desasignar]**.
5.  **Configuración de Capa**:
    *   Seleccione una capa del cuadro combinado superior para establecer nombres. La última capa utilizada se guarda al salir.

### Guardar y Restablecer Configuración

*   **Guardado Auto**: La configuración se guarda en `settings.json`.
*   **Restablecer**: Elimine `settings.json` y reinicie el programa para restablecer.

### Descargo de Responsabilidad

*   **Software No Oficial**: Este programa no está afiliado con SayoDevice y es una **herramienta no oficial**.
*   **Responsabilidad**: **El usuario asume toda la responsabilidad por el uso de este programa.** (Úselo bajo su propio riesgo.)

### Licencia

Este proyecto sigue la **Licencia MIT**.

---

<a id="chinese"></a>
## 中文

> **初始测试版**
> 目前仅支持 **SayoDevice 2x6v RGB** 型号。

专为 SayoDevice 键盘用户设计的**非官方** OSD（屏幕显示）工具。
它可以检测按键输入信号，并在屏幕上直观显示当前按键和层级信息。

### 主要功能

*   **OSD 显示**：按下按键时在屏幕上显示按键名称。
*   **层级支持**：支持 5 个层级（Fn0 ~ Fn4），每层可设置不同的按键名称。
*   **自定义**：可调整 OSD 的位置、大小、透明度、显示时间和背景浓度。
*   **自动检测**：通过“自动检测”功能轻松映射按键信号，无需复杂设置。
*   **RawInput**：使用 Windows 原生功能检测设备，无需额外驱动。
*   **托盘图标**：在后台运行，并在系统托盘显示图标。

### 本地化

*   **支持语言**：英语、韩语、法语、西班牙语、中文、德语。
    *   *注意：翻译由 AI 生成，可能不自然。*
*   **自定义翻译**：您可以修改 `languages.json` 文件以更正翻译。**程序优先使用 `languages.json` 的内容**，因此您的更改将在重启后立即生效。

### 使用方法

1.  **运行**：执行 `SayoOSD.exe`。
2.  **连接设备**：自动查找 SayoDevice (VID: 8089, PID: 000B)。如果未识别，请在设置中修改 VID/PID 并点击“扫描”。
3.  **按键映射**：
    1.  **准备**：选择要映射的**层**和**槽位 (键 1~12)**。
    2.  **设置名称**：输入所需的按键名称。
    3.  **层切换设置（重要）**：如果此按钮用于切换层级，请在**“目标”**下拉框中选择**目标层 (Fn 0 ~ Fn 4)**。这确保按下时 OSD 自动切换到该层。
    4.  **检测并注册**：点击**[自动检测]**并按下按键以注册信号。（自动保存。）
    *   *注意：要重命名现有按键，请输入名称并点击**[重命名]**以保存。*
4.  **取消映射**：
    *   要重置按键，请选择槽位并点击**[取消映射]**。
5.  **层设置**：
    *   从顶部下拉框选择层以设置名称。退出时保存最后使用的层。

### 设置保存与重置

*   **自动保存**：设置保存到 `settings.json`。
*   **重置**：删除 `settings.json` 并重启程序以重置。

### 免责声明

*   **非官方软件**：本程序与 SayoDevice 无关，是一个**非官方工具**。
*   **责任**：**用户承担使用本程序的所有责任。**（风险自负。）

### 许可证

本项目遵循 **MIT 许可证**。

---

<a id="german"></a>
## Deutsch

> **Erste Beta-Version**
> Unterstützt derzeit nur das Modell **SayoDevice 2x6v RGB**.

Ein **inoffizielles** OSD (On-Screen Display) Dienstprogramm für SayoDevice-Tastaturbenutzer.
Es erkennt Tasteneingabesignale und zeigt die aktuelle Taste und Ebeneninformationen visuell auf dem Bildschirm an.

### Hauptfunktionen

*   **OSD-Anzeige**: Zeigt ein Fenster mit dem Namen der gedrückten Taste an.
*   **Ebenen-Unterstützung**: Unterstützt 5 Ebenen (Fn0 ~ Fn4), erlaubt unterschiedliche Tastennamen pro Ebene.
*   **Anpassung**: OSD-Position, Größe, Deckkraft, Anzeigedauer und Hintergrunddichte einstellbar.
*   **Auto-Erkennung**: Einfaches Zuordnen von Tastensignalen über 'Auto-Erkennung' ohne komplexe Einrichtung.
*   **RawInput**: Verwendet native Windows-Funktionen zur Geräteerkennung ohne zusätzliche Treiber.
*   **Tray-Icon**: Läuft im Hintergrund mit einem Symbol in der Taskleiste.

### Lokalisierung

*   **Unterstützte Sprachen**: Englisch, Koreanisch, Französisch, Spanisch, Chinesisch, Deutsch.
    *   *Hinweis: Übersetzungen sind KI-generiert und können unnatürlich sein.*
*   **Benutzerdefinierte Übersetzung**: Sie können die Datei `languages.json` ändern, um Übersetzungen zu korrigieren. **Das Programm priorisiert den Inhalt von `languages.json` gegenüber internen Standards**, sodass Ihre Änderungen nach dem Neustart sofort wirksam werden.

### Verwendung

1.  **Ausführen**: Starten Sie `SayoOSD.exe`.
2.  **Gerät verbinden**: Findet automatisch SayoDevice (VID: 8089, PID: 000B). Falls nicht erkannt, ändern Sie VID/PID in den Einstellungen und klicken Sie auf 'Scannen'.
3.  **Tastenbelegung**:
    1.  **Vorbereitung**: Wählen Sie die **Ebene** und den **Slot (Taste 1~12)** zum Zuordnen.
    2.  **Name festlegen**: Geben Sie den gewünschten Tastennamen ein.
    3.  **Ebenenwechsel-Einstellung (Wichtig)**: Wenn diese Taste die Ebene wechselt, wählen Sie die **Zielebene (Fn 0 ~ Fn 4)** im Kombinationsfeld **'Ziel'**. Dies stellt sicher, dass das OSD beim Drücken automatisch zu dieser Ebene wechselt.
    4.  **Erkennen & Registrieren**: Klicken Sie auf **[Auto-Erkennung]** und drücken Sie die Taste, um das Signal zu registrieren. (Automatisch gespeichert.)
    *   *Hinweis: Um eine vorhandene Taste umzubenennen, geben Sie den Namen ein und klicken Sie auf **[Umbenennen]**, um zu speichern.*
4.  **Löschen**:
    *   Um eine Taste zurückzusetzen, wählen Sie den Slot und klicken Sie auf **[Löschen]**.
5.  **Ebeneneinstellungen**:
    *   Wählen Sie eine Ebene aus dem oberen Kombinationsfeld, um Namen festzulegen. Die zuletzt verwendete Ebene wird beim Beenden gespeichert.

### Einstellungen speichern & zurücksetzen

*   **Auto-Speichern**: Einstellungen werden in `settings.json` gespeichert.
*   **Zurücksetzen**: Löschen Sie `settings.json` und starten Sie das Programm neu, um es zurückzusetzen.

### Haftungsausschluss

*   **Inoffizielle Software**: Dieses Programm ist nicht mit SayoDevice verbunden und ist ein **inoffizielles Tool**.
*   **Haftung**: **Der Benutzer übernimmt alle Verantwortung für die Verwendung dieses Programms.** (Benutzung auf eigene Gefahr.)

### Lizenz

Dieses Projekt folgt der **MIT-Lizenz**.

---

*Developed for SayoDevice Users.*