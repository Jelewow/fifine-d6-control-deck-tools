# Заряд мыши и клавиатуры на FIFINE D6

Категория **Заряд устройств** содержит кнопку для **I/O Aurora** и кнопку для **Keychron Q1 HE**. Процент обновляется примерно раз в минуту, пока кнопка видна. Нажатие запускает обновление сразу.

- **I/O Aurora.** Плагин ищет интерфейс аккумулятора в проводном подключении и через USB-приёмник 2,4 ГГц. Приоритет у кабеля. В режиме, где мышь сообщает зарядку, экран показывает **ЗАРЯДКА**. Кабельный источник предстоит проверить на самой мыши. [Описание запроса Compx](https://github.com/sopparus/hitscan-battery#protocol)
- **Keychron Q1 HE.** Со штатной прошивкой плагин берёт заряд из данных Windows при подключении по Bluetooth. Через кабель или Keychron Link остаётся последнее значение, полученное по Bluetooth. [Как Windows получает заряд Bluetooth-устройств](https://learn.microsoft.com/en-us/windows-hardware/design/accessory-guidelines/bluetooth-accessory-guidelines/bluetooth-accessory-guidelines-battery-profiles-services)

Если очередное чтение не дало процента, кнопка показывает сохранённое значение с подписью **ПОСЛЕДНИЙ**. Значение сохраняется между перезапусками Control Deck. До первого успешного чтения на кнопке видна подпись **НЕТ ДАННЫХ**.

## Установка

Понадобятся Windows 10 или 11 и FIFINE Control Deck 3.10.201.0506 или новее.

1. Полностью закрой Control Deck через значок в трее.
2. Распакуй архив и скопируй папку `local.fifine.battery.sdPlugin` в `%APPDATA%\HotSpot\StreamDock\plugins`.
3. Запусти Control Deck. Перетащи **Заряд мыши** и **Заряд клавиатуры** из категории **Заряд устройств** на две свободные кнопки.

## Сборка

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1 -Plugin Battery
```

Архив `dist/fifine-battery-indicators-1.0.0.zip` содержит плагин и инструкцию.
