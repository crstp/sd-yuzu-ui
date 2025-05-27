using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SD.Yuzu
{
    /// <summary>
    /// チェックポイント情報を管理するクラス
    /// </summary>
    public class CheckpointInfo
    {
        public string Title { get; set; } = "";
        public string ModelName { get; set; } = "";
        public string? Hash { get; set; }
        public string? Sha256 { get; set; }
        public string Filename { get; set; } = "";
        public string? Config { get; set; }
    }

    /// <summary>
    /// APIのオプション情報を管理するクラス
    /// </summary>
    public class OptionsInfo
    {
        public string SdModelCheckpoint { get; set; } = "";
    }

    /// <summary>
    /// チェックポイントリストと現在選択されているチェックポイントを管理するマネージャー
    /// アプリケーション全体で一度だけAPIから取得し、全てのタブで共有する
    /// </summary>
    public class CheckpointManager
    {
        private static CheckpointManager? _instance;
        private static readonly object _lock = new object();

        private List<string> _checkpointTitles;
        private string _currentCheckpoint = "";
        private bool _isInitialized = false;
        private bool _isUpdatingFromOptions = false; // オプション取得時の無限ループを防ぐフラグ
        private readonly object _initLock = new object();

        public static CheckpointManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CheckpointManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private CheckpointManager()
        {
            // フォールバック用のデフォルトチェックポイントリスト
            _checkpointTitles = new List<string> { "Default Model" };
            _currentCheckpoint = "Default Model";
        }

        /// <summary>
        /// チェックポイントリストを取得（初回はAPIから取得、以降はキャッシュを返す）
        /// </summary>
        public async Task<List<string>> GetCheckpointTitlesAsync()
        {
            if (_isInitialized)
            {
                return new List<string>(_checkpointTitles);
            }

            Task? fetchTask = null;

            lock (_initLock)
            {
                if (_isInitialized)
                {
                    return new List<string>(_checkpointTitles);
                }

                // 非同期でAPIから取得（ブロッキングしないように）
                fetchTask = Task.Run(async () =>
                {
                    try
                    {
                        var checkpoints = await FetchCheckpointsFromApiAsync();
                        var currentOption = await FetchCurrentOptionAsync();
                        
                        lock (_initLock)
                        {
                            _checkpointTitles = checkpoints.Select(c => c.Title).ToList();
                            _currentCheckpoint = currentOption.SdModelCheckpoint;
                            _isInitialized = true;
                            Debug.WriteLine($"チェックポイントリストをAPIから取得完了: {_checkpointTitles.Count}個");
                            Debug.WriteLine($"現在のチェックポイント: {_currentCheckpoint}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"チェックポイントAPI取得エラー: {ex.Message}");
                        lock (_initLock)
                        {
                            // フォールバックリストを使用
                            _checkpointTitles = GetFallbackCheckpoints();
                            _currentCheckpoint = _checkpointTitles.FirstOrDefault() ?? "Default Model";
                            _isInitialized = true;
                            Debug.WriteLine("フォールバックチェックポイントリストを使用");
                        }
                    }
                });
            }

            // API取得が完了するまで待機
            if (fetchTask != null)
            {
                await fetchTask;
            }

            // 初期化完了後の実際のリストを返す
            lock (_initLock)
            {
                return new List<string>(_checkpointTitles);
            }
        }

        /// <summary>
        /// 現在選択されているチェックポイントを取得
        /// </summary>
        public async Task<string> GetCurrentCheckpointAsync()
        {
            await GetCheckpointTitlesAsync(); // 初期化を確実に実行
            lock (_initLock)
            {
                return _currentCheckpoint;
            }
        }

        /// <summary>
        /// チェックポイントを変更し、APIに送信
        /// </summary>
        public async Task SetCurrentCheckpointAsync(string checkpointTitle)
        {
            if (string.IsNullOrEmpty(checkpointTitle))
                return;

            lock (_initLock)
            {
                if (_isUpdatingFromOptions)
                    return; // オプション更新中は無限ループを防ぐ

                _currentCheckpoint = checkpointTitle;
            }

            try
            {
                await UpdateOptionAsync(checkpointTitle);
                Debug.WriteLine($"チェックポイントを変更: {checkpointTitle}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"チェックポイント変更エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// APIからチェックポイント情報を取得
        /// </summary>
        private async Task<List<CheckpointInfo>> FetchCheckpointsFromApiAsync()
        {
            string url = App.BASE_URL + "/sdapi/v1/sd-models";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // タイムアウトは短めに設定
            
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var checkpointData = JsonSerializer.Deserialize<List<CheckpointInfo>>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return checkpointData ?? new List<CheckpointInfo>();
        }

        /// <summary>
        /// APIから現在のオプション情報を取得
        /// </summary>
        private async Task<OptionsInfo> FetchCurrentOptionAsync()
        {
            string url = App.BASE_URL + "/sdapi/v1/options";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var optionsData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var optionsInfo = new OptionsInfo();
            if (optionsData != null && optionsData.ContainsKey("sd_model_checkpoint"))
            {
                optionsInfo.SdModelCheckpoint = optionsData["sd_model_checkpoint"]?.ToString() ?? "";
            }

            return optionsInfo;
        }

        /// <summary>
        /// APIのオプションを更新
        /// </summary>
        private async Task UpdateOptionAsync(string checkpointTitle)
        {
            string url = App.BASE_URL + "/sdapi/v1/options";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30); // オプション更新は時間がかかる場合がある
            
            var updateData = new Dictionary<string, object>
            {
                { "sd_model_checkpoint", checkpointTitle }
            };
            
            var jsonString = JsonSerializer.Serialize(updateData);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// フォールバック用のチェックポイントリスト
        /// </summary>
        private List<string> GetFallbackCheckpoints()
        {
            var fallbackList = new List<string>
            {
                "Default Model"
            };
            
            return fallbackList;
        }

        /// <summary>
        /// 強制的にAPIから再取得（設定変更時などに使用）
        /// </summary>
        public async Task RefreshCheckpointsAsync()
        {
            try
            {
                // まずrefresh-checkpoints APIを呼び出してCheckpointリストを更新
                await RefreshCheckpointsApiAsync();
                
                // その後、最新のリストを取得
                var checkpoints = await FetchCheckpointsFromApiAsync();
                var currentOption = await FetchCurrentOptionAsync();
                
                lock (_initLock)
                {
                    _isUpdatingFromOptions = true; // フラグを設定
                    _checkpointTitles = checkpoints.Select(c => c.Title).ToList();
                    _currentCheckpoint = currentOption.SdModelCheckpoint;
                    _isInitialized = true;
                    _isUpdatingFromOptions = false; // フラグをリセット
                    Debug.WriteLine($"チェックポイントリストを再取得完了: {_checkpointTitles.Count}個");
                    Debug.WriteLine($"現在のチェックポイント: {_currentCheckpoint}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"チェックポイント再取得エラー: {ex.Message}");
                lock (_initLock)
                {
                    _isUpdatingFromOptions = false; // エラー時もフラグをリセット
                }
            }
        }

        /// <summary>
        /// refresh-checkpoints APIを呼び出してCheckpointリストを強制更新
        /// </summary>
        private async Task RefreshCheckpointsApiAsync()
        {
            string url = App.BASE_URL + "/sdapi/v1/refresh-checkpoints";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30); // refresh処理は時間がかかる場合がある
            
            var content = new StringContent("", Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            
            Debug.WriteLine("refresh-checkpoints API呼び出し完了");
        }
    }
} 