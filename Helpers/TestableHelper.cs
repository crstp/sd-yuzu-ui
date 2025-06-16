namespace SD.Yuzu.Helpers
{
    /// <summary>
    /// テスト可能な機能を提供するヘルパークラス
    /// </summary>
    public static class TestableHelper
    {
        /// <summary>
        /// 挨拶メッセージを生成します
        /// </summary>
        /// <param name="name">挨拶する相手の名前</param>
        /// <returns>挨拶メッセージ</returns>
        public static string GetGreeting(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Hello, World!";
            }
            
            return $"Hello, {name}!";
        }
    }
} 