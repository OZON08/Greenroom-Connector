using System.Collections.Generic;
using GreenroomConnector.Models;
using Xunit;

namespace GreenroomConnector.Tests
{
    public class RoomConfigurationTests
    {
        private static RoomConfiguration Config(string key, string value) =>
            new RoomConfiguration(new Dictionary<string, string> { { key, value } });

        private static RoomConfiguration Empty() =>
            new RoomConfiguration(new Dictionary<string, string>());

        [Theory]
        [InlineData("optional", true)]
        [InlineData("default_enabled", true)]
        [InlineData("true", false)]
        [InlineData("false", false)]
        [InlineData("", false)]
        public void CanChange_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("s", configValue).CanChange("s"));

        [Fact]
        public void CanChange_returns_false_for_missing_key() =>
            Assert.False(Empty().CanChange("missing"));

        [Theory]
        [InlineData("default_enabled", true)]
        [InlineData("optional", false)]
        [InlineData("true", false)]
        [InlineData("false", false)]
        public void IsDefaultEnabled_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("s", configValue).IsDefaultEnabled("s"));

        [Theory]
        [InlineData("optional", true)]
        [InlineData("default_enabled", true)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("", false)]
        public void CanToggleAccessCode_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("glViewerAccessCode", configValue).CanToggleAccessCode("glViewerAccessCode"));
    }
}
