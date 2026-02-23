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
>
> *   **System Requirements**: Windows 10/11 (64-bit only)
> *   **Tested Environment**: Windows 11 25H2 (Build 26200.7462)

An **unofficial** OSD (On-Screen Display) utility for SayoDevice keypad users.
It detects button input signals and visually displays the current key and layer information on the screen.

### Key Features

*   **OSD Display**: Overlays a window showing the name or icon of the pressed key.
*   **App Profiles (Virtual Layers)**: Automatically switches key configurations based on the active window (e.g., activate Photoshop-specific keys when Photoshop is running).
*   **Layer Support**: Supports 5 hardware layers (Fn0 ~ Fn4), allowing different key settings per layer.
*   **Advanced Customization**:
    *   **OSD**: Freely adjust position, size, opacity, display duration, vertical mode, row swapping, and more.
    *   **Theming**: Set custom background/highlight/border colors and fonts (family, size, weight) for the OSD globally, per-layer, or per-profile.
*   **Icon Display**: When a program or icon file is linked to a key, its icon is displayed on the OSD instead of text.
*   **Convenient Function Assignment**:
    *   **Drag & Drop**: Simply drag a function (media, volume, run program, etc.) from the Function Palette and drop it onto a key slot.
    *   **File Drop**: Drag an executable file (.exe) onto a key slot in the main window or the OSD to instantly create a 'Run Program' shortcut. *(Note: May not work if running as Administrator due to Windows UIPI restrictions.)*
*   **Auto Detect**: Easily map key signals via 'Auto Detect' without complex setup.
*   **Diverse Functions**: Supports running/switching programs, text macros, media control, system/active-app volume adjustment, mic mute, audio device cycling, profile cycling, and more.
*   **RawInput**: Uses Windows native features to detect devices without extra drivers.
*   **Tray Icon**: Runs in the background with a system tray icon, providing a menu to change OSD mode and position.

### Localization

*   **Supported Languages**: English, Korean, French, Spanish, Chinese, German.
    *   *Note: Translations are AI-generated and may be unnatural.*
*   **Custom Translation**: You can modify the `languages.json` file to correct translations. **The program prioritizes the content of `languages.json` over internal defaults**, so your changes will be applied immediately upon restart.

### How to Use

1.  **Run**: Execute `SayoOSD.exe`.
2.  **Connect Device**: Click the **[Settings]** button to open the settings window. The program automatically finds the device. You can also manually enter VID/PID and click **[Apply]** in the settings window.
3.  **Key Mapping & Renaming**:
    1.  **Select Layer/Profile**:
        *   **Hardware Layer**: Choose the layer (Fn0 ~ Fn4) using the radio buttons at the top.
        *   **App Profile (Virtual Layer)**: Select a profile from the 'App Profiles' list on the left. (Profiles can be added/managed in the **[Settings]** window).
    2.  **Select Key & Rename**: Click one of the **12 Key Slots (Text boxes)** in the center. Type the desired name and press **Enter** to save.
    3.  **Register Signal (Link Key to Slot)**: Click **[Auto Detect]** and press the physical key on your keypad. The key's signal will be registered to the selected slot.
        *   *Tip*: You can also use **[Manual Detect]** to see a list of incoming signals and double-click the correct one.
    4.  **Assign Function**:
        *   **Drag & Drop (Recommended)**: Drag items from the **Function Palette** on the left (Layer Move, Media, Volume, Profile Cycle, etc.) and drop them onto a key slot.
        *   **File Drop**: Drag an executable file (.exe) directly onto a key slot in the main window or a slot on the OSD itself to register it as a 'Run Program' function.
        *   **Detail Settings**: When you assign **'Run Program'** or **'Text Macro'**, a detail panel appears at the bottom. Enter the program path, arguments, or macro text and click **[Save]**.
    5.  **Unmap**: Select a slot and click **[Unmap]**.
4.  **OSD Settings**:
    *   Click the **[Settings]** button. In the settings window, adjust Opacity, Timeout, and Mode (Auto/Always On/Off).
    *   **Layout**: Check **Vertical Mode** for a 6x2 layout or **Swap Rows** to flip the top/bottom rows.
    *   **Move**: Check **Allow OSD Move** or hold the `Ctrl` key while dragging the OSD window to change its position.
    *   **Theme/Font**: In the 'OSD Style' tab of the settings window, you can configure global, per-layer, and per-profile colors and fonts in detail.
5.  **Other**:
    *   **Start with Windows**: Enable in settings to run automatically on startup (some features may require Admin rights).

