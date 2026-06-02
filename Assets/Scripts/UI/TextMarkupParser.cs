public static class TextMarkupParser
{
    public static string Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("\\n", "\n")
            .Replace("[red]", "<color=#FF5555>")
            .Replace("[/red]", "</color>")
            .Replace("[yellow]", "<color=#FFD84A>")
            .Replace("[/yellow]", "</color>")
            .Replace("[blue]", "<color=#5AA7FF>")
            .Replace("[/blue]", "</color>")
            .Replace("[b]", "<b>")
            .Replace("[/b]", "</b>");
    }
}
