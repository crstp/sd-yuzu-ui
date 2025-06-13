using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Data;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;

namespace SD.Yuzu
{
    /// <summary>
    /// AvalonEditベースのオートコンプリート機能付きテキストボックス
    /// 高性能な検索ハイライト、オートコンプリート、構文チェック機能を提供
    /// </summary>
    public partial class AutoCompleteTextBox : UserControl
    {
        // 全てのインスタンスを管理する静的リスト
        private static readonly List<WeakReference> _allInstances = new List<WeakReference>();
        
        // 検索機能が有効かどうかを管理する静的フラグ
        private static bool _isSearchModeActive = false;
        private static bool _searchBoxHasFocus = false;
        
        private DispatcherTimer _searchTimer = null!;
        private string _currentWord = "";
        private int _currentWordStart = 0;
        private bool _isAdjustingWeight = false;
        private bool _isInsertingTag = false;
        private string _previousText = "";
        private bool _isInitialized = false;
        private bool _isUpdatingText = false;
        
        // 削除操作と重み調整後の制御用フラグ
        private bool _justPerformedDeletion = false;
        private bool _justAdjustedWeight = false;
        private DispatcherTimer? _suppressTimer = null;
        
        // AvalonEdit関連
        private CompletionWindow? _completionWindow;
        private SearchPanel? _searchPanel;
        
        // 検索時のキャレット位置制御用
        private int _searchStartCaretPosition = 0;
        private bool _isSearchActive = false;
        private bool _isMouseClicking = false;

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(AutoCompleteTextBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AutoCompleteTextBox autoCompleteTextBox && !autoCompleteTextBox._isUpdatingText)
            {
                string? newText = e.NewValue as string;
                if (newText != null)
                {
                    // テキスト置き換え前にキャレット位置を記録
                    int caretPosition = autoCompleteTextBox.GetTextEditorCaretPosition();
                    int textLength = autoCompleteTextBox.GetTextEditorText().Length;
                    double caretPositionRatio = textLength > 0 ? (double)caretPosition / textLength : 0;
                    
                    autoCompleteTextBox._isUpdatingText = true;
                    try
                    {
                        autoCompleteTextBox.SetTextEditorText(newText);
                        
                        // テキスト置き換え後にキャレット位置を復元
                        // テキストの長さが変わった場合は相対的な位置を維持
                        int newLength = newText.Length;
                        int newCaretPosition = (int)(caretPositionRatio * newLength);
                        // 範囲を確認して安全な位置に設定
                        newCaretPosition = Math.Max(0, Math.Min(newCaretPosition, newLength));
                        autoCompleteTextBox.SetTextEditorCaretPosition(newCaretPosition);
                    }
                    finally
                    {
                        autoCompleteTextBox._isUpdatingText = false;
                    }
                }
            }
        }

