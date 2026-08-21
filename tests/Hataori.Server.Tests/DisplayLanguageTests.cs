using System.Globalization;
using FluentAssertions;
using Hataori.Application.Localization;

namespace Hataori.Server.Tests;

public sealed class DisplayLanguageTests
{
    [Theory]
    [InlineData("ja-JP")]
    [InlineData("en-US")]
    public async Task ReadFromConfiguration_SupportedLanguage_ReturnsConfiguredValue(string language)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hataori-language-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, $$"""
                {
                  "application": {
                    "language": "{{language}}"
                  }
                }
                """);

            DisplayLanguage.ReadFromConfiguration(path).Should().Be(language);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Text_EnglishCulture_ReturnsEnglishText()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(DisplayLanguage.English);
            DisplayLanguage.Text("日本語", "English").Should().Be("English");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Text_JapaneseCulture_ReturnsJapaneseText()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(DisplayLanguage.Japanese);
            DisplayLanguage.Text("日本語", "English").Should().Be("日本語");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
