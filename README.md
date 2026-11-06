# Resilient Form Tester (C# + Selenium)

Automates three fields on the CloudQA practice form: **First Name** (text box), **Gender** (radio button) and **State** (dropdown). It keeps working when the page changes: fields reordered, `id` / `name` / `class` changed, HTML structure altered, look-alike fields added.

Fields are found by **what they mean** (label text, `aria-label`, placeholder, nearby text, fieldset legend), never by a fixed XPath, CSS selector or id. The page URL is configurable.

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| **.NET SDK 8.0 or newer** | Check with `dotnet --version`. Download: https://dotnet.microsoft.com/download |
| **Google Chrome** (or Chromium) | The ChromeDriver is downloaded automatically by Selenium Manager, so the machine needs internet access on the first run. |
| **Git** | Only to clone the repo. |

Works on Windows, macOS and Linux.

## 2. Get the code and build

```bash
git clone <your-repo-url>
cd <repo-folder>
dotnet build src
```

`<repo-folder>` is the folder git creates (the repo name). The commands below are run from this folder, which contains `src/` and `samples/`.

## 3. Run it

The `--` after the project path is required. It separates `dotnet`'s own options from the program's options.

### Test the CloudQA form (default URL)

On a normal desktop a Chrome window opens, so you can watch the form being filled:

```bash
dotnet run --project src
```

Add `--headless` to run without a window and `--screenshot` to save a picture of the result:

```bash
dotnet run --project src -- --headless --screenshot out.png
```

### Test a different URL

The URL can be set two ways. `--url` takes priority over the environment variable.

```bash
# 1) command-line option
dotnet run --project src -- --url "https://example.com/modified-form.html"

# 2) environment variable
```

macOS / Linux (bash, zsh):

```bash
FORM_URL="https://example.com/modified-form.html" dotnet run --project src
```

Windows PowerShell:

```powershell
$env:FORM_URL = "https://example.com/modified-form.html"
dotnet run --project src
```

Windows Command Prompt:

```bat
set FORM_URL=https://example.com/modified-form.html
dotnet run --project src
```

`file://` URLs also work, for example to test a saved copy of the page:

```bash
dotnet run --project src -- --url "file:///absolute/path/to/page.html"
```

On Windows a local file looks like `file:///C:/Users/you/page.html`.

### Options

| Option | Meaning | Default |
|---|---|---|
| `--url <url>` (or env `FORM_URL`) | Page to test (`http://`, `https://` or `file://`) | the CloudQA form |
| `--first-name <text>` | Text typed into First Name | `Hazel` |
| `--gender <text>` | Gender option to pick (`Male`, `Female`, ...) | `Female` |
| `--state <text>` | State option to pick | first real option |
| `--timeout <seconds>` | Max time to wait for a field to appear | `15` |
| `--headless` | Run Chrome without a window | off |
| `--screenshot <file>` | Save a screenshot at the end | off |
| `-h`, `--help` | Show usage | |

Example with everything set:

```bash
dotnet run --project src -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" \
    --first-name "Hazel" --gender Female --state Maharashtra --headless --screenshot out.png
```

