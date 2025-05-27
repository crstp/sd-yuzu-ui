using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SD.Yuzu
{
    /// <summary>
    /// スケジューラー情報を管理するクラス
    /// </summary>
    public class SchedulerInfo
    {
        public string Name { get; set; } = "";
        public string Label { get; set; } = "";
        public List<string>? Aliases { get; set; }
        public double DefaultRho { get; set; } = -1;
        public bool NeedInnerModel { get; set; } = false;
    }

    /// <summary>
    /// スケジューラーリストを管理するマネージャー
    /// アプリケーション全体で一度だけAPIから取得し、全てのタブで共有する
    /// </summary>
    public class SchedulerManager
    {
        private static SchedulerManager? _instance;
        private static readonly object _lock = new object();

        private readonly OneShotApi<List<string>> _oneShotApi;
        private readonly object _initLock = new object();

        public static SchedulerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SchedulerManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private SchedulerManager()
        {
            // OneShotApiを初期化
            _oneShotApi = new OneShotApi<List<string>>(FetchSchedulersFromApiAsync);
        }

        /// <summary>
        /// スケジューラーリストを取得（初回はAPIから取得、以降はキャッシュを返す）
        /// </summary>
        public async Task<List<string>> GetSchedulerLabelsAsync()
        {
            try
            {
                var schedulers = await _oneShotApi.GetAsync();
                // ソートして返す
                var sortedList = new List<string>(schedulers);
                sortedList.Sort();
                return sortedList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スケジューラーAPI取得エラー: {ex.Message}");
                // エラーの場合はフォールバックリストを返す
                var fallbackList = GetFallbackSchedulers();
                fallbackList.Sort();
                return fallbackList;
            }
        }

        /// <summary>
        /// 指定されたスケジューラーがリストに存在するかチェックし、なければ追加
        /// </summary>
        public async Task<List<string>> EnsureSchedulerExistsAsync(string schedulerLabel)
        {
            var schedulers = await GetSchedulerLabelsAsync();
            
            if (!string.IsNullOrEmpty(schedulerLabel) && !schedulers.Contains(schedulerLabel))
            {
                // 新しいスケジューラーを永続的に追加
                var newList = new List<string>(schedulers);
                newList.Add(schedulerLabel);
                newList.Sort();
                
                // キャッシュを更新（既存のAPIデータと新しいアイテムを組み合わせ）
                _oneShotApi.UpdateCache(newList);
                
                Debug.WriteLine($"スケジューラーリストに追加: {schedulerLabel}");
                return newList;
            }

            return schedulers;
        }

        /// <summary>
        /// APIからスケジューラー情報を取得
        /// </summary>
        private async Task<List<string>> FetchSchedulersFromApiAsync()
        {
            Debug.WriteLine("スケジューラーリストをAPIから取得開始");
            
            string url = App.BASE_URL + "/sdapi/v1/schedulers";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // タイムアウトは短めに設定
            
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var schedulerData = JsonSerializer.Deserialize<List<SchedulerInfo>>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var schedulers = schedulerData?.Select(s => s.Label).ToList() ?? new List<string>();
            
            // API呼び出しが失敗した場合や結果が空の場合はフォールバックを使用
            if (schedulers.Count == 0)
            {
                Debug.WriteLine("API呼び出し結果が空のため、フォールバックリストを使用");
                schedulers = GetFallbackSchedulers();
            }

            Debug.WriteLine($"スケジューラーリストをAPIから取得完了: {schedulers.Count}個");
            return schedulers;
        }

        /// <summary>
        /// フォールバック用のスケジューラーリスト
        /// </summary>
        private List<string> GetFallbackSchedulers()
        {
            return new List<string>
            {
                "Automatic",
                "Uniform", 
                "Karras", 
                "Exponential", 
                "Polyexponential", 
                "SGM Uniform",
                "Normal",
                "DDIM",
                "Simple",
                "Cosine",
                "Beta",
                "Turbo"
            };
        }

        /// <summary>
        /// 強制的にAPIから再取得（設定変更時などに使用）
        /// </summary>
        public async Task RefreshSchedulersAsync()
        {
            try
            {
                // キャッシュをリセットして再取得
                _oneShotApi.Reset();
                var schedulers = await _oneShotApi.GetAsync();
                Debug.WriteLine($"スケジューラーリストを再取得完了: {schedulers.Count}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"スケジューラー再取得エラー: {ex.Message}");
                // エラーの場合は既存のキャッシュを保持
            }
        }
    }
} 