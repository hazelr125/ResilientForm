# ResilientForm: a form tester that survives page changes (C# + Selenium)

Automates three fields on the CloudQA practice form: **First Name** (text box), **Gender** (radio button) and **State** (dropdown).
It keeps working when the page changes: fields reordered, `id` / `name` / `class` changed, HTML structure altered, look-alike fields added.
Fields are found by **what they mean** (label text, `aria-label`, placeholder, nearby text, fieldset legend), never by a fixed XPath, CSS selector or id.
The page URL is configurable.

## Setup
Requires **.NET SDK 8.0 or newer** (`dotnet --version`) and **Google Chrome** (or Chromium). Works on Windows, macOS and Linux.
The ChromeDriver is downloaded automatically by Selenium Manager, so the first run needs internet access.
```bash
git clone <your-repo-url>
cd <repo-folder>
dotnet build src
```
Run every command below from the repo folder (the one containing `src/` and `samples/`).
If Chrome is installed in a non-standard place, set `CHROME_BIN` to its executable path.

## Run
The `--` after the project path is required: it separates `dotnet`'s options from the program's options.
```bash
# default URL (the CloudQA form), a Chrome window opens so you can watch
dotnet run --project src

# no window, save a screenshot of the result
dotnet run --project src -- --headless --screenshot out.png

# any other URL (http://, https:// or file://), via the command line...
dotnet run --project src -- --url "https://example.com/modified-form.html"

# ...or via an environment variable (--url takes priority)
FORM_URL="https://example.com/modified-form.html" dotnet run --project src           # macOS/Linux
$env:FORM_URL = "https://example.com/modified-form.html"; dotnet run --project src   # PowerShell

# a saved local copy of a page (Windows: file:///C:/Users/you/page.html)
dotnet run --project src -- --url "file:///absolute/path/to/page.html"

# everything set
dotnet run --project src -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" \
    --first-name "Hazel" --gender Female --state Maharashtra --headless --screenshot out.png
```
Options: `--url`, `--first-name` (default `Hazel`), `--gender` (default `Female`), `--state` (default: first real option),
`--timeout <seconds>` (default `15`), `--headless`, `--screenshot <file>`, `-h`.
The state you pass must exist in the page's dropdown. On PowerShell put the long command on one line.

The program prints one PASS or FAIL line per field (with the label it matched and a score), then a summary.
Exit code: `0` all passed, `1` at least one check failed, `2` bad argument, `3` Chrome could not start.

**Try the mutated pages:** `samples/` has the original-style form plus five altered copies. Run them all with
```bash
./run-samples.sh        # macOS/Linux/Git Bash
.\run-samples.ps1       # Windows PowerShell (if blocked: powershell -ExecutionPolicy Bypass -File .\run-samples.ps1)
```
Extra options such as `--gender Male` are passed to every run. Don't pass `--screenshot` here, because each page would overwrite the same file.

## Approach
1. **Scan** (`src/PageScan.js`, runs inside the page). For each control of the right *type*, collect all human-readable text attached to it, tagged by source:
   `<label for>`, wrapping `<label>`, `aria-label` / `aria-labelledby`, `placeholder`, `title`, text just before or after it, and (weakly) `name` / `id`.
   For radios it also collects the labels of the whole **group** (fieldset legend, aria, heading text before the group).
2. **Score** (`src/Scoring.cs`). Text is normalised (`firstName`, `first_name`, `First Name` all become `first name`).
   Matching a field's phrases adds that source's weight (label 100 down to id/name 30). Matching a look-alike phrase
   (*Last*, *Full*, *Username*, *Country*, *City*...) subtracts 1.5 times the weight. The highest score wins.
   If nothing clears a minimum, the run fails with the top candidates listed instead of touching the wrong field.
3. **Act and verify** (`src/Program.cs`). Every action is followed by a read-back assertion:
   - **First Name:** type, read the value back, and check it appears in exactly one field. Falls back to setting the value with JavaScript if key events are ignored.
   - **Gender:** find the group labelled "Gender", pick the option by whole-word match (so `male` never matches `female`), click it
     (falling back to its label, then JavaScript, for hidden custom radios), check it is selected, then switch to another option and back to prove the group is exclusive.
   - **State:** select by visible text (exact, then contains) and check the selected text. If `--state` is given, a dropdown that offers that option scores higher.
4. Iframes are searched one level deep, and lookup retries for up to `--timeout` seconds so late-rendered fields are found.

## Limitations
- **English-only phrase lists** (`first name`, `gender`, `state`, `province`...). Other languages need more phrases.
- **Unlabelled fields:** a field with no label, placeholder, aria text or meaningful nearby text can only be matched through its `name`/`id`, which is deliberately weak.
- **Native elements only:** custom widgets (div-based dropdowns, canvas, shadow DOM) are not handled. Iframes are searched one level deep only.
- **Ambiguity:** if two fields look equally right (e.g. two "State" dropdowns) it picks the first in document order, and warns only for text boxes.
- **Page side effect:** the scan adds a harmless `data-rq-id` attribute to candidate elements so it can re-find them.
- **Hand-tuned:** phrase lists and weights were tuned on the sample pages, not tested against a large variety of real sites.
- **Chrome only.**

## Files
```
src/Program.cs             entry point: runs the three checks, prints PASS/FAIL, exit codes
src/Config.cs              command-line options, FORM_URL, defaults
src/Finder.cs              finds the text box / radio group / dropdown (retries, iframes)
src/Scoring.cs             phrase lists, weights, text normalisation, scoring
src/PageScan.js            in-page script that collects labels and nearby text per control
src/ResilientForm.csproj   .NET 8 project (Selenium.WebDriver, Selenium.Support)
samples/                   original-style form + 5 mutated copies
run-samples.sh / .ps1      run the program against every sample page
NOTE.md                    short note on approach and limitations
```