        public AutoCompleteTextBox()
        {
            InitializeComponent();
            
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1)
            };
            _searchTimer.Tick += SearchTimer_Tick;

            // サプレッションタイマーの初期化
            _suppressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300) // 300ms間オートコンプリートを抑制
            };
            _suppressTimer.Tick += (s, e) =>
            {
                _suppressTimer.Stop();
                _justPerformedDeletion = false;
                _justAdjustedWeight = false;
            };

            Loaded += AutoCompleteTextBox_Loaded;
            
            // Loadedイベントで安全に初期化
            Loaded += (s, e) =>
            {
                InitializeAvalonEdit();
                _previousText = GetTextEditorText();
                _isInitialized = true;
                Debug.WriteLine("AutoCompleteTextBox (AvalonEdit) 初期化完了");
                
                string currentText = GetTextEditorText();
                if (!string.IsNullOrEmpty(currentText))
                {
                    ValidateParentheses(currentText);
                    Debug.WriteLine("初期化時の構文チェック実行");
                }
            };
            
            // ウィンドウのアクティブ状態変更を監視
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Deactivated += MainWindow_Deactivated;
            }
            
            // インスタンスを静的リストに追加
            _allInstances.Add(new WeakReference(this));
            CleanupWeakReferences();
        }

        /// <summary>
        /// AvalonEditの初期化と設定
        /// </summary>
        private void InitializeAvalonEdit()
        {
            // 検索パネルの設定
            _searchPanel = SearchPanel.Install(MainTextEditor);
            
            // 検索時のキャレット制御のため、TextAreaのイベントを監視
            if (MainTextEditor.TextArea != null)
            {
                // キャレット位置変更イベントを監視
                MainTextEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
                
                // マウスクリックイベントを監視
                MainTextEditor.TextArea.TextView.MouseDown += TextView_MouseDown;
            }
            
            // 検索パネルのTextBoxのフォーカスイベントを監視
            if (_searchPanel != null)
            {
                // 検索パネルが表示された後にTextBoxを探してイベントを設定
                _searchPanel.Loaded += (sender, e) =>
                {
                    SetupSearchPanelEvents();
                };
                
                // 既に表示されている場合は即座に設定
                if (_searchPanel.IsLoaded)
                {
                    SetupSearchPanelEvents();
                }
            }
            
            // テキスト変更イベントの設定
            MainTextEditor.TextChanged += AvalonEdit_TextChanged;
            
            // コンテキストメニューの設定
            SetupContextMenu();
            
            // その他の設定
            MainTextEditor.Options.EnableRectangularSelection = false;
            MainTextEditor.Options.EnableTextDragDrop = true;
            MainTextEditor.Options.ShowBoxForControlCharacters = false;
            MainTextEditor.Options.ShowSpaces = false;
            MainTextEditor.Options.ShowTabs = false;
        }

        /// <summary>
        /// キャレット位置変更イベントハンドラー
        /// </summary>
        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            // マウスクリック中またはフォーカスがメインエディタにない場合は復元しない
            if (_isMouseClicking || !MainTextEditor.IsFocused)
            {
                return;
            }
            
            // 検索中で、検索パネルが開いている場合はキャレット位置を復元
            if (_isSearchActive && _searchPanel != null && _searchPanel.IsVisible)
            {
                int currentPosition = MainTextEditor.CaretOffset;
                if (currentPosition != _searchStartCaretPosition)
                {
                    // 少し遅延させてキャレット位置を復元
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_isSearchActive && !_isMouseClicking && MainTextEditor.IsFocused && 
                            MainTextEditor.CaretOffset != _searchStartCaretPosition)
                        {
                            MainTextEditor.CaretOffset = _searchStartCaretPosition;
                            Debug.WriteLine($"キャレット位置を自動復元: {_searchStartCaretPosition}");
                        }
                    }), DispatcherPriority.Background);
                }
            }
        }

        /// <summary>
        /// TextView マウスダウンイベントハンドラー
        /// </summary>
        private void TextView_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            _isMouseClicking = true;
            Debug.WriteLine("マウスクリック検出 - キャレット復元を一時停止");
            
            // 少し遅延してフラグをリセット
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _isMouseClicking = false;
                Debug.WriteLine("マウスクリック処理完了");
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 検索パネルのTextBoxイベントを設定
        /// </summary>
        private void SetupSearchPanelEvents()
        {
            if (_searchPanel == null) return;
            
            try
            {
                // 検索パネル内のTextBoxを探す
                var searchTextBox = FindVisualChild<TextBox>(_searchPanel);
                if (searchTextBox != null)
                {
                    Debug.WriteLine("検索パネルのTextBoxを発見、イベント設定中...");
                    
                    // フォーカスイベントを設定
                    searchTextBox.GotFocus += SearchPanelTextBox_GotFocus;
                    searchTextBox.LostFocus += SearchPanelTextBox_LostFocus;
                    
                    Debug.WriteLine("検索パネルTextBoxのフォーカスイベント設定完了");
                }
                else
                {
                    Debug.WriteLine("検索パネル内のTextBoxが見つかりませんでした");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"検索パネルイベント設定エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 検索パネルのTextBoxがフォーカスを取得した時
        /// </summary>
        private void SearchPanelTextBox_GotFocus(object? sender, RoutedEventArgs e)
        {
            SetSearchBoxFocus(true);
            // 検索ボックスにフォーカスが移った時は既存のオートコンプリートウィンドウを閉じる
            _completionWindow?.Close();
            Debug.WriteLine("検索パネルTextBoxフォーカス取得 - オートコンプリート無効化");
        }

        /// <summary>
        /// 検索パネルのTextBoxがフォーカスを失った時
        /// </summary>
        private void SearchPanelTextBox_LostFocus(object? sender, RoutedEventArgs e)
        {
            SetSearchBoxFocus(false);
            Debug.WriteLine("検索パネルTextBoxフォーカス失失 - オートコンプリート有効化");
        }

        /// <summary>
        /// 子要素のTextBoxを探すヘルパーメソッド
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result)
                {
                    return result;
                }
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        /// <summary>
        /// コンテキストメニューの設定
        /// </summary>
        private void SetupContextMenu()
        {
            var contextMenu = new ContextMenu();
            
            var cutItem = new MenuItem { Header = "切り取り(_T)" };
            cutItem.Click += (s, e) => MainTextEditor.Cut();
            contextMenu.Items.Add(cutItem);
            
            var copyItem = new MenuItem { Header = "コピー(_C)" };
            copyItem.Click += (s, e) => MainTextEditor.Copy();
            contextMenu.Items.Add(copyItem);
            
            var pasteItem = new MenuItem { Header = "貼り付け(_P)" };
            pasteItem.Click += (s, e) => MainTextEditor.Paste();
            contextMenu.Items.Add(pasteItem);
            
            contextMenu.Items.Add(new Separator());
            
            var selectAllItem = new MenuItem { Header = "すべて選択(_A)" };
            selectAllItem.Click += (s, e) => MainTextEditor.SelectAll();
            contextMenu.Items.Add(selectAllItem);
            
            MainTextEditor.ContextMenu = contextMenu;
        }

        /// <summary>
        /// AvalonEditのテキスト変更イベント処理
        /// </summary>
        private void AvalonEdit_TextChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingText || !_isInitialized) return;
            
            try
            {
                string currentText = GetTextEditorText();
                
                // 自動フォーマットは無効化
                
                // 依存関係プロパティの更新
                _isUpdatingText = true;
                SetValue(TextProperty, currentText);
                
                // 構文チェック
                ValidateParentheses(currentText);
                
                // オートコンプリートの処理
                if (!_isInsertingTag && !ShouldSuppressAutoComplete())
                {
                    _searchTimer.Stop();
                    _searchTimer.Start();
                }
                
                _previousText = currentText;
            }
            finally
            {
                _isUpdatingText = false;
            }
        }
        
        /// <summary>
        /// テキストを手動でフォーマットする
        /// </summary>
        public void FormatText()
        {
            if (_isUpdatingText || !_isInitialized) return;
            
            try
            {
                _isUpdatingText = true;
                string currentText = GetTextEditorText();
                int originalCaretPos = GetTextEditorCaretPosition();
                
                // フォーマットを適用
                string formattedText = FormatPromptText(currentText);
                
                // テキストが変更された場合のみ更新
                if (formattedText != currentText)
                {
                    // キャレット位置を調整するために変更前と変更後のテキスト位置関係を計算
                    int newCaretPos = CalculateNewCaretPosition(currentText, formattedText, originalCaretPos);
                    
                    // ドキュメントを直接編集してUndo操作を一つの単位として扱う
                    MainTextEditor.Document.BeginUpdate();
                    try 
                    {
                        MainTextEditor.Document.Text = formattedText;
                        // 新しい計算済みキャレット位置を設定
                        SetTextEditorCaretPosition(newCaretPos);
                    }
                    finally 
                    {
                        MainTextEditor.Document.EndUpdate();
                    }
                    
                    // 依存関係プロパティの更新
                    SetValue(TextProperty, formattedText);
                }
            }
            finally
            {
                _isUpdatingText = false;
            }
        }

        /// <summary>
        /// 元のテキストとフォーマット後のテキストから、新しいキャレット位置を計算
        /// </summary>
        private int CalculateNewCaretPosition(string originalText, string formattedText, int originalCaretPos)
        {
            if (originalCaretPos <= 0)
                return 0;
                
            if (originalCaretPos >= originalText.Length)
                return formattedText.Length;
                
            // 元のテキストでのキャレット位置までのテキストと、その後の部分に分割
            string beforeCaret = originalText.Substring(0, originalCaretPos);
            string afterCaret = originalText.Substring(originalCaretPos);
            
            // 分割したテキストに対して同じフォーマット処理を適用
            string formattedBeforeCaret = FormatPromptText(beforeCaret);
            
            // フォーマット後のbeforeCaretの長さが新しいキャレット位置
            return formattedBeforeCaret.Length;
        }

        /// <summary>
        /// プロンプトテキストをフォーマット
        /// - 先頭のスペースとカンマを削除
        /// - カンマの前のスペースを削除
        /// - カンマの後に1つのスペースを追加
        /// - 改行は保持する
        /// - 連続したカンマを1つにする（スペースを無視）
        /// - 連続したスペースを1つにする
        /// </summary>
        private string FormatPromptText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            // 行ごとに処理して改行を保持
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                // 1. 先頭のスペースとカンマを削除
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"^[\s,]+", "");
                
                // 2. まずスペースを考慮せずに連続したカンマを1つにする
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*,\s*", ",");
                
                // 3. 残っている連続したカンマを全て1つにする（複数のカンマがあるケース用）
                int safetyCounter = 0;
                while (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\s*,\s*,") && safetyCounter < 10)
                {
                    lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*,\s*", ",");
                    safetyCounter++;
                }
                
                // 4. 最後に残ったカンマの前のスペースを削除し、後に1つのスペースを追加
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*", ", ");
                
                // 5. 連続したスペースを1つにする（カンマ周りの処理後に実行）
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @" {2,}", " ");
            }
            
            // 元の改行文字を使って結合
            string result = string.Join(Environment.NewLine, lines);
            
            return result;
        }

        /// <summary>
        /// 無効な弱参照をリストから削除
        /// </summary>
        private static void CleanupWeakReferences()
        {
            for (int i = _allInstances.Count - 1; i >= 0; i--)
            {
                if (!_allInstances[i].IsAlive)
                {
                    _allInstances.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 全てのAutoCompleteTextBoxインスタンスの候補ポップアップを閉じる
        /// </summary>
        public static void CloseAllSuggestions()
        {
            CleanupWeakReferences();
            
            foreach (var weakRef in _allInstances)
            {
                if (weakRef.Target is AutoCompleteTextBox instance)
                {
                    instance._completionWindow?.Close();
                    Debug.WriteLine("静的メソッドから候補ポップアップを閉じました");
                }
            }
        }

        private async void AutoCompleteTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // TagDataManagerを使用してデータをロード（一度だけ実行される）
            try
            {
                await TagDataManager.Instance.LoadAllDataAsync();
                Debug.WriteLine("AutoCompleteTextBox: TagDataManagerからデータロード完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoCompleteTextBox: データロードエラー: {ex.Message}");
            }
            
            // TagUsageDatabaseを初期化（一度だけ実行される）
            try
            {
                await TagUsageDatabase.Instance.InitializeDatabaseAsync();
                Debug.WriteLine("AutoCompleteTextBox: TagUsageDatabaseの初期化完了");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutoCompleteTextBox: データベース初期化エラー: {ex.Message}");
            }
        }

        private void SearchTimer_Tick(object? sender, EventArgs e)
        {
            _searchTimer.Stop();
            
            // 重み調整中やタグ挿入中はスキップ
            if (_isAdjustingWeight || _isInsertingTag)
            {
                return;
            }
            
            // 適切な検索コンテキストかチェック
            if (!ShouldPerformAutoComplete())
            {
                _completionWindow?.Close();
                return;
            }
            
            PerformSearch();
        }

        /// <summary>
        /// オートコンプリートを実行すべきかチェック
        /// </summary>
        private bool ShouldPerformAutoComplete()
        {
            // 検索ボックスにフォーカスがある場合はオートコンプリートを無効化
            if (_searchBoxHasFocus)
            {
                return false;
            }

            // 削除操作直後や重み調整直後は抑制
            if (_justPerformedDeletion || _justAdjustedWeight)
            {
                return false;
            }

            int caretPosition = GetTextEditorCaretPosition();
            string text = GetTextEditorText();

            if (string.IsNullOrEmpty(text) || caretPosition <= 0)
            {
                return false;
            }

            // 直前の文字をチェック
            char prevChar = text[caretPosition - 1];
            
            // スペース、カンマ、改行の直後は候補を表示しない
            if (prevChar == ' ' || prevChar == ',' || prevChar == '\n' || prevChar == '\r' || prevChar == '\t')
            {
                return false;
            }

            // 単語の最小長をチェック（2文字以上）
            var currentWord = GetCurrentWordAtPosition(text, caretPosition);
            if (string.IsNullOrEmpty(currentWord) || currentWord.Length < 2)
            {
                return false;
            }

            // 数字のみの単語は候補を表示しない
            if (currentWord.All(char.IsDigit))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 重み調整直後かどうかを判定（簡素化）
        /// </summary>
        private bool IsAfterWeightAdjustment(string text, int caretPosition)
        {
            // この判定は削除して、より単純な条件に変更
            // 重み調整中フラグがあるので、ここでの複雑な判定は不要
                return false;
            }

        /// <summary>
        /// 指定位置の現在の単語を取得
        /// </summary>
        private string GetCurrentWordAtPosition(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition <= 0) return "";
            
            char[] separators = { ' ', ',', '\n', '\r', '\t', '(', ')', '[', ']', '{', '}', '<', '>' };
            
            int start = caretPosition;
            while (start > 0 && !separators.Contains(text[start - 1]))
            {
                start--;
            }
            
            int end = caretPosition;
            while (end < text.Length && !separators.Contains(text[end]))
            {
                end++;
            }
            
            if (end > start)
            {
                return text.Substring(start, end - start);
            }
            
            return "";
        }

        /// <summary>
        /// オートコンプリート検索を実行
        /// </summary>
        private void PerformSearch()
        {
            try
            {
                var tagDataManager = TagDataManager.Instance;
                if (!tagDataManager.IsDataLoaded)
                {
                    Debug.WriteLine("データがまだロードされていません");
                    return;
                }

                var allTags = tagDataManager.AllTags;
                var loraFiles = tagDataManager.LoraFiles;
                var wildcards = tagDataManager.Wildcards;

                int caretPosition = GetTextEditorCaretPosition();
                string text = GetTextEditorText();

                // ワイルドカードパターンをチェック
                var wildcardPattern = CheckWildcardPattern(text, caretPosition);
                if (wildcardPattern.HasValue)
                {
                    var (startPos, searchWord, isEnclosed, wildcardName) = wildcardPattern.Value;
                    _currentWordStart = startPos;
                    _currentWord = searchWord;
                    
                    Debug.WriteLine($"ワイルドカードモード検出: 検索語='{searchWord}', 囲まれた領域={isEnclosed}, ワイルドカード名='{wildcardName}'");
                    
                    // 囲まれた領域かつ有効なワイルドカード名の場合、内容を表示
                    if (isEnclosed && !string.IsNullOrEmpty(wildcardName) && wildcards.ContainsKey(wildcardName))
                    {
                        var wildcardContents = wildcards[wildcardName]
                            .Select(content => new WildcardContentItem 
                            { 
                                Content = content,
                                WildcardName = wildcardName
                            })
                            .OrderBy(w => w.Content)
                            .Take(50) // ワイルドカード内容は多いかもしれないので50個に制限
                            .ToList();

                        if (wildcardContents.Any())
                        {
                            ShowWildcardContentCompletionWindow(wildcardContents, startPos, wildcardName);
                        }
                        else
                        {
                            _completionWindow?.Close();
                        }
                        return;
                    }
                    
                    // 通常のワイルドカード候補を検索
                    var wildcardMatches = wildcards
                        .Where(kvp => string.IsNullOrEmpty(searchWord) || 
                                     kvp.Key.Contains(searchWord, StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => new WildcardItem 
                        { 
                            Name = kvp.Key,
                            EntryCount = kvp.Value.Count
                        })
                        .OrderBy(w => w.Name)
                        .Take(30)
                        .ToList();

                    if (wildcardMatches.Any())
                    {
                        ShowWildcardCompletionWindow(wildcardMatches, startPos);
                    }
                    else
                    {
                        _completionWindow?.Close();
                    }
                    return;
                }

                // LoRAパターンをチェック
                var loraPattern = CheckLoraPattern(text, caretPosition);
                if (loraPattern.HasValue)
                {
                    var (startPos, searchWord) = loraPattern.Value;
                    _currentWordStart = startPos;
                    _currentWord = searchWord;
                    
                    Debug.WriteLine($"LoRAモード検出: 検索語='{searchWord}'");
                    
                    if (loraFiles.Any())
                    {
                        var loraMatches = loraFiles
                            .Where(lora => string.IsNullOrEmpty(searchWord) || 
                                          lora.Name.Contains(searchWord, StringComparison.OrdinalIgnoreCase))
                            .Take(30)
                            .ToList();

                        if (loraMatches.Any())
                        {
                            // LoRAタグの使用回数情報を非同期で取得して設定
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await EnrichWithUsageDataAsync(loraMatches);
                                    
                                    // UIスレッドで並べ替えと表示を実行
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            // 使用頻度ベースで並べ替え
                                            var sortedLoraMatches = loraMatches
                                                .OrderByDescending(lora => lora.CalculatedPriority)
                                                .ThenBy(lora => lora.DisplayText)
                                                .ToList();
                                            
                                            // デバッグログ：LoRA並べ替え結果を出力
                                            Debug.WriteLine("=== LoRA並べ替え結果 ===");
                                            for (int i = 0; i < Math.Min(10, sortedLoraMatches.Count); i++)
                                            {
                                                var lora = sortedLoraMatches[i];
                                                Debug.WriteLine($"{i + 1}. {lora.DisplayText} - 優先度:{lora.CalculatedPriority:F2}, 元カウント:{lora.Count}, 使用回数:{lora.UsageCount}");
                                            }
                                            Debug.WriteLine("========================");
                                            
                                            ShowCompletionWindow(sortedLoraMatches, startPos);
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"LoRA UI更新エラー: {ex.Message}");
                                            // フォールバック：元のリストで表示
                            ShowCompletionWindow(loraMatches, startPos);
                                        }
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"LoRA使用回数取得エラー: {ex.Message}");
                                    // エラーの場合は元のリストで表示
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        ShowCompletionWindow(loraMatches, startPos);
                                    });
                                }
                            });
                        }
                        else
                        {
                            _completionWindow?.Close();
                        }
                        return;
                    }
                }

                // 通常のタグ検索
                GetCurrentWord(text, caretPosition, out _currentWord, out _currentWordStart);

                if (string.IsNullOrWhiteSpace(_currentWord) || _currentWord.Length < 2)
                {
                    _completionWindow?.Close();
                    return;
                }

                // 先頭文字候補をチェック（完全一致のみ）
                var initialCharMatches = tagDataManager.GetInitialCharacterMatches(_currentWord, 20);
                if (initialCharMatches.Any())
                {
                    Debug.WriteLine($"先頭文字候補検出: '{_currentWord}' -> {initialCharMatches.Count}個の候補");
                    
                    // 使用回数情報を非同期で取得して設定
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await EnrichWithUsageDataAsync(initialCharMatches);
                            
                            // UIスレッドで並べ替えと表示を実行
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    // 使用頻度ベースで並べ替え
                                    var sortedMatches = initialCharMatches
                                        .OrderByDescending(tag => tag.CalculatedPriority)
                                        .ThenBy(tag => tag.DisplayText)
                                        .ToList();
                                    
                                    // デバッグログ：先頭文字候補並べ替え結果を出力
                                    Debug.WriteLine("=== 先頭文字候補並べ替え結果 ===");
                                    for (int i = 0; i < Math.Min(10, sortedMatches.Count); i++)
                                    {
                                        var tag = sortedMatches[i];
                                        Debug.WriteLine($"{i + 1}. {tag.DisplayText} - 優先度:{tag.CalculatedPriority:F2}, 元カウント:{tag.Count}, 使用回数:{tag.UsageCount}");
                                    }
                                    Debug.WriteLine("==================================");
                                    
                                    ShowCompletionWindow(sortedMatches, _currentWordStart);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"先頭文字候補UI更新エラー: {ex.Message}");
                                    // フォールバック：元のリストで表示
                                    ShowCompletionWindow(initialCharMatches, _currentWordStart);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"先頭文字候補使用回数取得エラー: {ex.Message}");
                            // エラーの場合は元のリストで表示
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ShowCompletionWindow(initialCharMatches, _currentWordStart);
                            });
                        }
                    });
                    return;
                }

                // 改善されたマッチングロジック（従来の検索）
                var allMatches = allTags
                    .Where(tag => IsFlexibleMatch(tag.Name, _currentWord))
                    .ToList();

                // 重複除去とsynonym除外ロジック
                var filteredMatches = FilterAndDeduplicateMatches(allMatches, _currentWord);

                if (filteredMatches.Any())
                {
                    // 使用回数情報を非同期で取得して設定
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await EnrichWithUsageDataAsync(filteredMatches);
                            
                            // UIスレッドで並べ替えと表示を実行
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    // 使用頻度ベースで並べ替え
                                    var sortedMatches = filteredMatches
                                        .OrderByDescending(tag => tag.CalculatedPriority)
                                        .ThenBy(tag => tag.DisplayText)
                                        .ToList();
                                    
                                    // デバッグログ：並べ替え結果を出力
                                    Debug.WriteLine("=== オートコンプリート並べ替え結果 ===");
                                    for (int i = 0; i < Math.Min(10, sortedMatches.Count); i++)
                                    {
                                        var tag = sortedMatches[i];
                                        Debug.WriteLine($"{i + 1}. {tag.DisplayText} - 優先度:{tag.CalculatedPriority:F2}, 元カウント:{tag.Count}, 使用回数:{tag.UsageCount}");
                                    }
                                    Debug.WriteLine("=====================================");
                                    
                                    ShowCompletionWindow(sortedMatches, _currentWordStart);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"UI更新エラー: {ex.Message}");
                                    // フォールバック：元のリストで表示
                    ShowCompletionWindow(filteredMatches, _currentWordStart);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"使用回数取得エラー: {ex.Message}");
                            // エラーの場合は元のリストで表示
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ShowCompletionWindow(filteredMatches, _currentWordStart);
                            });
                        }
                    });
                }
                else
                {
                    _completionWindow?.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"検索エラー: {ex.Message}");
                _completionWindow?.Close();
            }
        }

        /// <summary>
        /// マッチ結果のフィルタリングと重複除去
        /// </summary>
        private List<TagItem> FilterAndDeduplicateMatches(List<TagItem> matches, string searchWord)
        {
            var result = new List<TagItem>();
            var processedTags = new HashSet<string>();

            foreach (var match in matches.OrderByDescending(m => m.Count))
            {
                // 元のタグ（エイリアスでない）の場合
                if (string.IsNullOrEmpty(match.AliasSource))
                {
                    if (!processedTags.Contains(match.Name))
                    {
                        result.Add(match);
                        processedTags.Add(match.Name);
                    }
                }
                // エイリアスの場合
                else
                {
                    // マップ先のタグが検索語の部分マッチかチェック
                    if (ShouldExcludeSynonym(match.AliasSource, searchWord))
                    {
                        // マップ先が部分マッチする場合はsynonymを除外
                        // ただし、マップ先自体がまだ追加されていない場合は追加
                        if (!processedTags.Contains(match.AliasSource))
                        {
                            var originalTag = new TagItem
                            {
                                Name = match.AliasSource,
                                Count = match.Count
                            };
                            result.Add(originalTag);
                            processedTags.Add(match.AliasSource);
                        }
                    }
                    else
                    {
                        // synonymを表示（ただし重複チェック）
                        string synonymKey = $"{match.Name}->{match.AliasSource}";
                        if (!processedTags.Contains(synonymKey))
                        {
                            result.Add(match);
                            processedTags.Add(synonymKey);
                        }
                    }
                }
            }

            return result.Take(30).ToList();
        }

        /// <summary>
        /// Synonymを除外すべきかどうかを判定（柔軟なマッチング対応）
        /// </summary>
        private bool ShouldExcludeSynonym(string mapTarget, string searchWord)
        {
            if (string.IsNullOrEmpty(mapTarget) || string.IsNullOrEmpty(searchWord))
                return false;

            // 複数のパターンでマップ先が検索語の部分マッチかチェック
            
            // 1. 元の文字列での部分マッチ
            if (mapTarget.StartsWith(searchWord, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. 正規化された文字列での部分マッチ（アンダースコア統一）
            string normalizedTarget = NormalizeForMatching(mapTarget);
            string normalizedSearch = NormalizeForMatching(searchWord);
            if (normalizedTarget.StartsWith(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                return true;

            // 3. スペースなし形式での部分マッチ
            string targetNoSpace = RemoveSpacesAndUnderscores(mapTarget);
            string searchNoSpace = RemoveSpacesAndUnderscores(searchWord);
            if (targetNoSpace.StartsWith(searchNoSpace, StringComparison.OrdinalIgnoreCase))
                return true;

            // 4. 各種変換パターンでの部分マッチ
            // ターゲットのアンダースコアをスペースに変換してチェック
            if (mapTarget.Replace("_", " ").StartsWith(searchWord, StringComparison.OrdinalIgnoreCase))
                return true;

            // 検索語のスペースをアンダースコアに変換してチェック
            if (mapTarget.StartsWith(searchWord.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase))
                return true;

            // ターゲットのスペースをアンダースコアに変換してチェック
            if (mapTarget.Replace(" ", "_").StartsWith(searchWord, StringComparison.OrdinalIgnoreCase))
                return true;

            // 検索語のアンダースコアをスペースに変換してチェック
            if (mapTarget.StartsWith(searchWord.Replace("_", " "), StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// AvalonEditのCompletionWindowを表示
        /// </summary>
        private void ShowCompletionWindow(List<TagItem> items, int startPosition)
        {
            try
            {
                _completionWindow?.Close();

                _completionWindow = new CompletionWindow(MainTextEditor.TextArea);
                _completionWindow.CloseWhenCaretAtBeginning = true;
                _completionWindow.StartOffset = startPosition;
                _completionWindow.EndOffset = startPosition + _currentWord.Length;

                var listBox = _completionWindow.CompletionList.ListBox;
                listBox.Padding = new Thickness(0);   // ← 余白を除去
                listBox.BorderThickness = new Thickness(0);   // 外枠は CompletionWindow 側で描くので不要なら 0

                // 候補データを追加
                foreach (var item in items)
                {
                    _completionWindow.CompletionList.CompletionData.Add(new TagCompletionData(item));
                }

                // ウィンドウ幅を動的に計算
                double maxWidth = CalculateOptimalWindowWidth(items);
                _completionWindow.Width = maxWidth;
                _completionWindow.MinWidth = Math.Min(200, maxWidth);
                _completionWindow.MaxWidth = 500; // 最大幅を制限
                
                // ウィンドウの高さも調整
                _completionWindow.Height = Math.Min(300, items.Count * 22 + 10);
                
                // スタイル設定
                _completionWindow.CompletionList.BorderThickness = new Thickness(1);
                _completionWindow.CompletionList.BorderBrush = new SolidColorBrush(Color.FromRgb(171, 173, 179));
                _completionWindow.CompletionList.Background = new SolidColorBrush(Colors.White);

                // ListBoxItemのカスタムスタイルを作成
                var itemStyle = new Style(typeof(ListBoxItem));
                
                // 通常の状態
                itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
                itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.Black));
                itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
                
                // 選択時のトリガー
                var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(0, 120, 215)))); // Windows 10風の青
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
                itemStyle.Triggers.Add(selectedTrigger);
                
                // マウスオーバー時のトリガー
                var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(224, 230, 255)))); // 薄い青
                itemStyle.Triggers.Add(hoverTrigger);
                
                // スタイルを適用
                _completionWindow.CompletionList.ListBox.ItemContainerStyle = itemStyle;

                // 最初の項目を選択状態にする
                if (_completionWindow.CompletionList.CompletionData.Count > 0)
                {
                    _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];
                }

                _completionWindow.Show();
                _completionWindow.Closed += (s, e) => _completionWindow = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CompletionWindow表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 候補リストに基づいて最適なウィンドウ幅を計算
        /// </summary>
        private double CalculateOptimalWindowWidth(List<TagItem> items)
        {
            double maxWidth = 200; // 最小幅
            
            foreach (var item in items)
            {
                // タグ名の文字数をベースに幅を計算
                double nameWidth = item.DisplayText.Length * 8; // 1文字あたり約8ピクセル
                
                // カウント表示がある場合の追加幅
                if (!string.IsNullOrEmpty(item.CountDisplay))
                {
                    nameWidth += item.CountDisplay.Length * 7 + 15; // カウント分 + マージン
                }
                
                // パディングとスクロールバー分の余裕
                nameWidth += 30;
                
                maxWidth = Math.Max(maxWidth, nameWidth);
            }
            
            return Math.Min(maxWidth, 450); // 最大450ピクセルに制限
        }

        /// <summary>
        /// 重み調整機能
        /// </summary>
        private void AdjustWeight(bool? increment)
        {
            try
            {
                _isAdjustingWeight = true;
                
                string text = GetTextEditorText();
                int caretPosition = GetTextEditorCaretPosition();
                
                // まずLoRAパターンをチェック
                if (ProcessLoRAWeightAdjustment(text, caretPosition, increment))
                {
                    return; // LoRA調整が実行された場合は終了
                }
                
                // 通常のタグ重み調整
                var selection = MainTextEditor.TextArea.Selection;
                string selectedText = "";
                int selectionStart = 0;
                int selectionLength = 0;
                
                if (!selection.IsEmpty)
                {
                    selectedText = MainTextEditor.SelectedText ?? "";
                    selectionStart = MainTextEditor.SelectionStart;
                    selectionLength = MainTextEditor.SelectionLength;
                    ProcessWeightAdjustment(text, selectedText, selectionStart, selectionLength, increment);
                    return;
                }
                
                // 選択範囲がない場合、まず括弧内にいるかをチェック
                var bracketBounds = FindBracketBounds(text, caretPosition);
                if (bracketBounds.HasValue)
                {
                    var (start, length) = bracketBounds.Value;
                    selectedText = text.Substring(start, length);
                    
                    SetTextEditorSelection(start, length);
                    ProcessWeightAdjustment(text, selectedText, start, length, increment);
                    return;
                }
                
                // 括弧内にいない場合はカンマ区切り範囲を探索
                var commaBounds = FindCommaDelimitedBounds(text, caretPosition);
                if (commaBounds.HasValue)
                {
                    var (start, length) = commaBounds.Value;
                    selectedText = text.Substring(start, length);
                    
                    SetTextEditorSelection(start, length);
                    ProcessWeightAdjustment(text, selectedText, start, length, increment);
                    return;
                }
                
                // カンマ区切り範囲が見つからない場合は単語境界を探索（フォールバック）
                var wordBounds = FindWordBounds(text, caretPosition);
                if (wordBounds.HasValue)
                {
                    var (start, length) = wordBounds.Value;
                    selectedText = text.Substring(start, length);
                    
                    SetTextEditorSelection(start, length);
                    ProcessWeightAdjustment(text, selectedText, start, length, increment);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重み調整エラー: {ex.Message}");
            }
            finally
            {
                _isAdjustingWeight = false;
                
                // 重み調整後のフラグを設定
                _justAdjustedWeight = true;
                _suppressTimer?.Stop();
                _suppressTimer?.Start();
            }
        }

        /// <summary>
        /// LoRAの重み調整処理
        /// </summary>
        private bool ProcessLoRAWeightAdjustment(string text, int caretPosition, bool? increment)
        {
            // LoRAパターンを検索
            var loraPattern = FindLoRAPattern(text, caretPosition);
            if (!loraPattern.HasValue)
            {
                return false;
            }
            
            var (startPos, endPos, loraName, currentWeight) = loraPattern.Value;
            
            // 新しい重みを計算
            double newWeight = CalculateNewWeight(currentWeight, increment, 0.05); // LoRAは0.05刻み
            
            // 新しいLoRAテキストを構築
            string newLoRAText = $"<lora:{loraName}:{newWeight:F2}>";
            
            // テキストを置換
            string newText = text.Substring(0, startPos) + newLoRAText + text.Substring(endPos + 1);
            SetTextEditorText(newText);
            
            // LoRA名部分を選択状態にする
            int nameStart = startPos + "<lora:".Length;
            int nameLength = loraName.Length;
            SetTextEditorSelection(nameStart, nameLength);
            
            _isUpdatingText = true;
            try
            {
                Text = newText;
            }
            finally
            {
                _isUpdatingText = false;
            }
            
            Debug.WriteLine($"LoRA重み調整: {loraName} {currentWeight:F2} → {newWeight:F2}");
            return true;
        }

        /// <summary>
        /// LoRAパターンを検索して情報を取得
        /// </summary>
        private (int startPos, int endPos, string loraName, double weight)? FindLoRAPattern(string text, int caretPosition)
        {
            // キャレット位置から前後を検索してLoRAパターンを探す
            int searchStart = Math.Max(0, caretPosition - 100);
            int searchEnd = Math.Min(text.Length, caretPosition + 100);
            
            for (int i = searchStart; i <= searchEnd; i++)
            {
                if (i < text.Length && text[i] == '<')
                {
                    int endBracket = text.IndexOf('>', i);
                    if (endBracket != -1)
                    {
                        // キャレットがこのLoRAタグの範囲内にあるかチェック
                        if (caretPosition >= i && caretPosition <= endBracket)
                        {
                            string content = text.Substring(i + 1, endBracket - i - 1);
                            
                            // lora:で始まるかチェック
                            if (content.StartsWith("lora:", StringComparison.OrdinalIgnoreCase))
                            {
                                // lora:name:weight パターンを解析
                                var parts = content.Substring(5).Split(':'); // "lora:"を除去
                                if (parts.Length >= 1)
                                {
                                    string loraName = parts[0];
                                    double weight = 1.0;
                                    
                                    if (parts.Length >= 2 && double.TryParse(parts[1], out double parsedWeight))
                                    {
                                        weight = parsedWeight;
                                    }
                                    
                                    return (i, endBracket, loraName, weight);
                                }
                            }
                        }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 重み調整処理
        /// </summary>
        private void ProcessWeightAdjustment(string text, string selectedText, int selectionStart, int selectionLength, bool? increment)
        {
            // 現在選択されている文字列が既に重み付きかチェック
            var weightPattern = FindWeightPattern(text, selectionStart, selectionLength);
            
            double currentWeight = 1.0;
            string tagName = selectedText;
            int actualTagStart = selectionStart;
            int actualTagLength = selectionLength;
            
            if (weightPattern.HasValue)
            {
                var (patternStart, patternEnd, extractedTagName, extractedWeight) = weightPattern.Value;
                tagName = extractedTagName;
                currentWeight = extractedWeight;
                actualTagStart = patternStart;
                actualTagLength = patternEnd - patternStart + 1;
            }
            
            double newWeight = CalculateNewWeight(currentWeight, increment, 0.1);
            
            string newWeightText;
            int newSelectionStart, newSelectionLength;
            
            if (Math.Abs(newWeight - 1.0) < 0.001)
            {
                // 重みが1.0の場合は括弧を除去
                newWeightText = tagName;
                newSelectionStart = actualTagStart;
                newSelectionLength = tagName.Length;
                }
                else
                {
                // 重み付きに変更
                newWeightText = $"({tagName}:{newWeight:F1})";
                newSelectionStart = actualTagStart + 1; // 括弧をスキップ
                newSelectionLength = tagName.Length;
            }
            
            string newText = text.Substring(0, actualTagStart) + newWeightText + text.Substring(actualTagStart + actualTagLength);
            SetTextEditorText(newText);
            
            // 正しい選択範囲を設定
            SetTextEditorSelection(newSelectionStart, newSelectionLength);
            
                _isUpdatingText = true;
                try
                {
                    Text = newText;
                }
                finally
                {
                    _isUpdatingText = false;
            }
        }

        /// <summary>
        /// 重み付きパターンを解析
        /// </summary>
        private (int patternStart, int patternEnd, string tagName, double weight)? FindWeightPattern(string text, int selectionStart, int selectionLength)
        {
            // 選択範囲の前後を確認して、重み付きパターンを探す
            int checkStart = Math.Max(0, selectionStart - 2);
            int checkEnd = Math.Min(text.Length - 1, selectionStart + selectionLength + 10);
            
            // 開き括弧を探す
            int openParen = -1;
            for (int i = selectionStart - 1; i >= checkStart; i--)
            {
                if (text[i] == '(')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    openParen = i;
                    break;
                }
            }
            
            if (openParen == -1) return null;
            
            // 対応する閉じ括弧を探す
            int closeParen = -1;
            int depth = 1;
            for (int i = openParen + 1; i <= checkEnd && i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    depth++;
                }
                else if (text[i] == ')')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    depth--;
                    if (depth == 0)
                    {
                        closeParen = i;
                        break;
                    }
                }
            }
            
            if (closeParen == -1) return null;
            
            // 括弧内の内容を解析
            string content = text.Substring(openParen + 1, closeParen - openParen - 1);
            int colonIndex = content.LastIndexOf(':');
            
            if (colonIndex > 0)
            {
                string tagPart = content.Substring(0, colonIndex);
                string weightPart = content.Substring(colonIndex + 1);
                
                if (double.TryParse(weightPart, out double weight))
                {
                    return (openParen, closeParen, tagPart, weight);
                }
            }
            
            // 重みなしの括弧パターン
            return (openParen, closeParen, content, 1.0);
        }

        /// <summary>
        /// 新しい重み値を計算
        /// </summary>
        private double CalculateNewWeight(double currentWeight, bool? increment, double step)
        {
            if (increment == null)
            {
                return -currentWeight;
            }
            else if (increment == true)
            {
                return Math.Round(currentWeight + step, 2);
            }
            else
            {
                return Math.Round(currentWeight - step, 2);
            }
        }

        /// <summary>
        /// 単語境界を検索
        /// </summary>
        private (int start, int length)? FindWordBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;

            char[] separators = { ' ', ',', '\n', '\r', '\t', '(', ')', '[', ']', '{', '}', '<', '>' };
            
            int start = caretPosition;
            int end = caretPosition;
            
            while (start > 0 && !separators.Contains(text[start - 1]))
            {
                start--;
            }
            
            while (end < text.Length && !separators.Contains(text[end]))
            {
                end++;
            }
            
            if (end > start)
            {
                return (start, end - start);
            }
            
            return null;
        }

        /// <summary>
        /// 括弧内の境界を検索（キャレットが括弧内にある場合のみ）
        /// </summary>
        private (int start, int length)? FindBracketBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;

            // キャレット位置から左方向に開き括弧を探す
            int openParen = -1;
            for (int i = caretPosition - 1; i >= 0; i--)
            {
                if (text[i] == '(')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    openParen = i;
                    break;
                }
                else if (text[i] == ')')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    // 閉じ括弧が先に見つかった場合は括弧外
                    break;
                }
            }

            if (openParen == -1) return null;

            // キャレット位置から右方向に閉じ括弧を探す
            int closeParen = -1;
            int depth = 1; // 開き括弧を1つ見つけた状態からスタート
            
            for (int i = openParen + 1; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    depth++;
                }
                else if (text[i] == ')')
                {
                    // エスケープされた括弧かチェック
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        // エスケープされた括弧なので無視
                        continue;
                    }
                    depth--;
                    if (depth == 0)
                    {
                        closeParen = i;
                        break;
                    }
                }
            }

            if (closeParen == -1) return null;

            // キャレットが括弧内にあるかチェック（閉じ括弧の直前も含む）
            if (caretPosition > openParen && caretPosition <= closeParen)
            {
                // 括弧内の内容を返す（括弧は含まない）
                int contentStart = openParen + 1;
                int contentLength = closeParen - openParen - 1;
                
                if (contentLength > 0)
                {
                    return (contentStart, contentLength);
                }
            }

            return null;
        }

        /// <summary>
        /// 文字入力キーかどうかを判定
        /// </summary>
        private bool IsCharacterInputKey(Key key)
        {
            if ((key >= Key.A && key <= Key.Z) || (key >= Key.D0 && key <= Key.D9))
                return true;
            
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return true;
            
            switch (key)
            {
                case Key.Space:
                case Key.OemComma:
                case Key.OemPeriod:
                case Key.OemMinus:
                case Key.OemPlus:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 柔軟なマッチング（アンダースコア⇔スペース⇔スペースなし対応、改善版）
        /// </summary>
        private bool IsFlexibleMatch(string target, string search)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(search))
                return false;

            // 1. 元の文字列でのcontainsマッチング（スペース除去前）
            if (target.Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. 正規化された文字列でのcontainsマッチング（アンダースコア統一）
            string normalizedSearch = NormalizeForMatching(search);
            string normalizedTarget = NormalizeForMatching(target);
            if (normalizedTarget.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                return true;

            // 3. 各種変換パターンでのcontainsマッチング（スペース除去前）
            // ターゲットのアンダースコアをスペースに変換してマッチング
            if (target.Replace("_", " ").Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            // 検索語のスペースをアンダースコアに変換してマッチング
            if (target.Contains(search.Replace(" ", "_"), StringComparison.OrdinalIgnoreCase))
                return true;

            // ターゲットのスペースをアンダースコアに変換してマッチング
            if (target.Replace(" ", "_").Contains(search, StringComparison.OrdinalIgnoreCase))
                return true;

            // 検索語のアンダースコアをスペースに変換してマッチング
            if (target.Contains(search.Replace("_", " "), StringComparison.OrdinalIgnoreCase))
                return true;

            // 4. スペース除去マッチ（prefix matchのみ、単語境界考慮）
            if (IsSpaceRemovedPrefixMatch(target, search))
                return true;

            return false;
        }

        /// <summary>
        /// スペース除去での単語境界を考慮したprefix match
        /// </summary>
        private bool IsSpaceRemovedPrefixMatch(string target, string search)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(search))
                return false;

            // スペースがないターゲットの場合は通常のprefix matchのみ
            if (!target.Contains(' ') && !target.Contains('_'))
            {
                return target.StartsWith(search, StringComparison.OrdinalIgnoreCase);
            }

            // スペースまたはアンダースコアを含むターゲットの場合、単語境界を考慮
            string searchNoSpace = RemoveSpacesAndUnderscores(search);
            
            if (string.IsNullOrEmpty(searchNoSpace))
                return false;

            // ターゲットを単語に分割してprefix matchを試行
            string targetNoSpace = RemoveSpacesAndUnderscores(target);
            
            // 1. 全体でのprefix match: "arms behind back" -> "armsbehindback"
            if (targetNoSpace.StartsWith(searchNoSpace, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. 単語境界からのprefix match: "arms behind back" -> "behindback", "back"
            var words = SplitIntoWords(target);
            for (int i = 1; i < words.Count; i++) // 最初の単語は全体マッチで既にチェック済み
            {
                // i番目以降の単語を結合
                string remainingWords = string.Join("", words.Skip(i).Select(RemoveSpacesAndUnderscores));
                if (remainingWords.StartsWith(searchNoSpace, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 文字列を単語に分割（スペースとアンダースコアで分割）
        /// </summary>
        private List<string> SplitIntoWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            // スペースとアンダースコアで分割し、空文字列を除去
            return text.Split(new char[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        /// <summary>
        /// マッチング用の文字列正規化（アンダースコアに統一）
        /// </summary>
        private string NormalizeForMatching(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // アンダースコアとスペースを統一（アンダースコアに統一）
            return text.Replace(" ", "_").ToLowerInvariant();
        }

        /// <summary>
        /// スペースとアンダースコアを除去した文字列を取得
        /// </summary>
        private string RemoveSpacesAndUnderscores(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            // スペースとアンダースコアを全て除去
            return text.Replace(" ", "").Replace("_", "").ToLowerInvariant();
        }

        /// <summary>
        /// LoRAパターンをチェック（&lt;lora:name:weight&gt; または &lt;name&gt;）
        /// </summary>
        private (int startPos, string searchWord)? CheckLoraPattern(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;

            // キャレット位置から左に向かって検索
            for (int i = caretPosition - 1; i >= 0; i--)
            {
                if (text[i] == '<')
                {
                    // LoRAタグの開始位置を見つけた
                    int tagStart = i + 1; // < の次の文字から
                    int tagEnd = text.IndexOf('>', tagStart);
                    
                    if (tagEnd == -1)
                    {
                        // 閉じタグがない場合は文字列の末尾まで
                        tagEnd = text.Length;
                    }
                    
                    // キャレットがタグの範囲内にあるかチェック
                    if (caretPosition >= tagStart && caretPosition <= tagEnd)
                    {
                        // タグ内容を取得
                        string tagContent = text.Substring(tagStart, Math.Min(tagEnd - tagStart, caretPosition - tagStart));
                        
                        if (tagContent.StartsWith("lora:", StringComparison.OrdinalIgnoreCase))
                        {
                            // 通常のLoRAパターン: <lora:name:weight>
                            string nameAndWeight = tagContent.Substring(5); // "lora:" を除去
                            string[] parts = nameAndWeight.Split(':');
                            
                            if (parts.Length > 0)
                            {
                                return (tagStart + 5, parts[0]); // "lora:" の後からの位置
                            }
                        }
                        else if (IsShortFormLoraPattern(text, caretPosition))
                        {
                            // 短縮形のLoRAパターン: <name>
                            return (tagStart, tagContent);
                        }
                    }
                    
                    break; // 最初の < で検索を停止
                }
                else if (text[i] == '>')
                {
                    // 他のタグの閉じタグに遭遇したら検索を停止
                    break;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 短縮形のLoRAパターンかどうかを判定
        /// </summary>
        private bool IsShortFormLoraPattern(string text, int position)
        {
            for (int i = position; i >= 0; i--)
            {
                if (text[i] == '<')
                {
                    // < の後の内容を確認
                    int endPos = text.IndexOf('>', i + 1);
                    if (endPos == -1) endPos = text.Length;
                    
                    // positionが< ... >の範囲内にあるかチェック
                    if (position >= i && position <= endPos)
                    {
                        string content = text.Substring(i + 1, Math.Min(endPos - i - 1, position - i - 1));
                        
                        // 短縮形と判定（loraで始まっていない）
                        if (!content.StartsWith("lora:", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// アンダースコアをスペースに変換
        /// </summary>
        private string ConvertUnderscoreToSpace(string text)
        {
            return text?.Replace("_", " ") ?? "";
        }

        /// <summary>
        /// 現在の単語を取得
        /// </summary>
        private void GetCurrentWord(string text, int caretPosition, out string word, out int startIndex)
        {
            word = "";
            startIndex = 0;

            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return;

            char[] separators = { ' ', ',', '\n', '\r', '\t', '(', ')', '[', ']', '{', '}', '<', '>' };

            startIndex = caretPosition;
            while (startIndex > 0 && !separators.Contains(text[startIndex - 1]))
            {
                startIndex--;
            }

            int endIndex = caretPosition;
            while (endIndex < text.Length && !separators.Contains(text[endIndex]))
            {
                endIndex++;
            }

            if (endIndex > startIndex)
            {
                word = text.Substring(startIndex, endIndex - startIndex);
            }
        }

        // 既存メソッドの実装
        private void ValidateParentheses(string text)
        {
            try
            {
                bool isValid = IsParenthesesValid(text);
                
                // AvalonEditでは背景色の設定方法が異なる
                if (isValid)
                {
                    MainTextEditor.Background = new SolidColorBrush(Colors.White);
                }
                else
                {
                    MainTextEditor.Background = new SolidColorBrush(Color.FromRgb(255, 240, 240));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"構文チェックエラー: {ex.Message}");
                MainTextEditor.Background = new SolidColorBrush(Colors.White);
            }
        }

        /// <summary>
        /// 括弧が正常に閉じられているかチェック
        /// </summary>
        private bool IsParenthesesValid(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;
            
            var openParentheses = new Stack<int>();
            
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                
                if (i > 0 && text[i - 1] == '\\')
                {
                    continue;
                }
                
                if (current == '(')
                {
                    openParentheses.Push(i);
                }
                else if (current == ')')
                {
                    if (openParentheses.Count == 0)
                    {
                        return false;
                    }
                    
                    openParentheses.Pop();
                }
            }
            
            return openParentheses.Count == 0;
        }

        private bool ShouldSuppressAutoComplete()
        {
            return _searchBoxHasFocus;
        }

        /// <summary>
        /// ウィンドウのアクティブ状態変更を監視
        /// </summary>
        private void MainWindow_Deactivated(object? sender, EventArgs e)
        {
            _completionWindow?.Close();
        }

        // その他の静的メソッド
        public static void SetSearchModeActive(bool isActive) { _isSearchModeActive = isActive; }
        public static void SetSearchBoxFocus(bool hasFocus) { _searchBoxHasFocus = hasFocus; }
        public static string GetAutoCompleteStatus() { return "AvalonEdit Ready"; }
        public static void ForceResetAllStates() { }

        /// <summary>
        /// 削除操作後に問題が発生するかを判定
        /// </summary>
        private bool WillCauseProblemAfterDeletion(string text, int caretPos, bool isBackspace)
        {
            if (string.IsNullOrEmpty(text) || caretPos < 0) return false;
            
            // 削除される文字と削除後の状況を予測
            if (isBackspace && caretPos > 0)
            {
                char charToDelete = text[caretPos - 1];
                
                // カンマやスペースを削除する場合
                if (charToDelete == ',' || charToDelete == ' ')
                {
                    // 削除後にタグ名が残る可能性をチェック
                    if (caretPos >= 2)
                    {
                        // カンマやスペースの前の文字が英字の場合（タグ名の一部）
                        char prevChar = text[caretPos - 2];
                        if (char.IsLetter(prevChar))
                        {
                            return true;
                        }
                    }
                }
                
                // 閉じ括弧を削除する場合（重み付きタグの一部）
                if (charToDelete == ')' && caretPos >= 3)
                {
                    // (tag:1.1) の ) を削除するケース
                    int openParen = text.LastIndexOf('(', caretPos - 1);
                    if (openParen >= 0)
                    {
                        string content = text.Substring(openParen + 1, caretPos - openParen - 2);
                        if (content.Contains(':') && content.Any(char.IsLetter))
                        {
                            return true;
                        }
                    }
                }
            }
            else if (!isBackspace && caretPos < text.Length)
            {
                char charToDelete = text[caretPos];
                
                // Deleteキーでカンマやスペースを削除する場合
                if (charToDelete == ',' || charToDelete == ' ')
                {
                    // 削除前にタグ名が存在するかチェック
                    if (caretPos > 0 && char.IsLetter(text[caretPos - 1]))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// ワイルドカードパターンをチェック
        /// 通常パターン: __ または __foo
        /// 囲まれたパターン: __foo__ (fooワイルドカードの内容を表示)
        /// </summary>
        private (int startPos, string searchWord, bool isEnclosed, string wildcardName)? CheckWildcardPattern(string text, int caretPosition)
        {
            // 1. まず囲まれた領域をチェック（キャレット位置の左側を検索）
            var enclosedPattern = CheckEnclosedWildcardPattern(text, caretPosition);
            if (enclosedPattern.HasValue)
            {
                return enclosedPattern;
            }
            
            // 2. 通常の__パターンを検索（キャレット位置から逆方向に検索、カンマを超えない）
            for (int i = Math.Min(caretPosition, text.Length - 1); i >= 1; i--)
            {
                // カンマに遭遇したら探索を停止
                if (text[i] == ',')
                {
                    break;
                }
                
                if (i >= 1 && text[i - 1] == '_' && text[i] == '_')
                {
                    // __が見つかった位置
                    int wildcardStart = i + 1; // __の直後から検索語開始
                    
                    // キャレット位置が__の後にある場合のみ処理
                    if (caretPosition >= wildcardStart)
                    {
                        // __の後から現在のキャレット位置までを検索語として取得
                        string searchWord = "";
                        if (caretPosition > wildcardStart)
                        {
                            // 検索語の終端を決定（スペース、カンマ、改行、閉じ__まで）
                            int endPos = caretPosition;
                            for (int j = wildcardStart; j < caretPosition; j++)
                            {
                                char c = text[j];
                                if (c == ' ' || c == ',' || c == '\n' || c == '\r' || c == '\t')
                                {
                                    endPos = j;
                                    break;
                                }
                                // 閉じ__をチェック
                                if (j < text.Length - 1 && text[j] == '_' && text[j + 1] == '_')
                                {
                                    endPos = j;
                                    break;
                                }
                            }
                            searchWord = text.Substring(wildcardStart, endPos - wildcardStart);
                        }
                        
                        Debug.WriteLine($"通常ワイルドカードパターン検出: 開始位置={i-1}, 検索語='{searchWord}'");
                        return (i - 1, searchWord, false, ""); // __の開始位置と検索語を返す
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 囲まれたワイルドカードパターンをチェック: __foo__
        /// キャレットが右端の__の右にある場合を検出
        /// </summary>
        private (int startPos, string searchWord, bool isEnclosed, string wildcardName)? CheckEnclosedWildcardPattern(string text, int caretPosition)
        {
            // キャレット位置から左方向に__を探す（カンマを超えない）
            int rightUnderscorePos = -1;
            
            // キャレット位置の直前から__を探す
            for (int i = caretPosition - 2; i >= 1; i--)
            {
                // カンマに遭遇したら探索を停止
                if (text[i] == ',')
                {
                    break;
                }
                
                if (text[i] == '_' && text[i + 1] == '_')
                {
                    rightUnderscorePos = i;
                    break;
                }
            }
            
            if (rightUnderscorePos == -1) return null;
            
            // 右側の__が見つかったので、さらに左側の__を探す（カンマを超えない）
            int leftUnderscorePos = -1;
            for (int i = rightUnderscorePos - 1; i >= 1; i--)
            {
                // カンマに遭遇したら探索を停止
                if (text[i] == ',')
                {
                    break;
                }
                
                if (text[i - 1] == '_' && text[i] == '_')
                {
                    leftUnderscorePos = i - 1;
                    break;
                }
            }
            
            if (leftUnderscorePos == -1) return null;
            
            // __foo__パターンが見つかった
            int wildcardNameStart = leftUnderscorePos + 2;
            int wildcardNameLength = rightUnderscorePos - wildcardNameStart;
            
            if (wildcardNameLength <= 0) return null;
            
            string wildcardName = text.Substring(wildcardNameStart, wildcardNameLength);
            
            // ワイルドカード名がファイル名として有効かチェック
            if (IsValidWildcardName(wildcardName))
            {
                Debug.WriteLine($"囲まれたワイルドカードパターン検出: ワイルドカード名='{wildcardName}', 位置={leftUnderscorePos}-{rightUnderscorePos + 1}");
                
                // キャレットが右の__の直後にあることを確認
                if (caretPosition == rightUnderscorePos + 2)
                {
                    return (leftUnderscorePos, "", true, wildcardName);
                }
            }
            
            return null;
        }

        /// <summary>
        /// ワイルドカード名がファイル名として有効かチェック
        /// </summary>
        private bool IsValidWildcardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            
            // ファイル名に使えない文字をチェック
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return !name.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// ワイルドカード専用のCompletionWindowを表示
        /// </summary>
        private void ShowWildcardCompletionWindow(List<WildcardItem> items, int startPosition)
        {
            try
            {
                _completionWindow?.Close();

                _completionWindow = new CompletionWindow(MainTextEditor.TextArea);
                _completionWindow.CloseWhenCaretAtBeginning = true;
                _completionWindow.StartOffset = startPosition;
                _completionWindow.EndOffset = startPosition + _currentWord.Length + 2; // __の分も含める

                var listBox = _completionWindow.CompletionList.ListBox;
                listBox.Padding = new Thickness(0);
                listBox.BorderThickness = new Thickness(0);

                // ワイルドカード候補データを追加
                foreach (var item in items)
                {
                    _completionWindow.CompletionList.CompletionData.Add(new WildcardCompletionData(item));
                }

                // ウィンドウ幅を動的に計算
                double maxWidth = CalculateOptimalWildcardWindowWidth(items);
                _completionWindow.Width = maxWidth;
                _completionWindow.MinWidth = Math.Min(200, maxWidth);
                _completionWindow.MaxWidth = 500;
                
                // ウィンドウの高さも調整
                _completionWindow.Height = Math.Min(300, items.Count * 22 + 10);
                
                // スタイル設定
                _completionWindow.CompletionList.BorderThickness = new Thickness(1);
                _completionWindow.CompletionList.BorderBrush = new SolidColorBrush(Color.FromRgb(171, 173, 179));
                _completionWindow.CompletionList.Background = new SolidColorBrush(Colors.White);

                // ListBoxItemのカスタムスタイルを作成（ワイルドカード用）
                var itemStyle = new Style(typeof(ListBoxItem));
                
                // 通常の状態
                itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
                itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.Black));
                itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
                
                // 選択時のトリガー（ワイルドカード用の緑色）
                var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(34, 139, 34)))); // フォレストグリーン
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
                itemStyle.Triggers.Add(selectedTrigger);
                
                // マウスオーバー時のトリガー（ワイルドカード用の薄い緑）
                var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(240, 255, 240)))); // 薄い緑
                itemStyle.Triggers.Add(hoverTrigger);
                
                // スタイルを適用
                _completionWindow.CompletionList.ListBox.ItemContainerStyle = itemStyle;

                // 最初の項目を選択状態にする
                if (_completionWindow.CompletionList.CompletionData.Count > 0)
                {
                    _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];
                }

                _completionWindow.Show();
                _completionWindow.Closed += (s, e) => { _completionWindow = null; };
                
                Debug.WriteLine($"ワイルドカード候補ウィンドウ表示: {items.Count}個の候補");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ワイルドカードCompletionWindow表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ワイルドカード候補リストに基づいて最適なウィンドウ幅を計算
        /// </summary>
        private double CalculateOptimalWildcardWindowWidth(List<WildcardItem> items)
        {
            double maxWidth = 250; // ワイルドカード用の最小幅（少し広め）
            
            foreach (var item in items)
            {
                // "wildcard (Wildcard)" 形式の表示幅を計算
                string displayText = $"{item.Name} (Wildcard)";
                double nameWidth = displayText.Length * 8; // 1文字あたり約8ピクセル
                
                // エントリ数表示がある場合の追加幅
                string entryDisplay = $"({item.EntryCount} entries)";
                nameWidth += entryDisplay.Length * 7 + 15; // エントリ数分 + マージン
                
                // パディングとスクロールバー分の余裕
                nameWidth += 40;
                
                maxWidth = Math.Max(maxWidth, nameWidth);
            }
            
            return Math.Min(maxWidth, 500); // 最大500ピクセルに制限
        }

        /// <summary>
        /// ワイルドカード内容専用のCompletionWindowを表示
        /// </summary>
        private void ShowWildcardContentCompletionWindow(List<WildcardContentItem> items, int startPosition, string wildcardName)
        {
            try
            {
                _completionWindow?.Close();

                _completionWindow = new CompletionWindow(MainTextEditor.TextArea);
                _completionWindow.CloseWhenCaretAtBeginning = true;
                _completionWindow.StartOffset = startPosition;
                
                // 囲まれた領域全体を置換範囲とする
                int enclosedLength = $"__{wildcardName}__".Length;
                _completionWindow.EndOffset = startPosition + enclosedLength;

                var listBox = _completionWindow.CompletionList.ListBox;
                listBox.Padding = new Thickness(0);
                listBox.BorderThickness = new Thickness(0);

                // ワイルドカード内容候補データを追加
                foreach (var item in items)
                {
                    _completionWindow.CompletionList.CompletionData.Add(new WildcardContentCompletionData(item));
                }

                // ウィンドウ幅を動的に計算
                double maxWidth = CalculateOptimalWildcardContentWindowWidth(items, wildcardName);
                _completionWindow.Width = maxWidth;
                _completionWindow.MinWidth = Math.Min(200, maxWidth);
                _completionWindow.MaxWidth = 600; // ワイルドカード内容は長いかもしれないので少し大きめ
                
                // ウィンドウの高さも調整
                _completionWindow.Height = Math.Min(400, items.Count * 22 + 10);
                
                // スタイル設定
                _completionWindow.CompletionList.BorderThickness = new Thickness(1);
                _completionWindow.CompletionList.BorderBrush = new SolidColorBrush(Color.FromRgb(171, 173, 179));
                _completionWindow.CompletionList.Background = new SolidColorBrush(Colors.White);

                // ListBoxItemのカスタムスタイルを作成（ワイルドカード内容用）
                var itemStyle = new Style(typeof(ListBoxItem));
                
                // 通常の状態
                itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
                itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.Black));
                itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(4, 2, 4, 2)));
                
                // 選択時のトリガー（ワイルドカード内容用の青緑色）
                var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(32, 178, 170)))); // ライトシーグリーン
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
                itemStyle.Triggers.Add(selectedTrigger);
                
                // マウスオーバー時のトリガー（ワイルドカード内容用の薄い青緑）
                var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, 
                    new SolidColorBrush(Color.FromRgb(224, 255, 255)))); // 薄い青緑
                itemStyle.Triggers.Add(hoverTrigger);
                
                // スタイルを適用
                _completionWindow.CompletionList.ListBox.ItemContainerStyle = itemStyle;

                // 最初の項目を選択状態にする
                if (_completionWindow.CompletionList.CompletionData.Count > 0)
                {
                    _completionWindow.CompletionList.SelectedItem = _completionWindow.CompletionList.CompletionData[0];
                }

                _completionWindow.Show();
                _completionWindow.Closed += (s, e) => { _completionWindow = null; };
                
                Debug.WriteLine($"ワイルドカード内容候補ウィンドウ表示: {wildcardName} - {items.Count}個の候補");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ワイルドカード内容CompletionWindow表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ワイルドカード内容候補リストに基づいて最適なウィンドウ幅を計算
        /// </summary>
        private double CalculateOptimalWildcardContentWindowWidth(List<WildcardContentItem> items, string wildcardName)
        {
            double maxWidth = 300; // ワイルドカード内容用の最小幅
            
            foreach (var item in items)
            {
                // 内容テキストの表示幅を計算
                double contentWidth = item.Content.Length * 8; // 1文字あたり約8ピクセル
                
                // ワイルドカード名表示の追加幅
                string wildcardDisplay = $"({wildcardName})";
                contentWidth += wildcardDisplay.Length * 7 + 15; // ワイルドカード名分 + マージン
                
                // パディングとスクロールバー分の余裕
                contentWidth += 40;
                
                maxWidth = Math.Max(maxWidth, contentWidth);
            }
            
            return Math.Min(maxWidth, 600); // 最大600ピクセルに制限
        }

        // TextEditorのテキスト操作メソッド
        private string GetTextEditorText()
        {
            return MainTextEditor.Text ?? string.Empty;
        }

        private void SetTextEditorText(string text)
        {
            if (MainTextEditor.Text != text)
            {
                _isUpdatingText = true;
                try
                {
                    // ドキュメントを直接編集してUndo操作を一つの単位として扱う
                    MainTextEditor.Document.BeginUpdate();
                    try
                    {
                        MainTextEditor.Document.Text = text ?? string.Empty;
                    }
                    finally
                    {
                        MainTextEditor.Document.EndUpdate();
                    }
                }
                finally
                {
                    _isUpdatingText = false;
                }
            }
        }

        private int GetTextEditorCaretPosition()
        {
            return MainTextEditor.CaretOffset;
        }

        private void SetTextEditorCaretPosition(int position)
        {
            if (position >= 0 && position <= MainTextEditor.Text.Length)
            {
                MainTextEditor.CaretOffset = position;
            }
        }

        private void SetTextEditorSelection(int start, int length)
        {
            if (start >= 0 && start + length <= MainTextEditor.Text.Length)
            {
                MainTextEditor.Select(start, length);
            }
        }

        // イベントハンドラーの実装
        private void MainTextEditor_TextChanged(object sender, EventArgs e)
        {
            // AvalonEdit_TextChangedで処理済み
        }

        private void MainTextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Shift+Enter: 生成コマンドを実行（改行を防ぐため、PreviewKeyDownで早期にキャッチ）
            if (e.Key == Key.Enter && (e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // オートコンプリートを閉じる
                _completionWindow?.Close();
                
                // 親ウィンドウで生成コマンドを実行
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow?.DataContext is MainViewModel vm && vm.GenerateCommand?.CanExecute(null) == true)
                {
                    vm.GenerateCommand.Execute(null);
                }
                
                e.Handled = true; // 改行入力を防ぐ
                return;
            }

            // Ctrl+P: 画像パネル全画面表示（MainWindowで処理させるため、ここでは何もしない）
            if (e.Key == Key.P && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                // オートコンプリートを閉じてイベントを親に委譲
                _completionWindow?.Close();
                return; // e.Handled = trueしない（親で処理させる）
            }

            // Ctrl+D: カンマで区切られた範囲を削除
            if (e.Key == Key.D && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                DeleteCommaDelimitedSection();
                e.Handled = true;
                return;
            }

            // Ctrl+F: 検索開始時のキャレット位置を保存
            if (e.Key == Key.F && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _searchStartCaretPosition = MainTextEditor.CaretOffset;
                _isSearchActive = true;
                Debug.WriteLine($"検索開始: キャレット位置 {_searchStartCaretPosition} を保存");
                return; // このイベントは通常通り処理される
            }
            
            // Escape: 検索終了時にキャレット位置を復元
            if (e.Key == Key.Escape && _isSearchActive)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MainTextEditor.CaretOffset = _searchStartCaretPosition;
                    _isSearchActive = false;
                    Debug.WriteLine($"検索終了: キャレット位置を {_searchStartCaretPosition} に復元");
                }), DispatcherPriority.Background);
                return; // このイベントは通常通り処理される
            }
            
            // 検索中の場合、矢印キーでのキャレット移動を制限
            // ただし、検索ボックスにフォーカスがある場合、またはメインエディタにフォーカスがない場合は制限しない
            if (_isSearchActive && !_searchBoxHasFocus && MainTextEditor.IsFocused && 
                (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right))
            {
                // Ctrl+矢印キーの重み調整は例外として許可
                if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                {
                    e.Handled = true;
                    Debug.WriteLine("検索中のキャレット移動をブロック");
                    return;
                }
            }
            
            // 重み調整機能のチェック（Ctrl+矢印キーまたはCtrl+-）
            if (e.Key == Key.Up && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                AdjustWeight(true);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                AdjustWeight(false);
                e.Handled = true;
                return;
            }
            if ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                AdjustWeight(null); // null = マイナス掛け
                e.Handled = true;
                return;
            }

            // CompletionWindowが開いている場合の処理
            if (_completionWindow != null)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        _completionWindow.Close();
                        e.Handled = true;
                        break;
                    case Key.Enter:
                    case Key.Tab:
                        _completionWindow.CompletionList.RequestInsertion(e);
                        e.Handled = true;
                        break;
                }
                return;
            }

            // 削除操作の検出
            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                // 削除操作が実行される前のテキスト状態をチェック
                string currentText = GetTextEditorText();
                int caretPos = GetTextEditorCaretPosition();
                
                if (WillCauseProblemAfterDeletion(currentText, caretPos, e.Key == Key.Back))
                {
                    _justPerformedDeletion = true;
                    _suppressTimer?.Stop();
                    _suppressTimer?.Start();
                }
                
                if (!_isAdjustingWeight && !_isInsertingTag)
                {
                    _searchTimer.Stop();
                    _searchTimer.Start();
                }
                return;
            }

            // 文字入力キーの検出
            if (!_isAdjustingWeight && !_isInsertingTag && IsCharacterInputKey(e.Key))
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }

        private void MainTextEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            Task.Delay(50).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!MainTextEditor.IsFocused)
                    {
                        _completionWindow?.Close();
                    }
                });
            });
        }

        private void MainTextEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            // フォーカス取得時の処理
        }

        private void MainTextEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // サイズ変更時の処理
        }

        /// <summary>
        /// Undo履歴をクリアする（タブ切り替え時に使用）
        /// </summary>
        public void ClearUndoHistory()
        {
            try
            {
                // AvalonEditのUndo履歴をクリア
                MainTextEditor.Document.UndoStack.ClearAll();
                Debug.WriteLine("Undo履歴をクリアしました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Undo履歴クリアエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// カンマで区切られた範囲の境界を検索
        /// </summary>
        private (int start, int length)? FindCommaDelimitedBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;

            // まず、LoRAタグ（<...>）の内部にいるかどうかをチェック
            var loraTagBounds = FindLoRATagBounds(text, caretPosition);
            if (loraTagBounds.HasValue)
            {
                // LoRAタグ内部にいる場合は、タグ全体を削除対象とする
                return loraTagBounds;
            }

            // キャレット位置から左方向にカンマまたは文字列開始を探す
            int start = caretPosition;
            while (start > 0)
            {
                if (text[start - 1] == ',')
                {
                    // カンマの直後の位置
                    break;
                }
                else if (text[start - 1] == '\n' || text[start - 1] == '\r')
                {
                    // 改行で区切られている場合も境界とする
                    break;
                }
                start--;
            }

            // キャレット位置から右方向にカンマまたは文字列終端を探す
            int end = caretPosition;
            while (end < text.Length)
            {
                if (text[end] == ',')
                {
                    // カンマの直前の位置
                    break;
                }
                else if (text[end] == '\n' || text[end] == '\r')
                {
                    // 改行で区切られている場合も境界とする
                    break;
                }
                end++;
            }

            // 開始位置と終了位置の前後のスペースをトリム
            while (start < end && (text[start] == ' ' || text[start] == '\t'))
            {
                start++;
            }
            
            while (end > start && end - 1 < text.Length && (text[end - 1] == ' ' || text[end - 1] == '\t'))
            {
                end--;
            }

            // 有効な範囲があるかチェック
            if (end > start)
            {
                int length = end - start;
                string content = text.Substring(start, length);
                
                // 空白のみの場合は無効
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }
                
                Debug.WriteLine($"カンマ区切り範囲検出: '{content}' (位置: {start}-{end})");
                return (start, length);
            }

            return null;
        }

        /// <summary>
        /// LoRAタグ（<...>）の境界を検索
        /// </summary>
        private (int start, int length)? FindLoRATagBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;

            // キャレット位置から左方向に '<' を探す
            int start = -1;
            for (int i = caretPosition; i >= 0; i--)
            {
                if (text[i] == '<')
                {
                    start = i;
                    break;
                }
                else if (text[i] == ',' || text[i] == '\n' || text[i] == '\r')
                {
                    // カンマや改行に遭遇したら、LoRAタグの範囲外
                    break;
                }
            }

            // '<' が見つからない場合は、LoRAタグ内部ではない
            if (start == -1)
                return null;

            // 見つかった '<' から右方向に '>' を探す
            int end = -1;
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '>')
                {
                    end = i + 1; // '>' の次の位置
                    break;
                }
                else if (text[i] == ',' || text[i] == '\n' || text[i] == '\r')
                {
                    // カンマや改行に遭遇したら、不正なLoRAタグ
                    return null;
                }
            }

            // '>' が見つからない場合は、不正なLoRAタグ
            if (end == -1)
                return null;

            // キャレット位置がLoRAタグの範囲内にあるかチェック
            if (caretPosition >= start && caretPosition <= end)
            {
                int length = end - start;
                string content = text.Substring(start, length);
                Debug.WriteLine($"LoRAタグ検出: '{content}' (位置: {start}-{end})");
                return (start, length);
            }

            return null;
        }

        /// <summary>
        /// タグリストに使用回数情報を非同期で取得して設定
        /// </summary>
        private async Task EnrichWithUsageDataAsync(List<TagItem> tags)
        {
            try
            {
                // タグ名のリストを作成（重複除去）
                var tagNames = tags.Select(tag => tag.ActualTag).Distinct().ToList();
                
                // 通常タグの使用回数を一括取得
                var normalTagUsages = await TagUsageDatabase.Instance.GetUsageCountsAsync(tagNames, 0);
                
                // LoRAタグの使用回数を一括取得
                var loraTagUsages = await TagUsageDatabase.Instance.GetUsageCountsAsync(tagNames, 1);
                
                // 各タグに使用回数情報を設定
                foreach (var tag in tags)
                {
                    var actualTag = tag.ActualTag;
                    TagUsageInfo? usageInfo = null;
                    
                    if (tag.IsLora)
                    {
                        loraTagUsages.TryGetValue(actualTag, out usageInfo);
                    }
                    else
                    {
                        normalTagUsages.TryGetValue(actualTag, out usageInfo);
                    }
                    
                    if (usageInfo != null)
                    {
                        tag.UsageCount = usageInfo.TotalCount;
                        tag.IsFrequentlyUsed = usageInfo.IsFrequentlyUsed;
                        tag.LastUsedDate = usageInfo.LastUsedDate;
                    }
                    else
                    {
                        tag.UsageCount = 0;
                        tag.IsFrequentlyUsed = false;
                        tag.LastUsedDate = DateTime.MinValue;
                    }
                }
                
                Debug.WriteLine($"使用回数情報を{tags.Count}個のタグに設定しました");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"使用回数情報取得エラー: {ex.Message}");
                // エラーの場合はデフォルト値のまま
            }
        }

        /// <summary>
        /// カンマで区切られた範囲を削除する
        /// </summary>
        private void DeleteCommaDelimitedSection()
        {
            try
            {
                string text = GetTextEditorText();
                int caretPosition = GetTextEditorCaretPosition();
                
                var bounds = FindCommaDelimitedBounds(text, caretPosition);
                if (bounds.HasValue)
                {
                    int deleteStart = bounds.Value.start;
                    int deleteLength = bounds.Value.length;
                    int deleteEnd = deleteStart + deleteLength;
                    
                    Debug.WriteLine($"削除範囲: {deleteStart}-{deleteEnd} ('{text.Substring(deleteStart, deleteLength)}')");
                    
                    // 末尾のタグかどうかをチェック（削除範囲の後ろに実質的な内容があるか）
                    bool isLastTag = true;
                    bool hasContentAfterNewline = false;
                    bool foundNewline = false;
                    
                    for (int i = deleteEnd; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c == '\n' || c == '\r')
                        {
                            foundNewline = true;
                            continue;
                        }
                        else if (c != ' ' && c != '\t' && c != ',')
                        {
                            if (foundNewline)
                            {
                                hasContentAfterNewline = true;
                            }
                            isLastTag = false;
                            break;
                        }
                    }
                    
                    // 改行の後に内容がある場合は、タグの直後のカンマと改行前までを削除範囲とする
                    bool isLastTagBeforeNewline = foundNewline && hasContentAfterNewline;
                    
                    // 先頭のタグかどうかをチェック（削除範囲の前に実質的な内容があるか）
                    bool isFirstTag = true;
                    for (int i = deleteStart - 1; i >= 0; i--)
                    {
                        char c = text[i];
                        if (c != ' ' && c != '\t' && c != ',' && c != '\n' && c != '\r')
                        {
                            isFirstTag = false;
                            break;
                        }
                    }
                    
                    Debug.WriteLine($"位置情報: isFirstTag={isFirstTag}, isLastTag={isLastTag}, isLastTagBeforeNewline={isLastTagBeforeNewline}");
                    
                    // 削除範囲を調整
                    int adjustedStart = deleteStart;
                    int adjustedEnd = deleteEnd;
                    
                    if (isLastTag && !isFirstTag)
                    {
                        // 末尾のタグの場合: 直前のカンマとスペースも削除対象に含める
                        for (int i = deleteStart - 1; i >= 0; i--)
                        {
                            char c = text[i];
                            if (c == ',' || c == ' ' || c == '\t')
                            {
                                adjustedStart = i;
                            }
                            else
                            {
                                break;
                            }
                        }
                        
                        // 末尾のタグの場合: 直後のカンマとスペースも削除対象に含める
                        for (int i = deleteEnd; i < text.Length; i++)
                        {
                            char c = text[i];
                            if (c == ',' || c == ' ' || c == '\t')
                            {
                                adjustedEnd = i + 1;
                            }
                            else if (c == '\n' || c == '\r')
                            {
                                // 改行に遭遇したら停止（改行は保持）
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else if (isFirstTag && !isLastTag)
                    {
                        // 先頭のタグの場合: 直後のカンマとスペースも削除対象に含める（改行は除く）
                        for (int i = deleteEnd; i < text.Length; i++)
                        {
                            char c = text[i];
                            if (c == ',' || c == ' ' || c == '\t')
                            {
                                adjustedEnd = i + 1;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    else if (isLastTagBeforeNewline)
                    {
                        // 改行前の最後のタグの場合: 直前のカンマとスペース、直後のカンマとスペースも削除（改行は保持）
                        for (int i = deleteStart - 1; i >= 0; i--)
                        {
                            char c = text[i];
                            if (c == ',' || c == ' ' || c == '\t')
                            {
                                adjustedStart = i;
                            }
                            else
                            {
                                break;
                            }
                        }
                        
                        for (int i = deleteEnd; i < text.Length; i++)
                        {
                            char c = text[i];
                            if (c == ',' || c == ' ' || c == '\t')
                            {
                                adjustedEnd = i + 1;
                            }
                            else if (c == '\n' || c == '\r')
                            {
                                // 改行に遭遇したら停止（改行は保持）
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    
                    Debug.WriteLine($"調整後の削除範囲: {adjustedStart}-{adjustedEnd}");
                    
                    // 調整後の範囲で削除を実行
                    string beforeSection = adjustedStart > 0 ? text.Substring(0, adjustedStart) : "";
                    string afterSection = adjustedEnd < text.Length ? text.Substring(adjustedEnd) : "";
                    
                    Debug.WriteLine($"Before: '{beforeSection}'");
                    Debug.WriteLine($"After: '{afterSection}'");
                    
                    // 削除後の不適切なカンマ/スペースパターンを修正（改行保持）
                    string newText = CleanupAfterDeletion(beforeSection, afterSection);
                    
                    // フォーマッティングを適用
                    string formattedText = FormatPromptText(newText);
                    
                    // 新しいキャレット位置を計算（フォーマット前のbeforeSectionの末尾を基準）
                    string formattedBefore = FormatPromptText(beforeSection);
                    int newCaretPosition = formattedBefore.Length;
                    
                    // カンマの後ろに移動する場合の調整
                    if (formattedText.Length > newCaretPosition && formattedText[newCaretPosition] == ',')
                    {
                        newCaretPosition++;
                        // カンマの後のスペースもスキップ
                        while (newCaretPosition < formattedText.Length && formattedText[newCaretPosition] == ' ')
                        {
                            newCaretPosition++;
                        }
                    }
                    
                    // テキストを更新
                    SetTextEditorText(formattedText);
                    SetTextEditorCaretPosition(Math.Min(newCaretPosition, formattedText.Length));
                    
                    // 依存関係プロパティも更新
                    _isUpdatingText = true;
                    try
                    {
                        Text = formattedText;
                    }
                    finally
                    {
                        _isUpdatingText = false;
                    }
                    
                    Debug.WriteLine($"カンマ区切り削除完了: 調整後位置{adjustedStart}-{adjustedEnd}、フォーマット適用済み");
                    Debug.WriteLine($"結果テキスト: '{formattedText}'");
                }
                else
                {
                    Debug.WriteLine("削除対象のカンマ区切り範囲が見つかりませんでした");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"カンマ区切り削除エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 削除後のテキストを整理（不適切なカンマやスペースを除去、改行保持）
        /// </summary>
        private string CleanupAfterDeletion(string beforeSection, string afterSection)
        {
            // 改行を保持するため、改行文字をチェック
            bool beforeEndsWithNewline = beforeSection.EndsWith("\n") || beforeSection.EndsWith("\r\n") || beforeSection.EndsWith("\r");
            bool afterStartsWithNewline = afterSection.StartsWith("\n") || afterSection.StartsWith("\r\n") || afterSection.StartsWith("\r");
            
            // 改行を一時的に保存
            string beforeNewline = "";
            string afterNewline = "";
            
            if (beforeEndsWithNewline)
            {
                if (beforeSection.EndsWith("\r\n"))
                {
                    beforeNewline = "\r\n";
                    beforeSection = beforeSection.Substring(0, beforeSection.Length - 2);
                }
                else if (beforeSection.EndsWith("\n"))
                {
                    beforeNewline = "\n";
                    beforeSection = beforeSection.Substring(0, beforeSection.Length - 1);
                }
                else if (beforeSection.EndsWith("\r"))
                {
                    beforeNewline = "\r";
                    beforeSection = beforeSection.Substring(0, beforeSection.Length - 1);
                }
            }
            
            if (afterStartsWithNewline)
            {
                if (afterSection.StartsWith("\r\n"))
                {
                    afterNewline = "\r\n";
                    afterSection = afterSection.Substring(2);
                }
                else if (afterSection.StartsWith("\n"))
                {
                    afterNewline = "\n";
                    afterSection = afterSection.Substring(1);
                }
                else if (afterSection.StartsWith("\r"))
                {
                    afterNewline = "\r";
                    afterSection = afterSection.Substring(1);
                }
            }
            
            // スペースをトリム（改行は既に除去済み）
            string trimmedBefore = beforeSection.TrimEnd();
            string trimmedAfter = afterSection.TrimStart();
            
            // 前部分がカンマで終わり、後部分がカンマで始まる場合
            if (trimmedBefore.EndsWith(",") && trimmedAfter.StartsWith(","))
            {
                // 後のカンマを削除
                trimmedAfter = trimmedAfter.Substring(1).TrimStart();
            }
            // 前部分がカンマで終わり、後部分が空または改行のみの場合
            else if (trimmedBefore.EndsWith(",") && (string.IsNullOrWhiteSpace(trimmedAfter) || string.IsNullOrWhiteSpace(afterSection)))
            {
                // 前のカンマを削除
                trimmedBefore = trimmedBefore.Substring(0, trimmedBefore.Length - 1).TrimEnd();
            }
            // 前部分が空で後部分がカンマで始まる場合
            else if (string.IsNullOrWhiteSpace(trimmedBefore) && string.IsNullOrWhiteSpace(beforeSection) && trimmedAfter.StartsWith(","))
            {
                // 後のカンマを削除
                trimmedAfter = trimmedAfter.Substring(1).TrimStart();
            }
            
            // 結果を組み合わせる（改行を適切に復元）
            string result = "";
            
            if (string.IsNullOrWhiteSpace(trimmedBefore) && string.IsNullOrWhiteSpace(beforeSection))
            {
                // 前部分が完全に空の場合
                result = afterNewline + trimmedAfter;
            }
            else if (string.IsNullOrWhiteSpace(trimmedAfter) && string.IsNullOrWhiteSpace(afterSection))
            {
                // 後部分が完全に空の場合
                result = trimmedBefore + beforeNewline;
            }
            else
            {
                // 両方に内容がある場合
                if (!string.IsNullOrEmpty(beforeNewline) || !string.IsNullOrEmpty(afterNewline))
                {
                    // 改行がある場合は改行を優先
                    result = trimmedBefore + beforeNewline + afterNewline + trimmedAfter;
                }
                else
                {
                    // 改行がない場合は適切にカンマとスペースを追加
                    if (trimmedAfter.StartsWith(","))
                    {
                        result = trimmedBefore + trimmedAfter;
                    }
                    else if (!string.IsNullOrEmpty(trimmedBefore) && !string.IsNullOrEmpty(trimmedAfter))
                    {
                        result = trimmedBefore + ", " + trimmedAfter;
                    }
                    else
                    {
                        result = trimmedBefore + trimmedAfter;
                    }
                }
            }
            
            return result;
        }
    }

    /// <summary>
    /// AvalonEditのCompletionWindow用のCompletionDataクラス
    /// </summary>
    public class TagCompletionData : ICompletionData
    {
        private readonly TagItem _tagItem;

        public TagCompletionData(TagItem tagItem)
        {
            _tagItem = tagItem ?? throw new ArgumentNullException(nameof(tagItem));
        }

        public ImageSource? Image => null;

        public string Text => _tagItem.InsertText;

        public object Content
        {
            get
            {
                var panel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                
                var nameText = new TextBlock
                {
                    Text = _tagItem.DisplayText,
                    FontWeight = FontWeights.Medium,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(nameText);
                
                // 使用頻度表示を追加
                if (!string.IsNullOrEmpty(_tagItem.UsageDisplay))
                {
                    var usageText = new TextBlock
                    {
                        Text = _tagItem.UsageDisplay,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0)), // オレンジ色
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, 0, 0, 0)
                    };
                    panel.Children.Add(usageText);
                }
                
                if (!string.IsNullOrEmpty(_tagItem.CountDisplay))
                {
                    var countText = new TextBlock
                    {
                        Text = _tagItem.CountDisplay,
                        Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    panel.Children.Add(countText);
                }
                
                return panel;
            }
        }

        #nullable disable
        public object Description => null; // ツールチップを無効化
        #nullable enable

        public double Priority => _tagItem.CalculatedPriority;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            try
            {
                string insertText = _tagItem.InsertText;
                
                // 現在のテキストとキャレット位置を取得
                string fullText = textArea.Document.Text;
                int caretPosition = textArea.Caret.Offset;
                
                // ネガティブプロンプトかどうかを判定（簡易的な判定）
                bool isNegative = IsInNegativePrompt(fullText, completionSegment.Offset);
                
                // LoRAパターンかどうかをチェック
                bool isLoraPattern = IsLoraPattern(fullText, completionSegment.Offset);
                
                if (isLoraPattern)
                {
                    // 短縮形かどうかをチェック
                    bool isShortForm = IsShortFormLoraPattern(fullText, completionSegment.Offset);
                    
                    if (isShortForm)
                    {
                        // 短縮形の場合は<lora:形式に変換
                        insertText = $"lora:{_tagItem.InsertText}:1.0>";
                    }
                    else
                    {
                        // 通常のLoRAの場合は重み付きで挿入
                        insertText = $"{_tagItem.InsertText}:1.0>";
                    }
                    
                    // 次にコンマを追加
                    if (completionSegment.EndOffset < fullText.Length && fullText[completionSegment.EndOffset] != ',')
                    {
                        insertText += ", ";
                    }
                    else if (completionSegment.EndOffset >= fullText.Length)
                    {
                        insertText += ", ";
                    }
                }
                else
                {
                    // 通常のタグの場合
                    insertText += ", ";
                }
                
                // テキストを置換
                textArea.Document.Replace(completionSegment, insertText);
                
                // キャレット位置を適切に設定
                textArea.Caret.Offset = completionSegment.Offset + insertText.Length;
                
                // 使用回数をデータベースに記録（非同期で実行）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        int tagType = _tagItem.IsLora ? 1 : 0; // 0=通常タグ、1=LoRA
                        await TagUsageDatabase.Instance.IncreaseUsageCountAsync(_tagItem.ActualTag, tagType, isNegative);
                        Debug.WriteLine($"使用回数を記録しました: {_tagItem.ActualTag} (type: {tagType}, negative: {isNegative})");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"使用回数記録エラー: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"オートコンプリート挿入エラー: {ex.Message}");
                // フォールバック: シンプルな置換
                textArea.Document.Replace(completionSegment, _tagItem.InsertText);
            }
        }
        
        /// <summary>
        /// ネガティブプロンプト内かどうかを判定（簡易的な実装）
        /// </summary>
        private bool IsInNegativePrompt(string text, int position)
        {
            // 簡易的な判定：「Negative prompt:」の後にあるかどうか
            int negativeIndex = text.LastIndexOf("Negative prompt:", position, StringComparison.OrdinalIgnoreCase);
            if (negativeIndex >= 0)
            {
                // Negative prompt: の後で、次の改行より前にある場合はネガティブ
                int nextLineIndex = text.IndexOf('\n', negativeIndex);
                if (nextLineIndex == -1 || position < nextLineIndex)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// LoRAパターンかどうかを判定
        /// </summary>
        private bool IsLoraPattern(string text, int position)
        {
            const string loraPrefix = "<lora:";
            
            // まず通常の<lora:パターンをチェック
            for (int i = position; i >= loraPrefix.Length - 1; i--)
            {
                if (i >= loraPrefix.Length - 1 && i - loraPrefix.Length + 1 >= 0)
                {
                    string substr = text.Substring(i - loraPrefix.Length + 1, loraPrefix.Length);
                    if (substr == loraPrefix)
                    {
                        // <lora: パターンが見つかった場合
                        // positionがこのLoRAタグの範囲内にあるかチェック
                        int loraStart = i - loraPrefix.Length + 1;
                        int loraEnd = text.IndexOf('>', i + 1);
                        if (loraEnd == -1) loraEnd = text.Length;
                        
                        // positionがLoRAタグの範囲内にある場合のみtrueを返す
                        if (position >= loraStart && position <= loraEnd)
                        {
                            return true;
                        }
                    }
                }
            }
            
            // 次に短縮形の<パターンをチェック
            for (int i = position; i >= 0; i--)
            {
                if (text[i] == '<')
                {
                    // < の後の内容を確認
                    int endPos = text.IndexOf('>', i + 1);
                    if (endPos == -1) endPos = text.Length;
                    
                    // positionが< ... >の範囲内にあるかチェック
                    if (position >= i && position <= endPos)
                    {
                        string content = text.Substring(i + 1, Math.Min(endPos - i - 1, position - i - 1));
                        
                        // 短縮形と判定（loraで始まっていない）
                        if (!content.StartsWith("lora:", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 短縮形のLoRAパターンかどうかを判定
        /// </summary>
        private bool IsShortFormLoraPattern(string text, int position)
        {
            for (int i = position; i >= 0; i--)
            {
                if (text[i] == '<')
                {
                    // < の後の内容を確認
                    int endPos = text.IndexOf('>', i + 1);
                    if (endPos == -1) endPos = text.Length;
                    
                    // positionが< ... >の範囲内にあるかチェック
                    if (position >= i && position <= endPos)
                    {
                        string content = text.Substring(i + 1, Math.Min(endPos - i - 1, position - i - 1));
                        
                        // 短縮形と判定（loraで始まっていない）
                        if (!content.StartsWith("lora:", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// アンダースコアをスペースに変換
        /// </summary>
        private string ConvertUnderscoreToSpace(string text)
        {
            return text?.Replace("_", " ") ?? "";
        }
    }

    /// <summary>
    /// 括弧ハイライト用レンダラー
    /// </summary>
    public class ParenthesesHighlightRenderer : IBackgroundRenderer
    {
        private readonly List<(int Start, int End)> _highlights = new List<(int, int)>();
        
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document == null) return;
            
            foreach (var (start, end) in _highlights)
            {
                try
                {
                    // オフセットが有効範囲内かチェック
                    if (start >= 0 && end <= textView.Document.TextLength && start < end)
                    {
                        // 1文字のハイライトのみを描画
                        var startLocation = textView.Document.GetLocation(start);
                        var endLocation = textView.Document.GetLocation(end);
                        
                        // 同じ行の場合のみ処理（1文字のハイライト）
                        if (startLocation.Line == endLocation.Line)
                        {
                            var visualLine = textView.GetVisualLine(startLocation.Line);
                            if (visualLine != null)
                            {
                                try
                                {
                                    // 1文字分の矩形を取得
                                    var rects = BackgroundGeometryBuilder.GetRectsFromVisualSegment(
                                        textView, 
                                        visualLine, 
                                        startLocation.Column - 1, 
                                        1); // 1文字分のみ
                                    
                                    // 薄い緑色で1文字のみをハイライト
                                    var brush = new SolidColorBrush(Color.FromArgb(60, 0, 200, 0));
                                    foreach (var rect in rects)
                                    {
                                        // 矩形のサイズを制限して確実に1文字分のみにする
                                        var limitedRect = new Rect(rect.X, rect.Y, 
                                            Math.Min(rect.Width, 20), // 最大20ピクセル幅
                                            rect.Height);
                                        drawingContext.DrawRectangle(brush, null, limitedRect);
                                    }
                                }
                                catch (ArgumentException)
                                {
                                    // VisualLine関連のエラーは無視
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // エラーが発生した場合はスキップ
                    Debug.WriteLine($"括弧ハイライト描画エラー: {ex.Message}");
                }
            }
        }

        public void AddHighlight(int start, int end)
        {
            _highlights.Add((start, end));
        }

        public void ClearHighlights()
        {
            _highlights.Clear();
        }
    }

    /// <summary>
    /// タグアイテムクラス（既存のまま）
    /// </summary>
    public class TagItem
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        public string? AliasSource { get; set; }
        public bool IsLora { get; set; } = false;
        
        // 使用回数情報を追加
        public int UsageCount { get; set; } = 0;
        public bool IsFrequentlyUsed { get; set; } = false;
        public DateTime LastUsedDate { get; set; } = DateTime.MinValue;
        
        public string CountDisplay
        {
            get
            {
                if (Count > 1000000)
                    return $"({Count / 1000000.0:F1}M)";
                else if (Count > 1000)
                    return $"({Count / 1000.0:F1}K)";
                else if (Count > 0)
                    return $"({Count})";
                else
                    return "";
            }
        }
        
        /// <summary>
        /// 使用頻度表示（よく使われるタグには✨マークを表示）
        /// </summary>
        public string UsageDisplay
        {
            get
            {
                if (IsFrequentlyUsed)
                    return "✨"; // よく使われるタグには✨のみ
                //else if (UsageCount > 0)
                    //return "🔁"; // 使用したことがあるタグには🔁のみ
                else
                    return "";
            }
        }

        public string ActualTag => AliasSource ?? Name;

        /// <summary>
        /// 表示用テキスト（エイリアスの場合は「alias → original」形式）
        /// </summary>
        public string DisplayText 
        {
            get
            {
                if (!string.IsNullOrEmpty(AliasSource))
                {
                    // エイリアスの場合：「エイリアス名 → 元のタグ名」形式
                    return $"{ConvertUnderscoreToSpace(Name)} → {ConvertUnderscoreToSpace(AliasSource)}";
                }
                else
                {
                    // 通常のタグの場合：タグ名をそのまま表示
                    return ConvertUnderscoreToSpace(Name);
                }
            }
        }

        /// <summary>
        /// 挿入用テキスト（エイリアスの場合は元のタグ、通常は自分自身）
        /// </summary>
        public string InsertText 
        {
            get
            {
                string text;
                
                if (!string.IsNullOrEmpty(AliasSource))
                {
                    // エイリアスの場合：元のタグを挿入
                    text = IsLora ? AliasSource : ConvertUnderscoreToSpace(AliasSource);
                }
                else
                {
                    // 通常のタグの場合：自分自身を挿入
                    text = IsLora ? Name : ConvertUnderscoreToSpace(Name);
                }
                
                return text;
            }
        }
        
        /// <summary>
        /// 使用頻度を考慮した優先度を計算
        /// </summary>
        public double CalculatedPriority
        {
            get
            {
                return TagUsageDatabase.CalculateUsageBias(Count, UsageCount, FrequencyFunction.Logarithmic, 3);
            }
        }

        /// <summary>
        /// アンダースコアをスペースに変換
        /// </summary>
        private string ConvertUnderscoreToSpace(string text)
        {
            return text?.Replace("_", " ") ?? "";
        }
    }

    /// <summary>
    /// ワイルドカードアイテムクラス
    /// </summary>
    public class WildcardItem
    {
        public string Name { get; set; } = "";
        public int EntryCount { get; set; }
        
        public string DisplayText => $"{Name} (Wildcard)";  // $記号を削除
        public string EntryCountDisplay => $"({EntryCount} entries)";
    }

    /// <summary>
    /// ワイルドカード用のCompletionDataクラス
    /// </summary>
    public class WildcardCompletionData : ICompletionData
    {
        private readonly WildcardItem _wildcardItem;

        public WildcardCompletionData(WildcardItem wildcardItem)
        {
            _wildcardItem = wildcardItem ?? throw new ArgumentNullException(nameof(wildcardItem));
        }

        public ImageSource? Image => null;

        public string Text => _wildcardItem.Name;

        public object Content
        {
            get
            {
                var panel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                
                // ワイルドカード名を緑色で表示
                var nameText = new TextBlock
                {
                    Text = _wildcardItem.DisplayText,
                    FontWeight = FontWeights.Medium,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(34, 139, 34)), // フォレストグリーン
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(nameText);
                
                // エントリ数を表示
                var countText = new TextBlock
                {
                    Text = _wildcardItem.EntryCountDisplay,
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                panel.Children.Add(countText);
                
                return panel;
            }
        }

        #nullable disable
        public object Description => null; // ツールチップを無効化
        #nullable enable

        public double Priority => _wildcardItem.EntryCount;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            try
            {
                // 7. 候補が選ばれたら、__$wildcard__が入力される
                string insertText = $"__{_wildcardItem.Name}__";
                
                Debug.WriteLine($"ワイルドカード挿入: '{insertText}'");
                
                // テキストを置換
                textArea.Document.Replace(completionSegment, insertText);
                
                // キャレット位置を適切に設定（挿入したテキストの直後）
                textArea.Caret.Offset = completionSegment.Offset + insertText.Length;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ワイルドカード挿入エラー: {ex.Message}");
                // フォールバック: シンプルな置換
                textArea.Document.Replace(completionSegment, $"__{_wildcardItem.Name}__");
            }
        }
    }

    /// <summary>
    /// ワイルドカード内容アイテムクラス
    /// </summary>
    public class WildcardContentItem
    {
        public string Content { get; set; } = "";
        public string WildcardName { get; set; } = "";
        
        public string DisplayText => Content;
        public string WildcardDisplay => $"({WildcardName})";
    }

    /// <summary>
    /// ワイルドカード内容用のCompletionDataクラス
    /// </summary>
    public class WildcardContentCompletionData : ICompletionData
    {
        private readonly WildcardContentItem _contentItem;

        public WildcardContentCompletionData(WildcardContentItem contentItem)
        {
            _contentItem = contentItem ?? throw new ArgumentNullException(nameof(contentItem));
        }

        public ImageSource? Image => null;

        public string Text => _contentItem.Content;

        public object Content
        {
            get
            {
                var panel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                
                // ワイルドカード内容を通常の黒色で表示
                var contentText = new TextBlock
                {
                    Text = _contentItem.DisplayText,
                    FontWeight = FontWeights.Normal,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Colors.Black),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(contentText);
                
                // ワイルドカード名を表示
                var wildcardText = new TextBlock
                {
                    Text = _contentItem.WildcardDisplay,
                    Foreground = new SolidColorBrush(Color.FromRgb(32, 178, 170)), // ライトシーグリーン
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                panel.Children.Add(wildcardText);
                
                return panel;
            }
        }

        #nullable disable
        public object Description => null; // ツールチップを無効化
        #nullable enable

        public double Priority => 1.0; // 全て同じ優先度

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            try
            {
                // ワイルドカード内容をそのまま挿入（カンマ区切り追加）
                string insertText = $"{_contentItem.Content}, ";
                
                Debug.WriteLine($"ワイルドカード内容挿入: '{_contentItem.Content}' from {_contentItem.WildcardName}");
                
                // テキストを置換
                textArea.Document.Replace(completionSegment, insertText);
                
                // キャレット位置を適切に設定（挿入したテキストの直後）
                textArea.Caret.Offset = completionSegment.Offset + insertText.Length;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ワイルドカード内容挿入エラー: {ex.Message}");
                // フォールバック: シンプルな置換
                textArea.Document.Replace(completionSegment, _contentItem.Content);
            }
        }
    }
} 