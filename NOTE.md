# Approach and limitations (Task 1)

**Approach.** Instead of one locator per field, the program asks "which control on this page *means* First Name / Gender / State?" It gathers all the human-readable text near each candidate, scores it against phrases for the wanted field and against phrases for look-alikes, and picks the best. Every action is followed by a read-back assertion, and a low-confidence match fails with a diagnostic rather than guessing.

**Limitations (honest list).**
- English-only phrase lists (`first name`, `gender`, `state`, `province`…). Other languages need more phrases.
- A field with no label, placeholder, aria text or meaningful nearby text can only be matched through its `name`/`id`, which is deliberately weak.
- Fully custom widgets (div-based dropdowns, canvas, shadow DOM) are not handled; only native `input`/`select` elements. Iframes are searched one level deep only.
- Ambiguity: if two fields look equally right (e.g. two "State" dropdowns) it picks the first in document order and prints a warning only for text boxes.
- The scan adds a harmless `data-rq-id` attribute to candidate elements to re-find them.
- Phrase lists and weights are hand-tuned on the sample pages; they have not been tested against a large variety of real sites.
- Written to run in Chrome only.
