using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using WtTranslator.Core;

namespace WtTranslator.App;

public partial class MainWindow : Window
{
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WtTranslator.Local");
    static readonly string ProfilesDir = Path.Combine(DataDir, "profiles");
    static readonly string SettingsPath = Path.Combine(DataDir, "settings.json");

    string _profile = "default";
    bool _switching;
    string ProfileDir => Path.Combine(ProfilesDir, _profile);
    string OverlayPath => Path.Combine(ProfileDir, "overlay.json");
    Overlay _overlay = new();
    LangIndex? _index;
    LangRow? _selected;
    bool _deleteArmed;
    DispatcherTimer? _debounce;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(ProfilesDir);
        _profile = ReadSettings().Profile is { Length: > 0 } p ? p : "default";
        _overlay = LoadOverlay();
        SetProfiles(LocalProfiles());
        PathBox.Text = Environment.GetEnvironmentVariable("WT_LANG_DIR") ?? ReadSettings().LangDir;
        LoadIndex();

        ProfileBox.SelectionChanged += (_, _) => { if (!_switching && ProfileBox.SelectedItem is string n) SwitchProfile(n); };
        RefreshBtn.Click += (_, _) => { SetProfiles(LocalProfiles()); LoadIndex(); };
        NewBtn.Click += (_, _) => Safely(CreateProfile);
        BrowseBtn.Click += async (_, _) => await Browse();
        PathBox.LostFocus += (_, _) => { SaveSetting(); LoadIndex(); };
        SearchBox.TextChanged += (_, _) =>
        {
            _debounce?.Stop();
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _debounce.Tick += (_, _) => { _debounce!.Stop(); RunSearch(); };
            _debounce.Start();
        };
        Results.SelectionChanged += (_, _) => Select(Results.SelectedItem as LangRow);
        AddBtn.Click += (_, _) => Safely(AddToPatch);
        RemoveBtn.Click += (_, _) => Safely(RemoveFromPatch);
        ApplyBtn.Click += (_, _) => Safely(Apply);
        ImportBtn.Click += async (_, _) => await ImportPatch();
        ExportBtn.Click += async (_, _) => await ExportPatch();
        DeleteBtn.Click += async (_, _) => await DeleteLang();
    }

    sealed class Settings { public string LangDir { get; set; } = ""; public string Profile { get; set; } = ""; public string AppliedProfile { get; set; } = ""; }

    static Settings ReadSettings()
    {
        try { return File.Exists(SettingsPath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new() : new(); }
        catch { return new(); }
    }

    static void WriteSettings(Settings s) => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));

    void SaveSetting()
    {
        var s = ReadSettings(); s.LangDir = PathBox.Text ?? "";
        WriteSettings(s);
    }

    Overlay LoadOverlay() => File.Exists(OverlayPath) ? Overlay.FromJson(File.ReadAllText(OverlayPath)) : new();

    static List<string> LocalProfiles() =>
        Directory.GetDirectories(ProfilesDir).Select(Path.GetFileName).Where(n => n != null).Select(n => n!).Append("default").Distinct().Order().ToList();

    void SetProfiles(IEnumerable<string> names)
    {
        _switching = true;
        var list = names.Append(_profile).Distinct().Order().ToList();
        ProfileBox.ItemsSource = list;
        ProfileBox.SelectedItem = _profile;
        _switching = false;
    }

    void SwitchProfile(string name)
    {
        _profile = name;
        _overlay = LoadOverlay();
        var s = ReadSettings(); s.Profile = name; WriteSettings(s);
        Select(null);
        Say($"Версия «{name}»: правок {_overlay.Count}.");
    }

    void CreateProfile()
    {
        var name = (NewName.Text ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[\w-]{1,40}$")) { Say("Имя версии: 1-40 символов, буквы, цифры, - и _."); return; }
        if (Directory.Exists(Path.Combine(ProfilesDir, name))) { Say($"Версия «{name}» уже есть."); return; }
        var keep = _overlay;
        _profile = name; Directory.CreateDirectory(ProfileDir);
        _overlay = CopyBox.IsChecked == true ? keep : new();
        SaveOverlay();
        var s = ReadSettings(); s.Profile = name; WriteSettings(s);
        SetProfiles(LocalProfiles());
        Select(null);
        NewName.Text = "";
        Say($"Создана версия «{name}» ({_overlay.Count} правок).");
    }

    string LangDir => (PathBox.Text ?? "").Trim();

    void Say(string s) => Status.Text = s;

    void Safely(Action action)
    {
        try { action(); }
        catch (Exception e) { Say("Ошибка: " + e.Message); }
    }

    void LoadIndex()
    {
        _index = null; Results.ItemsSource = null; Select(null);
        if (!Directory.Exists(LangDir)) { Say(LangDir == "" ? "Укажи папку lang" : "Папки lang нет: игра создаст её при запуске"); return; }
        try
        {
            _index = LangIndex.Load(LangDir);
            Say($"Загружено строк: {_index.Rows.Count}. В патче: {_overlay.Count}");
        }
        catch (Exception e) { Say("Ошибка чтения lang: " + e.Message); }
        RunSearch();
    }

    async Task Browse()
    {
        var r = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Папка lang" });
        if (r.Count == 0) return;
        PathBox.Text = r[0].Path.LocalPath;
        SaveSetting(); LoadIndex();
    }

    static readonly FilePickerFileType PatchFileType = new("Патч WT Translator (JSON)")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" },
        AppleUniformTypeIdentifiers = new[] { "public.json" }
    };

    async Task ExportPatch()
    {
        try
        {
            // Capture the saved profile before opening a native dialog.
            var profile = _profile;
            var json = _overlay.ToJson();
            var count = _overlay.Count;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт патча",
                SuggestedFileName = profile + ".json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { PatchFileType },
                ShowOverwritePrompt = true
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using (var writer = new StreamWriter(stream))
                await writer.WriteAsync(json);
            Say($"Экспортирована версия «{profile}»: {count} правок в {file.Name}.");
        }
        catch (Exception e) { Say("Ошибка экспорта патча: " + e.Message); }
    }

    async Task ImportPatch()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Импорт патча в новую версию",
                AllowMultiple = false,
                FileTypeFilter = new[] { PatchFileType }
            });
            if (files.Count == 0) return;
            Overlay imported;
            await using (var stream = await files[0].OpenReadAsync())
            using (var reader = new StreamReader(stream))
                imported = Overlay.FromJson(await reader.ReadToEndAsync());

            var stem = System.Text.RegularExpressions.Regex.Replace(
                Path.GetFileNameWithoutExtension(files[0].Name), @"[^\w-]", "-").Trim('-');
            if (stem.Length == 0) stem = "import";
            if (stem.Length > 30) stem = stem[..30];
            var name = stem;
            for (var suffix = 2; name == "default" || Directory.Exists(Path.Combine(ProfilesDir, name)); suffix++)
                name = stem + "-" + suffix;
            var dir = Path.Combine(ProfilesDir, name);
            Directory.CreateDirectory(dir);
            try { await File.WriteAllTextAsync(Path.Combine(dir, "overlay.json"), imported.ToJson()); }
            catch { Directory.Delete(dir, true); throw; }
            _profile = name;
            _overlay = imported;
            var settings = ReadSettings(); settings.Profile = name; WriteSettings(settings);
            SetProfiles(LocalProfiles());
            Select(null);
            Say($"Импортирована версия «{name}»: {_overlay.Count} правок. Нажми «Применить патч».");
        }
        catch (Exception e) { Say("Ошибка импорта патча: " + e.Message); }
    }

    void RunSearch() => Results.ItemsSource = _index?.Search(SearchBox.Text ?? "");

    bool InPatch(LangRow r) => _overlay.Files.TryGetValue(r.File, out var f) && f.ContainsKey(r.Key);

    void Select(LangRow? r)
    {
        _selected = r;
        NewText.IsEnabled = AddBtn.IsEnabled = r != null;
        RemoveBtn.IsEnabled = r != null && InPatch(r);
        if (r == null) { NewText.Text = ""; SelInfo.Text = "Выбери строку из списка"; return; }
        SelInfo.Text = $"{r.Title}\nEN: {r.En}";
        NewText.Text = InPatch(r) ? _overlay.Files[r.File][r.Key].Value : r.Ru;
    }

    void SaveOverlay() { Directory.CreateDirectory(ProfileDir); File.WriteAllText(OverlayPath, _overlay.ToJson()); }

    void AddToPatch()
    {
        if (_selected is not { } r) return;
        if (!_overlay.Files.TryGetValue(r.File, out var f)) _overlay.Files[r.File] = f = new();
        // keep the original hash if this string is already patched (the folder's Ru is then our text, not the game's)
        var baseHash = f.TryGetValue(r.Key, out var old) ? old.BaseHash : Patcher.Hash(r.Ru);
        f[r.Key] = new OverlayEntry(NewText.Text ?? "", baseHash);
        SaveOverlay();
        RemoveBtn.IsEnabled = true;
        Say($"Добавлено в патч. Всего правок: {_overlay.Count}. Нажми «Применить патч».");
    }

    void RemoveFromPatch()
    {
        if (_selected is not { } r || !InPatch(r)) return;
        _overlay.Files[r.File].Remove(r.Key);
        if (_overlay.Files[r.File].Count == 0) _overlay.Files.Remove(r.File);
        SaveOverlay();
        RemoveBtn.IsEnabled = false;
        Say($"Убрано из патча. Всего правок: {_overlay.Count}. Чтобы вернуть оригинал в игре, удали lang и дай игре создать её заново.");
    }

    void Apply()
    {
        if (!Directory.Exists(LangDir)) { Say("Папки lang нет"); return; }
        var st = ReadSettings();
        var warn = st.AppliedProfile is { Length: > 0 } a && a != _profile ? $" ВНИМАНИЕ: в lang уже применена версия «{a}», её правки остались. Для чистого результата удали lang и примени заново." : "";
        var rep = Patcher.Apply(LangDir, _overlay);
        st.AppliedProfile = _profile; WriteSettings(st);
        LoadIndex();
        Say($"Версия «{_profile}» применена: {rep.Applied}. Изменился оригинал в игре: {rep.Conflicts.Count}. Не найдено ключей: {rep.MissingKeys.Count}.{warn}");
    }

    async Task DeleteLang()
    {
        var dir = LangDir;
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            DeleteBtn.Content = "Точно удалить?";
            await Task.Delay(4000);
            _deleteArmed = false; DeleteBtn.Content = "Удалить папку lang";
            return;
        }
        _deleteArmed = false; DeleteBtn.Content = "Удалить папку lang";
        // guard: only a folder that really looks like a game's lang
        if (!Directory.Exists(dir) || Path.GetFileName(dir.TrimEnd('/', '\\')) != "lang" || !File.Exists(Path.Combine(dir, "localization.blk")))
        { Say("Не удаляю: это не папка lang игры (нет localization.blk)."); return; }
        try { Directory.Delete(dir, true); }
        catch (Exception e) { Say("Не удалось удалить lang: " + e.Message); return; }
        var cur = ReadSettings(); cur.AppliedProfile = ""; WriteSettings(cur);
        _index = null; Results.ItemsSource = null; Select(null);
        Say("Папка lang удалена. Запусти игру, чтобы она создала новую, затем нажми «Применить патч».");
    }

}
