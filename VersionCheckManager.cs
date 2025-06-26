using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Threading;

namespace SD.Yuzu
{
    public class VersionCheckManager
    {
        private const string VERSION_URL = "https://raw.githubusercontent.com/crstp/sd-yuzu-ui/refs/heads/main/SD.Yuzu.csproj";
        private const int CHECK_DELAY_SECONDS = 5;
        
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly DispatcherTimer _timer;
        private readonly Action<bool> _updateUICallback;
        
        public VersionCheckManager(Action<bool> updateUICallback)
        {
            _updateUICallback = updateUICallback;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(CHECK_DELAY_SECONDS);
            _timer.Tick += OnTimerTick;
        }
        
        public void StartVersionCheck()
        {
            _timer.Start();
        }
        
        private async void OnTimerTick(object? sender, EventArgs e)
        {
            _timer.Stop(); // 一度だけ実行
            
            try
            {
                bool hasNewVersion = await CheckForNewVersionAsync();
                _updateUICallback(hasNewVersion);
            }
            catch
            {
                // 読み込みが失敗したら何もしない（仕様通り）
                _updateUICallback(false);
            }
        }
        
        private async Task<bool> CheckForNewVersionAsync()
        {
            try
            {
                // 現在のバージョンを取得
                var currentVersion = GetCurrentVersion();
                if (currentVersion == null)
                {
                    return false;
                }
                
                // リモートのバージョンを取得
                var remoteVersion = await GetRemoteVersionAsync();
                if (remoteVersion == null)
                {
                    return false;
                }
                
                // バージョンを比較
                return IsNewerVersion(remoteVersion, currentVersion);
            }
            catch
            {
                return false;
            }
        }
        
        private Version? GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version;
            }
            catch
            {
                return null;
            }
        }
        
        private async Task<Version?> GetRemoteVersionAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(VERSION_URL);
                response.EnsureSuccessStatusCode();
                
                var xmlContent = await response.Content.ReadAsStringAsync();
                var doc = XDocument.Parse(xmlContent);
                
                // <Version>タグからバージョンを取得
                var versionElement = doc.Root?.Element("PropertyGroup")?.Element("Version");
                if (versionElement != null && !string.IsNullOrWhiteSpace(versionElement.Value))
                {
                    if (Version.TryParse(versionElement.Value, out var version))
                    {
                        return version;
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private static bool IsNewerVersion(Version remoteVersion, Version currentVersion)
        {
            return remoteVersion > currentVersion;
        }
        
        public void Dispose()
        {
            _timer?.Stop();
        }
    }
} 