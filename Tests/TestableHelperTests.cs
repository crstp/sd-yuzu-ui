using System;
using Xunit;
using SD.Yuzu.Helpers;

namespace SD.Yuzu.Tests
{
    public class TestableHelperTests
    {
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
    }
} 