(On Windows PowerShell put it on one line, or end each line with a backtick `` ` `` instead of `\`. The state must exist in the page's dropdown.)

### Reading the result

The program prints one PASS or FAIL line per field, with the label it matched and a score, then a summary:

```
Testing: <url>

RESULTS
  [PASS] First Name (text box)
         typed '...' | matched via label-for='First Name' | score ...
  [PASS] Gender (radio button)
         selected '...' of [...] | group matched via ...
  [PASS] State (dropdown)
         selected '...' of ... options | matched via ...

ALL PASSED
```

(The exact details depend on the page.) A FAIL line explains why. If a field could not be located with confidence, it lists the top candidates and their scores instead of touching the wrong field.

Exit codes: `0` all checks passed, `1` at least one check failed, `2` invalid command-line argument, `3` Chrome could not be started.

## 4. Prove it survives page changes

`samples/` contains the original-style form plus five mutated copies:

| File | What changed |
|---|---|
| `02-reordered-renamed` | Field order shuffled; all ids, names and classes renamed |
| `03-no-attributes-wrapped` | No ids/names/classes; table layout; labels wrap inputs; decoys *Full Name*, *Last Name*, *Country* |
| `04-placeholders-aria-only` | No visible labels, only placeholders and `aria-label`s; deeply nested divs |
| `05-nearby-text-decoys` | Labels are plain `<span>`s; decoys *Username*, *Last Name*, *Marital status* radios, *City* dropdown, *Statement of purpose* |
| `06-custom-hidden-radios` | Real radio inputs are invisible; styled `<label>` pills |

Run the program against all of them:

macOS / Linux / Git Bash:

```bash
./run-samples.sh
```

Windows PowerShell:

```powershell
.\run-samples.ps1
```

If PowerShell blocks the script, run it once with `powershell -ExecutionPolicy Bypass -File .\run-samples.ps1`.

The scripts already add `--headless`. Extra options such as `--gender Male` are passed through to every run. Don't pass `--screenshot` here, because each page would overwrite the same file.

## 5. Troubleshooting

| Problem | Fix |
|---|---|
| `Could not start Chrome` / `session not created` | Make sure Chrome is installed and up to date. If it is in a non-standard place, set `CHROME_BIN` to the executable path (see below). |
| `You must install or update .NET` | Install the .NET 8 SDK or newer. The project targets .NET 8 and is set to roll forward to a newer installed runtime. |
| Driver download fails | Selenium Manager needs internet access on first run. Behind a proxy, set `HTTPS_PROXY`. |
| `Could not confidently locate ...` | The page has no usable label for that field. The message lists the top candidates; try a larger `--timeout` if the page loads slowly. |
| PowerShell: `FORM_URL=... dotnet run` fails | That is bash syntax. Use `$env:FORM_URL = "..."` as shown above. |

Setting `CHROME_BIN`:

```bash
# macOS example
CHROME_BIN="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" dotnet run --project src
```

```powershell
# Windows PowerShell
$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"
dotnet run --project src
```

The program always starts Chrome with `--no-sandbox` and `--disable-dev-shm-usage`. These are only needed inside containers (Docker, Codespaces) and are harmless on a normal machine.

## 6. How it works

1. **Scan** (`src/PageScan.js`, run inside the page). For each candidate control of the right *type* (tag type is stable), collect every piece of human-readable text attached to it, tagged by source: `<label for>`, wrapping `<label>`, `aria-label` / `aria-labelledby`, `placeholder`, `title`, text immediately before or after it in the DOM, and (weakly) `name` / `id`. For radios it also collects the labels of the whole **group** (fieldset legend, aria, heading text before the group).
2. **Score** (`src/Scoring.cs`). Text is normalised (`firstName`, `first_name` and `First Name` all become `first name`). Matching a field's phrases adds that source's weight (label 100 down to id/name 30). Matching a *negative* phrase (*Last*, *Full*, *Username*, *Country*, *City*...) subtracts 1.5 times the weight, so look-alikes lose. The highest score wins. If nothing clears a minimum, the run fails loudly instead of guessing.
3. **Act and verify** (`src/Program.cs`):
   - **First Name:** type, read back, assert the value; also assert the value appears in exactly one field. Falls back to setting the value with JavaScript for inputs that ignore key events.
   - **Gender:** find the group labelled "Gender", pick the option by whole-word match (so `male` never matches `female`), click it (falling back to its label, then JavaScript, for hidden custom radios) and assert it is selected. Then switch to another option and back to prove it is a real exclusive group.
   - **State:** `SelectElement` by visible text (exact, then contains). If `--state` is given, a dropdown that actually offers that option scores higher. Assert the selected text.
4. Iframes are searched one level deep, and field lookup retries for up to `--timeout` seconds so late-rendered fields are found.

## 7. Limitations

See `NOTE.md`.