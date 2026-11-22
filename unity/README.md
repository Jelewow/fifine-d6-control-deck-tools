# Кнопки Unity для FIFINE D6

- **Play Mode** запускает проект в редакторе. Повторное нажатие возвращает Unity в Edit Mode.
- **Пауза** останавливает и продолжает выполнение в Play Mode.
- **Следующий кадр** выполняет один кадр и оставляет Play Mode на паузе. [Кнопки Unity Editor](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-navigating-managing/toolbar)

Если открыт один редактор Unity, кнопки работают и в фоне. При нескольких открытых проектах кнопки управляют редактором в фокусе.

## Установка

Понадобятся Windows 10 или 11, Unity 6 и FIFINE Control Deck 3.10.201.0506 или новее.

1. Распакуй архив. В каждом нужном проекте Unity открой **Package Manager → + → Add package from disk** и укажи файл `package/package.json` из архива. При работе из репозитория укажи `unity/package/package.json`. [Установка локального пакета Unity](https://docs.unity.com/en-us/engine/6000.0/manual/packages-list/managing-packages-window/upm-ui-actions/upm-ui-local)
2. Полностью закрой Control Deck через значок в трее. Скопируй папку `local.fifine.unitycontrols.sdPlugin` из архива в `%APPDATA%\HotSpot\StreamDock\plugins` и запусти Control Deck.
3. Перетащи **Play Mode**, **Пауза** и **Следующий кадр** из категории **Unity** на кнопки D6.

После принятия команды на кнопке появляется галочка. Восклицательный знак означает, что редактор недоступен или действие сейчас нельзя выполнить.

## Сборка

Запусти из корня репозитория.

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1 -Plugin Unity
```

Архив `dist/fifine-unity-controls-1.0.0.zip` содержит плагин для Control Deck, пакет Unity Editor и инструкцию.
