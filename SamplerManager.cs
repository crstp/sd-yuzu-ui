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
    /// サンプラー情報を管理するクラス
    /// </summary>
    public class SamplerInfo
    {
        public string Name { get; set; } = "";
        public List<string> Aliases { get; set; } = new List<string>();
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// サンプラーリストを管理するマネージャー
    /// アプリケーション全体で一度だけAPIから取得し、全てのタブで共有する
    /// </summary>
    public class SamplerManager
    {
        private static SamplerManager? _instance;
        private static readonly object _lock = new object();

        private readonly OneShotApi<List<string>> _oneShotApi;
        private readonly object _initLock = new object();

        public static SamplerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SamplerManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private SamplerManager()
        {
            // OneShotApiを初期化
            _oneShotApi = new OneShotApi<List<string>>(FetchSamplersFromApiAsync);
        }

        /// <summary>
        /// サンプラーリストを取得（初回はAPIから取得、以降はキャッシュを返す）
        /// </summary>
        public async Task<List<string>> GetSamplerNamesAsync()
        {
            try
            {
                var samplers = await _oneShotApi.GetAsync();
                // ソートして返す
                var sortedList = new List<string>(samplers);
                sortedList.Sort();
                return sortedList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"サンプラーAPI取得エラー: {ex.Message}");
                // エラーの場合はフォールバックリストを返す
                var fallbackList = GetFallbackSamplers();
                fallbackList.Sort();
                return fallbackList;
            }
        }

        /// <summary>
        /// 指定されたサンプラーがリストに存在するかチェックし、なければ追加
        /// </summary>
        public async Task<List<string>> EnsureSamplerExistsAsync(string samplerName)
        {
            var samplers = await GetSamplerNamesAsync();
            
            if (!string.IsNullOrEmpty(samplerName) && !samplers.Contains(samplerName))
            {
                // 新しいサンプラーを永続的に追加
                var newList = new List<string>(samplers);
                newList.Add(samplerName);
                newList.Sort();
                
                // キャッシュを更新（既存のAPIデータと新しいアイテムを組み合わせ）
                _oneShotApi.UpdateCache(newList);
                
                Debug.WriteLine($"サンプラーリストに追加: {samplerName}");
                return newList;
            }

            return samplers;
        }

        /// <summary>
        /// APIからサンプラー情報を取得
        /// </summary>
        private async Task<List<string>> FetchSamplersFromApiAsync()
        {
            Debug.WriteLine("サンプラーリストをAPIから取得開始");
            
            string url = App.BASE_URL + "/sdapi/v1/samplers";
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // タイムアウトは短めに設定
            
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var jsonString = await response.Content.ReadAsStringAsync();
            var samplerData = JsonSerializer.Deserialize<List<SamplerInfo>>(jsonString, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var samplers = samplerData?.Select(s => s.Name).ToList() ?? new List<string>();
            
            // API呼び出しが失敗した場合や結果が空の場合はフォールバックを使用
            if (samplers.Count == 0)
            {
                Debug.WriteLine("API呼び出し結果が空のため、フォールバックリストを使用");
                samplers = GetFallbackSamplers();
            }

            Debug.WriteLine($"サンプラーリストをAPIから取得完了: {samplers.Count}個");
            return samplers;
        }

        /// <summary>
        /// フォールバック用のサンプラーリスト
        /// </summary>
        private List<string> GetFallbackSamplers()
        {
            return new List<string>
            {
                "Euler a",
                "Euler",
                "DPM++ 2M",
                "DPM++ SDE",
                "DPM++ 2M SDE",
                "DPM++ 2S a",
                "DPM++ 3M SDE",
                "DDIM",
                "PLMS",
                "UniPC",
                "LMS",
                "Heun",
                "DPM2",
                "DPM2 a",
                "DPM fast",
                "DPM adaptive",
                "Restart",
                "LCM"
            };
        }

        /// <summary>
        /// 強制的にAPIから再取得（設定変更時などに使用）
        /// </summary>
        public async Task RefreshSamplersAsync()
        {
            try
            {
                // キャッシュをリセットして再取得
                _oneShotApi.Reset();
                var samplers = await _oneShotApi.GetAsync();
                Debug.WriteLine($"サンプラーリストを再取得完了: {samplers.Count}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"サンプラー再取得エラー: {ex.Message}");
                // エラーの場合は既存のキャッシュを保持
            }
        }
    }
} 