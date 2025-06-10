using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace SD.Yuzu
{
    /// <summary>
    /// タグ、LoRA、ワイルドカードデータを一元管理するSingletonクラス
    /// 複数のAutoCompleteTextBoxインスタンスで共有される
    /// </summary>
    public sealed class TagDataManager
    {
        private static TagDataManager? _instance;
        private static readonly object _lock = new object();
        
        // Loading状態を管理するフィールド
        private static bool _isLoading = false;
        private static bool _isLoaded = false;
        
        // データ格納用フィールド
        private List<TagItem> _allTags = new List<TagItem>();
        private List<TagItem> _loraFiles = new List<TagItem>();
        private Dictionary<string, List<string>> _wildcards = new Dictionary<string, List<string>>();
        
        // プロパティ（読み取り専用）
        public List<TagItem> AllTags => _allTags.ToList(); // 防御的コピー
        public List<TagItem> LoraFiles => _loraFiles.ToList(); // 防御的コピー
        public Dictionary<string, List<string>> Wildcards => new Dictionary<string, List<string>>(_wildcards); // 防御的コピー
        
        // Singletonインスタンス取得
        public static TagDataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new TagDataManager();
                        }
                    }
                }
                return _instance;
            }
        }
        
        // プライベートコンストラクタ
        private TagDataManager()
        {
            // 何もしない
        }
        
        /// <summary>
        /// 全てのデータを一度だけロードする
        /// 複数回呼ばれても安全（重複実行を防ぐ）
        /// </summary>
        public async Task LoadAllDataAsync()
        {
            // 既にロード済みまたはロード中の場合は何もしない
            lock (_lock)
            {
                if (_isLoaded || _isLoading)
                {
                    Debug.WriteLine("データは既にロード済みまたはロード中です");
                    return;
                }
                _isLoading = true;
            }
            
            try
            {
                Debug.WriteLine("TagDataManager: データロード開始");
                
                // 並列でロード
                var tagsTask = LoadTagsAsync();
                var loraTask = LoadLoraFilesAsync();
                var wildcardsTask = LoadWildcardsAsync();
                
                await Task.WhenAll(tagsTask, loraTask, wildcardsTask);
                
                // 先頭文字候補を生成
                GenerateInitialCharacterCandidates();
                
                lock (_lock)
                {
                    _isLoaded = true;
                    _isLoading = false;
                }
                
                Debug.WriteLine("TagDataManager: 全データロード完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: データロードエラー: {ex.Message}");
                
                lock (_lock)
                {
                    _isLoading = false;
                    // エラーが発生してもisLoadedはfalseのまま（再試行可能）
                }
                throw;
            }
        }
        
        /// <summary>
        /// データを強制的に再読み込みする
        /// </summary>
        public async Task ReloadAllDataAsync()
        {
            lock (_lock)
            {
                _isLoaded = false;
                _isLoading = false;
            }
            
            // データをクリア
            _allTags.Clear();
            _loraFiles.Clear();
            _wildcards.Clear();
            _initialCharacterCandidates.Clear();
            
            Debug.WriteLine("TagDataManager: 強制再読み込み開始");
            await LoadAllDataAsync();
        }
        
        /// <summary>
        /// データがロード完了しているかチェック
        /// </summary>
        public bool IsDataLoaded => _isLoaded;
        
        /// <summary>
        /// タグファイルを非同期でロード
        /// </summary>
        private async Task LoadTagsAsync()
        {
            try
            {
                string tagFilePath;
                
                // AutoCompleteTagFile設定を優先して使用
                if (!string.IsNullOrEmpty(AppSettings.Instance.AutoCompleteTagFile) && 
                    File.Exists(AppSettings.Instance.AutoCompleteTagFile))
                {
                    tagFilePath = AppSettings.Instance.AutoCompleteTagFile;
                }
                else
                {
                    // フォールバック：従来のデフォルトパス
                    string baseDirectory = AppSettings.Instance.StableDiffusionDirectory;
                    tagFilePath = Path.Combine(baseDirectory, "extensions", "a1111-sd-webui-tagcomplete", "tags", "danbooru.csv");
                }
                
                Debug.WriteLine($"TagDataManager: タグファイルパス: {tagFilePath}");
                
                if (File.Exists(tagFilePath))
                {
                    await Task.Run(() => LoadTagsFromFile(tagFilePath));
                }
                else
                {
                    _allTags = new List<TagItem>();
                    Debug.WriteLine($"TagDataManager: タグファイルが見つかりません: {tagFilePath}");
                }
            }
            catch (Exception ex)
            {
                _allTags = new List<TagItem>();
                Debug.WriteLine($"TagDataManager: タグファイルの読み込みエラー: {ex.Message}");
            }
        }
        
        /// <summary>
        /// タグファイルからデータを読み込み
        /// </summary>
        private void LoadTagsFromFile(string filePath)
        {
            try
            {
                Debug.WriteLine($"TagDataManager: タグファイル読み込み開始: {filePath}");
                var lines = File.ReadAllLines(filePath);
                Debug.WriteLine($"TagDataManager: ファイルから{lines.Length}行読み込み");
                
                var tags = new List<TagItem>();
                int loadedCount = 0;
                int aliasCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var parts = ParseCsvLine(line);
                    if (parts.Length >= 3)
                    {
                        if (int.TryParse(parts[2], out int count))
                        {
                            var tag = new TagItem
                            {
                                Name = parts[0],
                                Count = count
                            };
                            
                            // エイリアス情報の処理
                            if (parts.Length > 3 && !string.IsNullOrEmpty(parts[3]))
                            {
                                string aliasString = parts[3].Trim('"');
                                var aliases = aliasString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                        .Select(a => a.Trim())
                                                        .Where(a => !string.IsNullOrWhiteSpace(a))
                                                        .ToList();
                                        
                                tag.Aliases = aliases;
                                
                                // 各エイリアスに対してエイリアスアイテムを作成
                                foreach (var alias in aliases)
                                {
                                    if (alias != parts[0]) // 自分自身は除外
                                    {
                                        var aliasTag = new TagItem
                                        { 
                                            Name = alias, 
                                            Count = count,
                                            AliasSource = parts[0] // 元のタグ名を設定
                                        };
                                        tags.Add(aliasTag);
                                        aliasCount++;
                                    }
                                }
                            }
                            
                            tags.Add(tag);
                            loadedCount++;
                        }
                    }
                }
                
                _allTags = tags.OrderByDescending(t => t.Count).ToList();
                Debug.WriteLine($"TagDataManager: タグファイル読み込み完了: {loadedCount}個の元タグ + {aliasCount}個のエイリアス = 合計{_allTags.Count}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: タグファイル読み込みエラー: {ex.Message}");
                _allTags = new List<TagItem>();
            }
        }
        
        /// <summary>
        /// CSVラインをパースする
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField);
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            result.Add(currentField);
            return result.ToArray();
        }
        
        /// <summary>
        /// LoRAファイルを非同期でロード
        /// </summary>
        private async Task LoadLoraFilesAsync()
        {
            try
            {
                // 設定からLoRAディレクトリを取得
                string loraDirectory = AppSettings.Instance.LoraDirectory;
                
                if (Directory.Exists(loraDirectory))
                {
                    await Task.Run(() => LoadLoraFilesFromDirectory(loraDirectory));
                }
                else
                {
                    Debug.WriteLine($"TagDataManager: LoRAディレクトリが見つかりません: {loraDirectory}");
                    _loraFiles = new List<TagItem>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: LoRAファイルの読み込みエラー: {ex.Message}");
                _loraFiles = new List<TagItem>();
            }
        }
        
        /// <summary>
        /// LoRAファイルをディレクトリから読み込み
        /// </summary>
        private void LoadLoraFilesFromDirectory(string directoryPath)
        {
            try
            {
                var loraFiles = new List<TagItem>();
                var files = Directory.GetFiles(directoryPath, "*.safetensors", SearchOption.AllDirectories)
                           .Concat(Directory.GetFiles(directoryPath, "*.ckpt", SearchOption.AllDirectories))
                           .Concat(Directory.GetFiles(directoryPath, "*.pt", SearchOption.AllDirectories));

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    loraFiles.Add(new TagItem
                    {
                        Name = fileName,
                        Count = 0,
                        IsLora = true  // LoRAファイルであることを示すフラグを設定
                    });
                }

                _loraFiles = loraFiles.OrderBy(l => l.Name).ToList();
                Debug.WriteLine($"TagDataManager: LoRAファイル読み込み完了: {_loraFiles.Count}個");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: LoRAディレクトリ読み込みエラー: {ex.Message}");
                _loraFiles = new List<TagItem>();
            }
        }
        
        /// <summary>
        /// ワイルドカードファイルを非同期でロード
        /// </summary>
        private async Task LoadWildcardsAsync()
        {
            try
            {
                // 設定からベースディレクトリを取得してワイルドカードディレクトリのパスを構築
                string baseDirectory = AppSettings.Instance.StableDiffusionDirectory;
                string wildcardsDirectory = Path.Combine(baseDirectory, "extensions", "sd-dynamic-prompts", "wildcards");
                
                Debug.WriteLine($"TagDataManager: ワイルドカードディレクトリ: {wildcardsDirectory}");
                
                if (Directory.Exists(wildcardsDirectory))
                {
                    await Task.Run(() => LoadWildcardsFromDirectory(wildcardsDirectory));
                }
                else
                {
                    Debug.WriteLine($"TagDataManager: ワイルドカードディレクトリが見つかりません: {wildcardsDirectory}");
                    _wildcards = new Dictionary<string, List<string>>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: ワイルドカード読み込みエラー: {ex.Message}");
                _wildcards = new Dictionary<string, List<string>>();
            }
        }
        
        /// <summary>
        /// ワイルドカードファイルをディレクトリから読み込み
        /// </summary>
        private void LoadWildcardsFromDirectory(string directoryPath)
        {
            try
            {
                var wildcards = new Dictionary<string, List<string>>();
                
                var txtFiles = Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories);
                
                Debug.WriteLine($"TagDataManager: 発見された.txtファイル数: {txtFiles.Length}");
                
                foreach (var filePath in txtFiles)
                {
                    try
                    {
                        string wildcardName = Path.GetFileNameWithoutExtension(filePath);
                        
                        var lines = File.ReadAllLines(filePath)
                                       .Where(line => !string.IsNullOrWhiteSpace(line))
                                       .Select(line => line.Trim())
                                       .ToList();
                        
                        wildcards[wildcardName] = lines;
                        
                    }
                    catch (Exception fileEx)
                    {
                        Debug.WriteLine($"TagDataManager: ファイル読み込みエラー {filePath}: {fileEx.Message}");
                    }
                }
                
                _wildcards = wildcards;
                Debug.WriteLine($"TagDataManager: ワイルドカード読み込み完了: {_wildcards.Count}個のワイルドカード");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: ワイルドカードディレクトリ読み込みエラー: {ex.Message}");
                _wildcards = new Dictionary<string, List<string>>();
            }
        }

        // 先頭文字候補を格納するフィールド
        private Dictionary<string, List<TagItem>> _initialCharacterCandidates = new Dictionary<string, List<TagItem>>();
        
        /// <summary>
        /// 先頭文字候補を取得
        /// </summary>
        public Dictionary<string, List<TagItem>> InitialCharacterCandidates => new Dictionary<string, List<TagItem>>(_initialCharacterCandidates);
        
        /// <summary>
        /// 2単語以上のタグから単語の先頭文字を結合した候補を生成
        /// 例: "cowboy shot" -> "cs", "old school swimsuit" -> "oss"
        /// </summary>
        private void GenerateInitialCharacterCandidates()
        {
            try
            {
                Debug.WriteLine("TagDataManager: 先頭文字候補生成開始");
                
                var candidates = new Dictionary<string, List<TagItem>>();
                
                // 通常のタグから候補を生成（エイリアスは除外）
                foreach (var tag in _allTags.Where(t => string.IsNullOrEmpty(t.AliasSource)))
                {
                    var initialChars = GenerateInitialCharacters(tag.Name);
                    if (!string.IsNullOrEmpty(initialChars))
                    {
                        if (!candidates.ContainsKey(initialChars))
                        {
                            candidates[initialChars] = new List<TagItem>();
                        }
                        candidates[initialChars].Add(tag);
                    }
                }
                
                // 各候補リストを使用頻度順にソート
                foreach (var kvp in candidates)
                {
                    kvp.Value.Sort((a, b) => b.Count.CompareTo(a.Count));
                }
                
                _initialCharacterCandidates = candidates;
                
                Debug.WriteLine($"TagDataManager: 先頭文字候補生成完了: {candidates.Count}個の候補パターン");
                
                // デバッグ用：いくつかの例を出力
                var examples = candidates.Take(10).ToList();
                foreach (var example in examples)
                {
                    var topTags = example.Value.Take(3).Select(t => t.Name).ToList();
                    Debug.WriteLine($"  {example.Key} -> {string.Join(", ", topTags)}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TagDataManager: 先頭文字候補生成エラー: {ex.Message}");
                _initialCharacterCandidates = new Dictionary<string, List<TagItem>>();
            }
        }
        
        /// <summary>
        /// タグ名から単語の先頭文字を結合した文字列を生成
        /// 2単語以上の場合のみ生成、1単語の場合はnullを返す
        /// </summary>
        /// <param name="tagName">タグ名</param>
        /// <returns>先頭文字を結合した文字列、または null</returns>
        private string? GenerateInitialCharacters(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return null;
                
            // スペース、アンダースコア、ハイフンで単語を分割
            var words = tagName.Split(new char[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 2単語未満の場合は候補を生成しない
            if (words.Length < 2)
                return null;
                
            // 各単語の先頭文字を取得して結合
            var initialChars = new List<char>();
            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    initialChars.Add(char.ToLower(word[0]));
                }
            }
            
            // 2文字未満の場合は候補を生成しない
            if (initialChars.Count < 2)
                return null;
                
            return new string(initialChars.ToArray());
        }
        
        /// <summary>
        /// 指定された先頭文字パターンに一致する候補を取得
        /// </summary>
        /// <param name="pattern">先頭文字パターン（例: "cs"）</param>
        /// <param name="maxResults">最大結果数</param>
        /// <returns>一致する候補のリスト</returns>
        public List<TagItem> GetInitialCharacterMatches(string pattern, int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return new List<TagItem>();
                
            var normalizedPattern = pattern.ToLower();
            
            if (_initialCharacterCandidates.TryGetValue(normalizedPattern, out var matches))
            {
                return matches.Take(maxResults).ToList();
            }
            
            return new List<TagItem>();
        }
    }
} 