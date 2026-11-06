using System.Text.RegularExpressions;

namespace ResilientForm;

public class RawLabel { public string src { get; set; } = ""; public string text { get; set; } = ""; }

public class RawEl
{
    public int id { get; set; }
    public bool visible { get; set; }
    public bool @checked { get; set; }
    public List<RawLabel> labels { get; set; } = new();
    public List<RawLabel> group { get; set; } = new();
    public List<string> options { get; set; } = new();
}

/// <summary>Describes what a field "means": phrases that match it, and phrases that rule it out.</summary>
public record FieldSpec(string[] Phrases, string[] Negatives);

public static class Scoring
{
    // How much we trust each source of label text.
    static readonly Dictionary<string, double> Weight = new()
    {
        ["label-for"] = 100, ["label-wrap"] = 100, ["legend"] = 100,
        ["aria-label"] = 90, ["aria-labelledby"] = 90,
        ["after"] = 80, ["placeholder"] = 70, ["nearby"] = 60, ["leading"] = 60,
        ["title"] = 40, ["name-id"] = 30, ["value"] = 30,
    };

    public const double MinScore = 25;

    public static readonly FieldSpec FirstName = new(
        new[] { "first name", "given name", "forename", "fname" },
        new[] { "last name", "surname", "family name", "full name", "middle name", "user name", "username",
                "company", "nick name", "nickname", "father", "mother", "spouse", "email", "e mail" });

    public static readonly FieldSpec Gender = new(new[] { "gender", "sex" }, Array.Empty<string>());

    public static readonly FieldSpec State = new(
        new[] { "state", "province", "state province" },
        new[] { "country", "city", "nation", "status", "county" });

    public static string Norm(string s)
    {
        s = Regex.Replace(s ?? "", "([a-z])([A-Z])", "$1 $2");          // firstName -> first Name
        s = Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim(); // first_name -> first name
        return s;
    }

    static bool Has(string normalized, string phrase)
    {
        if ((" " + normalized + " ").Contains(" " + phrase + " ")) return true;
        // "firstname" written as one word
        return phrase.Contains(' ') && normalized.Replace(" ", "").Contains(phrase.Replace(" ", ""));
    }

    /// <summary>Positive weight for each label that matches, strong penalty for each that matches a negative.</summary>
    public static double Score(IEnumerable<RawLabel> labels, FieldSpec spec)
    {
        double total = 0;
        foreach (var l in labels)
        {
            var n = Norm(l.text);
            if (n.Length == 0) continue;
            double w = Weight.GetValueOrDefault(l.src, 20);
            if (spec.Negatives.Any(x => Has(n, x))) { total -= w * 1.5; continue; }
            if (spec.Phrases.Any(p => Has(n, p)))
            {
                total += w;
                if (spec.Phrases.Contains(n)) total += 20;   // label is exactly the phrase
            }
        }
        return total;
    }

    /// <summary>Does this radio option mean "Male"/"Female"/...? Word-level match so "male" never matches "female".</summary>
    public static double OptionScore(IEnumerable<RawLabel> labels, string desired)
    {
        var d = Norm(desired);
        string[] syn, opp;
        switch (d)
        {
            case "male": syn = new[] { "male", "man", "m", "boy" }; opp = new[] { "female", "woman", "f", "girl" }; break;
            case "female": syn = new[] { "female", "woman", "f", "girl" }; opp = new[] { "male", "man", "m", "boy" }; break;
            default: syn = new[] { d }; opp = Array.Empty<string>(); break;
        }
        double total = 0;
        foreach (var l in labels)
        {
            var words = Norm(l.text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;
            bool hit = syn.Length == 1 && syn[0].Contains(' ')
                ? Has(string.Join(' ', words), syn[0])
                : syn.Any(s => words.Contains(s));
            if (hit && !opp.Any(o => words.Contains(o))) total += Weight.GetValueOrDefault(l.src, 20);
        }
        return total;
    }
}
