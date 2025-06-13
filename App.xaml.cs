using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SD.Yuzu
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // BASE_URLを動的に取得するプロパティに変更
        public static string BASE_URL => AppSettings.Instance.BaseUrl;
        
        private static Mutex? _mutex = null;
        private static string GetMutexName()
        {
            // 実行ファイルのフルパスを取得してMutex名として使用
            string exePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            // パス区切り文字をアンダースコアに置換してMutex名として有効にする
            return "SDYuzu_" + exePath.Replace('\\', '_').Replace(':', '_').Replace('/', '_');
        }

        // Win32 API for window manipulation
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 実行ファイルの場所をカレントディレクトリに設定
            string? executableDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(executableDirectory))
            {
                Directory.SetCurrentDirectory(executableDirectory);
            }
            // 二重起動チェック
            bool createdNew;
            _mutex = new Mutex(true, GetMutexName(), out createdNew);

            if (!createdNew)
            {
                // 既存のインスタンスが存在する場合
                BringExistingInstanceToFront();
                Current.Shutdown();
                return;
            }

            // 設定を初期化（ファイルから読み込み）
            try
            {
                var settings = AppSettings.Instance; // これにより設定ファイルが読み込まれる
                System.Diagnostics.Debug.WriteLine($"設定読み込み完了: BaseUrl={settings.BaseUrl}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            base.OnStartup(e);
            
            // メモリ使用量削減のための設定
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
        }

        private void BringExistingInstanceToFront()
        {
            try
            {
                // 現在のプロセス名を取得
                string currentProcessName = Process.GetCurrentProcess().ProcessName;
                
                // 同じ名前のプロセスを検索
                Process[] processes = Process.GetProcessesByName(currentProcessName);
                
                foreach (Process process in processes)
                {
                    // 自分自身以外のプロセスを探す
                    if (process.Id != Process.GetCurrentProcess().Id)
                    {
                        IntPtr hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            // ウィンドウが最小化されている場合は復元
                            if (IsIconic(hWnd))
                            {
                                ShowWindow(hWnd, SW_RESTORE);
                            }
                            
                            // ウィンドウを前面に表示
                            SetForegroundWindow(hWnd);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // エラーが発生した場合はログに記録（デバッグ用）
                System.Diagnostics.Debug.WriteLine($"既存インスタンスの前面表示に失敗しました: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Mutexを解放
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
