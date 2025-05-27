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
    /// Latent upscale mode情報を管理するクラス
    /// </summary>
    public class LatentUpscaleModeInfo
    {
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Upscaler情報を管理するクラス
    /// </summary>
    public class UpscalerInfo
    {
        public string Name { get; set; } = "";
        public string Model_name { get; set; } = "";
        public string Model_path { get; set; } = "";
        public string Model_url { get; set; } = "";
        public double Scale { get; set; } = 1.0;
    }

    /// <summary>
    /// Upscalerリストを管理するマネージャー
    /// アプリケーション全体で一度だけAPIから取得し、全てのタブで共有する
    /// </summary>
    public class UpscalerManager
    {
        private static UpscalerManager? _instance;
        private static readonly object _lock = new object();

        private readonly OneShotApi<List<string>> _oneShotApi;
        private readonly object _initLock = new object();

        public static UpscalerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new UpscalerManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private UpscalerManager()
        {
            // OneShotApiを初期化
            _oneShotApi = new OneShotApi<List<string>>(FetchUpscalersFromApiAsync);
        }

        /// <summary>
        /// Upscalerリストを取得（初回はAPIから取得、以降はキャッシュを返す）
        /// </summary>
        public async Task<List<string>> GetUpscalerNamesAsync()
        {
            try
            {
                return await _oneShotApi.GetAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpscalerAPI取得エラー: {ex.Message}");
                // エラーの場合はフォールバックリストを返す
                return GetFallbackUpscalers();
            }
        }

        /// <summary>
        /// 指定されたUpscalerがリストに存在するかチェックし、なければ追加
        /// </summary>
        public async Task<List<string>> EnsureUpscalerExistsAsync(string upscalerName)
        {
            var upscalers = await GetUpscalerNamesAsync();
            
            if (!string.IsNullOrEmpty(upscalerName) && !upscalers.Contains(upscalerName))
            {
                // 新しいUpscalerを永続的に追加
                var newList = new List<string>(upscalers);
                newList.Add(upscalerName);
                
                // キャッシュを更新（既存のAPIデータと新しいアイテムを組み合わせ）
                _oneShotApi.UpdateCache(newList);
                
                Debug.WriteLine($"Upscalerリストに追加: {upscalerName}");
                return newList;
            }

            return upscalers;
        }

        /// <summary>
        /// APIからUpscaler情報を取得し、2つのAPIの結果を結合
        /// </summary>
        private async Task<List<string>> FetchUpscalersFromApiAsync()
        {
            Debug.WriteLine("UpscalerリストをAPIから取得開始");
            
            var combinedUpscalers = new List<string>();
            
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10); // タイムアウトは短めに設定
            
            try
            {
                // 1. Latent upscale modes を取得
                string latentUrl = App.BASE_URL + "/sdapi/v1/latent-upscale-modes";
                var latentResponse = await client.GetAsync(latentUrl);
                latentResponse.EnsureSuccessStatusCode();
                
                var latentJsonString = await latentResponse.Content.ReadAsStringAsync();
                var latentData = JsonSerializer.Deserialize<List<LatentUpscaleModeInfo>>(latentJsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (latentData != null)
                {
                    combinedUpscalers.AddRange(latentData.Select(l => l.Name));
                    Debug.WriteLine($"Latent upscale modes取得: {latentData.Count}個");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Latent upscale modes取得エラー: {ex.Message}");
            }

            try
            {
                // 2. Upscalers を取得
                string upscalersUrl = App.BASE_URL + "/sdapi/v1/upscalers";
                var upscalersResponse = await client.GetAsync(upscalersUrl);
                upscalersResponse.EnsureSuccessStatusCode();
                
                var upscalersJsonString = await upscalersResponse.Content.ReadAsStringAsync();
                var upscalersData = JsonSerializer.Deserialize<List<UpscalerInfo>>(upscalersJsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (upscalersData != null)
                {
                    combinedUpscalers.AddRange(upscalersData.Select(u => u.Name));
                    Debug.WriteLine($"Upscalers取得: {upscalersData.Count}個");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Upscalers取得エラー: {ex.Message}");
            }

            // 重複を除去（順序を保持）
            var uniqueUpscalers = new List<string>();
            foreach (var upscaler in combinedUpscalers)
            {
                if (!uniqueUpscalers.Contains(upscaler))
                {
                    uniqueUpscalers.Add(upscaler);
                }
            }

            // API呼び出しが失敗した場合や結果が空の場合はフォールバックを使用
            if (uniqueUpscalers.Count == 0)
            {
                Debug.WriteLine("API呼び出し結果が空のため、フォールバックリストを使用");
                uniqueUpscalers = GetFallbackUpscalers();
            }

            Debug.WriteLine($"UpscalerリストをAPIから取得完了: {uniqueUpscalers.Count}個");
            return uniqueUpscalers;
        }

        /// <summary>
        /// フォールバック用のUpscalerリスト
        /// </summary>
        private List<string> GetFallbackUpscalers()
        {
            return new List<string>
            {
                "Latent", 
                "Latent (antialiased)", 
                "Latent (bicubic)", 
                "Latent (bicubic antialiased)",
                "Latent (nearest)", 
                "Latent (nearest-exact)"
            };
        }

        /// <summary>
        /// 強制的にAPIから再取得（設定変更時などに使用）
        /// </summary>
        public async Task RefreshUpscalersAsync()
        {
            try
            {
                // キャッシュをリセットして再取得
                _oneShotApi.Reset();
                var upscalers = await _oneShotApi.GetAsync();
                Debug.WriteLine($"Upscalerリストを再取得完了: {upscalers.Count}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Upscaler再取得エラー: {ex.Message}");
                // エラーの場合は既存のキャッシュを保持
            }
        }
    }
} 