### Settings Save & Reset

*   **Auto Save**: All settings (key mappings, functions, OSD position/size/style, App Profiles, etc.) are automatically saved to `settings.json` in the program's folder.
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

**Third Party Licenses:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   License: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   See [Settings] > [Open Source Licenses] in the app for details.
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Apache 2.0 License)
    *   License: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   See [Settings] > [Open Source Licenses] in the app for details.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

---

<a id="korean"></a>
## 한국어

> **초기 릴리즈 베타 버전 (Initial Release Beta)**
> 현재 **SayoDevice 2x6v RGB** 모델만 지원합니다.
>
> *   **시스템 요구사항**: Windows 10/11 (64비트 전용)
> *   **테스트 환경**: Windows 11 25H2 (빌드 26200.7462)

SayoDevice 키패드 사용자를 위한 **비공식** OSD(On-Screen Display) 유틸리티입니다.
키패드의 버튼 입력 신호를 감지하여 화면에 현재 눌린 키와 레이어 정보를 시각적으로 표시해줍니다.

## 주요 기능

*   **OSD 표시**: 키 입력 시 화면에 오버레이 창을 띄워 현재 눌린 키의 이름이나 아이콘을 보여줍니다.
*   **앱별 프로필 (가상 레이어)**: 활성 창(프로그램)에 따라 키 설정을 자동으로 전환합니다. (예: 포토샵 실행 시 포토샵용 키 설정 활성화)
*   **레이어 지원**: 5개의 하드웨어 레이어(Fn0 ~ Fn4)를 지원하며, 레이어별로 키 설정을 다르게 할 수 있습니다.
*   **상세한 커스터마이징**:
    *   **OSD**: 위치, 크기, 투명도, 표시 시간, 세로 모드, 줄 교체 등 레이아웃을 자유롭게 조절할 수 있습니다.
    *   **테마**: 전역, 레이어별, 프로필별로 OSD의 배경/강조/테두리 색상과 폰트(글꼴, 크기, 굵기)를 개별적으로 설정할 수 있습니다.
*   **아이콘 표시**: 키에 프로그램이나 아이콘 파일을 연결하면 OSD에 텍스트 대신 아이콘이 표시됩니다.
*   **편리한 기능 할당**:
    *   **드래그 앤 드롭**: 기능 팔레트에서 원하는 기능(미디어, 볼륨, 프로그램 실행 등)을 키 슬롯으로 끌어다 놓기만 하면 기능이 할당됩니다.
    *   **파일 드롭**: 실행 파일(.exe)을 OSD 창이나 메인 창의 키 슬롯에 끌어다 놓으면 '프로그램 실행' 기능이 바로 등록됩니다. *(참고: 관리자 권한으로 실행 시 윈도우 보안 정책에 의해 작동하지 않을 수 있습니다.)*
*   **자동 감지**: 복잡한 설정 없이 '자동 감지' 기능을 통해 키 신호를 쉽게 매핑할 수 있습니다.
*   **다양한 기능**: 프로그램 실행/전환, 텍스트 매크로, 미디어 제어, 시스템/활성창 볼륨 조절, 마이크 음소거, 오디오 장치 순환, 프로필 순환 등 다양한 기능을 지원합니다.
*   **RawInput 방식**: 별도의 드라이버 설치 없이 윈도우 기본 기능을 사용하여 장치를 감지합니다.
*   **트레이 아이콘**: 시스템 트레이에서 백그라운드로 가볍게 동작하며, 메뉴를 통해 OSD 모드 변경 및 이동이 가능합니다.

## 다국어 지원 및 번역 수정

*   **지원 언어**: 한국어, 영어, 프랑스어, 스페인어, 중국어, 독일어.
    *   *참고: 번역은 AI 자동 번역을 사용하였으므로 표현이 다소 어색할 수 있습니다.*
*   **번역 수정**: `languages.json` 파일을 메모장으로 열어 직접 수정할 수 있습니다. **프로그램은 내부 기본값보다 `languages.json` 파일의 내용을 최우선으로 적용**하므로, 오역이 있다면 자유롭게 고쳐서 사용하세요.

## 사용 방법

