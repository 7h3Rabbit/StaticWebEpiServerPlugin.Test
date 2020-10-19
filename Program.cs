using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace StaticWebEpiServerPlugin.Test
{
    class Program
    {
        static string epiServerBaseUrl = "http://localhost:49822/";
        static string staticBaseUrl = "http://localhost/";
        static string outputFolder = @"C:\inetpub\wwwroot\";
        static bool EnableBackup = false;

        static List<Test> tests = null;
        static EdgeDriver driver = null;

        static void Main(string[] args)
        {
            tests = new List<Test>
            {
                new Test
                {
                    Name = "Backup Output folder and Clean it",
                    Func = BackupOutputFolderAndCleanIt
                },
                new Test
                {
                    Name = "Setup Selenium Client",
                    Func = CreateBrowser
                },
                new Test
                {
                    Name = "Login to CMS",
                    Func = LoginToCMS
                },
                new Test
                {
                    Name = "Scheduled Job Test",
                    Func = StartGenerateStaticWebScheduledJob
                },
                new Test
                {
                    Name = "Single Block Publishing Test",
                    Func = SingleBlockPublishingTest
                },
                new Test
                {
                    Name = "Single Page Publishing Test",
                    Func = SinglePagePublishingTest
                }
            };

            try
            {
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                var positionRatio = 0.66;
                var widthRatio = 0.33;
                var topCorner = new System.Drawing.Point((int)(screen.WorkingArea.Width * positionRatio), screen.WorkingArea.Y);

                SetWindowPosition(topCorner.X, topCorner.Y, (int)(screen.WorkingArea.Width * widthRatio), screen.WorkingArea.Height);

                PrintProgress();

                foreach (Test test in tests)
                {
                    test.Func.Invoke(driver, test);
                    test.Progress = TestProgress.Ended;
                    PrintProgress();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);

                if (tests != null && tests.Count != 0)
                {
                    foreach (Test item in tests)
                    {
                        if (item.Progress == TestProgress.Waiting)
                        {
                            item.Progress = TestProgress.Canceled;
                        }
                    }
                }
            }

            while (true)
            {
                PrintProgress();
                Console.WriteLine("Write \"Y/y\" and press <ENTER> to quit program");
                var value = Console.ReadLine();
                if (value == "Y" || value == "y")
                {
                    break;
                }
            }

            Console.WriteLine("Closing program, please wait...");

            driver.Close();
            driver.Quit();
        }

        const int SWP_NOZORDER = 0x4;
        const int SWP_NOACTIVATE = 0x10;

        [DllImport("kernel32")]
        static extern IntPtr GetConsoleWindow();


        [DllImport("user32")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, int flags);

        /// <summary>
        /// Sets the console window location and size in pixels
        /// </summary>
        public static void SetWindowPosition(int x, int y, int width, int height)
        {
            SetWindowPos(Handle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static IntPtr Handle
        {
            get
            {
                //Initialize();
                return GetConsoleWindow();
            }
        }

        private static void PrintProgress()
        {
            Console.Clear();

            if (tests == null || tests.Count == 0)
            {
                return;
            }

            Console.WriteLine("*************************************************");
            Console.WriteLine("* StaticWebEpiServerPlugin - Regression Test(s) *");
            Console.WriteLine("*************************************************");
            foreach (Test item in tests)
            {
                PrintTest(item);
            }
            Console.WriteLine("*************************************************");
        }

        private static void PrintTest(Test test)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            switch (test.Progress)
            {
                case TestProgress.Waiting:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("[Waiting]\t");
                    break;
                case TestProgress.Running:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("[Running]\t");
                    break;
                case TestProgress.Cleaning:
                    Console.Write("[Cleaning]\t");
                    break;
                case TestProgress.Ended:
                    if (test.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("[Success]\t");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("[FAIL]\t\t");
                    }
                    break;
                case TestProgress.Canceled:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[Canceled]\t");
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("[Unknown]\t");
                    break;
            }

            Console.Write(test.Name);
            if (!string.IsNullOrEmpty(test.Message))
            {
                Console.Write(" - ");
                Console.Write(test.Message);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void SingleBlockPublishingTest(EdgeDriver driver, Test result)
        {
            result.Progress = TestProgress.Running;
            PrintProgress();
            result.Success = true;
            //var result = new Test { Name = "Single Block Publishing Test", Success = true };
            string newTitle = "TEST-" + Guid.NewGuid();
            var errorMessage = ChangeAlloyMeetBlockHeading(driver, newTitle, "Sharing worldwide");
            if (string.IsNullOrEmpty(errorMessage))
            {
                // Verify markup written
                var alloyMeetFile = outputFolder + @"en\alloy-meet\index.html";
                if (File.Exists(alloyMeetFile))
                {
                    var alloyMeetMarkup = File.ReadAllText(alloyMeetFile, Encoding.UTF8);
                    if (alloyMeetMarkup.IndexOf(newTitle) > 0)
                    {
                        // Validate that they have same content
                        driver.Navigate().GoToUrl("http://localhost:49822/en/alloy-meet/");
                        Thread.Sleep(2 * 1000);

                        var driverPageSource = driver.PageSource;
                        var result2 = ValidateMarkup(alloyMeetMarkup, driverPageSource);
                        result.Success = result2.Success;
                        result.Message = result2.Message;
                    }
                    else
                    {
                        // TEST FAILED
                        //Console.WriteLine("Single Block Publishing Test - Alloy Meet Heading - FAIL (Generated html file has wrong content, probably not written or wrote old content)");
                        result.Success = false;
                        result.Message = "Generated html file has wrong content, probably not written or wrote old content";
                    }
                }
                else
                {
                    // TEST FAILED
                    //Console.WriteLine("Single Block Publishing Test - Alloy Plan Heading - FAIL (No generated html file found after publish, file: " + alloyMeetFile + ")");
                    result.Success = false;
                    result.Message = "No generated html file found after publish, file: " + alloyMeetFile;
                }
            }
            else
            {
                //Console.WriteLine("Single Block Publishing Test - Alloy Plan Heading - FAIL (" + errorMessage + ")");
                result.Success = false;
                result.Message = errorMessage;
            }

            Thread.Sleep(5 * 1000);

            result.Progress = TestProgress.Cleaning;
            PrintProgress();

            // Reset Alloy Plan page (So we can do the same test later)
            ChangeAlloyMeetBlockHeading(driver, "Sharing worldwide");

            Thread.Sleep(5 * 1000);
        }


        private static void SinglePagePublishingTest(EdgeDriver driver, Test result)
        {
            result.Progress = TestProgress.Running;
            PrintProgress();
            result.Success = true;
            //var result = new Test { Name = "Single Page Publishing Test", Success = true };
            string newTitle = "TEST-" + Guid.NewGuid();
            var errorMessage = ChangeAlloyPlanPageTitle(driver, newTitle, "Alloy Plan");
            if (string.IsNullOrEmpty(errorMessage))
            {
                // Verify markup written
                var alloyPlanFile = outputFolder + @"en\alloy-plan\index.html";
                if (File.Exists(alloyPlanFile))
                {
                    // Make sure we have our unique value
                    var alloyPlanMarkup = File.ReadAllText(alloyPlanFile, Encoding.UTF8);
                    if (alloyPlanMarkup.IndexOf(newTitle) > 0)
                    {
                        // Validate that they have same content
                        driver.Navigate().GoToUrl("http://localhost:49822/en/alloy-plan/");
                        Thread.Sleep(2 * 1000);

                        var driverPageSource = driver.PageSource;
                        var result2 = ValidateMarkup(alloyPlanMarkup, driverPageSource);
                        result.Success = result2.Success;
                        result.Message = result2.Message;
                    }
                    else
                    {
                        // TEST FAILED
                        //Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (Generated html file has wrong content, probably not written or wrote old content)");
                        result.Success = false;
                        result.Message = "Generated html file has wrong content, probably not written or wrote old content";
                        //return result;
                    }
                }
                else
                {
                    // TEST FAILED
                    //Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (No generated html file found after publish, file: " + alloyPlanFile + ")");
                    result.Success = false;
                    result.Message = "No generated html file found after publish, file: " + alloyPlanFile;
                    //return result;
                }
            }
            else
            {
                //Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (" + errorMessage + ")");
                result.Success = false;
                result.Message = errorMessage;
                //return result;
            }

            Thread.Sleep(5 * 1000);

            result.Progress = TestProgress.Cleaning;
            PrintProgress();

            // Reset Alloy Plan page (So we can do the same test later)
            ChangeAlloyPlanPageTitle(driver, "Alloy Plan");

            Thread.Sleep(5 * 1000);
        }

        private static Test ValidateMarkup(string alloyPlanMarkup, string driverPageSource)
        {
            Test result = new Test();
            result.Success = true;
            var errorMessage = "Generated html file has wrong content, ";

            var usesRequiredCssDemo = alloyPlanMarkup.IndexOf("<!-- RequiredCssDemo -->") != -1;

            // TODO: Validate written markup
            // 1. Do we have staticweb commment on removed functionality? We should.
            if (alloyPlanMarkup.IndexOf("<!-- StaticWeb - We are removing search as it is not working in the static version -->") == -1)
            {
                result.Message = errorMessage + "No StaticWeb comment about removed content found";
                result.Success = false;
            }
            // 2. Do we have epi-quickNavigator? We should NOT.
            if (alloyPlanMarkup.IndexOf("epi-quickNavigator") >= 0)
            {
                result.Message = errorMessage + "EpiServer QuickNavigator found";
                result.Success = false;
            }
            // 3. Do we have the correct amount of script and css includes? We should.
            var nOfLinks = Regex.Matches(alloyPlanMarkup, "<link").Count;
            var nOfStyles = Regex.Matches(alloyPlanMarkup, "<style").Count;
            var nOfScripts = Regex.Matches(alloyPlanMarkup, "<script").Count;
            if (nOfScripts != 3)
            {
                result.Message = errorMessage + "Number of script elements was NOT 3 but " + nOfScripts;
                result.Success = false;
            }
            else if (!usesRequiredCssDemo)
            {
                if (nOfLinks != 7)
                {
                    result.Message = errorMessage + "Number of link elements was NOT 7 but " + nOfLinks;
                    result.Success = false;
                }
                else if (nOfStyles != 0)
                {
                    result.Message = errorMessage + "Number of style elements was NOT 0 but " + nOfStyles;
                    result.Success = false;
                }
            }
            else
            {
                if (nOfLinks != 2)
                {
                    result.Message = errorMessage + "Number of link elements was NOT 0 but " + nOfLinks;
                    result.Success = false;
                }
                else if (nOfStyles != 5)
                {
                    result.Message = errorMessage + "Number of style elements was NOT 1 but " + nOfStyles;
                    result.Success = false;
                }
            }
            // 4. Do we have the the same amount of script and css includes? We should NOT (quick navigator should be different).
            // 5. Are resources rewritten? They should.
            if (alloyPlanMarkup.IndexOf("/cache/v1/") == -1)
            {
                result.Message = errorMessage + "Resource urls are not rewritten to /cache/v1/";
                result.Success = false;
            }
            else if (alloyPlanMarkup.IndexOf("/Static/") != -1)
            {
                result.Message = errorMessage + "There are still resources referenced to /Static/";
                result.Success = false;
            }
            else if (alloyPlanMarkup.IndexOf("/contentassets/") != -1)
            {
                result.Message = errorMessage + "There are still resources referenced to /contentassets/";
                result.Success = false;
            }
            else if (alloyPlanMarkup.IndexOf("/Util/") != -1)
            {
                result.Message = errorMessage + "There are still resources referenced to /Util/";
                result.Success = false;
            }
            // 6. Are resources present/ do they exist? They should.
            var resourceMatches = Regex.Matches(alloyPlanMarkup, @"\/cache\/[^""]+");
            foreach (Match match in resourceMatches)
            {
                if (match.Success)
                {
                    var filePath = outputFolder + match.Value.Replace("/", "\\");
                    if (!File.Exists(filePath))
                    {
                        result.Message = errorMessage + "Not all referenced resources was found: " + match.Value;
                        result.Success = false;
                    }
                }
            }

            return result;
        }

        private static void BackupOutputFolderAndCleanIt(EdgeDriver driver, Test result)
        {
            result.Progress = TestProgress.Running;
            PrintProgress();

            DirectoryInfo directory = new DirectoryInfo(outputFolder);
            if (directory.Exists)
            {
                var subDirectories = directory.GetDirectories();
                var subFiles = directory.GetFiles();

                // make backup if we have something to backup
                if (subDirectories.Length > 0 || subFiles.Length > 0)
                {
                    DirectoryInfo backupDir = null;
                    if (EnableBackup)
                    {
                        backupDir = directory.Parent.CreateSubdirectory(directory.Name + " - Backup " + DateTime.Now.ToShortDateString() + "." + DateTime.Now.Ticks);
                    }

                    foreach (var subDirectory in subDirectories)
                    {
                        result.Progress = TestProgress.Cleaning;
                        PrintProgress();

                        if (EnableBackup)
                        {
                            subDirectory.MoveTo(backupDir.FullName + Path.DirectorySeparatorChar + subDirectory.Name);
                        }
                        else
                        {
                            subDirectory.Delete(true);
                        }
                    }

                    foreach (var subFile in subFiles)
                    {
                        result.Progress = TestProgress.Cleaning;
                        PrintProgress();

                        if (EnableBackup)
                        {
                            subFile.MoveTo(backupDir.FullName + Path.DirectorySeparatorChar + subFile.Name);
                        }
                        else
                        {
                            subFile.Delete();
                        }
                    }
                }
            }
            result.Success = true;
        }

        private static void LoginToCMS(EdgeDriver driver, Test result)
        {
            result.Progress = TestProgress.Running;
            PrintProgress();

            try
            {

                driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/");
            }
            catch (Exception)
            {
                result.Progress = TestProgress.Ended;
                result.Message = "Unable to connect to: http://localhost:49822/EPiServer/CMS/";
                result.Success = false;

                throw;
            }
            Thread.Sleep(2 * 1000);

            driver.FindElementById("LoginControl_UserName").SendKeys("Wayne");
            driver.FindElementById("LoginControl_Password").SendKeys("3Aac6f9a-53fb-447c-be24-fe4bd3c42eb4");
            driver.FindElementById("LoginControl_Button1").Click();

            Thread.Sleep(2 * 1000);

            result.Success = true;
        }

        private static string ChangeAlloyMeetBlockHeading(EdgeDriver driver, string newTitle, string verifyOldHeading = null)
        {
            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///64&viewsetting=viewlanguage:///en");
            Thread.Sleep(2 * 1000);

            EnsureAllPropertyViewIsUsed(driver);

            IWebElement propertyElement = GetPropertyElement(driver, "heading");
            if (propertyElement != null)
            {
                var oldValue = propertyElement.GetAttribute("value");

                var verifyTitleBeforeContinue = !string.IsNullOrEmpty(verifyOldHeading);
                if (verifyTitleBeforeContinue)
                {
                    if (oldValue != verifyOldHeading)
                    {
                        return "Prerequirement not met for test, block heading is not '" + verifyOldHeading + "'";
                    }
                }

                propertyElement.SendKeys(Keys.Control + "A");
                propertyElement.SendKeys(Keys.Delete);
                propertyElement.SendKeys(newTitle);
                Thread.Sleep(1 * 1000);

                propertyElement.SendKeys(Keys.Tab);   // Activates saving of our change
                Thread.Sleep(2 * 1000);

                driver.Navigate().Refresh();
                Thread.Sleep(2 * 1000);

                PublishPage(driver);

                Thread.Sleep(1 * 1000);

                driver.Navigate().Refresh();
                //driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///64&viewsetting=viewlanguage:///en");

                Thread.Sleep(5 * 1000);
                return null;
            }

            return "Unable to find 'Name' property field";
        }

        private static string ChangeAlloyPlanPageTitle(EdgeDriver driver, string newTitle, string verifyOldTitle = null)
        {
            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");
            Thread.Sleep(2 * 1000);

            EnsureAllPropertyViewIsUsed(driver);

            IWebElement propertyElement = GetPropertyElement(driver, "icontent_name");
            if (propertyElement != null)
            {
                var oldValue = propertyElement.GetAttribute("value");

                var verifyTitleBeforeContinue = !string.IsNullOrEmpty(verifyOldTitle);
                if (verifyTitleBeforeContinue)
                {
                    if (oldValue != verifyOldTitle)
                    {
                        return "Prerequirement not met for test, page name is not 'Alloy Plan'"; // Aborting TEST, wrong page OR wrong initial value. Name should be = 'Alloy Plan'
                                                                                                 //System.Diagnostics.Process.Start("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");
                                                                                                 //throw new Exception("Aborting TEST, wrong page OR wrong initial value. Name should be = 'Alloy Plan'");
                    }
                }

                propertyElement.SendKeys(Keys.Control + "A");
                propertyElement.SendKeys(Keys.Delete);
                propertyElement.SendKeys(newTitle);
                Thread.Sleep(1 * 1000);

                propertyElement.SendKeys(Keys.Tab);   // Activates saving of our change
                Thread.Sleep(2 * 1000);

                driver.Navigate().Refresh();
                Thread.Sleep(2 * 1000);

                PublishPage(driver);

                Thread.Sleep(1 * 1000);

                driver.Navigate().Refresh();
                //driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");

                Thread.Sleep(5 * 1000);
                return null;
            }

            return "Unable to find 'Name' property field";
        }

        private static IWebElement GetPropertyElement(EdgeDriver driver, string propertyName)
        {
            var index = 0;
            while (index < 50)
            {
                var actions = new Actions(driver);
                actions.SendKeys(Keys.Tab).Build().Perform();

                var activeElement = driver.SwitchTo().ActiveElement();
                var attr = activeElement.GetAttribute("name");
                if (!string.IsNullOrEmpty(attr) && attr.Contains(propertyName))
                {
                    return activeElement;

                }

                Thread.Sleep(500);
                index++;
            }

            return null;
        }

        private static void PublishPage(EdgeDriver driver)
        {
            var actionElement = GetActionElementWithText(driver, "Publish?");
            if (actionElement != null)
            {
                // action element found, activate it to change to fold out publish options
                actionElement.SendKeys(Keys.Space);
                Thread.Sleep(500);

                var optionElement = GetActionOptionWithText(driver, "Publish Changes");
                // we now have new active action, press enter to activate 'publish'
                optionElement.SendKeys(Keys.Enter);
                Thread.Sleep(2 * 1000);
            }
        }

        private static string GetActionUrlWithText(EdgeDriver driver, string actionText)
        {
            int index = 0;
            while (index < 50)
            {
                var actions = new Actions(driver);
                actions.SendKeys(Keys.Tab).Build().Perform();
                var activeElement = driver.SwitchTo().ActiveElement();

                var attr = activeElement.GetAttribute("title");
                var value = activeElement.GetAttribute("href");
                if (string.IsNullOrEmpty(attr))
                {
                    attr = driver.ExecuteScript("return document.activeElement.innerText") as string;
                    value = driver.ExecuteScript("return document.activeElement.getAttribute('href')") as string;
                }
                if (string.IsNullOrEmpty(attr))
                {
                    // Return iframe selected content
                    attr = driver.ExecuteScript("if ('contentDocument' in document.activeElement) { return document.activeElement.contentDocument.activeElement.innerText; }else{return null;}") as string;
                    value = driver.ExecuteScript("if ('contentDocument' in document.activeElement) { return document.activeElement.contentDocument.activeElement.getAttribute('href'); }else{return null;}") as string;
                }

                if (!string.IsNullOrEmpty(attr) && attr.Contains(actionText))
                {
                    return value;
                }

                Thread.Sleep(100);
                index++;
            }

            return null;
        }

        private static IWebElement GetActionElementWithText(EdgeDriver driver, string actionText)
        {
            int index = 0;
            while (index < 50)
            {
                var actions = new Actions(driver);
                actions.SendKeys(Keys.Tab).Build().Perform();
                var activeElement = driver.SwitchTo().ActiveElement();

                var attr = activeElement.GetAttribute("title");
                if (string.IsNullOrEmpty(attr))
                {
                    attr = driver.ExecuteScript("return document.activeElement.innerText") as string;
                }
                if (string.IsNullOrEmpty(attr))
                {
                    // Return iframe selected content
                    attr = driver.ExecuteScript("if ('contentDocument' in document.activeElement) { return document.activeElement.contentDocument.activeElement.innerText; }else{return null;}") as string;
                }

                if (!string.IsNullOrEmpty(attr) && attr.Contains(actionText))
                {
                    return activeElement;
                }

                Thread.Sleep(100);
                index++;
            }

            return null;
        }

        private static IWebElement GetActionOptionWithText(EdgeDriver driver, string actionText)
        {
            int index = 0;
            while (index < 20)
            {
                var actions = new Actions(driver);
                actions.SendKeys(Keys.Down).Build().Perform();
                var activeElement = driver.SwitchTo().ActiveElement();

                var attr = activeElement.GetAttribute("title");
                if (string.IsNullOrEmpty(attr))
                {
                    attr = driver.ExecuteScript("return document.activeElement.innerText") as string;
                }

                if (!string.IsNullOrEmpty(attr) && attr.Contains(actionText))
                {
                    return activeElement;
                }

                Thread.Sleep(100);
                index++;
            }

            return null;
        }

        private static void StartGenerateStaticWebScheduledJob(EdgeDriver driver, Test result)
        {
            result.Progress = TestProgress.Running;
            result.Success = true;
            PrintProgress();
            //var result = new Test { Name = "Scheduled Job Test", Success = true };

            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/Admin/Default.aspx");

            var generateStaticWebScheduledJobUrl = GetActionUrlWithText(driver, "Generate StaticWeb");
            if (generateStaticWebScheduledJobUrl != null)
            {
                driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/Admin/" + generateStaticWebScheduledJobUrl);
                Thread.Sleep(5 * 1000);

                // When was the last run?
                var historyActionElement = GetActionElementWithText(driver, "History");
                historyActionElement.SendKeys(Keys.Enter);
                // Get last run date
                Thread.Sleep(2 * 1000);
                var previousRunDate = driver.ExecuteScript("return document.querySelector('td').innerText") as string;

                driver.Navigate().Refresh();
                driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/Admin/" + generateStaticWebScheduledJobUrl);
                Thread.Sleep(5 * 1000);

                var actionElement = GetActionElementWithText(driver, "Start Manually");
                actionElement.SendKeys(Keys.Space);

                // TODO: Wait until job is done (shows "The job has completed.")
                Thread.Sleep(60 * 1000);

                driver.Navigate().Refresh();
                driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/Admin/" + generateStaticWebScheduledJobUrl);

                // When was the last run?
                historyActionElement = GetActionElementWithText(driver, "History");
                historyActionElement.SendKeys(Keys.Enter);

                // Get last run date and result message
                Thread.Sleep(2 * 1000);
                var lastRunDate = driver.ExecuteScript("return document.querySelector('td').innerText") as string;
                var lastRunMessage = driver.ExecuteScript("return document.querySelector('td:nth-of-type(5)').innerText") as string;

                // Compare run dates
                if (string.IsNullOrEmpty(lastRunDate))
                {
                    //Console.WriteLine("Scheduled Job Test - FAIL (Unable to get date for when job as last runned, after it was started)");
                    result.Success = false;
                    result.Message = "Unable to get date for when job as last runned, after it was started";
                    return;
                }
                else if (previousRunDate == lastRunDate)
                {
                    // TEST FAILED
                    //Console.WriteLine("Scheduled Job Test - FAIL (Previous run date is the same as last run date, is job still running or did it not complete correctly)");
                    result.Success = false;
                    result.Message = "Previous run date is the same as last run date, is job still running or did it not complete correctly";
                    return;
                }
                else if (string.IsNullOrEmpty(lastRunMessage)) // Validate last run result message
                {
                    //Console.WriteLine("Scheduled Job Test - FAIL (Unable to get scheduled job result message)");
                    result.Success = false;
                    result.Message = "Unable to get scheduled job result message";
                    return;
                }
                if (result.Success)
                {
                    lastRunMessage = lastRunMessage.Trim(new[] { '\r', '\n', ' ' });

                    if (lastRunMessage.Equals("ExampleSite1 - 29 pages generated."))
                    {
                        //Console.WriteLine("Scheduled Job Test - SUCCESS");

                        // TODO: Validate that every URL exist AND that they have same content

                        result.Success = true;
                        result.Message = "";
                        return;
                    }
                    else
                    {
                        //Console.WriteLine("Scheduled Job Test - FAIL (Unexpected scheduled job result message: '" + lastRunMessage + "')");
                        result.Success = false;
                        result.Message = "Unexpected scheduled job result message: '" + lastRunMessage + "'";
                        return;
                    }
                }
            }
            else
            {
                //Console.WriteLine("Scheduled Job Test - FAIL (Unable to find scheduled job)");
                result.Success = false;
                result.Message = "Unable to find scheduled job";
                return;
            }

            result.Success = false;
            result.Message = "Unexpected error";
            return;
        }

        private static void EnsureAllPropertyViewIsUsed(EdgeDriver driver)
        {
            var actionElement = GetActionElementWithText(driver, "All Properties");
            if (actionElement != null)
            {
                // action element found, activate it to change to "all properties" view
                actionElement.SendKeys(Keys.Space);
                Thread.Sleep(2 * 1000);
            }
            else
            {
                driver.Navigate().Refresh();
                Thread.Sleep(2 * 1000);
            }
        }

        private static void CreateBrowser(EdgeDriver dummyArgument, Test result)
        {
            result.Progress = TestProgress.Running;
            PrintProgress();

            var options = new EdgeOptions();
            options.UseChromium = true;
            options.UseInPrivateBrowsing = true;

            driver = new EdgeDriver(@"C:\code\edgedriver_win64\", options);

            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            var topCorner = new System.Drawing.Point(screen.WorkingArea.X, screen.WorkingArea.Y);

            driver.Manage().Window.Position = topCorner;
            var ratio = 0.66;
            driver.Manage().Window.Size = new System.Drawing.Size((int)(screen.WorkingArea.Width * ratio), screen.WorkingArea.Height);

            //return driver;
            result.Success = true;
        }
    }
}
