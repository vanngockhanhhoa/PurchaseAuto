# AutoPurchase - TikTok Coin Automation

A Windows desktop application that automates the process of purchasing TikTok coins using a saved Chrome browser profile and credit/debit card information.

## Overview

AutoPurchase uses [Microsoft Playwright](https://playwright.dev/dotnet/) to drive a real Chrome browser, navigate to the TikTok coin store, select a coin package, and complete payment automatically — without any manual interaction after clicking **Start**.

## Tech Stack

| Component | Details |
|-----------|---------|
| Language | C# / .NET 10 |
| UI | Windows Forms (WinForms) |
| Browser Automation | Microsoft Playwright 1.58.0 |
| Browser | Google Chrome (persistent profile) |
| Target Platform | Windows |

## Project Structure

```
AutoPurchase/
├── AutoPurchase.sln
└── AutoPurchase/
    ├── Program.cs              # Entry point
    ├── Form1.cs                # Main UI form
    ├── Form1.Designer.cs
    ├── AutomationService.cs    # Core automation logic (Playwright)
    ├── CoinPackage.cs          # Model for coin package data
    ├── cards.json              # Payment card configurations (see below)
    └── Model/
        └── PaymentConfig.cs    # Model for card info deserialized from cards.json
```

## How It Works

1. **Launch** — Opens Chrome using a persistent profile stored at `C:\chrome-profile`, so your TikTok login session is preserved across runs.
2. **Navigate** — Goes to `https://www.tiktok.com/coin`.
3. **Login** — If not already logged in, clicks the login prompt and selects **QR code** login.
4. **Wait for login** — Waits up to **60 seconds** for the wallet page to fully load (detects the transaction history link, which confirms the user has scanned the QR code and logged in). If the element is not found within 60 seconds, the page is automatically **refreshed** and the wait restarts.
5. **Read packages** — Scrapes all available coin packages (coin amount + price) from the page.
6. **Select package** — Matches the entered coin amount against scraped packages using this logic:
   - If **coin ≤ 50,000** and an exact match exists → click that package.
   - If **coin ≤ 50,000** and no match exists → click `wallet-package-custom`, wait for the custom input box to appear, then type the coin amount into it.
   - If **coin > 50,000** and an exact match exists → click that package.
   - If **coin > 50,000** and no match exists → fall back to `wallet-package-0`.
7. **Wait 3s** — Pauses 3 seconds after package selection before proceeding.
8. **Buy** — Clicks **Buy Now**.
9. **Payment** — Selects the credit/debit card payment method, waits 5 seconds for the payment iframe to load, then fills in card details from `cards.json`.
10. **Confirm** — Clicks **Pay Now** and waits for the result modal, then closes it.

## Setup

### Prerequisites

- Windows OS
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Google Chrome installed at `C:\Program Files\Google\Chrome\Application\chrome.exe`
- A Chrome profile directory at `C:\chrome-profile` (created automatically on first run, or copy an existing profile)

### Install Playwright browsers

After building the project, run once to install the required browser drivers:

```bash
pwsh bin/Debug/net10.0-windows/playwright.ps1 install chromium
```

Or via .NET tool:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

### Configure payment cards

Edit `cards.json` with your actual card information:

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

> **Warning:** `cards.json` contains sensitive financial data. Do **not** commit this file to any public repository. Add it to `.gitignore`.

## Usage

1. Open the solution in Visual Studio or run with `dotnet run`.
2. In the **Automation Buy Tool** window:
   - Enter the number of coins in the **Số coin** field.
   - Select a card type (**Visa**, **MasterCard**, or **Momo**) from the dropdown.
3. Click **Start**.
4. The Chrome browser will open and execute the purchase flow automatically.

## Configuration

| Setting | Location | Default |
|---------|----------|---------|
| Chrome profile path | `AutomationService.cs` line 17 | `C:\chrome-profile` |
| Chrome executable path | `AutomationService.cs` line 19 | `C:\Program Files\Google\Chrome\Application\chrome.exe` |
| Card data file | `AutomationService.cs` (reads `cards.json`) | Project root |
| Browser slow-motion delay | `AutomationService.cs` line 20 | `200ms` |

## Notes

- The browser runs in **headed mode** (visible window) so you can monitor the automation.
- `--disable-blink-features=AutomationControlled` is passed to Chrome to reduce bot detection.
- The app currently always uses the **first card** in `cards.json` (`cards[0]`).
- After a successful purchase, the browser closes automatically.