1.  **실행**: `SayoOSD.exe`를 실행합니다.
2.  **장치 연결**: 메인 화면의 **[설정]** 버튼을 눌러 설정 창을 엽니다. 자동으로 장치를 찾으며, 필요 시 VID/PID를 직접 입력하고 **[적용]**을 누를 수 있습니다.
3.  **키 매핑 및 이름 변경**:
    1.  **레이어/프로필 선택**:
        *   **하드웨어 레이어**: 상단의 레이어 버튼(Fn0 ~ Fn4)을 눌러 편집할 레이어를 선택합니다.
        *   **앱 프로필 (가상 레이어)**: 좌측 '앱 프로필' 목록에서 프로필을 선택합니다. (프로필 추가/관리는 **[설정]** 창에서 가능)
    2.  **키 선택 및 이름 변경**: 화면 중앙의 **12개 키 슬롯(텍스트 상자)** 중 하나를 클릭하여 선택합니다. 원하는 이름을 직접 입력하고 **Enter**를 누르면 저장됩니다.
    3.  **신호 등록 (키-슬롯 연결)**: **[자동 감지]**를 누르고 실제 키패드의 키를 누릅니다. 해당 키의 신호가 현재 선택된 슬롯에 등록됩니다.
        *   *팁*: **[수동 감지]**를 누르면 입력되는 신호 목록을 직접 보고 더블클릭하여 등록할 수도 있습니다.
    4.  **기능 할당**:
        *   **드래그 앤 드롭 (권장)**: 왼쪽 **'기능 팔레트'**에서 원하는 기능(레이어 이동, 미디어, 볼륨, 프로필 순환 등)을 드래그하여 키 슬롯에 놓으세요.
        *   **파일 드롭**: 실행 파일(.exe)을 메인 창의 키 슬롯이나 OSD 창의 슬롯에 직접 끌어다 놓으면 '프로그램 실행' 기능이 바로 등록됩니다.
        *   **상세 설정**: **'프로그램 실행'**이나 **'텍스트 매크로'** 기능을 할당하면 하단에 상세 설정 패널이 나타납니다. 프로그램 경로, 인수, 매크로 텍스트 등을 입력하고 **[저장]**을 누르세요.
    5.  **매핑 해제**: 슬롯 선택 후 **[매핑해제]** 버튼을 누릅니다.
4.  **OSD 설정**:
    *   **[설정]** 버튼을 눌러 설정 창에서 투명도, 표시 시간, 표시 모드(자동/항상 켜기/끄기)를 조절할 수 있습니다.
    *   **레이아웃**: **세로 모드** 체크 시 6x2 배열로 변경되며, **줄 교체**로 윗줄/아랫줄 순서를 바꿀 수 있습니다.
    *   **이동**: **OSD 위치 이동 허용**을 체크하거나, `Ctrl` 키를 누른 상태에서 OSD 창을 드래그하여 위치를 변경합니다.
    *   **테마/폰트**: 설정 창의 'OSD 스타일' 탭에서 전역/레이어별/프로필별 색상과 폰트를 상세하게 설정할 수 있습니다.
5.  **기타**:
    *   **윈도우 시작 시 자동 실행**: 설정에서 체크하여 부팅 시 자동 실행할 수 있습니다. (일부 기능은 관리자 권한 필요)

## 설정 저장 및 초기화

*   **자동 저장**: 모든 설정(키 매핑, 기능, OSD 위치/크기/스타일, 앱 프로필 등)은 프로그램 실행 폴더 내의 `settings.json` 파일에 자동으로 저장됩니다.
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

**오픈소스 라이선스 고지:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   라이선스: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   앱 내 [설정] > [오픈소스 라이선스] 메뉴에서 전문을 확인할 수 있습니다.
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Apache 2.0 License)
    *   라이선스: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   앱 내 [설정] > [오픈소스 라이선스] 메뉴에서 전문을 확인할 수 있습니다.

<p align="right">(<a href="#readme-top">맨 위로</a>)</p>

---

<a id="french"></a>
## Français

> **Version Bêta Initiale**
> Actuellement compatible uniquement avec le modèle **SayoDevice 2x6v RGB**.

Un utilitaire OSD (On-Screen Display) **non officiel** pour les utilisateurs de claviers SayoDevice.
Il détecte les signaux d'entrée des boutons et affiche visuellement la touche actuelle et les informations de couche à l'écran.

### Fonctionnalités Principales

*   **Affichage OSD** : Affiche une fenêtre superposée indiquant le nom ou l'icône de la touche pressée.
*   **Profils d'application (Couches virtuelles)** : Change automatiquement les configurations de touches en fonction de la fenêtre active.
*   **Support des Couches** : Supporte 5 couches matérielles (Fn0 ~ Fn4), permettant des noms de touches différents par couche.
*   **Personnalisation Avancée** :
    *   **OSD** : Ajustez librement la position, la taille, l'opacité, la durée, le mode vertical, etc.
    *   **Thème** : Définissez des couleurs d'arrière-plan/surbrillance/bordure et des polices personnalisées pour l'OSD de manière globale, par couche ou par profil.
