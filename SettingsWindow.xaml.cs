using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.IO;

namespace SD.Yuzu
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private bool _isSaved = false;
        private string _validationMessage = string.Empty;
        private bool _isValidationMessageVisible = false;
        private bool _isValidationSuccess = false;
        private LocalizationHelper _localization;
        
        // Batch settings validation
        private string _batchValidationMessage = string.Empty;
        private bool _isBatchValidationMessageVisible = false;

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage != value)
                {
                    _validationMessage = value;
                    OnPropertyChanged();
                    IsValidationMessageVisible = !string.IsNullOrEmpty(value);
                }
            }
        }

        public bool IsValidationMessageVisible
        {
            get => _isValidationMessageVisible;
            set
            {
                if (_isValidationMessageVisible != value)
                {
                    _isValidationMessageVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValidationSuccess
        {
            get => _isValidationSuccess;
            set
            {
                if (_isValidationSuccess != value)
                {
                    _isValidationSuccess = value;
                    OnPropertyChanged();
                }
            }
        }

        public string BaseUrl
        {
            get => _settings.BaseUrl;
            set
            {
                _settings.BaseUrl = value;
                OnPropertyChanged();
            }
        }

        public string LoraDirectory
        {
            get => _settings.LoraDirectory;
            set
            {
                _settings.LoraDirectory = value;
                OnPropertyChanged();
            }
        }

        public string StableDiffusionDirectory
        {
            get => _settings.StableDiffusionDirectory;
            set
            {
                _settings.StableDiffusionDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoCompleteTagFile));
                OnPropertyChanged(nameof(IsAutoCompleteTagFileVisible));
                OnPropertyChanged(nameof(IsAutoCompleteTagFileValid));
            }
        }

        public string AutoCompleteTagFile
        {
            get 
            {
                if (string.IsNullOrEmpty(StableDiffusionDirectory))
                    return string.Empty;
                
                if (string.IsNullOrEmpty(_settings.AutoCompleteTagFile))
                {
                    return Path.Combine(StableDiffusionDirectory, "extensions", "a1111-sd-webui-tagcomplete", "tags", "danbooru.csv");
                }
                return _settings.AutoCompleteTagFile;
            }
            set
            {
                _settings.AutoCompleteTagFile = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAutoCompleteTagFileValid));
            }
        }

        public bool IsAutoCompleteTagFileVisible
        {
            get => !string.IsNullOrEmpty(StableDiffusionDirectory);
        }

        public bool IsAutoCompleteTagFileValid
        {
            get => string.IsNullOrEmpty(AutoCompleteTagFile) || File.Exists(AutoCompleteTagFile);
        }

        public new string Language
        {
            get 
            {
                var value = _settings.Language;
                System.Diagnostics.Debug.WriteLine($"Language getter called, returning: {value}");
                return value;
            }
            set
            {
                if (_settings.Language != value)
                {
                    System.Diagnostics.Debug.WriteLine($"Language setter called: {_settings.Language} -> {value}");
                    _settings.Language = value;
                    OnPropertyChanged();
                }
            }
        }

        public string BatchValidationMessage
        {
            get => _batchValidationMessage;
            set
            {
                if (_batchValidationMessage != value)
                {
                    _batchValidationMessage = value;
                    OnPropertyChanged();
                    IsBatchValidationMessageVisible = !string.IsNullOrEmpty(value);
                }
            }
        }

        public bool IsBatchValidationMessageVisible
        {
            get => _isBatchValidationMessageVisible;
            set
            {
                if (_isBatchValidationMessageVisible != value)
                {
                    _isBatchValidationMessageVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public int DefaultBatchSize
        {
            get => _settings.DefaultBatchSize;
            set
            {
                _settings.DefaultBatchSize = value;
                OnPropertyChanged();
            }
        }

        public int DefaultBatchCount
        {
            get => _settings.DefaultBatchCount;
            set
            {
                _settings.DefaultBatchCount = value;
                OnPropertyChanged();
            }
        }

        public int DefaultImageLimit
        {
            get => _settings.DefaultImageLimit;
            set
            {
                _settings.DefaultImageLimit = value;
                OnPropertyChanged();
            }
        }

        // Resolution presets properties
        public ResolutionPreset SmallPresets
        {
            get => _settings.SmallPresets;
            set
            {
                _settings.SmallPresets = value;
                OnPropertyChanged();
            }
        }

        public ResolutionPreset MediumPresets
        {
            get => _settings.MediumPresets;
            set
            {
                _settings.MediumPresets = value;
                OnPropertyChanged();
            }
        }

        public ResolutionPreset LargePresets
        {
            get => _settings.LargePresets;
            set
            {
                _settings.LargePresets = value;
                OnPropertyChanged();
            }
        }

        public SettingsWindow()
        {
            // LocalizationHelperを初期化
            _localization = LocalizationHelper.Instance;
            
            InitializeComponent();
            
            // 現在の設定をコピーして編集用に使用
            _settings = new AppSettings
            {
                BaseUrl = AppSettings.Instance.BaseUrl,
                LoraDirectory = AppSettings.Instance.LoraDirectory,
                StableDiffusionDirectory = AppSettings.Instance.StableDiffusionDirectory,
                AutoCompleteTagFile = AppSettings.Instance.AutoCompleteTagFile,
                Language = AppSettings.Instance.Language,
                DefaultBatchSize = AppSettings.Instance.DefaultBatchSize,
                DefaultBatchCount = AppSettings.Instance.DefaultBatchCount,
                DefaultImageLimit = AppSettings.Instance.DefaultImageLimit,
                SmallPresets = new ResolutionPreset
                {
                    Portrait = AppSettings.Instance.SmallPresets.Portrait,
                    Landscape = AppSettings.Instance.SmallPresets.Landscape,
                    Square = AppSettings.Instance.SmallPresets.Square
                },
                MediumPresets = new ResolutionPreset
                {
                    Portrait = AppSettings.Instance.MediumPresets.Portrait,
                    Landscape = AppSettings.Instance.MediumPresets.Landscape,
                    Square = AppSettings.Instance.MediumPresets.Square
                },
                LargePresets = new ResolutionPreset
                {
                    Portrait = AppSettings.Instance.LargePresets.Portrait,
                    Landscape = AppSettings.Instance.LargePresets.Landscape,
                    Square = AppSettings.Instance.LargePresets.Square
                }
            };

            DataContext = this;
            
            // Loadedイベントで言語コンボボックスの選択状態を設定
            Loaded += SettingsWindow_Loaded;
            
            // 言語変更イベントを購読
            _localization.LanguageChanged += OnLanguageChanged;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // DataContextがバインドされた後に言語コンボボックスの選択状態を設定
            SetLanguageComboBoxSelection();
        }

        private void SetLanguageComboBoxSelection()
        {
            // SelectedValueを使用して現在の言語を設定
            System.Diagnostics.Debug.WriteLine($"Setting language combo box to: {Language}");
            LanguageComboBox.SelectedValue = Language;
            System.Diagnostics.Debug.WriteLine($"ComboBox SelectedValue after setting: {LanguageComboBox.SelectedValue}");
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // 言語が変更された時にすべてのローカライズされたプロパティを更新
            OnPropertyChanged(nameof(Language));
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"LanguageComboBox_SelectionChanged called");
            System.Diagnostics.Debug.WriteLine($"SelectedValue: {LanguageComboBox.SelectedValue}");
            
            if (LanguageComboBox.SelectedValue is string selectedLanguage)
            {
                System.Diagnostics.Debug.WriteLine($"Setting language to: {selectedLanguage}");
                _localization.SetLanguage(selectedLanguage);
                Language = selectedLanguage;
                System.Diagnostics.Debug.WriteLine($"Language property after setting: {Language}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SelectedValue is not a string");
            }
        }

        private void BrowseLoraDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = _localization.Settings_BrowseLoraDialog,
                SelectedPath = LoraDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoraDirectory = dialog.SelectedPath;
            }
        }

        private void BrowseStableDiffusionDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = _localization.Settings_BrowseStableDiffusionDialog,
                SelectedPath = StableDiffusionDirectory,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                StableDiffusionDirectory = dialog.SelectedPath;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"SaveButton_Click called");
                System.Diagnostics.Debug.WriteLine($"Current Language property: {Language}");
                System.Diagnostics.Debug.WriteLine($"AppSettings.Instance.Language before update: {AppSettings.Instance.Language}");
                
                // ディレクトリのバリデーションを実行
                if (!ValidateDirectories())
                {
                    return;
                }
                
                // バッチ設定のバリデーションを実行
                if (!ValidateBatchSettings())
                {
                    return;
                }

                // 保存ボタンクリック時にエンドポイントバリデーションを実行
                if (!await PerformValidation(BaseUrl))
                {
                    return;
                }

                // 現在の設定を更新
                AppSettings.Instance.BaseUrl = BaseUrl;
                AppSettings.Instance.LoraDirectory = LoraDirectory;
                AppSettings.Instance.StableDiffusionDirectory = StableDiffusionDirectory;
                AppSettings.Instance.AutoCompleteTagFile = AutoCompleteTagFile;
                AppSettings.Instance.Language = Language;
                AppSettings.Instance.DefaultBatchSize = DefaultBatchSize;
                AppSettings.Instance.DefaultBatchCount = DefaultBatchCount;
                AppSettings.Instance.DefaultImageLimit = DefaultImageLimit;
                AppSettings.Instance.SmallPresets = SmallPresets;
                AppSettings.Instance.MediumPresets = MediumPresets;
                AppSettings.Instance.LargePresets = LargePresets;

                System.Diagnostics.Debug.WriteLine($"AppSettings.Instance.Language after update: {AppSettings.Instance.Language}");

                // ファイルに保存
                AppSettings.Instance.SaveToFile();
                
                System.Diagnostics.Debug.WriteLine($"Settings saved to file");

                _isSaved = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(_localization.GetString("Settings_SaveError", ex.Message), 
                               _localization.Settings_Error, 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckButton.IsEnabled = false;
                CheckButton.Content = _localization.Settings_CheckingButton;
                
                ValidationMessage = string.Empty; // メッセージをクリア
                
                bool isValid = await PerformValidation(BaseUrl);
                if (isValid)
                {
                    ValidationMessage = _localization.Settings_ConnectionSuccess;
                    IsValidationSuccess = true;
                }
            }
            finally
            {
                CheckButton.IsEnabled = true;
                CheckButton.Content = _localization.Settings_CheckButton;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isSaved)
            {
                DialogResult = false;
            }
            
            // イベントハンドラーの購読を解除
            Loaded -= SettingsWindow_Loaded;
            _localization.LanguageChanged -= OnLanguageChanged;
            
            base.OnClosing(e);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool ValidateDirectories()
        {
            // LoRAディレクトリのチェック
            if (!string.IsNullOrWhiteSpace(LoraDirectory))
            {
                if (!Directory.Exists(LoraDirectory))
                {
                    MessageBox.Show(_localization.GetString("Settings_LoraDirectoryNotFound", LoraDirectory), 
                                   _localization.Settings_DirectoryError, 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Warning);
                    return false;
                }
            }

            // Stable Diffusion WebUIディレクトリのチェック
            if (!string.IsNullOrWhiteSpace(StableDiffusionDirectory))
            {
                if (!Directory.Exists(StableDiffusionDirectory))
                {
                    MessageBox.Show(_localization.GetString("Settings_StableDiffusionDirectoryNotFound", StableDiffusionDirectory), 
                                   _localization.Settings_DirectoryError, 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Warning);
                    return false;
                }

                // extensionsフォルダの存在チェック
                string extensionsPath = Path.Combine(StableDiffusionDirectory, "extensions");
                if (!Directory.Exists(extensionsPath))
                {
                    MessageBox.Show(_localization.GetString("Settings_ExtensionsNotFound", extensionsPath), 
                                   _localization.Settings_DirectoryError, 
                                   MessageBoxButton.OK, 
                                   MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private bool ValidateBatchSettings()
        {
            // バッチサイズの検証
            if (DefaultBatchSize < 1)
            {
                MessageBox.Show(_localization.Settings_InvalidBatchSize, 
                               _localization.Settings_Error, 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                return false;
            }
            
            // バッチカウントの検証  
            if (DefaultBatchCount < 1 || DefaultBatchCount > 100)
            {
                MessageBox.Show(_localization.Settings_InvalidBatchCount, 
                               _localization.Settings_Error, 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                return false;
            }
            
            // 画像制限数の検証
            if (DefaultImageLimit < 1 || DefaultImageLimit > 1000)
            {
                MessageBox.Show(_localization.Settings_InvalidImageLimit, 
                               _localization.Settings_Error, 
                               MessageBoxButton.OK, 
                               MessageBoxImage.Warning);
                return false;
            }
            
            return true;
        }

        private async Task<bool> PerformValidation(string url)
        {
            // メッセージをクリアはCheckButtonで実行するので削除
            IsValidationSuccess = false; // エラー色にリセット

            // URLのpingチェック
            if (!await PingUrl(url))
            {
                ValidationMessage = _localization.Settings_PingFailed;
                return false;
            }

            // API呼び出しチェック
            if (!await CheckApiOptions(url))
            {
                ValidationMessage = _localization.Settings_ApiFailed;
                return false;
            }

            return true;
        }

        public async Task<bool> PingUrl(string url)
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
                Console.WriteLine($"URL到達性チェックエラー (GET): {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckApiOptions(string baseUrl)
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
                Console.WriteLine($"API呼び出しエラー: {ex.Message}");
                return false;
            }
        }

        private void DefaultBatchSizeTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateBatchSize();
        }

        private void DefaultBatchCountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateBatchCount();
        }

        private void DefaultImageLimitTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ValidateImageLimit();
        }

        private void ValidateBatchSize()
        {
            if (int.TryParse(DefaultBatchSizeTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 10)
                {
                    DefaultBatchSize = value;
                    ClearBatchValidationMessage();
                }
                else
                {
                    BatchValidationMessage = _localization.Settings_InvalidBatchSize;
                }
            }
            else if (!string.IsNullOrEmpty(DefaultBatchSizeTextBox.Text))
            {
                BatchValidationMessage = _localization.Settings_InvalidBatchSize;
            }
            else
            {
                ClearBatchValidationMessage();
            }
        }

        private void ValidateBatchCount()
        {
            if (int.TryParse(DefaultBatchCountTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 100)
                {
                    DefaultBatchCount = value;
                    ClearBatchValidationMessage();
                }
                else
                {
                    BatchValidationMessage = _localization.Settings_InvalidBatchCount;
                }
            }
            else if (!string.IsNullOrEmpty(DefaultBatchCountTextBox.Text))
            {
                BatchValidationMessage = _localization.Settings_InvalidBatchCount;
            }
            else
            {
                ClearBatchValidationMessage();
            }
        }

        private void ValidateImageLimit()
        {
            if (int.TryParse(DefaultImageLimitTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 1000)
                {
                    DefaultImageLimit = value;
                    ClearBatchValidationMessage();
                }
                else
                {
                    BatchValidationMessage = _localization.Settings_InvalidImageLimit;
                }
            }
            else if (!string.IsNullOrEmpty(DefaultImageLimitTextBox.Text))
            {
                BatchValidationMessage = _localization.Settings_InvalidImageLimit;
            }
            else
            {
                ClearBatchValidationMessage();
            }
        }

        private void ClearBatchValidationMessage()
        {
            BatchValidationMessage = string.Empty;
        }
        
        private void ResolutionPresetTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                ValidateResolutionPreset(textBox.Text, textBox.Name);
            }
        }

        private void ValidateResolutionPreset(string value, string textBoxName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SetResolutionValidationError($"{textBoxName}: 値が入力されていません");
                return;
            }

            // "width,height" 形式かチェック
            var parts = value.Split(',');
            if (parts.Length != 2)
            {
                SetResolutionValidationError($"{textBoxName}: 「幅,高さ」の形式で入力してください");
                return;
            }

            // 両方とも整数かチェック
            if (!int.TryParse(parts[0].Trim(), out int width) || !int.TryParse(parts[1].Trim(), out int height))
            {
                SetResolutionValidationError($"{textBoxName}: 幅と高さは整数で入力してください");
                return;
            }

            // 正の値かチェック
            if (width <= 0 || height <= 0)
            {
                SetResolutionValidationError($"{textBoxName}: 幅と高さは0より大きい値で入力してください");
                return;
            }

            // バリデーション成功
            ClearResolutionValidationError();
        }

        private void SetResolutionValidationError(string message)
        {
            // ここでエラーメッセージを表示する仕組みを実装
            // 現在のバリデーションメッセージシステムを再利用
            ValidationMessage = message;
            IsValidationSuccess = false;
        }

        private void ClearResolutionValidationError()
        {
            // 他のバリデーションエラーがない場合のみクリア
            if (ValidationMessage.Contains("Resolution") || ValidationMessage.Contains("解像度") || ValidationMessage.Contains("幅,高さ"))
            {
                ValidationMessage = string.Empty;
                IsValidationSuccess = true;
            }
        }

        private void AutoCompleteTagFileTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                AutoCompleteTagFile = textBox.Text;
            }
        }
    }
} 