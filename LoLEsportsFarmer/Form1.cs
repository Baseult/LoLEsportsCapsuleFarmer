using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.Drawing;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Bunifu.UI.WinForms;
using Bunifu.UI.WinForms.BunifuButton;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using SeleniumUndetectedChromeDriver;
using Label = System.Windows.Forms.Label;
using ThreadState = System.Threading.ThreadState;

namespace LoLEsportsFarmer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public class Drops
        {
            public int Dropcount { get; set; }
            public string User { get; set; }
        }

        public class RunningThreads
        { 
            public string Username { get; set; }
            public string MatchName { get; set; }
            public Thread Thread { get; set; }
            public CancellationTokenSource TokenSource { get; set; }
        }

        public class RunningDrivers
        {
            public UndetectedChromeDriver Driver { get; set; }
            public string Username { get; set; }
            public string Match { get; set; }
        }

        public class ActiveStreams
        {
            public string Url { get; set; }
            public string StreamName { get; set; }
        }


        public class User
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private bool _isTaskRunning;
        private int _lastYPosition;
        private int _lastYPosition2;
        private bool _makeImage;
        private bool _startNext = true;
        private bool _removeContent = true;
        private bool _headless = true;
        private bool _ownBrowser = false;
        private bool _isSleeping = false;

        private readonly List<Drops> _dropList = new List<Drops>();
        private readonly List<RunningDrivers> _driverList = new List<RunningDrivers>();
        private List<User> _userList = new List<User>();
        private List<ActiveStreams> _activeStreams = new List<ActiveStreams>();
        private readonly List<RunningThreads> _runningThreads = new List<RunningThreads>();
        private bool _doCancel;
        private bool _isRunning;

        private void Console(string msg)
        {
            try
            {
                if (ConsoleBox.InvokeRequired)
                {
                    Invoke(new Action<string>(Console), msg);
                }
                else
                {
                    ConsoleBox.AppendText(msg + Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private void LabelStreams(string msg)
        {
            try
            {
                if (LastCheck.InvokeRequired)
                {
                    Invoke(new Action<string>(LabelStreams), msg);
                }
                else
                {
                    LastCheck.Text = msg;
                }
            }
            catch
            {
            }
        }

        private void Label(string msg, UserControl label)
        {
            try
            {
                if (ConsoleBox.InvokeRequired)
                {
                    Invoke(new Action<string, UserControl>(Label), msg, label);
                }
                else
                {
                    label.Controls[1].Text = "Status: " + msg;
                }
            }
            catch
            {
            }
        }

        private async Task<UndetectedChromeDriver> StartDriver()
        {
            var options = new ChromeOptions();

            if (_headless) 
                options.AddArgument("--headless=new");

            options.AddArgument("--no-sandbox");
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-popup-blocking");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-software-rasterizer");
            options.AddArgument("--mute-audio");
            options.AddArguments("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.5993 Safari/537.36");
            options.SetLoggingPreference("browser", LogLevel.All);

            return UndetectedChromeDriver.Create(driverExecutablePath: await new ChromeDriverInstaller().Auto(), options: options, hideCommandPromptWindow: true);
        }

        private static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>
        {
            { "https://lolesports.com/live/lck_challengers_league", "https://lolesports.com/live/lck_challengers_league/lckcl" },
            { "https://lolesports.com/live/lpl", "https://lolesports.com/live/lpl/lpl" },
            { "https://lolesports.com/live/lck", "https://lolesports.com/live/lck/lck" },
            { "https://lolesports.com/live/lec", "https://lolesports.com/live/lec/lec" },
            { "https://lolesports.com/live/lcs", "https://lolesports.com/live/lcs/lcs" },
            { "https://lolesports.com/live/lco", "https://lolesports.com/live/lco/lco" },
            { "https://lolesports.com/live/cblol_academy", "https://lolesports.com/live/cblol_academy/cblol" },
            { "https://lolesports.com/live/cblol", "https://lolesports.com/live/cblol/cblol" },
            { "https://lolesports.com/live/lla", "https://lolesports.com/live/lla/lla" },
            { "https://lolesports.com/live/ljl-japan/ljl", "https://lolesports.com/live/ljl-japan/riotgamesjp" },
            { "https://lolesports.com/live/ljl-japan", "https://lolesports.com/live/ljl-japan/riotgamesjp" },
            { "https://lolesports.com/live/turkiye-sampiyonluk-ligi", "https://lolesports.com/live/turkiye-sampiyonluk-ligi/riotgamesturkish" },
            { "https://lolesports.com/live/cblol-brazil", "https://lolesports.com/live/cblol-brazil/cblol" },
            { "https://lolesports.com/live/pcs/lXLbvl3T_lc", "https://lolesports.com/live/pcs/lolpacific" },
            { "https://lolesports.com/live/ljl_academy/ljl", "https://lolesports.com/live/ljl_academy/riotgamesjp" },
            { "https://lolesports.com/live/european-masters", "https://lolesports.com/live/european-masters/EUMasters" },
            { "https://lolesports.com/live/worlds", "https://lolesports.com/live/worlds/riotgames" },
        };


        private async Task CheckLiveGames()
        {
            var checkMatches = new List<ActiveStreams>();
            var counter = 0;

            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("x-api-key", "0TvQnueqKa5mxJntVWt0w4LpLfEkrV1Ta8rQBb9Z");
                var response = await client.GetAsync("https://esports-api.lolesports.com/persisted/gw/getLive?hl=en-US");
                var responseContent = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(responseContent);
                var eventsData = data?.data.schedule.events;

                if (eventsData != null)
                {
                    foreach (var evnt in eventsData)
                    {
                        counter++;
                        var leagueData = evnt.league;
                        var streamsData = evnt.streams;
                        string slug = leagueData.slug;
                        string parameter = streamsData[0].parameter;
                        var url = $"https://lolesports.com/live/{slug}/{parameter}";

                        if (Overrides.ContainsKey(url))
                        {
                            url = Overrides[url];
                        }
                        checkMatches.Add(new ActiveStreams { Url = url, StreamName = leagueData.name.ToString() });
                    }
                }

                Console($"Found {counter} active streams");
            }
            catch (Exception e)
            {
                Console($"An error occurred while searching for live games - Error: {e.Message}");
            }

            _activeStreams = checkMatches;
            LabelStreams("Live Matches: " + _activeStreams.Count);
        }

        private Task Make_image(UndetectedChromeDriver driver)
        {
            if (_doCancel || _isSleeping) return Task.CompletedTask;

            try
            {
                var screenshotDriver = driver as ITakesScreenshot;
                var screenshot = screenshotDriver.GetScreenshot();

                Image screenshotImage;
                using (var ms = new MemoryStream(screenshot.AsByteArray))
                {
                    screenshotImage = Image.FromStream(ms);
                }

                PictureBox1.Invoke(new Action(() => PictureBox1.Image = screenshotImage));
            }
            catch (Exception e)
            {
                Console($"An error occurred while taking a browser screenshot. - Error: {e.Message}");
            }

            return Task.CompletedTask;
        }


        private Task<bool> Login(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, string password, UserControl label)
        {
            var failedloginattempts = 0;
            while (!_doCancel && !token.IsCancellationRequested && !_isSleeping)
            {
                try
                {
                    Label("Logging in", label);
                    Console($"User: {username} - Match: {matchName} - Logging in to LoLEsports");

                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

                    driver.GoToUrl("https://login.leagueoflegends.com/?redirect_uri=https://lolesports.com/&lang=en");

                    if (!javascriptBox.Checked)
                    {
                        wait.Until(ExpectedConditions.ElementIsVisible(By.Name("username")));
                        driver.FindElement(By.Name("username")).SendKeys(username);
                        driver.FindElement(By.Name("password")).SendKeys(password);
                        driver.FindElement(By.CssSelector("button[data-testid=btn-signin-submit]")).Click();
                        wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div[data-testid='riotbar:account:summonername']")));
                    }
                    else
                    {
                        wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('input[name=\"username\"][data-testid=\"input-username\"][type=\"text\"]');"));
                        ((IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('input[name=\"username\"][data-testid=\"input-username\"]');")).SendKeys(username);
                        ((IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('input[name=\"password\"][data-testid=\"input-password\"]');")).SendKeys(password);
                        ((IJavaScriptExecutor)driver).ExecuteScript("document.querySelector('button[data-testid=\"btn-signin-submit\"]').click();");
                        wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('[data-testid=\"riotbar:account:summonername\"]');"));
                    }

                    Console($"User: {username} - Match: {matchName} - Logged in to LoLEsports");
                    Label("Logged in", label);

                    return Task.FromResult(true);
                }
                catch (WebDriverException e)
                {
                    try
                    {
                        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));

                        if (!javascriptBox.Checked)
                        {
                            wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("[data-testid=error-message")));
                        }
                        else
                        {
                            wait.Until(browser =>
                                ((IJavaScriptExecutor)driver).ExecuteScript(
                                    "return document.querySelector('[data-testid=\"error-message\"]');"));
                        }

                        Console(
                            $"User: {username} - Match: {matchName} - An error occurred while logging in. - Error: Wrong Username or Password");
                        Label("Error > Wrong Username or Password", label);
                        return Task.FromResult(false);

                    }
                    catch
                    {

                        // If the specific iframe is found, wait for 10 seconds
                        try
                        {


                            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

                            if (!javascriptBox.Checked)
                            {
                                wait.Until(ExpectedConditions.FrameToBeAvailableAndSwitchToIt(By.CssSelector("iframe[src*='hcaptcha.com']")));
                            }
                            else
                            {
                                wait.Until(browser =>
                                    ((IJavaScriptExecutor)driver).ExecuteScript(
                                        "return document.querySelector('iframe[src*=\"hcaptcha.com\"]');"));
                            }

                            Console($"User: {username} - Match: {matchName} - An error occurred while logging in. - Captcha found!");
                            Console($"User: {username} - Match: {matchName} - If you use a VPN disable it to prevent Captchas");
                            Console($"User: {username} - Match: {matchName} - Otherwise restart this bot with 'headless mode' disabled");
                            Console($"User: {username} - Match: {matchName} - Then you need to manually solve the captcha");
                            Label("Action required > Captcha found", label);

                            if (!javascriptBox.Checked)
                            {
                                wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div[data-testid='riotbar:account:summonername']")));
                            }
                            else
                            {
                                wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('[data-testid=\"riotbar:account:summonername\"]');"));
                            }

                            return Task.FromResult(true);
                        }
                        catch
                        {
                            failedloginattempts++;
                            Console($"User: {username} - Match: {matchName} - An error occurred while logging in. - Error: {e.Message}");

                            if (failedloginattempts > 3)
                            {
                                Label("Error > Could not login", label);
                                return Task.FromResult(false);
                            }
                        }
                    }
                }
            }

            return Task.FromResult(false);
        }

        private Task<bool> RejectCookieMessage(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return Task.FromResult(true);

            try
            {
                Label("Removing cookie", label);
                Console($"User: {username} - Match: {matchName} - Rejecting cookie");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

                if (javascriptBox.Checked)
                {
                    // Use Javascript to remove cookie popup
                    wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('.osano-cm-window__dialog');"));
                    ((IJavaScriptExecutor)driver).ExecuteScript("var element = document.querySelector('.osano-cm-window__dialog'); element.parentNode.removeChild(element);");
                }
                else
                {
                    // Use Selenium's hardcoded actions to remove cookie popup
                    var closeButton = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(".osano-cm-dialog__close.osano-cm-close")));
                    closeButton.Click();
                }

                //((IJavaScriptExecutor)driver).ExecuteScript(@"var element = document.querySelector('.status-summary'); element.parentNode.removeChild(element); console.log('INFORMATION - Have only nulls exception yet, Probably optimizing that');");

                Console($"User: {username} - Match: {matchName} - Rejected cookie popup");
                Label("Removed cookie", label);
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - An error occurred while handling the cookie. - {e.Message}");
                return Task.FromResult(false);
            }

        }

        private async Task<bool> WaitForVideo(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return await Task.FromResult(true);

            try
            {
                Label("Waiting for stream window", label);
                Console($"User: {username} - Match: {matchName} - Waiting for stream window");

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

                // check if javascript should be used to find the video player element
                if (javascriptBox.Checked)
                {
                    wait.Until((browser) => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('#video-player');"));
                }
                else
                {
                    // use Selenium's hardcoded actions to find the video player element
                    wait.Until(e => e.FindElement(By.CssSelector("#video-player")));
                }

                Console($"User: {username} - Match: {matchName} - Stream video player found");
                Label("Stream window found", label);

                return await Task.FromResult(true);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - An error occurred while waiting for the video. - Error: {e.Message}");
                return await Task.FromResult(false);
            }
        }


        private Task<string> TwitchOrYoutube(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return Task.FromResult("skip");

            try
            {
                Label("Checking if YT or TW", label);
                Console($"User: {username} - Match: {matchName} - Checking if Stream is on YouTube or Twitch");

                // Check if javascriptBox is checked
                if (javascriptBox.Checked)
                {
                    // Use JavaScript to find elements
                    var yt = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('video-player-youtube') !== null;");
                    var tw = (bool)((IJavaScriptExecutor)driver).ExecuteScript("return document.getElementById('video-player-twitch') !== null;");

                    if (yt)
                    {
                        Console($"User: {username} - Match: {matchName} - Stream is on YouTube");
                        Label("Stream on YT", label);
                        return Task.FromResult("yt");
                    }
                    else if (tw)
                    {
                        Console($"User: {username} - Match: {matchName} - Stream is on Twitch");
                        Label("Stream on TW", label);
                        return Task.FromResult("tw");
                    }
                }
                else
                {
                    // Use Selenium's hardcoded actions to find elements
                    var yt = driver.FindElements(By.Id("video-player-youtube")).Count > 0;
                    var tw = driver.FindElements(By.Id("video-player-twitch")).Count > 0;

                    if (yt)
                    {
                        Console($"User: {username} - Match: {matchName} - Stream is on YouTube");
                        Label("Stream on YT", label);
                        return Task.FromResult("yt");
                    }
                    else if (tw)
                    {
                        Console($"User: {username} - Match: {matchName} - Stream is on Twitch");
                        Label("Stream on TW", label);
                        return Task.FromResult("tw");
                    }
                }
            }
            catch (Exception ex)
            {
                Console($"User: {username} - Match: {matchName} - An error occurred while checking if stream is on Twitch or YouTube. - Error: {ex.Message}");
                return Task.FromResult("err");
            }

            return Task.FromResult("err");
        }


        private async Task<bool> HandleTwitch(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return true;

            try
            {
                Label("Handling Twitch stream", label);
                Console($"User: {username} - Match: {matchName} - Handling Twitch stream");

                driver.Manage().Window.Maximize();

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

                // Check if javascriptBox.Checked is true, if yes use javascript, otherwise use selenium's hardcoded actions
                if (javascriptBox.Checked)
                {
                    // Use javascript to find the Twitch iframe and switch to it
                    wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('iframe[title=\"Twitch\"]');"));
                    driver.SwitchTo().Frame(((IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('iframe[title=\"Twitch\"]');")));

                    // Use javascript to click on the player settings button
                    wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('button[data-a-target=\"player-settings-button\"]');"));
                    ((IJavaScriptExecutor)driver).ExecuteScript(("document.querySelector('button[data-a-target=\"player-settings-button\"]').click();"));

                    // Use javascript to click on the quality menu item
                    wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('button[data-a-target=\"player-settings-menu-item-quality\"]');"));
                    ((IJavaScriptExecutor)driver).ExecuteScript("document.querySelector('button[data-a-target=\"player-settings-menu-item-quality\"]').click();");

                    // Use javascript to select 160p video quality
                    ((IJavaScriptExecutor)driver).ExecuteScript("var elements = Array.from(document.querySelectorAll('div[data-a-target=\"player-settings-submenu-quality-option\"]')); var element = elements.find(e => e.textContent.includes('160p')); element.click();");

                    // Use javascript to mute the video if it's not muted
                    wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('button[data-a-target=\"player-mute-unmute-button\"]');"));
                    var muteUnmuteButton = ((IWebElement)((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('button[data-a-target=\"player-mute-unmute-button\"]');"));
                    var ariaLabel = (string)((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].getAttribute('aria-label');", muteUnmuteButton);

                    if (ariaLabel == "Unmute (m)")
                    {
                        muteUnmuteButton.Click();
                    }

                    driver.SwitchTo().DefaultContent();
                }
                else
                {
                    // switch to Twitch iframe
                    wait.Until(browser => driver.FindElement(By.CssSelector("iframe[title='Twitch']")));
                    driver.SwitchTo().Frame(driver.FindElement(By.CssSelector("iframe[title='Twitch']")));

                    // click player settings button
                    wait.Until(browser => driver.FindElement(By.CssSelector("button[data-a-target='player-settings-button']")));
                    driver.FindElement(By.CssSelector("button[data-a-target='player-settings-button']")).Click();

                    // click quality settings menu item
                    wait.Until(browser => driver.FindElement(By.CssSelector("button[data-a-target='player-settings-menu-item-quality']")));
                    driver.FindElement(By.CssSelector("button[data-a-target='player-settings-menu-item-quality']")).Click();

                    // click 160p quality option
                    wait.Until(browser => driver.FindElements(By.CssSelector("div[data-a-target='player-settings-submenu-quality-option']")));
                    var qualityOptions = driver.FindElements(By.CssSelector("div[data-a-target='player-settings-submenu-quality-option']"));
                    var quality160P = qualityOptions.FirstOrDefault(o => o.Text.Contains("160p"));
                    quality160P?.Click();

                    // wait for a second
                    await Task.Delay(1000, token);

                    // mute player if it's not already muted
                    wait.Until(browser => driver.FindElement(By.CssSelector("button[data-a-target='player-mute-unmute-button']")));
                    var muteUnmuteButton = driver.FindElement(By.CssSelector("button[data-a-target='player-mute-unmute-button']"));
                    var ariaLabel = muteUnmuteButton.GetAttribute("aria-label");
                    if (ariaLabel == "Unmute (m)")
                    {
                        muteUnmuteButton.Click();
                    }

                    // switch back to default content
                    driver.SwitchTo().DefaultContent();
                }

                Console($"User: {username} - Match: {matchName} - Handled twitch stream");
                Label("Handled Twitch stream", label);

                return true;

            }
            catch (Exception e)
            {
                try
                {
                    driver.SwitchTo().DefaultContent();

                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

                    if (javascriptBox.Checked)
                    {
                        wait.Until(browser => ((IJavaScriptExecutor)driver).ExecuteScript("return document.querySelector('.offline-embeds');"));
                    }
                    else
                    {
                        wait.Until(ExpectedConditions.ElementIsVisible(By.ClassName("offline-embeds")));
                    }

                    Console($"User: {username} - Match: {matchName} - Match is over. - Error: {e.Message}");
                    driver.Quit();
                    return true;
                }
                catch
                {
                    driver.SwitchTo().DefaultContent();
                    Console($"User: {username} - Match: {matchName} - An error occurred while handeling the twitch stream. - Error: {e.Message}");
                    return false;
                }
            }
        }

        private async Task<bool> HandleYoutube(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return true;

            try
            {
                Label("Handling YouTube stream", label);
                Console($"User: {username} - Match: {matchName} - Handling YouTube stream");

                driver.Manage().Window.Maximize();

                // Wait for the youtube video iframe to load
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
                wait.Until(browser => driver.FindElement(By.Id("video-player-youtube")));

                if (javascriptBox.Checked) // if the javascript checkbox is checked, use JavaScript to interact with the elements
                {
                    // Click on the RewardsStatusInformer button to remove it from the screen
                    driver.FindElement(By.CssSelector(".RewardsStatusInformer")).Click();

                    // Play the video using JavaScript
                    ((IJavaScriptExecutor)driver).ExecuteScript("var iframe = document.getElementById(\"video-player-youtube\");\r\nvar script = \"arguments[0].contentWindow.postMessage(JSON.stringify({event:'command',func:'playVideo',args:''}), '*');\";\r\niframe.contentWindow.postMessage(JSON.stringify({event:'command',func:'playVideo',args:''}), '*');\r\n");

                }
                else // if the javascript checkbox is not checked, use Selenium's built-in functions to interact with the elements
                {
                    // Find the RewardsStatusInformer button and click it to remove it from the screen
                    IWebElement rewardsStatusInformer = wait.Until(ExpectedConditions.ElementToBeClickable(By.CssSelector(".RewardsStatusInformer")));
                    rewardsStatusInformer.Click();

                    // Find the youtube video player iframe
                    IWebElement youtubePlayerIframe = wait.Until(ExpectedConditions.ElementIsVisible(By.Id("video-player-youtube")));

                    // Switch to the youtube video player iframe
                    driver.SwitchTo().Frame(youtubePlayerIframe);
                   
                    // Find the play button
                    IWebElement playButton = driver.FindElement(By.CssSelector("button.ytp-play-button"));

                    // Check if the data-title-no-tooltip attribute says "Play"
                    if (playButton.GetAttribute("data-title-no-tooltip") == "Play")
                    {
                        // Click the button element
                        playButton.Click();
                    }

                    driver.SwitchTo().DefaultContent();

                }

                Label("Handled YouTube stream", label);
                return true;
            }
            catch (Exception e)
            {
                driver.SwitchTo().DefaultContent();
                Console($"User: {username} - Match: {matchName} - An error occurred while handling the YouTube stream. - Error: {e.Message}");
                return false;
            }
        }


        private Task RemoveEntirePage(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username, UserControl label)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return Task.CompletedTask;

            try
            {
                // Execute JavaScript to clear the content of the HTML document
                var js = (IJavaScriptExecutor)driver;
                js.ExecuteScript("document.documentElement.innerHTML = '';");

                Console($"User: {username} - Match: {matchName} - Removed webpage content");
                Label("Removed webpage content", label);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - An error occurred while removing the webpage content. - Error: {e.Message}");
            }

            return Task.CompletedTask;
        }


        private bool WaitForConsole(UndetectedChromeDriver driver, string consolemessage, string matchName, CancellationToken token, string username, int time)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return false;

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(time));
                wait.Until(d =>
                {
                    var logs = driver.Manage().Logs.GetLog("browser").Where(log => log.Message.Contains(consolemessage)).ToList();
                    return logs.LastOrDefault()?.Message.Contains(consolemessage) ?? false;
                });
                return true;
            }
            catch (TimeoutException ex)
            {
                Console($"User: {username} - Match: {matchName} - Timeout waiting for console message: {consolemessage}. - Error: {ex.Message}");
                return false;
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - Timeout waiting for console message: {consolemessage}. - Error: {e.Message}");
                return false;
            }
        }

        private Task<bool> IsEligible(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return Task.FromResult(true);

            try
            {

                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(180));
                wait.Until(d =>
                {
                    var logs = driver.Manage().Logs.GetLog("browser").Where(log => log.Message.Contains("RewardsStatusInformer")).ToList();
                    return logs.LastOrDefault()?.Message.Contains("heartbeater=heartbeating") == true || logs.LastOrDefault()?.Message.Contains("heartbeater=waiting") == true;
                });

                return Task.FromResult(true);

            }
            catch (TimeoutException ex)
            {
                Console($"User: {username} - Match: {matchName} - Timeout waiting for heartbeat. - Error: {ex.Message}");
                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - Timeout waiting for heartbeat. - Error: {e.Message}");
                return Task.FromResult(false);
            }
        }

        private Tuple<bool, string, string> NeedSessionRefresh(UndetectedChromeDriver driver, string username, string matchName)
        {
            try
            {
                if (driver.Manage().Cookies.AllCookies.All(x => x.Name != "access_token"))
                {
                    driver.Quit();
                    throw new Exception($"User: {username} - Match: {matchName} - Error Access_token not found");
                }

                var accessToken = driver.Manage().Cookies.GetCookieNamed("access_token").Value;
                var esrnasid = driver.Manage().Cookies.GetCookieNamed("esrna.sid").Value;
                var jwtHandler = new JwtSecurityTokenHandler();
                var jwtToken = jwtHandler.ReadJwtToken(accessToken);

                var timeLeft = jwtToken.ValidTo.Subtract(DateTime.Now.ToUniversalTime()).TotalSeconds;
                if (timeLeft < 600)
                {
                    return Tuple.Create(true, accessToken, esrnasid);
                }

                return Tuple.Create(false, accessToken, esrnasid);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - Error getting session. - Error: {e.Message}");
            }

            return Tuple.Create(false, "", "");
        }

        private async Task<string> RefreshSession(UndetectedChromeDriver driver, string cookie, string username, string matchName)
        {
            var accessToken = "";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://account.rewards.lolesports.com/v1/session/refresh");
                request.Method = "GET";
                request.Headers.Add("Origin", "https://lolesports.com");
                request.Headers.Add("Authorization", $"Cookie access_token");
                request.Headers.Add("cookie", $"{cookie}");

                using (var response = await request.GetResponseAsync())
                {
                }

                if (driver.Manage().Cookies.AllCookies.All(x => x.Name != "access_token"))
                {
                    driver.Quit();
                    throw new Exception("Access token cookie not found.");
                }

                accessToken = driver.Manage().Cookies.GetCookieNamed("access_token").Value;


            }
            catch (Exception ex)
            {
                Console($"User: {username} - Match: {matchName} - Error refreshing session. - Error: {ex.Message}");
            }

            return accessToken;
        }


        private async Task<string> MaintainSession(UndetectedChromeDriver driver, string username, string matchName)
        {
            var refresh = NeedSessionRefresh(driver, username, matchName);

            var token = refresh.Item2;

            if (refresh.Item1)
            {
                token = await RefreshSession(driver, "access_token=" + refresh.Item2 + "; esrna.sid=" + refresh.Item3, username, matchName);
            }

            return token;
        }

        private async Task<int> CheckNewDrops(UndetectedChromeDriver driver, string username, string matchName, CancellationToken token)
        {

            if (_doCancel || token.IsCancellationRequested || _isSleeping) return 99999;

            var count = -1;

            var cookie = await MaintainSession(driver, username, matchName);

            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://account.service.lolesports.com/fandom-account/v1/earnedDrops?locale=en_GB&site=LOLESPORTS");
                request.Method = "GET";
                request.Headers.Add("Origin", "https://lolesports.com");
                request.Headers.Add("Authorization", $"Cookie access_token");
                request.Headers.Add("cookie", $"access_token={cookie}");

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var jsonResponse = await reader.ReadToEndAsync();
                    var resJson = JsonConvert.DeserializeObject<List<object>>(jsonResponse);
                    count = resJson.Count();
                }
            }
            catch (WebException ex)
            {
                Console($"User: {username} - Match: {matchName} - Error getting Drops count. - Error: {ex.Message}");
            }

            return count;
        }


        private Task<bool> SwitchLanguage(UndetectedChromeDriver driver, string matchName, CancellationToken token, string username)
        {
            if (_doCancel || token.IsCancellationRequested || _isSleeping) return Task.FromResult(true);

            try
            {

                // Find the language switcher element
                IWebElement languageSwitcher = driver.FindElement(By.CssSelector("div.locale-switcher-icon a"));

                // Click the language switcher
                languageSwitcher.Click();

                IWebElement element = driver.FindElement(By.CssSelector("[data-testid='riotbar:localeswitcher:link-en-GB']"));

                // Click on the element
                element.Click();

                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                Console($"User: {username} - Match: {matchName} - Failure switching Laguge. - Error: {e.Message}");
                return Task.FromResult(false);
            }
        }


        private async void DoStuff(string matchUrl, string matchName, CancellationToken token, string username, string password, UserControl eligibleLabel, UserControl dropLabel)
        {
            
            RunningDrivers runningDriver = null;
            var failcounter = 0;
            var startednext = false;
            var dropsNow = 0;
            var rejected = false;

            try
            {
                using (var driver = await StartDriver())
                {
                    driver.Manage().Window.Maximize();

                    if (_driverList.All(driverx => driverx.Driver != driver))
                    {
                        runningDriver = new RunningDrivers { Driver = driver, Match = matchName, Username = username };
                        _driverList.Add(runningDriver);
                    }

                    if (!await Login(driver, matchName, token, username, password, eligibleLabel))
                    {
                        goto skipToEnd;
                    }

                    await Task.Delay(10000, token);

                    RefreshPage:
                    failcounter++;

                    if (!startednext && failcounter > 3)
                    {
                        _startNext = true;
                        startednext = true;
                    }

                    if (failcounter > 15)
                    {
                        goto skipToEnd;
                    }

                    var existingEntry = _dropList.FirstOrDefault(d => d.User == username);
                    var dropsAtStart = 0;
                    if (existingEntry != null)
                    {
                        dropsAtStart = existingEntry.Dropcount;
                    }
                    else
                    {
                        dropsAtStart = await CheckNewDrops(driver, username, matchName, token);
                        
                        switch (dropsAtStart)
                        {
                            case -1:
                                goto RefreshPage;
                            case 9999:
                                goto skipToEnd;
                        }

                        _dropList.Add(new Drops { User = username, Dropcount = dropsAtStart });
                    }


                    if (!await SwitchLanguage(driver, matchName, token, username))
                        goto RefreshPage;

                    driver.Navigate().GoToUrl(matchUrl);

                    if (!rejected || javascriptBox.Checked)
                    {
                        if (!await RejectCookieMessage(driver, matchName, token, username, eligibleLabel))
                            goto RefreshPage;
                    }

                    rejected = true;

                    if (!await WaitForVideo(driver, matchName, token, username, eligibleLabel))
                        goto RefreshPage;

                    var toY = await TwitchOrYoutube(driver, matchName, token, username, eligibleLabel);

                    if (toY == "err")
                    {
                        goto RefreshPage;
                    }

                    if (toY == "tw")
                    {
                        if (!await HandleTwitch(driver, matchName, token, username, eligibleLabel))
                            goto RefreshPage;
                    }
                    else if (toY == "yt")
                    {
                        if (!await HandleYoutube(driver, matchName, token, username, eligibleLabel))
                            goto RefreshPage;
                    }
                    else if (toY == "skip")
                        goto skipToEnd;

                    if(!WaitForConsole(driver, "heartbeater", matchName, token, username, 60))
                        goto RefreshPage;

                    if (_removeContent)
                        await RemoveEntirePage(driver, matchName, token, username, eligibleLabel);

                    if (!startednext)
                    {
                        _startNext = true;
                        startednext = true;
                    }

                    var refreshcount = 10;
                    while (!_doCancel && !token.IsCancellationRequested && !_isSleeping)
                    {

                        eligibleLabel.Controls[1].Text = "Status: Watching Stream";

                        var eligible = await IsEligible(driver, matchName, token, username);

                        if (eligible)
                        {
                            Console($"User: {username} - Match: {matchName} - Rewards eligible: heartbeat active");
                            eligibleLabel.Controls[1].Text = "Drops: Eligible - " + DateTime.Now;
                            eligibleLabel.Controls[1].ForeColor = Color.Lime;
                        }
                        else
                        {
                            Console($"User: {username} - Match: {matchName} - Rewards eligible: heartbeat not found");
                            failcounter++;
                            if (failcounter > 3)
                            {
                                eligibleLabel.Controls[1].Text = "Drops: Error (Retrying) - " + DateTime.Now;
                                eligibleLabel.Controls[1].ForeColor = Color.Red;
                                failcounter = 0;
                                goto RefreshPage;
                            }
                        }

                        if (refreshcount > 5)
                        {
                            refreshcount = 0;
                            dropsNow = await CheckNewDrops(driver, username, matchName, token);
                        }

                        refreshcount++;

                        dropLabel.Controls[1].Text = "Drops Total: " + dropsAtStart;
                        dropLabel.Controls[2].Text = "Drops Session: " + (dropsNow - dropsAtStart);

                        await Task.Delay(60000, token);
                    }

                    skipToEnd:

                    if (!startednext)
                    {
                        _startNext = true;
                        startednext = true;
                    }

                    if (runningDriver != null)
                    {
                        _driverList.Remove(runningDriver);
                    }

                    driver.Quit();
                }
            }
            catch(Exception ex)
            {
                Console($"User: {username} - Match: {matchName} - Stream cancelled. - Error: {ex.Message}");

                if (!startednext)
                {
                    _startNext = true;
                }

                if (runningDriver != null)
                {
                    _driverList.Remove(runningDriver);
                }
            }

        }


        private void Start()
        {
            while (!_doCancel && !_isSleeping)
            {
                try
                {
                    _lastYPosition2 = 0;
                   var firstloop = true;
                   
                   if (_ownBrowser)
                   {
                        //coming soon
                   }
                   else
                   {
                       foreach (var user in _userList)
                       {
                           if (firstloop)
                           {
                               firstloop = false;
                           }
                           else
                           {
                               AddNewSeperatorFields(user.Username);
                           }

                           var userControl = AddNewUserFields(user.Username);

                           foreach (var game in _activeStreams.ToList())
                           {
                               var eligibleControl = AddNewMatchFields(game, user.Username);

                               if (_runningThreads.Any(match => match.MatchName == game.StreamName && match.Username == user.Username))
                               {
                                   continue; // Match already running, skip it
                               }

                               _startNext = true;

                               var cancellationTokenSource = new CancellationTokenSource();
                               var thread = new Thread(() => DoStuff(game.Url, game.StreamName,
                                   cancellationTokenSource.Token, user.Username, user.Password, eligibleControl,
                                   userControl));
                               _runningThreads.Add(new RunningThreads
                               {
                                   MatchName = game.StreamName, Thread = thread, TokenSource = cancellationTokenSource,
                                   Username = user.Username
                               });

                               Thread.Sleep(1000);
                           }
                       }

                       foreach (var thread in _runningThreads.ToList().Where(thread => thread.Thread.ThreadState == ThreadState.Unstarted))
                       {
                           while (!_startNext)
                           {
                               Thread.Sleep(1000);

                               if (_doCancel)
                                   return;
                           }

                           _startNext = false;

                           Console($"User: {thread.Username} - Match: {thread.MatchName} - Stream starting in new tab");
                           thread.Thread.Start();
                           Thread.Sleep(5000);
                       }
                   }
                }
                catch
                {
                }

                Thread.Sleep(5000);

            }
        }


        private async void RunTask()
        {

            while (!_doCancel)
            {
                var shouldStopStream = false;

                if (timeFrameBox.Checked)
                {
                    // Get the timeframes from a textbox
                    var timeframesText = timeFrameTextBox.Text;
                    var timeframes = timeframesText.Split(',');

                    // Check if the current time is within any of the timeframes
                    foreach (var timeframe in timeframes)
                    {
                        var hours = timeframe.Split('-');

                        if (hours.Length != 2)
                        {
                            MessageBox.Show("Invalid time format. Please use the format '2-4' or '2:30-4:30'.");
                            return;
                        }

                        var startParts = hours[0].Trim().Split(':');
                        var endParts = hours[1].Trim().Split(':');

                        if (!int.TryParse(startParts[0], out var startHour) || !int.TryParse(endParts[0], out var endHour))
                        {
                            MessageBox.Show("Invalid time format. Please use numbers only.");
                            return;
                        }

                        var startMinute = startParts.Length == 2 ? int.Parse(startParts[1]) : 0;
                        var endMinute = endParts.Length == 2 ? int.Parse(endParts[1]) : 0;

                        if (startHour < 0 || startHour > 23 || endHour < 0 || endHour > 23 || startMinute < 0 || startMinute > 59 || endMinute < 0 || endMinute > 59)
                        {
                            MessageBox.Show("Invalid time format. Please enter valid hours and minutes.");
                            return;
                        }

                        var startTime = new TimeSpan(startHour, startMinute, 0);
                        var endTime = new TimeSpan(endHour, endMinute, 0);

                        if (endTime < startTime) // if end time is less than start time, add 24 hours to end time
                        {
                            endTime = endTime.Add(new TimeSpan(24, 0, 0));
                        }

                        if (DateTime.Now.TimeOfDay >= startTime && DateTime.Now.TimeOfDay < endTime)
                        {
                            var sleepTime = startTime - DateTime.Now.TimeOfDay + (endTime - startTime);
                            var sleepTimeMinutes = (int)sleepTime.TotalMinutes;
                            var sleepTimeHours = (int)sleepTime.TotalHours;

                            var sleepTimeMsg = "";
                            if (sleepTimeHours > 0)
                            {
                                sleepTimeMsg += $"{sleepTimeHours} hour(s) ";
                            }
                            if (sleepTimeMinutes > 0)
                            {
                                sleepTimeMsg += $"{sleepTimeMinutes % 60} minute(s)";
                            }

                            LastCheck.Text = $"Sleeping for {sleepTimeMsg} within the sleep timeframe";
                            shouldStopStream = true;
                            break;
                        }
                    }

                }

                // Stop the stream if necessary
                if (shouldStopStream)
                {
                    _isRunning = false;
                    _isSleeping = true;
                    Console("Hours off stream found");
                    Console("BOT WILL SLEEP NOW");
                    Console("Stopping all Streams");
                }
                else
                {

                    _isSleeping = false;

                    await Task.Run(CheckLiveGames);
                    await Task.Run(CloseFinishedMatches);

                    if (!_isRunning)
                    {
                        _isRunning = true;
                        Task.Run(Start);
                    }
                }

                await Task.Delay(30000);
            }
        }

        private void CloseFinishedMatches()
        {
            var threadsToRemove = new List<RunningThreads>();
            var controlsToRemove = new List<Control>();

            foreach (var thread in _runningThreads.Where(thread => _activeStreams.All(stream => stream.StreamName != thread.MatchName)))
            {
                Console($"User: {thread.Username} - Match: {thread.MatchName} - Stream finished... closing tab...");
                thread.TokenSource.Cancel();
                threadsToRemove.Add(thread);

                // Find the UserControl with the matching name and username
                var control = panel1.Controls.OfType<UserControl>().FirstOrDefault(c =>
                    c.Controls.OfType<Label>().Any(l => l.Text.Contains("Match: " + thread.MatchName) && c.Name.Contains(thread.MatchName+thread.Username)));

                if (control != null)
                {
                    controlsToRemove.Add(control);
                }
            }

            foreach (var control in controlsToRemove)
            {
                panel1.Controls.Remove(control);
            }

            foreach (var thread in threadsToRemove)
            {
                //thread.Thread.Join();
                _runningThreads.Remove(thread);
            }

        }

        private void Stop()
        {

            var threadsToRemove = new List<RunningThreads>();

            foreach (var thread in _runningThreads)
            {
                Console($"User: {thread.Username} - Match: {thread.MatchName} - Closing tab...");
                thread.TokenSource.Cancel();
                threadsToRemove.Add(thread);
            }

            var controlsToRemove = panel1.Controls.Cast<UserControl>().Cast<Control>().ToList();

            foreach (var control in controlsToRemove)
            {
                panel1.Controls.Remove(control);
            }

            var driversToRemove = _driverList.ToList();

            foreach (var driver in driversToRemove)
            {
                driver.Driver.Quit();
                _driverList.Remove(driver);
            }

            foreach (var thread in threadsToRemove)
            {
                //thread.Thread.Join();
                _runningThreads.Remove(thread);
            }
        }


        private bool SafeUsers()
        {
            var isEmpty = true;

            var users = new List<User>();

            foreach (Control control in containerPanel.Controls)
            {
                if (control is UserControl userControl)
                {
                    var usernameTextBox = userControl.Controls.OfType<BunifuTextBox>().FirstOrDefault(c => c.Name == "usernameTextBox");
                    var passwordTextBox = userControl.Controls.OfType<BunifuTextBox>().FirstOrDefault(c => c.Name == "passwordTextBox");

                    if (usernameTextBox != null && passwordTextBox != null && !string.IsNullOrEmpty(usernameTextBox.Text) && !string.IsNullOrEmpty(passwordTextBox.Text))
                    {
                        isEmpty = false;

                        // Check if user already exists in the list
                        if (_userList.All(u => u.Username != usernameTextBox.Text))
                        {
                            // Add user to the list
                            _userList.Add(new User { Username = usernameTextBox.Text, Password = passwordTextBox.Text });
                            users.Add(new User { Username = usernameTextBox.Text, Password = passwordTextBox.Text });
                            Console($"Added user: {usernameTextBox.Text}");
                        }
                    }
                }
            }

            // Serialize and save user data to a JSON file
            string json = JsonConvert.SerializeObject(users);
            File.WriteAllText("users.json", json);

            return isEmpty;
        }

        private UserControl DoesControlExist(string name)
        {
            foreach (Control control in panel1.Controls)
            {
                if (control is UserControl userControl && userControl.Name == name)
                {
                    return userControl;
                }
            }

            return null;
        }

        private void AddNewSeperatorFields(string user)
        {
            var uctrl = DoesControlExist("seperator" + user);

            if (uctrl != null)
            {
                uctrl.Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 10);
                _lastYPosition2 += 20;
            }
            else
            {

                // Create a new user control that contains a label and a panel
                var seperatorControl = new UserControl
                {
                    Name = "seperator" + user,
                    Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 10),
                    Size = new Size(panel1.Width - 40, 1)
                };

                var seperator = new BunifuSeparator
                {
                    BackColor = Color.Transparent,
                    DashCap = BunifuSeparator.CapStyles.Flat,
                    LineColor = Color.Gray,
                    LineStyle = BunifuSeparator.LineStyles.Solid,
                    LineThickness = 1,
                    Location = new Point(0, 0),
                    Orientation = BunifuSeparator.LineOrientation.Horizontal,
                    Size = new Size(seperatorControl.Size.Width, 1),
                    TabIndex = 0,
                };
                seperatorControl.Controls.Add(seperator);

                try
                {
                    panel1.Invoke(new Action(() =>
                    {
                        panel1.Controls.Add(seperatorControl);
                        _lastYPosition2 += 20;
                    }));
                }
                catch (InvalidOperationException ex)
                {
                    // Cross-thread operation not valid, handle exception
                    Console($"Error adding new match: {ex.Message}");
                }
            }
        }


        private UserControl AddNewUserFields(string user)
        {

            var uctrl = DoesControlExist("user" + user);

            if (uctrl != null)
            {
                uctrl.Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 5);
                _lastYPosition2 += 20;
                return uctrl;
            }
            else
            {

                // Create a new user control that contains a label and a panel
                var userControl = new UserControl
                {
                    Name = "user" + user,
                    Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 5),
                    Size = new Size(panel1.Width - 40, 15)
                };

                // Add a label for the match name
                var userLabel = new Label
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Text = "User: " + user,
                    Location = new Point(0, 0),
                    AutoSize = true,
                    BorderStyle = BorderStyle.FixedSingle
                };
                userControl.Controls.Add(userLabel);

                // Add a label for the match name
                var dropTotalLabel = new Label
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Text = "Drops Total: 0",
                    Location = new Point(userLabel.Size.Width + 15, 0),
                    AutoSize = true,
                    BorderStyle = BorderStyle.FixedSingle
                };
                userControl.Controls.Add(dropTotalLabel);

                // Add a label for the match name
                var dropSessionLabel = new Label
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Text = "Drops Session: 0",
                    Location = new Point(dropTotalLabel.Location.X + dropTotalLabel.Size.Width + 15, 0),
                    AutoSize = true,
                    BorderStyle = BorderStyle.FixedSingle
                };
                userControl.Controls.Add(dropSessionLabel);

                try
                {
                    panel1.Invoke(new Action(() =>
                    {
                        panel1.Controls.Add(userControl);
                        _lastYPosition2 += 20;
                    }));
                }
                catch (InvalidOperationException ex)
                {
                    // Cross-thread operation not valid, handle exception
                    Console($"Error adding new match: {ex.Message}");
                }

                return userControl;
            }

        }

        private UserControl AddNewMatchFields(ActiveStreams game, string user)
        {

            var uctrl = DoesControlExist(game.StreamName + user);

            if (uctrl != null)
            {
                uctrl.Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 5);
                _lastYPosition2 += 20;
                return uctrl;
            }
            else
            {
                // Create a new user control that contains a label and a panel
                var matchControl = new UserControl
                {
                    Name = game.StreamName + user,
                    Location = new Point(5, _lastYPosition2 - panel1.VerticalScroll.Value + 5),
                    Size = new Size(panel1.Width - 40, 15)
                };

                // Add a label for the match name
                var matchLabel = new Label
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Text = "Match: " + game.StreamName,
                    Location = new Point(0, 0),
                    AutoSize = true,
                    BorderStyle = BorderStyle.FixedSingle
                };
                matchControl.Controls.Add(matchLabel);

                // Add a panel for the label color
                var eligibleLabel = new Label
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Text = "Status: Waiting..",
                    Location = new Point(matchLabel.Size.Width + 15, 0),
                    AutoSize = true,
                    BorderStyle = BorderStyle.FixedSingle
                };
                matchControl.Controls.Add(eligibleLabel);


                try
                {
                    panel1.Invoke(new Action(() =>
                    {
                        panel1.Controls.Add(matchControl);
                        _lastYPosition2 += 20;
                    }));
                }
                catch (InvalidOperationException ex)
                {
                    // Cross-thread operation not valid, handle exception
                    Console($"Error adding new match: {ex.Message}");
                }

                return matchControl;
            }
        }

        private void AddNewUserFields(string username = "", string password = "")
        {
            // Create a new user control with the given username and password
            var userControl1 = new UserControl
            {
                Location = new Point(5, _lastYPosition - containerPanel.VerticalScroll.Value + 5),
                Size = new Size(containerPanel.Width - 40, 20)
            };

            // Create username TextBox
            var userBox = new BunifuTextBox
            {
                BackColor = Color.Transparent,
                BorderColorActive = Color.Lime,
                BorderColorHover = Color.Aqua,
                BorderColorIdle = Color.Silver,
                BorderRadius = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BorderThickness = 1,
                DefaultFont = new Font("Consolas", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
                FillColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.Silver,
                IconPadding = 10,
                Padding = new Padding(3),
                PlaceholderForeColor = Color.Silver,
                PlaceholderText = "Username",
                ReadOnly = false,
                Style = BunifuTextBox._Style.Bunifu,
                TextAlign = HorizontalAlignment.Left,
                TextMarginBottom = 0,
                TextMarginLeft = 3,
                TextMarginTop = 1,
                TextPlaceholder = "Username",
                UseSystemPasswordChar = false,
                WordWrap = true,
                Name = "usernameTextBox",
                Location = new Point(0, 0),
                Size = new Size((userControl1.Width / 2) - 20, userControl1.Height),
                Text = username // Set the username
            };
            userControl1.Controls.Add(userBox);

            // Create password TextBox
            var passwordBox = new BunifuTextBox
            {
                BackColor = Color.Transparent,
                BorderColorActive = Color.Lime,
                BorderColorHover = Color.Aqua,
                BorderColorIdle = Color.Silver,
                BorderRadius = 1,
                BorderStyle = BorderStyle.FixedSingle,
                BorderThickness = 1,
                DefaultFont = new Font("Consolas", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
                FillColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.Silver,
                IconPadding = 10,
                Padding = new Padding(3),
                PlaceholderForeColor = Color.Silver,
                PlaceholderText = "Password",
                ReadOnly = false,
                Style = BunifuTextBox._Style.Bunifu,
                TextAlign = HorizontalAlignment.Left,
                TextMarginBottom = 0,
                TextMarginLeft = 3,
                TextMarginTop = 1,
                TextPlaceholder = "Password",
                PasswordChar = '*',
                UseSystemPasswordChar = false,
                WordWrap = true,
                Name = "passwordTextBox",
                Location = new Point((userControl1.Width / 2) - 15, 0),
                Size = new Size((userControl1.Width / 2) - 20, userControl1.Height),
                Text = password // Set the password
            };
            userControl1.Controls.Add(passwordBox);

            var removeButton = new BunifuButton2
            {
                AllowAnimations = true,
                AllowMouseEffects = true,
                BackColor = Color.Transparent,
                BackColor1 = Color.FromArgb(80, 80, 80),
                BorderStyle = BunifuButton2.BorderStyles.Solid,
                ButtonText = "X",
                ButtonTextMarginLeft = 0,
                ColorContrastOnClick = 45,
                ColorContrastOnHover = 45,
                Cursor = Cursors.Default,
                Font = new Font("Consolas", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
                ForeColor = Color.Red,
                IdleBorderColor = Color.Red,
                IdleBorderRadius = 1,
                IdleBorderThickness = 1,
                IdleFillColor = Color.FromArgb(80, 80, 80),
                IdleIconLeftImage = null,
                IdleIconRightImage = null,
                TextAlign = ContentAlignment.MiddleCenter,
                TextAlignment = HorizontalAlignment.Center,
                Text = "X",
                Location = new Point(userControl1.Width - 30, 0),
                Size = new Size(25, userControl1.Height)
            };

            removeButton.Click += (sender, e) =>
            {
                containerPanel.Controls.Remove(userControl1);
                _userList.Remove(_userList.Find(u => u.Username == userBox.Text));
                _lastYPosition -= 25; // update lastYPosition
                AdjustUserControlPositions(); // adjust positions of remaining user controls
            };
            userControl1.Controls.Add(removeButton);

            containerPanel.Controls.Add(userControl1);
            _lastYPosition += 25;
        }

       
        private void AdjustUserControlPositions()
        {
            var yPosition = 0;
            foreach (UserControl userControl in containerPanel.Controls)
            {
                userControl.Location = new Point(5, yPosition + 5);
                yPosition += 25;
            }
            _lastYPosition = yPosition;
        }


        private int _driverselect;
        private async void ImageLoop()
        {
            try
            {
                while (true)
                {
                    while (_makeImage)
                    {
                        if (_driverList.Count > 0)
                        {
                            var driver = _driverList[_driverselect];
                            await Make_image(driver.Driver);
                            loaderIcon.Visible = false;

                            labelBrowser.Invoke(new Action(() => labelBrowser.Text = $"Account: {driver.Username} - Match: {driver.Match} - {_driverselect + 1}/{_driverList.Count}"));
                        }

                        await Task.Delay(1000);
                    }

                    await Task.Delay(1000);
                }
            }
            catch
            {
            }
        }

        //Die Start methode habe ich geändert zu tasks aber das ist maybe unnötig. ebenfalls funktioniert das starten eines neuen games nicht warum auch immer, wahrscheinlich weils von einem anderen thread aus gestartet wird?

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (SafeUsers())
            {
                MessageBox.Show("IT LOOKS LIKE YOU ADDED AN EMPTY USERNAME OR PASSWORD. CLICK ON 'ACCOUNTS' AND MAKE SURE THAT YOU ENTERED EVERYTHING CORRECTLY BEFORE STARTING THE BOT!");
            }
            else
            {
                _ownBrowser = ownBrowserCheckbox.Checked;
                _headless = headlessBox.Checked;
                _removeContent = removeContentBox.Checked;
                _startNext = true;
                _doCancel = false;
                stopButton.Enabled = true;
                startButton.Enabled = false;
                RunTask();
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            labelBrowser.Text = "WEB BROWSER VIEW: START THE BOT FIRST";
            _isRunning = false;
            _doCancel = true;
            startButton.Enabled = true;
            stopButton.Enabled = false;
            Task.Run(Stop); //This can cause issues!
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CheckForIllegalCrossThreadCalls = false;

            if (File.Exists("users.json"))
            {
                string json = File.ReadAllText("users.json");
                _userList = JsonConvert.DeserializeObject<List<User>>(json);

                // Populate the user interface with loaded user data
                foreach (var user in _userList)
                {
                    AddNewUserFields(user.Username, user.Password);
                    startButton.Enabled = true;
                }
            }
        }

        private const int MaxLines = 200;
        private void ConsoleBox_TextChanged(object sender, EventArgs e)
        {
            var excessLines = ConsoleBox.Lines.Length - MaxLines;
            if (excessLines > 0)
            {
                ConsoleBox.Text = string.Join(Environment.NewLine, ConsoleBox.Lines.Skip(excessLines));
            }

            ConsoleBox.SelectionStart = ConsoleBox.TextLength;
            ConsoleBox.ScrollToCaret();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            LastCheck.Text = "Live Matches: ";
            LastCheck.ForeColor = Color.DarkGray;
            startButton.Enabled = true;
            AddNewUserFields();
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            bPage.Page = page3;
            _makeImage = false;
        }

        private void botButton_Click(object sender, EventArgs e)
        {
            bPage.Page = page1;
            _makeImage = false;
        }

        private void accountsButton_Click(object sender, EventArgs e)
        {
            bPage.Page = page2;
            _makeImage = false;
        }

        private void debuggingButton_Click(object sender, EventArgs e)
        {
            bPage.Page = page4;
            _makeImage = true;

            if (!_isTaskRunning)
            {
                _isTaskRunning = true;
                Task.Run(ImageLoop);
            }

        }

        private void bunifuButton21_Click(object sender, EventArgs e)
        {
            var drivercount = _driverList.Count();
           
            if (_driverselect < drivercount - 1) 
                _driverselect++;

            PictureBox1.Image = null;
            loaderIcon.Visible = true;
        }

        private void bunifuButton22_Click(object sender, EventArgs e)
        {
            if (_driverselect > 0)
                _driverselect--;

            PictureBox1.Image = null;
            loaderIcon.Visible = true;
        }

        private void ownBrowserCheckbox_CheckedChanged(object sender, BunifuCheckBox.CheckedChangedEventArgs e)
        {
            if (ownBrowserCheckbox.Checked)
                openFileDialog1.ShowDialog();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            var dialogResult = MessageBox.Show("Hello there!\r\n\r\nIf you appreciate my work and would like to support me, you can make a donation of any amount you like. Your contribution will help me keep improving my programs and creating more useful tools.\r\n\r\nEvery donation, no matter how small, will be greatly appreciated.\r\n\r\nThank you for considering to support me. I hope my programs can continue to be useful to you in the future.\r\n\r\nWould you like to Donate? ", "Donations", MessageBoxButtons.YesNo);

            if (dialogResult == DialogResult.Yes)
            {
                Process.Start("https://www.paypal.com/donate/?hosted_button_id=JX6DWXC4TLPFW");
            }
        }

        private void minimize_Click(object sender, EventArgs e)
        {
            var obj = (Form1)Application.OpenForms["Form1"];
            if (obj != null) obj.WindowState = FormWindowState.Minimized;
        }

        private async void close_Click(object sender, EventArgs e)
        {
            _isRunning = false;
            _doCancel = true;
            await Task.Run(Stop); 
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Baseult");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.unknowncheats.me/forum/members/3154276.html");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.youtube.com/@baseultprivate9137");
            Process.Start("https://www.youtube.com/@baseult116");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Process.Start("https://discord.com/invite/PFBuC3T4RC");
        }
    }
}