*   **Affichage d'icônes** : Lorsqu'un programme est lié à une touche, son icône s'affiche à la place du texte.
*   **Assignation de Fonction Pratique**:
    *   **Glisser-déposer** : Faites simplement glisser une fonction de la Palette de fonctions et déposez-la sur un emplacement de touche.
    *   **Dépôt de fichier** : Faites glisser un fichier exécutable (.exe) sur un emplacement de touche pour créer un raccourci 'Lancer le programme'. *(Note : Peut ne pas fonctionner si le programme est exécuté en tant qu'administrateur.)*
*   **Détection Auto** : Mappez facilement les signaux de touches via 'Détection auto'.
*   **Fonctions Diverses** : Supporte le lancement de programmes, les macros texte, le contrôle multimédia, le réglage du volume (système/application active), la mise en sourdine du micro, etc.
*   **RawInput** : Utilise les fonctionnalités natives de Windows pour détecter les périphériques.
*   **Icône de la Zone de Notification** : Fonctionne en arrière-plan et offre un menu pour un accès rapide.

### Localisation

*   **Langues Supportées** : Anglais, Coréen, Français, Espagnol, Chinois, Allemand.
    *   *Note : Les traductions sont générées par IA et peuvent ne pas être naturelles.*
*   **Traduction Personnalisée** : Vous pouvez modifier le fichier `languages.json` pour corriger les traductions. **Le programme priorise le contenu de `languages.json` sur les défauts internes**, vos modifications seront donc appliquées immédiatement après redémarrage.

### Comment Utiliser

1.  **Lancer** : Exécutez `SayoOSD.exe`.
2.  **Connecter le Périphérique** : Cliquez sur le bouton **[Paramètres]** pour ouvrir la fenêtre de configuration. Le programme trouve automatiquement le périphérique.
3.  **Mappage des Touches** :
    1.  **Sélectionner Couche/Profil** : Choisissez une couche matérielle (Fn0-Fn4) ou un profil d'application.
    2.  **Sélectionner Touche & Renommer** : Cliquez sur un emplacement de touche, tapez le nom et appuyez sur **Entrée**.
    3.  **Enregistrer Signal** : Cliquez sur **[Détection auto]** et appuyez sur la touche physique.
    4.  **Assigner Fonction** :
        *   **Glisser-déposer (Recommandé)** : Faites glisser une fonction de la **Palette de fonctions** de gauche vers un emplacement de touche.
        *   **Dépôt de fichier** : Faites glisser un fichier .exe sur un emplacement pour créer un raccourci.
        *   **Paramètres Détaillés** : Pour 'Lancer le programme' ou 'Macro texte', un panneau de détails apparaît en bas. Entrez les informations et cliquez sur **[Enregistrer]**.
    5.  **Démapper** : Sélectionnez un emplacement et cliquez sur **[Démapper]**.
4.  **Paramètres OSD** :
    *   Cliquez sur **[Paramètres]**. Ajustez Opacité, Temps, Mode dans la fenêtre de configuration.
    *   **Disposition** : Cochez **Mode Vertical** ou **Échanger les rangées**.
    *   **Déplacer** : Cochez **Déplacer l'OSD** ou maintenez la touche `Ctrl` enfoncée et faites glisser la fenêtre OSD.
    *   **Thème/Police** : Configurez les couleurs et les polices dans l'onglet 'Style OSD' des paramètres.
5.  **Autre** :
    *   **Démarrer avec Windows** : Activez dans les paramètres.

### Sauvegarde & Réinitialisation

*   **Sauvegarde Auto** : Tous les paramètres (mappages, styles, profils, etc.) sont sauvegardés dans `settings.json`.
*   **Synchronisation des Couches** : Le programme ne peut pas récupérer la couche actuelle du périphérique au démarrage. Cependant, la couche active est enregistrée dans `settings.json` à chaque changement. Au démarrage, la dernière couche enregistrée est restaurée. Si les couches du périphérique et de l'OSD diffèrent, appuyer sur une touche mappée basculera instantanément l'OSD vers la bonne couche.
*   **Réinitialiser** : Supprimez `settings.json` et redémarrez le programme pour réinitialiser.

### Avertissement

*   **Logiciel Non Officiel** : Ce programme n'est pas affilié à SayoDevice et est un **outil non officiel**.
*   **Responsabilité** : **L'utilisateur assume toute responsabilité pour l'utilisation de ce programme.** (Utilisation à vos propres risques.)

### Licence

Ce projet suit la **Licence MIT**.

**Licences tierces:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   Licence: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   Voir [Paramètres] > [Licences Open Source] dans l'application pour plus de détails.
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Licence Apache 2.0)
    *   Licence: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   Voir [Paramètres] > [Licences Open Source] dans l'application pour plus de détails.

