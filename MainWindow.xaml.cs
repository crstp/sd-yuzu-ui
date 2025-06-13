using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ImageMagick;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Reflection;
using System.Text.Json;
using System.Globalization;
using System.Windows.Data;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Runtime.InteropServices;

/**
 * ***** WARNING *****
 * This source code was written using an AI. It's not the kind of code that humans should read. I think it's better to stop reading here unless you really need to.
 * このソースコードはAIを利用して書かれました。とても人間が読むようなコードではありません。何か変える必要があるのでなければ読むのはやめた方がいいと思います
 * ***** WARNING *****
 */
namespace SD.Yuzu
{
    public class OptimizedImageConverter : IValueConverter
    {
        // 画像キャッシュを管理する静的辞書（WeakReferenceを使用してメモリリーク防止）
        private static readonly Dictionary<string, WeakReference> _imageCache = new Dictionary<string, WeakReference>();
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string imagePath && File.Exists(imagePath))
            {
                try
                {
                    // キャッシュから既存のBitmapImageを取得を試行
                    if (_imageCache.TryGetValue(imagePath, out var weakRef) && weakRef.IsAlive)
                    {
                        var cachedBitmap = weakRef.Target as BitmapImage;
                        if (cachedBitmap != null)
                        {
                            return cachedBitmap;
                        }
                    }
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    
                    // メモリ効率を改善する設定（キャッシュを有効に変更）
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // メモリにキャッシュしてファイルハンドルを解放
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; // IgnoreImageCacheを削除
                    
                    bitmap.EndInit();
                    bitmap.Freeze(); // パフォーマンス改善のため凍結
                    
                    // WeakReferenceでキャッシュに保存（メモリリーク防止）
                    _imageCache[imagePath] = new WeakReference(bitmap);
                    
                    // 古いWeakReferenceをクリーンアップ
                    CleanupCache();
                    
                    return bitmap;
                }
                catch
                {
                    return DependencyProperty.UnsetValue;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        
        private static void CleanupCache()
        {
            // ガベージコレクションされたエントリを定期的に削除
            var keysToRemove = _imageCache.Where(kvp => !kvp.Value.IsAlive).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                _imageCache.Remove(key);
            }
        }
    }

