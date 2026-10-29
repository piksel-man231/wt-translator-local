# Готовые приложения — 1.1.0

ZIP-файлы находятся прямо в этом Git-репозитории и входят в скачивание исходников.
.NET Runtime уже включён: отдельно устанавливать .NET не требуется.

| Платформа | Скачать | Запуск |
| --- | --- | --- |
| Windows x64 | [WtTranslator-win-x64.zip](WtTranslator-win-x64.zip) | Распакуйте весь архив и откройте WtTranslator.App.exe. |
| macOS Apple Silicon | [WtTranslator-osx-arm64.zip](WtTranslator-osx-arm64.zip) | Распакуйте и перенесите WtTranslator.app в «Программы». |
| macOS Intel | [WtTranslator-osx-x64.zip](WtTranslator-osx-x64.zip) | Распакуйте и перенесите WtTranslator.app в «Программы». |

На странице ZIP в GitHub нажмите **Download raw file**, если скачивание не началось.
На macOS приложение имеет ad-hoc подпись без нотарификации Apple; может потребоваться
разрешить открытие в «Системные настройки → Конфиденциальность и безопасность».

Контрольные суммы: [SHA256SUMS.txt](SHA256SUMS.txt).
Подготовка игры и работа с переводами: [основной README](../README.md#как-пользоваться).

Эти файлы обновляются командой ./scripts/update-binaries.sh.
