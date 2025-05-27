using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;

namespace SD.Yuzu
{
    public class TabItemViewModel : INotifyPropertyChanged, IDisposable
    {
        private ObservableCollection<string> _imagePaths = new();
        private string _textBoxValue = "";
        private string _negativePromptValue = "";
        private bool _isButtonEnabled = true;
        private static int _tabCount = 0;
        private string _title = "タブ";
        private ObservableCollection<TabItemViewModel> _innerTabs = new();
        private TabItemViewModel? _selectedInnerTab;
        private int _innerTabCount = 0;
        
        // 復元中フラグ
        private bool _isRestoring = false;

        // GUIDプロパティを追加（一意識別子）
        private Guid _guid = Guid.NewGuid();
        public Guid Guid
        {
            get => _guid;
            set { _guid = value; OnPropertyChanged(nameof(Guid)); }
        }

        public ObservableCollection<string> ImagePaths
        {
            get => _imagePaths;
            set { _imagePaths = value; OnPropertyChanged(nameof(ImagePaths)); }
        }

        public string TextBoxValue
        {
            get => _textBoxValue;
            set { _textBoxValue = value; OnPropertyChanged(nameof(TextBoxValue)); }
        }

        public string NegativePromptValue
        {
            get => _negativePromptValue;
            set { _negativePromptValue = value; OnPropertyChanged(nameof(NegativePromptValue)); }
        }

        public bool IsButtonEnabled
        {
            get => _isButtonEnabled;
            set { _isButtonEnabled = value; OnPropertyChanged(nameof(IsButtonEnabled)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public ObservableCollection<TabItemViewModel> InnerTabs
        {
            get => _innerTabs;
            set { _innerTabs = value; OnPropertyChanged(nameof(InnerTabs)); }
        }

        public TabItemViewModel? SelectedInnerTab
        {
            get => _selectedInnerTab;
            set 
            { 
                _selectedInnerTab = value; 
                OnPropertyChanged(nameof(SelectedInnerTab));
                
                // タブ切り替え時にテキストボックスのUndo履歴をクリア
                ClearTextBoxUndoHistory();
            }
        }

        public ICommand RandomizeSeedCommand { get; }
        public ICommand SetDimensionsCommand { get; }
        public ICommand SwapDimensionsCommand { get; }
        public ICommand BulkReplaceCommand { get; }
        public ICommand BulkStepsChangeCommand { get; }

        // Sampling methods - SamplerManagerから動的に取得
        private readonly ObservableCollection<string> _samplingMethods = new();
        
        public ObservableCollection<string> SamplingMethods => _samplingMethods;

        // Schedule types - SchedulerManagerから動的に取得
        private readonly ObservableCollection<string> _scheduleTypes = new();
        
        public ObservableCollection<string> ScheduleTypes => _scheduleTypes;

        // Upscaler types - UpscalerManagerから動的に取得
        private readonly ObservableCollection<string> _upscalerTypes = new();
        
        public ObservableCollection<string> UpscalerTypes => _upscalerTypes;

        public TabItemViewModel(string? title = null, bool isRestoring = false)
        {
            _isRestoring = isRestoring;
            
            if (title != null)
            {
                _title = title;
            }
            _tabCount++;
            
            // AppSettingsからデフォルト値を取得
            _batchSize = AppSettings.Instance.DefaultBatchSize;
            _batchCount = AppSettings.Instance.DefaultBatchCount;
            
            RandomizeSeedCommand = new RelayCommand(_ =>
            {
                Seed = -1;
            });
            SetDimensionsCommand = new RelayCommand(parameter =>
            {
                if (parameter is string preset)
                {
                    SetDimensionPreset(preset);
                }
            });
            SwapDimensionsCommand = new RelayCommand(_ =>
            {
                SwapDimensions();
            });
            BulkReplaceCommand = new RelayCommand(_ => ExecuteBulkReplace());
            BulkStepsChangeCommand = new RelayCommand(_ => ExecuteBulkStepsChange());
            
            // 復元時以外はサンプラー、スケジューラー、アップスケーラーリストを非同期で初期化
            if (!_isRestoring)
            {
                InitializeListsAsync();
            }
        }

        private int _width = 512;
        public int Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(nameof(Width)); }
        }
        
        private int _height = 512;
        public int Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(nameof(Height)); }
        }
        
        private int _steps = 20;
        public int Steps
        {
            get => _steps;
            set { _steps = value; OnPropertyChanged(nameof(Steps)); }
        }
        
        private int _batchCount = 1;
        public int BatchCount
        {
            get => _batchCount;
            set { _batchCount = value; OnPropertyChanged(nameof(BatchCount)); }
        }
        
        private int _batchSize = 4;
        public int BatchSize
        {
            get => _batchSize;
            set { _batchSize = value; OnPropertyChanged(nameof(BatchSize)); }
        }
        
        private double _cfgScale = 7.0;
        private string _cfgScaleText = "7.0";
        
        public double CfgScale
        {
            get => _cfgScale;
            set 
            { 
                _cfgScale = value; 
                OnPropertyChanged(nameof(CfgScale));
                // スライダーからの変更を文字列プロパティにも反映（適切な桁数で丸める）
                _cfgScaleText = Math.Round(value, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(CfgScaleText));
            }
        }
        
