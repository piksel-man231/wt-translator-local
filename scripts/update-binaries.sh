#!/usr/bin/env bash
# Rebuild all committed desktop ZIPs. Run on macOS for native bundle signing.
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Run on macOS: macOS bundles require codesign and ditto." >&2
  exit 1
fi
VERSION="$(dotnet msbuild src/WtTranslator.App -getProperty:Version -nologo)"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "The application Version must have the form 1.2.3." >&2
  exit 1
fi
dotnet build WtTranslator.sln -c Release
dotnet test WtTranslator.sln -c Release --no-build
./scripts/package-macos.sh osx-arm64 "$VERSION"
./scripts/package-macos.sh osx-x64 "$VERSION"
# Clear only generated publish output to avoid keeping files from an older build.
rm -rf out/win-x64
dotnet publish src/WtTranslator.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:Version="$VERSION" -o out/win-x64
find out/win-x64 -name '*.pdb' -delete
mkdir -p out/binaries-staging binaries
ditto -c -k out/win-x64 out/binaries-staging/WtTranslator-win-x64.zip
cp "dist/WtTranslator-$VERSION-osx-arm64.zip" out/binaries-staging/WtTranslator-osx-arm64.zip
cp "dist/WtTranslator-$VERSION-osx-x64.zip" out/binaries-staging/WtTranslator-osx-x64.zip
# Replace tracked archives only after every build succeeds.
cp out/binaries-staging/*.zip binaries/
(cd binaries && shasum -a 256 WtTranslator-win-x64.zip WtTranslator-osx-arm64.zip WtTranslator-osx-x64.zip > SHA256SUMS.txt)
cat > binaries/README.md <<DOC
# Готовые приложения — $VERSION

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
DOC
echo "Updated binaries/ for version $VERSION. Commit source and ZIPs together, then run ./scripts/push-all.sh."
