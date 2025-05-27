using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SD.Yuzu
{
    public class LocalizationHelper : INotifyPropertyChanged
    {
        private static LocalizationHelper? _instance;
        private static readonly object _lock = new object();
        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture = null!;

        public static LocalizationHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalizationHelper();
                        }
                    }
                }
                return _instance;
            }
        }

        private LocalizationHelper()
        {
            _resourceManager = new ResourceManager("SD.Yuzu.Resources", typeof(LocalizationHelper).Assembly);
            
            // 設定から言語を取得
            var language = AppSettings.Instance.Language;
            SetLanguage(language);
        }

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            private set
            {
                if (_currentCulture != value)
                {
                    _currentCulture = value;
                    Thread.CurrentThread.CurrentCulture = value;
                    Thread.CurrentThread.CurrentUICulture = value;
                    OnPropertyChanged();
                    OnLanguageChanged();
                }
            }
        }

        public string CurrentLanguage => _currentCulture.Name;

        public void SetLanguage(string languageCode)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(languageCode);
                CurrentCulture = culture;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"言語設定エラー: {ex.Message}");
                // フォールバックとして英語を設定
                CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            }
        }

        public string GetString(string key)
        {
            try
            {
                var value = _resourceManager.GetString(key, _currentCulture);
                return value ?? $"[{key}]"; // キーが見つからない場合はキー名を表示
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"リソース取得エラー: {ex.Message}");
                return $"[{key}]";
            }
        }

        public string GetString(string key, params object[] args)
        {
            try
            {
                var format = GetString(key);
                return string.Format(format, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"フォーマット文字列取得エラー: {ex.Message}");
                return $"[{key}]";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? LanguageChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnLanguageChanged()
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            
            // すべてのローカライズプロパティの変更通知を発行
            OnPropertyChanged(nameof(Settings_Title));
            OnPropertyChanged(nameof(Settings_Language));
            OnPropertyChanged(nameof(Settings_Language_English));
            OnPropertyChanged(nameof(Settings_Language_Japanese));
            OnPropertyChanged(nameof(Settings_EndpointLabel));
            OnPropertyChanged(nameof(Settings_LoraDirectoryLabel));
            OnPropertyChanged(nameof(Settings_StableDiffusionDirectoryLabel));
            OnPropertyChanged(nameof(Settings_AutoCompleteTagFileLabel));
            OnPropertyChanged(nameof(Settings_CheckButton));
            OnPropertyChanged(nameof(Settings_CheckingButton));
            OnPropertyChanged(nameof(Settings_BrowseButton));
            OnPropertyChanged(nameof(Settings_SaveButton));
            OnPropertyChanged(nameof(Settings_CancelButton));
            OnPropertyChanged(nameof(Settings_ConnectionSuccess));
            OnPropertyChanged(nameof(Settings_PingFailed));
            OnPropertyChanged(nameof(Settings_ApiFailed));
            OnPropertyChanged(nameof(Settings_DirectoryError));
            OnPropertyChanged(nameof(Settings_Error));
            OnPropertyChanged(nameof(Settings_BrowseLoraDialog));
            OnPropertyChanged(nameof(Settings_BrowseStableDiffusionDialog));
            OnPropertyChanged(nameof(Settings_DefaultBatchSize));
            OnPropertyChanged(nameof(Settings_DefaultBatchCount));
            OnPropertyChanged(nameof(Settings_BatchSettingsDescription));
            OnPropertyChanged(nameof(Settings_InvalidBatchSize));
            OnPropertyChanged(nameof(Settings_InvalidBatchCount));
            OnPropertyChanged(nameof(Settings_DefaultImageLimit));
            OnPropertyChanged(nameof(Settings_DefaultImageLimitDescription));
            OnPropertyChanged(nameof(Settings_InvalidImageLimit));
            OnPropertyChanged(nameof(Settings_ResolutionPresets));
            OnPropertyChanged(nameof(Settings_ResolutionPresetsPortrait));
            OnPropertyChanged(nameof(Settings_ResolutionPresetsLandscape));
            OnPropertyChanged(nameof(Settings_ResolutionPresetsSquare));
            OnPropertyChanged(nameof(Settings_ResolutionPresetsFormat));
            
            // MainWindow用プロパティの変更通知
            OnPropertyChanged(nameof(MainWindow_Restart));
            OnPropertyChanged(nameof(MainWindow_RestartTooltip));
            OnPropertyChanged(nameof(MainWindow_Generating));
            OnPropertyChanged(nameof(MainWindow_CloseTab));
            OnPropertyChanged(nameof(MainWindow_DeleteTab));
            OnPropertyChanged(nameof(MainWindow_AddTab));
            OnPropertyChanged(nameof(MainWindow_AddNewTab));
            OnPropertyChanged(nameof(MainWindow_Duplicate));
            OnPropertyChanged(nameof(MainWindow_ExportImages));
            OnPropertyChanged(nameof(MainWindow_DuplicateRight));
            OnPropertyChanged(nameof(MainWindow_DuplicateLeft));
            OnPropertyChanged(nameof(MainWindow_Interrupt));
            OnPropertyChanged(nameof(MainWindow_Loading));
            OnPropertyChanged(nameof(MainWindow_Checkpoint));
            OnPropertyChanged(nameof(MainWindow_RefreshCheckpoints));
            OnPropertyChanged(nameof(MainWindow_SamplingMethod));
            OnPropertyChanged(nameof(MainWindow_ScheduleType));
            OnPropertyChanged(nameof(MainWindow_SamplingSteps));
            OnPropertyChanged(nameof(MainWindow_Width));
            OnPropertyChanged(nameof(MainWindow_Height));
            OnPropertyChanged(nameof(MainWindow_DimensionPresets));
            OnPropertyChanged(nameof(MainWindow_Portrait));
            OnPropertyChanged(nameof(MainWindow_Landscape));
            OnPropertyChanged(nameof(MainWindow_Square));
            OnPropertyChanged(nameof(MainWindow_PortraitM));
            OnPropertyChanged(nameof(MainWindow_LandscapeM));
            OnPropertyChanged(nameof(MainWindow_SquareM));
            OnPropertyChanged(nameof(MainWindow_PortraitL));
            OnPropertyChanged(nameof(MainWindow_LandscapeL));
            OnPropertyChanged(nameof(MainWindow_SquareL));
            OnPropertyChanged(nameof(MainWindow_SwapDimensions));
            OnPropertyChanged(nameof(MainWindow_BatchCount));
            OnPropertyChanged(nameof(MainWindow_BatchSize));
            OnPropertyChanged(nameof(MainWindow_CFGScale));
            OnPropertyChanged(nameof(MainWindow_Seed));
            OnPropertyChanged(nameof(MainWindow_SeedPlus1));
            OnPropertyChanged(nameof(MainWindow_SeedMinus1));
            OnPropertyChanged(nameof(MainWindow_RandomSeedTooltip));
            OnPropertyChanged(nameof(MainWindow_Recycle));
            OnPropertyChanged(nameof(MainWindow_CombinatorialGeneration));
            OnPropertyChanged(nameof(MainWindow_CombinatorialGenerationTooltip));
            OnPropertyChanged(nameof(MainWindow_DynamicPromptNotInstalled));
            OnPropertyChanged(nameof(MainWindow_KohyaHiresFixNotInstalled));
            OnPropertyChanged(nameof(MainWindow_KohyaHiresFix));
            OnPropertyChanged(nameof(MainWindow_KohyaHiresFixTooltip));
            OnPropertyChanged(nameof(MainWindow_BlockNumber));
            OnPropertyChanged(nameof(MainWindow_DownscaleFactor));
            OnPropertyChanged(nameof(MainWindow_HiresFix));
            OnPropertyChanged(nameof(MainWindow_Upscaler));
            OnPropertyChanged(nameof(MainWindow_HiresSteps));
            OnPropertyChanged(nameof(MainWindow_DenoisingStrength));
            OnPropertyChanged(nameof(MainWindow_UpscaleBy));
            OnPropertyChanged(nameof(MainWindow_RandomResolution));
            OnPropertyChanged(nameof(MainWindow_ModelType));
            OnPropertyChanged(nameof(MainWindow_ResolutionWeightMode));
            OnPropertyChanged(nameof(MainWindow_EqualWeights));
            OnPropertyChanged(nameof(MainWindow_FavorSmaller));
            OnPropertyChanged(nameof(MainWindow_FavorLarger));
            OnPropertyChanged(nameof(MainWindow_MinimumDimension));
            OnPropertyChanged(nameof(MainWindow_MaximumDimension));
            OnPropertyChanged(nameof(MainWindow_CurrentResolutions));
            OnPropertyChanged(nameof(MainWindow_CurrentResolutionsTooltip));
            OnPropertyChanged(nameof(MainWindow_NewWidth));
            OnPropertyChanged(nameof(MainWindow_NewHeight));
            OnPropertyChanged(nameof(MainWindow_AddResolution));
            OnPropertyChanged(nameof(MainWindow_AddResolutionTooltip));
            OnPropertyChanged(nameof(MainWindow_Replace));
            OnPropertyChanged(nameof(MainWindow_ReplaceTarget));
            OnPropertyChanged(nameof(MainWindow_ReplaceTargetTooltip));
            OnPropertyChanged(nameof(MainWindow_ReplaceWith));
            OnPropertyChanged(nameof(MainWindow_ReplaceWithTooltip));
            OnPropertyChanged(nameof(MainWindow_ExecuteReplace));
            OnPropertyChanged(nameof(MainWindow_ExecuteReplaceTooltip));
            OnPropertyChanged(nameof(MainWindow_BulkStepsTooltip));
            OnPropertyChanged(nameof(MainWindow_ChangeSteps));
            OnPropertyChanged(nameof(MainWindow_ChangeStepsTooltip));
            OnPropertyChanged(nameof(MainWindow_MResolutionize));
            OnPropertyChanged(nameof(MainWindow_MResolutionizeTooltip));
            OnPropertyChanged(nameof(MainWindow_LResolutionize));
            OnPropertyChanged(nameof(MainWindow_LResolutionizeTooltip));
            OnPropertyChanged(nameof(MainWindow_EnableHiresFix));
            OnPropertyChanged(nameof(MainWindow_EnableHiresFixTooltip));
            OnPropertyChanged(nameof(MainWindow_NormalResolutionize));
            OnPropertyChanged(nameof(MainWindow_NormalResolutionizeTooltip));
            OnPropertyChanged(nameof(MainWindow_RandomSeed));
            OnPropertyChanged(nameof(MainWindow_GenerateAll));
            OnPropertyChanged(nameof(MainWindow_GenerateAllTooltip));
            OnPropertyChanged(nameof(MainWindow_GenerateLeft));
            OnPropertyChanged(nameof(MainWindow_GenerateLeftTooltip));
            OnPropertyChanged(nameof(MainWindow_GenerateRight));
            OnPropertyChanged(nameof(MainWindow_GenerateRightTooltip));
            OnPropertyChanged(nameof(MainWindow_OpenSettings));
            OnPropertyChanged(nameof(MainWindow_OpenShortcuts));
            OnPropertyChanged(nameof(MainWindow_KeyboardShortcuts));
            OnPropertyChanged(nameof(MainWindow_ShortcutsOverlayTitle));
            OnPropertyChanged(nameof(MainWindow_ShortcutsGeneral));
            OnPropertyChanged(nameof(MainWindow_ShortcutsTabRelated));
            OnPropertyChanged(nameof(MainWindow_ShortcutsEditor));
            OnPropertyChanged(nameof(MainWindow_ShortcutsGenerate));
            OnPropertyChanged(nameof(MainWindow_ShortcutsNewTab));
            OnPropertyChanged(nameof(MainWindow_ShortcutsRestoreTab));
            OnPropertyChanged(nameof(MainWindow_ShortcutsDuplicateRight));
            OnPropertyChanged(nameof(MainWindow_ShortcutsDuplicateLeft));
            OnPropertyChanged(nameof(MainWindow_ShortcutsCloseTab));
            OnPropertyChanged(nameof(MainWindow_ShortcutsNextTab));
            OnPropertyChanged(nameof(MainWindow_ShortcutsPrevTab));
            OnPropertyChanged(nameof(MainWindow_ShortcutsIncreaseWeight));
            OnPropertyChanged(nameof(MainWindow_ShortcutsDecreaseWeight));
            OnPropertyChanged(nameof(MainWindow_ShortcutsInvertWeight));
            OnPropertyChanged(nameof(MainWindow_ShortcutsDeleteTag));
            OnPropertyChanged(nameof(MainWindow_ShortcutsShowSearch));
            OnPropertyChanged(nameof(MainWindow_ShortcutsHideSearch));
            OnPropertyChanged(nameof(MainWindow_ShortcutsCloseOverlay));
            OnPropertyChanged(nameof(MainWindow_BulkImport));
            OnPropertyChanged(nameof(MainWindow_MemoryUsage));
            
            // GroupBox Headers
            OnPropertyChanged(nameof(MainWindow_PromptBulkReplace));
            OnPropertyChanged(nameof(MainWindow_StepsBulkChange));
            OnPropertyChanged(nameof(MainWindow_SeedBulkChange));
            OnPropertyChanged(nameof(MainWindow_ResolutionBulkChange));
            OnPropertyChanged(nameof(MainWindow_ResolutionBulkChangeDescription));
            OnPropertyChanged(nameof(MainWindow_BulkGeneration));
            OnPropertyChanged(nameof(MainWindow_BatchOperationDescription));
            
            // エラーメッセージ用プロパティの変更通知
            OnPropertyChanged(nameof(Error_Title));
            OnPropertyChanged(nameof(Error_Information));
            OnPropertyChanged(nameof(Error_DynamicPromptsInit));
            OnPropertyChanged(nameof(Error_NoTabSelected));
            OnPropertyChanged(nameof(Error_ImageNotFound));
            OnPropertyChanged(nameof(Error_MetadataNotReadable));
            OnPropertyChanged(nameof(Error_SeedNotReadable));
            OnPropertyChanged(nameof(Error_NoParentTabSelected));
            OnPropertyChanged(nameof(Error_NoChildTabsToExecute));
            OnPropertyChanged(nameof(Error_NoCurrentTabSelected));
            OnPropertyChanged(nameof(Error_CurrentTabNotFound));
            OnPropertyChanged(nameof(Error_NoChildTabsToProcess));
            OnPropertyChanged(nameof(Error_NoChildTabsToProcessInfo));
            OnPropertyChanged(nameof(Error_ResolutionAlreadyExists));
            OnPropertyChanged(nameof(Error_DuplicateError));
            OnPropertyChanged(nameof(Error_InvalidDimensions));
            OnPropertyChanged(nameof(Error_InputError));
            
            // 成功メッセージ用プロパティの変更通知
            OnPropertyChanged(nameof(Success_LResolutionizeComplete));
            OnPropertyChanged(nameof(Success_MResolutionizeComplete));

            // Generation Time Messages
            OnPropertyChanged(nameof(GenerationTime_Label));
        }

        // WPFバインディング用のプロパティ
        public string Settings_Title => GetString("Settings_Title");
        public string Settings_Language => GetString("Settings_Language");
        public string Settings_Language_English => GetString("Settings_Language_English");
        public string Settings_Language_Japanese => GetString("Settings_Language_Japanese");
        public string Settings_EndpointLabel => GetString("Settings_EndpointLabel");
        public string Settings_LoraDirectoryLabel => GetString("Settings_LoraDirectoryLabel");
        public string Settings_StableDiffusionDirectoryLabel => GetString("Settings_StableDiffusionDirectoryLabel");
        public string Settings_AutoCompleteTagFileLabel => GetString("Settings_AutoCompleteTagFileLabel");
        public string Settings_CheckButton => GetString("Settings_CheckButton");
        public string Settings_CheckingButton => GetString("Settings_CheckingButton");
        public string Settings_BrowseButton => GetString("Settings_BrowseButton");
        public string Settings_SaveButton => GetString("Settings_SaveButton");
        public string Settings_CancelButton => GetString("Settings_CancelButton");
        public string Settings_ConnectionSuccess => GetString("Settings_ConnectionSuccess");
        public string Settings_PingFailed => GetString("Settings_PingFailed");
        public string Settings_ApiFailed => GetString("Settings_ApiFailed");
        public string Settings_DirectoryError => GetString("Settings_DirectoryError");
        public string Settings_Error => GetString("Settings_Error");
        public string Settings_BrowseLoraDialog => GetString("Settings_BrowseLoraDialog");
        public string Settings_BrowseStableDiffusionDialog => GetString("Settings_BrowseStableDiffusionDialog");
        
        // Batch Settings
        public string Settings_DefaultBatchSize => GetString("Settings_DefaultBatchSize");
        public string Settings_DefaultBatchCount => GetString("Settings_DefaultBatchCount");
        public string Settings_BatchSettingsDescription => GetString("Settings_BatchSettingsDescription");
        public string Settings_InvalidBatchSize => GetString("Settings_InvalidBatchSize");
        public string Settings_InvalidBatchCount => GetString("Settings_InvalidBatchCount");
        
        // Image Limit Settings
        public string Settings_DefaultImageLimit => GetString("Settings_DefaultImageLimit");
        public string Settings_DefaultImageLimitDescription => GetString("Settings_DefaultImageLimitDescription");
        public string Settings_InvalidImageLimit => GetString("Settings_InvalidImageLimit");

        // Resolution Presets Settings
        public string Settings_ResolutionPresets => GetString("Settings_ResolutionPresets");
        public string Settings_ResolutionPresetsPortrait => GetString("Settings_ResolutionPresetsPortrait");
        public string Settings_ResolutionPresetsLandscape => GetString("Settings_ResolutionPresetsLandscape");
        public string Settings_ResolutionPresetsSquare => GetString("Settings_ResolutionPresetsSquare");
        public string Settings_ResolutionPresetsFormat => GetString("Settings_ResolutionPresetsFormat");

        // Generation Time Messages
        public string GenerationTime_Label => GetString("GenerationTime_Label");

        // MainWindow用のプロパティ
        public string MainWindow_Restart => GetString("MainWindow_Restart");
        public string MainWindow_RestartTooltip => GetString("MainWindow_RestartTooltip");
        public string MainWindow_Generating => GetString("MainWindow_Generating");
        public string MainWindow_CloseTab => GetString("MainWindow_CloseTab");
        public string MainWindow_DeleteTab => GetString("MainWindow_DeleteTab");
        public string MainWindow_AddTab => GetString("MainWindow_AddTab");
        public string MainWindow_AddNewTab => GetString("MainWindow_AddNewTab");
        public string MainWindow_Duplicate => GetString("MainWindow_Duplicate");
        public string MainWindow_ExportImages => GetString("MainWindow_ExportImages");
        public string MainWindow_DuplicateRight => GetString("MainWindow_DuplicateRight");
        public string MainWindow_DuplicateLeft => GetString("MainWindow_DuplicateLeft");
        public string MainWindow_Interrupt => GetString("MainWindow_Interrupt");
        public string MainWindow_Loading => GetString("MainWindow_Loading");
        public string MainWindow_Checkpoint => GetString("MainWindow_Checkpoint");
        public string MainWindow_RefreshCheckpoints => GetString("MainWindow_RefreshCheckpoints");
        public string MainWindow_SamplingMethod => GetString("MainWindow_SamplingMethod");
        public string MainWindow_ScheduleType => GetString("MainWindow_ScheduleType");
        public string MainWindow_SamplingSteps => GetString("MainWindow_SamplingSteps");
        public string MainWindow_Width => GetString("MainWindow_Width");
        public string MainWindow_Height => GetString("MainWindow_Height");
        public string MainWindow_DimensionPresets => GetString("MainWindow_DimensionPresets");
        public string MainWindow_Portrait => GetString("MainWindow_Portrait");
        public string MainWindow_Landscape => GetString("MainWindow_Landscape");
        public string MainWindow_Square => GetString("MainWindow_Square");
        public string MainWindow_PortraitM => GetString("MainWindow_PortraitM");
        public string MainWindow_LandscapeM => GetString("MainWindow_LandscapeM");
        public string MainWindow_SquareM => GetString("MainWindow_SquareM");
        public string MainWindow_PortraitL => GetString("MainWindow_PortraitL");
        public string MainWindow_LandscapeL => GetString("MainWindow_LandscapeL");
        public string MainWindow_SquareL => GetString("MainWindow_SquareL");
        public string MainWindow_SwapDimensions => GetString("MainWindow_SwapDimensions");
        public string MainWindow_BatchCount => GetString("MainWindow_BatchCount");
        public string MainWindow_BatchSize => GetString("MainWindow_BatchSize");
        public string MainWindow_CFGScale => GetString("MainWindow_CFGScale");
        public string MainWindow_Seed => GetString("MainWindow_Seed");
        public string MainWindow_SeedPlus1 => GetString("MainWindow_SeedPlus1");
        public string MainWindow_SeedMinus1 => GetString("MainWindow_SeedMinus1");
        public string MainWindow_RandomSeedTooltip => GetString("MainWindow_RandomSeedTooltip");
        public string MainWindow_Recycle => GetString("MainWindow_Recycle");
        public string MainWindow_CombinatorialGeneration => GetString("MainWindow_CombinatorialGeneration");
        public string MainWindow_CombinatorialGenerationTooltip => GetString("MainWindow_CombinatorialGenerationTooltip");
        public string MainWindow_DynamicPromptNotInstalled => GetString("MainWindow_DynamicPromptNotInstalled");
        public string MainWindow_KohyaHiresFixNotInstalled => GetString("MainWindow_KohyaHiresFixNotInstalled");
        public string MainWindow_KohyaHiresFix => GetString("MainWindow_KohyaHiresFix");
        public string MainWindow_KohyaHiresFixTooltip => GetString("MainWindow_KohyaHiresFixTooltip");
        public string MainWindow_BlockNumber => GetString("MainWindow_BlockNumber");
        public string MainWindow_DownscaleFactor => GetString("MainWindow_DownscaleFactor");
        public string MainWindow_HiresFix => GetString("MainWindow_HiresFix");
        public string MainWindow_Upscaler => GetString("MainWindow_Upscaler");
        public string MainWindow_HiresSteps => GetString("MainWindow_HiresSteps");
        public string MainWindow_DenoisingStrength => GetString("MainWindow_DenoisingStrength");
        public string MainWindow_UpscaleBy => GetString("MainWindow_UpscaleBy");
        public string MainWindow_RandomResolution => GetString("MainWindow_RandomResolution");
        public string MainWindow_ModelType => GetString("MainWindow_ModelType");
        public string MainWindow_ResolutionWeightMode => GetString("MainWindow_ResolutionWeightMode");
        public string MainWindow_EqualWeights => GetString("MainWindow_EqualWeights");
        public string MainWindow_FavorSmaller => GetString("MainWindow_FavorSmaller");
        public string MainWindow_FavorLarger => GetString("MainWindow_FavorLarger");
        public string MainWindow_MinimumDimension => GetString("MainWindow_MinimumDimension");
        public string MainWindow_MaximumDimension => GetString("MainWindow_MaximumDimension");
        public string MainWindow_CurrentResolutions => GetString("MainWindow_CurrentResolutions");
        public string MainWindow_CurrentResolutionsTooltip => GetString("MainWindow_CurrentResolutionsTooltip");
        public string MainWindow_NewWidth => GetString("MainWindow_NewWidth");
        public string MainWindow_NewHeight => GetString("MainWindow_NewHeight");
        public string MainWindow_AddResolution => GetString("MainWindow_AddResolution");
        public string MainWindow_AddResolutionTooltip => GetString("MainWindow_AddResolutionTooltip");
        public string MainWindow_Replace => GetString("MainWindow_Replace");
        public string MainWindow_ReplaceTarget => GetString("MainWindow_ReplaceTarget");
        public string MainWindow_ReplaceTargetTooltip => GetString("MainWindow_ReplaceTargetTooltip");
        public string MainWindow_ReplaceWith => GetString("MainWindow_ReplaceWith");
        public string MainWindow_ReplaceWithTooltip => GetString("MainWindow_ReplaceWithTooltip");
        public string MainWindow_ExecuteReplace => GetString("MainWindow_ExecuteReplace");
        public string MainWindow_ExecuteReplaceTooltip => GetString("MainWindow_ExecuteReplaceTooltip");
        public string MainWindow_BulkStepsTooltip => GetString("MainWindow_BulkStepsTooltip");
        public string MainWindow_ChangeSteps => GetString("MainWindow_ChangeSteps");
        public string MainWindow_ChangeStepsTooltip => GetString("MainWindow_ChangeStepsTooltip");
        public string MainWindow_MResolutionize => GetString("MainWindow_MResolutionize");
        public string MainWindow_MResolutionizeTooltip => GetString("MainWindow_MResolutionizeTooltip");
        public string MainWindow_LResolutionize => GetString("MainWindow_LResolutionize");
        public string MainWindow_LResolutionizeTooltip => GetString("MainWindow_LResolutionizeTooltip");
        public string MainWindow_EnableHiresFix => GetString("MainWindow_EnableHiresFix");
        public string MainWindow_EnableHiresFixTooltip => GetString("MainWindow_EnableHiresFixTooltip");
        public string MainWindow_NormalResolutionize => GetString("MainWindow_NormalResolutionize");
        public string MainWindow_NormalResolutionizeTooltip => GetString("MainWindow_NormalResolutionizeTooltip");
        public string MainWindow_RandomSeed => GetString("MainWindow_RandomSeed");
        public string MainWindow_GenerateAll => GetString("MainWindow_GenerateAll");
        public string MainWindow_GenerateAllTooltip => GetString("MainWindow_GenerateAllTooltip");
        public string MainWindow_GenerateLeft => GetString("MainWindow_GenerateLeft");
        public string MainWindow_GenerateLeftTooltip => GetString("MainWindow_GenerateLeftTooltip");
        public string MainWindow_GenerateRight => GetString("MainWindow_GenerateRight");
        public string MainWindow_GenerateRightTooltip => GetString("MainWindow_GenerateRightTooltip");
        public string MainWindow_OpenSettings => GetString("MainWindow_OpenSettings");
        public string MainWindow_OpenShortcuts => GetString("MainWindow_OpenShortcuts");
        public string MainWindow_KeyboardShortcuts => GetString("MainWindow_KeyboardShortcuts");
        public string MainWindow_ShortcutsOverlayTitle => GetString("MainWindow_ShortcutsOverlayTitle");
        public string MainWindow_ShortcutsGeneral => GetString("MainWindow_ShortcutsGeneral");
        public string MainWindow_ShortcutsTabRelated => GetString("MainWindow_ShortcutsTabRelated");
        public string MainWindow_ShortcutsEditor => GetString("MainWindow_ShortcutsEditor");
        public string MainWindow_ShortcutsGenerate => GetString("MainWindow_ShortcutsGenerate");
        public string MainWindow_ShortcutsNewTab => GetString("MainWindow_ShortcutsNewTab");
        public string MainWindow_ShortcutsRestoreTab => GetString("MainWindow_ShortcutsRestoreTab");
        public string MainWindow_ShortcutsDuplicateRight => GetString("MainWindow_ShortcutsDuplicateRight");
        public string MainWindow_ShortcutsDuplicateLeft => GetString("MainWindow_ShortcutsDuplicateLeft");
        public string MainWindow_ShortcutsCloseTab => GetString("MainWindow_ShortcutsCloseTab");
        public string MainWindow_ShortcutsNextTab => GetString("MainWindow_ShortcutsNextTab");
        public string MainWindow_ShortcutsPrevTab => GetString("MainWindow_ShortcutsPrevTab");
        public string MainWindow_ShortcutsIncreaseWeight => GetString("MainWindow_ShortcutsIncreaseWeight");
        public string MainWindow_ShortcutsDecreaseWeight => GetString("MainWindow_ShortcutsDecreaseWeight");
        public string MainWindow_ShortcutsInvertWeight => GetString("MainWindow_ShortcutsInvertWeight");
        public string MainWindow_ShortcutsDeleteTag => GetString("MainWindow_ShortcutsDeleteTag");
        public string MainWindow_ShortcutsShowSearch => GetString("MainWindow_ShortcutsShowSearch");
        public string MainWindow_ShortcutsHideSearch => GetString("MainWindow_ShortcutsHideSearch");
        public string MainWindow_ShortcutsCloseOverlay => GetString("MainWindow_ShortcutsCloseOverlay");
        public string MainWindow_BulkImport => GetString("MainWindow_BulkImport");
        public string MainWindow_MemoryUsage => GetString("MainWindow_MemoryUsage");

        // GroupBox Headers
        public string MainWindow_PromptBulkReplace => GetString("MainWindow_PromptBulkReplace");
        public string MainWindow_StepsBulkChange => GetString("MainWindow_StepsBulkChange");
        public string MainWindow_SeedBulkChange => GetString("MainWindow_SeedBulkChange");
        public string MainWindow_ResolutionBulkChange => GetString("MainWindow_ResolutionBulkChange");
        public string MainWindow_ResolutionBulkChangeDescription => GetString("MainWindow_ResolutionBulkChangeDescription");
        public string MainWindow_BulkGeneration => GetString("MainWindow_BulkGeneration");
        public string MainWindow_BatchOperationDescription => GetString("MainWindow_BatchOperationDescription");

        // エラーメッセージ用のプロパティ
        public string Error_Title => GetString("Error_Title");
        public string Error_Information => GetString("Error_Information");
        public string Error_DynamicPromptsInit => GetString("Error_DynamicPromptsInit");
        public string Error_NoTabSelected => GetString("Error_NoTabSelected");
        public string Error_ImageNotFound => GetString("Error_ImageNotFound");
        public string Error_MetadataNotReadable => GetString("Error_MetadataNotReadable");
        public string Error_SeedNotReadable => GetString("Error_SeedNotReadable");
        public string Error_NoParentTabSelected => GetString("Error_NoParentTabSelected");
        public string Error_NoChildTabsToExecute => GetString("Error_NoChildTabsToExecute");
        public string Error_NoCurrentTabSelected => GetString("Error_NoCurrentTabSelected");
        public string Error_CurrentTabNotFound => GetString("Error_CurrentTabNotFound");
        public string Error_NoChildTabsToProcess => GetString("Error_NoChildTabsToProcess");
        public string Error_NoChildTabsToProcessInfo => GetString("Error_NoChildTabsToProcessInfo");
        public string Error_ResolutionAlreadyExists => GetString("Error_ResolutionAlreadyExists");
        public string Error_DuplicateError => GetString("Error_DuplicateError");
        public string Error_InvalidDimensions => GetString("Error_InvalidDimensions");
        public string Error_InputError => GetString("Error_InputError");

        // 成功メッセージ用のプロパティ
        public string Success_LResolutionizeComplete => GetString("Success_LResolutionizeComplete");
        public string Success_MResolutionizeComplete => GetString("Success_MResolutionizeComplete");

        public string MainWindow_LandscapeLTooltip => GetString("MainWindow_LandscapeLTooltip");
        public string MainWindow_PortraitTooltip => GetString("MainWindow_PortraitTooltip");
        public string MainWindow_LandscapeTooltip => GetString("MainWindow_LandscapeTooltip");
        public string MainWindow_SquareTooltip => GetString("MainWindow_SquareTooltip");
        public string MainWindow_PortraitMTooltip => GetString("MainWindow_PortraitMTooltip");
        public string MainWindow_LandscapeMTooltip => GetString("MainWindow_LandscapeMTooltip");
        public string MainWindow_SquareMTooltip => GetString("MainWindow_SquareMTooltip");
        public string MainWindow_PortraitLTooltip => GetString("MainWindow_PortraitLTooltip");
        public string MainWindow_SquareLTooltip => GetString("MainWindow_SquareLTooltip");
        public string MainWindow_SwapDimensionsTooltip => GetString("MainWindow_SwapDimensionsTooltip");
        public string MainWindow_SeedPlus1Tooltip => GetString("MainWindow_SeedPlus1Tooltip");
        public string MainWindow_SeedMinus1Tooltip => GetString("MainWindow_SeedMinus1Tooltip");
        public string MainWindow_RecycleTooltip => GetString("MainWindow_RecycleTooltip");
    }
} 