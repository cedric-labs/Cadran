# User Guide: Custom Transaction Statuses & Reconciliation

[← Back to Account Types](10-account-types.md) | [Back to Master Index](../README.md)

---

The **Transaction Statuses** screen allows you to configure reconciliation workflow tags. Statuses help you trace operations through their lifecycle: pending verification, cleared at the bank, reconciled with official statements, or flagged for dispute.

---

## 1. Visual Overview & Action Callouts

![Transaction Statuses Screen](../images/screens/11-account-statuses-screen-annotated.png)

---

## 2. Action Reference Table

| Marker | UI Element | Action Type | What to do |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **"New Status" Button** | **CLICK HERE** | Click to open the modal dialog and create a new status tag (e.g., `In Dispute`, `To Verify`). |
| <kbd>2</kbd> | **Status Tag Badge** | **SELECT** | Review the visual badge rendering (icon and color) used in the transaction ledger. |
| <kbd>3</kbd> | **Default Status Flag** | **SELECT** | Identifies the default status automatically assigned to incoming imported transactions. |
| <kbd>4</kbd> | **"Edit" Button** | **CLICK HERE** | Click to modify a status tag's label, color, icon, or toggle whether it serves as default. |

---

## 3. Recommended Status Workflow

A clean personal finance workflow generally follows three simple stages:

1. **Pending**: An operation has been scheduled or made, but not yet reflected on your banking statement.
2. **Cleared**: The transaction appears on your online bank feed with its confirmed date and amount.
3. **Reconciled**: The transaction has been checked against your monthly bank PDF statement.

---

## 4. Step-by-Step Procedure

1. **Create a custom status**:
   - Click **New Status** (<kbd>1</kbd>) in the top-right corner.
   - Enter a label (e.g., `To Verify`), pick an icon and color, and select whether to make it the default for new imports.
2. **Modify existing tags**:
   - Click the pencil **Edit** button (<kbd>4</kbd>) on any row in the list to update its name or styling.
3. **Designate a new import default**:
   - Edit the status you want as your initial intake tag and enable the **Default status** option (<kbd>3</kbd>).
