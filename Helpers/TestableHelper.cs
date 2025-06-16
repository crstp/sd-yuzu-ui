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

        /// <summary>
        /// カンマ区切り範囲の削除ロジック（UI非依存・テスト用）
        /// </summary>
        public static (string newText, int newCaretPosition) DeleteCommaDelimitedSectionCore(
            string text, int caretPosition)
        {
            // --- ここからAutoCompleteTextBox.xaml.csのロジックをコピペ（UI依存部分は除去） ---
            var bounds = FindCommaDelimitedBounds(text, caretPosition);
            if (!bounds.HasValue && caretPosition > 0 &&
                (text[caretPosition - 1] == ',' || text[caretPosition - 1] == ' ' || text[caretPosition - 1] == '\t'))
            {
                int checkPos = caretPosition;
                while (checkPos < text.Length && (text[checkPos] == ' ' || text[checkPos] == '\t'))
                {
                    checkPos++;
                }

                if (checkPos >= text.Length || text[checkPos] == '\n' || text[checkPos] == '\r')
                {
                    int newPos = caretPosition - 1;
                    while (newPos > 0 && (text[newPos] == ' ' || text[newPos] == '\t'))
                    {
                        newPos--;
                    }

                    bounds = FindCommaDelimitedBounds(text, Math.Max(0, newPos));
                }
            }
            if (bounds.HasValue)
            {
                int deleteStart = bounds.Value.start;
                int deleteLength = bounds.Value.length;
                int deleteEnd = deleteStart + deleteLength;

                // 末尾のタグかどうかをチェック
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
                bool isLastTagBeforeNewline = foundNewline && hasContentAfterNewline;
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
                int adjustedStart = deleteStart;
                int adjustedEnd = deleteEnd;
                if (isLastTag && !isFirstTag)
                {
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
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                string beforeSection = adjustedStart > 0 ? text.Substring(0, adjustedStart) : "";
                string afterSection = adjustedEnd < text.Length ? text.Substring(adjustedEnd) : "";
                string newText = CleanupAfterDeletion(beforeSection, afterSection);
                string formattedText = FormatPromptText(newText);
                string formattedBefore = FormatPromptText(beforeSection);
                int newCaretPosition = formattedBefore.Length;
                if (formattedText.Length > newCaretPosition && formattedText[newCaretPosition] == ',')
                {
                    newCaretPosition++;
                    while (newCaretPosition < formattedText.Length && formattedText[newCaretPosition] == ' ')
                    {
                        newCaretPosition++;
                    }
                }
                return (formattedText, Math.Min(newCaretPosition, formattedText.Length));
            }
            else
            {
                return (text, caretPosition);
            }
        }

        // --- 依存メソッドもコピペ（private→public static化） ---
        public static (int start, int length)? FindCommaDelimitedBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;
            bool isInsideBrackets = false;
            int openBracketPos = -1;
            int closeBracketPos = -1;
            for (int i = caretPosition; i >= 0; i--)
            {
                if (text[i] == '(')
                {
                    if (i > 0 && text[i - 1] == '\\')
                    {
                        continue;
                    }
                    openBracketPos = i;
                    break;
                }
                else if (text[i] == ')')
                {
                    break;
                }
            }
            if (openBracketPos >= 0)
            {
                int depth = 1;
                for (int i = openBracketPos + 1; i < text.Length; i++)
                {
                    if (text[i] == '(')
                    {
                        if (i > 0 && text[i - 1] == '\\')
                        {
                            continue;
                        }
                        depth++;
                    }
                    else if (text[i] == ')')
                    {
                        if (i > 0 && text[i - 1] == '\\')
                        {
                            continue;
                        }
                        depth--;
                        if (depth == 0)
                        {
                            closeBracketPos = i;
                            break;
                        }
                    }
                }
                if (closeBracketPos >= 0 && caretPosition > openBracketPos && caretPosition <= closeBracketPos)
                {
                    isInsideBrackets = true;
                }
            }
            if (isInsideBrackets && openBracketPos >= 0 && closeBracketPos >= 0)
            {
                return (openBracketPos, closeBracketPos - openBracketPos + 1);
            }
            var loraTagBounds = FindLoRATagBounds(text, caretPosition);
            if (loraTagBounds.HasValue)
            {
                return loraTagBounds;
            }
            int start = caretPosition;
            int bracketDepth = 0;
            while (start > 0)
            {
                char c = text[start - 1];
                if (c == ')')
                {
                    if (start > 1 && text[start - 2] == '\\')
                    {
                        start--;
                        continue;
                    }
                    bracketDepth++;
                }
                else if (c == '(')
                {
                    if (start > 1 && text[start - 2] == '\\')
                    {
                        start--;
                        continue;
                    }
                    bracketDepth--;
                }
                else if (c == ',' && bracketDepth == 0)
                {
                    break;
                }
                else if ((c == '\n' || c == '\r') && bracketDepth == 0)
                {
                    break;
                }
                start--;
            }
            int end = caretPosition;
            bracketDepth = 0;
            while (end < text.Length)
            {
                char c = text[end];
                if (c == '(')
                {
                    if (end > 0 && text[end - 1] == '\\')
                    {
                        end++;
                        continue;
                    }
                    bracketDepth++;
                }
                else if (c == ')')
                {
                    if (end > 0 && text[end - 1] == '\\')
                    {
                        end++;
                        continue;
                    }
                    bracketDepth--;
                }
                else if (c == ',' && bracketDepth == 0)
                {
                    break;
                }
                else if ((c == '\n' || c == '\r') && bracketDepth == 0)
                {
                    break;
                }
                end++;
            }
            while (start < end && (text[start] == ' ' || text[start] == '\t'))
            {
                start++;
            }
            while (end > start && end - 1 < text.Length && (text[end - 1] == ' ' || text[end - 1] == '\t'))
            {
                end--;
            }
            if (end > start)
            {
                int length = end - start;
                string content = text.Substring(start, length);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return null;
                }
                return (start, length);
            }
            return null;
        }
        public static (int start, int length)? FindLoRATagBounds(string text, int caretPosition)
        {
            if (string.IsNullOrEmpty(text) || caretPosition < 0 || caretPosition > text.Length)
                return null;
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
                    break;
                }
            }
            if (start == -1)
                return null;
            int end = -1;
            for (int i = start + 1; i < text.Length; i++)
            {
                if (text[i] == '>')
                {
                    end = i + 1;
                    break;
                }
                else if (text[i] == ',' || text[i] == '\n' || text[i] == '\r')
                {
                    return null;
                }
            }
            if (end == -1)
                return null;
            if (caretPosition >= start && caretPosition <= end)
            {
                int length = end - start;
                return (start, length);
            }
            return null;
        }
        public static string CleanupAfterDeletion(string beforeSection, string afterSection)
        {
            bool beforeEndsWithNewline = beforeSection.EndsWith("\n") || beforeSection.EndsWith("\r\n") || beforeSection.EndsWith("\r");
            bool afterStartsWithNewline = afterSection.StartsWith("\n") || afterSection.StartsWith("\r\n") || afterSection.StartsWith("\r");
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
            string trimmedBefore = beforeSection.TrimEnd();
            string trimmedAfter = afterSection.TrimStart();
            if (trimmedBefore.EndsWith(",") && trimmedAfter.StartsWith(","))
            {
                trimmedAfter = trimmedAfter.Substring(1).TrimStart();
            }
            else if (trimmedBefore.EndsWith(",") && (string.IsNullOrWhiteSpace(trimmedAfter) || string.IsNullOrWhiteSpace(afterSection)))
            {
                trimmedBefore = trimmedBefore.Substring(0, trimmedBefore.Length - 1).TrimEnd();
            }
            else if (string.IsNullOrWhiteSpace(trimmedBefore) && string.IsNullOrWhiteSpace(beforeSection) && trimmedAfter.StartsWith(","))
            {
                trimmedAfter = trimmedAfter.Substring(1).TrimStart();
            }
            string result = "";
            if (string.IsNullOrWhiteSpace(trimmedBefore) && string.IsNullOrWhiteSpace(beforeSection))
            {
                result = afterNewline + trimmedAfter;
            }
            else if (string.IsNullOrWhiteSpace(trimmedAfter) && string.IsNullOrWhiteSpace(afterSection))
            {
                result = trimmedBefore + beforeNewline;
            }
            else
            {
                if (!string.IsNullOrEmpty(beforeNewline) || !string.IsNullOrEmpty(afterNewline))
                {
                    result = trimmedBefore + beforeNewline + afterNewline + trimmedAfter;
                }
                else
                {
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
        public static string FormatPromptText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"^[\s,]+", "");
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*,\s*", ",");
                int safetyCounter = 0;
                while (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\s*,\s*,") && safetyCounter < 10)
                {
                    lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*,\s*", ",");
                    safetyCounter++;
                }
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @"\s*,\s*", ", ");
                lines[i] = System.Text.RegularExpressions.Regex.Replace(lines[i], @" {2,}", " ");
            }
            string result = string.Join(Environment.NewLine, lines);
            return result;
        }
    }
} 