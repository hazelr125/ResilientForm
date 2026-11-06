using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using ResilientForm;

Config cfg;
try { cfg = Config.Parse(args); }
catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); Config.PrintHelp(); return 2; }
if (cfg.Help) { Config.PrintHelp(); return 0; }

var opts = new ChromeOptions();
if (cfg.Headless) opts.AddArgument("--headless=new");
opts.AddArgument("--window-size=1400,1000");
opts.AddArgument("--allow-file-access-from-files");   // lets file:// sample pages work

using var driver = new ChromeDriver(opts);
var js = (IJavaScriptExecutor)driver;
var finder = new Finder(driver, TimeSpan.FromSeconds(cfg.TimeoutSeconds));
var results = new List<(string Name, bool Ok, string Detail)>();

void Check(string name, Func<string> body)
{
    try { results.Add((name, true, body())); }
    catch (Exception ex) { results.Add((name, false, ex.Message.Split('\n')[0])); }
}

Console.WriteLine($"Testing: {cfg.Url}\n");
driver.Navigate().GoToUrl(cfg.Url);
new WebDriverWait(driver, TimeSpan.FromSeconds(cfg.TimeoutSeconds))
    .Until(d => js.ExecuteScript("return document.readyState")!.Equals("complete"));

// ------------------------------------------------------------------ First Name
Check("First Name (text box)", () =>
{
    var hit = finder.FindTextBox(Scoring.FirstName, "the First Name text box");
    var el = hit.Element;
    js.ExecuteScript("arguments[0].scrollIntoView({block:'center'})", el);
    try { el.Clear(); el.SendKeys(cfg.FirstName); } catch (WebDriverException) { }
    if (el.GetAttribute("value") != cfg.FirstName)   // fallback for fields that swallow key events
        js.ExecuteScript("""
            const e=arguments[0]; const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;
            set.call(e, arguments[1]);
            e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true}));
            """, el, cfg.FirstName);
    var actual = el.GetAttribute("value");
    if (actual != cfg.FirstName) throw new Exception($"Expected '{cfg.FirstName}' but field holds '{actual}'");
    // guard against typing into a look-alike: our value must appear in exactly one text field
    var copies = Convert.ToInt32(js.ExecuteScript(
        "return Array.from(document.querySelectorAll('input,textarea')).filter(i=>i.value===arguments[0]).length", cfg.FirstName));
    if (copies != 1) throw new Exception($"Value ended up in {copies} fields (expected 1)");
    return $"typed '{actual}' | matched via {hit.Why} | score {hit.Score:0}";
});

// ------------------------------------------------------------------ Gender
void ClickRadio(IWebElement radio)
{
    js.ExecuteScript("arguments[0].scrollIntoView({block:'center'})", radio);
    try { if (radio.Displayed && radio.Enabled) { radio.Click(); if (radio.Selected) return; } }
    catch (WebDriverException) { }
    try   // custom-styled radios: the real input is hidden, so click its label
    {
        js.ExecuteScript("""
            const e=arguments[0];
            const l=(e.id && document.querySelector('label[for="'+CSS.escape(e.id)+'"]')) || e.closest('label');
            if(l) l.click();
            """, radio);
    }
    catch (WebDriverException) { }
    if (radio.Selected) return;
    js.ExecuteScript("""
        const e=arguments[0]; e.checked=true;
        e.dispatchEvent(new Event('input',{bubbles:true})); e.dispatchEvent(new Event('change',{bubbles:true}));
        """, radio);
}

Check("Gender (radio button)", () =>
{
    var group = finder.FindRadioGroup(Scoring.Gender, "the Gender radio group");
    var target = finder.PickOption(group, cfg.Gender);
    ClickRadio(target.Element);
    if (!target.Element.Selected) throw new Exception($"Clicked '{target.Text}' but it is not selected");

    // switch to a different option and back: proves we hit a real, mutually exclusive group
    var other = group.Options.FirstOrDefault(o => o.Element != target.Element);
    if (other != null)
    {
        ClickRadio(other.Element);
        if (!other.Element.Selected || target.Element.Selected)
            throw new Exception("Group did not behave like a radio group when switching options");
        ClickRadio(target.Element);
        if (!target.Element.Selected) throw new Exception("Could not re-select the requested option");
    }
    return $"selected '{target.Text}' of [{string.Join(", ", group.Options.Select(o => o.Text))}] | group matched via {group.Why}";
});

// ------------------------------------------------------------------ State
Check("State (dropdown)", () =>
{
    var hit = finder.FindSelect(Scoring.State, "the State dropdown", cfg.State);
    js.ExecuteScript("arguments[0].scrollIntoView({block:'center'})", hit.Element);
    var sel = new SelectElement(hit.Element);

    bool Placeholder(IWebElement o) =>
        string.IsNullOrWhiteSpace(o.GetAttribute("value")) ||
        Scoring.Norm(o.Text).StartsWith("select") || Scoring.Norm(o.Text).StartsWith("choose") || o.Text.Trim().StartsWith("--");

    IWebElement? opt;
    if (cfg.State != null)
    {
        var want = Scoring.Norm(cfg.State);
        opt = sel.Options.FirstOrDefault(o => Scoring.Norm(o.Text) == want)
              ?? sel.Options.FirstOrDefault(o => Scoring.Norm(o.Text).Contains(want));
        if (opt == null) throw new Exception($"No option '{cfg.State}'. Available: {string.Join(", ", sel.Options.Take(8).Select(o => o.Text))}...");
    }
    else
    {
        opt = sel.Options.FirstOrDefault(o => !Placeholder(o)) ?? throw new Exception("Dropdown has no selectable options");
    }
    var wanted = opt.Text;
    sel.SelectByText(wanted);
    var actual = sel.SelectedOption.Text;
    if (actual != wanted) throw new Exception($"Expected '{wanted}' but dropdown shows '{actual}'");
    return $"selected '{actual}' of {sel.Options.Count} options | matched via {hit.Why} | score {hit.Score:0}";
});

// ------------------------------------------------------------------ report
if (cfg.ScreenshotPath != null)
{
    driver.SwitchTo().DefaultContent();
    ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(cfg.ScreenshotPath);
}
Console.WriteLine("RESULTS");
foreach (var (name, ok, detail) in results)
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}\n         {detail}");
bool all = results.All(r => r.Ok);
Console.WriteLine($"\n{(all ? "ALL PASSED" : "FAILURES PRESENT")}");
return all ? 0 : 1;
