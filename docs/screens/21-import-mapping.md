# User Guide: CSV Import Profile Builder

[← Previous: 20 - Statement Import (OFX / CSV)](20-statement-imports.md) | [← Back to Master Index](../README.md) | [Next: 22 - Financial Reports & Charts →](22-financial-reports.md)

---

The **CSV Profile Builder** lets you map arbitrary CSV columns from any financial institution (e.g. BNP Paribas, Crédit Agricole, Revolut, Chase) to Cadran's core accounting fields.

---

## 1. Visual Overview & Action Callouts

![CSV Mapping Profile Builder](../images/screens/21-import-mapping-screen-annotated.png)

---

## 2. Action Reference Table

| Marker | UI Element | Action Type | What to do |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **"New Profile" Toggle** | **CLICK HERE** | Expand the profile builder form to configure a new CSV format. |
| <kbd>2</kbd> | **Profile Name Input** | **TYPE HERE** | Give this format an identifiable bank name (e.g., *BoursoBank Standard Export*, *N26 Statement*). |
| <kbd>3</kbd> | **Column Delimiter** | **TYPE CHARACTER** | Specify the separator between columns (usually `;`, `,`, or `\t`). |
| <kbd>4</kbd> | **Date Format Pattern** | **TYPE FORMAT** | Enter the date format used in the CSV (e.g., `dd/MM/yyyy`, `yyyy-MM-dd`, or `MM/dd/yyyy`). |
| <kbd>5</kbd> | **"Has Header" Checkbox** | **TOGGLE CHECKBOX** | Check this box if the first row contains column headers so Cadran skips it during parsing. |
| <kbd>6</kbd> | **Column Index Mappings** | **SET COLUMN NUMBERS** | Enter the 0-based column indexes for Date (0), Label (1), and Amount (2). |
| <kbd>7</kbd> | **"Save Profile" Button** | **CLICK HERE** | Save the profile. It will now appear in the Statement Importer dropdown. |

---

## 3. Understanding 0-Based Column Indexes

In a spreadsheet or CSV:
- Column A = Index **0**
- Column B = Index **1**
- Column C = Index **2**
- Column D = Index **3**

If your bank uses separate Debit and Credit columns rather than a single signed Amount column, you can leave the single Amount field empty and map the separate Debit and Credit indexes.

---

## 4. Step-by-Step Instructions

1. **Open the builder**: Click **New Profile** (<kbd>1</kbd>).
2. **Name your bank profile**: Enter the institution name in field (<kbd>2</kbd>).
3. **Set separator and date format**: Enter the delimiter (<kbd>3</kbd>) and matching date syntax (<kbd>4</kbd>).
4. **Map the columns**: Look at your bank CSV in Excel or Notepad and enter the column indexes (<kbd>6</kbd>).
5. **Save**: Click **Save Profile** (<kbd>7</kbd>).
