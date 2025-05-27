using System;
using System.Threading;
using System.Threading.Tasks;

namespace SD.Yuzu
{
    /// <summary>
    /// API呼び出しの重複実行を防ぐためのクラス
    /// 最初の呼び出しのみが実際にAPIを実行し、後続の呼び出しは同じ結果を共有する
    /// </summary>
    /// <typeparam name="T">API呼び出しの戻り値の型</typeparam>
    public class OneShotApi<T>
    {
        private TaskCompletionSource<T>? _tcs;
        private readonly Func<Task<T>> _apiCall;
        private readonly object _lock = new object();

        public OneShotApi(Func<Task<T>> apiCall)
        {
            _apiCall = apiCall ?? throw new ArgumentNullException(nameof(apiCall));
        }

        /// <summary>
        /// API呼び出しを実行します。複数のスレッドから同時に呼ばれても、実際のAPI呼び出しは一度だけ実行されます。
        /// </summary>
        /// <returns>API呼び出しの結果</returns>
        public async Task<T> GetAsync()
        {
            TaskCompletionSource<T>? existing = _tcs;
            
            if (existing == null)
            {
                lock (_lock)
                {
                    existing = _tcs;
                    if (existing == null)
                    {
                        // 最初のスレッドだけがここに入る
                        existing = new TaskCompletionSource<T>();
                        _tcs = existing;
                        
                        // バックグラウンドでAPI呼び出しを実行
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var result = await _apiCall();
                                existing.SetResult(result);
                            }
                            catch (Exception ex)
                            {
                                existing.SetException(ex);
                            }
                        });
                    }
                }
            }

            return await existing.Task;
        }

        /// <summary>
        /// キャッシュされた結果をクリアして、次回の呼び出し時に再度API呼び出しを実行するようにします
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _tcs = null;
            }
        }

        /// <summary>
        /// API呼び出しが既に実行済みかどうかを確認します
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                var tcs = _tcs;
                return tcs?.Task.IsCompleted ?? false;
            }
        }

        /// <summary>
        /// API呼び出しが成功した場合の結果を同期的に取得します（まだ完了していない場合はnullを返します）
        /// </summary>
        public T? GetResultIfAvailable()
        {
            var tcs = _tcs;
            if (tcs?.Task.IsCompletedSuccessfully == true)
            {
                return tcs.Task.Result;
            }
            return default(T);
        }

        /// <summary>
        /// キャッシュされた結果を外部から更新します
        /// </summary>
        /// <param name="newValue">新しい値</param>
        public void UpdateCache(T newValue)
        {
            lock (_lock)
            {
                _tcs = new TaskCompletionSource<T>();
                _tcs.SetResult(newValue);
            }
        }
    }
} 