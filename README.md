# Resilient Form Tester (C# + Selenium)

Automates three fields on the CloudQA practice form — **First Name** (text box), **Gender** (radio) and **State** (dropdown) — and keeps working when the page changes: fields reordered, `id` / `name` / `class` changed, HTML structure altered, look-alike fields added.

Fields are found by **what they mean** (label text, `aria-label`, placeholder, nearby text, legend), never by a fixed XPath/CSS/id.

## Setup

Requirements: **.NET 8 SDK** and **Google Chrome** (Selenium Manager downloads the matching driver automatically).
If Chrome is installed in a non-standard location, set `CHROME_BIN` to its executable path.

```bash
git clone <this repo>
cd ResilientForm
dotnet build src
```

## Run

```bash
# default URL = http://app.cloudqa.io/home/AutomationPracticeForm
dotnet run --project src

# any URL (the URL is fully configurable) - CLI flag or env var
dotnet run --project src -- --url "https://example.com/modified-form.html"
FORM_URL="https://example.com/modified-form.html" dotnet run --project src

# non-standard Chrome/Chromium location
CHROME_BIN="/path/to/chrome" dotnet run --project src -- --headless

# local file, headless, custom values, screenshot
dotnet run --project src -- --url "file:///abs/path/page.html" --headless \
    --first-name "Hazel" --gender Female --state Maharashtra --screenshot out.png
```

| Option | Meaning | Default |
|---|---|---|
| `--url` / env `FORM_URL` | page to test (`http(s)://` or `file://`) | the CloudQA form |
| `--first-name` | text to type | `Hazel` |
| `--gender` | option to pick (`Male`/`Female`/…) | `Female` |
| `--state` | State option text | first real option |
| `--timeout` | seconds to wait for a field | `15` |
| `--headless` | no browser window | off |
| `--screenshot` | save a screenshot at the end | off |

Exit code is `0` if all three checks pass, `1` otherwise.

## Prove it survives page changes

`samples/` has the original form plus five mutated copies:

| File | What changed |
|---|---|
| `02-reordered-renamed` | field order shuffled; all ids, names, classes renamed |
| `03-no-attributes-wrapped` | no ids/names/classes at all; table layout; labels wrap inputs; decoys *Full Name*, *Last Name*, *Country* |
| `04-placeholders-aria-only` | no visible labels; only placeholders and `aria-label`s; deeply nested divs |
| `05-nearby-text-decoys` | labels are plain `<span>`s; decoys *Username*, *Last Name*, *Marital status* radios, *City* select, *Statement of purpose* |
| `06-custom-hidden-radios` | real radios are invisible; styled `<label>` pills |

```bash
./run-samples.sh          # or .\run-samples.ps1 on Windows
```

## How it works

1. **Scan** (`src/PageScan.js`, run in the page): for each candidate control of the right *type* (tag type is stable), collect every piece of human-readable text attached to it, tagged by source: `<label for>`, wrapping `<label>`, `aria-label(ledby)`, `placeholder`, `title`, text immediately before/after it in the DOM, and (weakly) `name`/`id`. For radios it also collects labels of the whole **group** (fieldset legend, aria, heading text before the group).
2. **Score** (`src/Scoring.cs`): text is normalised (`firstName`, `first_name`, `First Name` all become `first name`). Matching a field's phrases adds that source's weight (label 100 … id/name 30). Matching a *negative* phrase (*Last*, *Full*, *Username*, *Country*, *City*…) subtracts 1.5× the weight, so look-alikes lose. Highest score wins; if nothing clears a minimum, the run **fails loudly** listing the top candidates instead of touching a wrong field.
3. **Act + verify** (`src/Program.cs`):
   - First Name: type, read back, assert value; also assert the value appears in exactly one field. Falls back to JS value-setting for inputs that swallow key events.
   - Gender: find the group called "Gender", pick the option by *word* match (so `male` ≠ `female`), click (falls back to clicking the label, then JS, for hidden custom radios), assert `Selected`; then switch to another option and back to prove it is a real exclusive group.
   - State: `SelectElement` by visible text (exact, then contains); if `--state` is given, a dropdown that actually offers that option scores higher; assert the selected text.
4. Also searches same-origin/cross-origin **iframes** (one level deep) and retries for up to `--timeout` seconds so late-rendered fields are found.

## Limitations

See `NOTE.md`.
