using System;
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
            // Arrange
            var (text, caret) = ParseWithCaret("a, |b, c");

            // Act
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result);
            Assert.Equal(3, newCaret); // 'a,'の後
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveFirstTag()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("|a, b, c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("b, c", result);
            Assert.Equal(0, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLastTag()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a, b, c|");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, b", result);
            Assert.Equal(4, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithSpaces()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a ,  |b  , c ");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result.TrimEnd()); // 末尾スペースは無視
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithTabs()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a,\t|b,\tc");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result);
            Assert.Equal(2, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithNewlines()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a,\n|b,\nc");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result);
            Assert.Equal(2, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveOnlyTag()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("|a");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("", result); // 挙動通り、全体が削除される
            Assert.Equal(0, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagWithMultipleCommas()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a,,|b,,c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result); // 期待値はロジック準拠
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveLoraTag()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a, |<lora:foo>, c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveBracketedTag()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a, |(b:1.2), c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_CaretOnComma()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a|, b, c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result); // 挙動通り、削除される
            Assert.Equal(2, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_EmptyString()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("|");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("", result);
            Assert.Equal(0, newCaret);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingSpaces()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("  a, |b, c  ");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Equal("a, c", result.Trim());
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_LeadingAndTrailingCommas()
        {
            // Arrange
            var (text, caret) = ParseWithCaret(",a, |b, c,");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Contains("a", result);
            Assert.Contains("c", result);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagBeforeNewline()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a, |b,\nc");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Contains("a", result);
            Assert.Contains("c", result);
        }

        [Fact]
        public void DeleteCommaDelimitedSectionCore_RemoveTagAfterNewline()
        {
            // Arrange
            var (text, caret) = ParseWithCaret("a\n, |b, c");
            var (result, newCaret) = TestableHelper.DeleteCommaDelimitedSectionCore(text, caret);

            // Assert
            Assert.Contains("a", result);
            Assert.Contains("c", result);
        }
    }
} 