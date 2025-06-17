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

        /// <summary>
        /// 戻り値とキャレットを1つの '|' で指定して DeleteCommaDelimitedSectionCore の結果を検証します
        /// </summary>
        private static void ForEachCaretAndAssert(string inputRangeWithCarets, string expectedWithCaret)
        {
            var (text, start, end) = ParseWithCaretRange(inputRangeWithCarets);
            var (expectedText, expectedCaret) = ParseExpectedCaret(expectedWithCaret);
            for (int caret = start; caret <= end; caret++)
            {
                var displayInput = text.Insert(caret, "|");
                var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);
                Assert.True(result == expectedText, $"Caret case: {displayInput}: Expected '{expectedText}' but got '{result}'");
                if (expectedCaret >= 0)
                {
                    Assert.True(newCaret == expectedCaret, $"Caret case: {displayInput}: Expected caret {expectedCaret} but got {newCaret}");
                }
            }
        }

        private static (string text, int caret) ParseExpectedCaret(string inputWithCaret)
        {
            int caret = inputWithCaret.IndexOf('|');
            if (caret < 0) return (inputWithCaret, -1);
            var text = inputWithCaret.Remove(caret, 1);
            return (text, caret);
        }


        [Fact]
        public void DeleteCommaDelimitedSectionCore_NormalCase_RemovesMiddleTag()
        {
            ForEachCaretAndAssert("a,| b|, c", "a, |c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveFirstTag()
        {
            ForEachCaretAndAssert("|a|, b, c", "|b, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLastTag()
        {
            ForEachCaretAndAssert("a, b,| c|", "a, b|");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithSpaces()
        {
            ForEachCaretAndAssert("a ,|  b  |, c ", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithNewlines()
        {
            ForEachCaretAndAssert("a,\r\n|b,|\r\nc", "a,\r\n\r\n|c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveOnlyTag()
        {
            ForEachCaretAndAssert("|a|", "|");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithMultipleCommas()
        {
            ForEachCaretAndAssert("a,,|b|,,c", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLoraTag()
        {
            ForEachCaretAndAssert("a,| <lora:foo:1>|, c", "a, c");
            ForEachCaretAndAssert("a,| <lora:foo:0.8>|, c", "a, c");
            ForEachCaretAndAssert("a,| <lora:foo:0.85>|, c", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveBracketedTag()
        {
            ForEachCaretAndAssert("a,| (b:1.2)|, c", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveBracketedMultiTag()
        {
            ForEachCaretAndAssert("a,| (b1, b2:1.2)|, c", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_CaretOnComma()
        {
            ForEachCaretAndAssert("a,| b|, c", "a,| c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_EmptyString()
        {
            ForEachCaretAndAssert("|", "|");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingSpaces()
        {
            ForEachCaretAndAssert("  a,| b|, c  ", "a, c");
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingCommas()
        {
            ForEachCaret("a,| b|, c,", (text, caret) =>
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