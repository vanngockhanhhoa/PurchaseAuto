using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using AutoPurchase.Model;

namespace AutoPurchase;

public class AutomationService
{
    public async Task Run(string coin, string cardType, Action<int>? onQrShown = null, Action? onQrDone = null)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();

            var tempProfileDir = Path.Combine(Path.GetTempPath(), "chrome-autopurchase-" + Guid.NewGuid().ToString("N"));

            var context = await playwright.Chromium.LaunchPersistentContextAsync(tempProfileDir, new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                SlowMo = 200,
                ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled"
                }
            });

            var page = await context.NewPageAsync();

            await page.GotoAsync("https://www.tiktok.com/coin");

            await page.ClickAsync("div.login-prompt-KyEi8e");
            await page.GetByText("Use QR code").ClickAsync();

            onQrShown?.Invoke(60);

            // Doi den luc tren man hinh load xong text View transaction history : <a data-e2e="wallet-transaction-history-entrance" class="transaction-history-link-me24w5">View transaction history</a>
            // Đợi trang load xong phần ví (tối đa 60 giây, nếu timeout thì refresh)
            while (true)
            {
                try
                {
                    await page.WaitForSelectorAsync(
                        "[data-e2e='wallet-transaction-history-entrance']",
                        new PageWaitForSelectorOptions { Timeout = 60000 }
                    );
                    break;
                }
                catch (TimeoutException)

                {
                    onQrShown?.Invoke(60);
                    await page.ReloadAsync();
                }
            }

            onQrDone?.Invoke();

            // Lấy tất cả element có data-e2e bắt đầu bằng wallet-package-coin-num-
            var coinElements = await page.QuerySelectorAllAsync("[data-e2e^='wallet-package-coin-num-']");
            var priceElements = await page.QuerySelectorAllAsync("[data-e2e^='wallet-package-price-']");

            var coinList = new List<int>();
            var priceList = new List<int>();

            for (int i = 0; i < coinElements.Count; i++)
            {
                var coinText = await coinElements[i].InnerTextAsync();
                var priceText = await priceElements[i].InnerTextAsync();

                // ---- XỬ LÝ PRICE ----
                priceText = Regex.Replace(priceText, @"\D", "");

                if (int.TryParse(priceText, out int priceValue))
                {
                    priceList.Add(priceValue);
                }

                // ---- XỬ LÝ COIN ----
                coinText = coinText.Replace(",", "").Trim();

                if (int.TryParse(coinText, out int coinValue))
                {
                    coinList.Add(coinValue);
                }
            }

            for (int i = 0; i < coinElements.Count; i++)
            {
                Console.WriteLine("coin:" + coinList[i]);
                Console.WriteLine("price:" + priceList[i]);
            }

            int targetCoin = int.TryParse(coin, out int parsed) ? parsed : 0;
            int packageIndex = coinList.IndexOf(targetCoin);

            if (targetCoin <= 50000 && packageIndex < 0)
            {
                // Không có gói khớp → chọn custom
                var customBtn = page.Locator("[data-e2e='wallet-package-custom']");
                if (await customBtn.CountAsync() > 0)
                {
                    await customBtn.ClickAsync();

                    var customInput = page.Locator("[data-e2e='wallet-package-coin-custom-input-box']");
                    await customInput.WaitForAsync(new() { State = WaitForSelectorState.Visible });
                    await customInput.FillAsync(coin);
                }
            }
            else
            {
                // Có gói khớp hoặc coin > 50000 → chọn theo index, fallback về 0
                if (packageIndex < 0) packageIndex = 0;

                var packageBtn = page.Locator($"[data-e2e='wallet-package-{packageIndex}']");
                if (await packageBtn.CountAsync() > 0)
                {
                    await packageBtn.ClickAsync();
                }
            }

            // Đợi 3 giây sau khi chọn gói
            await page.WaitForTimeoutAsync(3000);

            var buyBtn = page.Locator("[data-e2e='wallet-buy-now-button']");
            await buyBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await buyBtn.ClickAsync();

            var cardOption = page.Locator(
                "[data-e2e='payment-method-item-ccdc']"
            );

            await cardOption.WaitForAsync();
            await cardOption.ClickAsync();

            // Đợi 5 giây cho payment frame load
            await page.WaitForTimeoutAsync(5000);

            var paymentFrame = page.FrameLocator("iframe[src*='pipo_checkouts']");

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var projectPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\..\"));

            var filePath = Path.Combine(projectPath, "cards.json");

            var json = await File.ReadAllTextAsync(filePath);

            var cards = JsonSerializer.Deserialize<List<PaymentConfig>>(json);

            await paymentFrame.Locator("input[name='card_number']")
                .FillAsync(cards[0].CardNumber);

            await paymentFrame.Locator("input[name='holder_name']")
                .FillAsync(cards[0].HolderName);

            await paymentFrame.Locator("input[name='expiration_date']")
                .FillAsync(cards[0].ExpirationDate);

            await paymentFrame.Locator("input[name='cvv']")
                .FillAsync(cards[0].Cvv);


            // // Cách đúng hơn:
            //             var saveCardCheckbox = page.Locator("[data-e2e='payment-method-save-button'] input[type='checkbox']");
            //
            //             if (await saveCardCheckbox.IsCheckedAsync())
            //             {
            //                 await saveCardCheckbox.UncheckAsync();
            //             }


            // ===== 3. Click Pay Now (popup - KHÔNG iframe) =====
            var payButton = page.Locator("[data-e2e='cashier-footer-button']");

            await payButton.WaitForAsync();
            await payButton.ClickAsync();

            var resultModal = page.Locator("[data-e2e='recharge-end-result-page']");
            await resultModal.WaitForAsync(new() { Timeout = 120000 }); // 2 minutes for OTP/3D Secure
            var closeBtn = resultModal.Locator("span.cursor-pointer");
            await closeBtn.WaitForAsync();
            await closeBtn.ClickAsync();

            await context.CloseAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}