using OpenQA.Selenium.Edge;
using System;

namespace StaticWebEpiServerPlugin.Test
{
    public class Test
    {
        public string Name { get; set; }
        public string Message { get; set; }
        public TestProgress Progress { get; set; }

        public bool Success { get; set; }
        public Action<EdgeDriver, Test> Func { get; set; }
    }
}
