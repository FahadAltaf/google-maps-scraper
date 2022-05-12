using CsvHelper;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace GoogleMapsScraper
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        List<GBusinessData_Results> allData = new List<GBusinessData_Results>();
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            GoogleScraper(new List<GBusinessData_Entries> { new GBusinessData_Entries { SearchKey = "shoe stores", SearchLocation = "Dublin" } });
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }

        public  void GoogleScraper(List<GBusinessData_Entries> entries)
        {
            ChromeOptions options = new ChromeOptions();
            options.AddArguments(new List<string>()
      {
        //"--silent-launch",
        //"--no-startup-window",
        //"no-sandbox",
        //"headless"
      });
            ChromeDriverService defaultService = ChromeDriverService.CreateDefaultService();
            defaultService.HideCommandPromptWindow = true;


            using (IWebDriver m_driver = new ChromeDriver(defaultService, options))
            {
                string url = "https://www.google.com/search?tbs=lf:1,lf_ui:10&tbm=lcl&sxsrf=ALiCzsbZcVEl3n3D1-d89j7QXkxeJMQ6Vg:1652347823996&q=%E2%80%98shoe+stores%E2%80%99+in+dublin+ireland&rflfq=1&num=10&sa=X&ved=2ahUKEwiAyOCo09n3AhU8R_EDHfC3DIAQjGp6BAgPEAE&biw=1920&bih=937&dpr=1#rlfi=hd:;si:10835098716400773423,l,CiPigJhzaG9lIHN0b3Jlc-KAmSBpbiBkdWJsaW4gaXJlbGFuZEjk2L2Bq6qAgAhaKxAAEAEYABgBGAMYBCIdc2hvZSBzdG9yZXMgaW4gZHVibGluIGlyZWxhbmSSAQpzaG9lX3N0b3JlqgETEAEqDyILc2hvZSBzdG9yZXMoAA,y,fzFM-P3NtsA;mv:[[53.3512161,-6.255186],[53.340811599999995,-6.2665576]]";
                m_driver.Navigate().GoToUrl(url);

                foreach (var item in entries)
                {

                    try
                    {
                        m_driver.Navigate().GoToUrl(url);
                        HtmlDocument doc = new HtmlDocument();

                        var wait1 = new WebDriverWait(m_driver, new TimeSpan(0, 1, 0));
                        //var element1 = wait1.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//*[@id=\"lst-ib\"]")));
                        var element1 = m_driver.FindElement(By.XPath("//*[@id=\"lst-ib\"]"));
                        element1.Clear();
                        element1.SendKeys(item.SearchKey + " " + item.SearchLocation);


                        var searchBtn = m_driver.FindElement(By.XPath("//*[@id=\"mKlEF\"]"));
                        searchBtn.Click();

                        var wait2 = new WebDriverWait(m_driver, new TimeSpan(0, 1, 0));
                    //var element2 = wait1.Until(WaitHelpers.ExpectedConditions.ElementToBeClickable(By.XPath("//*[@id=\"rl_ist0\"]/div[1]/div[4]")));

                    task:
                        doc.LoadHtml(m_driver.PageSource);
                        var pageNode = doc.DocumentNode.SelectSingleNode("//div[@aria-label='Local results pagination']");
                        var paginationSection = pageNode.ChildNodes[0].ChildNodes[0].ChildNodes[0];

                        if (paginationSection != null && paginationSection.ChildNodes.Count > 0)
                        {
                            //Scrape entries
                            List<GBusinessData_Results> results = new List<GBusinessData_Results>();
                            var entriesToClick = m_driver.FindElements(By.ClassName("rllt__link"));
                            foreach (var eToClick in entriesToClick)
                            {
                                eToClick.Click();
                                Thread.Sleep(3000);
                                var docWithPopUp = new HtmlDocument();
                                docWithPopUp.LoadHtml(m_driver.PageSource);
                                GBusinessData_Results res = new GBusinessData_Results();

                                ScrapeEntry(docWithPopUp, item, out res);
                                try
                                {
                                    var btns = docWithPopUp.DocumentNode.SelectNodes("//a[@role='button']").ToList();
                                    var websiteNode = btns.FirstOrDefault(x => x.InnerText == "Website");
                                    if (websiteNode != null)
                                    {
                                        var text = HttpUtility.HtmlDecode(websiteNode.Attributes.FirstOrDefault(x => x.Name == "href").Value);
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            var parts = text.Split('&');
                                            if (parts.Length == 1)
                                            {
                                                res.Website = parts[0];
                                            }
                                            else
                                                res.Website = parts.FirstOrDefault(x => x.Contains("url=")).Replace("url=", "");
                                        }
                                    }
                                }
                                catch { }
                                results.Add(res);
                            }

                            //SaveEntries(results, item.SearchKey);

                            IWebElement nextBtn = null;
                            try
                            {
                                nextBtn = m_driver.FindElement(By.XPath("//*[@id=\"pnnext\"]"));

                            }
                            catch (Exception)
                            {
                                Console.WriteLine("No Pagination- Next Button not found");
                            }

                            if (nextBtn != null)
                            {
                                var wait = new WebDriverWait(m_driver, new TimeSpan(0, 1, 0));
                                var element = m_driver.FindElement(By.XPath("//*[@id=\"pnnext\"]"));
                                nextBtn.Click();
                                Thread.Sleep(3000);
                                goto task;
                            }
                        }
                        else
                        {
                            Console.WriteLine("No Pagination");
                            //Scrape entries
                            List<GBusinessData_Results> results = new List<GBusinessData_Results>();
                            var entriesToClick = m_driver.FindElements(By.ClassName("rllt__link"));
                            foreach (var eToClick in entriesToClick)
                            {
                                eToClick.Click();
                                Thread.Sleep(3000);
                                var docWithPopUp = new HtmlDocument();
                                docWithPopUp.LoadHtml(m_driver.PageSource);
                                GBusinessData_Results res = new GBusinessData_Results();

                                ScrapeEntry(docWithPopUp, item, out res);
                                try
                                {
                                    var btns = docWithPopUp.DocumentNode.SelectNodes("//a[@role='button']").ToList();
                                    var websiteNode = btns.FirstOrDefault(x => x.InnerText == "Website");
                                    if (websiteNode != null)
                                    {
                                        var text = HttpUtility.HtmlDecode(websiteNode.Attributes.FirstOrDefault(x => x.Name == "href").Value);
                                        if (!string.IsNullOrEmpty(text))
                                        {
                                            var parts = text.Split('&');
                                            if (parts.Length == 1)
                                            {
                                                res.Website = parts.FirstOrDefault(x => x.Contains("url=")).Replace("url=", "");
                                            }
                                            else
                                                res.Website = parts.FirstOrDefault(x => x.Contains("url=")).Replace("url=", "");
                                        }
                                    }
                                }
                                catch { }
                                results.Add(res);
                            }

                        //    SaveEntries(results, item.SearchKey);
                            //  ScrapeEntries(doc, item);

                        }

                        //item.Status = JobEntryStatus.Processed;
                    }
                    catch (Exception ex)
                    {
                       
                    }


                }
                m_driver?.Close();
                Thread.Sleep(3000);
                m_driver?.Quit();
                m_driver?.Dispose();
            }
        }
        private  void ScrapeEntry(HtmlDocument doc, GBusinessData_Entries id, out GBusinessData_Results output)
        {
            GBusinessData_Results data = new GBusinessData_Results();

            try
            {
                var businessDiv = doc.DocumentNode.SelectSingleNode("//h2[@data-attrid='title']");
                if (businessDiv != null)
                {
                    data.BusinessName = HttpUtility.HtmlDecode(businessDiv.InnerText);
                }
            }
            catch (Exception)
            {

            }

            try
            {
                var ratingDiv = doc.DocumentNode.SelectSingleNode("//g-review-stars");
                if (ratingDiv != null)
                {
                    data.Rating = ratingDiv.ChildNodes[0].Attributes.FirstOrDefault(x => x.Name == "aria-label").Value;
                    if (!string.IsNullOrEmpty(data.Rating))
                    {
                        data.Rating = data.Rating.Replace(" out of 5,", "").Replace("Rated ", "");
                    }
                }


            }
            catch (Exception)
            {

            }

            

            try
            {
                var categoryNode = doc.DocumentNode.SelectSingleNode("/html/body/div[6]/div/div[9]/div[2]/div/div[2]/async-local-kp/div/div/div[1]/div/div/block-component/div/div[1]/div/div/div/div[1]/div/div/div[1]/div/div[2]/div[2]/div/span[1]");
                if (categoryNode != null)
                {
                    data.Category =HttpUtility.HtmlDecode( categoryNode.InnerText);
                }
            }
            catch (Exception)
            {

            }

            try
            {
                var websiteAnchor = doc.DocumentNode.SelectSingleNode("/html/body/div[6]/div/div[9]/div[2]/div/div[2]/async-local-kp/div/div/div[1]/div/div/block-component/div/div[1]/div/div/div/div[1]/div/div/div[1]/div/div[1]/div/div[2]/div[1]/a");
                if (websiteAnchor != null)
                {
                    data.Website = websiteAnchor.Attributes.FirstOrDefault(x => x.Name == "href").Value;
                }
            }
            catch (Exception)
            {

            }

            try
            {
                var addressDiv = doc.DocumentNode.SelectSingleNode("//div[@data-attrid='kc:/location/location:address']");
                if (addressDiv != null)

                {
                    data.Address = addressDiv.InnerText.Replace("Address: ", "").Replace("Map location is approximate.Can you help us improve it?", "");
                    string[] parts = data.Address.Split(',');
                    switch (parts.Count())
                    {
                        case 2:

                            data.City = parts[0];

                            string[] p1 = parts[1].Split(' ');
                            if (p1.Count() == 3)
                            {
                                data.State = p1[1];
                                data.Zip = p1[2];
                            }
                            break;
                        case 3:
                            data.StreetAddress = parts[0];
                            data.City = parts[1];

                            string[] p2 = parts[2].Split(' ');
                            if (p2.Count() == 3)
                            {
                                data.State = p2[1];
                                data.Zip = p2[2];
                            }

                            break;
                        case 4:
                            data.StreetAddress = parts[0];
                            data.City = parts[1];

                            string[] p3 = parts[2].Split(' ');
                            if (p3.Count() == 3)
                            {
                                data.State = p3[1];
                                data.Zip = p3[2];
                            }

                            if (string.IsNullOrEmpty(data.Zip))
                            {
                                data.StreetAddress = parts[0] + " " + parts[1];
                                data.City = parts[2];

                                string[] p4 = parts[3].Split(' ');
                                if (p4.Count() == 3)
                                {
                                    data.State = p4[1];
                                    data.Zip = p4[2];
                                }
                            }

                            break;
                        default:
                            break;
                    }

                }

            }
            catch (Exception)
            {

            }

            try
            {
                var phoneDiv = doc.DocumentNode.SelectSingleNode("//div[@data-attrid='kc:/collection/knowledge_panels/has_phone:phone']");
                if (phoneDiv != null)
                { data.Phone = phoneDiv.InnerText.Replace("Phone: ", ""); }

            }
            catch (Exception)
            {

            }

            if (string.IsNullOrEmpty(data.Address) && string.IsNullOrEmpty(data.BusinessName) && string.IsNullOrEmpty(data.Phone)
                && string.IsNullOrEmpty(data.Rating) && string.IsNullOrEmpty(data.Website))
            { }
            else
            {
             //   data.EntryId = id.Id;
            }
            allData.Add(data);
            using (var writer = new StreamWriter("results.csv"))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(allData);
            }
            output = data;

            

        }
    }

    public class GBusinessData_Entries 
    {
        public string SearchKey { get; set; }
        public string SearchLocation { get; set; }
    }

    public class GBusinessData_Results 
    {
        public string CreatedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public string BusinessName { get; set; }
        public string Category { get; set; }
        public string Website { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Rating { get; set; }

    }
}
