# Заряд мыши и клавиатуры на FIFINE D6

Категория **Заряд устройств** содержит кнопку для **I/O Aurora** и кнопку для **Keychron Q1 HE**. Процент обновляется примерно раз в минуту, пока кнопка видна. Нажатие запускает обновление сразу.

- **I/O Aurora.** Плагин ищет интерфейс аккумулятора в проводном подключении и через USB-приёмник 2,4 ГГц. Приоритет у кабеля. В режиме, где мышь сообщает зарядку, экран показывает **ЗАРЯДКА**. Кабельный источник предстоит проверить на самой мыши. [Описание запроса Compx](https://github.com/sopparus/hitscan-battery#protocol)
- **Keychron Q1 HE ANSI Knob.** С модифицированной прошивкой кнопка читает текущий процент по кабелю и через приёмник Keychron Link 2,4 ГГц. При зарядке по кабелю появляется подпись **ЗАРЯДКА**. Со штатной прошивкой процент доступен по Bluetooth через Windows. [Модификация прошивки QMK](https://github.com/OutersGIT/q1he-battery-firmware) · [Как Windows получает заряд Bluetooth-устройств](https://learn.microsoft.com/en-us/windows-hardware/design/accessory-guidelines/bluetooth-accessory-guidelines/bluetooth-accessory-guidelines-battery-profiles-services)

Если очередное чтение не дало процента, кнопка показывает сохранённое значение с подписью **ПОСЛЕДНИЙ**. Значение сохраняется между перезапусками Control Deck. До первого успешного чтения на кнопке видна подпись **НЕТ ДАННЫХ**.

## Прошивка Keychron для USB и 2,4 ГГц

Используй сборку `keychron_q1_he_ansi_encoder_keychron.bin` для Q1 HE ANSI Knob с USB ID `3434:0B10`. Для этого варианта автор модификации проверил USB и 2,4 ГГц на клавиатуре. Сохрани свои назначения клавиш и штатную прошивку перед обновлением. Прошивается сама клавиатура. [Сборка и порядок прошивки](https://github.com/OutersGIT/q1he-battery-firmware/tree/03207a6de186d59f87b351f1326497578e0ae00c#flash-the-prebuilt-firmware-recommended-for-testers) · [Штатная прошивка Keychron](https://www.keychron.com/pages/firmware-and-json-files-of-the-keychron-qmk-q-he-and-q-he-8k-series-keyboards)

После прошивки нажми **Заряд клавиатуры** с переключателем в положении **Cable**. Затем переведи клавиатуру в **2.4G**, отключи её USB-кабель и ещё раз нажми кнопку на D6. По радио ожидание отчёта может занять несколько секунд. Когда клавиатура спит, разбуди её клавишей перед обновлением.

## Установка

Понадобятся Windows 10 или 11 и FIFINE Control Deck 3.10.201.0506 или новее.

1. Полностью закрой Control Deck через значок в трее.
2. Распакуй архив и скопируй папку `local.fifine.battery.sdPlugin` в `%APPDATA%\HotSpot\StreamDock\plugins`.
3. Запусти Control Deck. Перетащи **Заряд мыши** и **Заряд клавиатуры** из категории **Заряд устройств** на две свободные кнопки.

## Сборка

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1 -Plugin Battery
```

Архив `dist/fifine-battery-indicators-1.1.0.zip` содержит плагин и инструкцию.