<p align="right">(<a href="#readme-top">Haut de page</a>)</p>

---

<a id="spanish"></a>
## Español

> **Versión Beta Inicial**
> Actualmente solo soporta el modelo **SayoDevice 2x6v RGB**.

Una utilidad OSD (On-Screen Display) **no oficial** para usuarios de teclados SayoDevice.
Detecta las señales de entrada de los botones y muestra visualmente la tecla actual y la información de la capa en la pantalla.

### Características Principales

*   **Pantalla OSD**: Muestra una ventana superpuesta con el nombre o el icono de la tecla presionada.
*   **Perfiles de aplicación (Capas virtuales)**: Cambia automáticamente las configuraciones de teclas según la ventana activa.
*   **Soporte de Capas**: Soporta 5 capas de hardware (Fn0 ~ Fn4), permitiendo diferentes configuraciones por capa.
*   **Personalización Avanzada**:
    *   **OSD**: Ajuste libremente la posición, tamaño, opacidad, duración, modo vertical, etc.
    *   **Tema**: Configure colores de fondo/resaltado/borde y fuentes personalizadas para el OSD de forma global, por capa o por perfil.
*   **Visualización de Iconos**: Cuando un programa se vincula a una tecla, su icono se muestra en lugar del texto.
*   **Asignación de Funciones Conveniente**:
    *   **Arrastrar y soltar**: Simplemente arrastre una función desde la Paleta de funciones y suéltela en una ranura de tecla.
    *   **Soltar archivo**: Arrastre un archivo ejecutable (.exe) a una ranura de tecla para crear un acceso directo de 'Ejecutar programa'. *(Nota: Puede no funcionar si se ejecuta como administrador.)*
*   **Detección Auto**: Asigne fácilmente señales de teclas mediante 'Detección auto'.
*   **Funciones Diversas**: Admite la ejecución de programas, macros de texto, control de medios, ajuste de volumen (sistema/aplicación activa), silenciamiento de micrófono, etc.
*   **RawInput**: Utiliza funciones nativas de Windows para detectar dispositivos.
*   **Icono de Bandeja**: Se ejecuta en segundo plano y ofrece un menú para un acceso rápido.

### Localización

*   **Idiomas Soportados**: Inglés, Coreano, Francés, Español, Chino, Alemán.
    *   *Nota: Las traducciones son generadas por IA y pueden no ser naturales.*
*   **Traducción Personalizada**: Puede modificar el archivo `languages.json` para corregir traducciones. **El programa prioriza el contenido de `languages.json` sobre los valores internos**, por lo que sus cambios se aplicarán inmediatamente al reiniciar.

### Cómo Usar

1.  **Ejecutar**: Ejecute `SayoOSD.exe`.
2.  **Conectar Dispositivo**: Haga clic en el botón **[Configuración]** para abrir la ventana de ajustes. El programa encuentra el dispositivo automáticamente.
3.  **Mapeo de Teclas**:
    1.  **Seleccionar Capa/Perfil**: Elija una capa de hardware (Fn0-Fn4) o un perfil de aplicación.
    2.  **Seleccionar Tecla y Renombrar**: Haga clic en una ranura de tecla, escriba el nombre y presione **Enter**.
    3.  **Registrar Señal**: Haga clic en **[Detección auto]** y presione la tecla física.
    4.  **Asignar Función**:
        *   **Arrastrar y soltar (Recomendado)**: Arrastre una función desde la **Paleta de funciones** de la izquierda a una ranura de tecla.
        *   **Soltar archivo**: Arrastre un archivo .exe a una ranura para crear un acceso directo.
        *   **Configuración Detallada**: Para 'Ejecutar programa' o 'Macro de texto', aparece un panel de detalles abajo. Ingrese la información y haga clic en **[Guardar]**.
    5.  **Desasignar**: Seleccione una ranura y haga clic en **[Desasignar]**.
