using System;
using System.Linq;
using Xunit;
using SD.Yuzu.Helpers;

namespace SD.Yuzu.Tests
{
    public class TestableHelperTests
    {
        /// <summary>
        /// キャレット位置を'|'で指定して文字列と位置を取得します
        /// </summary>
        private static (string text, int caret) ParseWithCaret(string inputWithCaret)
        {
            int caret = inputWithCaret.IndexOf('|');
            var text = inputWithCaret.Remove(caret, 1);
            return (text, caret);
        }

        /// <summary>
        /// キャレット範囲を'|'で2つ指定して文字列と開始・終了位置を取得します
        /// </summary>
        private static (string text, int start, int end) ParseWithCaretRange(string inputWithCarets)
        {
            var indices = inputWithCarets.Select((ch, i) => new { ch, i })
                                         .Where(x => x.ch == '|')
                                         .Select(x => x.i)
                                         .ToList();
            if (indices.Count == 1)
                indices.Add(indices[0]);
            if (indices.Count != 2)
                throw new ArgumentException("Input must contain 1 or 2 carets");
            var low = Math.Min(indices[0], indices[1]);
            var high = Math.Max(indices[0], indices[1]);
            var text = inputWithCarets.Remove(high, 1).Remove(low, 1);
            return (text, low, high - 1);
        }

        /// <summary>
        /// 範囲内のすべてのキャレット位置でアクションを実行します
        /// </summary>
        private static void ForEachCaret(string inputWithCarets, Action<string, int> testAction)
        {
            var (text, start, end) = ParseWithCaretRange(inputWithCarets);
            for (int caret = start; caret <= end; caret++)
            {
                testAction(text, caret);
            }
        }

        [Fact]
        public void GetGreeting_WithEmptyName_ReturnsHelloWorld()
        {
            // Arrange
            string name = "";

            // Act
            string result = TestableHelper.GetGreeting(name);

            // Assert
            Assert.Equal("Hello, World!", result);
        }

        [Fact]
        public void GetGreeting_WithName_ReturnsHelloName()
        {
            // Arrange
            string name = "Yuzu";

            // Act
            string result = TestableHelper.GetGreeting(name);

            // Assert
            Assert.Equal("Hello, Yuzu!", result);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_NormalCase_RemovesMiddleTag()
        {
            ForEachCaret("a,| b|, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result);
                Assert.Equal(3, newCaret); // 'a,'の後
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveFirstTag()
        {
            ForEachCaret("|a|, b, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("b, c", result);
                Assert.Equal(0, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLastTag()
        {
            ForEachCaret("a, b,| c|", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, b", result);
                Assert.Equal(4, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithSpaces()
        {
            ForEachCaret("a ,|  b  |, c ", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result.TrimEnd()); // 末尾スペースは無視
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithTabs()
        {
            ForEachCaret("a,\t|b,\tc", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result);
                Assert.Equal(2, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithNewlines()
        {
            ForEachCaret("a,\n|b,\nc", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result);
                Assert.Equal(2, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveOnlyTag()
        {
            ForEachCaret("|a|", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("", result); // 挙動通り、全体が削除される
                Assert.Equal(0, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithMultipleCommas()
        {
            ForEachCaret("a,,|b|,,c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result); // 期待値はロジック準拠
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLoraTag()
        {
            ForEachCaret("a,| <lora:foo>|, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveBracketedTag()
        {
            ForEachCaret("a,| (b:1.2)|, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_CaretOnComma()
        {
            ForEachCaret("a,| b|, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result); // 挙動通り、削除される
                Assert.Equal(2, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_EmptyString()
        {
            ForEachCaret("|", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("", result);
                Assert.Equal(0, newCaret);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingSpaces()
        {
            ForEachCaret("  a,| b|, c  ", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Equal("a, c", result.Trim());
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingCommas()
        {
            ForEachCaret(",a,| b|, c,", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Contains("a", result);
                Assert.Contains("c", result);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagBeforeNewline()
        {
            ForEachCaret("a, |b,\nc", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Contains("a", result);
                Assert.Contains("c", result);
            });
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagAfterNewline()
        {
            ForEachCaret("a\n,| b|, c", (text, caret) =>
            {
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.Contains("a", result);
                Assert.Contains("c", result);
            });
        }
    }
} 