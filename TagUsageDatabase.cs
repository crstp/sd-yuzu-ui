using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Reflection;

namespace SD.Yuzu
{
    /// <summary>
    /// タグの使用回数を記録・管理するSQLiteデータベースクラス
    /// a1111-sd-webui-tagcompleteの実装を参考にした使用頻度ベースの並べ替え機能を提供
    /// </summary>
    public sealed class TagUsageDatabase
    {
        private static TagUsageDatabase? _instance;
        private static readonly object _lock = new object();
        
        private readonly string _databasePath;
        private readonly string _connectionString;
        
        // Singletonインスタンス取得
        public static TagUsageDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new TagUsageDatabase();
                        }
                    }
                }
                return _instance;
            }
        }
        
        private TagUsageDatabase()
        {
            // 実行ファイルのディレクトリ下にsettingsフォルダを作成
            string exeDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
            string settingsDirectory = Path.Combine(exeDirectory, "settings");
            
            // settingsディレクトリが存在しない場合は作成
            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
                Debug.WriteLine($"TagUsageDatabase: settingsディレクトリを作成しました: {settingsDirectory}");
            }
            
            _databasePath = Path.Combine(settingsDirectory, "tag_usage.db");
            _connectionString = $"Data Source={_databasePath}";
            
            Debug.WriteLine($"TagUsageDatabase: データベースパス: {_databasePath}");
        }
        
        /// <summary>
        /// データベースを初期化（テーブル作成）
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // タグ使用回数テーブルの作成
                var createTableCommand = connection.CreateCommand();
                createTableCommand.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tag_usage (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        tag_name TEXT NOT NULL,
                        tag_type INTEGER NOT NULL DEFAULT 0,
                        positive_count INTEGER NOT NULL DEFAULT 0,
                        negative_count INTEGER NOT NULL DEFAULT 0,
                        last_used_date TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        created_date TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UNIQUE(tag_name, tag_type)
                    )";
                await createTableCommand.ExecuteNonQueryAsync();
                
                // インデックスの作成（検索性能向上）
                var createIndexCommand = connection.CreateCommand();
                createIndexCommand.CommandText = @"
                    CREATE INDEX IF NOT EXISTS idx_tag_usage_name_type 
                    ON tag_usage(tag_name, tag_type)";
                await createIndexCommand.ExecuteNonQueryAsync();
                
                var createUsageIndexCommand = connection.CreateCommand();
                createUsageIndexCommand.CommandText = @"
                    CREATE INDEX IF NOT EXISTS idx_tag_usage_counts 
                    ON tag_usage(positive_count DESC, negative_count DESC, last_used_date DESC)";
                await createUsageIndexCommand.ExecuteNonQueryAsync();
                
                Debug.WriteLine("TagUsageDatabase: データベース初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: データベース初期化エラー: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// タグの使用回数を増加
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <param name="tagType">タグタイプ（0=通常タグ、1=LoRA、2=ワイルドカードなど）</param>
        /// <param name="isNegative">ネガティブプロンプトでの使用かどうか</param>
        public async Task IncreaseUsageCountAsync(string tagName, int tagType = 0, bool isNegative = false)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return;
                
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                if (isNegative)
                {
                    command.CommandText = @"
                        INSERT INTO tag_usage (tag_name, tag_type, negative_count, last_used_date)
                        VALUES (@tagName, @tagType, 1, @currentTime)
                        ON CONFLICT(tag_name, tag_type) DO UPDATE SET
                            negative_count = negative_count + 1,
                            last_used_date = @currentTime";
                }
                else
                {
                    command.CommandText = @"
                        INSERT INTO tag_usage (tag_name, tag_type, positive_count, last_used_date)
                        VALUES (@tagName, @tagType, 1, @currentTime)
                        ON CONFLICT(tag_name, tag_type) DO UPDATE SET
                            positive_count = positive_count + 1,
                            last_used_date = @currentTime";
                }
                
                command.Parameters.AddWithValue("@tagName", tagName);
                command.Parameters.AddWithValue("@tagType", tagType);
                command.Parameters.AddWithValue("@currentTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                
                await command.ExecuteNonQueryAsync();
                
                Debug.WriteLine($"TagUsageDatabase: 使用回数を増加しました - {tagName} (type: {tagType}, negative: {isNegative})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: 使用回数増加エラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 特定のタグの使用回数を取得
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <param name="tagType">タグタイプ</param>
        /// <returns>使用回数情報</returns>
        public async Task<TagUsageInfo?> GetUsageCountAsync(string tagName, int tagType = 0)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return null;
                
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT positive_count, negative_count, last_used_date
                    FROM tag_usage
                    WHERE tag_name = @tagName AND tag_type = @tagType";
                
                command.Parameters.AddWithValue("@tagName", tagName);
                command.Parameters.AddWithValue("@tagType", tagType);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new TagUsageInfo
                    {
                        TagName = tagName,
                        TagType = tagType,
                        PositiveCount = reader.GetInt32(reader.GetOrdinal("positive_count")),
                        NegativeCount = reader.GetInt32(reader.GetOrdinal("negative_count")),
                        LastUsedDate = DateTime.TryParse(reader.GetString(reader.GetOrdinal("last_used_date")), out var date) ? date : DateTime.MinValue
                    };
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: 使用回数取得エラー: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 複数のタグの使用回数を一括取得
        /// </summary>
        /// <param name="tagNames">タグ名のリスト</param>
        /// <param name="tagType">タグタイプ</param>
        /// <returns>使用回数情報のディクショナリ</returns>
        public async Task<Dictionary<string, TagUsageInfo>> GetUsageCountsAsync(List<string> tagNames, int tagType = 0)
        {
            var result = new Dictionary<string, TagUsageInfo>();
            
            if (tagNames == null || tagNames.Count == 0)
                return result;
                
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                // IN句用のパラメータを構築
                var parameters = new List<string>();
                var command = connection.CreateCommand();
                
                for (int i = 0; i < tagNames.Count; i++)
                {
                    var paramName = $"@tag{i}";
                    parameters.Add(paramName);
                    command.Parameters.AddWithValue(paramName, tagNames[i]);
                }
                
                command.CommandText = $@"
                    SELECT tag_name, positive_count, negative_count, last_used_date
                    FROM tag_usage
                    WHERE tag_name IN ({string.Join(",", parameters)}) AND tag_type = @tagType";
                
                command.Parameters.AddWithValue("@tagType", tagType);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tagName = reader.GetString(reader.GetOrdinal("tag_name"));
                    result[tagName] = new TagUsageInfo
                    {
                        TagName = tagName,
                        TagType = tagType,
                        PositiveCount = reader.GetInt32(reader.GetOrdinal("positive_count")),
                        NegativeCount = reader.GetInt32(reader.GetOrdinal("negative_count")),
                        LastUsedDate = DateTime.TryParse(reader.GetString(reader.GetOrdinal("last_used_date")), out var date) ? date : DateTime.MinValue
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: 一括使用回数取得エラー: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 使用頻度に基づいてタグの優先度を計算
        /// a1111-sd-webui-tagcompleteの実装を参考
        /// </summary>
        /// <param name="originalCount">元のタグカウント（投稿数など）</param>
        /// <param name="usageCount">使用回数</param>
        /// <param name="frequencyFunction">計算方式</param>
        /// <param name="minUsageCount">最小使用回数（これ以下は無視）</param>
        /// <returns>計算された優先度</returns>
        public static double CalculateUsageBias(int originalCount, int usageCount, FrequencyFunction frequencyFunction = FrequencyFunction.Logarithmic, int minUsageCount = 3)
        {
            double basePriority = Math.Log(1 + originalCount); // 元のカウントの対数
            
            // 使用回数が最小使用回数に満たない場合は元の優先度をそのまま返す
            if (usageCount < minUsageCount)
            {
                return basePriority;
            }
            
            // 使用回数に基づくボーナス計算
            double usageBonus = frequencyFunction switch
            {
                FrequencyFunction.Logarithmic => Math.Log(1 + usageCount) * 2.0, // 使用回数の対数 × 2
                FrequencyFunction.LogarithmicStrong => Math.Log(1 + usageCount) * 4.0, // 使用回数の対数 × 4
                FrequencyFunction.UsageFirst => usageCount * 0.5, // 使用回数 × 0.5
                FrequencyFunction.CountFirst => Math.Log(1 + usageCount) * 0.5, // 使用回数の対数 × 0.5
                _ => Math.Log(1 + usageCount) * 2.0
            };
            
            double finalPriority = basePriority + usageBonus;
            
            // デバッグログ出力
            if (usageCount >= minUsageCount)
            {
                Debug.WriteLine($"優先度計算: originalCount={originalCount}, usageCount={usageCount}, basePriority={basePriority:F2}, usageBonus={usageBonus:F2}, finalPriority={finalPriority:F2}");
            }
            
            return finalPriority;
        }
        
        /// <summary>
        /// 使用回数をリセット
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <param name="tagType">タグタイプ</param>
        /// <param name="resetPositive">ポジティブカウントをリセットするか</param>
        /// <param name="resetNegative">ネガティブカウントをリセットするか</param>
        public async Task ResetUsageCountAsync(string tagName, int tagType = 0, bool resetPositive = true, bool resetNegative = true)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return;
                
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                
                if (resetPositive && resetNegative)
                {
                    command.CommandText = @"
                        DELETE FROM tag_usage 
                        WHERE tag_name = @tagName AND tag_type = @tagType";
                }
                else if (resetPositive)
                {
                    command.CommandText = @"
                        UPDATE tag_usage 
                        SET positive_count = 0 
                        WHERE tag_name = @tagName AND tag_type = @tagType";
                }
                else if (resetNegative)
                {
                    command.CommandText = @"
                        UPDATE tag_usage 
                        SET negative_count = 0 
                        WHERE tag_name = @tagName AND tag_type = @tagType";
                }
                else
                {
                    return; // 何もリセットしない場合
                }
                
                command.Parameters.AddWithValue("@tagName", tagName);
                command.Parameters.AddWithValue("@tagType", tagType);
                
                await command.ExecuteNonQueryAsync();
                
                Debug.WriteLine($"TagUsageDatabase: 使用回数をリセットしました - {tagName} (type: {tagType})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: 使用回数リセットエラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 全ての使用回数データを取得（管理画面用）
        /// </summary>
        /// <returns>全ての使用回数情報</returns>
        public async Task<List<TagUsageInfo>> GetAllUsageCountsAsync()
        {
            var result = new List<TagUsageInfo>();
            
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT tag_name, tag_type, positive_count, negative_count, last_used_date
                    FROM tag_usage
                    ORDER BY (positive_count + negative_count) DESC, last_used_date DESC";
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new TagUsageInfo
                    {
                        TagName = reader.GetString(reader.GetOrdinal("tag_name")),
                        TagType = reader.GetInt32(reader.GetOrdinal("tag_type")),
                        PositiveCount = reader.GetInt32(reader.GetOrdinal("positive_count")),
                        NegativeCount = reader.GetInt32(reader.GetOrdinal("negative_count")),
                        LastUsedDate = DateTime.TryParse(reader.GetString(reader.GetOrdinal("last_used_date")), out var date) ? date : DateTime.MinValue
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagUsageDatabase: 全使用回数取得エラー: {ex.Message}");
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// タグの使用回数情報
    /// </summary>
    public class TagUsageInfo
    {
        public string TagName { get; set; } = "";
        public int TagType { get; set; } = 0;
        public int PositiveCount { get; set; } = 0;
        public int NegativeCount { get; set; } = 0;
        public DateTime LastUsedDate { get; set; } = DateTime.MinValue;
        
        /// <summary>
        /// 総使用回数
        /// </summary>
        public int TotalCount => PositiveCount + NegativeCount;
        
        /// <summary>
        /// 使用頻度が高いかどうか（表示用）
        /// </summary>
        public bool IsFrequentlyUsed => TotalCount >= 3;
    }
    
    /// <summary>
    /// 頻度計算方式の列挙型
    /// </summary>
    public enum FrequencyFunction
    {
        /// <summary>対数関数（弱）</summary>
        Logarithmic,
        /// <summary>対数関数（強）</summary>
        LogarithmicStrong,
        /// <summary>使用回数優先</summary>
        UsageFirst,
        /// <summary>元のカウント優先</summary>
        CountFirst
    }
} 