using System;
using System.IO;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;

namespace SD.Yuzu
{
    public class ResolutionPreset
    {
        public string Portrait { get; set; } = "1024,1360";  // 縦長
        public string Landscape { get; set; } = "1360,1024"; // 横長
        public string Square { get; set; } = "1360,1360";    // 正方
    }

    public class AppSettings
    {
        private static AppSettings? _instance;
        private static readonly object _lock = new object();
        private const string SETTINGS_DIRECTORY = "settings";
        private const string SETTINGS_FILE = "app_settings.json";

        public string BaseUrl { get; set; } = "http://127.0.0.1:7860";
        public string LoraDirectory { get; set; } = @"";
        public string StableDiffusionDirectory { get; set; } = @"";
        public string AutoCompleteTagFile { get; set; } = @"";
        public string Language { get; set; } = GetDefaultLanguage();
        
        // Batch settings for new tabs
        public int DefaultBatchSize { get; set; } = 4;
        public int DefaultBatchCount { get; set; } = 1;
        
        // Image response limit
        public int DefaultImageLimit { get; set; } = 20;
        
        // Image grid columns setting
        public int DefaultImageGridColumns { get; set; } = 4;

        // Resolution presets
        public ResolutionPreset SmallPresets { get; set; } = new ResolutionPreset
        {
            Portrait = "1024,1360",   // 縦長
            Landscape = "1360,1024",  // 横長
            Square = "1360,1360"      // 正方
        };

        public ResolutionPreset MediumPresets { get; set; } = new ResolutionPreset
        {
            Portrait = "1331,1768",   // 縦長M
            Landscape = "1768,1331",  // 横長M
            Square = "1768,1768"      // 正方M
        };

        public ResolutionPreset LargePresets { get; set; } = new ResolutionPreset
        {
            Portrait = "1536,2040",   // 縦長L
            Landscape = "2040,1536",  // 横長L
            Square = "2040,2040"      // 正方L
        };

        /// <summary>
        /// 設定からプリセット辞書を動的に生成
        /// </summary>
        public Dictionary<string, (int width, int height)> GetResolutionPresets()
        {
            var presets = new Dictionary<string, (int width, int height)>();
            
            // Small presets
            if (TryParseResolution(SmallPresets.Portrait, out var smallPortrait))
                presets["縦長"] = smallPortrait;
            if (TryParseResolution(SmallPresets.Landscape, out var smallLandscape))
                presets["横長"] = smallLandscape;
            if (TryParseResolution(SmallPresets.Square, out var smallSquare))
                presets["正方"] = smallSquare;
                
            // Medium presets
            if (TryParseResolution(MediumPresets.Portrait, out var mediumPortrait))
                presets["縦長M"] = mediumPortrait;
            if (TryParseResolution(MediumPresets.Landscape, out var mediumLandscape))
                presets["横長M"] = mediumLandscape;
            if (TryParseResolution(MediumPresets.Square, out var mediumSquare))
                presets["正方M"] = mediumSquare;
                
            // Large presets
            if (TryParseResolution(LargePresets.Portrait, out var largePortrait))
                presets["縦長L"] = largePortrait;
            if (TryParseResolution(LargePresets.Landscape, out var largeLandscape))
                presets["横長L"] = largeLandscape;
            if (TryParseResolution(LargePresets.Square, out var largeSquare))
                presets["正方L"] = largeSquare;
                
            return presets;
        }

        /// <summary>
        /// "width,height" 形式の文字列を解析
        /// </summary>
        private bool TryParseResolution(string resolutionString, out (int width, int height) resolution)
        {
            resolution = (0, 0);
            
            if (string.IsNullOrWhiteSpace(resolutionString))
                return false;
                
            var parts = resolutionString.Split(',');
            if (parts.Length != 2)
                return false;
                
            if (int.TryParse(parts[0].Trim(), out int width) && 
                int.TryParse(parts[1].Trim(), out int height) && 
                width > 0 && height > 0)
            {
                resolution = (width, height);
                return true;
            }
            
            return false;
        }

        private static string GetDefaultLanguage()
        {
            // システムの言語を取得し、日本語の場合はja-JP、それ以外はen-USを返す
            var culture = CultureInfo.CurrentCulture;
            var result = culture.Name.StartsWith("ja") ? "ja-JP" : "en-US";
            System.Diagnostics.Debug.WriteLine($"Default language determined: {result} (from culture: {culture.Name})");
            return result;
        }

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = LoadFromFile();
                        }
                    }
                }
                return _instance;
            }
        }

        private static AppSettings LoadFromFile()
        {
            try
            {
                string settingsPath = Path.Combine(SETTINGS_DIRECTORY, SETTINGS_FILE);
                
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    System.Diagnostics.Debug.WriteLine($"Loading settings from file: {json}");
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Loaded language from file: {settings.Language}");
                        return settings;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Settings file not found: {settingsPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
            }

            var newSettings = new AppSettings();
            System.Diagnostics.Debug.WriteLine($"Creating new settings with language: {newSettings.Language}");
            return newSettings;
        }

        public void SaveToFile()
        {
            try
            {
                if (!Directory.Exists(SETTINGS_DIRECTORY))
                {
                    Directory.CreateDirectory(SETTINGS_DIRECTORY);
                }

                string settingsPath = Path.Combine(SETTINGS_DIRECTORY, SETTINGS_FILE);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string json = JsonSerializer.Serialize(this, options);
                System.Diagnostics.Debug.WriteLine($"Saving settings to file: {json}");
                File.WriteAllText(settingsPath, json);
                System.Diagnostics.Debug.WriteLine($"Settings saved successfully to: {settingsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
                throw;
            }
        }
    }
} 