        // 文字列プロパティ（UI用、フォーマットなし）
        public string CfgScaleText
        {
            get => _cfgScaleText;
            set
            {
                // 入力された値を常に保持（前の値に戻さない）
                _cfgScaleText = value;
                OnPropertyChanged(nameof(CfgScaleText));
                
                // 有効な正の浮動小数点数の場合のみ内部値を更新
                if (!string.IsNullOrEmpty(value) && 
                    double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result) && 
                    result > 0)
                {
                    _cfgScale = result;
                    OnPropertyChanged(nameof(CfgScale));
                }
            }
        }
        
        private double _denoisingStrength = 0.7;
        private string _denoisingStrengthText = "0.7";
        
        public double DenoisingStrength
        {
            get => _denoisingStrength;
            set 
            { 
                _denoisingStrength = value; 
                OnPropertyChanged(nameof(DenoisingStrength));
                // スライダーからの変更を文字列プロパティにも反映（適切な桁数で丸める）
                _denoisingStrengthText = Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(DenoisingStrengthText));
            }
        }
        
        // 文字列プロパティ（UI用、フォーマットなし）
        public string DenoisingStrengthText
        {
            get => _denoisingStrengthText;
            set
            {
                // 入力された値を常に保持（前の値に戻さない）
                _denoisingStrengthText = value;
                OnPropertyChanged(nameof(DenoisingStrengthText));
                
                // 有効な正の浮動小数点数の場合のみ内部値を更新
                if (!string.IsNullOrEmpty(value) && 
                    double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result) && 
                    result > 0)
                {
                    _denoisingStrength = result;
                    OnPropertyChanged(nameof(DenoisingStrength));
                }
            }
        }

        // New properties based on the reference image
        private string _selectedSamplingMethod = "Euler a";
        public string SelectedSamplingMethod
        {
            get => _selectedSamplingMethod;
            set { _selectedSamplingMethod = value; OnPropertyChanged(nameof(SelectedSamplingMethod)); }
        }

        private string _selectedScheduleType = "Automatic";
        public string SelectedScheduleType
        {
            get => _selectedScheduleType;
            set { _selectedScheduleType = value; OnPropertyChanged(nameof(SelectedScheduleType)); }
        }

        private long _seed = -1;
        public long Seed
        {
            get => _seed;
            set { _seed = value; OnPropertyChanged(nameof(Seed)); }
        }

        private bool _combinatorialGeneration = false;
        public bool CombinatorialGeneration
        {
            get => _combinatorialGeneration;
            set 
            { 
                _combinatorialGeneration = value; 
                OnPropertyChanged(nameof(CombinatorialGeneration));
                
                // Combinatorial Generationが有効になったらBatch Sizeを1に設定
                if (value && BatchSize != 1)
                {
                    BatchSize = 1;
                    Debug.WriteLine("Combinatorial Generation有効: Batch Sizeを1に設定");
                }
            }
        }

        // Kohya hires.fix関連のプロパティ
        private bool _enableKohyaHiresFix = false;
        public bool EnableKohyaHiresFix
        {
            get => _enableKohyaHiresFix;
            set { _enableKohyaHiresFix = value; OnPropertyChanged(nameof(EnableKohyaHiresFix)); }
        }

        private int _kohyaBlockNumber = 3;
        public int KohyaBlockNumber
        {
            get => _kohyaBlockNumber;
            set { _kohyaBlockNumber = value; OnPropertyChanged(nameof(KohyaBlockNumber)); }
        }

        private double _kohyaDownscaleFactor = 1.75;
        private string _kohyaDownscaleFactorText = "1.75";
        
        public double KohyaDownscaleFactor
        {
            get => _kohyaDownscaleFactor;
            set 
            { 
                if (value > 0)
                {
                    _kohyaDownscaleFactor = value; 
                    OnPropertyChanged(nameof(KohyaDownscaleFactor)); 
                    // テキストプロパティは更新しない（ユーザー入力を保持）
                }
            }
        }

        // 文字列プロパティ（UI用、フォーマットなし）
        public string KohyaDownscaleFactorText
        {
            get => _kohyaDownscaleFactorText;
            set
            {
                // 入力された値を常に保持（前の値に戻さない）
                _kohyaDownscaleFactorText = value;
                OnPropertyChanged(nameof(KohyaDownscaleFactorText));
                
                // 有効な正の浮動小数点数の場合のみ内部値を更新
                if (!string.IsNullOrEmpty(value) && 
                    double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result) && 
                    result > 0)
                {
                    _kohyaDownscaleFactor = result;
                    OnPropertyChanged(nameof(KohyaDownscaleFactor));
                }
            }
        }

        private long _subseed = -1;
        public long Subseed
        {
            get => _subseed;
            set { _subseed = value; OnPropertyChanged(nameof(Subseed)); }
        }

        private bool _enableHiresFix = false;
        public bool EnableHiresFix
        {
            get => _enableHiresFix;
            set { _enableHiresFix = value; OnPropertyChanged(nameof(EnableHiresFix)); }
        }

        private string _selectedUpscaler = "Latent (nearest-exact)";
        public string SelectedUpscaler
        {
            get => _selectedUpscaler;
            set { _selectedUpscaler = value; OnPropertyChanged(nameof(SelectedUpscaler)); }
        }

        private int _hiresSteps = 0;
        public int HiresSteps
        {
            get => _hiresSteps;
            set { _hiresSteps = value; OnPropertyChanged(nameof(HiresSteps)); }
        }

        private double _hiresUpscaleBy = 2.0;
        private string _hiresUpscaleByText = "2.0";
        
        public double HiresUpscaleBy
        {
            get => _hiresUpscaleBy;
            set 
            { 
                _hiresUpscaleBy = value; 
                OnPropertyChanged(nameof(HiresUpscaleBy));
                // スライダーからの変更を文字列プロパティにも反映（適切な桁数で丸める）
                _hiresUpscaleByText = Math.Round(value, 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(HiresUpscaleByText));
            }
        }
        
        // 文字列プロパティ（UI用、フォーマットなし）
        public string HiresUpscaleByText
        {
            get => _hiresUpscaleByText;
            set
            {
                // 入力された値を常に保持（前の値に戻さない）
                _hiresUpscaleByText = value;
                OnPropertyChanged(nameof(HiresUpscaleByText));
                
                // 有効な正の浮動小数点数の場合のみ内部値を更新
                if (!string.IsNullOrEmpty(value) && 
                    double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result) && 
                    result > 0)
                {
                    _hiresUpscaleBy = result;
                    OnPropertyChanged(nameof(HiresUpscaleBy));
                }
            }
        }

        private int _hiresResizeWidth = 1024;
        public int HiresResizeWidth
        {
            get => _hiresResizeWidth;
            set { _hiresResizeWidth = value; OnPropertyChanged(nameof(HiresResizeWidth)); }
        }

        private int _hiresResizeHeight = 1024;
        public int HiresResizeHeight
        {
            get => _hiresResizeHeight;
            set { _hiresResizeHeight = value; OnPropertyChanged(nameof(HiresResizeHeight)); }
        }

        // Random Resolution機能関連のプロパティ
        private bool _enableRandomResolution = false;
        public bool EnableRandomResolution
        {
            get => _enableRandomResolution;
            set { _enableRandomResolution = value; OnPropertyChanged(nameof(EnableRandomResolution)); }
        }

        // プログレス表示用プロパティ
        private double _progressValue = 0.0;
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(nameof(ProgressValue)); }
        }

        private string _etaText = "";
        public string EtaText
        {
            get => _etaText;
            set { _etaText = value; OnPropertyChanged(nameof(EtaText)); }
        }

        private bool _isGenerating = false;
        public bool IsGenerating
        {
            get => _isGenerating;
            set { _isGenerating = value; OnPropertyChanged(nameof(IsGenerating)); }
        }

        private string _processingTime = "";
        public string ProcessingTime
        {
            get => _processingTime;
            set { _processingTime = value; OnPropertyChanged(nameof(ProcessingTime)); }
        }

        private string _generateButtonText = "Generate";
        public string GenerateButtonText
        {
            get => _generateButtonText;
            set { _generateButtonText = value; OnPropertyChanged(nameof(GenerateButtonText)); }
        }

        private bool _isEditingTitle = false;
        public bool IsEditingTitle
        {
            get => _isEditingTitle;
            set { _isEditingTitle = value; OnPropertyChanged(nameof(IsEditingTitle)); }
        }

        private bool _isImageExpanded = false;
        public bool IsImageExpanded
        {
            get => _isImageExpanded;
            set { _isImageExpanded = value; OnPropertyChanged(nameof(IsImageExpanded)); }
        }

        private string _expandedImagePath = "";
        public string ExpandedImagePath
        {
            get => _expandedImagePath;
            set { _expandedImagePath = value; OnPropertyChanged(nameof(ExpandedImagePath)); }
        }

        private int _expandedImageIndex = -1;
        public int ExpandedImageIndex
        {
            get => _expandedImageIndex;
            set { _expandedImageIndex = value; OnPropertyChanged(nameof(ExpandedImageIndex)); }
        }

        // 置換機能関連のプロパティ
        private bool _enableBulkReplace = false;
        public bool EnableBulkReplace
        {
            get => _enableBulkReplace;
            set { _enableBulkReplace = value; OnPropertyChanged(nameof(EnableBulkReplace)); }
        }

        private string _replaceTarget = "";
        public string ReplaceTarget
        {
            get => _replaceTarget;
            set { _replaceTarget = value; OnPropertyChanged(nameof(ReplaceTarget)); }
        }

        private string _replaceWith = "";
        public string ReplaceWith
        {
            get => _replaceWith;
            set { _replaceWith = value; OnPropertyChanged(nameof(ReplaceWith)); }
        }

        // コンテンツ表示制御用プロパティ
        private bool _isContentReady = true; // デフォルトではtrueにして既存動作を維持
        public bool IsContentReady
        {
            get => _isContentReady;
            set { _isContentReady = value; OnPropertyChanged(nameof(IsContentReady)); }
        }

        // Steps変更機能関連のプロパティ
        private long _bulkStepsValue = 8;
        public long BulkStepsValue
        {
            get => _bulkStepsValue;
            set { _bulkStepsValue = value; OnPropertyChanged(nameof(BulkStepsValue)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public class TabItemDto
        {
            public Guid Guid { get; set; } = Guid.NewGuid(); // 一意識別子
            public List<string> ImagePaths { get; set; } = new();
            public string TextBoxValue { get; set; } = "";
            public string NegativePromptValue { get; set; } = "";
            public bool IsButtonEnabled { get; set; } = true;
            public string Title { get; set; } = "タブ";
            public List<TabItemDto> InnerTabs { get; set; } = new();
            public int SelectedInnerTabIndex { get; set; } = 0;
            
            // Additional properties for save/restore
            public int Width { get; set; } = 512;
            public int Height { get; set; } = 512;
            public int Steps { get; set; } = 20;
            public int BatchCount { get; set; } = 1;
            public int BatchSize { get; set; } = 4;
            public double CfgScale { get; set; } = 7.0;
            public double DenoisingStrength { get; set; } = 0.7;
            public string SelectedSamplingMethod { get; set; } = "Euler a";
            public string SelectedScheduleType { get; set; } = "Automatic";
            public long Seed { get; set; } = -1;
            public long Subseed { get; set; } = -1;
            public bool CombinatorialGeneration { get; set; } = false;
            public bool EnableHiresFix { get; set; } = false;
            public string SelectedUpscaler { get; set; } = "Latent (nearest-exact)";
            public int HiresSteps { get; set; } = 6;
            public double HiresUpscaleBy { get; set; } = 1.5;
            public int HiresResizeWidth { get; set; } = 1024;
            public int HiresResizeHeight { get; set; } = 1024;
            
            // Kohya hires.fix関連のプロパティ
            public bool EnableKohyaHiresFix { get; set; } = false;
            public int KohyaBlockNumber { get; set; } = 3;
            public double KohyaDownscaleFactor { get; set; } = 1.75;
            
            // Random Resolution関連のプロパティ
            public bool EnableRandomResolution { get; set; } = false;
            
            // GenerateButtonTextは保存対象から除外（アプリ終了時にGenerate状態はリセットされるため）
        }

        public TabItemDto ToDto()
        {
            return new TabItemDto
            {
                Guid = this.Guid,
                ImagePaths = this.ImagePaths.Select(ConvertToRelativePath).ToList(),
                TextBoxValue = this.TextBoxValue,
                NegativePromptValue = this.NegativePromptValue,
                IsButtonEnabled = this.IsButtonEnabled,
                Title = this.Title,
                InnerTabs = this.InnerTabs.Select(x => x.ToDto()).ToList(),
                SelectedInnerTabIndex = this.SelectedInnerTab != null ? this.InnerTabs.IndexOf(this.SelectedInnerTab) : -1,
                Width = this.Width,
                Height = this.Height,
                Steps = this.Steps,
                BatchCount = this.BatchCount,
                BatchSize = this.BatchSize,
                CfgScale = this.CfgScale,
                DenoisingStrength = this.DenoisingStrength,
                SelectedSamplingMethod = this.SelectedSamplingMethod,
                SelectedScheduleType = this.SelectedScheduleType,
                Seed = this.Seed,
                Subseed = this.Subseed,
                EnableHiresFix = this.EnableHiresFix,
                SelectedUpscaler = this.SelectedUpscaler,
                HiresSteps = this.HiresSteps,
                HiresUpscaleBy = this.HiresUpscaleBy,
                HiresResizeWidth = this.HiresResizeWidth,
                HiresResizeHeight = this.HiresResizeHeight,
                CombinatorialGeneration = this.CombinatorialGeneration,
                EnableKohyaHiresFix = this.EnableKohyaHiresFix,
                KohyaBlockNumber = this.KohyaBlockNumber,
                KohyaDownscaleFactor = this.KohyaDownscaleFactor,
                EnableRandomResolution = this.EnableRandomResolution
                // GenerateButtonTextは保存しない（アプリ終了時にGenerate状態はリセットされるため）
            };
        }

        /// <summary>
        /// 絶対パスを実行ファイルからの相対パスに変換
        /// </summary>
        private string ConvertToRelativePath(string absolutePath)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var exeDir = System.IO.Path.GetDirectoryName(exePath) ?? ".";
                
                // 既に相対パスの場合はそのまま返す
                if (!System.IO.Path.IsPathRooted(absolutePath))
                {
                    return absolutePath;
                }
                
                // 実行ファイルのディレクトリからの相対パスを計算
                var relativePath = System.IO.Path.GetRelativePath(exeDir, absolutePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"相対パス変換エラー: {ex.Message}");
                return absolutePath; // エラーの場合は元のパスを返す
            }
        }

        /// <summary>
        /// 相対パスを絶対パスに復元
        /// </summary>
        private string ConvertToAbsolutePath(string relativePath)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var exeDir = System.IO.Path.GetDirectoryName(exePath) ?? ".";
                
                // 既に絶対パスの場合はそのまま返す
                if (System.IO.Path.IsPathRooted(relativePath))
                {
                    return relativePath;
                }
                
                // 実行ファイルのディレクトリを基準に絶対パスを構築
                var absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(exeDir, relativePath));
                return absolutePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"絶対パス変換エラー: {ex.Message}");
                return relativePath; // エラーの場合は元のパスを返す
            }
        }

        /// <summary>
        /// 相対パスを絶対パスに復元（static版）
        /// </summary>
        private static string ConvertToAbsolutePathStatic(string relativePath)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var exeDir = System.IO.Path.GetDirectoryName(exePath) ?? ".";
                
                // 既に絶対パスの場合はそのまま返す
                if (System.IO.Path.IsPathRooted(relativePath))
                {
                    return relativePath;
                }
                
                // 実行ファイルのディレクトリを基準に絶対パスを構築
                var absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(exeDir, relativePath));
                return absolutePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"絶対パス変換エラー: {ex.Message}");
                return relativePath; // エラーの場合は元のパスを返す
            }
        }

        public static TabItemViewModel FromDto(TabItemDto dto)
        {
            // 復元時はInitializeListsAsyncを自動実行しない
            var vm = new TabItemViewModel(isRestoring: true)
            {
                Guid = dto.Guid != Guid.Empty ? dto.Guid : Guid.NewGuid(),
                ImagePaths = new ObservableCollection<string>((dto.ImagePaths ?? new List<string>()).Select(ConvertToAbsolutePathStatic)),
                TextBoxValue = dto.TextBoxValue,
                NegativePromptValue = dto.NegativePromptValue,
                IsButtonEnabled = dto.IsButtonEnabled,
                Title = dto.Title,
                Width = dto.Width,
                Height = dto.Height,
                Steps = dto.Steps,
                BatchCount = dto.BatchCount,
                BatchSize = dto.BatchSize,
                CfgScale = dto.CfgScale,
                DenoisingStrength = dto.DenoisingStrength,
                SelectedSamplingMethod = dto.SelectedSamplingMethod,
                SelectedScheduleType = dto.SelectedScheduleType,
                Seed = dto.Seed,
                Subseed = dto.Subseed,
                EnableHiresFix = dto.EnableHiresFix,
                SelectedUpscaler = dto.SelectedUpscaler,
                HiresSteps = dto.HiresSteps,
                HiresUpscaleBy = dto.HiresUpscaleBy,
                HiresResizeWidth = dto.HiresResizeWidth,
                HiresResizeHeight = dto.HiresResizeHeight,
                CombinatorialGeneration = dto.CombinatorialGeneration,
                EnableKohyaHiresFix = dto.EnableKohyaHiresFix,
                KohyaBlockNumber = dto.KohyaBlockNumber,
                KohyaDownscaleFactor = dto.KohyaDownscaleFactor,
                EnableRandomResolution = dto.EnableRandomResolution,
                GenerateButtonText = "Generate" // 復元時は常に"Generate"に設定
            };
            
            // 復元フラグを無効にして、以降は通常動作にする
            vm._isRestoring = false;
            
            // 内側タブを復元
            foreach (var inner in dto.InnerTabs ?? new List<TabItemDto>())
            {
                vm.InnerTabs.Add(FromDto(inner));
            }
            if (vm.InnerTabs.Count > 0 && dto.SelectedInnerTabIndex >= 0 && dto.SelectedInnerTabIndex < vm.InnerTabs.Count)
            {
                vm.SelectedInnerTab = vm.InnerTabs[dto.SelectedInnerTabIndex];
            }
            
            Debug.WriteLine($"FromDto復元完了: タブ '{vm.Title}', Sampler='{vm.SelectedSamplingMethod}', Scheduler='{vm.SelectedScheduleType}', Upscaler='{vm.SelectedUpscaler}'");
            
            return vm;
        }

        /// <summary>
        /// 復元されたタブのリストを初期化（復元完了後に呼び出し）
        /// </summary>
        public void InitializeListsAfterRestore()
        {
            if (_isRestoring)
            {
                _isRestoring = false;
            }
            InitializeListsAsync();
        }

        public void AddInnerTab()
        {
            _innerTabCount++;
            var innerTab = new TabItemViewModel($"{_innerTabCount}")
            {
                TextBoxValue = MainViewModel.LastPrompt,
                NegativePromptValue = MainViewModel.LastNegativePrompt,
                Width = MainViewModel.LastWidth,
                Height = MainViewModel.LastHeight,
                Steps = MainViewModel.LastSteps,
                BatchCount = AppSettings.Instance.DefaultBatchCount,
                BatchSize = AppSettings.Instance.DefaultBatchSize,
                CfgScale = MainViewModel.LastCfgScale,
                DenoisingStrength = MainViewModel.LastDenoisingStrength,
                Seed = -1,
                Subseed = MainViewModel.LastSubseed,
                EnableHiresFix = MainViewModel.LastEnableHiresFix,
                HiresSteps = MainViewModel.LastHiresSteps,
                HiresUpscaleBy = MainViewModel.LastHiresUpscaleBy,
                HiresResizeWidth = MainViewModel.LastHiresResizeWidth,
                HiresResizeHeight = MainViewModel.LastHiresResizeHeight,
                CombinatorialGeneration = MainViewModel.LastCombinatorialGeneration,
                EnableKohyaHiresFix = MainViewModel.LastEnableKohyaHiresFix,
                KohyaBlockNumber = MainViewModel.LastKohyaBlockNumber,
                KohyaDownscaleFactor = MainViewModel.LastKohyaDownscaleFactor,
                EnableRandomResolution = MainViewModel.LastEnableRandomResolution
            };
            
            // Sampler/Scheduler/Upscalerを安全に設定
            innerTab.SetSamplerSafely(MainViewModel.LastSelectedSamplingMethod);
            innerTab.SetSchedulerSafely(MainViewModel.LastSelectedScheduleType);
            innerTab.SetUpscalerSafely(MainViewModel.LastSelectedUpscaler);
            
            InnerTabs.Add(innerTab);
            SelectedInnerTab = innerTab;
            
            // この外側タブの内側タブのタイトルを更新
            UpdateInnerTabTitles();
        }

        public void AddInnerTabAtPosition()
        {
            // 現在選択されている内側タブのインデックスを取得
            int currentIndex = SelectedInnerTab != null ? InnerTabs.IndexOf(SelectedInnerTab) : -1;
            
            // 新しいタブを作成
            _innerTabCount++;
            var innerTab = new TabItemViewModel($"{_innerTabCount}")
            {
                TextBoxValue = MainViewModel.LastPrompt,
                NegativePromptValue = MainViewModel.LastNegativePrompt,
                Width = MainViewModel.LastWidth,
                Height = MainViewModel.LastHeight,
                Steps = MainViewModel.LastSteps,
                BatchCount = AppSettings.Instance.DefaultBatchCount,
                BatchSize = AppSettings.Instance.DefaultBatchSize,
                CfgScale = MainViewModel.LastCfgScale,
                DenoisingStrength = MainViewModel.LastDenoisingStrength,
                Seed = -1,
                Subseed = MainViewModel.LastSubseed,
                EnableHiresFix = MainViewModel.LastEnableHiresFix,
                HiresSteps = MainViewModel.LastHiresSteps,
                HiresUpscaleBy = MainViewModel.LastHiresUpscaleBy,
                HiresResizeWidth = MainViewModel.LastHiresResizeWidth,
                HiresResizeHeight = MainViewModel.LastHiresResizeHeight,
                CombinatorialGeneration = MainViewModel.LastCombinatorialGeneration,
                EnableKohyaHiresFix = MainViewModel.LastEnableKohyaHiresFix,
                KohyaBlockNumber = MainViewModel.LastKohyaBlockNumber,
                KohyaDownscaleFactor = MainViewModel.LastKohyaDownscaleFactor,
                EnableRandomResolution = MainViewModel.LastEnableRandomResolution
            };
            
            // Sampler/Scheduler/Upscalerを安全に設定
            innerTab.SetSamplerSafely(MainViewModel.LastSelectedSamplingMethod);
            innerTab.SetSchedulerSafely(MainViewModel.LastSelectedScheduleType);
            innerTab.SetUpscalerSafely(MainViewModel.LastSelectedUpscaler);
            
            // 現在のタブの右側に挿入
            if (currentIndex >= 0 && currentIndex < InnerTabs.Count - 1)
            {
                InnerTabs.Insert(currentIndex + 1, innerTab);
            }
            else
            {
                // 現在のタブが最後の場合、または選択されていない場合は末尾に追加
                InnerTabs.Add(innerTab);
            }
            
            // 新しく作成したタブを選択
            SelectedInnerTab = innerTab;
            
            // この外側タブの内側タブのタイトルを更新
            UpdateInnerTabTitles();
        }

        /// <summary>
        /// 現在選択されている内側タブを複製して、その右側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        public void DuplicateCurrentInnerTab()
        {
            DuplicateCurrentInnerTabAt(true);
        }

        /// <summary>
        /// 現在選択されている内側タブを複製して、その左側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        public void DuplicateCurrentInnerTabLeft()
        {
            DuplicateCurrentInnerTabAt(false);
        }

        /// <summary>
        /// 指定された内側タブを複製して、その右側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        /// <param name="targetTab">複製対象の内側タブ</param>
        public void DuplicateInnerTabRight(TabItemViewModel targetTab)
        {
            DuplicateInnerTabAt(targetTab, true);
        }

        /// <summary>
        /// 指定された内側タブを複製して、その左側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        /// <param name="targetTab">複製対象の内側タブ</param>
        public void DuplicateInnerTabLeft(TabItemViewModel targetTab)
        {
            DuplicateInnerTabAt(targetTab, false);
        }

        /// <summary>
        /// 指定された内側タブを複製して、指定された側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        /// <param name="targetTab">複製対象の内側タブ</param>
        /// <param name="toRight">trueなら右側、falseなら左側に複製</param>
        private void DuplicateInnerTabAt(TabItemViewModel targetTab, bool toRight)
        {
            if (targetTab == null || !InnerTabs.Contains(targetTab)) return;
            
            // 指定された内側タブのインデックスを取得
            int targetIndex = InnerTabs.IndexOf(targetTab);
            
            // 新しいタブを作成（指定されたタブの設定をコピー）
            _innerTabCount++;
            var innerTab = new TabItemViewModel($"{_innerTabCount}")
            {
                TextBoxValue = targetTab.TextBoxValue,
                NegativePromptValue = targetTab.NegativePromptValue,
                Width = targetTab.Width,
                Height = targetTab.Height,
                Steps = targetTab.Steps,
                BatchCount = targetTab.BatchCount,
                BatchSize = targetTab.BatchSize,
                CfgScale = targetTab.CfgScale,
                DenoisingStrength = targetTab.DenoisingStrength,
                Seed = -1, // Seedは-1にリセット
                Subseed = targetTab.Subseed,
                EnableHiresFix = targetTab.EnableHiresFix,
                HiresSteps = targetTab.HiresSteps,
                HiresUpscaleBy = targetTab.HiresUpscaleBy,
                HiresResizeWidth = targetTab.HiresResizeWidth,
                HiresResizeHeight = targetTab.HiresResizeHeight,
                CombinatorialGeneration = targetTab.CombinatorialGeneration,
                EnableKohyaHiresFix = targetTab.EnableKohyaHiresFix,
                KohyaBlockNumber = targetTab.KohyaBlockNumber,
                KohyaDownscaleFactor = targetTab.KohyaDownscaleFactor,
                EnableRandomResolution = targetTab.EnableRandomResolution
                // ImagePathsは空のままにする（画像は複製しない）
            };
            
            // Sampler/Scheduler/Upscalerを安全に設定（targetTabから複製）
            innerTab.SetSamplerSafely(targetTab.SelectedSamplingMethod);
            innerTab.SetSchedulerSafely(targetTab.SelectedScheduleType);
            innerTab.SetUpscalerSafely(targetTab.SelectedUpscaler);
            
            // 指定された側に挿入
            int insertIndex;
            if (toRight)
            {
                // 右側に挿入
                if (targetIndex >= 0 && targetIndex < InnerTabs.Count - 1)
                {
                    insertIndex = targetIndex + 1;
                }
                else
                {
                    // 対象タブが最後の場合は末尾に追加
                    insertIndex = InnerTabs.Count;
                }
            }
            else
            {
                // 左側に挿入
                insertIndex = Math.Max(0, targetIndex);
            }
            
            InnerTabs.Insert(insertIndex, innerTab);
            
            // 新しく作成したタブを選択
            SelectedInnerTab = innerTab;
            
            // この外側タブの内側タブのタイトルを更新
            UpdateInnerTabTitles();
        }

        /// <summary>
        /// 現在選択されている内側タブを複製して、指定された側に新しい内側タブを追加します（画像は除く）
        /// </summary>
        /// <param name="toRight">trueなら右側、falseなら左側に複製</param>
        private void DuplicateCurrentInnerTabAt(bool toRight)
        {
            if (SelectedInnerTab == null) return;
            
            // 現在選択されている内側タブのインデックスを取得
            int currentIndex = InnerTabs.IndexOf(SelectedInnerTab);
            
            // 新しいタブを作成（現在のタブの設定をコピー）
            _innerTabCount++;
            var innerTab = new TabItemViewModel($"{_innerTabCount}")
            {
                TextBoxValue = SelectedInnerTab.TextBoxValue,
                NegativePromptValue = SelectedInnerTab.NegativePromptValue,
                Width = SelectedInnerTab.Width,
                Height = SelectedInnerTab.Height,
                Steps = SelectedInnerTab.Steps,
                BatchCount = SelectedInnerTab.BatchCount,
                BatchSize = SelectedInnerTab.BatchSize,
                CfgScale = SelectedInnerTab.CfgScale,
                DenoisingStrength = SelectedInnerTab.DenoisingStrength,
                Seed = -1, // Seedは-1にリセット
                Subseed = SelectedInnerTab.Subseed,
                EnableHiresFix = SelectedInnerTab.EnableHiresFix,
                HiresSteps = SelectedInnerTab.HiresSteps,
                HiresUpscaleBy = SelectedInnerTab.HiresUpscaleBy,
                HiresResizeWidth = SelectedInnerTab.HiresResizeWidth,
                HiresResizeHeight = SelectedInnerTab.HiresResizeHeight,
                CombinatorialGeneration = SelectedInnerTab.CombinatorialGeneration,
                EnableKohyaHiresFix = SelectedInnerTab.EnableKohyaHiresFix,
                KohyaBlockNumber = SelectedInnerTab.KohyaBlockNumber,
                KohyaDownscaleFactor = SelectedInnerTab.KohyaDownscaleFactor,
                EnableRandomResolution = SelectedInnerTab.EnableRandomResolution
                // ImagePathsは空のままにする（画像は複製しない）
            };
            
            // Sampler/Scheduler/Upscalerを安全に設定
            innerTab.SetSamplerSafely(SelectedInnerTab.SelectedSamplingMethod);
            innerTab.SetSchedulerSafely(SelectedInnerTab.SelectedScheduleType);
            innerTab.SetUpscalerSafely(SelectedInnerTab.SelectedUpscaler);
            
            // 指定された側に挿入
            int insertIndex;
            if (toRight)
            {
                // 右側に挿入
                if (currentIndex >= 0 && currentIndex < InnerTabs.Count - 1)
                {
                    insertIndex = currentIndex + 1;
                }
                else
                {
                    // 現在のタブが最後の場合は末尾に追加
                    insertIndex = InnerTabs.Count;
                }
            }
            else
            {
                // 左側に挿入
                insertIndex = Math.Max(0, currentIndex);
            }
            
            InnerTabs.Insert(insertIndex, innerTab);
            
            // 新しく作成したタブを選択
            SelectedInnerTab = innerTab;
            
            // この外側タブの内側タブのタイトルを更新
            UpdateInnerTabTitles();
        }

        public void UpdateInnerTabTitles()
        {
            for (int i = 0; i < InnerTabs.Count; i++)
            {
                InnerTabs[i].Title = (i + 1).ToString();
            }
        }

        /// <summary>
        /// この外側タブとその内側タブ全てを複製します
        /// </summary>
        /// <param name="newTitle">新しいタブのタイトル</param>
        /// <returns>複製された外側タブ</returns>
        public TabItemViewModel Duplicate(string newTitle)
        {
            // 現在のタブをDTOに変換してから新しいタブを作成することで、完全なコピーを作成
            var dto = this.ToDto();
            dto.Title = newTitle;
            dto.Guid = Guid.NewGuid(); // 複製時は新しいGUIDを生成
            
            // 画像パスはコピーしない（空にする）
            dto.ImagePaths = new List<string>();
            
            // 内側タブの画像パスもクリアし、Seedを-1に設定、新しいGUIDを生成
            foreach (var innerTabDto in dto.InnerTabs)
            {
                innerTabDto.Guid = Guid.NewGuid(); // 内側タブも新しいGUIDを生成
                innerTabDto.ImagePaths = new List<string>();
                innerTabDto.Seed = -1; // 複製時は常にSeedを-1に設定
            }
            
            var duplicatedTab = TabItemViewModel.FromDto(dto);
            
            return duplicatedTab;
        }

        /// <summary>
        /// 指定されたプリセットに基づいて幅と高さを設定します
        /// </summary>
        /// <param name="preset">プリセット名</param>
        private void SetDimensionPreset(string preset)
        {
            var presets = AppSettings.Instance.GetResolutionPresets();
            
            if (presets.TryGetValue(preset, out var resolution))
            {
                Width = resolution.width;
                Height = resolution.height;
            }
            else
            {
                // フォールバック: プリセットが見つからない場合はデフォルト値
                System.Diagnostics.Debug.WriteLine($"プリセット '{preset}' が見つかりません。デフォルト値を使用します。");
                Width = 512;
                Height = 512;
            }
        }

        /// <summary>
        /// 幅と高さを入れ替えます
        /// </summary>
        private void SwapDimensions()
        {
            int temp = Width;
            Width = Height;
            Height = temp;
        }

        /// <summary>
        /// この外側タブの全ての内側タブのプロンプトとネガティブプロンプトに対して一括置換を実行します
        /// </summary>
        public void ExecuteBulkReplace()
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteBulkReplace called: Target='{ReplaceTarget}', With='{ReplaceWith}', InnerTabs Count={InnerTabs.Count}");
            System.Diagnostics.Debug.WriteLine($"Current Tab Title: '{Title}'");
            
            // 各内側タブの詳細をデバッグ出力
            for (int i = 0; i < InnerTabs.Count; i++)
            {
                var tab = InnerTabs[i];
                System.Diagnostics.Debug.WriteLine($"InnerTab[{i}]: Title='{tab.Title}', Prompt='{tab.TextBoxValue}', NegativePrompt='{tab.NegativePromptValue}'");
            }
            
            if (string.IsNullOrEmpty(ReplaceTarget))
            {
                return;
            }

            if (InnerTabs.Count == 0)
            {
                System.Windows.MessageBox.Show($"No inner tabs found.\n\nCurrent tab: '{Title}'\nInner tab count: {InnerTabs.Count}\n\nPlease add inner tabs before executing replace.", "Replace Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int replacedCount = 0;
            int totalChanges = 0;
            
            // 全ての内側タブに対して置換を実行
            foreach (var innerTab in InnerTabs)
            {
                bool hasChanges = false;
                int tabChanges = 0;
                
                System.Diagnostics.Debug.WriteLine($"Processing tab '{innerTab.Title}': Prompt='{innerTab.TextBoxValue}', NegativePrompt='{innerTab.NegativePromptValue}'");
                
                // プロンプトの置換
                if (!string.IsNullOrEmpty(innerTab.TextBoxValue) && innerTab.TextBoxValue.Contains(ReplaceTarget))
                {
                    var oldValue = innerTab.TextBoxValue;
                    innerTab.TextBoxValue = innerTab.TextBoxValue.Replace(ReplaceTarget, ReplaceWith ?? "");
                    hasChanges = true;
                    tabChanges++;
                    System.Diagnostics.Debug.WriteLine($"Replaced in Prompt: '{oldValue}' -> '{innerTab.TextBoxValue}'");
                }
                
                // ネガティブプロンプトの置換
                if (!string.IsNullOrEmpty(innerTab.NegativePromptValue) && innerTab.NegativePromptValue.Contains(ReplaceTarget))
                {
                    var oldValue = innerTab.NegativePromptValue;
                    innerTab.NegativePromptValue = innerTab.NegativePromptValue.Replace(ReplaceTarget, ReplaceWith ?? "");
                    hasChanges = true;
                    tabChanges++;
                    System.Diagnostics.Debug.WriteLine($"Replaced in Negative Prompt: '{oldValue}' -> '{innerTab.NegativePromptValue}'");
                }
                
                if (hasChanges)
                {
                    replacedCount++;
                    totalChanges += tabChanges;
                }
            }
            
            // 結果をメッセージボックスで表示
            string message;
            if (replacedCount == 0)
            {
                message = $"置換対象 '{ReplaceTarget}' が見つかりませんでした。\n\n確認したタブ数: {InnerTabs.Count}";
                
                // 各タブの内容も表示
                if (InnerTabs.Count > 0)
                {
                    message += "\n\n各タブの内容:";
                    for (int i = 0; i < Math.Min(3, InnerTabs.Count); i++) // 最初の3つまで表示
                    {
                        var tab = InnerTabs[i];
                        message += $"\nタブ{i + 1}: '{tab.TextBoxValue ?? "(空)"}' / '{tab.NegativePromptValue ?? "(空)"}'";
                    }
                    if (InnerTabs.Count > 3)
                    {
                        message += $"\n... 他 {InnerTabs.Count - 3} タブ";
                    }
                }
            }
        }

        /// <summary>
        /// この外側タブの全ての内側タブのStepsを指定された値に変更します
        /// </summary>
        public void ExecuteBulkStepsChange()
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteBulkStepsChange called: BulkStepsValue={BulkStepsValue}, InnerTabs Count={InnerTabs.Count}");
            System.Diagnostics.Debug.WriteLine($"Current Tab Title: '{Title}'");
            
            if (InnerTabs.Count == 0)
            {
                System.Windows.MessageBox.Show($"No inner tabs found.\n\nCurrent tab: '{Title}'\nInner tab count: {InnerTabs.Count}\n\nPlease add inner tabs before executing steps change.", "Steps Change Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            int changedCount = 0;
            
            // 全ての内側タブに対してStepsを変更
            foreach (var innerTab in InnerTabs)
            {
                System.Diagnostics.Debug.WriteLine($"Processing tab '{innerTab.Title}': Current Steps={innerTab.Steps}");
                
                innerTab.Steps = (int)BulkStepsValue;
                changedCount++;
                
                System.Diagnostics.Debug.WriteLine($"Changed Steps: '{innerTab.Title}' -> {BulkStepsValue}");
            }
            
            // 結果をメッセージボックスで表示
            System.Diagnostics.Debug.WriteLine($"ExecuteBulkStepsChange completed: {changedCount} tabs changed to Steps {BulkStepsValue}");
        }

        public void RemoveInnerTab(TabItemViewModel outerTab, TabItemViewModel innerTab)
        {
            if (outerTab.InnerTabs.Contains(innerTab) && outerTab.InnerTabs.Count > 1)
            {
                if (innerTab != null)
                {
                    var originalIndex = outerTab.InnerTabs.IndexOf(innerTab);
                    outerTab.InnerTabs.RemoveAt(originalIndex);
                }
            }
        }

        /// <summary>
        /// サンプラー、スケジューラー、アップスケーラーリストを非同期で初期化
        /// </summary>
        public async void InitializeListsAsync()
        {
            try
            {
                // 現在選択されているサンプラー、スケジューラー、アップスケーラーを保持
                string currentSampler = _selectedSamplingMethod;
                string currentScheduler = _selectedScheduleType;
                string currentUpscaler = _selectedUpscaler;
                
                Debug.WriteLine($"タブ '{Title}' のリスト初期化開始: Sampler='{currentSampler}', Scheduler='{currentScheduler}', Upscaler='{currentUpscaler}'");
                
                // SamplerManagerからサンプラーリストを取得（現在の選択を確実に含む）
                var samplers = await SamplerManager.Instance.EnsureSamplerExistsAsync(currentSampler);
                
                // SchedulerManagerからスケジューラーリストを取得（現在の選択を確実に含む）
                var schedulers = await SchedulerManager.Instance.EnsureSchedulerExistsAsync(currentScheduler);
                
                // UpscalerManagerからアップスケーラーリストを取得（現在の選択を確実に含む）
                var upscalers = await UpscalerManager.Instance.EnsureUpscalerExistsAsync(currentUpscaler);
                
                // UIスレッドでコレクションを更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // サンプラーリストを更新
                    _samplingMethods.Clear();
                    foreach (var sampler in samplers)
                    {
                        _samplingMethods.Add(sampler);
                    }
                    
                    // スケジューラーリストを更新
                    _scheduleTypes.Clear();
                    foreach (var scheduler in schedulers)
                    {
                        _scheduleTypes.Add(scheduler);
                    }
                    
                    // アップスケーラーリストを更新
                    _upscalerTypes.Clear();
                    foreach (var upscaler in upscalers)
                    {
                        _upscalerTypes.Add(upscaler);
                    }
                    
                    Debug.WriteLine($"タブ '{Title}' のリスト更新完了: サンプラー{_samplingMethods.Count}個、スケジューラー{_scheduleTypes.Count}個、アップスケーラー{_upscalerTypes.Count}個");
                    
                    // プロパティセッターを使用して選択状態を復元（リストに確実に存在することが保証されている）
                    if (!string.IsNullOrEmpty(currentSampler) && _samplingMethods.Contains(currentSampler))
                    {
                        SelectedSamplingMethod = currentSampler;
                        Debug.WriteLine($"サンプラー選択復元: {currentSampler} ✓");
                    }
                    else if (!string.IsNullOrEmpty(currentSampler))
                    {
                        Debug.WriteLine($"警告: サンプラー '{currentSampler}' がリストに存在しません");
                        SelectedSamplingMethod = _samplingMethods.FirstOrDefault() ?? "Euler a";
                    }
                    
                    if (!string.IsNullOrEmpty(currentScheduler) && _scheduleTypes.Contains(currentScheduler))
                    {
                        SelectedScheduleType = currentScheduler;
                        Debug.WriteLine($"スケジューラー選択復元: {currentScheduler} ✓");
                    }
                    else if (!string.IsNullOrEmpty(currentScheduler))
                    {
                        Debug.WriteLine($"警告: スケジューラー '{currentScheduler}' がリストに存在しません");
                        SelectedScheduleType = _scheduleTypes.FirstOrDefault() ?? "Automatic";
                    }
                    
                    if (!string.IsNullOrEmpty(currentUpscaler) && _upscalerTypes.Contains(currentUpscaler))
                    {
                        SelectedUpscaler = currentUpscaler;
                        Debug.WriteLine($"アップスケーラー選択復元: {currentUpscaler} ✓");
                    }
                    else if (!string.IsNullOrEmpty(currentUpscaler))
                    {
                        Debug.WriteLine($"警告: アップスケーラー '{currentUpscaler}' がリストに存在しません");
                        SelectedUpscaler = _upscalerTypes.FirstOrDefault() ?? "Latent (nearest-exact)";
                    }
                    
                    Debug.WriteLine($"タブ '{Title}' のリスト初期化完了");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"リスト初期化エラー: {ex.Message}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
            }
        }

        private bool _disposed = false;
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // PropertyChangedイベントハンドラーをクリア
                    PropertyChanged = null;
                    
                    // 内側タブのDisposeを実行
                    foreach (var innerTab in _innerTabs)
                    {
                        innerTab?.Dispose();
                    }
                    
                    // コレクションをクリア
                    _innerTabs.Clear();
                    _imagePaths.Clear();
                }
                
                _disposed = true;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// テキストボックスのUndo履歴をクリアする
        /// </summary>
        private void ClearTextBoxUndoHistory()
        {
            try
            {
                // UIスレッドで実行する
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // MainWindowを取得
                            if (Application.Current.MainWindow is MainWindow mainWindow)
                            {
                                // PromptTextBoxとNegativePromptTextBoxを探してUndo履歴をクリア
                                var promptTextBox = FindVisualChild<AutoCompleteTextBox>(mainWindow, "PromptTextBox");
                                var negativeTextBox = FindVisualChild<AutoCompleteTextBox>(mainWindow, "NegativePromptTextBox");
                                
                                promptTextBox?.ClearUndoHistory();
                                negativeTextBox?.ClearUndoHistory();
                                
                                Debug.WriteLine("タブ切り替え時: テキストボックスのUndo履歴をクリアしました");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Undo履歴クリア処理エラー: {ex.Message}");
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo履歴クリアスケジューリングエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ビジュアルツリーから指定した名前のコントロールを検索
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T element && element.Name == childName)
                {
                    return element;
                }
                
                var childOfChild = FindVisualChild<T>(child, childName);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void SetSamplerSafely(string sampler)
        {
            if (!string.IsNullOrEmpty(sampler))
            {
                // リストに存在しない場合は一時的に追加
                if (!_samplingMethods.Contains(sampler))
                {
                    _samplingMethods.Add(sampler);
                    Debug.WriteLine($"サンプラー '{sampler}' を一時的にリストに追加");
                }
                _selectedSamplingMethod = sampler;
                OnPropertyChanged(nameof(SelectedSamplingMethod));
                Debug.WriteLine($"サンプラー設定: {sampler}");
            }
        }

        private void SetSchedulerSafely(string scheduler)
        {
            if (!string.IsNullOrEmpty(scheduler))
            {
                // リストに存在しない場合は一時的に追加
                if (!_scheduleTypes.Contains(scheduler))
                {
                    _scheduleTypes.Add(scheduler);
                    Debug.WriteLine($"スケジューラー '{scheduler}' を一時的にリストに追加");
                }
                _selectedScheduleType = scheduler;
                OnPropertyChanged(nameof(SelectedScheduleType));
                Debug.WriteLine($"スケジューラー設定: {scheduler}");
            }
        }

        private void SetUpscalerSafely(string upscaler)
        {
            if (!string.IsNullOrEmpty(upscaler))
            {
                // リストに存在しない場合は一時的に追加
                if (!_upscalerTypes.Contains(upscaler))
                {
                    _upscalerTypes.Add(upscaler);
                    Debug.WriteLine($"アップスケーラー '{upscaler}' を一時的にリストに追加");
                }
                _selectedUpscaler = upscaler;
                OnPropertyChanged(nameof(SelectedUpscaler));
                Debug.WriteLine($"アップスケーラー設定: {upscaler}");
            }
        }
    }

    /// <summary>
    /// 正の浮動小数点数のバリデーションルール
    /// </summary>
    public class PositiveDoubleValidationRule : System.Windows.Controls.ValidationRule
    {
        public override System.Windows.Controls.ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value == null)
            {
                return new System.Windows.Controls.ValidationResult(false, "値が必要です");
            }

            string input = value.ToString();
            
            // 空文字列は一時的に許可（入力中の可能性）
            if (string.IsNullOrWhiteSpace(input))
            {
                return System.Windows.Controls.ValidationResult.ValidResult;
            }

            // 浮動小数点数としてパース
            if (!double.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
            {
                return new System.Windows.Controls.ValidationResult(false, "有効な数値を入力してください");
            }

            // 正の数値かチェック
            if (result <= 0)
            {
                return new System.Windows.Controls.ValidationResult(false, "正の数値を入力してください");
            }

            return System.Windows.Controls.ValidationResult.ValidResult;
        }
    }
}