4.  **Configuración OSD**:
    *   Haga clic en **[Configuración]**. Ajuste Opacidad, Tiempo, Modo en la ventana de ajustes.
    *   **Diseño**: Marque **Modo Vertical** o **Intercambiar filas**.
    *   **Mover**: Marque **Mover OSD** o mantenga presionada la tecla `Ctrl` y arrastre la ventana OSD.
    *   **Tema/Fuente**: Configure los colores y las fuentes en la pestaña 'Estilo OSD' de la configuración.
5.  **Otro**:
    *   **Iniciar con Windows**: Activar en configuración.

### Guardar y Restablecer Configuración

*   **Guardado Auto**: Toda la configuración (mapas, estilos, perfiles, etc.) se guarda en `settings.json`.
*   **Sincronización de Capas**: El programa no puede recuperar la capa actual del dispositivo al inicio. Sin embargo, la capa activa se guarda en `settings.json` cada vez que cambia. Al inicio, se restaura la última capa guardada. Si las capas del dispositivo y del OSD difieren, presionar una tecla asignada cambiará instantáneamente el OSD a la capa correcta.
*   **Restablecer**: Elimine `settings.json` y reinicie el programa para restablecer.

### Descargo de Responsabilidad

*   **Software No Oficial**: Este programa no está afiliado con SayoDevice y es una **herramienta no oficial**.
*   **Responsabilidad**: **El usuario asume toda la responsabilidad por el uso de este programa.** (Úselo bajo su propio riesgo.)

### Licencia

Este proyecto sigue la **Licencia MIT**.

**Licencias de terceros:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   Licencia: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   Consulte [Configuración] > [Licencias de código abierto] en la aplicación para obtener más detalles.
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Licencia Apache 2.0)
    *   Licencia: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   Consulte [Configuración] > [Licencias de código abierto] en la aplicación para obtener más detalles.

<p align="right">(<a href="#readme-top">Volver arriba</a>)</p>

---

<a id="chinese"></a>
## 中文

> **初始测试版**
> 目前仅支持 **SayoDevice 2x6v RGB** 型号。

专为 SayoDevice 键盘用户设计的**非官方** OSD（屏幕显示）工具。
它可以检测按键输入信号，并在屏幕上直观显示当前按键和层级信息。

### 主要功能

*   **OSD 显示**：在屏幕上显示被按下按键的名称或图标。
*   **应用配置 (虚拟层)**：根据当前活动的窗口自动切换按键配置。
*   **层级支持**：支持 5 个硬件层（Fn0 ~ Fn4），每层可设置不同的按键。
*   **高级自定义**:
    *   **OSD**: 自由调整位置、大小、透明度、显示时间、垂直模式等。
    *   **主题**: 可为 OSD 全局、每层或每个配置文件设置自定义的背景/高亮/边框颜色和字体。
*   **图标显示**: 当一个程序被链接到一个键时，它的图标会显示在 OSD 上而不是文本。
*   **便捷的功能分配**:
    *   **拖放**: 只需从功能面板拖动一个功能并将其放到一个按键槽中。
    *   **文件拖放**: 将一个可执行文件（.exe）拖到一个按键槽中，即可立即创建一个“运行程序”快捷方式。*（注意：如果以管理员身份运行，可能无法工作。）*
*   **自动检测**：通过“自动检测”功能轻松映射按键信号。
*   **多样化功能**：支持运行程序、文本宏、媒体控制、系统/活动应用音量调节、麦克风静音等。
*   **RawInput**：使用 Windows 原生功能检测设备。
*   **托盘图标**：在后台运行，并提供一个菜单以便快速访问。

### 本地化

*   **支持语言**：英语、韩语、法语、西班牙语、中文、德语。
    *   *注意：翻译由 AI 生成，可能不自然。*
*   **自定义翻译**：您可以修改 `languages.json` 文件以更正翻译。**程序优先使用 `languages.json` 的内容**，因此您的更改将在重启后立即生效。

### 使用方法

1.  **运行**：执行 `SayoOSD.exe`。
2.  **连接设备**：点击**[设置]**按钮打开设置窗口。程序会自动查找设备。您也可以手动输入 VID/PID 并点击**[应用]**。
3.  **按键映射与重命名**：
    1.  **选择层/配置**：选择一个硬件层（Fn0-Fn4）或一个应用配置。
    2.  **选择按键并重命名**：点击一个按键槽，输入名称，然后按 **Enter**。
    3.  **注册信号**：点击**[自动检测]**并按下物理按键。
    4.  **分配功能**：
        *   **拖放（推荐）**：从左侧的**功能面板**拖动一个功能到一个按键槽。
        *   **文件拖放**：将一个 .exe 文件拖到一个槽中以创建快捷方式。
        *   **详细设置**：对于“运行程序”或“文本宏”，底部会出现一个详细面板。输入信息并点击**[保存]**。
    5.  **取消映射**：选择一个槽位并点击**[取消映射]**。
