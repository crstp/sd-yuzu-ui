using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using static SD.Yuzu.TabItemViewModel;
using System.Text.Json.Serialization;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace SD.Yuzu
{
    // 閉じたタブの復元情報を保存するクラス（シリアライゼーション対応）
    public class ClosedTabInfo
    {
        // 外側タブ識別用の軽量情報（GUIDベース）
        [JsonPropertyName("outerTabGuid")]
        public Guid? OuterTabGuid { get; set; } // 外側タブの一意識別子（GUID）
        
        // 後方互換性のため（古い形式サポート）
        [JsonPropertyName("outerTabId")]
        public string? OuterTabId { get; set; } // 外側タブの旧形式ID（廃止予定）
        
        [JsonPropertyName("outerTabTitle")]
        public string? OuterTabTitle { get; set; } // 外側タブのタイトル（デバッグ用）
        
        // 外側タブ復元用（外側タブが閉じられた場合のみ使用）
        [JsonPropertyName("outerTabData")]
        public TabItemDto? OuterTabData { get; set; }
        
        // 内側タブ復元用
        [JsonPropertyName("closedInnerTab")]
        public TabItemDto? ClosedInnerTab { get; set; }
        
        [JsonPropertyName("originalIndex")]
        public int OriginalIndex { get; set; }
        
        [JsonPropertyName("isOuterTab")]
        public bool IsOuterTab { get; set; } // 外側タブかどうかを示すフラグ
        
        [JsonPropertyName("outerTabOriginalIndex")]
        public int OuterTabOriginalIndex { get; set; } // 外側タブの元の位置
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now; // 削除された時刻
        
        // 実行時にのみ使用するメモリ内データ（シリアライゼーション対象外）
        [JsonIgnore]
        public TabItemViewModel? OuterTabInstance { get; set; }
        
        [JsonIgnore]
        public TabItemViewModel? ClosedInnerTabInstance { get; set; }
        
        /// <summary>
        /// 外側タブ削除用のClosedTabInfoを作成
        /// </summary>
        public static ClosedTabInfo CreateForOuterTab(TabItemViewModel outerTab, int originalIndex)
        {
            return new ClosedTabInfo
            {
                OuterTabGuid = outerTab.Guid, // GUIDを使用
                OuterTabTitle = outerTab.Title,
                OuterTabData = outerTab.ToDto(), // 外側タブ全体のデータを保存
                ClosedInnerTab = null,
                OriginalIndex = -1,
                IsOuterTab = true,
                OuterTabOriginalIndex = originalIndex,
                Timestamp = DateTime.Now,
                OuterTabInstance = outerTab,
                ClosedInnerTabInstance = null
            };
        }
        
        /// <summary>
        /// 内側タブ削除用のClosedTabInfoを作成
        /// </summary>
        public static ClosedTabInfo CreateForInnerTab(TabItemViewModel outerTab, TabItemViewModel innerTab, int originalIndex)
        {
            return new ClosedTabInfo
            {
                OuterTabGuid = outerTab.Guid, // 親の外側タブのGUIDのみ
                OuterTabTitle = outerTab.Title,
                OuterTabData = null, // 内側タブUndoでは外側タブデータは保存しない
                ClosedInnerTab = innerTab.ToDto(),
                OriginalIndex = originalIndex,
                IsOuterTab = false,
                OuterTabOriginalIndex = -1,
                Timestamp = DateTime.Now,
                OuterTabInstance = outerTab,
                ClosedInnerTabInstance = innerTab
            };
        }
        
        /// <summary>
        /// TabItemViewModelインスタンスを復元
        /// </summary>
        public void RestoreInstances()
        {
            if (IsOuterTab && OuterTabData != null)
            {
                // 外側タブの場合：完全復元
                OuterTabInstance = TabItemViewModel.FromDto(OuterTabData);
            }
            
            if (ClosedInnerTab != null)
            {
                // 内側タブのインスタンス復元
                ClosedInnerTabInstance = TabItemViewModel.FromDto(ClosedInnerTab);
            }
        }
    }

    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _disposed = false;
        private TabItemViewModel? _selectedTab = null;
        public ObservableCollection<TabItemViewModel> Tabs { get; } = new();
        public TabItemViewModel? SelectedTab
        {
            get => _selectedTab;
            set 
            { 
                _selectedTab = value; 
                OnPropertyChanged(nameof(SelectedTab));
                
                // 外側タブ切り替え時にテキストボックスのUndo履歴をクリア
                ClearTextBoxUndoHistoryOnOuterTabChange();
            }
        }

        public RelayCommand GenerateCommand { get; set; } = null!;
        public RelayCommand InterruptCommand { get; set; } = null!;
        public RelayCommand RemoveTabCommand { get; set; } = null!;
        public RelayCommand SeedUpCommand { get; set; } = null!;
        public RelayCommand SeedDownCommand { get; set; } = null!;
        public RelayCommand RefreshCheckpointCommand { get; set; } = null!;
        public RelayCommand MoveToPreviousInnerTabCommand { get; set; } = null!;
        public RelayCommand MoveToNextInnerTabCommand { get; set; } = null!;
        public RelayCommand MoveToPreviousOuterTabCommand { get; set; } = null!;
        public RelayCommand MoveToNextOuterTabCommand { get; set; } = null!;

        private int _outerTabCount = 0;
        
        // 最後に実行されたプロンプトと設定を保存するプロパティ
        public static string LastPrompt { get; set; } = "";
        public static string LastNegativePrompt { get; set; } = "";
        public static int LastWidth { get; set; } = 512;
        public static int LastHeight { get; set; } = 512;
        public static int LastSteps { get; set; } = 20;
        public static double LastCfgScale { get; set; } = 7.0;
        public static double LastDenoisingStrength { get; set; } = 0.7;
        public static string LastSelectedSamplingMethod { get; set; } = "Euler a";
        public static string LastSelectedScheduleType { get; set; } = "Automatic";
        public static long LastSeed { get; set; } = -1;
        public static long LastSubseed { get; set; } = -1;
        public static bool LastEnableHiresFix { get; set; } = false;
        public static string LastSelectedUpscaler { get; set; } = "Latent (nearest-exact)";
        public static int LastHiresSteps { get; set; } = 6;
        public static double LastHiresUpscaleBy { get; set; } = 1.5;
        public static int LastHiresResizeWidth { get; set; } = 1024;
        public static int LastHiresResizeHeight { get; set; } = 1024;
        public static bool LastCombinatorialGeneration { get; set; } = false;

        // Kohya hires.fix関連のLastプロパティ
        public static bool LastEnableKohyaHiresFix { get; set; } = false;
        public static int LastKohyaBlockNumber { get; set; } = 3;
        public static double LastKohyaDownscaleFactor { get; set; } = 1.75;
        public static bool LastKohyaAlwaysEnableCondition { get; set; } = false;
        public static int LastKohyaConditionShortSide { get; set; } = 1280;
        public static int LastKohyaConditionLongSide { get; set; } = 1420;

        // Random Resolution関連のLastプロパティ
        public static bool LastEnableRandomResolution { get; set; } = false;

        // Random Resolution関連のプロパティ（グローバル設定）
        public string RandomResolutionModelType
        {
            get => MainWindow.GlobalRandomResolutionSettings.ModelType;
            set 
            { 
                MainWindow.GlobalRandomResolutionSettings.ModelType = value; 
                OnPropertyChanged(nameof(RandomResolutionModelType));
                OnPropertyChanged(nameof(IsRandomResolutionModelSD15));
                OnPropertyChanged(nameof(IsRandomResolutionModelSDXL));
            }
        }

        // Dynamic Prompts利用可能性プロパティ
        private bool _isDynamicPromptAvailable = false;
        public bool IsDynamicPromptAvailable
        {
            get => _isDynamicPromptAvailable;
            set 
            { 
                _isDynamicPromptAvailable = value; 
                OnPropertyChanged(nameof(IsDynamicPromptAvailable));
            }
        }

        // Kohya hires.fix利用可能性プロパティ
        private bool _isKohyaHiresFixAvailable = false;
        public bool IsKohyaHiresFixAvailable
        {
            get => _isKohyaHiresFixAvailable;
            set 
            { 
                _isKohyaHiresFixAvailable = value; 
                OnPropertyChanged(nameof(IsKohyaHiresFixAvailable));
            }
        }

        // Random resolution利用可能性プロパティ
        private bool _isRandomResolutionAvailable = false;
        public bool IsRandomResolutionAvailable
        {
            get => _isRandomResolutionAvailable;
            set 
            { 
                _isRandomResolutionAvailable = value; 
                OnPropertyChanged(nameof(IsRandomResolutionAvailable));
            }
        }

        public bool IsRandomResolutionModelSD15
        {
            get => RandomResolutionModelType == "SD1.5";
            set { if (value) RandomResolutionModelType = "SD1.5"; }
        }

        public bool IsRandomResolutionModelSDXL
        {
            get => RandomResolutionModelType == "SDXL";
            set { if (value) RandomResolutionModelType = "SDXL"; }
        }

        public string RandomResolutionWeightMode
        {
            get => MainWindow.GlobalRandomResolutionSettings.WeightMode;
            set 
            { 
                MainWindow.GlobalRandomResolutionSettings.WeightMode = value; 
                OnPropertyChanged(nameof(RandomResolutionWeightMode));
                OnPropertyChanged(nameof(IsRandomResolutionWeightModeEqual));
                OnPropertyChanged(nameof(IsRandomResolutionWeightModeSmaller));
                OnPropertyChanged(nameof(IsRandomResolutionWeightModeLarger));
            }
        }

        public bool IsRandomResolutionWeightModeEqual
        {
            get => RandomResolutionWeightMode == "Equal Weights";
            set { if (value) RandomResolutionWeightMode = "Equal Weights"; }
        }

        public bool IsRandomResolutionWeightModeSmaller
        {
            get => RandomResolutionWeightMode == "Favor Smaller";
            set { if (value) RandomResolutionWeightMode = "Favor Smaller"; }
        }

        public bool IsRandomResolutionWeightModeLarger
        {
            get => RandomResolutionWeightMode == "Favor Larger";
            set { if (value) RandomResolutionWeightMode = "Favor Larger"; }
        }

        public int RandomResolutionMinDim
        {
            get => MainWindow.GlobalRandomResolutionSettings.MinDim;
            set 
            { 
                MainWindow.GlobalRandomResolutionSettings.MinDim = value; 
                OnPropertyChanged(nameof(RandomResolutionMinDim));
            }
        }

        public int RandomResolutionMaxDim
        {
            get => MainWindow.GlobalRandomResolutionSettings.MaxDim;
            set 
            { 
                MainWindow.GlobalRandomResolutionSettings.MaxDim = value; 
                OnPropertyChanged(nameof(RandomResolutionMaxDim));
            }
        }

        private string _rawRandomResolutionText = "";
        
        public string RandomResolutionCurrentResolutions
        {
            get 
            { 
                // 初期表示時または外部からの更新時は、フォーマットされた文字列を返す
                if (string.IsNullOrEmpty(_rawRandomResolutionText))
                {
                    _rawRandomResolutionText = string.Join(";", MainWindow.GlobalRandomResolutionSettings.CurrentResolutions.Select(r => $"{r.Width},{r.Height}")) + (MainWindow.GlobalRandomResolutionSettings.CurrentResolutions.Count > 0 ? ";" : "");
                }
                return _rawRandomResolutionText;
            }
            set 
            { 
                try
                {
                    Debug.WriteLine($"RandomResolutionCurrentResolutions.set: 入力値='{value}'");
                    
                    // 編集中の文字列を保存
                    _rawRandomResolutionText = value ?? "";
                    
                    // 空の場合は空のリストを設定
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        MainWindow.GlobalRandomResolutionSettings.CurrentResolutions = new List<ResolutionItem>();
                        Debug.WriteLine("空の値を設定しました");
                        OnPropertyChanged(nameof(RandomResolutionCurrentResolutions));
                        return;
                    }
                    
                    var resolutions = new List<ResolutionItem>();
                    
                    // セミコロンで分割して各解像度を処理
                    var resolutionStrings = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    Debug.WriteLine($"解像度文字列の数: {resolutionStrings.Length}");
                    
                    foreach (var resStr in resolutionStrings)
                    {
                        Debug.WriteLine($"処理中の解像度文字列: '{resStr.Trim()}'");
                        var parts = resStr.Trim().Split(',');
                        
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int width) && int.TryParse(parts[1].Trim(), out int height))
                        {
                            resolutions.Add(new ResolutionItem(width, height));
                            Debug.WriteLine($"有効な解像度を追加: {width}x{height}");
                        }
                        else
                        {
                            Debug.WriteLine($"無効な解像度文字列をスキップ: '{resStr.Trim()}' (パーツ数: {parts.Length})");
                        }
                    }
                    
                    // 有効な解像度のみを設定に反映
                    MainWindow.GlobalRandomResolutionSettings.CurrentResolutions = resolutions;
                    Debug.WriteLine($"解像度リストを更新: {resolutions.Count}個の解像度");
                    OnPropertyChanged(nameof(RandomResolutionCurrentResolutions));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Random Resolution解像度解析エラー: {ex.Message}");
                    Debug.WriteLine($"スタックトレース: {ex.StackTrace}");
                }
            }
        }

        private int _randomResolutionNewWidth = 0;
        public int RandomResolutionNewWidth
        {
            get => _randomResolutionNewWidth;
            set { _randomResolutionNewWidth = value; OnPropertyChanged(nameof(RandomResolutionNewWidth)); }
        }

        private int _randomResolutionNewHeight = 0;
        public int RandomResolutionNewHeight
        {
            get => _randomResolutionNewHeight;
            set { _randomResolutionNewHeight = value; OnPropertyChanged(nameof(RandomResolutionNewHeight)); }
        }

        // 閉じたタブの復元用スタック
        private readonly Stack<ClosedTabInfo> _closedTabsStack = new Stack<ClosedTabInfo>();

        // Bulk import用のプロパティ
        private bool _isBulkImporting = false;
        public bool IsBulkImporting
        {
            get => _isBulkImporting;
            set { _isBulkImporting = value; OnPropertyChanged(nameof(IsBulkImporting)); }
        }

        private double _bulkImportProgress = 0.0;
        public double BulkImportProgress
        {
            get => _bulkImportProgress;
            set { _bulkImportProgress = value; OnPropertyChanged(nameof(BulkImportProgress)); }
        }

        private string _bulkImportStatus = "";
        public string BulkImportStatus
        {
            get => _bulkImportStatus;
            set { _bulkImportStatus = value; OnPropertyChanged(nameof(BulkImportStatus)); }
        }

        // Checkpoint関連のプロパティ（グローバル設定）
        private readonly ObservableCollection<string> _checkpointTitles = new();
        public ObservableCollection<string> CheckpointTitles => _checkpointTitles;
        
        private string _selectedCheckpoint = "";
        public string SelectedCheckpoint
        {
            get => _selectedCheckpoint;
            set 
            { 
                if (_selectedCheckpoint != value)
                {
                    _selectedCheckpoint = value;
                    OnPropertyChanged(nameof(SelectedCheckpoint));
                    
                    // CheckpointManagerに変更を通知（非同期で実行）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await CheckpointManager.Instance.SetCurrentCheckpointAsync(value);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Checkpoint変更エラー: {ex.Message}");
                        }
                    });
                }
            }
        }

        public MainViewModel()
        {
            // 最低1つの外側タブを作成
            AddOuterTab();
            
            GenerateCommand = new RelayCommand(_ => { }, _ => true);
            InterruptCommand = new RelayCommand(_ => { }, _ => true);
            RemoveTabCommand = new RelayCommand(parameter =>
            {
                if (parameter is TabItemViewModel tab)
                {
                    RemoveOuterTab(tab);
                }
            });
            SeedUpCommand = new RelayCommand(_ => { });
            SeedDownCommand = new RelayCommand(_ => { });
            
            // Checkpoint更新コマンドの初期化
            RefreshCheckpointCommand = new RelayCommand(_ =>
            {
                _ = Task.Run(async () => await RefreshCheckpointsAsync());
            });
            
            // 内側タブ移動コマンドの初期化
            MoveToPreviousInnerTabCommand = new RelayCommand(_ => MoveToPreviousInnerTab());
            MoveToNextInnerTabCommand = new RelayCommand(_ => MoveToNextInnerTab());
            
            // 外側タブ移動コマンドの初期化
            MoveToPreviousOuterTabCommand = new RelayCommand(_ => MoveToPreviousOuterTab());
            MoveToNextOuterTabCommand = new RelayCommand(_ => MoveToNextOuterTab());
            
            // Checkpoint初期化を非同期で実行
            _ = Task.Run(async () => await InitializeCheckpointsAsync());
        }

        /// <summary>
        /// Checkpointリストを初期化
        /// </summary>
        public async Task InitializeCheckpointsAsync()
        {
            try
            {
                Debug.WriteLine("Checkpointリストの初期化を開始");
                
                // CheckpointManagerからCheckpointリストと現在の選択を取得
                var checkpoints = await CheckpointManager.Instance.GetCheckpointTitlesAsync();
                var currentCheckpoint = await CheckpointManager.Instance.GetCurrentCheckpointAsync();
                
                // UIスレッドでコレクションを更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Checkpointリストを更新
                    _checkpointTitles.Clear();
                    foreach (var checkpoint in checkpoints)
                    {
                        _checkpointTitles.Add(checkpoint);
                    }
                    
                    // 現在の選択を設定（APIを呼び出さないよう直接フィールドを更新）
                    _selectedCheckpoint = currentCheckpoint;
                    OnPropertyChanged(nameof(SelectedCheckpoint));
                    
                    Debug.WriteLine($"Checkpoint初期化完了: {_checkpointTitles.Count}個のCheckpointを取得");
                    Debug.WriteLine($"現在選択されているCheckpoint: {currentCheckpoint}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Checkpoint初期化エラー: {ex.Message}");
                
                // エラー時もUIスレッドでフォールバック処理
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _checkpointTitles.Clear();
                    _checkpointTitles.Add("Default Model");
                    _selectedCheckpoint = "Default Model";
                    OnPropertyChanged(nameof(SelectedCheckpoint));
                });
            }
        }

        /// <summary>
        /// Checkpointリストを強制的に再取得
        /// </summary>
        public async Task RefreshCheckpointsAsync()
        {
            try
            {
                Debug.WriteLine("Checkpointリストの再取得を開始");
                
                // CheckpointManagerで強制的に再取得
                await CheckpointManager.Instance.RefreshCheckpointsAsync();
                
                // 再取得されたリストを取得
                var checkpoints = await CheckpointManager.Instance.GetCheckpointTitlesAsync();
                var currentCheckpoint = await CheckpointManager.Instance.GetCurrentCheckpointAsync();
                
                // UIスレッドでコレクションを更新
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Checkpointリストを更新
                    _checkpointTitles.Clear();
                    foreach (var checkpoint in checkpoints)
                    {
                        _checkpointTitles.Add(checkpoint);
                    }
                    
                    // 現在の選択を設定（APIを呼び出さないよう直接フィールドを更新）
                    _selectedCheckpoint = currentCheckpoint;
                    OnPropertyChanged(nameof(SelectedCheckpoint));
                    
                    Debug.WriteLine($"Checkpoint再取得完了: {_checkpointTitles.Count}個のCheckpointを取得");
                    Debug.WriteLine($"現在選択されているCheckpoint: {currentCheckpoint}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Checkpoint再取得エラー: {ex.Message}");
                
                // エラー時はトーストメッセージまたはダイアログで通知
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to update checkpoint list.\n\nError: {ex.Message}",
                                  "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        public void AddOuterTab()
        {
            _outerTabCount++;
            var tab = new TabItemViewModel($"{_outerTabCount}");
            tab.AddInnerTab();
            tab.UpdateInnerTabTitles(); // この外側タブの内側タブのタイトルを更新
            Tabs.Add(tab);
            SelectedTab = tab;
        }

        public void RemoveOuterTab(TabItemViewModel? tab)
        {
            // 警告 CS8600, CS8604 を修正: tab が null の場合は処理しない
            if (tab == null)
            {
                return;
            }

            if (Tabs.Contains(tab))
            {
                var originalIndex = Tabs.IndexOf(tab);
                
                // 外側タブが1つしかない場合は削除しない
                if (Tabs.Count <= 1)
                    return;
                
                // 閉じた外側タブの情報をスタックに保存（新しい方式）
                _closedTabsStack.Push(ClosedTabInfo.CreateForOuterTab(tab, originalIndex));
                
                // 削除される外側タブに関連する内側タブの履歴をクリア（外側タブ自体の復元情報は保持）
                var tempStack = new Stack<ClosedTabInfo>();
                
                // スタックから該当する項目を除外して再構築
                while (_closedTabsStack.Count > 0)
                {
                    var item = _closedTabsStack.Pop();
                    if (item.IsOuterTab || item.OuterTabInstance != tab)
                    {
                        tempStack.Push(item);
                    }
                }
                
                // スタックを元に戻す
                while (tempStack.Count > 0)
                {
                    _closedTabsStack.Push(tempStack.Pop());
                }
                
                Tabs.Remove(tab);
                if (Tabs.Count > 0)
                {
                    // 削除したタブの位置に応じて次に選択するタブを決定
                    if (originalIndex < Tabs.Count)
                        SelectedTab = Tabs[originalIndex];
                    else
                        SelectedTab = Tabs[Tabs.Count - 1];
                }
                else
                    SelectedTab = null;
            }
            RemoveTabCommand?.RaiseCanExecuteChanged();
        }

        public void MoveOuterTab(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Tabs.Count || newIndex < 0 || newIndex >= Tabs.Count || oldIndex == newIndex)
                return;
            var tab = Tabs[oldIndex];
            Tabs.RemoveAt(oldIndex);
            Tabs.Insert(newIndex, tab);
            SelectedTab = tab;
        }

        /// <summary>
        /// 外側のタブを複製します
        /// </summary>
        /// <param name="originalTab">複製元のタブ</param>
        public void DuplicateOuterTab(TabItemViewModel originalTab)
        {
            if (originalTab == null || !Tabs.Contains(originalTab))
                return;

            // 新しいタブ番号を生成
            _outerTabCount++;
            var newTitle = $"{_outerTabCount}";
            
            // タブを複製
            var duplicatedTab = originalTab.Duplicate(newTitle);
            
            // 複製されたタブの内側タブのタイトルを更新
            duplicatedTab.UpdateInnerTabTitles();
            
            // 複製されたタブとその内側タブのリストを初期化（サンプラー、スケジューラー、アップスケーラーの表示のため）
            duplicatedTab.InitializeListsAfterRestore();
            foreach (var innerTab in duplicatedTab.InnerTabs)
            {
                innerTab.InitializeListsAfterRestore();
            }
            
            // 元のタブの右側に挿入
            var originalIndex = Tabs.IndexOf(originalTab);
            if (originalIndex >= 0 && originalIndex < Tabs.Count - 1)
            {
                Tabs.Insert(originalIndex + 1, duplicatedTab);
            }
            else
            {
                // 元のタブが最後の場合は末尾に追加
                Tabs.Add(duplicatedTab);
            }
            
            // 複製されたタブを選択
            SelectedTab = duplicatedTab;
        }

        public void AddInnerTab(TabItemViewModel outerTab)
        {
            outerTab.AddInnerTab();
        }

        public void AddInnerTabAtPosition(TabItemViewModel outerTab)
        {
            outerTab.AddInnerTabAtPosition();
        }

        /// <summary>
        /// 現在選択されている内側タブを複製します
        /// </summary>
        /// <param name="outerTab">対象の外側タブ</param>
        public void DuplicateCurrentInnerTab(TabItemViewModel outerTab)
        {
            outerTab.DuplicateCurrentInnerTab();
        }

        /// <summary>
        /// 現在選択されている内側タブを左側に複製します
        /// </summary>
        /// <param name="outerTab">対象の外側タブ</param>
        public void DuplicateCurrentInnerTabLeft(TabItemViewModel outerTab)
        {
            outerTab.DuplicateCurrentInnerTabLeft();
        }

        /// <summary>
        /// 指定された内側タブを右側に複製します
        /// </summary>
        /// <param name="outerTab">対象の外側タブ</param>
        /// <param name="innerTab">複製対象の内側タブ</param>
        public void DuplicateInnerTabRight(TabItemViewModel outerTab, TabItemViewModel innerTab)
        {
            outerTab.DuplicateInnerTabRight(innerTab);
        }

        /// <summary>
        /// 指定された内側タブを左側に複製します
        /// </summary>
        /// <param name="outerTab">対象の外側タブ</param>
        /// <param name="innerTab">複製対象の内側タブ</param>
        public void DuplicateInnerTabLeft(TabItemViewModel outerTab, TabItemViewModel innerTab)
        {
            outerTab.DuplicateInnerTabLeft(innerTab);
        }

        public void RemoveInnerTab(TabItemViewModel outerTab, TabItemViewModel innerTab)
        {
            if (outerTab.InnerTabs.Contains(innerTab) && outerTab.InnerTabs.Count > 1)
            {
                var originalIndex = outerTab.InnerTabs.IndexOf(innerTab);
                
                // 閉じたタブの情報をスタックに保存（新しい方式）
                _closedTabsStack.Push(ClosedTabInfo.CreateForInnerTab(outerTab, innerTab, originalIndex));
                
                outerTab.InnerTabs.Remove(innerTab);
                if (outerTab.InnerTabs.Count > 0)
                {
                    // 削除したタブの位置に応じて次に選択するタブを決定
                    if (originalIndex < outerTab.InnerTabs.Count)
                        outerTab.SelectedInnerTab = outerTab.InnerTabs[originalIndex];
                    else
                        outerTab.SelectedInnerTab = outerTab.InnerTabs[outerTab.InnerTabs.Count - 1];
                }
                else
                    outerTab.SelectedInnerTab = null;
                
                // この外側タブの内側タブのタイトルを更新
                outerTab.UpdateInnerTabTitles();
            }
        }

        public void CloseActiveInnerTab()
        {
            if (SelectedTab?.SelectedInnerTab != null && SelectedTab.InnerTabs.Count > 1)
            {
                var outerTab = SelectedTab;
                var innerTab = outerTab.SelectedInnerTab;
                var originalIndex = outerTab.InnerTabs.IndexOf(innerTab);
                
                // 閉じたタブの情報をスタックに保存（新しい方式）
                _closedTabsStack.Push(ClosedTabInfo.CreateForInnerTab(outerTab, innerTab, originalIndex));
                
                // タブを削除
                outerTab.InnerTabs.Remove(innerTab);
                
                // 次に選択するタブを決定
                if (originalIndex < outerTab.InnerTabs.Count)
                {
                    // 削除したタブの位置にタブがある場合はそれを選択
                    outerTab.SelectedInnerTab = outerTab.InnerTabs[originalIndex];
                }
                else if (outerTab.InnerTabs.Count > 0)
                {
                    // 最後のタブを削除した場合は一つ前のタブを選択
                    outerTab.SelectedInnerTab = outerTab.InnerTabs[outerTab.InnerTabs.Count - 1];
                }
                
                // この外側タブの内側タブのタイトルを更新
                outerTab.UpdateInnerTabTitles();
            }
        }

        public void RestoreLastClosedTab()
        {
            if (_closedTabsStack.Count > 0)
            {
                var closedTabInfo = _closedTabsStack.Pop();
                
                Debug.WriteLine($"Undo復元開始: IsOuterTab={closedTabInfo.IsOuterTab}, OuterTabInstance={closedTabInfo.OuterTabInstance?.Title ?? "null"}");
                
                // インスタンスが復元されていない場合は復元
                if (closedTabInfo.OuterTabInstance == null)
                {
                    Debug.WriteLine("OuterTabInstanceがnullのため、RestoreInstancesを実行");
                    closedTabInfo.RestoreInstances();
                }
                
                if (closedTabInfo.IsOuterTab)
                {
                    // 外側タブの復元
                    var outerTab = closedTabInfo.OuterTabInstance;
                    var originalIndex = closedTabInfo.OuterTabOriginalIndex;
                    
                    Debug.WriteLine($"外側タブを復元: Title={outerTab?.Title}, OriginalIndex={originalIndex}");
                    
                    if (outerTab != null)
                    {
                        // 元の位置に外側タブを復元
                        if (originalIndex <= Tabs.Count)
                        {
                            Tabs.Insert(originalIndex, outerTab);
                        }
                        else
                        {
                            Tabs.Add(outerTab);
                        }
                        
                        // 復元した外側タブを選択
                        SelectedTab = outerTab;
                        
                        // 画像の読み込みを実行
                        OnPropertyChanged(nameof(SelectedTab));
                        
                        Debug.WriteLine($"外側タブ復元完了: 現在のタブ数={Tabs.Count}");
                    }
                }
                else
                {
                    // 内側タブの復元
                    var outerTab = closedTabInfo.OuterTabInstance;
                    var innerTab = closedTabInfo.ClosedInnerTabInstance;

                    Debug.WriteLine($"内側タブを復元: OuterTab={outerTab?.Title}, InnerTab={innerTab?.Title}");
                    Debug.WriteLine($"既存タブに含まれる？ {(outerTab != null && Tabs.Contains(outerTab))}");

                    // 警告 CS8604 を修正: innerTab が null でないことを確認してから追加
                    if (outerTab != null && innerTab != null && Tabs.Contains(outerTab))
                    {
                        var originalIndex = closedTabInfo.OriginalIndex;
                        
                        Debug.WriteLine($"既存の外側タブに内側タブを復元: OriginalIndex={originalIndex}, 現在の内側タブ数={outerTab.InnerTabs.Count}");
                        
                        // 元の位置に内側タブを復元
                        if (originalIndex <= outerTab.InnerTabs.Count)
                        {
                            outerTab.InnerTabs.Insert(originalIndex, innerTab);
                        }
                        else
                        {
                            outerTab.InnerTabs.Add(innerTab);
                        }
                        
                        // 復元した内側タブを選択
                        outerTab.SelectedInnerTab = innerTab;
                        
                        // この外側タブの内側タブのタイトルを更新
                        outerTab.UpdateInnerTabTitles();
                        
                        // 🎯 復元された内側タブがある外側タブにフォーカス
                        if (SelectedTab != outerTab)
                        {
                            SelectedTab = outerTab;
                            Debug.WriteLine($"外側タブ '{outerTab.Title}' にフォーカスしました");
                        }
                        
                        Debug.WriteLine($"内側タブ復元完了: 外側タブ '{outerTab.Title}' の内側タブ数={outerTab.InnerTabs.Count}");
                    }
                    else
                    {
                        Debug.WriteLine("既存の外側タブが見つからないため、新しい外側タブとして復元");
                        
                        // 外側タブが見つからない場合は、新しい外側タブとして復元
                        if (outerTab != null && innerTab != null)
                        {
                            Tabs.Add(outerTab);
                            SelectedTab = outerTab;
                            OnPropertyChanged(nameof(SelectedTab));
                            
                            Debug.WriteLine($"新しい外側タブとして復元完了: Title={outerTab.Title}");
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("Undo履歴が空です");
            }
        }

        public void MoveInnerTab(TabItemViewModel outerTab, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= outerTab.InnerTabs.Count || newIndex < 0 || newIndex >= outerTab.InnerTabs.Count || oldIndex == newIndex)
                return;
            var tab = outerTab.InnerTabs[oldIndex];
            outerTab.InnerTabs.RemoveAt(oldIndex);
            outerTab.InnerTabs.Insert(newIndex, tab);
            outerTab.SelectedInnerTab = tab;
            
            // 内側タブのタイトルを更新
            outerTab.UpdateInnerTabTitles();
        }

        /// <summary>
        /// 現在の外側タブの内側タブを左に移動（cyclic）
        /// </summary>
        public void MoveToPreviousInnerTab()
        {
            if (SelectedTab == null || SelectedTab.InnerTabs.Count <= 1)
                return;

            int currentIndex = SelectedTab.SelectedInnerTab != null 
                ? SelectedTab.InnerTabs.IndexOf(SelectedTab.SelectedInnerTab) 
                : 0;

            // cyclicで前のタブに移動
            int newIndex = currentIndex > 0 ? currentIndex - 1 : SelectedTab.InnerTabs.Count - 1;
            SelectedTab.SelectedInnerTab = SelectedTab.InnerTabs[newIndex];
        }

        /// <summary>
        /// 現在の外側タブの内側タブを右に移動（cyclic）
        /// </summary>
        public void MoveToNextInnerTab()
        {
            if (SelectedTab == null || SelectedTab.InnerTabs.Count <= 1)
                return;

            int currentIndex = SelectedTab.SelectedInnerTab != null 
                ? SelectedTab.InnerTabs.IndexOf(SelectedTab.SelectedInnerTab) 
                : 0;

            // cyclicで次のタブに移動
            int newIndex = currentIndex < SelectedTab.InnerTabs.Count - 1 ? currentIndex + 1 : 0;
            SelectedTab.SelectedInnerTab = SelectedTab.InnerTabs[newIndex];
        }

        /// <summary>
        /// 前の外側タブに移動（cyclic）
        /// </summary>
        public void MoveToPreviousOuterTab()
        {
            if (Tabs.Count <= 1)
                return;

            int currentIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
            
            // cyclicで前のタブに移動
            int newIndex = currentIndex > 0 ? currentIndex - 1 : Tabs.Count - 1;
            SelectedTab = Tabs[newIndex];
            
            // フォーカスを現在の内側タブのプロンプトテキストボックスに設定
            SetFocusToCurrentInnerTab();
        }

        /// <summary>
        /// 次の外側タブに移動（cyclic）
        /// </summary>
        public void MoveToNextOuterTab()
        {
            if (Tabs.Count <= 1)
                return;

            int currentIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0;
            
            // cyclicで次のタブに移動
            int newIndex = currentIndex < Tabs.Count - 1 ? currentIndex + 1 : 0;
            SelectedTab = Tabs[newIndex];
            
            // フォーカスを現在の内側タブのプロンプトテキストボックスに設定
            SetFocusToCurrentInnerTab();
        }
        
        /// <summary>
        /// 現在の内側タブのプロンプトテキストボックスにフォーカスを設定
        /// </summary>
        private void SetFocusToCurrentInnerTab()
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
                                // PromptTextBoxを探してフォーカスを設定
                                var promptTextBox = FindVisualChild<AutoCompleteTextBox>(mainWindow, "PromptTextBox");
                                if (promptTextBox != null)
                                {
                                    promptTextBox.Focus();
                                    Debug.WriteLine("外側タブ移動後: PromptTextBoxにフォーカスを設定しました");
                                }
                                else
                                {
                                    Debug.WriteLine("外側タブ移動後: PromptTextBoxが見つかりませんでした");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"外側タブ移動後のフォーカス設定処理エラー: {ex.Message}");
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"外側タブ移動後のフォーカス設定処理エラー: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public void SaveToFile(string filePath)
        {
            var dtoList = Tabs.Select(x => x.ToDto()).ToList();
            var json = JsonSerializer.Serialize(dtoList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            var json = File.ReadAllText(filePath);
            var dtoList = JsonSerializer.Deserialize<List<TabItemDto>>(json);
            Tabs.Clear();
            if (dtoList != null)
            {
                foreach (var dto in dtoList)
                {
                    var tab = TabItemViewModel.FromDto(dto);
                    // この外側タブの内側タブのタイトルを並び順で更新
                    tab.UpdateInnerTabTitles();
                    Tabs.Add(tab);
                }
                SelectedTab = Tabs.Count > 0 ? Tabs[0] : null;
            }
        }
        
        /// <summary>
        /// Undo履歴ファイルのパスを取得
        /// </summary>
        private string GetUndoHistoryFilePath()
        {
            var exePath = Environment.ProcessPath;
            var dir = Path.GetDirectoryName(exePath) ?? ".";
            var settingsDir = Path.Combine(dir, "settings");
            Directory.CreateDirectory(settingsDir); // settingsディレクトリが存在しない場合は作成
            return Path.Combine(settingsDir, "undo_history.json");
        }

        /// <summary>
        /// 絶対パスを実行ファイルからの相対パスに変換
        /// </summary>
        private string ConvertToRelativePath(string absolutePath)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var exeDir = Path.GetDirectoryName(exePath) ?? ".";
                
                // 既に相対パスの場合はそのまま返す
                if (!Path.IsPathRooted(absolutePath))
                {
                    return absolutePath;
                }
                
                // 実行ファイルのディレクトリからの相対パスを計算
                var relativePath = Path.GetRelativePath(exeDir, absolutePath);
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
                var exeDir = Path.GetDirectoryName(exePath) ?? ".";
                
                // 既に絶対パスの場合はそのまま返す
                if (Path.IsPathRooted(relativePath))
                {
                    return relativePath;
                }
                
                // 実行ファイルのディレクトリを基準に絶対パスを構築
                var absolutePath = Path.GetFullPath(Path.Combine(exeDir, relativePath));
                return absolutePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"絶対パス変換エラー: {ex.Message}");
                return relativePath; // エラーの場合は元のパスを返す
            }
        }

        /// <summary>
        /// Undo履歴を保存（最新50件に制限）
        /// </summary>
        public void SaveUndoHistory()
        {
            try
            {
                // スタックから配列に変換（新しいものから古いものへの順序）
                var historyArray = _closedTabsStack.ToArray();
                
                // 最新50件に制限
                var limitedHistory = historyArray.Take(50).ToList();
                
                var json = JsonSerializer.Serialize(limitedHistory, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(GetUndoHistoryFilePath(), json);
                
                Debug.WriteLine($"Undo履歴を保存しました: {limitedHistory.Count}件");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo履歴保存エラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Undo履歴を読み込み
        /// </summary>
        public void LoadUndoHistory()
        {
            try
            {
                string filePath = GetUndoHistoryFilePath();
                if (!File.Exists(filePath))
                {
                    Debug.WriteLine("Undo履歴ファイルが存在しません");
                    return;
                }
                
                var json = File.ReadAllText(filePath);
                var historyList = JsonSerializer.Deserialize<List<ClosedTabInfo>>(json);
                
                if (historyList != null)
                {
                    // スタックをクリア
                    _closedTabsStack.Clear();
                    
                    // リストを逆順でスタックにプッシュ（最も古いものを最初にプッシュ）
                    for (int i = historyList.Count - 1; i >= 0; i--)
                    {
                        var item = historyList[i];
                        
                        // 既存のタブとの関連付けを試行
                        if (item.IsOuterTab)
                        {
                            // 外側タブの場合：完全に復元（新しいインスタンス作成）
                            item.RestoreInstances();
                            Debug.WriteLine($"外側タブUndoを復元: Title={item.OuterTabTitle}");
                        }
                        else
                        {
                            // 内側タブの場合：外側タブGUIDで既存タブを検索
                            var matchingOuterTab = FindMatchingOuterTabByGuid(item.OuterTabGuid);
                            if (matchingOuterTab != null)
                            {
                                // 既存の外側タブが見つかった場合
                                item.OuterTabInstance = matchingOuterTab;
                                // 閉じた内側タブのインスタンスのみ復元
                                if (item.ClosedInnerTab != null)
                                {
                                    item.ClosedInnerTabInstance = TabItemViewModel.FromDto(item.ClosedInnerTab);
                                }
                                Debug.WriteLine($"内側タブUndoを既存外側タブ '{matchingOuterTab.Title}' (GUID: {matchingOuterTab.Guid}) に関連付けました");
                            }
                            else
                            {
                                // 既存の外側タブが見つからない場合は復元をスキップ
                                Debug.WriteLine($"内側タブUndoで外側タブGUID '{item.OuterTabGuid}' が見つからないため、スキップしました");
                                continue; // この項目はスタックに追加せずにスキップ
                            }
                        }
                        
                        _closedTabsStack.Push(item);
                    }
                    
                    Debug.WriteLine($"Undo履歴を読み込みました: {historyList.Count}件中{_closedTabsStack.Count}件を復元");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo履歴読み込みエラー: {ex.Message}");
                // エラーの場合はスタックをクリア
                _closedTabsStack.Clear();
            }
        }
        
        /// <summary>
        /// 外側タブGUIDで既存の外側タブを検索（高速で確実）
        /// </summary>
        private TabItemViewModel? FindMatchingOuterTabByGuid(Guid? outerTabGuid)
        {
            if (outerTabGuid == null || outerTabGuid == Guid.Empty) return null;
            
            // GUIDで直接検索（一意性保証）
            return Tabs.FirstOrDefault(tab => tab.Guid == outerTabGuid);
        }
        
        /// <summary>
        /// Undo履歴をクリア
        /// </summary>
        public void ClearUndoHistory()
        {
            _closedTabsStack.Clear();
            
            try
            {
                string filePath = GetUndoHistoryFilePath();
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                Debug.WriteLine("Undo履歴をクリアしました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo履歴ファイル削除エラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Undo履歴の件数を取得
        /// </summary>
        public int GetUndoHistoryCount()
        {
            return _closedTabsStack.Count;
        }

        /// <summary>
        /// 閉じたタブのスタックを取得（画像クリーンアップ処理用）
        /// </summary>
        public IEnumerable<ClosedTabInfo> GetClosedTabsStack()
        {
            return _closedTabsStack.ToArray();
        }

        /// <summary>
        /// Random Resolution設定が外部から変更された際に呼び出すメソッド
        /// </summary>
        public void RefreshRandomResolutionText()
        {
            _rawRandomResolutionText = "";
            OnPropertyChanged(nameof(RandomResolutionCurrentResolutions));
        }

        /// <summary>
        /// 外側タブ切り替え時にテキストボックスのUndo履歴をクリアする
        /// </summary>
        private void ClearTextBoxUndoHistoryOnOuterTabChange()
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
                                
                                Debug.WriteLine("外側タブ切り替え時: テキストボックスのUndo履歴をクリアしました");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"外側タブ切り替え時のUndo履歴クリア処理エラー: {ex.Message}");
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"外側タブ切り替え時のUndo履歴クリアスケジューリングエラー: {ex.Message}");
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

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // PropertyChangedイベントハンドラーをクリア
                    PropertyChanged = null;
                    
                    // すべてのタブのDisposeを実行
                    foreach (var tab in Tabs)
                    {
                        tab?.Dispose();
                    }
                    
                    // コレクションをクリア
                    Tabs.Clear();
                    
                    // 閉じたタブのスタックをクリア
                    _closedTabsStack.Clear();
                    
                    // その他のリソースのクリア
                    _selectedTab = null;
                }
                
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
