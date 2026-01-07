# Stream Deck Media Manager

Плагин для Stream Deck, который отображает информацию о текущем воспроизводимом медиа в Windows.

## Функции

- Отображение названия трека
- Отображение списка авторов/исполнителей
- Отображение обложки альбома
- Автоматическое обновление каждые 3 секунды
- Обновление при нажатии на кнопку

## Требования

- Node.js 20 или выше
- .NET 8.0 SDK или выше (для сборки C# утилиты)
- Windows 10 или выше
- Stream Deck приложение версии 6.9 или выше

## Установка и сборка

### 1. Установите зависимости Node.js

```bash
npm install
```

### 2. Соберите C# утилиту

**Важно:** Без этого шага плагин не будет работать!

Перейдите в папку `MediaManagerHelper` и запустите:

```cmd
cd MediaManagerHelper
build.bat
```

Скрипт автоматически соберет проект и скопирует `MediaManagerHelper.exe` в нужную папку.

**Если скрипт не работает**, выполните вручную:

```cmd
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
copy /Y "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\MediaManagerHelper.exe" "..\ru.valentderah.media-manager.sdPlugin\bin\MediaManagerHelper.exe"
```

**Проверка:** Убедитесь, что файл `ru.valentderah.media-manager.sdPlugin\bin\MediaManagerHelper.exe` существует.

### 3. Соберите TypeScript код

```bash
npm run build
```

Это создаст `ru.valentderah.media-manager.sdPlugin/bin/plugin.js`.

## Установка плагина

### Автоматическая установка

```bash
npx streamdeck install ru.valentderah.media-manager.sdPlugin
```

### Ручная установка

1. Скопируйте папку `ru.valentderah.media-manager.sdPlugin` в:
   - Windows: `%appdata%\Elgato\StreamDeck\Plugins\`
2. Перезапустите Stream Deck приложение

## Разработка

Для разработки с автоматической перезагрузкой:

```bash
npm run watch
```

Это будет:
- Автоматически собирать проект при изменениях
- Перезапускать плагин в Stream Deck

## Использование

1. Откройте Stream Deck приложение
2. Найдите плагин "Media Manager" в списке действий
3. Перетащите действие "Media Info" на Stream Deck
4. Информация о медиа будет автоматически обновляться каждые 3 секунды
5. Нажмите на кнопку для немедленного обновления

## Логи

Логи плагина находятся в:
- Windows: `%appdata%\Elgato\StreamDeck\Plugins\ru.valentderah.media-manager.sdPlugin\logs\`

## Структура проекта

```
media-manager/
├── MediaManagerHelper/          # C# утилита для получения информации о медиа
│   ├── Program.cs
│   ├── MediaManagerHelper.csproj
│   └── build.bat
├── src/
│   ├── actions/
│   │   ├── increment-counter.ts # Пример действия (счетчик)
│   │   └── media-info.ts        # Действие для отображения информации о медиа
│   └── plugin.ts                # Главный файл плагина
└── ru.valentderah.media-manager.sdPlugin/
    ├── bin/
    │   ├── plugin.js            # Скомпилированный JavaScript
    │   └── MediaManagerHelper.exe # C# утилита
    └── manifest.json            # Манифест плагина
```

## Устранение неполадок

### Плагин не загружается

- Убедитесь, что `MediaManagerHelper.exe` находится в папке `bin/`
- Проверьте логи в папке `logs/`
- Убедитесь, что Node.js версии 20 установлен

### Ошибка "ENOENT" или "File Not Found"

Эта ошибка означает, что файл `MediaManagerHelper.exe` не найден. Решение:

1. Убедитесь, что вы собрали C# проект:
   ```cmd
   cd MediaManagerHelper
   build.bat
   ```

2. Проверьте, что файл существует:
   ```cmd
   dir "ru.valentderah.media-manager.sdPlugin\bin\MediaManagerHelper.exe"
   ```

3. Если файл отсутствует, скопируйте его вручную из папки сборки в `ru.valentderah.media-manager.sdPlugin\bin\`

### Информация о медиа не отображается

- Убедитесь, что медиа-плеер запущен и воспроизводит музыку
- Проверьте, что `MediaManagerHelper.exe` работает (запустите его вручную)
- Проверьте логи плагина

### Ошибки компиляции C#

- Убедитесь, что установлен .NET 8.0 SDK
- Проверьте, что проект использует правильный Target Framework

