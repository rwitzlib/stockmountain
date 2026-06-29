using System.Text.Json;
using System.Text.Json.Serialization;

namespace Optimus.UnitTests
{
    public class UnitTest1
    {
        JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        [Fact]
        public async Task Test1()
        {
            //    var current = $"{Directory.GetCurrentDirectory()}/response.json";
            //    var path = current;
            //    var file = File.OpenRead(path);

            //    using var stream = new StreamReader(file);

            //    var json = await stream.ReadToEndAsync();

            //    var response = JsonSerializer.Deserialize<BacktestV2Response>(json, _serializerOptions);


            //    var asdf = response.Results.Where(q => q.Date.Year == 2023);
            //    var asdfProfit = asdf.Sum(q => q.Hold.SumProfit);

            //    List<KeyValuePair<float, List<(int, float)>>> profits2023 = [];
            //    for (var i = 1; i <= 12; i++)
            //    {
            //        var monthlyProfits = asdf.Where(q => q.Date.Month == i).Sum(q => q.Hold.SumProfit);
            //        var dailyProfits = new List<(int, float)>();
            //        for (int j = 1; j <= 31; j++)
            //        {
            //            var day = asdf.Where(q => q.Date.Month == i && q.Date.Day == j);
            //            var count = day.Any() ? day.First().Results.Count : 0;
            //            var dailyProfit = day.Sum(q => q.Hold.SumProfit);
            //            dailyProfits.Add((count, dailyProfit));
            //        }
            //        profits2023.Add(new (monthlyProfits, dailyProfits));
            //    }

            //    var qwer = response.Results.Where(q => q.Date.Year == 2024);
            //    var qwerProfit = qwer.Sum(q => q.Hold.SumProfit);

            //    List<KeyValuePair<float, List<(int, float)>>> profits2024 = [];
            //    for (var i = 1; i <= 12; i++)
            //    {
            //        var monthlyProfits = qwer.Where(q => q.Date.Month == i).Sum(q => q.Hold.SumProfit);
            //        var dailyProfits = new List<(int, float)>();
            //        for (int j = 1; j <= 31; j++)
            //        {
            //            var day = qwer.Where(q => q.Date.Month == i && q.Date.Day == j);
            //            var count = day.Any() ? day.First().Results.Count : 0;
            //            var dailyProfit = day.Sum(q => q.Hold.SumProfit);
            //            dailyProfits.Add((count, dailyProfit));
            //        }
            //        profits2024.Add(new(monthlyProfits, dailyProfits));
            //    }
        }
    }
}