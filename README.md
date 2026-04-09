# PurchaseAuto

A Windows desktop application that automates TikTok coin purchases using a real Chrome browser driven by Playwright. Logs in via QR code, selects a coin package, fills card details, handles payment (with or without OTP), then saves a screenshot and a reusable Chrome profile shortcut.

---

## Tech Stack

| Component | Details |
|---|---|
| Language | C# / .NET 10 |
| UI | Windows Forms (WinForms) |
| Browser Automation | Microsoft Playwright 1.58.0 |
| Browser | Google Chrome (persistent profile) |
| Platform | Windows |

---

## Project Structure

```
PurchaseAuto/
├── AutoPurchase.sln
└── AutoPurchase/
    ├── Program.cs                # Entry point
    ├── Form1.cs                  # Main UI form
    ├── Form1.Designer.cs
    ├── AutomationService.cs      # Core automation logic
    ├── cards.json                # Payment card config (sensitive — do not commit)
    └── Model/
        └── PaymentConfig.cs      # Card info model
```

---

## Prerequisites

- Windows OS
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Google Chrome at `C:\Program Files\Google\Chrome\Application\chrome.exe`

### Install Playwright browsers (once after build)

```bash
pwsh bin/Debug/net10.0-windows/playwright.ps1 install chromium
```

---

## cards.json format

Located at the project root. Always uses the first card (`cards[0]`).

```json
[
  {
    "CardNumber": "YOUR_CARD_NUMBER",
    "HolderName": "YOUR_NAME",
    "ExpirationDate": "MM/YY",
    "Cvv": "CVV"
  }
]
```

> **Warning:** Contains sensitive financial data. Add `cards.json` to `.gitignore` and never commit it.

---

## Usage

1. Run the solution (`dotnet run` or Visual Studio).
2. In the **Automation Buy Tool** window:
   - Enter the number of coins in the **Số coin** field.
   - Select card type (**Visa**, **MasterCard**, or **Momo**).
3. Click **Start**.
4. Scan the QR code in Chrome when prompted.
5. The rest runs automatically.

---

## Automation Flow

### 1. Login via QR code
Opens `https://www.tiktok.com/coin`, clicks the login prompt, and switches to QR code mode. Waits up to **60 seconds** for the wallet page to confirm login (detected via `[data-e2e="wallet-transaction-history-entrance"]`). Auto-refreshes and retries if the timeout is hit.

### 2. Select coin package
- Coin ≤ 50,000 with exact match → click that package
- Coin ≤ 50,000 with no match → use the custom input field
- Coin > 50,000 with exact match → click that package
- Coin > 50,000 with no match → fall back to package index 0

### 3. Fill card details
Selects the credit/debit card payment method, waits for the payment iframe (`pipo_checkouts`) to load, then fills card number, holder name, expiration date, and CVV from `cards.json`.

### 4. Payment result — two cases

After clicking **Pay Now**, the automation races between two outcomes:

#### Case A — No OTP (direct result)
The result modal appears immediately. The automation closes it and scrolls to top.

#### Case B — OTP required
A method selection modal appears. The automation:
1. Selects **SMS** automatically
2. Clicks **Continue** to send the OTP
3. Waits up to **3 minutes** for you to enter the OTP manually in the browser
4. Once the result modal appears, closes it and scrolls to top

### 5. Screenshot
Captures the top third of the viewport and saves it to:
```
C:\Users\Ngs-MT1694\Desktop\TransactionInfo\ScreenShoot\result_yyyyMMdd_HHmmss.png
```

### 6. Save Chrome profile + shortcut
The browser is closed first (so Chrome flushes cookies and session tokens to disk), then:
- Copies the Chrome profile to `Desktop\TransactionInfo\Profile\{userName}\`
- Creates a `.lnk` shortcut at `Desktop\TransactionInfo\Profile\{userName}.lnk`

Opening the shortcut launches Chrome directly at `https://www.tiktok.com/coin` with the saved TikTok login session — no re-login needed.

---

## Output files

| Path | Description |
|---|---|
| `Desktop\TransactionInfo\ScreenShoot\result_*.png` | Screenshot of the result page |
| `Desktop\TransactionInfo\Profile\{userName}\` | Saved Chrome profile (login session) |
| `Desktop\TransactionInfo\Profile\{userName}.lnk` | Shortcut to reopen Chrome with this profile |

---

## Method reference (`AutomationService.cs`)

| Method | Description |
|---|---|
| `Run(coin, cardType, ...)` | Main entry point |
| `HandlePaymentResultAsync(page)` | Detects Case A or B after pay click |
| `HandleDirectResultAsync(page)` | Case A — closes result modal directly |
| `HandleOtpAsync(page)` | Case B — selects SMS, clicks Continue, waits for user OTP |
| `TakeScreenshotAsync(page)` | Saves top-third screenshot to ScreenShoot folder |
| `SaveProfileAsync(tempDir, userName)` | Copies profile and creates `.lnk` shortcut |
| `CopyDirectory(source, dest)` | Recursive directory copy, skips locked files |

---

## Notes

- Browser runs in **headed mode** (visible) so you can monitor and intervene if needed.
- `--disable-blink-features=AutomationControlled` is passed to reduce bot detection.
- The browser slow-motion delay is set to `200ms`.