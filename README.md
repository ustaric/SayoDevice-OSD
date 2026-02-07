<a id="readme-top"></a>
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
2.  **Connect Device**: Click the **[Settings]** button to open the settings window. The program automatically finds the device. You can also manually enter VID/PID and click **[Apply]** in the settings window.
3.  **Key Mapping & Renaming**:
    1.  **Select Layer**: Choose the layer (Fn0 ~ Fn4) using the radio buttons at the top.
    2.  **Select Key & Rename**: Click one of the **12 Key Slots (Text boxes)** in the center. Type the desired name directly and press **Enter** to save.
    3.  **Register Signal**: Click **[Auto Detect]** and press the physical key.
        *   **Example**: If the signal repeats (e.g., `63`) and then changes (e.g., `12` or others), **double-click the first log with the new value**. (Even if it's not `12`, select the first distinct log. This usually works).
        ```text
        21:11:36	63	... (Repeating)
        21:11:36	12	... (Candidate Signal) <-- Double-click this!
        21:11:36	-	...
        ```
    4.  **Layer Move**: If the key changes layers, select the target layer in the **'Layer Move'** combo box. (Keys with layer move are highlighted in light blue).
    5.  **Unmap**: Select a slot and click **[Unmap]** to reset.
4.  **OSD Settings**:
    *   Click the **[Settings]** button. In the settings window, adjust Opacity, Timeout, and Mode (Auto/Always On/Off).
    *   Check **Allow OSD Move** to drag the OSD window, or click **Reset Size** to restore defaults.
5.  **Other**:
    *   Click **[Settings]** for more options, or **[Hide to Tray]** to run in the background.

### Settings Save & Reset

*   **Auto Save**: Settings are saved to `settings.json`.
*   **Layer Synchronization**: The program cannot retrieve the device's current layer on startup. However, the active layer is saved to `settings.json` whenever it changes. On startup, the last saved layer is restored. If the device and OSD layers differ, pressing a mapped key will instantly switch the OSD to the correct layer.
*   **Reset**: Delete `settings.json` and restart the program to reset.

### Disclaimer

*   **Unofficial Software**:
    This program is not affiliated with SayoDevice and is an **unofficial tool**.
*   **Liability**:
    **The user assumes all responsibility for the use of this program.**
    (Use at your own risk.)

### License

This project follows the **MIT License**.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

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
2.  **장치 연결**: 메인 화면의 **[설정]** 버튼을 눌러 설정 창을 엽니다. 자동으로 장치를 찾으며, 필요 시 VID/PID를 직접 입력하고 **[적용]**을 누를 수 있습니다.
3.  **키 매핑 및 이름 변경**:
    1.  **레이어 선택**: 상단의 레이어 버튼(Fn0 ~ Fn4)을 눌러 편집할 레이어를 선택합니다.
    2.  **키 선택 및 이름 변경**: 화면 중앙의 **12개 키 슬롯(텍스트 상자)** 중 하나를 클릭하여 선택합니다. 원하는 이름을 직접 입력하고 **Enter**를 누르면 저장됩니다.
    3.  **신호 등록**: **[자동 감지]**를 누르고 키를 입력합니다.
        *   **예시**: `63` 등이 반복되다가 `12` 등으로 변하거나 새로운 로그가 보이면, **변화된 첫 번째 로그**를 더블클릭하세요. (12가 나오지 않더라도, 변화가 감지된 첫 번째 로그를 등록하면 대부분 됩니다.)
        ```text
        21:11:36	63	... (반복)
        21:11:36	12	... (Candidate Signal) <-- 이것을 더블클릭!
        21:11:36	-	...
        ```
    4.  **레이어 이동 설정**: 해당 키가 레이어를 변경하는 키라면, **'레이어 이동'** 콤보박스에서 이동할 레이어를 선택하세요. (설정된 키는 연한 파란색으로 표시됩니다.)
    5.  **매핑 해제**: 슬롯 선택 후 **[매핑해제]** 버튼을 누르면 초기화됩니다.
4.  **OSD 설정**:
    *   **[설정]** 버튼을 눌러 설정 창에서 투명도, 표시 시간, 표시 모드(자동/항상 켜기/끄기)를 조절할 수 있습니다.
    *   **OSD 위치 이동 허용** 체크 후 드래그하여 위치를 변경하고, **크기 초기화** 버튼으로 되돌릴 수 있습니다.
5.  **기타**:
    *   **[설정]** 버튼을 눌러 추가 설정을 확인하거나, **[트레이로 숨기기]**를 통해 백그라운드로 전환합니다.

## 설정 저장 및 초기화

*   **자동 저장**: 모든 설정(키 매핑, OSD 위치/크기, 레이어 정보 등)은 프로그램 실행 폴더 내의 `settings.json` 파일에 자동으로 저장됩니다.
*   **레이어 동기화**: 프로그램 시작 시 기기의 현재 레이어 정보를 가져올 수 없어 즉시 동기화되지 않을 수 있습니다. 하지만 레이어 변경 시마다 설정 파일에 즉시 저장되어 다음 실행 시 복원되며, 기기와 OSD의 레이어가 다르더라도 키 입력 시 해당 레이어로 즉시 이동하여 동기화됩니다.
*   **초기화**: 설정을 초기화하려면 프로그램을 종료한 후 `settings.json` 파일을 삭제하고 다시 실행하세요. 파일이 없으면 초기 상태로 시작됩니다.

## 주의사항 (Disclaimer)

*   **비공식 소프트웨어 (Unofficial Software)**:
    이 프로그램은 SayoDevice 제조사와 관련이 없으며, 사용자가 편의를 위해 개발한 **비공식 도구**입니다. 공식 소프트웨어가 아닙니다.

*   **책임 면책**:
    **이 프로그램 사용으로 인한 책임은 사용자에게 있습니다.**
    개발자는 이 프로그램의 사용으로 인해 발생하는 기기 오작동, 데이터 손실, 또는 기타 문제에 대해 어떠한 책임도 지지 않습니다. (Use at your own risk.)

## 라이선스

이 프로젝트는 **MIT 라이선스**를 따릅니다. 자유롭게 수정하고 배포하셔도 됩니다.

<p align="right">(<a href="#readme-top">맨 위로</a>)</p>

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
2.  **Connecter le Périphérique** : Cliquez sur le bouton **[Paramètres]** pour ouvrir la fenêtre de configuration. Le programme trouve automatiquement le périphérique.
3.  **Mappage des Touches** :
    1.  **Sélectionner Couche** : Choisissez la couche (Fn0 ~ Fn4).
    2.  **Sélectionner Touche & Renommer** : Cliquez sur l'un des **12 Slots (Boîtes de texte)**. Tapez le nom et appuyez sur **Entrée**.
    3.  **Enregistrer Signal** : Cliquez sur **[Détection auto]** et appuyez sur la touche.
        *   **Exemple** : Si le signal se répète (ex: `63`) puis change (ex: `12` ou autre), **double-cliquez sur la première ligne avec la nouvelle valeur**. (Même si ce n'est pas `12`, sélectionnez le premier journal distinct. Cela fonctionne généralement).
        ```text
        21:11:36	63	... (Répétition)
        21:11:36	12	... (Candidate Signal) <-- Double-cliquez ici !
        21:11:36	-	...
        ```
    4.  **Changer Couche** : Si la touche change de couche, sélectionnez la cible dans **'Changer couche'**. (Surligné en bleu clair).
    5.  **Démapper** : Sélectionnez un slot et cliquez sur **[Démapper]**.
4.  **Paramètres OSD** :
    *   Cliquez sur **[Paramètres]**. Ajustez Opacité, Temps, Mode dans la fenêtre de configuration.
    *   Cochez **Déplacer l'OSD** pour bouger la fenêtre.
5.  **Autre** :
    *   Cliquez sur **[Paramètres]** ou **[Cacher]**.

### Sauvegarde & Réinitialisation

*   **Sauvegarde Auto** : Les paramètres sont sauvegardés dans `settings.json`.
*   **Synchronisation des Couches** : Le programme ne peut pas récupérer la couche actuelle du périphérique au démarrage. Cependant, la couche active est enregistrée dans `settings.json` à chaque changement. Au démarrage, la dernière couche enregistrée est restaurée. Si les couches du périphérique et de l'OSD diffèrent, appuyer sur une touche mappée basculera instantanément l'OSD vers la bonne couche.
*   **Réinitialiser** : Supprimez `settings.json` et redémarrez le programme pour réinitialiser.

### Avertissement

*   **Logiciel Non Officiel** : Ce programme n'est pas affilié à SayoDevice et est un **outil non officiel**.
*   **Responsabilité** : **L'utilisateur assume toute responsabilité pour l'utilisation de ce programme.** (Utilisation à vos propres risques.)

### Licence

Ce projet suit la **Licence MIT**.

<p align="right">(<a href="#readme-top">Haut de page</a>)</p>

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
2.  **Conectar Dispositivo**: Haga clic en el botón **[Configuración]** para abrir la ventana de ajustes. El programa encuentra el dispositivo automáticamente.
3.  **Mapeo de Teclas**:
    1.  **Seleccionar Capa**: Elija la capa (Fn0 ~ Fn4).
    2.  **Seleccionar Tecla y Renombrar**: Haga clic en una de las **12 Ranuras (Cuadros de texto)**. Escriba el nombre y presione **Enter**.
    3.  **Registrar Señal**: Haga clic en **[Detección auto]** y presione la tecla.
        *   **Ejemplo**: Si el valor se repite (ej. `63`) y luego cambia (ej. `12` u otro), **haga doble clic en la primera línea con el nuevo valor**. (Incluso si no es `12`, seleccione el primer registro distinto. Esto generalmente funciona).
        ```text
        21:11:36	63	... (Repetición)
        21:11:36	12	... (Candidate Signal) <-- ¡Doble clic aquí!
        21:11:36	-	...
        ```
    4.  **Cambio de Capa**: Si la tecla cambia de capa, seleccione el destino en **'Cambiar capa'**. (Resaltado en azul claro).
    5.  **Desasignar**: Seleccione una ranura y haga clic en **[Desasignar]**.
4.  **Configuración OSD**:
    *   Haga clic en **[Configuración]**. Ajuste Opacidad, Tiempo, Modo en la ventana de ajustes.
    *   Marque **Mover OSD** para arrastrar la ventana.
5.  **Otro**:
    *   Haga clic en **[Configuración]** o **[Ocultar]**.

### Guardar y Restablecer Configuración

*   **Guardado Auto**: La configuración se guarda en `settings.json`.
*   **Sincronización de Capas**: El programa no puede recuperar la capa actual del dispositivo al inicio. Sin embargo, la capa activa se guarda en `settings.json` cada vez que cambia. Al inicio, se restaura la última capa guardada. Si las capas del dispositivo y del OSD difieren, presionar una tecla asignada cambiará instantáneamente el OSD a la capa correcta.
*   **Restablecer**: Elimine `settings.json` y reinicie el programa para restablecer.

### Descargo de Responsabilidad

*   **Software No Oficial**: Este programa no está afiliado con SayoDevice y es una **herramienta no oficial**.
*   **Responsabilidad**: **El usuario asume toda la responsabilidad por el uso de este programa.** (Úselo bajo su propio riesgo.)

### Licencia

Este proyecto sigue la **Licencia MIT**.

<p align="right">(<a href="#readme-top">Volver arriba</a>)</p>

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
2.  **连接设备**：点击**[设置]**按钮打开设置窗口。程序会自动查找设备。您也可以手动输入 VID/PID 并点击**[应用]**。
3.  **按键映射与重命名**：
    1.  **选择层**：使用顶部的单选按钮选择层 (Fn0 ~ Fn4)。
    2.  **选择按键并重命名**：点击中间的 **12 个按键槽（文本框）**之一。直接输入名称并按 **Enter** 保存。
    3.  **注册信号**：点击**[自动检测]**并按下按键。
        *   **示例**：如果数值重复（如 `63`）然后发生变化（如 `12` 或其他），请**双击变化后的第一条日志**。（即使不是 `12`，选择出现的第一个不同日志通常也能成功）。
        ```text
        21:11:36	63	... (重复)
        21:11:36	12	... (Candidate Signal) <-- 双击这里！
        21:11:36	-	...
        ```
    4.  **层级移动**：如果按键用于切换层，请在**“层级移动”**中选择目标层。（显示为浅蓝色）。
    5.  **取消映射**：选择槽位并点击**[取消映射]**。
4.  **OSD 设置**：
    *   点击**[设置]**按钮。在设置窗口中调整透明度、时间和模式。
    *   勾选**允许移动 OSD** 以拖动窗口，或点击**重置大小**。
5.  **其他**：
    *   点击**[设置]**查看更多选项，或点击**[隐藏到托盘]**。

### 设置保存与重置

*   **自动保存**：设置保存到 `settings.json`。
*   **层级同步**：程序启动时无法获取设备的当前层级信息。但是，每当层级发生变化时，都会立即保存到 `settings.json` 中。下次启动时将恢复上次保存的层级。即使设备和 OSD 的层级不同，按下按键也会立即切换到相应的层级。
*   **重置**：删除 `settings.json` 并重启程序以重置。

### 免责声明

*   **非官方软件**：本程序与 SayoDevice 无关，是一个**非官方工具**。
*   **责任**：**用户承担使用本程序的所有责任。**（风险自负。）

### 许可证

本项目遵循 **MIT 许可证**。

<p align="right">(<a href="#readme-top">回到顶部</a>)</p>

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
2.  **Gerät verbinden**: Klicken Sie auf die Schaltfläche **[Einstellungen]**, um das Einstellungsfenster zu öffnen. Das Programm findet das Gerät automatisch.
3.  **Tastenbelegung**:
    1.  **Ebene wählen**: Wählen Sie die Ebene (Fn0 ~ Fn4).
    2.  **Taste wählen & Umbenennen**: Klicken Sie auf einen der **12 Slots (Textfelder)**. Namen eingeben und **Enter** drücken.
    3.  **Signal registrieren**: Klicken Sie auf **[Auto-Erkennung]** und drücken Sie die Taste.
        *   **Beispiel**: Wenn sich der Wert wiederholt (z. B. `63`) und dann ändert (z. B. `12` oder andere), **doppelklicken Sie auf den ersten Eintrag mit dem neuen Wert**. (Auch wenn es nicht `12` ist, wählen Sie den ersten abweichenden Eintrag. Das funktioniert meistens).
        ```text
        21:11:36	63	... (Wiederholung)
        21:11:36	12	... (Candidate Signal) <-- Hier doppelklicken!
        21:11:36	-	...
        ```
    4.  **Ebene wechseln**: Wenn die Taste die Ebene wechselt, wählen Sie das Ziel unter **'Ebene wechseln'**. (Hellblau hervorgehoben).
    5.  **Löschen**: Slot wählen und auf **[Löschen]** klicken.
4.  **OSD-Einstellungen**:
    *   Klicken Sie auf **[Einstellungen]**. Passen Sie Deckkraft, Zeit und Modus im Einstellungsfenster an.
    *   Aktivieren Sie **OSD verschieben**, um das Fenster zu ziehen.
5.  **Sonstiges**:
    *   Klicken Sie auf **[Einstellungen]** oder **[In Tray minimieren]**.

### Einstellungen speichern & zurücksetzen

*   **Auto-Speichern**: Einstellungen werden in `settings.json` gespeichert.
*   **Ebenen-Synchronisation**: Das Programm kann beim Start nicht die aktuelle Ebene des Geräts abrufen. Die aktive Ebene wird jedoch bei jeder Änderung in `settings.json` gespeichert. Beim Start wird die zuletzt gespeicherte Ebene wiederhergestellt. Wenn sich die Ebenen von Gerät und OSD unterscheiden, wechselt das OSD beim Drücken einer zugeordneten Taste sofort zur korrekten Ebene.
*   **Zurücksetzen**: Löschen Sie `settings.json` und starten Sie das Programm neu, um es zurückzusetzen.

### Haftungsausschluss

*   **Inoffizielle Software**: Dieses Programm ist nicht mit SayoDevice verbunden und ist ein **inoffizielles Tool**.
*   **Haftung**: **Der Benutzer übernimmt alle Verantwortung für die Verwendung dieses Programms.** (Benutzung auf eigene Gefahr.)

### Lizenz

Dieses Projekt folgt der **MIT-Lizenz**.

<p align="right">(<a href="#readme-top">Nach oben</a>)</p>

---

*Developed for SayoDevice Users.*