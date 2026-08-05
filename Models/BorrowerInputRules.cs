namespace LibraryManager.Models;

public static class BorrowerInputRules
{
    public static string FilterFullName(string? value) =>
        new((value ?? string.Empty)
            .Where(character => char.IsLetter(character) || character == ' ')
            .ToArray());

    public static string FilterId(string? value) =>
        new((value ?? string.Empty)
            .Where(character => character is >= '0' and <= '9')
            .ToArray());

    public static string NormalizeFullName(string? value) =>
        string.Join(
            " ",
            (value ?? string.Empty).Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public static bool IsValidFullName(string? value)
    {
        var normalizedValue = NormalizeFullName(value);
        var nameParts = normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return nameParts.Length >= 2 &&
               nameParts.All(part => part.All(char.IsLetter));
    }

    public static bool IsValidId(string? value)
    {
        var trimmedValue = value?.Trim() ?? string.Empty;
        return trimmedValue.Length > 0 &&
               trimmedValue.All(character => character is >= '0' and <= '9');
    }
}
