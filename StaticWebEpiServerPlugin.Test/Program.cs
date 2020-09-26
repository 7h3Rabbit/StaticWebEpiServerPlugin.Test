using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
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
                BackupOutputFolderAndCleanIt();

                LoginToCMS(driver);

                ChangeAlloyPlanPageTitle(driver);
            }
            catch (Exception ex)
            {
            }
            driver.Close();
            driver.Quit();
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
                        }else
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

        private static void ChangeAlloyPlanPageTitle(EdgeDriver driver)
        {
            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");
            Thread.Sleep(2 * 1000);

            EnsureAllPropertyViewIsUsed(driver);

            IWebElement propertyElement = GetPropertyElement(driver, "icontent_name");
            if (propertyElement != null)
            {
                var oldValue = propertyElement.GetAttribute("value");

                if (oldValue == "Alloy Plan")
                {
                    string newValue = "TEST-" + Guid.NewGuid();

                    propertyElement.SendKeys(Keys.Control + "A");
                    propertyElement.SendKeys(Keys.Delete);
                    propertyElement.SendKeys(newValue);
                    Thread.Sleep(1 * 1000);
                    propertyElement.SendKeys(Keys.Tab);   // Activates saving of our change

                    driver.Navigate().Refresh();
                    Thread.Sleep(2 * 1000);

                    PublishPage(driver);
                }
                else
                {
                    System.Diagnostics.Process.Start("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");
                    throw new Exception("Aborting TEST, wrong page OR wrong initial value. Name should be = 'Alloy Plan'");
                }

                Thread.Sleep(1 * 1000);
            }

            driver.Navigate().GoToUrl("http://localhost:49822/EPiServer/CMS/#context=epi.cms.contentdata:///6&viewsetting=viewlanguage:///en");

            Thread.Sleep(5 * 1000);
        }

        private static IWebElement GetPropertyElement(EdgeDriver driver, string propertyName)
        {
            var index = 0;
            while (index < 20)
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
