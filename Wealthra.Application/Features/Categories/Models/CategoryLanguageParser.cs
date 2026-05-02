namespace Wealthra.Application.Features.Categories.Models;

public static class CategoryLanguageParser
{
    public static bool TryParse(string? value, out CategoryDisplayLanguage language)
    {
        language = CategoryDisplayLanguage.English;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "en":
                return true;
            case "tr":
                language = CategoryDisplayLanguage.Turkish;
                return true;
            default:
                return false;
        }
    }
}
