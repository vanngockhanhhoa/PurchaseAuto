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
            var playwright = await Playwright.CreateAsync();

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

            var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

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
            var userNameElement = await page.QuerySelectorAsync("[data-e2e^='wallet-user-name']");
            var userName = userNameElement != null ? await userNameElement.InnerTextAsync() : "unknown";

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
            await page.WaitForTimeoutAsync(2000);

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

            await HandlePaymentResultAsync(page);

            await TakeScreenshotAsync(page);
            
            await page.WaitForTimeoutAsync(2000);
            
            // Chrome keeps session data (cookies, login tokens) in memory while running. It only flushes them to disk when the browser closes gracefully.                                                                                      
            //                                                                                                                                                                                                                        
            //     If you copy the profile while Chrome is still open:
            // - The Cookies SQLite file may be incomplete or mid-write                                                                                                                                                                         
            //     - Login session tokens haven't been persisted yet                                                                                                                                                                              
            //     - Some files are still locked by the Chrome process, causing copy failures
            //
            // context.CloseAsync() forces Chrome to:
            // 1. Write all cookies/session data to disk
            // 2. Release file locks on profile files
            //
            //     Without it → profile is copied before login is saved → opening the shortcut shows logged-out TikTok.
            await context.CloseAsync(); // flush cookies/session to disk before copying profile, disk save 
            
            await SaveProfileAsync(tempProfileDir, userName);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    // Detects which case after Pay: A = direct result, B = OTP required
    private static async Task HandlePaymentResultAsync(IPage page)
    {
        var resultModalTask = page.WaitForSelectorAsync(
            "[data-e2e='recharge-end-result-page']",
            new PageWaitForSelectorOptions { Timeout = 120000 });

        var otpInputTask = page.WaitForSelectorAsync(
            "#radioContainer",
            new PageWaitForSelectorOptions { Timeout = 120000 });

        var winner = await Task.WhenAny(resultModalTask, otpInputTask);

        if (winner == otpInputTask)
            await HandleOtpAsync(page);
        else
            await HandleDirectResultAsync(page);
    }

    // Case A: no OTP — payment succeeded or failed immediately
    private static async Task HandleDirectResultAsync(IPage page)
    {
        var resultModal = page.Locator("[data-e2e='recharge-end-result-page']");
        await resultModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var closeBtn = resultModal.Locator("span.cursor-pointer");
        await closeBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await closeBtn.ClickAsync();
        await page.EvaluateAsync("window.scrollTo(0, 0)");
    }

    // Case B: OTP required — auto-select SMS, click Continue, then wait for user to enter OTP
    private static async Task HandleOtpAsync(IPage page)
    {
        Console.WriteLine("OTP required — selecting SMS method...");

        // Select SMS radio option
        var smsRadio = page.Locator("input[name='method'][value='OTP_SMS']");
        await smsRadio.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await smsRadio.CheckAsync();

        // Click Continue to send SMS OTP
        var continueBtn = page.Locator("button#btnSubmit");
        await continueBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await continueBtn.ClickAsync();
        Console.WriteLine("SMS OTP sent — waiting for user to enter OTP...");

        // Wait until the result modal appears after user submits OTP (up to 3 minutes)
        var resultModal = page.Locator("[data-e2e='recharge-end-result-page']");
        await resultModal.WaitForAsync(new() { Timeout = 180000 });

        var closeBtn = resultModal.Locator("span.cursor-pointer");
        await closeBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await closeBtn.ClickAsync();
        await page.EvaluateAsync("window.scrollTo(0, 0)");
    }

    private static async Task TakeScreenshotAsync(IPage page)
    {
        var screenshotDir = @"C:\Users\Ngs-MT1694\Desktop\TransactionInfo\ScreenShoot";
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir, $"result_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var viewportSize = page.ViewportSize;
        var clipHeight = (viewportSize?.Height ?? 900) / 3;
        var clipWidth = viewportSize?.Width ?? 1280;
        await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = false, Clip = new Clip { X = 0, Y = 0, Width = clipWidth, Height = clipHeight } });
        Console.WriteLine($"Screenshot saved: {screenshotPath}");
    }

    private static async Task SaveProfileAsync(string tempProfileDir, string userName)
    {
        var safeName = string.Concat(userName.Split(Path.GetInvalidFileNameChars()));
        var profileRoot = @"C:\Users\Ngs-MT1694\Desktop\TransactionInfo\Profile";
        var profileDest = Path.Combine(profileRoot, safeName);
        if (Directory.Exists(profileDest)) Directory.Delete(profileDest, recursive: true);
        CopyDirectory(tempProfileDir, profileDest);
        Console.WriteLine($"Profile saved: {profileDest}");

        var lnkPath = Path.Combine(profileRoot, $"{safeName}.lnk");
        var chromeExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        var chromeArgs = $"--user-data-dir=\"{profileDest}\" \"https://www.tiktok.com/coin\"";
        var chromeDir = Path.GetDirectoryName(chromeExe)!;
        var psScript = $"""
            $s = (New-Object -ComObject WScript.Shell).CreateShortcut("{lnkPath}")
            $s.TargetPath = "{chromeExe}"
            $s.Arguments = '{chromeArgs}'
            $s.IconLocation = "{chromeExe}, 0"
            $s.WorkingDirectory = "{chromeDir}"
            $s.Save()
            """;
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));
        using var ps = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        await ps.WaitForExitAsync();
        Console.WriteLine($"Shortcut saved: {lnkPath}");
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            try { File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true); }
            catch { /* skip locked files */ }
        }
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}