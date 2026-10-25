# User Guide: Automated Categorization Rules Engine

[← Previous: 17 - Create & Edit Categories](17-create-category.md) | [← Back to Master Index](../README.md) | [Next: 19 - Recurring Operations & Bills Schedule →](19-recurring-transactions.md)

---

The **Rules Engine** automates transaction categorization and merchant linking. When importing bank statements or entering operations, Cadran evaluates your active rules in priority order and automatically assigns categories and payees.

---

## 1. Visual Overview & Action Callouts

![Rules Engine & Pattern Builder](../images/screens/18-rules-engine-screen-annotated.png)

---

## 2. Action Reference Table

| Marker | UI Element | Action Type | What to do |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **"Add Rule" Toggle** | **CLICK HERE** | Expand the rule creation form to configure a new auto-categorization rule. |
| <kbd>2</kbd> | **"Apply to Existing" Button** | **CLICK HERE** | Run all active categorization rules retroactively across all past unclassified transactions. |
| <kbd>3</kbd> | **Rule Name Input** | **TYPE HERE** | Give the rule a clear name (e.g., *Supermarket Groceries*, *Electricity Bills*). |
| <kbd>4</kbd> | **Assigned Category Dropdown** | **SELECT OPTION** | Choose the destination category automatically applied whenever this rule matches. |
| <kbd>5</kbd> | **Detection Keywords & Wildcards** | **TYPE HERE** | Enter the text to match (e.g., `CARREFOUR*` or `EDF`). Wildcards `*` and `%` are supported. |
| <kbd>6</kbd> | **Live Statement Tester** | **TEST SAMPLE** | Paste a sample transaction label from your bank statement to verify that the rule matches. |
| <kbd>7</kbd> | **"Create Rule" Button** | **CLICK HERE** | Save the rule and add it to the active rules list. |

---

## 3. Match Conditions & Rule Priority

- **Contains**: Matches if the keywords appear anywhere within the raw bank description.
- **Starts With / Ends With**: Matches when the keyword appears at the start or end of the statement text.
- **Wildcard Support**: `*` or `%` replaces any arbitrary text (e.g., `AMAZON*PRIME`).
- **Priority**: Rules evaluate from lowest number (1 = highest priority) to highest. Once a high-priority rule matches, processing stops for that transaction.

---

## 4. Step-by-Step Instructions

1. **Open rule creator**: Click **Add Rule** (<kbd>1</kbd>).
2. **Name and target category**: Enter a description (<kbd>3</kbd>) and select the target category (<kbd>4</kbd>).
3. **Define keywords**: Enter the vendor identifier or wildcard pattern in field (<kbd>5</kbd>).
4. **Test the pattern**: Paste an actual statement label into field (<kbd>6</kbd>) to confirm immediate matching.
5. **Save rule**: Click **Create Rule** (<kbd>7</kbd>).
6. **Retroactive classification**: Click **Apply Rules** (<kbd>2</kbd>) to categorize all past matching transactions in bulk.