4.  **OSD 设置**：
    *   点击**[设置]**按钮。在设置窗口中调整透明度、时间和模式。
    *   **布局**：勾选**垂直模式**或**交换行**。
    *   **移动**：勾选**允许移动 OSD** 或按住 `Ctrl` 键并拖动 OSD 窗口。
    *   **主题/字体**：在设置的“OSD 样式”选项卡中配置颜色和字体。
5.  **其他**：
    *   **开机自启**：在设置中启用。

### 设置保存与重置

*   **自动保存**：所有设置（映射、样式、配置等）都保存到 `settings.json`。
*   **层级同步**：程序启动时无法获取设备的当前层级信息。但是，每当层级发生变化时，都会立即保存到 `settings.json` 中。下次启动时将恢复上次保存的层级。即使设备和 OSD 的层级不同，按下按键也会立即切换到相应的层级。
*   **重置**：删除 `settings.json` 并重启程序以重置。

### 免责声明

*   **非官方软件**：本程序与 SayoDevice 无关，是一个**非官方工具**。
*   **责任**：**用户承担使用本程序的所有责任。**（风险自负。）

### 许可证

本项目遵循 **MIT 许可证**。

**第三方许可:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   许可证: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   有关详细信息，请参阅应用程序中的[设置] > [开源许可证]。
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Apache 2.0 许可证)
    *   许可证: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   有关详细信息，请参阅应用程序中的[设置] > [开源许可证]。

<p align="right">(<a href="#readme-top">回到顶部</a>)</p>

---

<a id="german"></a>
## Deutsch

> **Erste Beta-Version**
> Unterstützt derzeit nur das Modell **SayoDevice 2x6v RGB**.

Ein **inoffizielles** OSD (On-Screen Display) Dienstprogramm für SayoDevice-Tastaturbenutzer.
Es erkennt Tasteneingabesignale und zeigt die aktuelle Taste und Ebeneninformationen visuell auf dem Bildschirm an.

### Hauptfunktionen

*   **OSD-Anzeige**: Zeigt ein Fenster mit dem Namen oder Symbol der gedrückten Taste an.
*   **App-Profile (Virtuelle Ebenen)**: Wechselt automatisch die Tastenkonfigurationen basierend auf dem aktiven Fenster.
*   **Ebenen-Unterstützung**: Unterstützt 5 Hardware-Ebenen (Fn0 ~ Fn4) mit unterschiedlichen Tastennamen pro Ebene.
*   **Erweiterte Anpassung**:
    *   **OSD**: Passen Sie Position, Größe, Deckkraft, Dauer, vertikalen Modus usw. frei an.
    *   **Design**: Legen Sie benutzerdefinierte Hintergrund-/Hervorhebungs-/Randfarben und Schriftarten für das OSD global, pro Ebene oder pro Profil fest.
*   **Symbolanzeige**: Wenn ein Programm mit einer Taste verknüpft ist, wird dessen Symbol anstelle von Text angezeigt.
*   **Bequeme Funktionszuweisung**:
    *   **Drag & Drop**: Ziehen Sie einfach eine Funktion aus der Funktionspalette und legen Sie sie auf einem Tasten-Slot ab.
    *   **Datei-Drop**: Ziehen Sie eine ausführbare Datei (.exe) auf einen Tasten-Slot, um eine 'Programm ausführen'-Verknüpfung zu erstellen. *(Hinweis: Funktioniert möglicherweise nicht, wenn das Programm als Administrator ausgeführt wird.)*
*   **Auto-Erkennung**: Einfaches Zuordnen von Tastensignalen über 'Auto-Erkennung'.
*   **Vielfältige Funktionen**: Unterstützt das Ausführen von Programmen, Textmakros, Mediensteuerung, Lautstärkeregelung (System/aktive App), Mikrofonstummschaltung usw.
*   **RawInput**: Verwendet native Windows-Funktionen zur Geräteerkennung.
*   **Tray-Icon**: Läuft im Hintergrund und bietet ein Menü für schnellen Zugriff.

### Lokalisierung

*   **Unterstützte Sprachen**: Englisch, Koreanisch, Französisch, Spanisch, Chinesisch, Deutsch.
    *   *Hinweis: Übersetzungen sind KI-generiert und können unnatürlich sein.*
