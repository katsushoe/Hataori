using System.Globalization;
using System.Text.Json;

namespace Hataori.Application.Localization;

/// <summary>設定ファイルに保存された表示言語を解決します。</summary>
public static class DisplayLanguage
{
    public const string Japanese = "ja-JP";
    public const string English = "en-US";

    public static bool IsJapanese => CultureInfo.CurrentUICulture.Name.Equals(Japanese, StringComparison.OrdinalIgnoreCase);

    public static string ApplyFromConfiguration(string configurationPath)
    {
        var language = ReadFromConfiguration(configurationPath);
        var culture = CultureInfo.GetCultureInfo(language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        return language;
    }

    public static string ReadFromConfiguration(string configurationPath)
    {
        if (!File.Exists(configurationPath))
        {
            return Japanese;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
            if (document.RootElement.TryGetProperty("application", out var application) &&
                application.TryGetProperty("language", out var languageElement) &&
                languageElement.ValueKind == JsonValueKind.String)
            {
                var language = languageElement.GetString();
                if (language is Japanese or English)
                {
                    return language;
                }
            }
        }
        catch (JsonException)
        {
            // 設定検証側で詳細を報告するため、表示言語は既定値へ戻します。
        }
        catch (IOException)
        {
            // 一時的に読み込めない場合も、アプリケーションの起動を妨げません。
        }

        return Japanese;
    }

    public static string Text(string japanese, string english) => IsJapanese ? japanese : english;
}
