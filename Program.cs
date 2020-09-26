using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace StaticWebEpiServerPlugin.Test
{
    class Program
    {
        static string epiServerBaseUrl = "http://localhost:49822/";
        static string staticBaseUrl = "http://localhost/";
        static string outputFolder = @"C:\inetpub\wwwroot\";
        static bool EnableBackup = false;

        static void Main(string[] args)
        {
            EdgeDriver driver = CreateBrowser();
            try
            {
                Console.Clear(); // Remove Selenium debug messages
                Console.WriteLine("*****************************");

                BackupOutputFolderAndCleanIt();

                LoginToCMS(driver);

                SinglePagePublishingTest(driver);

                SingleBlockPublishingTest(driver);

                Console.WriteLine("*****************************");

                // show result for 60 seconds
                Thread.Sleep(60 * 1000);
            }
            catch (Exception ex)
            {
            }
            driver.Close();
            driver.Quit();
        }

        private static void SingleBlockPublishingTest(EdgeDriver driver)
        {
            string newTitle = "TEST-" + Guid.NewGuid();
            var errorMessage = ChangeAlloyMeetBlockHeading(driver, newTitle, "Sharing worldwide");
            if (string.IsNullOrEmpty(errorMessage))
            {
                // Verify markup written
                var alloyMeetFile = outputFolder + @"en\alloy-meet\index.html";
                if (File.Exists(alloyMeetFile))
                {
                    var alloyPlanMarkup = File.ReadAllText(alloyMeetFile, Encoding.UTF8);
                    if (alloyPlanMarkup.IndexOf(newTitle) > 0)
                    {
                        // TEST SUCCESS
                        Console.WriteLine("Single Block Publishing Test - Alloy Meet Heading - Success");
                    }
                    else
                    {
                        // TEST FAILED
                        Console.WriteLine("Single Block Publishing Test - Alloy Meet Heading - FAIL (Generated html file has wrong content, probably not written or wrote old content)");
                    }
                }
                else
                {
                    // TEST FAILED
                    Console.WriteLine("Single Block Publishing Test - Alloy Plan Heading - FAIL (No generated html file found after publish, file: " + alloyMeetFile + ")");
                }
            }
            else
            {
                Console.WriteLine("Single Block Publishing Test - Alloy Plan Heading - FAIL (" + errorMessage + ")");
            }

            Thread.Sleep(5 * 1000);

            // Reset Alloy Plan page (So we can do the same test later)
            ChangeAlloyMeetBlockHeading(driver, "Sharing worldwide");

            Thread.Sleep(5 * 1000);
        }


        private static void SinglePagePublishingTest(EdgeDriver driver)
        {
            string newTitle = "TEST-" + Guid.NewGuid();
            var errorMessage = ChangeAlloyPlanPageTitle(driver, newTitle, "Alloy Plan");
            if (string.IsNullOrEmpty(errorMessage))
            {
                // Verify markup written
                var alloyPlanFile = outputFolder + @"en\alloy-plan\index.html";
                if (File.Exists(alloyPlanFile))
                {
                    var alloyPlanMarkup = File.ReadAllText(alloyPlanFile, Encoding.UTF8);
                    if (alloyPlanMarkup.IndexOf(newTitle) > 0)
                    {
                        // TEST SUCCESS
                        Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - Success");
                    }
                    else
                    {
                        // TEST FAILED
                        Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (Generated html file has wrong content, probably not written or wrote old content)");
                    }
                }
                else
                {
                    // TEST FAILED
                    Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (No generated html file found after publish, file: " + alloyPlanFile + ")");
                }
            }
            else
            {
                Console.WriteLine("Single Page Publishing Test - Alloy Plan Title - FAIL (" + errorMessage + ")");
            }

            Thread.Sleep(5 * 1000);

            // Reset Alloy Plan page (So we can do the same test later)
            ChangeAlloyPlanPageTitle(driver, "Alloy Plan");

            Thread.Sleep(5 * 1000);
        }

        private static void BackupOutputFolderAndCleanIt()
        {
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
        }

        private static void LoginToCMS(EdgeDriver driver)
        {
            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/");
            Thread.Sleep(2 * 1000);

            driver.FindElementById("LoginControl_UserName").SendKeys("Wayne");
            driver.FindElementById("LoginControl_Password").SendKeys("3Aac6f9a-53fb-447c-be24-fe4bd3c42eb4");
            driver.FindElementById("LoginControl_Button1").Click();

            Thread.Sleep(2 * 1000);
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

        private static IWebElement GetActionElementWithText(EdgeDriver driver, string actionText)
        {
            int index = 0;
            while (index < 20)
            {
                var actions = new Actions(driver);
                actions.SendKeys(Keys.Tab).Build().Perform();
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


        private static void EnsureAllPropertyViewIsUsed(EdgeDriver driver)
        {
            var actionElement = GetActionElementWithText(driver, "All Properties");
            if (actionElement != null)
            {
                // action element found, activate it to change to "all properties" view
                actionElement.SendKeys(Keys.Space);
                Thread.Sleep(2 * 1000);
            }
            else {
                driver.Navigate().Refresh();
                Thread.Sleep(2 * 1000);
            }
        }

        private static EdgeDriver CreateBrowser()
        {
            var options = new EdgeOptions();
            options.UseChromium = true;

            var driver = new EdgeDriver(@"C:\code\edgedriver_win64\", options);
            return driver;
        }
    }
}
