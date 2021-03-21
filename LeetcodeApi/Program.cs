using LeetcodeApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LeetcodeApi
{
    internal class Program
    {
        private static async Task Main()
        {
            var lastDay = DateTime.Today;
            var last7Days = lastDay.AddDays(-6);
            var last30Days = lastDay.AddDays(-29);

            var submissions = await GetSubmissionsAsync(last30Days);

            var summaries = submissions
                .Where(x => x.StatusDisplay == "Accepted")
                .GroupBy(x => x.Title)
                .Select(x =>
                    new
                    {
                        Title = x.Key,
                        LastDayCount = x.Count(y => y.CreatedDateTime > lastDay),
                        Last7DaysCount = x.Count(y => y.CreatedDateTime > last7Days),
                        Last30DaysCount = x.Count(y => y.CreatedDateTime > last30Days),
                    })
                .OrderByDescending(x => x.LastDayCount)
                .ThenByDescending(x => x.Last7DaysCount)
                .ThenByDescending(x => x.Last30DaysCount)
                .ToList();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Title                                                  Day     Week   Month");
            Console.ResetColor();
            foreach (var summary in summaries)
            {
                Console.WriteLine($"{summary.Title,-50}\t{summary.LastDayCount}\t{summary.Last7DaysCount}\t{summary.Last30DaysCount}");
            }
        }

        private static async Task<List<SubmissionHistoryEntry>> GetSubmissionsAsync(DateTime fromDateTime)
        {
            using var httpClient = CreateHttpClient();

            var submissions = new List<SubmissionHistoryEntry>();
            var offset = 0;
            var submissionHistory = new SubmissionHistory { HasNext = true };
            while (submissionHistory.HasNext)
            {
                var url = $"https://leetcode.com/api/submissions/?offset={offset}&limit=20&lastkey={submissionHistory.LastKey}";
                var responseJsonString = await httpClient.GetStringAsync(url);
                submissionHistory = JsonSerializer.Deserialize<SubmissionHistory>(responseJsonString);

                foreach (var entry in submissionHistory.Entries)
                {
                    if (entry.CreatedDateTime < fromDateTime)
                    {
                        submissionHistory.HasNext = false;
                        break;
                    }

                    submissions.Add(entry);
                }

                offset += 20;
                await Task.Delay(TimeSpan.FromSeconds(1)); // crawling courtesy
            }
            return submissions;
        }

        private static HttpClient CreateHttpClient()
        {
            var httpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }; // supports gzip, deflate and br
            var httpClient = new HttpClient(httpClientHandler);

            var cookie = GetCookieFromChrome();
            httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

            return httpClient;
        }

        private static string GetCookieFromChrome()
        {
            var chromeCookieReader = new ChromeCookieReader();
            var cookies = chromeCookieReader.ReadCookies("leetcode.com"); // steal cookies from authenticated Chrome session
            var cookiesString = string.Join(';', cookies.Select(c => $"{c.Key}={c.Value}"));
            return cookiesString;
        }
    }
}
