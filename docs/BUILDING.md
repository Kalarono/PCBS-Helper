# Сборка PCBS-Helper

Нужны Windows, PC Building Simulator 1 и BepInEx 5 x64. Проверенная версия игры:
1.15.3. Используется компилятор C# из .NET Framework в Windows; отдельный SDK
не требуется. Ссылки на игровые DLL берутся из локальной установки игры.

## Сборка DLL

Запустите PowerShell из корня репозитория:

```powershell
.\Build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PC Building Simulator'
```

Результат: `build/package/PCBS-Helper/PCBS-Helper.dll`. HTML и JavaScript
калькулятора уже встроены в DLL.

Для локальной установки закройте игру и выполните:

```powershell
.\Build.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PC Building Simulator' -Install
```

Предыдущая DLL сохраняется в `build/backups/`. Скрипт установки хелпера
не меняет игровые DLL и сохранения.

Вместо параметра `-GamePath` можно использовать переменную `PCBS_GAME_PATH`
или локальный `build.local.json`, исключённый из Git:

```json
{ "gamePath": "D:\\SteamLibrary\\steamapps\\common\\PC Building Simulator" }
```

## Подготовка релизного пакета

```powershell
.\scripts\Package-Release.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\PC Building Simulator'
```

Скрипт собирает DLL, загружает официальный архив BepInEx 5.4.23.5 для Windows
x64 и проверяет SHA-256. Уже скачанный архив можно указать параметром
`-BepInExArchivePath`. Подготовка пакета не устанавливает его в игру и ничего
не публикует.

В `build/release/<версия>/` создаются:

- `PCBS-Helper-<версия>-Full.zip` с BepInEx, хелпером и инструкциями.
- `PCBS-Helper.dll` для обновления существующей установки.
- Официальный архив `BepInEx_win_x64_5.4.23.5.zip`.
- `README.txt` и `README_EN.txt`.

Полный архив содержит лицензию BepInEx. Игровые DLL, сторонние моды,
сохранения, настройки пользователя и локальные материалы разработки
в релизный пакет не входят.

Версия задаётся в атрибуте `BepInPlugin` файла `src/PCBSCalculator.cs`.
Перед упаковкой обновите CHANGELOG и обе пары README. Проверки и внутренние
заметки разработчика хранятся локально и не нужны для обычной сборки.
