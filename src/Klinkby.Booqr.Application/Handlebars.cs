using System.Text;
using System.Text.RegularExpressions;

namespace Klinkby.Booqr.Application;

internal static partial class Handlebars
{
    public static string Replace(ReadOnlySpan<char> template, Dictionary<string, string> replacements)
    {
        if (template.IsEmpty)
        {
            return string.Empty;
        }

        var lastIndex = 0;
        var anyMatch = false;
        var sb = new StringBuilder(template.Length);

        // Get alternate lookup to enable span-based lookups
        Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> lookup =
            replacements.GetAlternateLookup<ReadOnlySpan<char>>();

        foreach (ValueMatch m in HandleBarsMatcher().EnumerateMatches(template))
        {
            anyMatch = true;
            var start = m.Index;
            var length = m.Length;

            // Append literal segment
            if (start > lastIndex)
            {
                sb.Append(template.Slice(lastIndex, start - lastIndex));
            }

            // Extract key (strip '{{' and '}}')
            ReadOnlySpan<char> keySpan = template.Slice(start + 2, length - 4);

            // Zero-allocation span-based lookup
            if (lookup.TryGetValue(keySpan, out var value))
            {
                sb.Append(value);
            }
            else
            {
                throw new ArgumentException($"The key {keySpan} was missing", nameof(replacements));
            }

            lastIndex = start + length;
        }

        if (!anyMatch)
        {
            return template.ToString();
        }

        if (lastIndex < template.Length)
        {
            sb.Append(template[lastIndex..]);
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"{{(\w+)}}",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex HandleBarsMatcher();
}
