using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SD.Yuzu
{
    /// <summary>
    /// 画像生成リクエストのキュー項目
    /// </summary>
    public class GenerationQueueItem
    {
        public string TabId { get; set; } = "";
        public TabItemViewModel Tab { get; set; } = null!;
        public Dictionary<string, object> Payload { get; set; } = new();
        public DateTime QueuedAt { get; set; } = DateTime.Now;
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
        public DateTime? GenerationStartTime { get; set; } = null;
    }

    /// <summary>
    /// txt2img APIのレスポンス
    /// </summary>
    public class Response
    {
        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = new();
        
        [JsonPropertyName("parameters")]
        public JsonElement? Parameters { get; set; }
        
        [JsonPropertyName("info")]
        public string Info { get; set; } = "";
    }

    /// <summary>
    /// プログレス監視APIのレスポンス
    /// </summary>
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

    /// <summary>
    /// プログレス状態
    /// </summary>
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

    /// <summary>
    /// 画像生成キューの管理クラス
    /// 複数タブでの同時生成を制御し、適切な順序で処理を行う
    /// </summary>
    public class GenerationQueueManager
    {
        private static readonly Lazy<GenerationQueueManager> _instance = new(() => new GenerationQueueManager());
        public static GenerationQueueManager Instance => _instance.Value;

        private readonly ConcurrentQueue<GenerationQueueItem> _queue = new();
        private readonly Dictionary<string, GenerationQueueItem> _activeGenerations = new();
        private readonly object _lock = new object();
        private readonly Timer _processingTimer;
        private volatile bool _isProcessing = false;

        private GenerationQueueManager()
        {
            // 100ms間隔でキューをチェック
            _processingTimer = new Timer(ProcessQueue, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        /// <summary>
        /// 生成リクエストをキューに追加
        /// </summary>
        public void EnqueueGeneration(TabItemViewModel tab, Dictionary<string, object> payload)
        {
            var tabId = GetTabId(tab);
            
            lock (_lock)
            {
                // 重複チェックを削除 - 同じタブで複数回Generateを許可
                var queueItem = new GenerationQueueItem
                {
                    TabId = tabId,
                    Tab = tab,
                    Payload = payload
                };

                _queue.Enqueue(queueItem);
                Debug.WriteLine($"タブ {tabId} をキューに追加しました。キューサイズ: {_queue.Count}");
                
                // キューに追加した時点でIsGeneratingをtrueに設定（タブ名の生成中アイコンを即座に表示）
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    tab.IsGenerating = true;
                    Debug.WriteLine($"タブ {tabId} のIsGeneratingをtrueに設定しました（キューに追加）");
                });
            }
        }

        /// <summary>
        /// 指定されたタブの生成をキャンセル
        /// </summary>
        public void CancelGeneration(TabItemViewModel tab)
        {
            var tabId = GetTabId(tab);
            
            lock (_lock)
            {
                // アクティブな生成をキャンセル
                if (_activeGenerations.TryGetValue(tabId, out var activeItem))
                {
                    activeItem.CancellationTokenSource.Cancel();
                    Debug.WriteLine($"タブ {tabId} のアクティブな生成をキャンセルしました");
                }
                
                // キューに入っているタスクも削除
                var itemsToRemove = new List<GenerationQueueItem>();
                var tempQueue = new Queue<GenerationQueueItem>();
                
                while (_queue.TryDequeue(out var queueItem))
                {
                    if (queueItem.TabId == tabId)
                    {
                        queueItem.CancellationTokenSource.Cancel();
                        itemsToRemove.Add(queueItem);
                    }
                    else
                    {
                        tempQueue.Enqueue(queueItem);
                    }
                }
                
                // 削除されなかったアイテムをキューに戻す
                while (tempQueue.Count > 0)
                {
                    _queue.Enqueue(tempQueue.Dequeue());
                }
                
                Debug.WriteLine($"タブ {tabId} のキューから {itemsToRemove.Count} 個のタスクを削除しました");
                
                // UIスレッドでタブの状態をリセット
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    tab.IsGenerating = false;
                    tab.ProgressValue = 0.0;
                    tab.EtaText = "";
                    
                    // すべてのタスクがキャンセルされたのでボタンテキストをリセット
                    try
                    {
                        tab.GenerateButtonText = "Generate";
                        Debug.WriteLine($"タブ {tabId} のすべてのタスクがキャンセルされたため、ボタンテキストを「Generate」にリセットしました");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"キャンセル時のGenerateButtonTextリセットエラー: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// 指定されたタブが現在生成中かどうかを確認
        /// </summary>
        public bool IsGenerating(TabItemViewModel tab)
        {
            var tabId = GetTabId(tab);
            lock (_lock)
            {
                return _activeGenerations.ContainsKey(tabId);
            }
        }

        /// <summary>
        /// 指定されたタブがキューに入っているかどうかを確認
        /// </summary>
        public bool IsQueued(TabItemViewModel tab)
        {
            var tabId = GetTabId(tab);
            lock (_lock)
            {
                return _queue.Any(item => item.TabId == tabId);
            }
        }

        /// <summary>
        /// 指定されたタブがキューに入っているか生成中かを確認
        /// </summary>
        public bool IsTabBusy(TabItemViewModel tab)
        {
            return IsGenerating(tab) || IsQueued(tab);
        }

        /// <summary>
        /// 指定されたタブのキュー内の残りタスク数を取得
        /// </summary>
        public int GetTabQueueCount(TabItemViewModel tab)
        {
            var tabId = GetTabId(tab);
            lock (_lock)
            {
                int queuedCount = _queue.Count(item => item.TabId == tabId);
                int activeCount = _activeGenerations.ContainsKey(tabId) ? 1 : 0;
                return queuedCount + activeCount;
            }
        }

        /// <summary>
        /// キューの状態を取得
        /// </summary>
        public (int queueCount, int activeCount) GetQueueStatus()
        {
            lock (_lock)
            {
                return (_queue.Count, _activeGenerations.Count);
            }
        }

        /// <summary>
        /// タブの一意識別子を生成
        /// </summary>
        private string GetTabId(TabItemViewModel tab)
        {
            // GUIDを使用してより確実な一意識別子を作成
            return tab.Guid.ToString();
        }

        /// <summary>
        /// キューを処理するメインループ
        /// </summary>
        private async void ProcessQueue(object? state)
        {
            if (_isProcessing)
                return;

            _isProcessing = true;

            try
            {
                GenerationQueueItem? nextItem = null;

                lock (_lock)
                {
                    // アクティブな生成が既にある場合は待機
                    if (_activeGenerations.Count > 0)
                        return;

                    // キューから次のアイテムを取得
                    if (_queue.TryDequeue(out nextItem))
                    {
                        _activeGenerations[nextItem.TabId] = nextItem;
                        Debug.WriteLine($"タブ {nextItem.TabId} の生成を開始します");
                    }
                }

                if (nextItem != null)
                {
                    await ProcessGenerationAsync(nextItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"キュー処理エラー: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// 実際の画像生成処理
        /// </summary>
        private async Task ProcessGenerationAsync(GenerationQueueItem item)
        {
            try
            {
                // IsGeneratingは既にEnqueueGenerationで設定済みなので、ここでは設定しない
                // 進捗値のみリセット
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    item.Tab.ProgressValue = 0.0;
                    item.Tab.EtaText = "";
                });

                // 進捗監視を開始
                var progressTask = StartProgressMonitoring(item);

                // 生成開始時刻を記録（API呼び出し直前）
                item.GenerationStartTime = DateTime.Now;

                // 実際の生成処理を実行
                await ExecuteGenerationAsync(item);

                // 進捗監視を停止（キャンセルトークンを使用）
                item.CancellationTokenSource.Cancel();
                
                try
                {
                    await progressTask;
                }
                catch (OperationCanceledException)
                {
                    // 進捗監視のキャンセルは正常な動作
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"タブ {item.TabId} の生成がキャンセルされました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"タブ {item.TabId} の生成エラー: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _activeGenerations.Remove(item.TabId);
                    Debug.WriteLine($"タブ {item.TabId} の生成が完了しました");
                    
                    // 同じタブのキューが残っているかチェック
                    bool hasRemainingTasks = _queue.Any(queueItem => queueItem.TabId == item.TabId);
                    
                    // UIスレッドで状態を更新
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // 同じタブのタスクがすべて完了した場合のみ状態をリセット
                        if (!hasRemainingTasks)
                        {
                            try
                            {
                                item.Tab.IsGenerating = false;
                                item.Tab.ProgressValue = 0.0;
                                item.Tab.EtaText = "";
                                item.Tab.GenerateButtonText = "Generate";
                                Debug.WriteLine($"タブ {item.TabId} のすべてのタスクが完了したため、状態をリセットしました");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"状態リセットエラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            // まだキューが残っている場合は進捗値のみリセット
                            item.Tab.ProgressValue = 0.0;
                            item.Tab.EtaText = "";
                            Debug.WriteLine($"タブ {item.TabId} にはまだキューが残っているため、IsGeneratingとボタンテキストは維持します");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 進捗監視を開始
        /// </summary>
        private async Task StartProgressMonitoring(GenerationQueueItem item)
        {
            string url = App.BASE_URL;
            
            await Task.Run(async () =>
            {
                using var progressClient = new HttpClient();
                progressClient.Timeout = TimeSpan.FromSeconds(10);
                
                int consecutiveErrors = 0;
                const int maxConsecutiveErrors = 5;
                
                while (item.Tab.IsGenerating && !item.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var progressResponse = await progressClient.GetAsync($"{url}/sdapi/v1/progress", item.CancellationTokenSource.Token);
                        if (progressResponse.IsSuccessStatusCode)
                        {
                            var progressData = await progressResponse.Content.ReadFromJsonAsync<ProgressResponse>(cancellationToken: item.CancellationTokenSource.Token);
                            if (progressData != null)
                            {
                                // UIスレッドで進捗を更新
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // 現在アクティブなタブの場合のみ進捗を表示
                                    if (IsCurrentlyActiveTab(item.Tab))
                                    {
                                        item.Tab.ProgressValue = progressData.Progress;
                                        // EtaRelativeが0未満の場合は0とする
                                        var etaSeconds = Math.Max(0, progressData.EtaRelative);
                                        item.Tab.EtaText = $"{etaSeconds:F1} sec";
                                    }
                                    else
                                    {
                                        // アクティブでないタブは進捗を非表示にする
                                        item.Tab.ProgressValue = 0.0;
                                        item.Tab.EtaText = "";
                                    }
                                });
                                consecutiveErrors = 0;
                            }
                        }
                        else
                        {
                            consecutiveErrors++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        consecutiveErrors++;
                        Debug.WriteLine($"進捗監視エラー: {ex.Message}");
                        
                        if (consecutiveErrors >= maxConsecutiveErrors)
                        {
                            break;
                        }
                    }
                    
                    await Task.Delay(200, item.CancellationTokenSource.Token);
                }
                
                // 進捗監視終了時に進捗値をリセット
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Tab.ProgressValue = 0.0;
                    item.Tab.EtaText = "";
                });
            });
        }

        /// <summary>
        /// 指定されたタブが現在アクティブ（表示中）かどうかを確認
        /// </summary>
        private bool IsCurrentlyActiveTab(TabItemViewModel tab)
        {
            try
            {
                // UIスレッドで現在選択されているタブを確認
                return System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mainWindow?.DataContext is MainViewModel mainViewModel)
                    {
                        var selectedOuterTab = mainViewModel.SelectedTab;
                        var selectedInnerTab = selectedOuterTab?.SelectedInnerTab;
                        
                        // 現在選択されているタブと比較
                        return selectedInnerTab != null && selectedInnerTab.Guid == tab.Guid;
                    }
                    return false;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"アクティブタブ判定エラー: {ex.Message}");
                return true; // エラーの場合は安全側に倒して表示する
            }
        }

        /// <summary>
        /// 実際のtxt2img生成処理
        /// </summary>
        private async Task ExecuteGenerationAsync(GenerationQueueItem item)
        {
            string url = App.BASE_URL;
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(30);

            var response = await client.PostAsJsonAsync($"{url}/sdapi/v1/txt2img", item.Payload, item.CancellationTokenSource.Token);
            var result = await response.Content.ReadFromJsonAsync<Response>(cancellationToken: item.CancellationTokenSource.Token);

            if (result?.Images?.Count > 0)
            {
                // 画像処理をUIスレッドで実行
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ProcessGeneratedImages(result, item);
                });
            }
        }

        /// <summary>
        /// 生成された画像を処理
        /// </summary>
        private async Task ProcessGeneratedImages(Response response, GenerationQueueItem item)
        {
            try
            {
                var exePath = Environment.ProcessPath;
                var dir = System.IO.Path.GetDirectoryName(exePath) ?? ".";
                string legacyOutputDir = System.IO.Path.Combine(dir, "image_db");
                System.IO.Directory.CreateDirectory(legacyOutputDir);

                var imagePaths = new System.Collections.ObjectModel.ObservableCollection<string>();
                var limitedImages = response.Images.Take(AppSettings.Instance.DefaultImageLimit).ToList();

                var tasks = limitedImages.Select(async (base64Image, index) =>
                {
                    return await Task.Run(() =>
                    {
                        try
                        {
                            byte[] imageBytes = Convert.FromBase64String(base64Image);
                            string fileName = $"cache_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{index}_{Guid.NewGuid()}.webp";
                            string filePath = System.IO.Path.Combine(legacyOutputDir, fileName);
                            System.IO.File.WriteAllBytes(filePath, imageBytes);
                            return System.IO.Path.GetFullPath(filePath);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"画像保存エラー (Index: {index}): {ex.Message}");
                            return null;
                        }
                    });
                });

                var paths = await Task.WhenAll(tasks);

                foreach (var path in paths)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        imagePaths.Add(path);
                    }
                }

                // 既存のコレクションを更新
                item.Tab.ImagePaths.Clear();
                foreach (var path in imagePaths)
                {
                    item.Tab.ImagePaths.Add(path);
                }

                // 生成時間を計算して表示を更新
                if (item.GenerationStartTime.HasValue)
                {
                    var generationTime = DateTime.Now - item.GenerationStartTime.Value;
                    item.Tab.ProcessingTime = string.Format(LocalizationHelper.Instance.GenerationTime_Label, generationTime.TotalSeconds.ToString("F1"));
                    Debug.WriteLine($"生成完了: {generationTime.TotalSeconds:F1} 秒");
                }

                Debug.WriteLine($"画像保存完了: {imagePaths.Count}枚を {legacyOutputDir} に保存しました。");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            _processingTimer?.Dispose();
            
            lock (_lock)
            {
                foreach (var item in _activeGenerations.Values)
                {
                    item.CancellationTokenSource.Cancel();
                    item.CancellationTokenSource.Dispose();
                }
                _activeGenerations.Clear();
            }
        }
    }
} 