*   **Benutzerdefinierte Übersetzung**: Sie können die Datei `languages.json` ändern, um Übersetzungen zu korrigieren. **Das Programm priorisiert den Inhalt von `languages.json` gegenüber internen Standards**, sodass Ihre Änderungen nach dem Neustart sofort wirksam werden.

### Verwendung

1.  **Ausführen**: Starten Sie `SayoOSD.exe`.
2.  **Gerät verbinden**: Klicken Sie auf die Schaltfläche **[Einstellungen]**, um das Einstellungsfenster zu öffnen. Das Programm findet das Gerät automatisch.
3.  **Tastenbelegung**:
    1.  **Ebene/Profil wählen**: Wählen Sie eine Hardware-Ebene (Fn0-Fn4) oder ein App-Profil.
    2.  **Taste wählen & Umbenennen**: Klicken Sie auf einen Tasten-Slot, geben Sie den Namen ein und drücken Sie **Enter**.
    3.  **Signal registrieren**: Klicken Sie auf **[Auto-Erkennung]** und drücken Sie die physische Taste.
    4.  **Funktion zuweisen**:
        *   **Drag & Drop (Empfohlen)**: Ziehen Sie eine Funktion aus der linken **Funktionspalette** auf einen Tasten-Slot.
        *   **Datei-Drop**: Ziehen Sie eine .exe-Datei auf einen Slot, um eine Verknüpfung zu erstellen.
        *   **Detaillierte Einstellungen**: Für 'Programm ausführen' oder 'Textmakro' erscheint unten ein Detail-Panel. Geben Sie die Informationen ein und klicken Sie auf **[Speichern]**.
    5.  **Löschen**: Wählen Sie einen Slot und klicken Sie auf **[Löschen]**.
4.  **OSD-Einstellungen**:
    *   Klicken Sie auf **[Einstellungen]**. Passen Sie Deckkraft, Zeit und Modus im Einstellungsfenster an.
    *   **Layout**: **Vertikaler Modus** oder **Zeilen tauschen**.
    *   **Verschieben**: Aktivieren Sie **OSD verschieben** oder halten Sie die `Strg`-Taste gedrückt und ziehen Sie das OSD-Fenster.
    *   **Design/Schriftart**: Konfigurieren Sie Farben und Schriftarten im Tab 'OSD-Stil' der Einstellungen.
5.  **Sonstiges**:
    *   **Mit Windows starten**: In Einstellungen aktivieren.

### Einstellungen speichern & zurücksetzen

*   **Auto-Speichern**: Alle Einstellungen (Belegungen, Stile, Profile usw.) werden in `settings.json` gespeichert.
*   **Ebenen-Synchronisation**: Das Programm kann beim Start nicht die aktuelle Ebene des Geräts abrufen. Die aktive Ebene wird jedoch bei jeder Änderung in `settings.json` gespeichert. Beim Start wird die zuletzt gespeicherte Ebene wiederhergestellt. Wenn sich die Ebenen von Gerät und OSD unterscheiden, wechselt das OSD beim Drücken einer zugeordneten Taste sofort zur korrekten Ebene.
*   **Zurücksetzen**: Löschen Sie `settings.json` und starten Sie das Programm neu, um es zurückzusetzen.

### Haftungsausschluss

*   **Inoffizielle Software**: Dieses Programm ist nicht mit SayoDevice verbunden und ist ein **inoffizielles Tool**.
*   **Haftung**: **Der Benutzer übernimmt alle Verantwortung für die Verwendung dieses Programms.** (Benutzung auf eigene Gefahr.)

### Lizenz

Dieses Projekt folgt der **MIT-Lizenz**.

**Lizenzen von Drittanbietern:**
*   **NAudio** (v2.2.1) - Copyright 2020 Mark Heath (MIT License)
    *   Lizenz: https://www.nuget.org/packages/NAudio/2.2.1/License
    *   Siehe [Einstellungen] > [Open-Source-Lizenzen] in der App für Details.
*   **HidSharp** (v2.6.4) - Copyright © 2012-2021 James F. Bellinger (Apache 2.0 Lizenz)
    *   Lizenz: https://www.nuget.org/packages/HidSharp/2.6.4/License
    *   Siehe [Einstellungen] > [Open-Source-Lizenzen] in der App für Details.

<p align="right">(<a href="#readme-top">Nach oben</a>)</p>

---

*Developed for SayoDevice Users.*