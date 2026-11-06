using System.Reflection;
using System.Text.Json;
using OpenQA.Selenium;

namespace ResilientForm;

public record Hit(IWebElement Element, double Score, string Why);
public record RadioOption(IWebElement Element, string Text, double OptionScore);
public record RadioHit(double GroupScore, string Why, List<RadioOption> Options);

/// <summary>Locates fields by meaning (labels, placeholders, nearby text), never by fixed id/xpath/css.</summary>
public class Finder
{
    readonly IWebDriver d;
    readonly TimeSpan timeout;
    readonly string scanJs;
    string lastDiag = "";

    public Finder(IWebDriver driver, TimeSpan timeout)
    {
        d = driver; this.timeout = timeout;
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("PageScan.js")!;
        scanJs = new StreamReader(s).ReadToEnd();
    }

    record Scan(int Frame, List<RawEl> Els);

    void Enter(int frame)
    {
        d.SwitchTo().DefaultContent();
        if (frame >= 0) d.SwitchTo().Frame(frame);
    }

    // Scan the main document and every top-level iframe.
    List<Scan> ScanAll(string kind)
    {
        var scans = new List<Scan>();
        d.SwitchTo().DefaultContent();
        int frames = d.FindElements(By.TagName("iframe")).Count;
        for (int f = -1; f < frames; f++)
        {
            try
            {
                Enter(f);
                var json = ((IJavaScriptExecutor)d).ExecuteScript(scanJs, kind) as string;
                if (string.IsNullOrWhiteSpace(json)) continue;
                var els = JsonSerializer.Deserialize<List<RawEl>>(json) ?? new();
                scans.Add(new Scan(f, els));
            }
            catch (WebDriverException) { /* unreadable frame: skip */ }
        }
        return scans;
    }

    IWebElement Grab(int frame, int id)
    {
        Enter(frame);
        return d.FindElement(By.CssSelector($"[data-rq-id='{id}']"));
    }

    T Retry<T>(string what, Func<T?> attempt) where T : class
    {
        var end = DateTime.UtcNow + timeout;
        while (true)
        {
            var r = attempt();
            if (r != null) return r;
            if (DateTime.UtcNow > end)
                throw new NoSuchElementException($"Could not confidently locate {what}. {lastDiag}");
            Thread.Sleep(400);   // page may still be rendering
        }
    }

    static string Describe(IEnumerable<RawLabel> labels) =>
        string.Join("; ", labels.Where(l => l.src != "name-id").Take(3).Select(l => $"{l.src}='{l.text}'"));

    // ---- text box ----
    public Hit FindTextBox(FieldSpec spec, string what) => Retry(what, () =>
    {
        var cands = new List<(int Frame, RawEl El, double Score)>();
        foreach (var s in ScanAll("text"))
            foreach (var e in s.Els) cands.Add((s.Frame, e, Scoring.Score(e.labels, spec)));
        var ranked = cands.OrderByDescending(c => c.Score).ToList();
        lastDiag = "Top candidates: " + string.Join(" | ", ranked.Take(3).Select(c => $"[{c.Score:0}] {Describe(c.El.labels)}"));
        if (ranked.Count == 0 || ranked[0].Score < Scoring.MinScore) return null;
        var best = ranked[0];
        var note = ranked.Count > 1 && ranked[1].Score >= best.Score * 0.9
            ? " (WARNING: near-tie with another field)" : "";
        return new Hit(Grab(best.Frame, best.El.id), best.Score, Describe(best.El.labels) + note);
    });

    // ---- dropdown ----
    public Hit FindSelect(FieldSpec spec, string what, string? wantedValue) => Retry(what, () =>
    {
        var cands = new List<(int Frame, RawEl El, double Score)>();
        foreach (var s in ScanAll("select"))
            foreach (var e in s.Els)
            {
                double sc = Scoring.Score(e.labels, spec);
                // If we know which option we want, a dropdown that actually offers it is a better candidate.
                if (sc > 0 && wantedValue != null &&
                    e.options.Any(o => Scoring.Norm(o) == Scoring.Norm(wantedValue))) sc += 40;
                cands.Add((s.Frame, e, sc));
            }
        var ranked = cands.OrderByDescending(c => c.Score).ToList();
        lastDiag = "Top candidates: " + string.Join(" | ", ranked.Take(3).Select(c => $"[{c.Score:0}] {Describe(c.El.labels)}"));
        if (ranked.Count == 0 || ranked[0].Score < Scoring.MinScore) return null;
        var best = ranked[0];
        return new Hit(Grab(best.Frame, best.El.id), best.Score, Describe(best.El.labels));
    });

    // ---- radio group ----
    public RadioHit FindRadioGroup(FieldSpec groupSpec, string what) => Retry(what, () =>
    {
        var all = new List<(int Frame, RawEl El, double GroupScore, string Sig)>();
        foreach (var s in ScanAll("radio"))
            foreach (var e in s.Els)
                all.Add((s.Frame, e, Scoring.Score(e.group, groupSpec),
                         s.Frame + "|" + string.Join("|", e.group.Select(g => g.src + g.text))));
        lastDiag = "Top groups: " + string.Join(" | ", all.OrderByDescending(a => a.GroupScore).Take(3)
                       .Select(a => $"[{a.GroupScore:0}] {Describe(a.El.group)}"));
        if (all.Count == 0) return null;
        var best = all.OrderByDescending(a => a.GroupScore).First();
        if (best.GroupScore < Scoring.MinScore) return null;
        // every radio that shares the winner's group signature belongs to the group
        var members = all.Where(a => a.Sig == best.Sig).ToList();
        var opts = members.Select(m =>
        {
            var el = Grab(m.Frame, m.El.id);
            var text = m.El.labels.FirstOrDefault(l => l.src is "label-for" or "label-wrap" or "after")?.text
                       ?? m.El.labels.FirstOrDefault(l => l.src == "value")?.text ?? "?";
            return (m, el, text);
        }).ToList();
        // return elements + their raw labels for option matching
        optionLabels = opts.ToDictionary(o => o.el, o => o.m.El.labels);
        return new RadioHit(best.GroupScore, Describe(best.El.group),
            opts.Select(o => new RadioOption(o.el, o.text, 0)).ToList());
    });

    Dictionary<IWebElement, List<RawLabel>> optionLabels = new();

    public RadioOption PickOption(RadioHit group, string desired)
    {
        var scored = group.Options
            .Select(o => o with { OptionScore = Scoring.OptionScore(optionLabels.GetValueOrDefault(o.Element) ?? new(), desired) })
            .OrderByDescending(o => o.OptionScore).ToList();
        if (scored.Count == 0 || scored[0].OptionScore <= 0)
            throw new NoSuchElementException(
                $"Found the group ({group.Why}) but no option matching '{desired}'. Options seen: {string.Join(", ", group.Options.Select(o => o.Text))}");
        return scored[0];
    }
}