    // Boolean値を逆転してVisibilityに変換するコンバーター
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // ウィンドウ設定を保存するためのクラス
    public class WindowSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public double SplitterPosition { get; set; } = 350; // デフォルト値
    }

    // 最後のGenerate設定を保存するためのクラス
    public class LastGenerateSettings
    {
        public string LastPrompt { get; set; } = "";
        public string LastNegativePrompt { get; set; } = "";
        public int LastWidth { get; set; } = 512;
        public int LastHeight { get; set; } = 512;
        public int LastSteps { get; set; } = 20;
        public double LastCfgScale { get; set; } = 7.0;
        public double LastDenoisingStrength { get; set; } = 0.7;
        public string LastSelectedSamplingMethod { get; set; } = "Euler a";
        public string LastSelectedScheduleType { get; set; } = "Automatic";
        public long LastSeed { get; set; } = -1;
        public long LastSubseed { get; set; } = -1;
        public bool LastEnableHiresFix { get; set; } = false;
        public string LastSelectedUpscaler { get; set; } = "Latent (nearest-exact)";
        public int LastHiresSteps { get; set; } = 6;
        public double LastHiresUpscaleBy { get; set; } = 1.5;
        public int LastHiresResizeWidth { get; set; } = 1024;
        public int LastHiresResizeHeight { get; set; } = 1024;
        public bool LastCombinatorialGeneration { get; set; } = false;
        
        // Kohya hires.fix関連のプロパティ
        public bool LastEnableKohyaHiresFix { get; set; } = false;
        public int LastKohyaBlockNumber { get; set; } = 3;
        public double LastKohyaDownscaleFactor { get; set; } = 1.75;
        public bool LastKohyaAlwaysEnableCondition { get; set; } = false;
        public int LastKohyaConditionShortSide { get; set; } = 1280;
        public int LastKohyaConditionLongSide { get; set; } = 1420;
        
        // Random Resolution関連のプロパティ
        public bool LastEnableRandomResolution { get; set; } = false;
    }

    // 選択されたタブの状態を保存するためのクラス
    public class TabSelectionSettings
    {
        public int SelectedOuterTabIndex { get; set; } = 0;
        public int SelectedInnerTabIndex { get; set; } = 0;
    }

    public class ResolutionItem
    {
        public int Width { get; set; }
        public int Height { get; set; }
        
        public ResolutionItem() { }
        
        public ResolutionItem(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public class RandomResolutionSettings
    {
        public string ModelType { get; set; } = "SDXL";
        public List<ResolutionItem> CurrentResolutions { get; set; } = new List<ResolutionItem>
        {
            new ResolutionItem(1024, 1360), 
            new ResolutionItem(1360, 1024), 
            new ResolutionItem(1360, 1360),
        };
        public string WeightMode { get; set; } = "Equal Weights";
        public int MinDim { get; set; } = 832;
        public int MaxDim { get; set; } = 1216;
    }

    /// <summary>
    /// IsGeneratingプロパティに基づいてボタンの背景色を変更するコンバーター
    /// </summary>
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGenerating)
            {
                // Running状態（IsGenerating = true）の時はグレー、通常時はオレンジ
                return isGenerating ? new SolidColorBrush(Color.FromRgb(128, 128, 128)) : new SolidColorBrush(Color.FromRgb(255, 107, 53));
            }
            // デフォルトはオレンジ
            return new SolidColorBrush(Color.FromRgb(255, 107, 53));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// タブ数に応じて幅を調整するコンバーター
    /// </summary>
    public class TabWidthConverter : IMultiValueConverter
    {
        public double MinWidth { get; set; } = 20.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is double totalWidth &&
                values[1] is int count &&
                count > 0)
            {
                // 追加ボタン分の幅を差し引く
                double available = Math.Max(0, totalWidth - 30);
                double width = available / count;
                if (width < MinWidth) width = MinWidth;
                return width;
            }

            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// タブの幅に応じて閉じるボタンの表示を切り替えるコンバーター
    /// </summary>
    public class CloseButtonVisibilityConverter : IMultiValueConverter
    {
        public double Threshold { get; set; } = 60.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is bool isGenerating &&
                values[1] is double width)
            {
                if (isGenerating) return Visibility.Collapsed;
                return width < Threshold ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _dragStartPoint;
        private Point _innerDragStartPoint;
        private DateTime _lastClickTime = DateTime.MinValue;
        private TabItemViewModel? _lastClickedTab = null;
        private DateTime _lastGCTime = DateTime.MinValue; // 最後にGCを実行した時刻
        
        // メモリ監視用フィールド
        private DispatcherTimer? _memoryMonitorTimer;
        private const long MEMORY_THRESHOLD_BYTES = 3 * 1024L * 1024L * 1024L; // 3GB
        
        // バージョンチェック関連
        private VersionCheckManager? _versionCheckManager;

        // Dynamic Prompts スクリプト名
        private string? _dynamicPromptScriptName = null;
        private string? _kohyaHiresFixScriptName = null;
        private string? _randomResolutionScriptName = null;

        // Random Resolution設定（グローバル）
        public static RandomResolutionSettings GlobalRandomResolutionSettings { get; set; } = new RandomResolutionSettings();

        // ショートカットオーバーレイの表示状態
        public static readonly DependencyProperty IsShortcutsOverlayVisibleProperty =
            DependencyProperty.Register("IsShortcutsOverlayVisible", typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));

        public bool IsShortcutsOverlayVisible
        {
            get { return (bool)GetValue(IsShortcutsOverlayVisibleProperty); }
            set { SetValue(IsShortcutsOverlayVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsImagePanelFullscreenProperty =
            DependencyProperty.Register("IsImagePanelFullscreen", typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));

        public bool IsImagePanelFullscreen
        {
            get { return (bool)GetValue(IsImagePanelFullscreenProperty); }
            set { SetValue(IsImagePanelFullscreenProperty, value); }
        }

        // 画像列数管理のDependencyProperty
        public static readonly DependencyProperty ImageGridColumnsProperty =
            DependencyProperty.Register("ImageGridColumns", typeof(int), typeof(MainWindow),
                new PropertyMetadata(4)); // デフォルトは4列

        public int ImageGridColumns
        {
            get { return (int)GetValue(ImageGridColumnsProperty); }
            set { SetValue(ImageGridColumnsProperty, value); }
        }

        public static readonly DependencyProperty IsLeftPanelVisibleProperty =
            DependencyProperty.Register("IsLeftPanelVisible", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        public bool IsLeftPanelVisible
        {
            get { return (bool)GetValue(IsLeftPanelVisibleProperty); }
            set { SetValue(IsLeftPanelVisibleProperty, value); }
        }

        public MainWindow()
        {
            InitializeComponent();
            
            var vm = (MainViewModel)DataContext;
            vm.GenerateCommand = new RelayCommand(async _ => await GenerateImagesAsync(), _ => vm.SelectedTab?.IsButtonEnabled == true);
            vm.InterruptCommand = new RelayCommand(async _ => await InterruptGenerationAsync(), _ => true);
            vm.PropertyChanged += MainViewModel_PropertyChanged;
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
            
            // キーボードイベントを設定
            this.KeyDown += MainWindow_KeyDown;
            
            // メモリ監視タイマーを初期化
            InitializeMemoryMonitor();
            
            // バージョンチェック機能を初期化
            InitializeVersionCheck();
        }

        private string GetDataFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "tabs_data.json");
        }

        private string GetImageCacheDir()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var imageCacheDir = Path.Combine(dir, "image_cache");
            Directory.CreateDirectory(imageCacheDir); // image_cacheディレクトリが存在しない場合は作成
            return imageCacheDir;
        }

        private string GetWindowSettingsFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "window_settings.json");
        }

        private string GetLastGenerateSettingsFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "last_generate_settings.json");
        }

        private string GetTabSelectionSettingsFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "tab_selection_settings.json");
        }

        private string GetRandomResolutionSettingsFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "random_resolution_settings.json");
        }

        private void LoadWindowSettings()
        {
            try
            {
                var filePath = GetWindowSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                    
                    if (settings != null)
                    {
                        // 画面境界内かどうかをチェック
                        var screenWidth = SystemParameters.PrimaryScreenWidth;
                        var screenHeight = SystemParameters.PrimaryScreenHeight;
                        
                        // ウィンドウが画面外に出ないようにチェック
                        if (settings.Left >= 0 && settings.Top >= 0 && 
                            settings.Left + settings.Width <= screenWidth && 
                            settings.Top + settings.Height <= screenHeight)
                        {
                            this.Left = settings.Left;
                            this.Top = settings.Top;
                        }
                        
                        // サイズの最小値をチェック
                        if (settings.Width >= this.MinWidth && settings.Height >= this.MinHeight)
                        {
                            this.Width = settings.Width;
                            this.Height = settings.Height;
                        }
                        
                        // WindowStateを設定（最大化状態も含めて）
                        this.WindowState = settings.WindowState;
                        
                        // Splitterの位置を復元（ウィンドウが読み込まれた後に実行）
                        this.Dispatcher.BeginInvoke(new Action(() => {
                            SetSplitterPosition(settings.SplitterPosition);
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                        
                        Debug.WriteLine($"ウィンドウ設定を復元: 位置({settings.Left}, {settings.Top}) サイズ({settings.Width}x{settings.Height}) 状態({settings.WindowState})");
                    }
                }
            }
            catch (Exception ex)
            {
                // 設定読み込みに失敗した場合はデフォルト値を使用
                Debug.WriteLine($"ウィンドウ設定の読み込みに失敗しました: {ex.Message}");
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                var settings = new WindowSettings();
                
                // 最大化状態の場合、RestoreBoundsを使用して通常状態の位置とサイズを取得
                if (this.WindowState == WindowState.Maximized)
                {
                    settings.Left = this.RestoreBounds.Left;
                    settings.Top = this.RestoreBounds.Top;
                    settings.Width = this.RestoreBounds.Width;
                    settings.Height = this.RestoreBounds.Height;
                }
                else
                {
                    settings.Left = this.Left;
                    settings.Top = this.Top;
                    settings.Width = this.Width;
                    settings.Height = this.Height;
                }
                
                settings.WindowState = this.WindowState;
                
                // 左側パネルが表示されている場合のみSplitterの位置を保存
                if (IsLeftPanelVisible)
                {
                    settings.SplitterPosition = GetCurrentSplitterPosition();
                }
                // 左側パネルが隠れている場合は既存の位置を保持（保存しない）

                var filePath = GetWindowSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ウィンドウ設定の保存に失敗しました: {ex.Message}");
            }
        }

        private WindowSettings? LoadWindowSettingsFromFile()
        {
            try
            {
                var filePath = GetWindowSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ウィンドウ設定の読み込みに失敗しました: {ex.Message}");
            }
            return null;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWindowSettings();
            
            // app_settings.jsonから画像列数を読み込み
            ImageGridColumns = AppSettings.Instance.DefaultImageGridColumns;
            
            var vm = (MainViewModel)DataContext;
            vm.LoadFromFile(GetDataFilePath());
            LoadLastGenerateSettings();
            LoadTabSelectionSettings();
            
            // Random Resolution設定を読み込み
            GlobalRandomResolutionSettings = LoadRandomResolutionSettings();
            
            // ViewModelにRandom Resolution設定を反映
            vm.RefreshRandomResolutionText();
            
            // Undo履歴を読み込み
            vm.LoadUndoHistory();
            System.Diagnostics.Debug.WriteLine($"Undo履歴読み込み完了: {vm.GetUndoHistoryCount()}件");
            
            // メモリ監視の初期化
            InitializeMemoryMonitor();
            
            // 設定の検証を実行し、問題があれば設定ウィンドウを表示
            await CheckAndShowSettingsIfNeededAsync();
            
            // 拡張機能スクリプトの検索を非同期で実行
            _ = Task.Run(async () => await InitializeExtensionScriptsAsync());
            
            // サンプラーリストの初期化を非同期で実行
            _ = Task.Run(async () => await InitializeSamplersAsync());
            
            // スケジューラーリストの初期化を非同期で実行
            _ = Task.Run(async () => await InitializeSchedulersAsync());
            
            // Upscalerリストの初期化を非同期で実行
            _ = Task.Run(async () => await InitializeUpscalersAsync());
            
            // Checkpointリストの初期化を非同期で実行
            _ = Task.Run(async () => await InitializeCheckpointsAsync());
            
            // タブのSampler/Scheduler/Upscalerリストの初期化を非同期で実行
            _ = Task.Run(async () =>
            {
                // 少し待機してManagerの初期化を先に完了させる
                await Task.Delay(500);
                
                // 全タブのリストを初期化
                await Dispatcher.InvokeAsync(() =>
                {
                    var vm = (MainViewModel)DataContext;
                    foreach (var outerTab in vm.Tabs)
                    {
                        foreach (var innerTab in outerTab.InnerTabs)
                        {
                            // 復元されたタブのリストを初期化（選択値を保持）
                            innerTab.InitializeListsAfterRestore();
                        }
                    }
                    Debug.WriteLine("全タブのSampler/Scheduler/Upscalerリスト初期化を開始しました");
                });
            });

            _ = Task.Run(async () => await CleanupUnreferencedImagesAsync());
            
            // バージョンチェックを開始
            _versionCheckManager?.StartVersionCheck();
            
            // 初期レイアウトを設定（少し遅延させて確実に実行）
            Dispatcher.BeginInvoke(new Action(() => UpdateLeftPanelLayout()), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// 設定をチェックし、問題がある場合は設定ウィンドウを自動表示する
        /// </summary>
        private async Task CheckAndShowSettingsIfNeededAsync()
        {
            try
            {
                bool needsConfiguration = false;

                var settings = AppSettings.Instance;
                
                // LoRAディレクトリのチェック
                if (!string.IsNullOrWhiteSpace(settings.LoraDirectory))
                {
                    if (!Directory.Exists(settings.LoraDirectory))
                    {
                        needsConfiguration = true;
                    }
                }
                else
                {
                    needsConfiguration = true;
                }

                // Stable Diffusion WebUIディレクトリのチェック
                if (!string.IsNullOrWhiteSpace(settings.StableDiffusionDirectory))
                {
                    if (!Directory.Exists(settings.StableDiffusionDirectory))
                    {
                        needsConfiguration = true;
                    }
                    else
                    {
                        // extensionsフォルダの存在チェック
                        string extensionsPath = Path.Combine(settings.StableDiffusionDirectory, "extensions");
                        if (!Directory.Exists(extensionsPath))
                        {
                            needsConfiguration = true;
                        }
                    }
                }
                else
                {
                    needsConfiguration = true;
                }

                // APIアクセスチェックを追加
                bool apiAccessible = false;
                if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
                {
                    // PingUrlとCheckApiOptionsを呼び出してAPIアクセスを確認
                    bool pingResult = await PingUrlAsync(settings.BaseUrl);
                    bool apiOptionsResult = await CheckApiOptionsAsync(settings.BaseUrl);
                    
                    apiAccessible = pingResult && apiOptionsResult;
                }

                if (needsConfiguration || !apiAccessible)
                {
                    // 設定ウィンドウを直接表示
                    var settingsWindow = new SettingsWindow
                    {
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    await Task.Delay(100); // UIの更新を待つ
                    settingsWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定チェックエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 拡張機能スクリプトの初期化（Dynamic Prompts、Kohya hires.fix）
        /// </summary>
        private async Task InitializeExtensionScriptsAsync()
        {
            try
            {
                // Check if Dynamic Prompts extension is installed
                string url = App.BASE_URL;
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(2);

                var response = await client.GetAsync($"{url}/sdapi/v1/scripts");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var scriptsData = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(content);

                                        if (scriptsData != null && scriptsData.TryGetValue("txt2img", out var txt2imgScripts))
                    {
                        // "dynamic prompts"で始まるスクリプトを検索
                        var dynamicPromptScripts = txt2imgScripts
                            .Where(script => script.StartsWith("dynamic prompts", StringComparison.OrdinalIgnoreCase)) // Script.title()をlowercaseしたもの。Dynamic Promptはバージョン名が含まれる
                            .ToList();

                        // "kohya hrfix integrated"スクリプトを検索
                        var kohyaHiresFixScripts = txt2imgScripts
                            .Where(script => script.Equals("kohya hrfix integrated", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // "random resolution"スクリプトを検索
                        var randomResolutionScripts = txt2imgScripts
                            .Where(script => script.Equals("random resolution", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        // Dynamic Prompts スクリプトの処理
                        bool isDynamicPromptAvailable = false;
                        if (dynamicPromptScripts.Count == 1)
                        {
                            _dynamicPromptScriptName = dynamicPromptScripts[0];
                            isDynamicPromptAvailable = true;
                            Debug.WriteLine($"Dynamic Prompts スクリプトを発見: {_dynamicPromptScriptName}");
                        }
                        else if (dynamicPromptScripts.Count == 0)
                        {
                            Debug.WriteLine("Dynamic Prompts スクリプトが見つかりませんでした");
                        }
                        else
                        {
                            Debug.WriteLine($"複数のDynamic Prompts スクリプトが見つかりました ({dynamicPromptScripts.Count}個)");
                        }

                        // Kohya hires.fix スクリプトの処理
                        bool isKohyaHiresFixAvailable = false;
                        if (kohyaHiresFixScripts.Count == 1)
                        {
                            _kohyaHiresFixScriptName = kohyaHiresFixScripts[0];
                            isKohyaHiresFixAvailable = true;
                            Debug.WriteLine($"Kohya hires.fix スクリプトを発見: {_kohyaHiresFixScriptName}");
                        }
                        else if (kohyaHiresFixScripts.Count == 0)
                        {
                            Debug.WriteLine("Kohya hires.fix スクリプトが見つかりませんでした");
                        }
                        else
                        {
                            Debug.WriteLine($"複数のKohya hires.fix スクリプトが見つかりました ({kohyaHiresFixScripts.Count}個)");
                        }

                        // Random resolution スクリプトの処理
                        bool isRandomResolutionAvailable = false;
                        if (randomResolutionScripts.Count == 1)
                        {
                            _randomResolutionScriptName = randomResolutionScripts[0];
                            isRandomResolutionAvailable = true;
                            Debug.WriteLine($"Random resolution スクリプトを発見: {_randomResolutionScriptName}");
                        }
                        else if (randomResolutionScripts.Count == 0)
                        {
                            Debug.WriteLine("Random resolution スクリプトが見つかりませんでした");
                        }
                        else
                        {
                            Debug.WriteLine($"複数のRandom resolution スクリプトが見つかりました ({randomResolutionScripts.Count}個)");
                        }

                        // MainViewModelに結果を通知（UIスレッドで実行）
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var vm = (MainViewModel)DataContext;
                            vm.IsDynamicPromptAvailable = isDynamicPromptAvailable;
                            vm.IsKohyaHiresFixAvailable = isKohyaHiresFixAvailable;
                            vm.IsRandomResolutionAvailable = isRandomResolutionAvailable;
                        });
                    }
                    else
                    {
                        await ShowErrorMessageAsync("スクリプト一覧の取得に失敗しました。\ntxt2img スクリプトが見つかりません。");
                        
                        // MainViewModelに拡張機能利用不可能を通知（UIスレッドで実行）
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var vm = (MainViewModel)DataContext;
                            vm.IsDynamicPromptAvailable = false;
                            vm.IsKohyaHiresFixAvailable = false;
                            vm.IsRandomResolutionAvailable = false;
                        });
                    }
                }
                else
                {
                    await ShowErrorMessageAsync($"スクリプト一覧の取得に失敗しました。\nHTTP Status: {response.StatusCode}");
                    
                    // MainViewModelに拡張機能利用不可能を通知（UIスレッドで実行）
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var vm = (MainViewModel)DataContext;
                        vm.IsDynamicPromptAvailable = false;
                        vm.IsKohyaHiresFixAvailable = false;
                        vm.IsRandomResolutionAvailable = false;
                    });
                }
            }
            catch (HttpRequestException)
            {
                // サーバーが起動していない場合は警告を表示しない（通常の状況）
                Debug.WriteLine("WebUI APIサーバーに接続できませんでした（起動時）");
                
                // MainViewModelに拡張機能利用不可能を通知（UIスレッドで実行）
                await Dispatcher.InvokeAsync(() =>
                {
                    var vm = (MainViewModel)DataContext;
                    vm.IsDynamicPromptAvailable = false;
                    vm.IsKohyaHiresFixAvailable = false;
                    vm.IsRandomResolutionAvailable = false;
                });
            }
            catch (Exception ex)
            {
                // MainViewModelに拡張機能利用不可能を通知（UIスレッドで実行）
                await Dispatcher.InvokeAsync(() =>
                {
                    var vm = (MainViewModel)DataContext;
                    vm.IsDynamicPromptAvailable = false;
                    vm.IsKohyaHiresFixAvailable = false;
                    vm.IsRandomResolutionAvailable = false;
                });
            }
        }

        /// <summary>
        /// UIスレッドでエラーメッセージを表示
        /// </summary>
        private async Task ShowErrorMessageAsync(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "Dynamic Prompts Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        /// <summary>
        /// 指定されたURLへの接続確認
        /// </summary>
        private async Task<bool> PingUrlAsync(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // Pingの代わりにGETリクエストを送信
                    client.Timeout = TimeSpan.FromSeconds(3); // 3秒のタイムアウト
                    HttpResponseMessage response = await client.GetAsync(url);
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"URL到達性チェックエラー (GET): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// APIオプションへのアクセス確認
        /// </summary>
        private async Task<bool> CheckApiOptionsAsync(string baseUrl)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    HttpResponseMessage response = await client.GetAsync($"{baseUrl}/sdapi/v1/options");
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API呼び出しエラー: {ex.Message}");
                return false;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // メモリ監視タイマーを停止・クリーンアップ
            if (_memoryMonitorTimer != null)
            {
                _memoryMonitorTimer.Stop();
                _memoryMonitorTimer = null;
            }
            
            // バージョンチェックマネージャーを解放
            _versionCheckManager?.Dispose();
            _versionCheckManager = null;
            
            // キューマネージャーを解放
            try
            {
                GenerationQueueManager.Instance.Dispose();
                Debug.WriteLine("キューマネージャーを解放しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キューマネージャー解放エラー: {ex.Message}");
            }
            
            var vm = (MainViewModel)DataContext;
            
            // アプリケーション終了時にすべてのタブのGenerate状態をリセット
            try
            {
                foreach (var outerTab in vm.Tabs)
                {
                    foreach (var innerTab in outerTab.InnerTabs)
                    {
                        innerTab.IsGenerating = false;
                        innerTab.ProgressValue = 0.0;
                        innerTab.EtaText = "";
                        innerTab.GenerateButtonText = "Generate";
                    }
                }
                Debug.WriteLine("すべてのタブのGenerate状態をリセットしました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generate状態リセットエラー: {ex.Message}");
            }
            
            SaveWindowSettings();
            SaveTabSelectionSettings();
            
            // Random Resolution設定を保存
            SaveRandomResolutionSettings(GlobalRandomResolutionSettings);
            
            // AutoCompleteの状態をリセット
            AutoCompleteTextBox.SetSearchModeActive(false);
            AutoCompleteTextBox.SetSearchBoxFocus(false);
            
            // イベントハンドラーの登録解除（メモリリーク防止）
            vm.PropertyChanged -= MainViewModel_PropertyChanged;
            if (vm.SelectedTab != null)
            {
                vm.SelectedTab.PropertyChanged -= OuterTab_PropertyChanged;
            }
            
            // すべてのタブからイベントハンドラーを解除
            foreach (var tab in vm.Tabs)
            {
                tab.PropertyChanged -= OuterTab_PropertyChanged;
            }
            
            // タブデータを保存（Undo履歴と整合性を保つため）
            vm.SaveToFile(GetDataFilePath());
            System.Diagnostics.Debug.WriteLine("タブデータ保存完了");
            
            // Undo履歴を保存
            vm.SaveUndoHistory();
            System.Diagnostics.Debug.WriteLine($"Undo履歴保存完了: {vm.GetUndoHistoryCount()}件");
            
            // MainViewModelのDisposeを実行（メモリリーク防止）
            vm.Dispose();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            
            // デバッグ用：キーイベントの確認
            Debug.WriteLine($"KeyDown: {e.Key}, Modifiers: {Keyboard.Modifiers}, SelectedTab: {vm.SelectedTab?.Title}, SelectedInnerTab: {vm.SelectedTab?.SelectedInnerTab?.Title}, IsImageExpanded: {vm.SelectedTab?.SelectedInnerTab?.IsImageExpanded}");
            
            // Ctrl+Tabが押された場合、次の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                vm.MoveToNextInnerTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Ctrl+Tab: 次の内側タブに移動");
                return;
            }
            
            // Ctrl+Shift+Tabが押された場合、前の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                vm.MoveToPreviousInnerTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Ctrl+Shift+Tab: 前の内側タブに移動");
                return;
            }
            
            // Alt+Aが押された場合、左の内側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                vm.MoveToPreviousInnerTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+A: 左の内側タブに移動");
                return;
            }
            
            // Alt+Dが押された場合、右の内側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                vm.MoveToNextInnerTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+D: 右の内側タブに移動");
                return;
            }
            
            // Alt+Shift+Aが押された場合、前の外側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                vm.MoveToPreviousOuterTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+Shift+A: 前の外側タブに移動");
                return;
            }
            
            // Alt+Shift+Dが押された場合、次の外側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                vm.MoveToNextOuterTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+Shift+D: 次の外側タブに移動");
                return;
            }
            
            // Alt+Ctrl+Aが押された場合、前の外側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == (ModifierKeys.Alt | ModifierKeys.Control))
            {
                vm.MoveToPreviousOuterTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+Ctrl+A: 前の外側タブに移動");
                return;
            }
            
            // Alt+Ctrl+Dが押された場合、次の外側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == (ModifierKeys.Alt | ModifierKeys.Control))
            {
                vm.MoveToNextOuterTab();
                e.Handled = true;
                Debug.WriteLine("KeyDown Alt+Ctrl+D: 次の外側タブに移動");
                return;
            }
            
            // 拡大表示中の矢印キーナビゲーション
            if (vm.SelectedTab?.SelectedInnerTab?.IsImageExpanded == true)
            {
                var innerTab = vm.SelectedTab.SelectedInnerTab;
                
                if (e.Key == Key.Left)
                {
                    // 前の画像に移動
                    if (innerTab.ExpandedImageIndex > 0 && innerTab.ImagePaths.Count > 0)
                    {
                        innerTab.ExpandedImageIndex--;
                        if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count)
                        {
                            innerTab.ExpandedImagePath = innerTab.ImagePaths[innerTab.ExpandedImageIndex];
                        }
                        Debug.WriteLine($"Left arrow: moved to index {innerTab.ExpandedImageIndex}");
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Right)
                {
                    // 次の画像に移動
                    if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count - 1 && innerTab.ImagePaths.Count > 0)
                    {
                        innerTab.ExpandedImageIndex++;
                        if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count)
                        {
                            innerTab.ExpandedImagePath = innerTab.ImagePaths[innerTab.ExpandedImageIndex];
                        }
                        Debug.WriteLine($"Right arrow: moved to index {innerTab.ExpandedImageIndex}");
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Escape)
                {
                    // Escapeキーで拡大表示を終了
                    innerTab.IsImageExpanded = false;
                    innerTab.ExpandedImagePath = "";
                    innerTab.ExpandedImageIndex = -1;
                    Debug.WriteLine("Escape: closed expanded view");
                    e.Handled = true;
                    return;
                }
            }

            // Escapeキーでショートカットオーバーレイを閉じる（拡大表示より優先）
            if (e.Key == Key.Escape && IsShortcutsOverlayVisible)
            {
                CloseShortcutsOverlay();
                e.Handled = true;
                return;
            }

            // Escapeキーで画像パネル全画面表示を閉じる
            if (e.Key == Key.Escape && IsImagePanelFullscreen)
            {
                ToggleImagePanelFullscreen();
                e.Handled = true;
                return;
            }

            // Ctrl+Pで画像パネル全画面表示を切り替え
            if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleImagePanelFullscreen();
                e.Handled = true;
                return;
            }



            if (e.Key == Key.Enter &&
                ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                 (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
            {
                if (vm.GenerateCommand?.CanExecute(null) == true)
                {
                    vm.GenerateCommand.Execute(null);
                    e.Handled = true;
                }
            }
            // Ctrl+Shift+Tが押された場合、最後に閉じたタブを復元
            else if (e.Key == Key.T && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                vm.RestoreLastClosedTab();
                e.Handled = true; // イベントを処理済みとしてマーク
                return;
            }

            // Ctrl+Tが押された場合、現在のタブの右側に新しい内側タブを追加
            else if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedTab != null)
                {
                    vm.AddInnerTabAtPosition(vm.SelectedTab);
                    e.Handled = true; // イベントを処理済みとしてマーク
                }
            }
            // Ctrl+Wが押された場合、現在アクティブな内側タブを閉じる
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                vm.CloseActiveInnerTab();
                e.Handled = true; // イベントを処理済みとしてマーク
                Debug.WriteLine("PreviewKeyDown Ctrl+W: アクティブな内側タブを閉じる");
                return;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            
            // デバッグ用：PreviewKeyDownイベントの確認
            Debug.WriteLine($"PreviewKeyDown: {e.Key}, Modifiers: {Keyboard.Modifiers}, SelectedTab: {vm.SelectedTab?.Title}, SelectedInnerTab: {vm.SelectedTab?.SelectedInnerTab?.Title}, IsImageExpanded: {vm.SelectedTab?.SelectedInnerTab?.IsImageExpanded}, IsFocused: {this.IsFocused}");
            
            // Ctrl+1/2/3/4で画像グリッドの列数を変更（オートコンプリート回避のためPreviewKeyDownで処理）
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.D1:
                    case Key.NumPad1:
                        ImageGridColumns = 1;
                        e.Handled = true;
                        Debug.WriteLine("PreviewKeyDown Ctrl+1: 画像グリッドを1列に変更");
                        return;
                    case Key.D2:
                    case Key.NumPad2:
                        ImageGridColumns = 2;
                        e.Handled = true;
                        Debug.WriteLine("PreviewKeyDown Ctrl+2: 画像グリッドを2列に変更");
                        return;
                    case Key.D3:
                    case Key.NumPad3:
                        ImageGridColumns = 3;
                        e.Handled = true;
                        Debug.WriteLine("PreviewKeyDown Ctrl+3: 画像グリッドを3列に変更");
                        return;
                    case Key.D4:
                    case Key.NumPad4:
                        ImageGridColumns = 4;
                        e.Handled = true;
                        Debug.WriteLine("PreviewKeyDown Ctrl+4: 画像グリッドを4列に変更");
                        return;
                }
            }
            
            // Ctrl+Lが押された場合、左側パネルの表示/非表示を切り替え（オートコンプリート回避のためPreviewKeyDownで処理）
            if (e.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ToggleLeftPanel();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Ctrl+L: 左側パネルの表示/非表示を切り替え");
                return;
            }
            
            // Ctrl+Tabが押された場合、次の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                vm.MoveToNextInnerTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Ctrl+Tab: 次の内側タブに移動");
                return;
            }
            
            // Ctrl+Shift+Tabが押された場合、前の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                vm.MoveToPreviousInnerTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Ctrl+Shift+Tab: 前の内側タブに移動");
                return;
            }
            
            // Alt+Aが押された場合、左の内側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                vm.MoveToPreviousInnerTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+A: 左の内側タブに移動");
                return;
            }
            
            // Alt+Dが押された場合、右の内側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                vm.MoveToNextInnerTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+D: 右の内側タブに移動");
                return;
            }
            
            // Alt+Shift+Aが押された場合、前の外側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                vm.MoveToPreviousOuterTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+Shift+A: 前の外側タブに移動");
                return;
            }
            
            // Alt+Shift+Dが押された場合、次の外側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                vm.MoveToNextOuterTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+Shift+D: 次の外側タブに移動");
                return;
            }
            
            // Alt+Ctrl+Aが押された場合、前の外側タブに移動（cyclic）
            if (e.Key == Key.A && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == (ModifierKeys.Alt | ModifierKeys.Control))
            {
                vm.MoveToPreviousOuterTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+Ctrl+A: 前の外側タブに移動");
                return;
            }
            
            // Alt+Ctrl+Dが押された場合、次の外側タブに移動（cyclic）
            if (e.Key == Key.D && (Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == (ModifierKeys.Alt | ModifierKeys.Control))
            {
                vm.MoveToNextOuterTab();
                e.Handled = true;
                Debug.WriteLine("PreviewKeyDown Alt+Ctrl+D: 次の外側タブに移動");
                return;
            }
            
            // Ctrl+Sが押された場合、プロンプトをフォーマット
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                FormatCurrentTabPrompts();
                e.Handled = true; // イベントを処理済みとしてマーク
                Debug.WriteLine("PreviewKeyDown Ctrl+S: プロンプトをフォーマット");
                return;
            }
            
            // Ctrl+Shift+Tが押された場合、最後に閉じたタブを復元（Ctrl+Tより先にチェック）
            if (e.Key == Key.T && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                vm.RestoreLastClosedTab();
                e.Handled = true; // イベントを処理済みとしてマーク
                Debug.WriteLine("PreviewKeyDown Ctrl+Shift+T: 最後に閉じたタブを復元");
                return;
            }
            // Ctrl+Tが押された場合、現在のタブの右側に新しい内側タブを追加
            else if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedTab != null)
                {
                    vm.AddInnerTabAtPosition(vm.SelectedTab);
                    e.Handled = true; // イベントを処理済みとしてマーク
                    Debug.WriteLine("PreviewKeyDown Ctrl+T: 新しい内側タブを追加");
                }
                return;
            }
            // Ctrl+Wが押された場合、現在アクティブな内側タブを閉じる
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                vm.CloseActiveInnerTab();
                e.Handled = true; // イベントを処理済みとしてマーク
                Debug.WriteLine("PreviewKeyDown Ctrl+W: アクティブな内側タブを閉じる");
                return;
            }
            // Ctrl+Shift+Rが押された場合、現在の内側タブを左側に複製
            else if (e.Key == Key.R && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (vm.SelectedTab?.SelectedInnerTab != null)
                {
                    vm.DuplicateCurrentInnerTabLeft(vm.SelectedTab);
                    e.Handled = true; // イベントを処理済みとしてマーク
                    Debug.WriteLine("PreviewKeyDown Ctrl+Shift+R: 現在の内側タブを左側に複製");
                }
                return;
            }
            // Ctrl+Rが押された場合、現在の内側タブを複製
            else if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedTab?.SelectedInnerTab != null)
                {
                    vm.DuplicateCurrentInnerTab(vm.SelectedTab);
                    e.Handled = true; // イベントを処理済みとしてマーク
                    Debug.WriteLine("PreviewKeyDown Ctrl+R: 現在の内側タブを複製");
                }
                return;
            }
            
            // グローバルなCtrl+Enter処理（アプリケーションのどこでも動作）
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.GenerateCommand?.CanExecute(null) == true)
                {
                    vm.GenerateCommand.Execute(null);
                    e.Handled = true; // イベントを処理済みとしてマーク
                    Debug.WriteLine("グローバルCtrl+Enter: Generateコマンドを実行");
                    return;
                }
            }
            
            // 拡大表示中の矢印キーナビゲーション（PreviewKeyDownで処理）
            if (vm.SelectedTab?.SelectedInnerTab?.IsImageExpanded == true)
            {
                var innerTab = vm.SelectedTab.SelectedInnerTab;
                
                if (e.Key == Key.Left)
                {
                    // 前の画像に移動
                    if (innerTab.ExpandedImageIndex > 0 && innerTab.ImagePaths.Count > 0)
                    {
                        innerTab.ExpandedImageIndex--;
                        if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count)
                        {
                            innerTab.ExpandedImagePath = innerTab.ImagePaths[innerTab.ExpandedImageIndex];
                        }
                        Debug.WriteLine($"PreviewKeyDown Left arrow: moved to index {innerTab.ExpandedImageIndex}");
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Right)
                {
                    // 次の画像に移動
                    if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count - 1 && innerTab.ImagePaths.Count > 0)
                    {
                        innerTab.ExpandedImageIndex++;
                        if (innerTab.ExpandedImageIndex < innerTab.ImagePaths.Count)
                        {
                            innerTab.ExpandedImagePath = innerTab.ImagePaths[innerTab.ExpandedImageIndex];
                        }
                        Debug.WriteLine($"PreviewKeyDown Right arrow: moved to index {innerTab.ExpandedImageIndex}");
                    }
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Escape)
                {
                    // Escapeキーで拡大表示を終了
                    innerTab.IsImageExpanded = false;
                    innerTab.ExpandedImagePath = "";
                    innerTab.ExpandedImageIndex = -1;
                    Debug.WriteLine("PreviewKeyDown Escape: closed expanded view");
                    e.Handled = true;
                    return;
                }
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
            {
                // タブ切替時に画像を遅延読み込み
                var vm = (MainViewModel)DataContext;
                if (vm.SelectedTab != null)
                {
                    // 内側タブのコンテンツを一時的に非表示にする
                    if (vm.SelectedTab.SelectedInnerTab != null)
                    {
                        vm.SelectedTab.SelectedInnerTab.IsContentReady = false;
                    }
                    
                    LoadImagesForTab(vm.SelectedTab);
                    
                    // 内側タブの変更も監視するためにイベントハンドラーを設定
                    vm.SelectedTab.PropertyChanged -= OuterTab_PropertyChanged;
                    vm.SelectedTab.PropertyChanged += OuterTab_PropertyChanged;
                    
                    // 現在選択されている内側タブの画像も読み込み
                    if (vm.SelectedTab.SelectedInnerTab != null)
                    {
                        LoadImagesForInnerTab(vm.SelectedTab.SelectedInnerTab);
                    }
                    
                    // バックグラウンドでGCを実行（メモリ最適化）
                    RunBackgroundGC();
                    
                    // タブ切り替え後にコンテンツを表示（遅延実行で確実に）
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // フォーカス設定をコメントアウト（AutoCompleteTextBoxのフォーカスを保持するため）
                        // this.Focus();
                        // this.Activate(); // ウィンドウをアクティブにする
                        
                        // コンテンツを表示
                        if (vm.SelectedTab?.SelectedInnerTab != null)
                        {
                            vm.SelectedTab.SelectedInnerTab.IsContentReady = true;
                        }
                        
                        Debug.WriteLine($"Tab switched to: {vm.SelectedTab?.Title}, IsFocused: {this.IsFocused}, IsActive: {this.IsActive}");
                        
                        // タブ切り替え後にレイアウトを更新
                        UpdateLeftPanelLayout();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void OuterTab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabItemViewModel.SelectedInnerTab))
            {
                var outerTab = sender as TabItemViewModel;
                if (outerTab?.SelectedInnerTab != null)
                {
                    // 内側タブのコンテンツを一時的に非表示にする
                    outerTab.SelectedInnerTab.IsContentReady = false;
                    
                    LoadImagesForInnerTab(outerTab.SelectedInnerTab);
                    
                    // バックグラウンドでGCを実行（メモリ最適化）
                    RunBackgroundGC();
                    
                    // 内側タブ切り替え後にコンテンツを表示（遅延実行で確実に）
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // フォーカス設定をコメントアウト（AutoCompleteTextBoxのフォーカスを保持するため）
                        // this.Focus();
                        // this.Activate(); // ウィンドウをアクティブにする
                        
                        // コンテンツを表示
                        if (outerTab?.SelectedInnerTab != null)
                        {
                            outerTab.SelectedInnerTab.IsContentReady = true;
                        }
                        
                        Debug.WriteLine($"Inner tab switched to: {outerTab?.SelectedInnerTab?.Title}, IsFocused: {this.IsFocused}, IsActive: {this.IsActive}");
                        
                        // 内側タブ切り替え後にレイアウトを更新
                        UpdateLeftPanelLayout();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void LoadImagesForTab(TabItemViewModel tab)
        {
            // 外側タブの全ての内側タブの画像を確認（現在は使用していないが、互換性のため残す）
            foreach (var innerTab in tab.InnerTabs)
            {
                LoadImagesForInnerTab(innerTab);
            }
        }

        private void LoadImagesForInnerTab(TabItemViewModel innerTab)
        {
            // 既存のコレクションをクリアして要素を追加（メモリリーク防止）
            var validPaths = new List<string>();
            foreach (var path in innerTab.ImagePaths.ToList()) // ToList()でコピーを作成してから処理
            {
                if (File.Exists(path))
                {
                    validPaths.Add(path);
                }
            }
            
            // 既存のコレクションを更新（新しいコレクションの代入は避ける）
            innerTab.ImagePaths.Clear();
            foreach (var path in validPaths)
            {
                innerTab.ImagePaths.Add(path);
            }
            
            // タブ切り替え後に画像キャッシュをクリーンアップ
            CleanupImageCache();
        }
        
        /// <summary>
        /// 画像キャッシュの定期的なクリーンアップ
        /// </summary>
        private static void CleanupImageCache()
        {
            try
            {
                // OptimizedImageConverterのキャッシュクリーンアップを実行
                var converterType = typeof(OptimizedImageConverter);
                var cleanupMethod = converterType.GetMethod("CleanupCache", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                cleanupMethod?.Invoke(null, null);
                
                Debug.WriteLine("画像キャッシュクリーンアップ完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像キャッシュクリーンアップエラー: {ex.Message}");
            }
        }

        private async Task InterruptGenerationAsync()
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var outerTab = vm.SelectedTab;
                var innerTab = outerTab?.SelectedInnerTab;

                // キューシステムを使用して現在のタブの生成をキャンセル
                if (innerTab != null)
                {
                    var queueManager = GenerationQueueManager.Instance;
                    queueManager.CancelGeneration(innerTab);
                    Debug.WriteLine($"タブ {innerTab.Title} の生成をキャンセルしました");
                }

                // サーバー側の処理も中断
                string url = App.BASE_URL;
                var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15); // 中断処理は15秒でタイムアウト
                
                // Interrupt API endpoint を呼び出し
                var response = await client.PostAsync($"{url}/sdapi/v1/interrupt", null);
                
                if (response.IsSuccessStatusCode)
                {
                    // 成功した場合、特に何もしない（サーバー側で処理が中断される）
                    System.Diagnostics.Debug.WriteLine("Generation interrupted successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to interrupt generation: {response.StatusCode}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                // サーバー接続エラー（サーバーダウンなど）
                this.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to send interrupt request.\nThe server may have stopped.\n\nError details: {httpEx.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                System.Diagnostics.Debug.WriteLine($"Interrupt HTTP接続エラー: {httpEx.Message}");
            }
            catch (TaskCanceledException tcEx)
            {
                // タイムアウトエラー
                System.Diagnostics.Debug.WriteLine($"Interrupt タイムアウトエラー: {tcEx.Message}");
                // タイムアウトの場合はユーザーに通知しない（中断処理は継続されている可能性があるため）
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error interrupting generation: {ex.Message}");
                // その他のエラーの場合もユーザーに通知しない（中断は最善努力で行う）
            }
        }

        /// <summary>
        /// 画像生成を実行する処理
        /// </summary>
        /// <summary>
        /// Kohya hires.fixの条件付き有効化の条件が満たされているかチェックします
        /// </summary>
        /// <param name="innerTab">チェック対象のタブ</param>
        /// <returns>条件が満たされている場合はtrue</returns>
        private bool IsKohyaConditionMet(TabItemViewModel innerTab)
        {
            int shortSide = Math.Min(innerTab.Width, innerTab.Height);
            int longSide = Math.Max(innerTab.Width, innerTab.Height);
            
            bool conditionMet = shortSide >= innerTab.KohyaConditionShortSide && longSide >= innerTab.KohyaConditionLongSide;
            
            Debug.WriteLine($"Kohya条件チェック: short={shortSide}>={innerTab.KohyaConditionShortSide}, long={longSide}>={innerTab.KohyaConditionLongSide} => {conditionMet}");
            
            return conditionMet;
        }

        private async Task GenerateImagesAsync()
        {
            // 画像生成前にプロンプトをフォーマット
            FormatCurrentTabPrompts();
            
            // オートコンプリートの候補が表示されている場合は閉じる
            AutoCompleteTextBox.CloseAllSuggestions();
            
            var vm = (MainViewModel)DataContext;
            var outerTab = vm.SelectedTab;
            var innerTab = outerTab?.SelectedInnerTab;

            if (innerTab == null)
            {
                Debug.WriteLine("選択されたタブが見つかりません。");
                return;
            }

            // キューシステムを使用して重複チェック（同じタブで複数回押すことは許可）
            var queueManager = GenerationQueueManager.Instance;
            
            // 処理時間測定開始
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 最後に実行されたプロンプトと設定を保存
            string prompt = (innerTab.TextBoxValue ?? "").Replace("\r\n", "\n");
            string negativePrompt = (innerTab.NegativePromptValue ?? "").Replace("\r\n", "\n");

            MainViewModel.LastPrompt = prompt;
            MainViewModel.LastNegativePrompt = negativePrompt;
            MainViewModel.LastWidth = innerTab.Width;
            MainViewModel.LastHeight = innerTab.Height;
            MainViewModel.LastSteps = innerTab.Steps;
            MainViewModel.LastCfgScale = innerTab.CfgScale;
            MainViewModel.LastDenoisingStrength = innerTab.DenoisingStrength;
            MainViewModel.LastSelectedSamplingMethod = innerTab.SelectedSamplingMethod;
            MainViewModel.LastSelectedScheduleType = innerTab.SelectedScheduleType;
            MainViewModel.LastSeed = innerTab.Seed;
            MainViewModel.LastSubseed = innerTab.Subseed;
            MainViewModel.LastEnableHiresFix = innerTab.EnableHiresFix;
            MainViewModel.LastSelectedUpscaler = innerTab.SelectedUpscaler;
            MainViewModel.LastHiresSteps = innerTab.HiresSteps;
            MainViewModel.LastHiresUpscaleBy = innerTab.HiresUpscaleBy;
            MainViewModel.LastHiresResizeWidth = innerTab.HiresResizeWidth;
            MainViewModel.LastHiresResizeHeight = innerTab.HiresResizeHeight;
            MainViewModel.LastCombinatorialGeneration = innerTab.CombinatorialGeneration;
            MainViewModel.LastEnableKohyaHiresFix = innerTab.EnableKohyaHiresFix;
            MainViewModel.LastKohyaBlockNumber = innerTab.KohyaBlockNumber;
            MainViewModel.LastKohyaDownscaleFactor = innerTab.KohyaDownscaleFactor;
            MainViewModel.LastEnableRandomResolution = innerTab.EnableRandomResolution;
            
            // 設定をファイルに即座に保存
            SaveLastGenerateSettings();
            vm.SaveToFile(GetDataFilePath());

            // Always-on Scripts の準備
            Dictionary<string, object>? alwaysonScripts = null;
            
            if (innerTab.CombinatorialGeneration && !string.IsNullOrEmpty(_dynamicPromptScriptName))
            {
                alwaysonScripts = new Dictionary<string, object>
                {
                    [_dynamicPromptScriptName] = new Dictionary<string, object>
                    {
                        ["args"] = new object[] { true, true }
                    }
                };
                Debug.WriteLine($"Dynamic Prompts を有効化: {_dynamicPromptScriptName}");
            }
            else if (innerTab.CombinatorialGeneration)
            {
                Debug.WriteLine("Dynamic Prompts が有効化されていますが、スクリプト名が取得できていません");
            }
            
            // Kohya hires.fixのalwaysonScriptsサポート
            // 通常のチェックボックスがオンまたは条件付き有効化がオンかつ条件を満たしている場合
            bool shouldEnableKohyaHiresFix = innerTab.EnableKohyaHiresFix || 
                (innerTab.KohyaAlwaysEnableCondition && IsKohyaConditionMet(innerTab));
            
            if (shouldEnableKohyaHiresFix && !string.IsNullOrEmpty(_kohyaHiresFixScriptName))
            {
                if (alwaysonScripts == null)
                {
                    alwaysonScripts = new Dictionary<string, object>();
                }
                
                alwaysonScripts[_kohyaHiresFixScriptName] = new Dictionary<string, object>
                {
                    ["args"] = new object[] 
                    { 
                        true, 
                        innerTab.KohyaBlockNumber, 
                        innerTab.KohyaDownscaleFactor 
                    }
                };
                
                string reason = innerTab.EnableKohyaHiresFix ? "チェックボックス" : "条件付き有効化";
                Debug.WriteLine($"Kohya hires.fix を有効化 ({reason}): {_kohyaHiresFixScriptName}, Block={innerTab.KohyaBlockNumber}, Downscale={innerTab.KohyaDownscaleFactor}");
            }
            else if (shouldEnableKohyaHiresFix)
            {
                Debug.WriteLine("Kohya hires.fix が有効化されていますが、スクリプト名が取得できていません");
            }
            
            // Random ResolutionのalwaysonScriptsサポート
            if (innerTab.EnableRandomResolution && !string.IsNullOrEmpty(_randomResolutionScriptName))
            {
                if (alwaysonScripts == null)
                {
                    alwaysonScripts = new Dictionary<string, object>();
                }
                
                // Random Resolution用の引数を構築
                var randomResolutionArgs = new object[]
                {
                    true, // innerTab.EnableRandomResolutionがtrueの場合は常にtrue
                    GlobalRandomResolutionSettings.ModelType,
                    string.Join(";", GlobalRandomResolutionSettings.CurrentResolutions.Select(r => $"{r.Width},{r.Height}")) + (GlobalRandomResolutionSettings.CurrentResolutions.Count > 0 ? ";" : ""),
                    GlobalRandomResolutionSettings.WeightMode,
                    GlobalRandomResolutionSettings.MinDim,
                    GlobalRandomResolutionSettings.MaxDim
                };
                
                alwaysonScripts[_randomResolutionScriptName] = new Dictionary<string, object>
                {
                    ["args"] = randomResolutionArgs
                };
                Debug.WriteLine($"Random Resolution を有効化: {_randomResolutionScriptName}, ModelType={GlobalRandomResolutionSettings.ModelType}, WeightMode={GlobalRandomResolutionSettings.WeightMode}, Resolutions={GlobalRandomResolutionSettings.CurrentResolutions.Count}個");
            }
            else if (innerTab.EnableRandomResolution)
            {
                Debug.WriteLine("Random Resolution が有効化されていますが、スクリプト名が取得できていません");
            }
            
            // ペイロードをDictionaryで動的に構築
            var txt2imgPayload = new Dictionary<string, object>
            {
                ["prompt"] = prompt,
                ["negative_prompt"] = negativePrompt,
                ["steps"] = innerTab.Steps,
                ["width"] = innerTab.Width,
                ["height"] = innerTab.Height,
                ["batch_size"] = innerTab.BatchSize,
                ["n_iter"] = innerTab.BatchCount,
                ["cfg_scale"] = innerTab.CfgScale,
                ["sampler_name"] = innerTab.SelectedSamplingMethod,
                ["scheduler"] = innerTab.SelectedScheduleType,
                ["seed"] = innerTab.Seed,
                ["subseed"] = innerTab.Subseed,
                ["save_images"] = true,
                // Hires.fix関連のパラメータ
                ["enable_hr"] = innerTab.EnableHiresFix,
                ["hr_upscaler"] = innerTab.EnableHiresFix ? innerTab.SelectedUpscaler : null,
                ["hr_second_pass_steps"] = innerTab.EnableHiresFix ? innerTab.HiresSteps : 0,
                ["denoising_strength"] = innerTab.EnableHiresFix ? innerTab.DenoisingStrength : 0,
                ["hr_scale"] = innerTab.EnableHiresFix ? innerTab.HiresUpscaleBy : 1.0
            };
            
            // CombinatorialGenerationがオンの場合のみalwayson_scriptsを追加
            if (alwaysonScripts != null)
            {
                txt2imgPayload["alwayson_scripts"] = alwaysonScripts;
                Debug.WriteLine("alwayson_scripts をペイロードに追加しました");
            }
            else
            {
                Debug.WriteLine("alwayson_scripts をペイロードから除外しました");
            }

            try
            {
                // キューに追加して処理を開始
                queueManager.EnqueueGeneration(innerTab, txt2imgPayload);
                
                // キューに追加した時点で現在のタブのボタンテキストを「Running」に変更
                // （IsGeneratingはGenerationQueueManagerで設定される）
                innerTab.GenerateButtonText = "Running";
                
                // 処理時間の表示は GenerationQueueManager で設定されるため、ここでは削除
                // stopwatch.Stop();
                // innerTab.ProcessingTime = $"キューに追加: {stopwatch.Elapsed.TotalMilliseconds:F0} ms";
                
                Debug.WriteLine($"生成リクエストをキューに追加しました: {innerTab.Title}");
                
                // Generate実行完了時にタブデータを保存
                vm.SaveToFile(GetDataFilePath());
            }
            catch (Exception ex)
            {
                // キューへの追加でエラーが発生した場合
                this.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"An error occurred while adding the generation request.\n\nError details: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                System.Diagnostics.Debug.WriteLine($"キューエラー: {ex.Message}");
            }
        }

        public class Response
        {
            [JsonPropertyName("images")]
            public List<string> Images { get; set; } = new();
            
            [JsonPropertyName("parameters")]
            public JsonElement? Parameters { get; set; }
            
            [JsonPropertyName("info")]
            public string Info { get; set; } = "";
        }

        public class InfoData
        {
            [JsonPropertyName("infotexts")]
            public List<string> InfoTexts { get; set; } = new();
            
            [JsonPropertyName("styles")]
            public List<string> Styles { get; set; } = new();
            
            [JsonPropertyName("job_timestamp")]
            public string JobTimestamp { get; set; } = "";
            
            [JsonPropertyName("clip_skip")]
            public int ClipSkip { get; set; }
            
            [JsonPropertyName("is_using_inpainting_conditioning")]
            public bool IsUsingInpaintingConditioning { get; set; }
            
            [JsonPropertyName("version")]
            public string Version { get; set; } = "";
        }

        public class ProgressResponse
        {
            [JsonPropertyName("progress")]
            public double Progress { get; set; }

            [JsonPropertyName("eta_relative")]
            public double EtaRelative { get; set; }

            [JsonPropertyName("state")]
            public ProgressState State { get; set; } = null!;

            [JsonPropertyName("current_image")]
            public string CurrentImage { get; set; } = "";

            [JsonPropertyName("textinfo")]
            public string TextInfo { get; set; } = "";
        }

        public class ProgressState
        {
            [JsonPropertyName("skipped")]
            public bool Skipped { get; set; }

            [JsonPropertyName("interrupted")]
            public bool Interrupted { get; set; }

            [JsonPropertyName("stopping_generation")]
            public bool StoppingGeneration { get; set; }

            [JsonPropertyName("job")]
            public string Job { get; set; } = "";

            [JsonPropertyName("job_count")]
            public int JobCount { get; set; }

            [JsonPropertyName("job_timestamp")]
            public string JobTimestamp { get; set; } = "";

            [JsonPropertyName("job_no")]
            public int JobNo { get; set; }

            [JsonPropertyName("sampling_step")]
            public int SamplingStep { get; set; }

            [JsonPropertyName("sampling_steps")]
            public int SamplingSteps { get; set; }
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            vm.AddOuterTab();
        }

        /// <summary>
        /// 外側タブの複製メニューアイテムがクリックされた時の処理
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // コンテキストメニューのDataContextから元のタブを取得
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var tab = contextMenu?.DataContext as TabItemViewModel;
            
            if (tab != null)
            {
                var vm = (MainViewModel)DataContext;
                vm.DuplicateOuterTab(tab);
            }
        }

        /// <summary>
        /// 外側タブの画像エクスポートメニューアイテムがクリックされた時の処理
        /// </summary>
        private void ExportImagesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // コンテキストメニューのDataContextから元のタブを取得
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var tab = contextMenu?.DataContext as TabItemViewModel;
            
            if (tab != null)
            {
                ExportImagesFromTab(tab);
            }
        }

        /// <summary>
        /// 指定された外側タブの画像を一括エクスポートします
        /// </summary>
        /// <param name="outerTab">エクスポート対象の外側タブ</param>
        private void ExportImagesFromTab(TabItemViewModel outerTab)
        {
            try
            {
                // フォルダー選択ダイアログを表示
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "画像エクスポート先フォルダーを選択してください",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var exportDir = dialog.SelectedPath;
                    var vm = (MainViewModel)DataContext;
                    var outerTabIndex = vm.Tabs.IndexOf(outerTab) + 1; // 1から始まる

                    var exportedCount = 0;
                    var totalImages = outerTab.InnerTabs.Sum(innerTab => innerTab.ImagePaths?.Count ?? 0);

                    if (totalImages == 0)
                    {
                        MessageBox.Show("エクスポートする画像がありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 各内側タブの画像をエクスポート
                    for (int innerTabIndex = 0; innerTabIndex < outerTab.InnerTabs.Count; innerTabIndex++)
                    {
                        var innerTab = outerTab.InnerTabs[innerTabIndex];
                        var innerTabNumber = innerTabIndex + 1; // 1から始まる

                        if (innerTab.ImagePaths != null && innerTab.ImagePaths.Count > 0)
                        {
                            for (int imageIndex = 0; imageIndex < innerTab.ImagePaths.Count; imageIndex++)
                            {
                                var imagePath = innerTab.ImagePaths[imageIndex];
                                var imageNumber = imageIndex + 1; // 1から始まる

                                try
                                {
                                    if (File.Exists(imagePath))
                                    {
                                        var extension = Path.GetExtension(imagePath);
                                        var fileName = $"{outerTab.Title}_tab_{innerTabNumber}_{imageNumber}{extension}";
                                        var destinationPath = Path.Combine(exportDir, fileName);

                                        // ファイル名が重複する場合は連番を付ける
                                        var counter = 1;
                                        while (File.Exists(destinationPath))
                                        {
                                            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                            fileName = $"{nameWithoutExt}_{counter}{extension}";
                                            destinationPath = Path.Combine(exportDir, fileName);
                                            counter++;
                                        }

                                        File.Copy(imagePath, destinationPath);
                                        exportedCount++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"画像エクスポートエラー - ファイル: {imagePath}, エラー: {ex.Message}");
                                }
                            }
                        }
                    }

                    MessageBox.Show($"Exported {exportedCount}images to {exportDir}", 
                                   "Export successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"画像エクスポート中にエラーが発生しました:\n{ex.Message}", "エラー", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"画像エクスポートエラー: {ex}");
            }
        }

        /// <summary>
        /// 内側タブの右側複製メニューアイテムがクリックされた時の処理
        /// </summary>
        private void DuplicateInnerTabRightMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // コンテキストメニューのDataContextから内側タブを取得
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var innerTab = contextMenu?.DataContext as TabItemViewModel;
            
            if (innerTab != null)
            {
                var outerTab = FindOuterTabForInnerTab(innerTab);
                if (outerTab != null)
                {
                    var vm = (MainViewModel)DataContext;
                    vm.DuplicateInnerTabRight(outerTab, innerTab);
                }
            }
        }

        /// <summary>
        /// 内側タブの左側複製メニューアイテムがクリックされた時の処理
        /// </summary>
        private void DuplicateInnerTabLeftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // コンテキストメニューのDataContextから内側タブを取得
            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            var innerTab = contextMenu?.DataContext as TabItemViewModel;
            
            if (innerTab != null)
            {
                var outerTab = FindOuterTabForInnerTab(innerTab);
                if (outerTab != null)
                {
                    var vm = (MainViewModel)DataContext;
                    vm.DuplicateInnerTabLeft(outerTab, innerTab);
                }
            }
        }

        // 内側タブ追加ボタン用（XAMLで必要に応じてバインド）
        private void AddInnerTabButton_Click(object sender, RoutedEventArgs e)
        {
            var outerTab = (sender as FrameworkElement)?.DataContext as TabItemViewModel;
            if (outerTab != null)
            {
                var vm = (MainViewModel)DataContext;
                vm.AddInnerTab(outerTab);
            }
        }

        // 内側タブ削除ボタン用（XAMLで必要に応じてバインド）
        private void RemoveInnerTabButton_Click(object sender, MouseButtonEventArgs e)
        {
            // バブリングを停止
            e.Handled = true;
            
            var button = sender as Button;
            var innerTab = button?.DataContext as TabItemViewModel;
            if (innerTab != null)
            {
                var outerTab = FindOuterTabForInnerTab(innerTab);
                if (outerTab != null)
                {
                    var vm = (MainViewModel)DataContext;
                    vm.RemoveInnerTab(outerTab, innerTab);
                }
            }
        }

        // 内側タブの親を探す（ヘルパー）
        private TabItemViewModel? FindOuterTabForInnerTab(TabItemViewModel innerTab)
        {
            var vm = (MainViewModel)DataContext;
            foreach (var outer in vm.Tabs)
            {
                if (outer.InnerTabs.Contains(innerTab))
                    return outer;
            }
            return null;
        }

        // 外側タブのドラッグ＆ドロップ
        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var tabControl = sender as TabControl;
                if (tabControl == null) return;
                
                var pos = e.GetPosition(tabControl);
                
                // ドラッグ開始点が設定されていない場合は現在位置を設定
                if (_dragStartPoint == default)
                {
                    _dragStartPoint = pos;
                    return; // 最初のマウス移動では何もしない
                }
                
                // ドラッグ距離をチェック
                var deltaX = Math.Abs(pos.X - _dragStartPoint.X);
                var deltaY = Math.Abs(pos.Y - _dragStartPoint.Y);
                
                if (deltaX > SystemParameters.MinimumHorizontalDragDistance || 
                    deltaY > SystemParameters.MinimumVerticalDragDistance)
                {
                    // TabItemを探す - より確実な方法で検索
                    TabItem? tabItem = null;
                    
                    // 最初にヒットテストで要素を取得
                    var hitElement = tabControl.InputHitTest(_dragStartPoint) as DependencyObject;
                    if (hitElement != null)
                    {
                        // TabItemまたはその親要素を探す
                        tabItem = FindAncestor<TabItem>(hitElement);
                    }
                    
                    // TabItemが見つからない場合は、別の方法で探す
                    if (tabItem == null)
                    {
                        // タブヘッダーパネル内のTabItemを直接検索
                        for (int i = 0; i < tabControl.Items.Count; i++)
                        {
                            var container = tabControl.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                            if (container != null)
                            {
                                var itemBounds = new Rect(
                                    container.TranslatePoint(new Point(0, 0), tabControl),
                                    new Size(container.ActualWidth, container.ActualHeight)
                                );
                                
                                if (itemBounds.Contains(_dragStartPoint))
                                {
                                    tabItem = container;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (tabItem != null && tabItem.DataContext != null)
                    {
                        try
                        {
                            // ドラッグ操作を開始
                            DragDrop.DoDragDrop(tabItem, tabItem.DataContext, DragDropEffects.Move);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"外側タブドラッグ開始エラー: {ex.Message}");
                        }
                        finally
                        {
                            // ドラッグ完了後にドラッグ開始点をリセット
                            _dragStartPoint = default;
                        }
                    }
                }
            }
            else
            {
                // 左ボタンが押されていない場合はドラッグ開始点をリセット
                _dragStartPoint = default;
            }
        }

        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            // TabItemViewModelのドラッグの場合のみ許可
            if (e.Data.GetDataPresent(typeof(TabItemViewModel)))
            {
                var tabControl = sender as TabControl;
                var sourceTab = e.Data.GetData(typeof(TabItemViewModel)) as TabItemViewModel;
                var vm = (MainViewModel)DataContext;
                
                // 同じTabControl内のタブのドラッグのみ許可
                if (tabControl != null && sourceTab != null && vm.Tabs.Contains(sourceTab))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                // その他のドラッグは拒否
                e.Effects = DragDropEffects.None;
            }
            
            e.Handled = true;
        }

        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null) return;
            
            var vm = (MainViewModel)DataContext;
            var sourceTab = e.Data.GetData(typeof(TabItemViewModel)) as TabItemViewModel;
            if (sourceTab == null) return;
            var pos = e.GetPosition(tabControl);
            for (int i = 0; i < tabControl.Items.Count; i++)
            {
                var item = (TabItem)tabControl.ItemContainerGenerator.ContainerFromIndex(i);
                if (item != null)
                {
                    var itemRect = new Rect(item.TranslatePoint(new Point(), tabControl), new Size(item.ActualWidth, item.ActualHeight));
                    if (itemRect.Contains(pos))
                    {
                        int oldIndex = vm.Tabs.IndexOf(sourceTab);
                        if (oldIndex != i)
                        {
                            vm.MoveOuterTab(oldIndex, i);
                        }
                        break;
                    }
                }
            }
        }

        // 内側タブのドラッグ＆ドロップ
        private void InnerTabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var tabControl = sender as TabControl;
                if (tabControl == null) return;
                
                var pos = e.GetPosition(tabControl);
                
                // ドラッグ開始点が設定されていない場合は現在位置を設定
                if (_innerDragStartPoint == default)
                {
                    _innerDragStartPoint = pos;
                    return; // 最初のマウス移動では何もしない
                }
                
                // ドラッグ距離をチェック
                var deltaX = Math.Abs(pos.X - _innerDragStartPoint.X);
                var deltaY = Math.Abs(pos.Y - _innerDragStartPoint.Y);
                
                if (deltaX > SystemParameters.MinimumHorizontalDragDistance || 
                    deltaY > SystemParameters.MinimumVerticalDragDistance)
                {
                    // TabItemを探す - より確実な方法で検索
                    TabItem? tabItem = null;
                    
                    // 最初にヒットテストで要素を取得
                    var hitElement = tabControl.InputHitTest(_innerDragStartPoint) as DependencyObject;
                    if (hitElement != null)
                    {
                        // TabItemまたはその親要素を探す
                        tabItem = FindAncestor<TabItem>(hitElement);
                    }
                    
                    // TabItemが見つからない場合は、別の方法で探す
                    if (tabItem == null)
                    {
                        // タブヘッダーパネル内のTabItemを直接検索
                        for (int i = 0; i < tabControl.Items.Count; i++)
                        {
                            var container = tabControl.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                            if (container != null)
                            {
                                var itemBounds = new Rect(
                                    container.TranslatePoint(new Point(0, 0), tabControl),
                                    new Size(container.ActualWidth, container.ActualHeight)
                                );
                                
                                if (itemBounds.Contains(_innerDragStartPoint))
                                {
                                    tabItem = container;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (tabItem != null && tabItem.DataContext != null)
                    {
                        try
                        {
                            Debug.WriteLine($"内側タブドラッグ開始: {tabItem.DataContext}");
                            // ドラッグ操作を開始
                            DragDrop.DoDragDrop(tabItem, tabItem.DataContext, DragDropEffects.Move);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"内側タブドラッグ開始エラー: {ex.Message}");
                        }
                        finally
                        {
                            // ドラッグ完了後にドラッグ開始点をリセット
                            _innerDragStartPoint = default;
                        }
                    }
                }
            }
            else
            {
                // 左ボタンが押されていない場合はドラッグ開始点をリセット
                _innerDragStartPoint = default;
            }
        }

        private void InnerTabControl_Drop(object sender, DragEventArgs e)
        {
            // ファイルドロップの場合は、メインウィンドウのドロップ処理に転送
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                MainWindow_Drop(sender, e);
                return;
            }
            
            // タブのドラッグアンドドロップ処理
            try
            {
                var tabControl = sender as TabControl;
                if (tabControl == null) return;
                
                var outerTab = tabControl.DataContext as TabItemViewModel;
                if (outerTab == null) return;
                
                var sourceTab = e.Data.GetData(typeof(TabItemViewModel)) as TabItemViewModel;
                if (sourceTab == null) return;
                
                // ソースタブが同じ外側タブに属するかチェック
                if (!outerTab.InnerTabs.Contains(sourceTab)) return;
                
                var pos = e.GetPosition(tabControl);
                int targetIndex = -1;
                
                // ドロップ位置からターゲットインデックスを計算
                for (int i = 0; i < tabControl.Items.Count; i++)
                {
                    var container = tabControl.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                    if (container != null)
                    {
                        var itemPos = container.TranslatePoint(new Point(0, 0), tabControl);
                        var itemRect = new Rect(itemPos, new Size(container.ActualWidth, container.ActualHeight));
                        
                        if (itemRect.Contains(pos))
                        {
                            // タブの左半分なら手前に、右半分なら後ろに挿入
                            var relativeX = pos.X - itemPos.X;
                            if (relativeX < container.ActualWidth / 2)
                            {
                                targetIndex = i;
                            }
                            else
                            {
                                targetIndex = i + 1;
                            }
                            break;
                        }
                    }
                }
                
                // ターゲットインデックスが見つからない場合は最後に配置
                if (targetIndex == -1)
                {
                    targetIndex = outerTab.InnerTabs.Count;
                }
                
                // インデックスの範囲チェック
                targetIndex = Math.Max(0, Math.Min(targetIndex, outerTab.InnerTabs.Count));
                
                int oldIndex = outerTab.InnerTabs.IndexOf(sourceTab);
                
                // 同じ位置への移動はスキップ
                if (oldIndex == targetIndex || (oldIndex + 1 == targetIndex))
                {
                    return;
                }
                
                // 移動後のインデックスを調整（元の位置より後ろに移動する場合）
                if (oldIndex < targetIndex)
                {
                    targetIndex--;
                }
                
                // 実際に移動を実行
                if (targetIndex >= 0 && targetIndex < outerTab.InnerTabs.Count && oldIndex != targetIndex)
                {
                    var vm = (MainViewModel)DataContext;
                    vm.MoveInnerTab(outerTab, oldIndex, targetIndex);
                    
                    Debug.WriteLine($"内側タブ移動: {oldIndex} -> {targetIndex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ドロップ処理エラー: {ex.Message}");
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                
                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    // FlowDocumentなどVisualではないオブジェクトの場合は
                    // LogicalTreeHelperを使用して親を取得
                    current = LogicalTreeHelper.GetParent(current);
                }
                catch (Exception ex)
                {
                    // その他の予期しないエラーの場合はログに記録して終了
                    Debug.WriteLine($"FindAncestor エラー: {ex.Message}");
                    break;
                }
            }
            return null;
        }

        private double GetCurrentSplitterPosition()
        {
            try
            {
                // ContentTemplateの中のGridを探す
                var splitterGrid = FindVisualChild<Grid>(MainTabControl, g => g.ColumnDefinitions.Count >= 3);
                if (splitterGrid != null)
                {
                    return splitterGrid.ColumnDefinitions[0].ActualWidth;
                }
                return 350; // デフォルト値
            }
            catch
            {
                return 350; // エラー時はデフォルト値
            }
        }

        private void SetSplitterPosition(double position)
        {
            try
            {
                // ContentTemplateの中のGridを探す
                var splitterGrid = FindVisualChild<Grid>(MainTabControl, g => g.ColumnDefinitions.Count >= 3);
                if (splitterGrid != null)
                {
                    // 位置が妥当な範囲内かチェック
                    if (position >= 250 && position <= this.Width - 100)
                    {
                        splitterGrid.ColumnDefinitions[0].Width = new GridLength(position);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Splitter位置の設定に失敗しました: {ex.Message}");
            }
        }

        private void MainSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Splitter移動後の位置を保存
            SaveWindowSettings();
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var foundChild = FindVisualChild<T>(child);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }
            return null;
        }

        private T? FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && predicate(typedChild))
                {
                    return typedChild;
                }

                var foundChild = FindVisualChild(child, predicate);
                if (foundChild != null)
                {
                    return foundChild;
                }
            }
            return null;
        }

        private void LoadLastGenerateSettings()
        {
            try
            {
                var filePath = GetLastGenerateSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<LastGenerateSettings>(json);
                    
                    if (settings != null)
                    {
                        MainViewModel.LastPrompt = settings.LastPrompt;
                        MainViewModel.LastNegativePrompt = settings.LastNegativePrompt;
                        MainViewModel.LastWidth = settings.LastWidth;
                        MainViewModel.LastHeight = settings.LastHeight;
                        MainViewModel.LastSteps = settings.LastSteps;
                        MainViewModel.LastCfgScale = settings.LastCfgScale;
                        MainViewModel.LastDenoisingStrength = settings.LastDenoisingStrength;
                        MainViewModel.LastSelectedSamplingMethod = settings.LastSelectedSamplingMethod;
                        MainViewModel.LastSelectedScheduleType = settings.LastSelectedScheduleType;
                        MainViewModel.LastSeed = settings.LastSeed;
                        MainViewModel.LastSubseed = settings.LastSubseed;
                        MainViewModel.LastEnableHiresFix = settings.LastEnableHiresFix;
                        MainViewModel.LastSelectedUpscaler = settings.LastSelectedUpscaler;
                        MainViewModel.LastHiresSteps = settings.LastHiresSteps;
                        MainViewModel.LastHiresUpscaleBy = settings.LastHiresUpscaleBy;
                        MainViewModel.LastHiresResizeWidth = settings.LastHiresResizeWidth;
                        MainViewModel.LastHiresResizeHeight = settings.LastHiresResizeHeight;
                        MainViewModel.LastCombinatorialGeneration = settings.LastCombinatorialGeneration;
                        MainViewModel.LastEnableKohyaHiresFix = settings.LastEnableKohyaHiresFix;
                        MainViewModel.LastKohyaBlockNumber = settings.LastKohyaBlockNumber;
                        MainViewModel.LastKohyaDownscaleFactor = settings.LastKohyaDownscaleFactor;
                        MainViewModel.LastKohyaAlwaysEnableCondition = settings.LastKohyaAlwaysEnableCondition;
                        MainViewModel.LastKohyaConditionShortSide = settings.LastKohyaConditionShortSide;
                        MainViewModel.LastKohyaConditionLongSide = settings.LastKohyaConditionLongSide;
                        MainViewModel.LastEnableRandomResolution = settings.LastEnableRandomResolution;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"最後のGenerate設定の読み込みに失敗しました: {ex.Message}");
            }
        }

        private void SaveLastGenerateSettings()
        {
            try
            {
                var settings = new LastGenerateSettings
                {
                    LastPrompt = MainViewModel.LastPrompt,
                    LastNegativePrompt = MainViewModel.LastNegativePrompt,
                    LastWidth = MainViewModel.LastWidth,
                    LastHeight = MainViewModel.LastHeight,
                    LastSteps = MainViewModel.LastSteps,
                    LastCfgScale = MainViewModel.LastCfgScale,
                    LastDenoisingStrength = MainViewModel.LastDenoisingStrength,
                    LastSelectedSamplingMethod = MainViewModel.LastSelectedSamplingMethod,
                    LastSelectedScheduleType = MainViewModel.LastSelectedScheduleType,
                    LastSeed = MainViewModel.LastSeed,
                    LastSubseed = MainViewModel.LastSubseed,
                    LastEnableHiresFix = MainViewModel.LastEnableHiresFix,
                    LastSelectedUpscaler = MainViewModel.LastSelectedUpscaler,
                    LastHiresSteps = MainViewModel.LastHiresSteps,
                    LastHiresUpscaleBy = MainViewModel.LastHiresUpscaleBy,
                    LastHiresResizeWidth = MainViewModel.LastHiresResizeWidth,
                    LastHiresResizeHeight = MainViewModel.LastHiresResizeHeight,
                    LastCombinatorialGeneration = MainViewModel.LastCombinatorialGeneration,
                    LastEnableKohyaHiresFix = MainViewModel.LastEnableKohyaHiresFix,
                    LastKohyaBlockNumber = MainViewModel.LastKohyaBlockNumber,
                    LastKohyaDownscaleFactor = MainViewModel.LastKohyaDownscaleFactor,
                    LastKohyaAlwaysEnableCondition = MainViewModel.LastKohyaAlwaysEnableCondition,
                    LastKohyaConditionShortSide = MainViewModel.LastKohyaConditionShortSide,
                    LastKohyaConditionLongSide = MainViewModel.LastKohyaConditionLongSide,
                    LastEnableRandomResolution = MainViewModel.LastEnableRandomResolution
                };

                var filePath = GetLastGenerateSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"最後のGenerate設定の保存に失敗しました: {ex.Message}");
            }
        }

        private void SaveTabSelectionSettings()
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var settings = new TabSelectionSettings();
                
                // 選択されている外側タブのインデックスを取得
                if (vm.SelectedTab != null)
                {
                    settings.SelectedOuterTabIndex = vm.Tabs.IndexOf(vm.SelectedTab);
                    
                    // 選択されている内側タブのインデックスを取得
                    if (vm.SelectedTab.SelectedInnerTab != null)
                    {
                        settings.SelectedInnerTabIndex = vm.SelectedTab.InnerTabs.IndexOf(vm.SelectedTab.SelectedInnerTab);
                    }
                }

                var filePath = GetTabSelectionSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"タブ選択設定の保存に失敗しました: {ex.Message}");
            }
        }

        private void LoadTabSelectionSettings()
        {
            try
            {
                var filePath = GetTabSelectionSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<TabSelectionSettings>(json);
                    
                    if (settings != null)
                    {
                        var vm = (MainViewModel)DataContext;
                        
                        // 外側タブの選択を復元
                        if (settings.SelectedOuterTabIndex >= 0 && settings.SelectedOuterTabIndex < vm.Tabs.Count)
                        {
                            var selectedOuterTab = vm.Tabs[settings.SelectedOuterTabIndex];
                            vm.SelectedTab = selectedOuterTab;
                            
                            // 内側タブの選択を復元
                            if (settings.SelectedInnerTabIndex >= 0 && settings.SelectedInnerTabIndex < selectedOuterTab.InnerTabs.Count)
                            {
                                selectedOuterTab.SelectedInnerTab = selectedOuterTab.InnerTabs[settings.SelectedInnerTabIndex];
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"タブ選択設定の読み込みに失敗しました: {ex.Message}");
            }
        }

        private RandomResolutionSettings LoadRandomResolutionSettings()
        {
            try
            {
                var filePath = GetRandomResolutionSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<RandomResolutionSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Random Resolution設定の読み込みに失敗しました: {ex.Message}");
            }
            return new RandomResolutionSettings(); // デフォルト設定を返す
        }

        private void SaveRandomResolutionSettings(RandomResolutionSettings settings)
        {
            try
            {
                var filePath = GetRandomResolutionSettingsFilePath();
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Random Resolution設定の保存に失敗しました: {ex.Message}");
            }
        }

        // タイトル編集関連のイベントハンドラー
        private void TitleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            var tab = textBlock?.DataContext as TabItemViewModel;
            if (tab == null) return;

            var currentTime = DateTime.Now;
            var timeDiff = currentTime - _lastClickTime;

            // ダブルクリック判定（500ms以内の2回目のクリック）
            if (timeDiff.TotalMilliseconds < 500 && _lastClickedTab == tab)
            {
                StartEditingTitle(textBlock);
                e.Handled = true; // ダブルクリックイベントを処理済みとしてマーク
            }

            _lastClickTime = currentTime;
            _lastClickedTab = tab;
        }

        private void StartEditingTitle(TextBlock? textBlock)
        {
            if (textBlock?.DataContext is TabItemViewModel tab)
            {
                // 編集モードに切り替え
                tab.IsEditingTitle = true;

                // TextBoxを表示してTextBlockを非表示にする
                var parent = textBlock.Parent as StackPanel;
                if (parent != null)
                {
                    var textBox = parent.Children.OfType<TextBox>().FirstOrDefault();
                    if (textBox != null)
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                        textBox.Visibility = Visibility.Visible;
                        
                        // TextBoxにフォーカスを設定し、テキストを全選択
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            var tab = textBox?.DataContext as TabItemViewModel;
            if (tab == null) return;

            if (e.Key == Key.Enter)
            {
                // Enterキーで編集完了
                FinishEditingTitle(textBox);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Escapeキーで編集キャンセル
                CancelEditingTitle(textBox);
                e.Handled = true;
            }
        }

        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            FinishEditingTitle(textBox);
        }

        private void FinishEditingTitle(TextBox? textBox)
        {
            if (textBox?.DataContext is TabItemViewModel tab)
            {
                // 編集モードを終了
                tab.IsEditingTitle = false;

                // TextBlockを表示してTextBoxを非表示にする
                var parent = textBox.Parent as StackPanel;
                if (parent != null)
                {
                    var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBlock != null)
                    {
                        textBox.Visibility = Visibility.Collapsed;
                        textBlock.Visibility = Visibility.Visible;
                    }
                }

                // タブデータを保存
                var vm = (MainViewModel)DataContext;
                vm.SaveToFile(GetDataFilePath());
            }
        }

        private void CancelEditingTitle(TextBox? textBox)
        {
            if (textBox?.DataContext is TabItemViewModel tab)
            {
                // 編集をキャンセル（元のタイトルに戻す）
                textBox.Text = tab.Title;
                
                // 編集モードを終了
                tab.IsEditingTitle = false;

                // TextBlockを表示してTextBoxを非表示にする
                var parent = textBox.Parent as StackPanel;
                if (parent != null)
                {
                    var textBlock = parent.Children.OfType<TextBlock>().FirstOrDefault();
                    if (textBlock != null)
                    {
                        textBox.Visibility = Visibility.Collapsed;
                        textBlock.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        // 画像クリック時のイベントハンドラー
        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var image = sender as Image;
            var imagePath = image?.DataContext as string;
            
            if (!string.IsNullOrEmpty(imagePath))
            {
                // 画像が属する内側タブを特定
                var vm = (MainViewModel)DataContext;
                var outerTab = vm.SelectedTab;
                var innerTab = outerTab?.SelectedInnerTab;
                
                if (innerTab != null)
                {
                    // 画像のインデックスを取得
                    int imageIndex = innerTab.ImagePaths.IndexOf(imagePath);
                    
                    // 拡大表示モードに切り替え
                    innerTab.IsImageExpanded = true;
                    innerTab.ExpandedImagePath = imagePath;
                    innerTab.ExpandedImageIndex = imageIndex;
                }
            }
            
            e.Handled = true;
        }

        private void ExpandedImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 拡大表示を終了して通常表示に戻る
            var vm = (MainViewModel)DataContext;
            var outerTab = vm.SelectedTab;
            var innerTab = outerTab?.SelectedInnerTab;
            
            if (innerTab != null)
            {
                innerTab.IsImageExpanded = false;
                innerTab.ExpandedImagePath = "";
                innerTab.ExpandedImageIndex = -1;
            }
            
            e.Handled = true;
        }

        private void ListBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 通常表示時のListBoxクリック（特に処理なし）
            // このハンドラーは将来的な拡張のために残しておく
        }

        private void ExpandedImageBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 拡大表示の背景がクリックされた場合、拡大表示を終了
            // 画像以外の部分がクリックされた場合のみ処理
            var clickedElement = e.OriginalSource as FrameworkElement;
            
            // クリックされた要素が画像でない場合、拡大表示を終了
            if (clickedElement != null && !(clickedElement is Image))
            {
                var vm = (MainViewModel)DataContext;
                var outerTab = vm.SelectedTab;
                var innerTab = outerTab?.SelectedInnerTab;
                
                if (innerTab != null && innerTab.IsImageExpanded)
                {
                    innerTab.IsImageExpanded = false;
                    innerTab.ExpandedImagePath = "";
                    innerTab.ExpandedImageIndex = -1;
                    e.Handled = true;
                }
            }
        }

        // ドラッグ&ドロップ処理
        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            // ファイルのドラッグかどうかをチェック
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var supportedFiles = files.Where(file => IsSupportedImageFile(file)).ToArray();
                var allImageFiles = files.Where(file => IsImageFile(file)).ToArray();
                
                // 対話的なメタデータ抽出をサポートするファイル（WebP、PNG）がある場合
                if (supportedFiles.Length > 0)
                {
                    if (supportedFiles.Length == 1)
                    {
                        ProcessImageFile(supportedFiles[0]);
                    }
                    else
                    {
                        // 複数のサポートファイル、またはサポートファイルと非サポートファイルが混在
                        ProcessBulkImport(allImageFiles);
                    }
                }
                // サポートファイルがないが、他の画像ファイルがある場合はバルクインポート
                else if (allImageFiles.Length > 0)
                {
                    ProcessBulkImport(allImageFiles);
                }
                else
                {
                    MessageBox.Show("Image file not found.\nSupported formats: WebP, PNG, JPG, JPEG, BMP, GIF", 
                                   "File Format Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            e.Handled = true;
        }

        private bool IsSupportedImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension == ".webp" || 
                   extension == ".png" || 
                   extension == ".jpg" || 
                   extension == ".jpeg";
        }

        private async void ProcessImageFile(string filePath)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentTab = vm.SelectedTab?.SelectedInnerTab ?? vm.SelectedTab;
                
                if (currentTab == null)
                {
                    MessageBox.Show("No tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // メタデータを抽出
                string comment = ExtractImageMetadata(filePath);
                if (!string.IsNullOrEmpty(comment))
                {
                    // メタデータを解析してGUIに反映
                    ParseAndApplyMetadata(comment);
                }
                
                // 画像をタブにセット（既存の画像をクリアしてから）
                await SetImageToCurrentTab(filePath, currentTab);
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(filePath);
                MessageBox.Show($"File: {fileName}\n\nAn error occurred while processing the image file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task SetImageToCurrentTab(string filePath, TabItemViewModel currentTab)
        {
            try
            {
                // 画像をoutputディレクトリにコピー
                var copiedImagePath = await CopyImageToOutputDir(filePath);
                if (copiedImagePath != null)
                {
                    // 現在のタブの画像をクリア
                    currentTab.ImagePaths.Clear();
                    
                    // 新しい画像をセット
                    currentTab.ImagePaths.Add(copiedImagePath);
                    
                    // UIに反映（画像の読み込み）
                    LoadImagesForInnerTab(currentTab);
                    
                    var fileName = Path.GetFileName(filePath);
                    Debug.WriteLine($"画像セット完了: {fileName} -> タブ「{currentTab.Title}」");
                }
            }
            catch (Exception ex)
            {
                var fileName = Path.GetFileName(filePath);
                Debug.WriteLine($"画像セットエラー ({fileName}): {ex.Message}");
                MessageBox.Show($"File: {fileName}\n\nAn error occurred while setting the image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private async Task<string?> CopyImageToOutputDir(string filePath)
        {
            try
            {
                string outputDir = GetImageCacheDir();
                
                // ディレクトリが存在しない場合は作成
                Directory.CreateDirectory(outputDir);
                
                string originalFileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath);
                
                // ユニークなファイル名を生成（タイムスタンプ + GUID）
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string uniqueId = Guid.NewGuid().ToString("N")[..8]; // 8文字のGUID
                string newFileName = $"Import_{timestamp}_{uniqueId}_{originalFileName}{extension}";
                string destinationFilePath = Path.Combine(outputDir, newFileName);
                
                // ファイルが存在するか確認
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"警告: ソースファイルが存在しません: {filePath}");
                    return null;
                }
                
                // 非同期でファイルをコピー
                await Task.Run(() =>
                {
                    File.Copy(filePath, destinationFilePath, overwrite: false);
                });
                
                Debug.WriteLine($"画像コピー完了: {Path.GetFileName(filePath)} -> {newFileName}");
                return destinationFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像コピーエラー ({Path.GetFileName(filePath)}): {ex.Message}");
                return null;
            }
        }

        private string ExtractImageMetadata(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                
                using (var image = new MagickImage(filePath))
                {
                    // まず、ImageMagickの基本的なプロパティをチェック
                    if (!string.IsNullOrEmpty(image.Comment))
                    {
                        return image.Comment;
                    }

                    // EXIFプロファイルをチェック
                    var exifProfile = image.GetExifProfile();
                    if (exifProfile != null)
                    {
                        // EXIFプロファイルの全ての値をチェック
                        foreach (var exifValue in exifProfile.Values)
                        {
                            try
                            {
                                // コメント関連のタグをチェック
                                if (exifValue.Tag == ExifTag.UserComment || 
                                    exifValue.Tag == ExifTag.ImageDescription ||
                                    exifValue.Tag == ExifTag.Software ||
                                    exifValue.Tag == ExifTag.Artist ||
                                    exifValue.Tag == ExifTag.Copyright ||
                                    exifValue.Tag == ExifTag.DocumentName ||
                                    exifValue.Tag == ExifTag.Make ||
                                    exifValue.Tag == ExifTag.Model)
                                {
                                    var commentValue = exifValue.GetValue();
                                    if (commentValue != null)
                                    {
                                        // バイト配列の場合は文字列に変換
                                        if (commentValue is byte[] byteArray)
                                        {
                                            try
                                            {
                                                string? decodedString = null;
                                                string debugInfo = "";
                                                
                                                // デバッグ用：最初の32バイトを表示
                                                var previewBytes = byteArray.Take(32).Select(b => b.ToString()).ToArray();
                                                debugInfo = $"Bytes: [{string.Join(",", previewBytes)}...]";
                                                
                                                // UNICODEプレフィックスをチェック
                                                if (byteArray.Length > 8)
                                                {
                                                    var prefix = Encoding.ASCII.GetString(byteArray, 0, 7); // "UNICODE" = 7文字
                                                    if (prefix == "UNICODE" && byteArray[7] == 0) // UNICODEの後にnull文字
                                                    {
                                                        // バイト8以降がUTF-16 BEデータ（最初の0もデータの一部）
                                                        var dataStart = 8;
                                                        
                                                        debugInfo += $" | Data starts at: {dataStart}";
                                                        
                                                        // UTF-16のnull終端(2バイト)を探す
                                                        var dataEnd = byteArray.Length;
                                                        for (int i = dataStart; i < byteArray.Length - 1; i += 2)
                                                        {
                                                            if (byteArray[i] == 0 && byteArray[i + 1] == 0)
                                                            {
                                                                dataEnd = i;
                                                                break;
                                                            }
                                                        }
                                                        
                                                        debugInfo += $" | Data ends at: {dataEnd}";
                                                        
                                                        if (dataEnd > dataStart)
                                                        {
                                                            var unicodeBytes = new byte[dataEnd - dataStart];
                                                            Array.Copy(byteArray, dataStart, unicodeBytes, 0, dataEnd - dataStart);
                                                            
                                                            // デバッグ用：実際のUnicodeバイトを表示
                                                            var unicodePreview = unicodeBytes.Take(16).Select(b => b.ToString()).ToArray();
                                                            debugInfo += $" | Unicode bytes: [{string.Join(",", unicodePreview)}...]";
                                                            
                                                            // UTF-16 Big Endianで直接デコード
                                                            try
                                                            {
                                                                decodedString = Encoding.BigEndianUnicode.GetString(unicodeBytes);
                                                                if (!string.IsNullOrEmpty(decodedString) && IsValidComment(decodedString))
                                                                {
                                                                    // デバッグ情報は本番では非表示にできるよう、簡潔にする
                                                                    return $"{exifValue.Tag}: {decodedString}";
                                                                }
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                debugInfo += $" | BE decode error: {ex.Message}";
                                                            }
                                                        }
                                                    }
                                                }
                                                
                                                // UNICODEプレフィックスがない場合は他の方法を試す
                                                if (string.IsNullOrEmpty(decodedString))
                                                {
                                                    // UTF-16 LEを試す
                                                    try
                                                    {
                                                        decodedString = Encoding.Unicode.GetString(byteArray).Trim('\0');
                                                        if (!string.IsNullOrEmpty(decodedString))
                                                            debugInfo += $" | UTF-16 decoded: '{decodedString}'";
                                                    }
                                                    catch
                                                    {
                                                        // UTF-8で文字列に変換を試す
                                                        decodedString = Encoding.UTF8.GetString(byteArray).Trim('\0');
                                                        if (!string.IsNullOrEmpty(decodedString))
                                                            debugInfo += $" | UTF-8 decoded: '{decodedString}'";
                                                    }
                                                }
                                                
                                                // 最後の手段としてASCIIを試す
                                                if (string.IsNullOrEmpty(decodedString) || !IsValidComment(decodedString))
                                                {
                                                    var asciiString = Encoding.ASCII.GetString(byteArray).Trim('\0');
                                                    if (!string.IsNullOrEmpty(asciiString))
                                                    {
                                                        decodedString = asciiString;
                                                        debugInfo += $" | ASCII decoded: '{decodedString}'";
                                                    }
                                                }
                                                
                                                if (!string.IsNullOrEmpty(decodedString) && IsValidComment(decodedString))
                                                {
                                                    return $"{exifValue.Tag}: {decodedString}";
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                // 変換に失敗した場合は次の方法を試す
                                                return $"{exifValue.Tag}: デコードエラー - {ex.Message}";
                                            }
                                        }
                                        else if (commentValue is string stringValue)
                                        {
                                            if (!string.IsNullOrEmpty(stringValue) && IsValidComment(stringValue))
                                            {
                                                return $"{exifValue.Tag}: {stringValue}";
                                            }
                                        }
                                        else
                                        {
                                            // その他の型の場合
                                            var stringRepresentation = commentValue.ToString();
                                            if (!string.IsNullOrEmpty(stringRepresentation) && 
                                                stringRepresentation != "System.Byte[]" && 
                                                IsValidComment(stringRepresentation))
                                            {
                                                return $"{exifValue.Tag}: {stringRepresentation}";
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // 特定のタグの取得に失敗した場合は次のタグに進む
                                continue;
                            }
                        }
                    }

                    // XMPプロファイルをチェック（PNG、WebP両方でサポート）
                    if (image.HasProfile("xmp"))
                    {
                        var xmpProfile = image.GetXmpProfile();
                        if (xmpProfile != null)
                        {
                            var xmpData = xmpProfile.ToByteArray();
                            var xmpString = Encoding.UTF8.GetString(xmpData);
                            // XMPデータからコメントを抽出
                            if (xmpString.Contains("description"))
                            {
                                var description = ExtractXmpDescription(xmpString);
                                if (!string.IsNullOrEmpty(description))
                                {
                                    return description;
                                }
                            }
                        }
                    }

                    // PNGファイル特有のテキストチャンクをチェック
                    if (extension == ".png")
                    {
                        var pngComment = ExtractPngTextChunks(filePath);
                        if (!string.IsNullOrEmpty(pngComment))
                        {
                            return pngComment;
                        }
                    }

                    // すべてのアトリビュートをチェック
                    foreach (var attribute in image.AttributeNames)
                    {
                        if (attribute.ToLower().Contains("comment") || 
                            attribute.ToLower().Contains("description") ||
                            attribute.ToLower().Contains("prompt"))
                        {
                            var attributeValue = image.GetAttribute(attribute);
                            if (!string.IsNullOrEmpty(attributeValue))
                            {
                                return $"{attribute}: {attributeValue}";
                            }
                        }
                    }

                    // WebPの場合は追加チャンクからメタデータを読み取り
                    if (extension == ".webp")
                    {
                        return ExtractWebPMetadata(filePath);
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                throw new Exception($"画像メタデータの読み取りに失敗しました: {ex.Message}");
            }
        }

        private string ExtractPngTextChunks(string filePath)
        {
            try
            {
                // PNGファイルのバイナリデータを直接読み取る
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    // PNGヘッダーをチェック
                    var pngSignature = reader.ReadBytes(8);
                    var expectedSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
                    
                    if (!pngSignature.SequenceEqual(expectedSignature))
                        return "";

                    // チャンクを読み取る
                    while (fileStream.Position < fileStream.Length)
                    {
                        if (fileStream.Position + 8 > fileStream.Length)
                            break;

                        // チャンクの長さを読み取る（ビッグエンディアン）
                        var lengthBytes = reader.ReadBytes(4);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(lengthBytes);
                        var chunkLength = BitConverter.ToUInt32(lengthBytes, 0);

                        // チャンクタイプを読み取る
                        var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));

                        // データサイズが異常に大きい場合はスキップ
                        if (chunkLength > 10 * 1024 * 1024) // 10MB制限
                        {
                            break;
                        }

                        // テキストチャンクの場合はデータを読み取る
                        if (chunkType == "tEXt" || chunkType == "zTXt" || chunkType == "iTXt")
                        {
                            if (fileStream.Position + chunkLength + 4 <= fileStream.Length)
                            {
                                var chunkData = reader.ReadBytes((int)chunkLength);
                                var comment = ExtractPngTextData(chunkData, chunkType);
                                if (!string.IsNullOrEmpty(comment))
                                {
                                    // CRCをスキップ
                                    reader.ReadBytes(4);
                                    return comment;
                                }
                                // CRCをスキップ
                                reader.ReadBytes(4);
                            }
                            else
                            {
                                break;
                            }
                        }
                        else if (chunkType == "IEND")
                        {
                            // IENDチャンクに到達したら終了
                            break;
                        }
                        else
                        {
                            // その他のチャンクはスキップ
                            if (fileStream.Position + chunkLength + 4 <= fileStream.Length)
                            {
                                fileStream.Seek(chunkLength + 4, SeekOrigin.Current); // データ + CRC
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PNG テキストチャンク抽出エラー: {ex.Message}");
                return "";
            }
        }

        private string ExtractPngTextData(byte[] chunkData, string chunkType)
        {
            try
            {
                switch (chunkType)
                {
                    case "tEXt":
                        // tEXt: キーワード\0テキスト（Latin-1エンコーディング）
                        var nullIndex = Array.IndexOf(chunkData, (byte)0);
                        if (nullIndex > 0 && nullIndex < chunkData.Length - 1)
                        {
                            var keyword = Encoding.Latin1.GetString(chunkData, 0, nullIndex);
                            var text = Encoding.Latin1.GetString(chunkData, nullIndex + 1, chunkData.Length - nullIndex - 1);
                            
                            // AI画像生成関連のキーワードをチェック
                            if (IsPngRelevantKeyword(keyword))
                            {
                                return $"{keyword}: {text}";
                            }
                        }
                        break;

                    case "zTXt":
                        // zTXt: キーワード\0圧縮メソッド\0圧縮されたテキスト
                        var firstNull = Array.IndexOf(chunkData, (byte)0);
                        if (firstNull > 0 && firstNull < chunkData.Length - 2)
                        {
                            var keyword = Encoding.Latin1.GetString(chunkData, 0, firstNull);
                            var compressionMethod = chunkData[firstNull + 1];
                            
                            if (compressionMethod == 0 && IsPngRelevantKeyword(keyword)) // zlib圧縮
                            {
                                try
                                {
                                    var compressedData = new byte[chunkData.Length - firstNull - 2];
                                    Array.Copy(chunkData, firstNull + 2, compressedData, 0, compressedData.Length);
                                    
                                    // zlib解凍を試す
                                    using (var memoryStream = new MemoryStream(compressedData))
                                    using (var deflateStream = new System.IO.Compression.DeflateStream(memoryStream, System.IO.Compression.CompressionMode.Decompress))
                                    using (var reader = new StreamReader(deflateStream, Encoding.Latin1))
                                    {
                                        var text = reader.ReadToEnd();
                                        return $"{keyword}: {text}";
                                    }
                                }
                                catch
                                {
                                    // 解凍に失敗した場合は次の方法を試す
                                }
                            }
                        }
                        break;

                    case "iTXt":
                        // iTXt: キーワード\0圧縮フラグ\0圧縮メソッド\0言語タグ\0翻訳キーワード\0テキスト
                        var parts = new List<int>();
                        for (int i = 0; i < chunkData.Length; i++)
                        {
                            if (chunkData[i] == 0)
                                parts.Add(i);
                            if (parts.Count >= 4) break;
                        }
                        
                        if (parts.Count >= 4)
                        {
                            var keyword = Encoding.UTF8.GetString(chunkData, 0, parts[0]);
                            var compressionFlag = chunkData[parts[0] + 1];
                            var compressionMethod = chunkData[parts[0] + 2];
                            
                            if (IsPngRelevantKeyword(keyword))
                            {
                                var textStart = parts[3] + 1;
                                if (textStart < chunkData.Length)
                                {
                                    var textData = new byte[chunkData.Length - textStart];
                                    Array.Copy(chunkData, textStart, textData, 0, textData.Length);
                                    
                                    string text;
                                    if (compressionFlag == 1 && compressionMethod == 0) // 圧縮あり
                                    {
                                        try
                                        {
                                            using (var memoryStream = new MemoryStream(textData))
                                            using (var deflateStream = new System.IO.Compression.DeflateStream(memoryStream, System.IO.Compression.CompressionMode.Decompress))
                                            using (var reader = new StreamReader(deflateStream, Encoding.UTF8))
                                            {
                                                text = reader.ReadToEnd();
                                            }
                                        }
                                        catch
                                        {
                                            text = Encoding.UTF8.GetString(textData);
                                        }
                                    }
                                    else
                                    {
                                        text = Encoding.UTF8.GetString(textData);
                                    }
                                    
                                    return $"{keyword}: {text}";
                                }
                            }
                        }
                        break;
                }

                return "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PNG テキストデータ抽出エラー: {ex.Message}");
                return "";
            }
        }

        private bool IsPngRelevantKeyword(string keyword)
        {
            var lowerKeyword = keyword.ToLower();
            return lowerKeyword.Contains("comment") ||
                   lowerKeyword.Contains("description") ||
                   lowerKeyword.Contains("prompt") ||
                   lowerKeyword.Contains("negative") ||
                   lowerKeyword.Contains("parameters") ||
                   lowerKeyword.Contains("generation") ||
                   lowerKeyword.Contains("seed") ||
                   lowerKeyword.Contains("steps") ||
                   lowerKeyword.Contains("cfg") ||
                   lowerKeyword.Contains("sampler") ||
                   lowerKeyword.Contains("model") ||
                   lowerKeyword.Contains("software") ||
                   keyword == "Title" ||
                   keyword == "Author" ||
                   keyword == "Software" ||
                   keyword == "Comment";
        }

        private string ExtractXmpDescription(string xmpData)
        {
            try
            {
                // XMPデータからdescriptionを抽出（簡易実装）
                var startTag = "<dc:description>";
                var endTag = "</dc:description>";
                var startIndex = xmpData.IndexOf(startTag);
                if (startIndex >= 0)
                {
                    startIndex += startTag.Length;
                    var endIndex = xmpData.IndexOf(endTag, startIndex);
                    if (endIndex >= 0)
                    {
                        return xmpData.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                }

                // 別のパターンも試す
                startTag = "<rdf:li>";
                endTag = "</rdf:li>";
                startIndex = xmpData.IndexOf(startTag);
                if (startIndex >= 0)
                {
                    startIndex += startTag.Length;
                    var endIndex = xmpData.IndexOf(endTag, startIndex);
                    if (endIndex >= 0)
                    {
                        return xmpData.Substring(startIndex, endIndex - startIndex).Trim();
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private string ExtractWebPMetadata(string filePath)
        {
            try
            {
                // WebPファイルのバイナリデータを直接読み取る
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(fileStream))
                {
                    // WebPヘッダーをチェック
                    var riffHeader = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (riffHeader != "RIFF")
                        return "";

                    var fileSize = reader.ReadUInt32();
                    var webpHeader = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (webpHeader != "WEBP")
                        return "";

                    // チャンクを読み取る
                    while (fileStream.Position < fileStream.Length - 8)
                    {
                        var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        var chunkSize = reader.ReadUInt32();

                        // 様々なメタデータチャンクをチェック
                        if (chunkType == "EXIF" || chunkType == "META" || chunkType == "XMP " || chunkType == "ICCP")
                        {
                            var chunkData = reader.ReadBytes((int)chunkSize);
                            var comment = ExtractCommentFromChunk(chunkData, chunkType);
                            if (!string.IsNullOrEmpty(comment))
                                return comment;
                        }
                        else
                        {
                            // チャンクをスキップ
                            if (fileStream.Position + chunkSize <= fileStream.Length)
                            {
                                fileStream.Seek(chunkSize, SeekOrigin.Current);
                            }
                            else
                            {
                                break; // ファイル末尾に達した
                            }
                        }

                        // チャンクサイズが奇数の場合は1バイトパディング
                        if (chunkSize % 2 == 1 && fileStream.Position < fileStream.Length)
                            fileStream.Seek(1, SeekOrigin.Current);
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private string ExtractCommentFromChunk(byte[] chunkData, string chunkType)
        {
            try
            {
                var dataString = Encoding.UTF8.GetString(chunkData);
                
                // チャンクタイプに応じた特別な処理
                if (chunkType == "XMP ")
                {
                    // XMPデータの場合
                    return ExtractXmpDescription(dataString);
                }
                
                // 一般的なコメントパターンを検索
                if (dataString.Contains("comment") || dataString.Contains("description") || dataString.Contains("prompt"))
                {
                    return dataString;
                }

                // バイナリデータから可読文字列を抽出
                var result = new StringBuilder();
                bool inText = false;
                for (int i = 0; i < chunkData.Length; i++)
                {
                    var b = chunkData[i];
                    if (b >= 32 && b <= 126) // 印刷可能ASCII文字
                    {
                        result.Append((char)b);
                        inText = true;
                    }
                    else if (inText && (b == 0 || b == 10 || b == 13))
                    {
                        if (result.Length > 10) // 意味のある長さの文字列
                        {
                            var text = result.ToString().Trim();
                            if (!string.IsNullOrEmpty(text) && IsValidComment(text))
                                return text;
                        }
                        result.Clear();
                        inText = false;
                    }
                    else
                    {
                        if (result.Length > 0 && inText)
                        {
                            var text = result.ToString().Trim();
                            if (text.Length > 10 && IsValidComment(text))
                                return text;
                        }
                        result.Clear();
                        inText = false;
                    }
                }

                var finalText = result.ToString().Trim();
                return finalText.Length > 10 && IsValidComment(finalText) ? finalText : "";
            }
            catch
            {
                return "";
            }
        }

        private bool IsValidComment(string text)
        {
            // 意味のあるコメントかどうかを判定
            if (string.IsNullOrWhiteSpace(text))
                return false;
            
            // 最小長チェック
            if (text.Trim().Length < 3)
                return false;
            
            // バイナリデータっぽい文字列を除外
            var nonPrintableCount = text.Count(c => c < 32 && c != 9 && c != 10 && c != 13); // タブ、改行以外の制御文字
            if (nonPrintableCount > text.Length * 0.2) // 20%以上が制御文字の場合は除外（UTF-16の場合はnull文字が多いため緩和）
                return false;
            
            // 意味のない繰り返し文字列を除外
            if (text.All(c => c == text[0])) // 全て同じ文字
                return false;
            
            // よくあるシステム文字列を除外
            var invalidStrings = new[] { "System.Byte[]", "null", "undefined" };
            if (invalidStrings.Any(invalid => text.Contains(invalid)))
                return false;
            
            // プロンプトっぽい文字列は有効とみなす
            var promptIndicators = new[] { "girl", "boy", "1girl", "1boy", "masterpiece", "high quality", "detailed", "anime", "realistic" };
            if (promptIndicators.Any(indicator => text.ToLower().Contains(indicator)))
                return true;
            
            // 一般的な単語が含まれていれば有効
            var commonWords = new[] { "the", "and", "or", "with", "by", "from", "to", "in", "on", "at" };
            if (commonWords.Any(word => text.ToLower().Contains(word)))
                return true;
            
            // 数字とアルファベットの比率をチェック
            var alphaNumericCount = text.Count(c => char.IsLetterOrDigit(c));
            return alphaNumericCount > text.Length * 0.5; // 50%以上が英数字なら有効
        }

        private void ParseAndApplyMetadata(string metadata)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var selectedTab = vm.SelectedTab;
                var selectedInnerTab = selectedTab?.SelectedInnerTab;
                
                if (selectedInnerTab == null)
                {
                    MessageBox.Show("No tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 共通の解析メソッドを使用
                var parsedData = ParseMetadata(metadata);
                if (parsedData != null)
                {
                    // GUIに反映
                    selectedInnerTab.TextBoxValue = parsedData.Prompt;
                    selectedInnerTab.NegativePromptValue = parsedData.NegativePrompt;
                    
                    // パラメータを適用
                    ApplyParametersToTab(parsedData, selectedInnerTab);
                    
                    // データを保存
                    vm.SaveToFile(GetDataFilePath());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while parsing metadata: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// メタデータを解析してパラメータを抽出する共通メソッド
        /// InfoTextsとWebPコメントの両方で使用される
        /// </summary>
        private ParsedMetadata? ParseMetadata(string metadata)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(metadata))
                    return null;

                var result = new ParsedMetadata();

                // UserComment:プレフィックスがある場合は除去
                var cleanMetadata = metadata;
                if (metadata.Contains("UserComment:"))
                {
                    var startIndex = metadata.IndexOf("UserComment:") + "UserComment:".Length;
                    cleanMetadata = metadata.Substring(startIndex).Trim();
                }

                // Negative promptの位置を探す
                var negativePromptIndex = cleanMetadata.IndexOf("Negative prompt:");
                
                if (negativePromptIndex > 0)
                {
                    // プロンプト部分を抽出（先頭から"Negative prompt:"まで）
                    result.Prompt = cleanMetadata.Substring(0, negativePromptIndex).Trim();
                    
                    // Negative prompt以降の部分
                    var remainingText = cleanMetadata.Substring(negativePromptIndex);
                    
                    // Negative promptを抽出（"Negative prompt:"の後から"Steps:"まで）
                    var stepsIndex = remainingText.IndexOf("Steps:");
                    if (stepsIndex > 0)
                    {
                        var negativeStart = remainingText.IndexOf(":") + 1;
                        result.NegativePrompt = remainingText.Substring(negativeStart, stepsIndex - negativeStart).Trim();
                        
                        // パラメータ部分を抽出して構造化
                        var parametersText = remainingText.Substring(stepsIndex);
                        result.RawParameters = parametersText;
                        ParseStructuredParameters(parametersText, result);
                    }
                    else
                    {
                        // Stepsが見つからない場合は、Negative prompt以降全てをnegative promptとして扱う
                        var negativeStart = remainingText.IndexOf(":") + 1;
                        result.NegativePrompt = remainingText.Substring(negativeStart).Trim();
                        result.RawParameters = "";
                    }
                }
                else
                {
                    // Negative promptがない場合、Stepsがあるかチェック
                    var stepsIndex = cleanMetadata.IndexOf("Steps:");
                    if (stepsIndex > 0)
                    {
                        // Stepsまでがプロンプト
                        result.Prompt = cleanMetadata.Substring(0, stepsIndex).Trim();
                        result.NegativePrompt = "";
                        var parametersText = cleanMetadata.Substring(stepsIndex);
                        result.RawParameters = parametersText;
                        ParseStructuredParameters(parametersText, result);
                    }
                    else
                    {
                        // Stepsもない場合は全体をプロンプトとして扱う
                        result.Prompt = cleanMetadata;
                        result.NegativePrompt = "";
                        result.RawParameters = "";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"メタデータ解析エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// パラメータ文字列を構造化して解析する
        /// エスケープされた引用符内のカンマも適切に処理する
        /// </summary>
        private void ParseStructuredParameters(string parametersText, ParsedMetadata result)
        {
            if (string.IsNullOrEmpty(parametersText))
                return;

            try
            {
                // エスケープされた引用符を考慮してパラメータを分割
                var parameters = SplitParametersWithQuotes(parametersText);
                
                foreach (var param in parameters)
                {
                    if (string.IsNullOrEmpty(param))
                        continue;
                        
                    var colonIndex = param.IndexOf(':');
                    if (colonIndex < 0)
                        continue;
                        
                    var key = param.Substring(0, colonIndex).Trim();
                    var value = param.Substring(colonIndex + 1).Trim();
                    
                    // 主要パラメータを個別フィールドに格納
                    switch (key.ToLower())
                    {
                        case "steps":
                            if (int.TryParse(value, out int steps))
                                result.Steps = steps;
                            break;
                            
                        case "seed":
                            if (long.TryParse(value, out long seed))
                                result.Seed = seed;
                            break;
                            
                        case "cfg scale":
                            if (double.TryParse(value, out double cfgScale))
                                result.CfgScale = cfgScale;
                            break;
                            
                        case "sampler":
                            result.SamplingMethod = value;
                            break;
                            
                        case "schedule type":
                            result.ScheduleType = value;
                            break;
                            
                        case "size":
                            // Size: 1024x1360 形式の解析
                            var sizeParts = value.Split('x');
                            if (sizeParts.Length == 2 && 
                                int.TryParse(sizeParts[0], out int width) && 
                                int.TryParse(sizeParts[1], out int height))
                            {
                                result.Width = width;
                                result.Height = height;
                            }
                            else
                            {
                                // 解析に失敗した場合はOtherParametersに格納
                                result.OtherParameters[key] = value;
                            }
                            break;
                            
                        // Hires.fix関連のパラメータ
                        case "hires upscale":
                            if (double.TryParse(value, out double hiresUpscale))
                                result.HiresUpscale = hiresUpscale;
                            break;
                            
                        case "hires steps":
                            if (int.TryParse(value, out int hiresSteps))
                                result.HiresSteps = hiresSteps;
                            break;
                            
                        case "denoising strength":
                            if (double.TryParse(value, out double denoisingStrength))
                                result.DenoisingStrength = denoisingStrength;
                            break;
                            
                        default:
                            // その他のパラメータはDictionaryに格納
                            result.OtherParameters[key] = value;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"構造化パラメータ解析エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// エスケープされた引用符を考慮してパラメータを分割する
        /// </summary>
        private List<string> SplitParametersWithQuotes(string parametersText)
        {
            var parameters = new List<string>();
            var currentParam = new StringBuilder();
            bool inQuotes = false;
            bool escaped = false;
            
            for (int i = 0; i < parametersText.Length; i++)
            {
                char c = parametersText[i];
                
                if (escaped)
                {
                    // エスケープされた文字はそのまま追加
                    currentParam.Append(c);
                    escaped = false;
                    continue;
                }
                
                if (c == '\\')
                {
                    // エスケープ文字
                    escaped = true;
                    currentParam.Append(c);
                    continue;
                }
                
                if (c == '"')
                {
                    // 引用符の開始/終了
                    inQuotes = !inQuotes;
                    currentParam.Append(c);
                    continue;
                }
                
                if (c == ',' && !inQuotes)
                {
                    // 引用符の外のカンマ：パラメータの区切り
                    if (currentParam.Length > 0)
                    {
                        parameters.Add(currentParam.ToString().Trim());
                        currentParam.Clear();
                    }
                    continue;
                }
                
                // その他の文字はそのまま追加
                currentParam.Append(c);
            }
            
            // 最後のパラメータを追加
            if (currentParam.Length > 0)
            {
                parameters.Add(currentParam.ToString().Trim());
            }
            
            return parameters;
        }

        /// <summary>
        /// 解析されたパラメータをタブに適用する
        /// </summary>
        private void ApplyParametersToTab(ParsedMetadata parsedData, TabItemViewModel tab)
        {
            if (parsedData == null)
                return;

            // 主要パラメータを直接適用
            if (parsedData.Steps.HasValue)
                tab.Steps = parsedData.Steps.Value;
                
            if (parsedData.Seed.HasValue)
                tab.Seed = parsedData.Seed.Value;
                
            if (parsedData.CfgScale.HasValue)
                tab.CfgScale = parsedData.CfgScale.Value;
                
            if (parsedData.Width.HasValue)
                tab.Width = parsedData.Width.Value;
                
            if (parsedData.Height.HasValue)
                tab.Height = parsedData.Height.Value;
                
            // Sampler/Scheduler の適用を非同期で実行し、確実にリストに追加してから選択
            if (!string.IsNullOrEmpty(parsedData.SamplingMethod) || !string.IsNullOrEmpty(parsedData.ScheduleType))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // SamplerManagerとSchedulerManagerで確実にリストに追加
                        if (!string.IsNullOrEmpty(parsedData.SamplingMethod))
                        {
                            await SamplerManager.Instance.EnsureSamplerExistsAsync(parsedData.SamplingMethod);
                        }
                        
                        if (!string.IsNullOrEmpty(parsedData.ScheduleType))
                        {
                            await SchedulerManager.Instance.EnsureSchedulerExistsAsync(parsedData.ScheduleType);
                        }
                        
                        // UIスレッドで選択を設定
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (!string.IsNullOrEmpty(parsedData.SamplingMethod))
                            {
                                // TabのInitializeListsAsyncを呼び出してから選択を設定
                                tab.SelectedSamplingMethod = parsedData.SamplingMethod;
                                Debug.WriteLine($"メタデータからサンプラー設定: {parsedData.SamplingMethod}");
                            }
                            
                            if (!string.IsNullOrEmpty(parsedData.ScheduleType))
                            {
                                tab.SelectedScheduleType = parsedData.ScheduleType;
                                Debug.WriteLine($"メタデータからスケジューラー設定: {parsedData.ScheduleType}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Sampler/Scheduler設定エラー: {ex.Message}");
                    }
                });
            }
            
                         // Hires.fix関連のパラメータを適用
             if (parsedData.HiresUpscale.HasValue)
                 tab.HiresUpscaleBy = parsedData.HiresUpscale.Value;
                 
             if (parsedData.HiresSteps.HasValue)
                 tab.HiresSteps = parsedData.HiresSteps.Value;
                 
             if (parsedData.DenoisingStrength.HasValue)
                 tab.DenoisingStrength = parsedData.DenoisingStrength.Value;
                 
             // Hires stepsの有無によってhires.fixの有効/無効を制御
             if (parsedData.HiresSteps.HasValue)
             {
                 // Hires stepsが0より大きい場合はhires.fixを有効にする
                 if (parsedData.HiresSteps.Value > 0)
                 {
                     tab.EnableHiresFix = true;
                 }
                 else
                 {
                     // Hires stepsが0の場合はhires.fixを無効にする
                     tab.EnableHiresFix = false;
                 }
             }
             else
             {
                 // Hires stepsが存在しない場合はhires.fixを無効にする（値は変えない）
                 tab.EnableHiresFix = false;
             }
             
             // その他のパラメータの処理（将来的な拡張のため）
             foreach (var otherParam in parsedData.OtherParameters)
             {
                 // 必要に応じて特定のパラメータを処理
                 System.Diagnostics.Debug.WriteLine($"その他のパラメータ: {otherParam.Key} = {otherParam.Value}");
             }
                 
             Debug.WriteLine("メタデータのパラメータを適用しました");
         }

        /// <summary>
        /// 旧形式のパラメータ適用メソッド（下位互換性のため）
        /// </summary>
        private void ApplyParametersToTab(string parametersText, TabItemViewModel tab)
        {
            try
            {
                if (string.IsNullOrEmpty(parametersText))
                    return;

                // 旧形式のパラメータ解析（レガシーサポート）
                var tempMetadata = new ParsedMetadata { RawParameters = parametersText };
                ParseStructuredParameters(parametersText, tempMetadata);
                ApplyParametersToTab(tempMetadata, tab);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"レガシーパラメータ適用エラー: {ex.Message}");
            }
        }

        // 古いメソッドは下位互換性のために残す（非推奨）
        private void ExtractAndApplyParameters(string parametersText, TabItemViewModel tab)
        {
            ApplyParametersToTab(parametersText, tab);
        }

        /// <summary>
        /// 解析されたメタデータを格納するクラス
        /// </summary>
        public class ParsedMetadata
        {
            public string Prompt { get; set; } = "";
            public string NegativePrompt { get; set; } = "";
            
            // 主要パラメータを個別フィールドとして保持
            public long? Seed { get; set; }
            public int? Steps { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
            public double? CfgScale { get; set; }
            public string? SamplingMethod { get; set; }
            public string? ScheduleType { get; set; }
            
            // Hires.fix関連のパラメータ
            public double? HiresUpscale { get; set; }
            public int? HiresSteps { get; set; }
            public double? DenoisingStrength { get; set; }
            
            // その他のパラメータをDictionaryとして保持
            public Dictionary<string, string> OtherParameters { get; set; } = new();
            
            // 元のパラメータ文字列（デバッグ用）
            public string RawParameters { get; set; } = "";
        }

        private void OuterTabHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // ドラッグ開始位置を記録
            if (sender is StackPanel stackPanel)
            {
                var tabControl = FindAncestor<TabControl>(stackPanel);
                if (tabControl != null)
                {
                    _dragStartPoint = e.GetPosition(tabControl);
                }
            }
        }

        private void InnerTabHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 内側タブ用のドラッグ開始位置を記録
            if (sender is StackPanel stackPanel)
            {
                var tabControl = FindAncestor<TabControl>(stackPanel);
                if (tabControl != null)
                {
                    _innerDragStartPoint = e.GetPosition(tabControl);
                    Debug.WriteLine($"内側タブドラッグ開始点設定: {_innerDragStartPoint}");
                }
            }
        }

        private void InnerTabControl_DragOver(object sender, DragEventArgs e)
        {
            // ファイルドロップの場合は最優先で許可
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                return;
            }

            // TabItemViewModelのドラッグの場合
            if (e.Data.GetDataPresent(typeof(TabItemViewModel)))
            {
                var tabControl = sender as TabControl;
                var outerTab = tabControl?.DataContext as TabItemViewModel;
                var sourceTab = e.Data.GetData(typeof(TabItemViewModel)) as TabItemViewModel;
                
                // 同じ外側タブ内のタブのドラッグのみ許可
                if (outerTab != null && sourceTab != null && outerTab.InnerTabs.Contains(sourceTab))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                // その他のドラッグは拒否
                e.Effects = DragDropEffects.None;
            }
            
            e.Handled = true;
        }

        /// <summary>
        /// バックグラウンドでガベージコレクションを実行
        /// </summary>
        private void RunBackgroundGC()
        {
            // 最後のGC実行から15秒以内の場合はスキップ（メモリプレッシャー軽減）
            var now = DateTime.Now;
            if ((now - _lastGCTime).TotalSeconds < 15)
            {
                Debug.WriteLine("GC実行をスキップ（間隔が短すぎます）");
                return;
            }
            
            _lastGCTime = now;
            
            Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine("バックグラウンドGC開始");
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // 使用メモリ量を記録（GC前）
                    long memoryBefore = GC.GetTotalMemory(false);
                    
                    // 世代0と世代1のGCを実行
                    GC.Collect(1, GCCollectionMode.Optimized);
                    
                    // ファイナライザーキューを処理
                    GC.WaitForPendingFinalizers();
                    
                    // 再度GCを実行してファイナライザーで解放されたオブジェクトを回収
                    GC.Collect(1, GCCollectionMode.Optimized);
                    
                    // 使用メモリ量を記録（GC後）
                    long memoryAfter = GC.GetTotalMemory(false);
                    stopwatch.Stop();
                    
                    double memoryBeforeMB = memoryBefore / (1024.0 * 1024.0);
                    double memoryAfterMB = memoryAfter / (1024.0 * 1024.0);
                    double freedMB = (memoryBefore - memoryAfter) / (1024.0 * 1024.0);
                    
                    Debug.WriteLine($"バックグラウンドGC完了 - 実行時間: {stopwatch.ElapsedMilliseconds}ms, " +
                                  $"メモリ: {memoryBeforeMB:F1}MB → {memoryAfterMB:F1}MB (解放: {freedMB:F1}MB)");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"バックグラウンドGCエラー: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// リサイクル機能: 現在のタブの最初の画像からseedを読み取ってUIにセットする
        /// </summary>
        private void RecycleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var selectedTab = vm.SelectedTab;
                var selectedInnerTab = selectedTab?.SelectedInnerTab;
                
                if (selectedInnerTab == null)
                {
                    MessageBox.Show("No tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 最初の画像ファイルを取得
                if (selectedInnerTab.ImagePaths == null || selectedInnerTab.ImagePaths.Count == 0)
                {
                    MessageBox.Show("Image not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var firstImagePath = selectedInnerTab.ImagePaths[0];
                if (!File.Exists(firstImagePath))
                {
                    MessageBox.Show($"画像ファイルが存在しません: {Path.GetFileName(firstImagePath)}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 画像からコメントを抽出
                var comment = ExtractImageMetadata(firstImagePath);
                if (string.IsNullOrEmpty(comment))
                {
                    MessageBox.Show("画像からメタデータを読み取れませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // コメントを解析してseedを取得
                var parsedData = ParseMetadata(comment);
                if (parsedData?.Seed == null)
                {
                    MessageBox.Show("画像からseedを読み取れませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // UIのSeedにセット
                selectedInnerTab.Seed = parsedData.Seed.Value;
                
                // タブデータを保存
                vm.SaveToFile(GetDataFilePath());
                
                Debug.WriteLine($"リサイクル機能: Seed {parsedData.Seed.Value} を設定しました (画像: {Path.GetFileName(firstImagePath)})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during recycle processing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"リサイクル処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Seed上ボタンがクリックされた時の処理
        /// </summary>
        private void SeedUpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var selectedTab = vm.SelectedTab;
                var selectedInnerTab = selectedTab?.SelectedInnerTab;
                
                if (selectedInnerTab != null)
                {
                    selectedInnerTab.Seed++;
                    
                    // タブデータを保存
                    vm.SaveToFile(GetDataFilePath());
                    
                    Debug.WriteLine($"Seed incremented to: {selectedInnerTab.Seed}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Seed increment error: {ex.Message}");
            }
        }

        /// <summary>
        /// Seed下ボタンがクリックされた時の処理
        /// </summary>
        private void SeedDownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var selectedTab = vm.SelectedTab;
                var selectedInnerTab = selectedTab?.SelectedInnerTab;
                
                if (selectedInnerTab != null)
                {
                    selectedInnerTab.Seed--;
                    
                    // タブデータを保存
                    vm.SaveToFile(GetDataFilePath());
                    
                    Debug.WriteLine($"Seed decremented to: {selectedInnerTab.Seed}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Seed decrement error: {ex.Message}");
            }
        }

        /// <summary>
        /// SeedTextBoxでキーが押された時の処理
        /// 上下矢印キーでSeedを増減する
        /// </summary>
        private void SeedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var selectedTab = vm.SelectedTab;
                var selectedInnerTab = selectedTab?.SelectedInnerTab;
                
                if (selectedInnerTab != null)
                {
                    if (e.Key == Key.Up)
                    {
                        selectedInnerTab.Seed++;
                        vm.SaveToFile(GetDataFilePath());
                        e.Handled = true; // イベントを処理済みとしてマーク
                        Debug.WriteLine($"Seed incremented to: {selectedInnerTab.Seed} (Up arrow key)");
                    }
                    else if (e.Key == Key.Down)
                    {
                        selectedInnerTab.Seed--;
                        vm.SaveToFile(GetDataFilePath());
                        e.Handled = true; // イベントを処理済みとしてマーク
                        Debug.WriteLine($"Seed decremented to: {selectedInnerTab.Seed} (Down arrow key)");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Seed keyboard navigation error: {ex.Message}");
            }
        }

        /// <summary>
        /// 参照されていない画像ファイルをゴミ箱に移動する
        /// </summary>
        private async Task CleanupUnreferencedImagesAsync()
        {
            try
            {
                await Task.Delay(3000); // 起動後3秒待機してからクリーンアップ開始
                
                string legacyOutputDir = GetImageCacheDir();
                
                if (!Directory.Exists(legacyOutputDir))
                {
                    Debug.WriteLine($"画像フォルダが存在しません: {legacyOutputDir}");
                    return;
                }
                
                Debug.WriteLine("画像クリーンアップを開始します...");
                
                // 画像フォルダ内の全ファイルを取得
                var allImageFiles = Directory.GetFiles(legacyOutputDir, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                    .Where(file => IsImageFile(file))
                    .ToList();
                
                Debug.WriteLine($"画像フォルダ内のファイル数: {allImageFiles.Count}");
                
                // タブデータから参照されている画像パスを収集
                var referencedImagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // UIスレッドでタブデータにアクセス
                await this.Dispatcher.InvokeAsync(() =>
                {
                    var vm = (MainViewModel)DataContext;
                    foreach (var outerTab in vm.Tabs)
                    {
                        foreach (var innerTab in outerTab.InnerTabs)
                        {
                            foreach (var imagePath in innerTab.ImagePaths)
                            {
                                if (!string.IsNullOrEmpty(imagePath))
                                {
                                    referencedImagePaths.Add(Path.GetFullPath(imagePath));
                                }
                            }
                        }
                    }
                });
                
                Debug.WriteLine($"参照されている画像数: {referencedImagePaths.Count}");
                
                // 参照されていないファイルを特定
                var unreferencedFiles = allImageFiles
                    .Where(file => !referencedImagePaths.Contains(Path.GetFullPath(file)))
                    .ToList();
                
                Debug.WriteLine($"参照されていない画像数: {unreferencedFiles.Count}");
                
                if (unreferencedFiles.Count == 0)
                {
                    Debug.WriteLine("クリーンアップ対象のファイルはありません。");
                    return;
                }
                
                // ファイルをゴミ箱に移動
                int deletedCount = 0;
                int errorCount = 0;
                
                foreach (var file in unreferencedFiles)
                {
                    try
                    {
                        // ファイルが使用中でないかチェック
                        if (IsFileInUse(file))
                        {
                            Debug.WriteLine($"ファイルが使用中のためスキップ: {Path.GetFileName(file)}");
                            continue;
                        }
                        
                        // ゴミ箱に移動
                        FileSystem.DeleteFile(file, 
                            UIOption.OnlyErrorDialogs, 
                            RecycleOption.SendToRecycleBin);
                        
                        deletedCount++;
                        Debug.WriteLine($"ゴミ箱に移動: {Path.GetFileName(file)}");
                        
                        // 大量のファイルがある場合は少し待機
                        if (deletedCount % 10 == 0)
                        {
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Debug.WriteLine($"ファイル削除エラー ({Path.GetFileName(file)}): {ex.Message}");
                    }
                }
                
                Debug.WriteLine($"画像クリーンアップ完了: {deletedCount}個のファイルをゴミ箱に移動, {errorCount}個のエラー");
                
                // UIスレッドで完了通知（デバッグ用）
                if (deletedCount > 0)
                {
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        Debug.WriteLine($"クリーンアップ完了: {deletedCount}個の未参照画像をゴミ箱に移動しました。");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像クリーンアップエラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ファイルが画像ファイルかどうかを判定
        /// </summary>
        private bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension == ".webp" || extension == ".png" || extension == ".jpg" || 
                   extension == ".jpeg" || extension == ".bmp" || extension == ".gif";
        }
        
        /// <summary>
        /// ファイルが使用中かどうかを判定
        /// </summary>
        private bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false; // ファイルを開けた場合は使用中ではない
                }
            }
            catch (IOException)
            {
                return true; // ファイルが使用中
            }
            catch
            {
                return false; // その他のエラーは使用中ではないと判定
            }
        }

        /// <summary>
        /// 複数画像ファイルからメタデータを読み取り、新しいタブを作成するBulk import処理
        /// </summary>
        private async void ProcessBulkImport(string[] imageFiles)
        {
            var vm = (MainViewModel)DataContext;
            
            try
            {
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Bulk import開始
                vm.IsBulkImporting = true;
                vm.BulkImportProgress = 0.0;
                vm.BulkImportStatus = "Bulk import を開始しています...";
                
                var successCount = 0;
                var errorCount = 0;
                var duplicateCount = 0;
                var errorMessages = new List<string>();
                var processedMetadata = new List<ParsedMetadata>();
                
                Debug.WriteLine($"Bulk import開始: {imageFiles.Length}ファイル -> 親タブ「{currentOuterTab.Title}」");
                
                // 第1段階：全ての画像からメタデータを抽出
                vm.BulkImportStatus = "画像からメタデータを抽出中...";
                var imageMetadataList = new List<(string filePath, ParsedMetadata? metadata)>();
                
                for (int i = 0; i < imageFiles.Length; i++)
                {
                    var filePath = imageFiles[i];
                    
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        Debug.WriteLine($"メタデータ抽出中 ({i + 1}/{imageFiles.Length}): {fileName}");
                        
                        // プログレス更新（第1段階は全体の70%）
                        var progress = (double)(i + 1) / imageFiles.Length * 70.0;
                        vm.BulkImportProgress = progress;
                        vm.BulkImportStatus = $"メタデータ抽出中... ({i + 1}/{imageFiles.Length}) {fileName}";
                        
                        var metadata = await ExtractImageMetadataAsync(filePath);
                        imageMetadataList.Add((filePath, metadata));
                        
                        // UIの応答性を保つため少し待機
                        if (i % 3 == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var fileName = Path.GetFileName(filePath);
                        var errorMsg = $"{fileName}: {ex.Message}";
                        errorMessages.Add(errorMsg);
                        Debug.WriteLine($"メタデータ抽出エラー: {errorMsg}");
                        imageMetadataList.Add((filePath, null));
                    }
                }
                
                // 第2段階：重複チェックとタブ作成
                vm.BulkImportStatus = "タブを作成中...";
                var validMetadataList = imageMetadataList.Where(x => x.metadata != null).ToList();
                
                for (int i = 0; i < imageMetadataList.Count; i++)
                {
                    var (filePath, metadata) = imageMetadataList[i];
                    var fileName = Path.GetFileName(filePath);
                    
                    // プログレス更新（第2段階は70%から100%）
                    var progress = 70.0 + (double)(i + 1) / imageMetadataList.Count * 30.0;
                    vm.BulkImportProgress = progress;
                    vm.BulkImportStatus = $"タブ作成中... ({i + 1}/{imageMetadataList.Count}) {fileName}";
                    
                    if (metadata == null)
                    {
                        if (!errorMessages.Any(msg => msg.Contains(fileName)))
                        {
                            errorCount++;
                            var errorMsg = $"{fileName}: メタデータが見つかりませんでした";
                            errorMessages.Add(errorMsg);
                            Debug.WriteLine($"エラー: {errorMsg}");
                        }
                        continue;
                    }
                    
                    // 重複チェック：既に処理されたメタデータと比較
                    if (IsMetadataDuplicate(metadata, processedMetadata))
                    {
                        duplicateCount++;
                        Debug.WriteLine($"重複スキップ: {fileName} (既存のメタデータと一致)");
                        continue;
                    }
                    
                    // ユニークなメタデータの場合、タブを追加
                    vm.AddInnerTab(currentOuterTab);
                    var newInnerTab = currentOuterTab.SelectedInnerTab;
                    
                    if (newInnerTab != null)
                    {
                        // メタデータを新しい内側タブに適用
                        ApplyBulkImportMetadata(metadata, newInnerTab, fileName);
                        
                        // 画像ファイルをlegacyOutputDirにコピー
                        var copiedFilePath = await CopyImageToLegacyOutputDirAsync(filePath);
                        if (!string.IsNullOrEmpty(copiedFilePath))
                        {
                            // コピーされた画像をタブの画像として設定
                            newInnerTab.ImagePaths.Clear();
                            newInnerTab.ImagePaths.Add(copiedFilePath);
                            Debug.WriteLine($"成功: {fileName} -> 内側タブ「{newInnerTab.Title}」(画像コピー済み: {Path.GetFileName(copiedFilePath)})");
                        }
                        else
                        {
                            Debug.WriteLine($"警告: {fileName} -> 内側タブ「{newInnerTab.Title}」(画像コピー失敗、元ファイルを参照)");
                            // コピーに失敗した場合は元ファイルを参照
                            newInnerTab.ImagePaths.Clear();
                            newInnerTab.ImagePaths.Add(filePath);
                        }
                        
                        processedMetadata.Add(metadata);
                        successCount++;
                    }
                    
                    // UIの応答性を保つため少し待機
                    await Task.Delay(50);
                }
                
                // 完了処理
                vm.BulkImportStatus = $"完了！{successCount}個のタブを作成しました。";
                vm.BulkImportProgress = 100.0;
                
                Debug.WriteLine($"Bulk import完了: {successCount}個のタブを作成、{errorCount}個のエラー");
                
                // タブデータを保存
                vm.SaveToFile(GetDataFilePath());
                
                // 待ち時間を削除して即座に終了
            }
            catch (Exception ex)
            {
                vm.BulkImportStatus = $"エラーが発生しました: {ex.Message}";
                await Task.Delay(3000);
                
                MessageBox.Show($"Bulk import処理中に予期しないエラーが発生しました:\n{ex.Message}", 
                               "Bulk Importエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Bulk import致命的エラー: {ex.Message}");
            }
            finally
            {
                // Bulk import終了
                vm.IsBulkImporting = false;
                vm.BulkImportProgress = 0.0;
                vm.BulkImportStatus = "";
            }
        }

        /// <summary>
        /// 画像ファイルからメタデータを非同期で抽出
        /// </summary>
        private async Task<ParsedMetadata?> ExtractImageMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 統一されたExtractImageMetadataメソッドを使用
                    var comment = ExtractImageMetadata(filePath);
                    if (!string.IsNullOrEmpty(comment))
                    {
                        return ParseMetadata(comment);
                    }
                    
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"メタデータ抽出エラー ({Path.GetFileName(filePath)}): {ex.Message}");
                    return null;
                }
            });
        }
        
        /// <summary>
        /// 一般的な画像形式からメタデータを抽出
        /// </summary>
        private ParsedMetadata? ExtractGeneralImageMetadata(string filePath)
        {
            try
            {
                using (var image = new MagickImage(filePath))
                {
                    // まず、ImageMagickの基本的なプロパティをチェック
                    if (!string.IsNullOrEmpty(image.Comment))
                    {
                        var parsed = ParseMetadata(image.Comment);
                        if (parsed != null) return parsed;
                    }
                    
                    // EXIFプロファイルをチェック
                    var exifProfile = image.GetExifProfile();
                    if (exifProfile != null)
                    {
                        foreach (var exifValue in exifProfile.Values)
                        {
                            try
                            {
                                // コメント関連のタグをチェック
                                if (exifValue.Tag == ExifTag.UserComment || 
                                    exifValue.Tag == ExifTag.ImageDescription ||
                                    exifValue.Tag == ExifTag.Software)
                                {
                                    var commentValue = exifValue.GetValue();
                                    if (commentValue != null)
                                    {
                                        string? comment = null;
                                        
                                        if (commentValue is byte[] byteArray)
                                        {
                                            // バイト配列を文字列に変換
                                            comment = TryDecodeByteArray(byteArray);
                                        }
                                        else if (commentValue is string stringValue)
                                        {
                                            comment = stringValue;
                                        }
                                        else
                                        {
                                            comment = commentValue.ToString();
                                        }
                                        
                                        if (!string.IsNullOrEmpty(comment) && IsValidComment(comment))
                                        {
                                            var parsed = ParseMetadata(comment);
                                            if (parsed != null) return parsed;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // 特定のタグの取得に失敗した場合は次のタグに進む
                                continue;
                            }
                        }
                    }
                    
                    // XMPプロファイルをチェック
                    if (image.HasProfile("xmp"))
                    {
                        var xmpProfile = image.GetXmpProfile();
                        if (xmpProfile != null)
                        {
                            var xmpData = xmpProfile.ToByteArray();
                            var xmpString = Encoding.UTF8.GetString(xmpData);
                            var description = ExtractXmpDescription(xmpString);
                            if (!string.IsNullOrEmpty(description))
                            {
                                var parsed = ParseMetadata(description);
                                if (parsed != null) return parsed;
                            }
                        }
                    }
                    
                    // すべてのアトリビュートをチェック
                    foreach (var attribute in image.AttributeNames)
                    {
                        if (attribute.ToLower().Contains("comment") || 
                            attribute.ToLower().Contains("description") ||
                            attribute.ToLower().Contains("prompt"))
                        {
                            var attributeValue = image.GetAttribute(attribute);
                            if (!string.IsNullOrEmpty(attributeValue) && IsValidComment(attributeValue))
                            {
                                var parsed = ParseMetadata(attributeValue);
                                if (parsed != null) return parsed;
                            }
                        }
                    }
                    
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"一般画像メタデータ抽出エラー: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// バイト配列を文字列にデコードを試行
        /// </summary>
        private string? TryDecodeByteArray(byte[] byteArray)
        {
            try
            {
                // UNICODEプレフィックスをチェック
                if (byteArray.Length > 8)
                {
                    var prefix = Encoding.ASCII.GetString(byteArray, 0, 7);
                    if (prefix == "UNICODE" && byteArray[7] == 0)
                    {
                        var dataStart = 8;
                        var dataEnd = byteArray.Length;
                        
                        // UTF-16のnull終端を探す
                        for (int i = dataStart; i < byteArray.Length - 1; i += 2)
                        {
                            if (byteArray[i] == 0 && byteArray[i + 1] == 0)
                            {
                                dataEnd = i;
                                break;
                            }
                        }
                        
                        if (dataEnd > dataStart)
                        {
                            var unicodeBytes = new byte[dataEnd - dataStart];
                            Array.Copy(byteArray, dataStart, unicodeBytes, 0, dataEnd - dataStart);
                            return Encoding.BigEndianUnicode.GetString(unicodeBytes);
                        }
                    }
                }
                
                // UTF-16 LEを試す
                var utf16String = Encoding.Unicode.GetString(byteArray).Trim('\0');
                if (!string.IsNullOrEmpty(utf16String) && IsValidComment(utf16String))
                    return utf16String;
                
                // UTF-8を試す
                var utf8String = Encoding.UTF8.GetString(byteArray).Trim('\0');
                if (!string.IsNullOrEmpty(utf8String) && IsValidComment(utf8String))
                    return utf8String;
                
                // ASCIIを試す
                var asciiString = Encoding.ASCII.GetString(byteArray).Trim('\0');
                if (!string.IsNullOrEmpty(asciiString) && IsValidComment(asciiString))
                    return asciiString;
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Bulk import用のメタデータ適用（制限されたパラメータのみ）
        /// Prompt, Negative prompt, width, height, cfg scale, hires.fixのみを復元
        /// Sampling method, schedule type, stepsは復元しない
        /// </summary>
        private void ApplyBulkImportMetadata(ParsedMetadata parsedData, TabItemViewModel tab, string fileName)
        {
            try
            {
                if (parsedData == null)
                    return;

                // プロンプトとネガティブプロンプトを適用
                if (!string.IsNullOrEmpty(parsedData.Prompt))
                    tab.TextBoxValue = parsedData.Prompt;
                    
                if (!string.IsNullOrEmpty(parsedData.NegativePrompt))
                    tab.NegativePromptValue = parsedData.NegativePrompt;
                
                // 寸法を適用
                if (parsedData.Width.HasValue)
                    tab.Width = parsedData.Width.Value;
                    
                if (parsedData.Height.HasValue)
                    tab.Height = parsedData.Height.Value;
                    
                // CFG Scaleを適用
                if (parsedData.CfgScale.HasValue)
                    tab.CfgScale = parsedData.CfgScale.Value;
                
                // Hires.fix関連のパラメータを適用
                if (parsedData.HiresUpscale.HasValue)
                    tab.HiresUpscaleBy = parsedData.HiresUpscale.Value;
                    
                if (parsedData.HiresSteps.HasValue)
                    tab.HiresSteps = parsedData.HiresSteps.Value;
                    
                if (parsedData.DenoisingStrength.HasValue)
                    tab.DenoisingStrength = parsedData.DenoisingStrength.Value;
                
                // Hires stepsの有無によってhires.fixの有効/無効を制御
                if (parsedData.HiresSteps.HasValue)
                {
                    tab.EnableHiresFix = parsedData.HiresSteps.Value > 0;
                }
                else
                {
                    tab.EnableHiresFix = false;
                }
                
                // Seedは常に-1（新しいタブの動作に合わせる）
                tab.Seed = -1;
                
                Debug.WriteLine($"Bulk import適用完了: {fileName}");
                Debug.WriteLine($"  Prompt: {(string.IsNullOrEmpty(parsedData.Prompt) ? "" : parsedData.Prompt.Substring(0, Math.Min(50, parsedData.Prompt.Length)))}...");
                Debug.WriteLine($"  Size: {parsedData.Width}x{parsedData.Height}");
                Debug.WriteLine($"  CFG: {parsedData.CfgScale}");
                Debug.WriteLine($"  Hires.fix: {tab.EnableHiresFix}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Bulk importメタデータ適用エラー ({fileName}): {ex.Message}");
            }
        }

        private bool IsMetadataDuplicate(ParsedMetadata metadata, List<ParsedMetadata> processedMetadata)
        {
            return processedMetadata.Any(existingMetadata =>
                existingMetadata.Prompt == metadata.Prompt &&
                existingMetadata.NegativePrompt == metadata.NegativePrompt &&
                existingMetadata.Steps == metadata.Steps &&
                existingMetadata.Width == metadata.Width &&
                existingMetadata.Height == metadata.Height &&
                existingMetadata.CfgScale == metadata.CfgScale &&
                existingMetadata.SamplingMethod == metadata.SamplingMethod &&
                existingMetadata.ScheduleType == metadata.ScheduleType &&
                existingMetadata.HiresUpscale == metadata.HiresUpscale &&
                existingMetadata.HiresSteps == metadata.HiresSteps &&
                existingMetadata.DenoisingStrength == metadata.DenoisingStrength);
        }

        private async Task<string?> CopyImageToLegacyOutputDirAsync(string filePath)
        {
            try
            {
                string legacyOutputDir = GetImageCacheDir();
                
                // ディレクトリが存在しない場合は作成
                Directory.CreateDirectory(legacyOutputDir);
                
                string originalFileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath);
                
                // ユニークなファイル名を生成（タイムスタンプ + GUID）
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string uniqueId = Guid.NewGuid().ToString("N")[..8]; // 8文字のGUID
                string newFileName = $"BulkImport_{timestamp}_{uniqueId}_{originalFileName}{extension}";
                string destinationFilePath = Path.Combine(legacyOutputDir, newFileName);
                
                // ファイルが存在するか確認
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"警告: ソースファイルが存在しません: {filePath}");
                    return null;
                }
                
                // 非同期でファイルをコピー
                await Task.Run(() =>
                {
                    File.Copy(filePath, destinationFilePath, overwrite: false);
                });
                
                Debug.WriteLine($"画像コピー完了: {Path.GetFileName(filePath)} -> {newFileName}");
                return destinationFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像コピーエラー ({Path.GetFileName(filePath)}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generate All ボタンがクリックされた時の処理
        /// 現在の親タブの全ての子タブで順次Generate実行
        /// </summary>
        private async void GenerateAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.InnerTabs.Count == 0)
                {
                    MessageBox.Show("No child tabs to execute.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                Debug.WriteLine($"Generate All開始: 親タブ「{currentOuterTab.Title}」の{currentOuterTab.InnerTabs.Count}個の子タブで実行");
                
                // 元の選択されていた内側タブを記憶
                var originalSelectedInnerTab = currentOuterTab.SelectedInnerTab;
                
                // 各内側タブで順次Generate実行
                for (int i = 0; i < currentOuterTab.InnerTabs.Count; i++)
                {
                    var innerTab = currentOuterTab.InnerTabs[i];
                    
                    try
                    {
                        Debug.WriteLine($"Generate All ({i + 1}/{currentOuterTab.InnerTabs.Count}): 内側タブ「{innerTab.Title}」");
                        
                        // 内側タブを選択
                        currentOuterTab.SelectedInnerTab = innerTab;
                        
                        // UIの更新を待機
                        await Task.Delay(100);
                        
                        // Generateコマンドが実行可能かチェック
                        if (vm.GenerateCommand?.CanExecute(null) == true)
                        {
                            // Generateコマンドを実行（非同期、レスポンスを待たない）
                            vm.GenerateCommand.Execute(null);
                            Debug.WriteLine($"Generate実行: 内側タブ「{innerTab.Title}」");
                            
                            // 次のタブに移る前に少し待機（UIの応答性を保つため）
                            await Task.Delay(200);
                        }
                        else
                        {
                            Debug.WriteLine($"Generate実行不可: 内側タブ「{innerTab.Title}」");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Generate All エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        // エラーが発生しても次のタブに進む
                        continue;
                    }
                }
                
                // 元の選択されていた内側タブに戻す
                if (originalSelectedInnerTab != null && currentOuterTab.InnerTabs.Contains(originalSelectedInnerTab))
                {
                    currentOuterTab.SelectedInnerTab = originalSelectedInnerTab;
                }
                
                Debug.WriteLine($"Generate All完了: 親タブ「{currentOuterTab.Title}」の{currentOuterTab.InnerTabs.Count}個の子タブで実行完了");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during Generate All processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Generate All致命的エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate Left ボタンがクリックされた時の処理
        /// 現在のタブとその左側のタブで順次Generate実行
        /// </summary>
        private async void GenerateLeftButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.SelectedInnerTab == null)
                {
                    MessageBox.Show("Current tab is not selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 現在のタブのインデックスを取得
                var currentIndex = currentOuterTab.InnerTabs.IndexOf(currentOuterTab.SelectedInnerTab);
                if (currentIndex == -1)
                {
                    MessageBox.Show("Current tab not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 現在のタブから左側のタブを取得（インデックス0から現在のタブまで）
                var tabsToGenerate = currentOuterTab.InnerTabs.Take(currentIndex + 1).ToList();
                
                Debug.WriteLine($"Generate Left開始: 親タブ「{currentOuterTab.Title}」の{tabsToGenerate.Count}個のタブ（左側から現在のタブまで）で実行");
                
                // 元の選択されていた内側タブを記憶
                var originalSelectedInnerTab = currentOuterTab.SelectedInnerTab;
                
                // 左側から現在のタブまでで順次Generate実行
                for (int i = 0; i < tabsToGenerate.Count; i++)
                {
                    var innerTab = tabsToGenerate[i];
                    
                    try
                    {
                        Debug.WriteLine($"Generate Left ({i + 1}/{tabsToGenerate.Count}): 内側タブ「{innerTab.Title}」");
                        
                        // 内側タブを選択
                        currentOuterTab.SelectedInnerTab = innerTab;
                        
                        // UIの更新を待機
                        await Task.Delay(100);
                        
                        // Generateコマンドが実行可能かチェック
                        if (vm.GenerateCommand?.CanExecute(null) == true)
                        {
                            // Generateコマンドを実行（非同期、レスポンスを待たない）
                            vm.GenerateCommand.Execute(null);
                            Debug.WriteLine($"Generate実行: 内側タブ「{innerTab.Title}」");
                            
                            // 次のタブに移る前に少し待機（UIの応答性を保つため）
                            await Task.Delay(200);
                        }
                        else
                        {
                            Debug.WriteLine($"Generate実行不可: 内側タブ「{innerTab.Title}」");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Generate Left エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        // エラーが発生しても次のタブに進む
                        continue;
                    }
                }
                
                // 元の選択されていた内側タブに戻す
                if (originalSelectedInnerTab != null && currentOuterTab.InnerTabs.Contains(originalSelectedInnerTab))
                {
                    currentOuterTab.SelectedInnerTab = originalSelectedInnerTab;
                }
                
                Debug.WriteLine($"Generate Left完了: 親タブ「{currentOuterTab.Title}」の{tabsToGenerate.Count}個のタブで実行完了");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during Generate Left processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Generate Left致命的エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate Right ボタンがクリックされた時の処理
        /// 現在のタブとその右側のタブで順次Generate実行
        /// </summary>
        private async void GenerateRightButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.SelectedInnerTab == null)
                {
                    MessageBox.Show("Current tab is not selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 現在のタブのインデックスを取得
                var currentIndex = currentOuterTab.InnerTabs.IndexOf(currentOuterTab.SelectedInnerTab);
                if (currentIndex == -1)
                {
                    MessageBox.Show("Current tab not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 現在のタブから右側のタブを取得
                var tabsToGenerate = currentOuterTab.InnerTabs.Skip(currentIndex).ToList();
                
                Debug.WriteLine($"Generate Right開始: 親タブ「{currentOuterTab.Title}」の{tabsToGenerate.Count}個のタブ（現在のタブから右側）で実行");
                
                // 元の選択されていた内側タブを記憶
                var originalSelectedInnerTab = currentOuterTab.SelectedInnerTab;
                
                // 現在のタブから右側のタブで順次Generate実行
                for (int i = 0; i < tabsToGenerate.Count; i++)
                {
                    var innerTab = tabsToGenerate[i];
                    
                    try
                    {
                        Debug.WriteLine($"Generate Right ({i + 1}/{tabsToGenerate.Count}): 内側タブ「{innerTab.Title}」");
                        
                        // 内側タブを選択
                        currentOuterTab.SelectedInnerTab = innerTab;
                        
                        // UIの更新を待機
                        await Task.Delay(100);
                        
                        // Generateコマンドが実行可能かチェック
                        if (vm.GenerateCommand?.CanExecute(null) == true)
                        {
                            // Generateコマンドを実行（非同期、レスポンスを待たない）
                            vm.GenerateCommand.Execute(null);
                            Debug.WriteLine($"Generate実行: 内側タブ「{innerTab.Title}」");
                            
                            // 次のタブに移る前に少し待機（UIの応答性を保つため）
                            await Task.Delay(200);
                        }
                        else
                        {
                            Debug.WriteLine($"Generate実行不可: 内側タブ「{innerTab.Title}」");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Generate Right エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        // エラーが発生しても次のタブに進む
                        continue;
                    }
                }
                
                // 元の選択されていた内側タブに戻す
                if (originalSelectedInnerTab != null && currentOuterTab.InnerTabs.Contains(originalSelectedInnerTab))
                {
                    currentOuterTab.SelectedInnerTab = originalSelectedInnerTab;
                }
                
                Debug.WriteLine($"Generate Right完了: 親タブ「{currentOuterTab.Title}」の{tabsToGenerate.Count}個のタブで実行完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generate Right 致命的エラー: {ex.Message}");
                MessageBox.Show($"An error occurred during Generate Right processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// L解像度化ボタンがクリックされた時の処理
        /// 各タブのアスペクト比を計算し、最も近いプリセット（縦長L、横長L、正方L）を選択して解像度を調整
        /// </summary>
        private void UpscaleResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.InnerTabs.Count == 0)
                {
                    MessageBox.Show("No child tabs to process.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                int processedCount = 0;
                int skippedCount = 0;
                
                Debug.WriteLine($"L解像度化開始: 親タブ「{currentOuterTab.Title}」の{currentOuterTab.InnerTabs.Count}個の子タブで実行");
                
                // アプリ設定からプリセット値を動的に取得
                var allPresets = AppSettings.Instance.GetResolutionPresets();
                
                // Lサイズのプリセット値のみを抽出してアスペクト比を計算
                var largePresets = new Dictionary<string, (int width, int height, double aspectRatio)>();
                
                if (allPresets.ContainsKey("縦長L"))
                {
                    (int width, int height) = allPresets["縦長L"];
                    largePresets["縦長L"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("横長L"))
                {
                    (int width, int height) = allPresets["横長L"];
                    largePresets["横長L"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("正方L"))
                {
                    (int width, int height) = allPresets["正方L"];
                    largePresets["正方L"] = (width, height, (double)width / height);
                }
                
                // Lプリセットが見つからない場合はエラー
                if (largePresets.Count == 0)
                {
                    MessageBox.Show("No Large presets found in settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                foreach (var innerTab in currentOuterTab.InnerTabs)
                {
                    try
                    {
                        // 現在の解像度の最大値をチェック
                        int maxDimension = Math.Max(innerTab.Width, innerTab.Height);
                        
                        // 現在のタブのアスペクト比を計算
                        double currentAspectRatio = (double)innerTab.Width / innerTab.Height;
                            
                        // 最も近いプリセットを見つける
                        string bestPresetName = "";
                        double minAspectRatioDiff = double.MaxValue;
                        (int width, int height, double aspectRatio) bestPreset = (0, 0, 0);
                            
                        foreach (var preset in largePresets)
                        {
                            double aspectRatioDiff = Math.Abs(currentAspectRatio - preset.Value.aspectRatio);
                            if (aspectRatioDiff < minAspectRatioDiff)
                            {
                                minAspectRatioDiff = aspectRatioDiff;
                                bestPresetName = preset.Key;
                                bestPreset = preset.Value;
                            }
                        }
                            
                        // 最も近いプリセットの長辺を固定し、短辺をアスペクト比から計算
                        int newWidth, newHeight;
                        int longSide = Math.Max(bestPreset.width, bestPreset.height);
                            
                        if (currentAspectRatio >= 1.0) // 横長または正方形
                        {
                            newWidth = longSide;
                            newHeight = (int)Math.Round(longSide / currentAspectRatio);
                        }
                        else // 縦長
                        {
                            newHeight = longSide;
                            newWidth = (int)Math.Round(longSide * currentAspectRatio);
                        }
                            
                        // 新しい解像度を設定
                        innerTab.Width = newWidth;
                        innerTab.Height = newHeight;
                            
                        // Hires.fixを無効化
                        innerTab.EnableHiresFix = false;
                            
                        Debug.WriteLine($"L解像度化 (プリセット「{bestPresetName}」, アスペクト比{currentAspectRatio:F3}): 内側タブ「{innerTab.Title}」 -> {newWidth}×{newHeight}");
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"L解像度化エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        skippedCount++;
                    }
                }
                
                // タブデータを保存
                vm.SaveToFile(GetDataFilePath());
                string message = $"L解像度化が完了しました。\n\n処理済み: {processedCount}個\nスキップ: {skippedCount}個";
                
                Debug.WriteLine($"L解像度化完了: 親タブ「{currentOuterTab.Title}」 - 処理済み{processedCount}個、スキップ{skippedCount}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"L解像度化 致命的エラー: {ex.Message}");
                MessageBox.Show($"An error occurred during L resolutionize processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// hires.fix化ボタンがクリックされた時の処理
        /// 現在の親タブの全ての子タブでHires.fixを有効化（最大解像度1500px以下のみ）
        /// </summary>
        private void EnableHiresFixButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            if (viewModel?.SelectedTab == null)
            {
                MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var outerTab = viewModel.SelectedTab;
            if (outerTab.InnerTabs.Count == 0)
            {
                MessageBox.Show("No child tabs to process.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int enabledCount = 0;
            int alreadyEnabledCount = 0;
            int skippedCount = 0;

            foreach (var innerTab in outerTab.InnerTabs)
            {
                if (innerTab.EnableHiresFix)
                {
                    alreadyEnabledCount++;
                    Debug.WriteLine($"既に有効: {innerTab.Title}");
                    continue;
                }

                // Hires.fixを有効化
                innerTab.EnableHiresFix = true;

                // デフォルト値を設定（現在値が適切でない場合のみ）
                if (innerTab.HiresUpscaleBy < 1.0)
                {
                    innerTab.HiresUpscaleBy = 1.5;
                }

                if (innerTab.HiresSteps <= 0)
                {
                    innerTab.HiresSteps = 6;
                }

                if (innerTab.DenoisingStrength <= 0.0)
                {
                    innerTab.DenoisingStrength = 0.7;
                }

                enabledCount++;
                Debug.WriteLine($"Hires.fix有効化: {innerTab.Title} (解像度: {innerTab.Width}×{innerTab.Height})");
            }

            // 設定を保存
            SaveLastGenerateSettings();

            // 結果を表示
            var resultMessage = $"Hires.fix化完了:\n" +
                              $"• 有効化: {enabledCount}タブ\n" +
                              $"• 既に有効: {alreadyEnabledCount}タブ\n" +
                              $"• スキップ(解像度1500px超): {skippedCount}タブ";

            Debug.WriteLine($"Hires.fix化完了 - 有効化: {enabledCount}, 既に有効: {alreadyEnabledCount}, スキップ: {skippedCount}");
        }

        private void DownscaleResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.InnerTabs.Count == 0)
                {
                    MessageBox.Show("No child tabs to process.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                int processedCount = 0;
                int skippedCount = 0;
                
                Debug.WriteLine($"S解像度化開始: 親タブ「{currentOuterTab.Title}」の{currentOuterTab.InnerTabs.Count}個の子タブで実行");
                
                // アプリ設定からプリセット値を動的に取得
                var allPresets = AppSettings.Instance.GetResolutionPresets();
                
                // Sサイズのプリセット値のみを抽出してアスペクト比を計算
                var smallPresets = new Dictionary<string, (int width, int height, double aspectRatio)>();
                
                if (allPresets.ContainsKey("縦長S"))
                {
                    (int width, int height) = allPresets["縦長"];
                    smallPresets["縦長"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("横長"))
                {
                    (int width, int height) = allPresets["横長"];
                    smallPresets["横長"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("正方"))
                {
                    (int width, int height) = allPresets["正方"];
                    smallPresets["正方"] = (width, height, (double)width / height);
                }
                
                // Sプリセットが見つからない場合はエラー
                if (smallPresets.Count == 0)
                {
                    MessageBox.Show("No Small presets found in settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                foreach (var innerTab in currentOuterTab.InnerTabs)
                {
                    try
                    {
                        // 現在のタブのアスペクト比を計算
                        double currentAspectRatio = (double)innerTab.Width / innerTab.Height;
                            
                        // 最も近いプリセットを見つける
                        string bestPresetName = "";
                        double minAspectRatioDiff = double.MaxValue;
                        (int width, int height, double aspectRatio) bestPreset = (0, 0, 0);
                            
                        foreach (var preset in smallPresets)
                        {
                            double aspectRatioDiff = Math.Abs(currentAspectRatio - preset.Value.aspectRatio);
                            if (aspectRatioDiff < minAspectRatioDiff)
                            {
                                minAspectRatioDiff = aspectRatioDiff;
                                bestPresetName = preset.Key;
                                bestPreset = preset.Value;
                            }
                        }
                            
                        // 最も近いプリセットの長辺を固定し、短辺をアスペクト比から計算
                        int newWidth, newHeight;
                        int longSide = Math.Max(bestPreset.width, bestPreset.height);
                            
                        if (currentAspectRatio >= 1.0) // 横長または正方形
                        {
                            newWidth = longSide;
                            newHeight = (int)Math.Round(longSide / currentAspectRatio);
                        }
                        else // 縦長
                        {
                            newHeight = longSide;
                            newWidth = (int)Math.Round(longSide * currentAspectRatio);
                        }
                            
                        // 新しい解像度を設定
                        innerTab.Width = newWidth;
                        innerTab.Height = newHeight;
                            
                        // Hires.fixを無効化
                        innerTab.EnableHiresFix = false;
                            
                        Debug.WriteLine($"S解像度化 (プリセット「{bestPresetName}」, アスペクト比{currentAspectRatio:F3}): 内側タブ「{innerTab.Title}」 -> {newWidth}×{newHeight}");
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"S解像度化エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        skippedCount++;
                    }
                }
                
                // タブデータを保存
                vm.SaveToFile(GetDataFilePath());
                string message = $"S解像度化が完了しました。\n\n処理済み: {processedCount}個\nスキップ: {skippedCount}個";
                
                Debug.WriteLine($"S解像度化完了: 親タブ「{currentOuterTab.Title}」 - 処理済み{processedCount}個、スキップ{skippedCount}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"S解像度化 致命的エラー: {ex.Message}");
                MessageBox.Show($"An error occurred during S resolutionize processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RandomSeedButton_Click(object sender, RoutedEventArgs e)
        {
            var outerTab = MainTabControl.SelectedItem as TabItemViewModel;
            if (outerTab?.InnerTabs == null) return;

            int seedChangedCount = 0;

            foreach (var innerTab in outerTab.InnerTabs)
            {
                innerTab.Seed = -1; // ランダムSeedに設定
                seedChangedCount++;
                Debug.WriteLine($"ランダムSeed設定: {innerTab.Title} (Seed: -1)");
            }

            Debug.WriteLine($"ランダムSeed設定完了 - 変更されたタブ数: {seedChangedCount}");
        }

        #region 検索機能

        /// <summary>
        /// 検索パネルを表示してフォーカスを設定
        /// </summary>
        private void ShowSearchPanel()
        {
        }

        /// <summary>
        /// 検索パネルを非表示にする
        /// </summary>
        private void HideSearchPanel()
        {
        }

        /// <summary>
        /// 検索テキストボックスのテキスト変更処理
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        /// <summary>
        /// 検索テキストボックスでのキー押下処理
        /// </summary>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
        }

        /// <summary>
        /// 検索ボックスを閉じる
        /// </summary>
        private void CloseSearchButton_Click(object sender, RoutedEventArgs e)
        {
        }

        /// <summary>
        /// 指定されたRichTextBoxのハイライトをクリア - より確実な方法
        /// </summary>
        private void ClearHighlightsInRichTextBox(RichTextBox richTextBox)
        {
            try
            {
                if (richTextBox?.Document == null) return;

                var document = richTextBox.Document;
                var fullRange = new TextRange(document.ContentStart, document.ContentEnd);
                
                Debug.WriteLine($"ハイライトクリア開始: {fullRange.Text.Length}文字");
                
                // 背景色と前景色を明示的にデフォルト値に設定
                fullRange.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, null);
                
                // さらに確実にするため、Brushes.Transparentとデフォルト前景色も試す
                fullRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
                
                // RichTextBoxのデフォルト前景色を取得して設定
                var defaultForeground = richTextBox.Foreground;
                fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, defaultForeground);
                
                Debug.WriteLine($"ハイライトクリア完了: 背景=Transparent, 前景=デフォルト");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RichTextBoxハイライトクリアエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 全てのハイライトをクリア - より確実な方法
        /// </summary>
        private void ClearAllHighlights()
        {
            try
            {
                Debug.WriteLine("=== ハイライト全クリア開始 ===");
                
                var (promptTextBox, negativeTextBox) = GetCurrentTabTextBoxes();
                
                Debug.WriteLine($"ハイライトクリア対象 - Prompt: {promptTextBox != null}, Negative: {negativeTextBox != null}");
                
                if (promptTextBox != null)
                {
                    Debug.WriteLine("PromptTextBoxのハイライトをクリア中...");
                    ClearHighlightsInRichTextBox(promptTextBox);
                }

                if (negativeTextBox != null)
                {
                    Debug.WriteLine("NegativePromptTextBoxのハイライトをクリア中...");
                    ClearHighlightsInRichTextBox(negativeTextBox);
                }
                
                Debug.WriteLine("=== ハイライト全クリア完了 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ハイライトクリアエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索を実行 - ハイライトクリアを強化
        /// </summary>
        private void PerformSearch(string searchText)
        {
            try
            {
                Debug.WriteLine($"=== 検索実行開始: '{searchText}' ===");
                
                // 検索前に必ずハイライトをクリア
                Debug.WriteLine("検索前のハイライトクリア実行...");
                ClearAllHighlights();
                
                // 少し待機してクリアが確実に完了するようにする
                System.Threading.Thread.Sleep(10);

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    Debug.WriteLine("検索テキストが空、検索終了");
                    return;
                }

                // 現在のタブの両方のTextBoxを取得してハイライト
                var (promptTextBox, negativeTextBox) = GetCurrentTabTextBoxes();
                
                if (promptTextBox != null)
                {
                    Debug.WriteLine("PromptTextBoxでハイライト開始");
                    HighlightTextInRichTextBox(promptTextBox, searchText);
                }
                else
                {
                    Debug.WriteLine("PromptTextBoxが見つからない");
                }

                if (negativeTextBox != null)
                {
                    Debug.WriteLine("NegativePromptTextBoxでハイライト開始");
                    HighlightTextInRichTextBox(negativeTextBox, searchText);
                }
                else
                {
                    Debug.WriteLine("NegativePromptTextBoxが見つからない");
                }
                
                Debug.WriteLine("=== 検索実行完了 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"検索エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブのTextBoxを取得
        /// </summary>
        private (RichTextBox? promptTextBox, RichTextBox? negativeTextBox) GetCurrentTabTextBoxes()
        {
            try
            {
                Debug.WriteLine("TextBox検索開始");
                
                // PromptTextBoxとNegativePromptTextBoxをそれぞれ直接名前で検索
                var promptTextBox = FindVisualChild<RichTextBox>(MainTabControl, rtb => 
                {
                    var parent = FindAncestor<AutoCompleteTextBox>(rtb);
                    bool isPrompt = parent?.Name == "PromptTextBox";
                    if (isPrompt) Debug.WriteLine($"PromptTextBox発見: {parent?.Name}");
                    return isPrompt;
                });

                var negativeTextBox = FindVisualChild<RichTextBox>(MainTabControl, rtb => 
                {
                    var parent = FindAncestor<AutoCompleteTextBox>(rtb);
                    bool isNegative = parent?.Name == "NegativePromptTextBox";
                    if (isNegative) Debug.WriteLine($"NegativePromptTextBox発見: {parent?.Name}");
                    return isNegative;
                });

                Debug.WriteLine($"TextBox検索結果 - Prompt: {promptTextBox != null}, Negative: {negativeTextBox != null}");
                
                return (promptTextBox, negativeTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextBox取得エラー: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// RichTextBox内の指定されたテキストをハイライト - 改行文字を考慮した正確な方法
        /// </summary>
        private void HighlightTextInRichTextBox(RichTextBox richTextBox, string searchText)
        {
            try
            {
                if (richTextBox?.Document == null || string.IsNullOrEmpty(searchText)) 
                    return;

                var document = richTextBox.Document;
                Debug.WriteLine($"=== ハイライト処理開始: '{searchText}' ===");

                // まず全体をクリア
                var fullRange = new TextRange(document.ContentStart, document.ContentEnd);
                fullRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);

                // 全体テキストを取得
                string fullText = fullRange.Text;
                Debug.WriteLine($"全体テキスト: '{fullText.Replace("\r", "\\r").Replace("\n", "\\n")}'");
                Debug.WriteLine($"全体テキスト長: {fullText.Length}文字");

                // 検索とハイライト
                int index = 0;
                int matchCount = 0;
                
                while ((index = fullText.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    Debug.WriteLine($"マッチ {matchCount + 1}: インデックス {index} で発見");
                    
                    // 改行文字によるオフセット調整
                    int adjustedStartIndex = AdjustIndexForLineBreaks(fullText, index);
                    int adjustedEndIndex = AdjustIndexForLineBreaks(fullText, index + searchText.Length);
                    
                    Debug.WriteLine($"調整後インデックス: 開始 {index} → {adjustedStartIndex}, 終了 {index + searchText.Length} → {adjustedEndIndex}");
                    
                    // 開始位置と終了位置のTextPointerを取得
                    TextPointer? start = GetTextPositionAtOffset(document.ContentStart, adjustedStartIndex);
                    TextPointer? end = GetTextPositionAtOffset(document.ContentStart, adjustedEndIndex);
                    
                    if (start != null && end != null)
                    {
                        TextRange matchRange = new TextRange(start, end);
                        string actualText = matchRange.Text;
                        
                        Debug.WriteLine($"ハイライト範囲: '{actualText}' (期待: '{searchText}')");
                        
                        // ハイライト適用
                        matchRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
                        matchCount++;
                    }
                    else
                    {
                        Debug.WriteLine($"TextPointer取得失敗: インデックス {adjustedStartIndex}");
                    }
                    
                    index += searchText.Length; // 次の検索開始位置
                }

                Debug.WriteLine($"=== ハイライト処理完了: {matchCount}件 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ハイライト処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 改行文字によるインデックスのずれを調整
        /// </summary>
        private int AdjustIndexForLineBreaks(string fullText, int originalIndex)
        {
            try
            {
                // originalIndexまでの範囲で改行文字（\r または \n）の数をカウント
                int lineBreakCount = 0;
                
                for (int i = 0; i < originalIndex && i < fullText.Length; i++)
                {
                    if (fullText[i] == '\r' || fullText[i] == '\n')
                    {
                        lineBreakCount++;
                    }
                }
                int adjustedIndex = originalIndex - lineBreakCount;
                
                Debug.WriteLine($"改行調整: 元インデックス {originalIndex}, 改行文字数 {lineBreakCount}, 調整後 {adjustedIndex}");
                
                return adjustedIndex;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"改行調整エラー: {ex.Message}");
                return originalIndex;
            }
        }

        /// <summary>
        /// 指定されたオフセット位置のTextPointerを取得 - 参考コードベースの効率的な方法
        /// </summary>
        private TextPointer? GetTextPositionAtOffset(TextPointer start, int offset)
        {
            try
            {
                Debug.WriteLine($"TextPointer取得: オフセット {offset}");
                
                if (offset <= 0) return start;
                
                TextPointer current = start;
                int remaining = offset;
                
                while (current != null && remaining > 0)
                {
                    TextPointerContext context = current.GetPointerContext(LogicalDirection.Forward);
                    
                    if (context == TextPointerContext.Text)
                    {
                        // テキストラン内の処理
                        string textRun = current.GetTextInRun(LogicalDirection.Forward);
                        Debug.WriteLine($"テキストラン: '{textRun}' (長さ: {textRun.Length}, 残り: {remaining})");
                        
                        if (textRun.Length >= remaining)
                        {
                            // このテキストラン内で目標位置に到達可能
                            var result = current.GetPositionAtOffset(remaining);
                            Debug.WriteLine($"テキストラン内移動: {remaining}文字移動完了");
                            return result;
                        }
                        else
                        {
                            // このテキストラン全体を通過
                            remaining -= textRun.Length;
                            current = current.GetPositionAtOffset(textRun.Length);
                            Debug.WriteLine($"テキストラン通過: {textRun.Length}文字, 残り: {remaining}");
                        }
                    }
                    else
                    {
                        // テキスト以外の要素（改行、要素境界など）
                        Debug.WriteLine($"非テキスト要素: {context}");
                        current = current.GetNextContextPosition(LogicalDirection.Forward);
                    }
                }
                
                Debug.WriteLine($"TextPointer取得完了: 残り移動距離 {remaining}");
                return current;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextPointer取得エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 検索テキストボックスがフォーカスを取得した時の処理
        /// </summary>
        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
        }

        /// <summary>
        /// 検索テキストボックスがフォーカスを失った時の処理
        /// </summary>
        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
        }

        #endregion
        
        // メモリ監視とプロセス再起動関連のメソッド
        #region Memory Monitoring
        
        private void InitializeMemoryMonitor()
        {
            _memoryMonitorTimer = new DispatcherTimer();
            _memoryMonitorTimer.Interval = TimeSpan.FromSeconds(5); // 5秒ごとにチェック
            _memoryMonitorTimer.Tick += MemoryMonitorTimer_Tick;
            _memoryMonitorTimer.Start();
        }

        private void MemoryMonitorTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                long memoryBytes = process.WorkingSet64;
                double memoryMB = memoryBytes / (1024.0 * 1024.0);
                
                // メモリ使用量ラベルを更新
                MemoryUsageLabel.Text = $"Memory: {memoryMB:F0} MB";
                
                // 1GB以上の場合は警告パネルを表示
                if (memoryBytes >= MEMORY_THRESHOLD_BYTES)
                {
                    MemoryMonitorPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    MemoryMonitorPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"メモリ監視エラー: {ex.Message}");
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RestartApplication();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"再起動に失敗しました: {ex.Message}\n\n" +
                    "手動でアプリケーションを再起動することをお勧めします。",
                    "再起動エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Debug.WriteLine($"再起動エラー: {ex}");
            }
        }

        private void RestartApplication()
        {
            try
            {
                // 再起動前にすべてのデータを保存
                SaveAllDataBeforeRestart();
                
                // 現在の実行ファイルのパスを取得
                string? executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                
                if (string.IsNullOrEmpty(executablePath))
                {
                    throw new InvalidOperationException("実行ファイルのパスを取得できませんでした。");
                }

                // プロセス開始情報を設定
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)
                };

                // 新しいプロセスを開始
                Process.Start(startInfo);

                // 現在のアプリケーションを終了
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"アプリケーションの再起動に失敗しました: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 再起動前にすべてのデータを安全に保存する
        /// </summary>
        private void SaveAllDataBeforeRestart()
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                
                // 進行状況をユーザーに表示
                Debug.WriteLine("再起動前データ保存開始...");
                
                // 1. ウィンドウ設定を保存
                SaveWindowSettings();
                Debug.WriteLine("ウィンドウ設定保存完了");
                
                // 2. タブ選択状態を保存
                SaveTabSelectionSettings();
                Debug.WriteLine("タブ選択状態保存完了");
                
                // 4. タブデータを保存（最も重要）
                vm.SaveToFile(GetDataFilePath());
                Debug.WriteLine("タブデータ保存完了");
                
                // 5. Undo履歴を保存
                vm.SaveUndoHistory();
                Debug.WriteLine($"Undo履歴保存完了: {vm.GetUndoHistoryCount()}件");
                
                // 6. 少し待機してファイルI/Oが完了することを確実にする
                System.Threading.Thread.Sleep(100);
                
                Debug.WriteLine("すべてのデータ保存が完了しました。再起動を開始します...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"データ保存エラー: {ex.Message}");
                // エラーが発生してもユーザーに選択肢を与える
                var result = MessageBox.Show(
                    $"データ保存中にエラーが発生しました：{ex.Message}\n\n" +
                    "それでも再起動を続行しますか？\n" +
                    "「はい」: データが失われる可能性がありますが再起動します\n" +
                    "「いいえ」: 再起動をキャンセルします",
                    "データ保存エラー",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.No)
                {
                    throw new OperationCanceledException("ユーザーによって再起動がキャンセルされました。");
                }
            }
        }
        
        #endregion

        /// <summary>
        /// M解像度化ボタンがクリックされた時の処理
        /// 現在の親タブの全ての子タブの解像度をMサイズプリセットに変換し、Hires.fixを無効化
        /// </summary>
        private void MediumResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = (MainViewModel)DataContext;
                var currentOuterTab = vm.SelectedTab;
                
                if (currentOuterTab == null)
                {
                    MessageBox.Show("No parent tab selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (currentOuterTab.InnerTabs.Count == 0)
                {
                    MessageBox.Show("No child tabs to process.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                int processedCount = 0;
                int skippedCount = 0;
                
                Debug.WriteLine($"M解像度化開始: 親タブ「{currentOuterTab.Title}」の{currentOuterTab.InnerTabs.Count}個の子タブで実行");
                
                // アプリ設定からプリセット値を動的に取得
                var allPresets = AppSettings.Instance.GetResolutionPresets();
                
                // Mサイズのプリセット値のみを抽出してアスペクト比を計算
                var mediumPresets = new Dictionary<string, (int width, int height, double aspectRatio)>();
                
                if (allPresets.ContainsKey("縦長M"))
                {
                    (int width, int height) = allPresets["縦長M"];
                    mediumPresets["縦長M"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("横長M"))
                {
                    (int width, int height) = allPresets["横長M"];
                    mediumPresets["横長M"] = (width, height, (double)width / height);
                }
                
                if (allPresets.ContainsKey("正方M"))
                {
                    (int width, int height) = allPresets["正方M"];
                    mediumPresets["正方M"] = (width, height, (double)width / height);
                }
                
                // Mプリセットが見つからない場合はエラー
                if (mediumPresets.Count == 0)
                {
                    MessageBox.Show("No Medium presets found in settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                foreach (var innerTab in currentOuterTab.InnerTabs)
                {
                    try
                    {
                        // 現在のタブのアスペクト比を計算
                        double currentAspectRatio = (double)innerTab.Width / innerTab.Height;
                            
                        // 最も近いプリセットを見つける
                        string bestPresetName = "";
                        double minAspectRatioDiff = double.MaxValue;
                        (int width, int height, double aspectRatio) bestPreset = (0, 0, 0);
                            
                        foreach (var preset in mediumPresets)
                        {
                            double aspectRatioDiff = Math.Abs(currentAspectRatio - preset.Value.aspectRatio);
                            if (aspectRatioDiff < minAspectRatioDiff)
                            {
                                minAspectRatioDiff = aspectRatioDiff;
                                bestPresetName = preset.Key;
                                bestPreset = preset.Value;
                            }
                        }
                            
                        // 最も近いプリセットの長辺を固定し、短辺をアスペクト比から計算
                        int newWidth, newHeight;
                        int longSide = Math.Max(bestPreset.width, bestPreset.height);
                            
                        if (currentAspectRatio >= 1.0) // 横長または正方形
                        {
                            newWidth = longSide;
                            newHeight = (int)Math.Round(longSide / currentAspectRatio);
                        }
                        else // 縦長
                        {
                            newHeight = longSide;
                            newWidth = (int)Math.Round(longSide * currentAspectRatio);
                        }
                            
                        // 新しい解像度を設定
                        innerTab.Width = newWidth;
                        innerTab.Height = newHeight;
                            
                        // Hires.fixを無効化
                        innerTab.EnableHiresFix = false;
                            
                        Debug.WriteLine($"M解像度化 (プリセット「{bestPresetName}」, アスペクト比{currentAspectRatio:F3}): 内側タブ「{innerTab.Title}」 -> {newWidth}×{newHeight}");
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"M解像度化エラー (内側タブ「{innerTab.Title}」): {ex.Message}");
                        skippedCount++;
                    }
                }
                
                // タブデータを保存
                vm.SaveToFile(GetDataFilePath());
                string message = $"M解像度化が完了しました。\n\n処理済み: {processedCount}個\nスキップ: {skippedCount}個";
                
                Debug.WriteLine($"M解像度化完了: 親タブ「{currentOuterTab.Title}」 - 処理済み{processedCount}個、スキップ{skippedCount}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"M解像度化 致命的エラー: {ex.Message}");
                MessageBox.Show($"An error occurred during M resolutionize processing:\n{ex.Message}", 
                               "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Add Resolution ボタンがクリックされた時の処理
        /// Random Resolution設定に新しい解像度を追加する
        /// </summary>
        private void AddResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            
            if (vm.RandomResolutionNewWidth > 0 && vm.RandomResolutionNewHeight > 0)
            {
                var newResolution = new ResolutionItem(vm.RandomResolutionNewWidth, vm.RandomResolutionNewHeight);
                
                // 既存の解像度と重複していないかチェック
                bool isDuplicate = GlobalRandomResolutionSettings.CurrentResolutions.Any(r => r.Width == newResolution.Width && r.Height == newResolution.Height);
                
                if (!isDuplicate)
                {
                    GlobalRandomResolutionSettings.CurrentResolutions.Add(newResolution);
                    
                    // テキストボックスを更新
                    vm.RefreshRandomResolutionText();
                    
                    // 設定を保存
                    SaveRandomResolutionSettings(GlobalRandomResolutionSettings);
                    
                    
                    // 入力フィールドをクリア
                    vm.RandomResolutionNewWidth = 0;
                    vm.RandomResolutionNewHeight = 0;
                    
                    Debug.WriteLine($"新しい解像度を追加: {newResolution.Width}×{newResolution.Height}");
                }
                else
                {
                    MessageBox.Show("This resolution already exists.", "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Please enter valid width and height.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 現在のタブのプロンプトをフォーマット
        /// </summary>
        private void FormatCurrentTabPrompts()
        {
            try
            {
                var (promptTextBox, negativeTextBox) = GetCurrentTabTextBoxParents();
                
                // プロンプトテキストボックスにフォーマットを適用
                promptTextBox?.FormatText();
                
                // ネガティブプロンプトテキストボックスにフォーマットを適用
                negativeTextBox?.FormatText();
                
                Debug.WriteLine("テキストボックスへのフォーマット適用完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"フォーマット適用エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 現在のタブのプロンプトテキストボックスの親コントロールを取得
        /// </summary>
        private (AutoCompleteTextBox? promptTextBox, AutoCompleteTextBox? negativeTextBox) GetCurrentTabTextBoxParents()
        {
            try
            {
                Debug.WriteLine("TextBox親コントロール検索開始");
                
                // PromptTextBoxとNegativePromptTextBoxをそれぞれ直接名前で検索
                var promptTextBox = FindVisualChild<AutoCompleteTextBox>(MainTabControl, box => 
                {
                    bool isPrompt = box.Name == "PromptTextBox";
                    if (isPrompt) Debug.WriteLine($"PromptTextBox発見: {box.Name}");
                    return isPrompt;
                });

                var negativeTextBox = FindVisualChild<AutoCompleteTextBox>(MainTabControl, box => 
                {
                    bool isNegative = box.Name == "NegativePromptTextBox";
                    if (isNegative) Debug.WriteLine($"NegativePromptTextBox発見: {box.Name}");
                    return isNegative;
                });

                Debug.WriteLine($"TextBox親コントロール検索結果 - Prompt: {promptTextBox != null}, Negative: {negativeTextBox != null}");
                
                return (promptTextBox, negativeTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextBox親コントロール取得エラー: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// サンプラーリストの初期化
        /// </summary>
        private async Task InitializeSamplersAsync()
        {
            try
            {
                Debug.WriteLine("サンプラーリストの初期化を開始");
                
                // SamplerManagerからサンプラーリストを取得（APIから初回取得）
                var samplers = await SamplerManager.Instance.GetSamplerNamesAsync();
                
                Debug.WriteLine($"サンプラー初期化完了: {samplers.Count}個のサンプラーを取得");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"サンプラー初期化エラー: {ex.Message}");
                // エラーが発生してもアプリケーションの起動を妨げない
            }
        }

        /// <summary>
        /// スケジューラーリストの初期化
        /// </summary>
        private async Task InitializeSchedulersAsync()
        {
            try
            {
                Debug.WriteLine("スケジューラーリストの初期化を開始");
                
                // SchedulerManagerからスケジューラーリストを取得（APIから初回取得）
                var schedulers = await SchedulerManager.Instance.GetSchedulerLabelsAsync();
                
                Debug.WriteLine($"スケジューラー初期化完了: {schedulers.Count}個のスケジューラーを取得");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スケジューラー初期化エラー: {ex.Message}");
                // エラーが発生してもアプリケーションの起動を妨げない
            }
        }

        /// <summary>
        /// Checkpointリストの初期化
        /// </summary>
        private async Task InitializeCheckpointsAsync()
        {
            try
            {
                Debug.WriteLine("Checkpointリストの初期化を開始");
                
                // MainViewModelのCheckpoint初期化を呼び出し
                var vm = (MainViewModel)DataContext;
                await vm.InitializeCheckpointsAsync();
                
                Debug.WriteLine("Checkpoint初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Checkpoint初期化エラー: {ex.Message}");
                // エラーが発生してもアプリケーションの起動を妨げない
            }
        }

        /// <summary>
        /// 設定ボタンがクリックされた時の処理
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 設定画面を開く前の画像グリッド列数を記録
                int previousGridColumns = AppSettings.Instance.DefaultImageGridColumns;
                
                var settingsWindow = new SettingsWindow
                {
                    Owner = this
                };

                bool? result = settingsWindow.ShowDialog();
                
                if (result == true)
                {
                    // 設定が保存された場合の処理
                    Debug.WriteLine("設定が保存されました");
                    
                    // 保存後の画像グリッド列数と比較
                    int newGridColumns = AppSettings.Instance.DefaultImageGridColumns;
                    if (previousGridColumns != newGridColumns)
                    {
                        // 実際に値が変更された場合のみ表示に反映
                        ImageGridColumns = newGridColumns;
                        Debug.WriteLine($"画像グリッド列数を更新しました: {previousGridColumns}列 → {newGridColumns}列");
                    }
                    else
                    {
                        Debug.WriteLine($"画像グリッド列数は変更されていません: {newGridColumns}列（一時的な変更を保持）");
                    }
                    
                    // 必要に応じてTagDataManagerを再初期化
                    // （BASE_DIRECTORYやLoRAディレクトリが変更された場合）
                    Task.Run(async () =>
                    {
                        try
                        {
                            await TagDataManager.Instance.LoadAllDataAsync();
                            Debug.WriteLine("TagDataManagerを再初期化しました");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"TagDataManager再初期化エラー: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"設定ダイアログ表示エラー: {ex.Message}");
                MessageBox.Show($"Failed to display settings dialog: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 画像全画面表示ボタンがクリックされた時の処理
        /// </summary>
        private void ImageFullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleImagePanelFullscreen();
        }

        /// <summary>
        /// ショートカットボタンがクリックされた時の処理
        /// </summary>
        private void ShortcutsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsShortcutsOverlayVisible = true;
                Debug.WriteLine("ショートカットオーバーレイを表示しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ショートカットオーバーレイ表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ショートカットオーバーレイを閉じる
        /// </summary>
        private void CloseShortcutsOverlay()
        {
            IsShortcutsOverlayVisible = false;
            Debug.WriteLine("ショートカットオーバーレイを閉じました");
        }
        
        /// <summary>
        /// ショートカットオーバーレイの閉じるボタンがクリックされた時の処理
        /// </summary>
        private void CloseShortcutsButton_Click(object sender, RoutedEventArgs e)
        {
            CloseShortcutsOverlay();
        }
        
        /// <summary>
        /// ショートカットオーバーレイの背景がクリックされた時の処理
        /// </summary>
        private void ShortcutsOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == sender)
            {
                CloseShortcutsOverlay();
            }
        }

        /// <summary>
        /// Upscalerリストの初期化
        /// </summary>
        private async Task InitializeUpscalersAsync()
        {
            try
            {
                Debug.WriteLine("Upscalerリストの初期化を開始");
                
                // UpscalerManagerからUpscalerリストを取得（APIから初回取得）
                var upscalers = await UpscalerManager.Instance.GetUpscalerNamesAsync();
                
                Debug.WriteLine($"Upscaler初期化完了: {upscalers.Count}個のUpscalerを取得");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Upscaler初期化エラー: {ex.Message}");
                // エラーが発生してもアプリケーションの起動を妨げない
            }
        }
        
        private void InitializeVersionCheck()
        {
            _versionCheckManager = new VersionCheckManager(UpdateVersionCheckUI);
        }
        
        private void UpdateVersionCheckUI(bool hasNewVersion)
        {
            Dispatcher.Invoke(() =>
            {
                if (hasNewVersion)
                {
                    VersionCheckPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    VersionCheckPanel.Visibility = Visibility.Collapsed;
                }
            });
        }
        
        private void NewVersionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // GitHubページをデフォルトブラウザで開く
                var url = "https://github.com/crstp/sd-yuzu/";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                
                Debug.WriteLine($"GitHubページを開きました: {url}");
                
                // ボタンを非表示にする
                VersionCheckPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ブラウザ起動エラー: {ex.Message}");
                // エラーが発生してもアプリケーションは継続
                // エラーの場合でもボタンを非表示にする
                VersionCheckPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        private void CloseVersionButton_Click(object sender, RoutedEventArgs e)
        {
            // バージョンチェックパネルを非表示にする
            VersionCheckPanel.Visibility = Visibility.Collapsed;
            Debug.WriteLine("バージョンチェック通知を閉じました");
        }

        /// <summary>
        /// 画像パネルの全画面表示を切り替える
        /// </summary>
        private void ToggleImagePanelFullscreen()
        {
            IsImagePanelFullscreen = !IsImagePanelFullscreen;
            Debug.WriteLine($"画像パネル全画面表示: {IsImagePanelFullscreen}");
            
            if (IsImagePanelFullscreen)
            {
                // 全画面表示を開く時、オーバーレイにフォーカスを設定
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var overlay = FindVisualChild<Grid>(this, g => g.Background?.ToString() == "#FFF0F0F0" && Panel.GetZIndex(g) == 999);
                    if (overlay != null)
                    {
                        overlay.Focus();
                        Debug.WriteLine("画像パネル全画面表示オーバーレイにフォーカスを設定");
                    }
                }), DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 左側パネルの表示/非表示を切り替える
        /// </summary>
        private void ToggleLeftPanel()
        {
            IsLeftPanelVisible = !IsLeftPanelVisible;
            
            // 少し遅延させてUIが更新されてから実行
            Dispatcher.BeginInvoke(new Action(() => 
            {
                UpdateLeftPanelLayout();
            }), DispatcherPriority.Render);
        }

        /// <summary>
        /// パネル折り畳みボタンがクリックされた時の処理
        /// </summary>
        private void ToggleLeftPanelButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftPanel();
            Debug.WriteLine("パネル折り畳みボタンクリック: 左側パネルの表示/非表示を切り替え");
        }

        /// <summary>
        /// 左側パネルのレイアウトを更新する
        /// </summary>
        private void UpdateLeftPanelLayout()
        {
            try
            {
                // すべてのグリッドを見つけて更新する
                var contentGrids = new List<Grid>();
                FindAllContentGrids(this, contentGrids);
                
                foreach (var contentGrid in contentGrids)
                {
                    if (contentGrid.ColumnDefinitions.Count == 3)
                    {
                        if (IsLeftPanelVisible)
                        {
                            // パネル表示時
                            contentGrid.ColumnDefinitions[0].Width = new GridLength(350, GridUnitType.Pixel);
                            contentGrid.ColumnDefinitions[0].MinWidth = 250;
                            contentGrid.ColumnDefinitions[1].Width = new GridLength(5, GridUnitType.Pixel);
                            
                            // 子要素のVisibilityも更新
                            UpdateChildVisibility(contentGrid, Visibility.Visible);
                        }
                        else
                        {
                            // パネル非表示時
                            contentGrid.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Pixel);
                            contentGrid.ColumnDefinitions[0].MinWidth = 0;
                            contentGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
                            
                            // 子要素のVisibilityも更新
                            UpdateChildVisibility(contentGrid, Visibility.Collapsed);
                        }
                        
                        // 強制的にレイアウトを更新
                        contentGrid.InvalidateArrange();
                        contentGrid.InvalidateMeasure();
                        contentGrid.UpdateLayout();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"レイアウト更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// グリッドの子要素のVisibilityを更新する
        /// </summary>
        private void UpdateChildVisibility(Grid grid, Visibility visibility)
        {
            try
            {
                int childCount = VisualTreeHelper.GetChildrenCount(grid);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(grid, i);
                    if (child is FrameworkElement element)
                    {
                        int column = Grid.GetColumn(element);
                        // 左側パネル（列0）とスプリッター（列1）の要素のみ制御
                        if (column == 0 || column == 1)
                        {
                            element.Visibility = visibility;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"子要素のVisibility更新エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// すべてのコンテンツグリッドを再帰的に見つける
        /// </summary>
        private void FindAllContentGrids(DependencyObject parent, List<Grid> grids)
        {
            try
            {
                int childCount = VisualTreeHelper.GetChildrenCount(parent);
                for (int i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is Grid grid && grid.ColumnDefinitions.Count == 3)
                    {
                        // 3列のグリッドで、ScrollViewerとGridSplitterを含むかチェック
                        bool hasScrollViewer = false;
                        bool hasGridSplitter = false;
                        
                        int gridChildCount = VisualTreeHelper.GetChildrenCount(grid);
                        
                        for (int j = 0; j < gridChildCount; j++)
                        {
                            var gridChild = VisualTreeHelper.GetChild(grid, j);
                            int column = Grid.GetColumn(gridChild as FrameworkElement);
                            
                            if (gridChild is ScrollViewer && column == 0) hasScrollViewer = true;
                            if (gridChild is GridSplitter && column == 1) hasGridSplitter = true;
                        }
                        
                        // ScrollViewerとGridSplitterがあれば対象とする
                        if (hasScrollViewer && hasGridSplitter)
                        {
                            grids.Add(grid);
                        }
                    }
                    
                    FindAllContentGrids(child, grids);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FindAllContentGridsエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 画像パネル全画面表示の閉じるボタンがクリックされた時の処理
        /// </summary>
        private void CloseImagePanelFullscreen_Click(object sender, RoutedEventArgs e)
        {
            IsImagePanelFullscreen = false;
            Debug.WriteLine("画像パネル全画面表示を閉じました（ボタンクリック）");
        }

        /// <summary>
        /// 画像パネル全画面表示の背景がクリックされた時の処理
        /// </summary>
        private void ImagePanelFullscreen_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 背景がクリックされた場合のみ全画面表示を閉じる
            if (e.Source == sender)
            {
                IsImagePanelFullscreen = false;
                Debug.WriteLine("画像パネル全画面表示を閉じました（背景クリック）");
                e.Handled = true;
            }
        }

        /// <summary>
        /// 画像パネル全画面表示中のキーイベント処理
        /// </summary>
        private void ImagePanelFullscreen_KeyDown(object sender, KeyEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            
            // Ctrl+Tabが押された場合、次の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                vm.MoveToNextInnerTab();
                e.Handled = true;
                Debug.WriteLine("画像パネル全画面表示: Ctrl+Tab - 次の内側タブに移動");
                return;
            }
            
            // Ctrl+Shift+Tabが押された場合、前の内側タブに移動（cyclic）
            if (e.Key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                vm.MoveToPreviousInnerTab();
                e.Handled = true;
                Debug.WriteLine("画像パネル全画面表示: Ctrl+Shift+Tab - 前の内側タブに移動");
                return;
            }
            
            // 左矢印キーで前のタブに移動
            if (e.Key == Key.Left)
            {
                if (vm.MoveToPreviousInnerTabCommand?.CanExecute(null) == true)
                {
                    vm.MoveToPreviousInnerTabCommand.Execute(null);
                    Debug.WriteLine("画像パネル全画面表示: 前のタブに移動");
                }
                e.Handled = true;
                return;
            }
            
            // 右矢印キーで次のタブに移動
            if (e.Key == Key.Right)
            {
                if (vm.MoveToNextInnerTabCommand?.CanExecute(null) == true)
                {
                    vm.MoveToNextInnerTabCommand.Execute(null);
                    Debug.WriteLine("画像パネル全画面表示: 次のタブに移動");
                }
                e.Handled = true;
                return;
            }
        }
    }

    // RelayCommand 実装
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
        
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}

