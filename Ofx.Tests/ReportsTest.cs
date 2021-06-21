﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using OfxWeb.Asp.Controllers.Helpers;
using OfxWeb.Asp.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ofx.Tests
{
    [TestClass]
    public class ReportsTest
    {
        class Item : IReportable
        {
            public decimal Amount { get; set; }

            public DateTime Timestamp { get; set; }

            public string Category { get; set; }
        }

        public class ReportSeries : IGrouping<string, IReportable>
        {
            public string Key { get; set; }

            public IEnumerable<IReportable> Items { get; set; }

            public IEnumerator<IReportable> GetEnumerator() => Items.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
        }

        List<Item> Items;

        Report report = null;

        RowLabel GetRow(Func<RowLabel, bool> predicate)
        {
            var result = report.RowLabels.Where(predicate).SingleOrDefault();

            Assert.IsNotNull(result);

            return result;
        }
        ColumnLabel GetColumn(Func<ColumnLabel, bool> predicate)
        {
            var result = report.ColumnLabels.Where(predicate).SingleOrDefault();

            Assert.IsNotNull(result);

            return result;
        }

        [TestInitialize]
        public void SetUp()
        {
            report = new Report();

            Items = new List<Item>();
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 01, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 01, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 02, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 02, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 03, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 02, 01), Category = "Other" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 02, 01), Category = "Other" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 03, 01), Category = "Other" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 04, 01), Category = "Other" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 04, 01), Category = "Other:Something:A" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 04, 01), Category = "Other:Something:A" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 05, 01), Category = "Other:Something:A" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 06, 01), Category = "Other:Something:B" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 06, 01), Category = "Other:Else" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 06, 01), Category = "Other:Else" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 07, 01), Category = "Other:Else:X" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 08, 01), Category = "Other:Else:X" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 08, 01), Category = "Other:Else:Y" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 08, 01), Category = "Other:Else:Y" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 06, 01), Category = "Name" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 04, 01), Category = "Other" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 08, 01), Category = "Other:Else:Y" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 08, 01), Category = "Other:Else:Y" });
            Items.Add(new Item() { Amount = 100, Timestamp = new DateTime(2000, 07, 01), Category = "Other:Else:X" });
            Items.Add(new Item() { Amount = 1000, Timestamp = new DateTime(2000, 06, 01), Category = "Other:Something:B" });
        }

        [TestMethod]
        public void Empty()
        {
            Assert.IsNotNull(report);
        }

        [TestMethod]
        public void OneItemCols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(1).AsQueryable();
            report.Build();

            var Name = GetRow(x => x.Name == "Name");
            var Jan = GetColumn(x => x.Name == "Jan");

            Assert.AreEqual(2, report.RowLabels.Count());
            Assert.AreEqual(100, report[Jan, Name]);
        }
        [TestMethod]
        public void OneItemColsJson()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(1).AsQueryable();
            report.Build();

            string json = report.ToJson();

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var actual = root.EnumerateArray().First();
            var Name = actual.GetProperty("Name").GetString();
            var Jan = actual.GetProperty("ID:01").GetDecimal();

            var total = root.EnumerateArray().Last();
            var IsTotal = total.GetProperty("IsTotal").GetBoolean();
            var TotalTotal = total.GetProperty("TOTAL").GetDecimal();

            Assert.AreEqual("Name",Name);
            Assert.AreEqual(100m, Jan);
            Assert.AreEqual(true, IsTotal);
            Assert.AreEqual(100m, TotalTotal);
        }
        [TestMethod]
        public void ThreeMonthsCols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(5).AsQueryable();
            report.Build();

            var Name = GetRow(x => x.Name == "Name");
            var Feb = GetColumn(x => x.Name == "Feb");

            Assert.AreEqual(2, report.RowLabels.Count());
            Assert.AreEqual(4, report.ColumnLabels.Count());
            Assert.AreEqual(200m, report[Feb, Name]);
            Assert.AreEqual(500m, report[report.TotalColumn, Name]);
        }
        [TestMethod]
        public void TwoCategoriesCols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(9).AsQueryable();
            report.Build();

            var Other = GetRow(x => x.Name == "Other");
            var Feb = GetColumn(x => x.Name == "Feb");

            Assert.AreEqual(3, report.RowLabels.Count());
            Assert.AreEqual(5, report.ColumnLabels.Count());
            Assert.AreEqual(200m, report[Feb, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(900m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void TwoCategoriesColsCustomSimple()
        {
            var custom = new ColumnLabel()
            {
                Name = "Custom",
                UniqueID = "Z",
                Custom = x => 10000m
            };

            report.AddCustomColumn(custom);
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(9).AsQueryable();
            report.Build();
            report.WriteToConsole();

            var Other = GetRow(x => x.Name == "Other");
            var Feb = GetColumn(x => x.Name == "Feb");
            var Custom = GetColumn(x => x.Name == "Custom");

            Assert.AreEqual(3, report.RowLabels.Count());
            Assert.AreEqual(6, report.ColumnLabels.Count());
            Assert.AreEqual(200m, report[Feb, Other]);
            Assert.AreEqual(10000m, report[Custom, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(900m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void TwoCategoriesColsCustomComplex()
        {
            Func<Dictionary<string, decimal>, decimal> func = (cols) =>
            {
                var feb = cols["ID:02"];
                var mar = cols["ID:03"];

                return feb + mar;
            };

            var custom = new ColumnLabel()
            {
                Name = "Custom",
                UniqueID = "Z",
                Custom = func
            };

            report.AddCustomColumn(custom);
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(9).AsQueryable();
            report.Build();
            report.WriteToConsole();

            var Other = GetRow(x => x.Name == "Other");
            var Feb = GetColumn(x => x.Name == "Feb");
            var Custom = GetColumn(x => x.Name == "Custom");

            Assert.AreEqual(3, report.RowLabels.Count());
            Assert.AreEqual(6, report.ColumnLabels.Count());
            Assert.AreEqual(200m, report[Feb, Other]);
            Assert.AreEqual(300m, report[Custom, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(900m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void SubCategoriesCols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Skip(5).Take(8).AsQueryable();
            report.Build();

            var Other = GetRow(x => x.Name == "Other");
            var Apr = GetColumn(x => x.Name == "Apr");

            Assert.AreEqual(2, report.RowLabels.Count());
            Assert.AreEqual(6, report.ColumnLabels.Count());
            Assert.AreEqual(300m, report[Apr, Other]);
            Assert.AreEqual(800m, report[report.TotalColumn, Other]);
            Assert.AreEqual(800m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void Simple()
        {
            report.SingleSource = Items.Take(13).AsQueryable();
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");

            Assert.AreEqual(3, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(500m, report[report.TotalColumn, Name]);
            Assert.AreEqual(800m, report[report.TotalColumn, Other]);
            Assert.AreEqual(1300m, report[report.TotalColumn, report.TotalRow]);

        }
        [TestMethod]
        public void SimpleJson()
        {
            report.SingleSource = Items.Take(13).AsQueryable();
            report.Build();

            string json = report.ToJson();

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var Name = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Name").Single();
            var Other = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Other").Single();
            var Total = root.EnumerateArray().Where(x => x.GetProperty("IsTotal").GetBoolean()).Single();

            Assert.AreEqual(3, root.GetArrayLength());
            Assert.AreEqual(5, Name.EnumerateObject().Count());
            Assert.AreEqual(500m, Name.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(800m, Other.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(1300m, Total.GetProperty("TOTAL").GetDecimal());
        }
        [TestMethod]
        public void SimpleSorted()
        {
            report.SingleSource = Items.Skip(3).Take(6).AsQueryable();
            report.SortOrder = Report.SortOrders.TotalAscending;
            report.Build();
            report.WriteToConsole(sorted:true);

            var actual = report.RowLabelsOrdered;

            Assert.AreEqual("Other", actual.First().Name);
            Assert.IsTrue(actual.Last().IsTotal);
        }
        [TestMethod]
        public void SubItems()
        {
            report.SingleSource = Items.Skip(9).Take(10).AsQueryable();
            report.Build();

            var Other = GetRow(x => x.Name == "Other");

            Assert.AreEqual(2, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(1000m, report[report.TotalColumn, Other]);
            Assert.AreEqual(1000m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void SubItemsDeep()
        {
            report.SingleSource = Items.Skip(9).Take(10).AsQueryable();
            report.NumLevels = 2;
            report.Build();

            var Other = GetRow(x => x.Name == "Other" && x.Level == 1);
            var Something = GetRow(x => x.Name == "Something" && x.Level == 0);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 0);

            Assert.AreEqual(4, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(1000m, report[report.TotalColumn, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(1000m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void SubItemsDeepSorted()
        {
            report.SingleSource = Items.Skip(9).Take(6).AsQueryable();
            report.NumLevels = 2;
            report.SortOrder = Report.SortOrders.TotalAscending;
            report.Build();
            report.WriteToConsole(sorted: true);

            var actual = report.RowLabelsOrdered.ToList();

            // Default explicit sort order is Descending by Total amount

            Assert.AreEqual("Other", actual.First().Name);
            Assert.AreEqual("Something", actual.Skip(1).First().Name); // Comes in first with 400
            Assert.AreEqual("Else", actual.Skip(2).First().Name); // Second place with 200
            Assert.IsTrue(actual.Last().IsTotal);

        }
        [TestMethod]
        public void SubItemsAllDeep()
        {
            report.SingleSource = Items.Take(19).AsQueryable();
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name" && x.Level == 1);
            var Other = GetRow(x => x.Name == "Other" && x.Level == 1);
            var Something = GetRow(x => x.Name == "Something" && x.Level == 0);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 0);

            Assert.AreEqual(6, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(500m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(1900m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void SubItemsAllDeepJson()
        {
            report.SingleSource = Items.Take(19).AsQueryable();
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            string json = report.ToJson();

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var Name = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Name").Single();
            var Other = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Other").Single();
            var Something = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Something").Single();
            var Else = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Else").Single();
            var Total = root.EnumerateArray().Where(x => x.GetProperty("IsTotal").GetBoolean()).Single();

            Assert.AreEqual(6, root.GetArrayLength());
            Assert.AreEqual(5, Name.EnumerateObject().Count());
            Assert.AreEqual(500m, Name.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(1400m, Other.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(400m, Something.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(600m, Else.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(1900m, Total.GetProperty("TOTAL").GetDecimal());
        }
        [TestMethod]
        public void SubItemsAllDeepSorted()
        {
            report.SingleSource = Items.Take(24).AsQueryable();
            report.NumLevels = 2;
            report.SortOrder = Report.SortOrders.TotalAscending;
            report.Build();
            report.WriteToConsole(sorted:true);

            var actual = report.RowLabelsOrdered.ToList();

            // Default explicit sort order is Descending by Total amount

            Assert.AreEqual("Other", actual.First().Name);
            Assert.AreEqual("Else", actual.Skip(1).First().Name); // First place with 900
            Assert.AreEqual("Name", actual.Skip(4).First().Name); // Last with 600
            Assert.IsTrue(actual.Last().IsTotal);
        }
        [TestMethod]
        public void SubItemsAllThreeDeepSorted()
        {
            report.SingleSource = Items.Take(25).AsQueryable();
            report.NumLevels = 3;
            report.SortOrder = Report.SortOrders.TotalAscending;
            report.Build();
            report.WriteToConsole(sorted: true);

            var actual = report.RowLabelsOrdered.ToList();

            // Default explicit sort order is Descending by Total amount

            Assert.AreEqual("Other", actual.First().Name);
            Assert.AreEqual("Something", actual.Skip(1).First().Name);
            Assert.AreEqual("A", actual.Skip(3).First().Name);
            Assert.AreEqual("X", actual.Skip(6).First().Name);
            Assert.IsTrue(actual.Last().IsTotal);
        }
        [TestMethod]
        public void SubItemsAllDeepCols()
        {
            report.WithMonthColumns = true;
            report.NumLevels = 2;
            report.SingleSource = Items.Take(20).AsQueryable();
            report.Build();
            report.WriteToConsole();

            string json = report.ToJson();

            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var Name = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Name").Single();
            var Other = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Other").Single();
            var Something = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Something").Single();
            var Else = root.EnumerateArray().Where(x => x.GetProperty("Name").GetString() == "Else").Single();
            var Total = root.EnumerateArray().Where(x => x.GetProperty("IsTotal").GetBoolean()).Single();

            Assert.AreEqual(6, root.GetArrayLength());
            Assert.AreEqual(13, Name.EnumerateObject().Count());
            Assert.AreEqual(600m, Name.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(1400m, Other.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(400m, Something.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(600m, Else.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(2000m, Total.GetProperty("TOTAL").GetDecimal());
            Assert.AreEqual(400m, Total.GetProperty("ID:06").GetDecimal());
            Assert.AreEqual(200m, Else.GetProperty("ID:06").GetDecimal());
        }
        [TestMethod]
        public void SubItemsAllDeepColsJson()
        {
            report.WithMonthColumns = true;
            report.NumLevels = 2;
            report.SingleSource = Items.Take(20).AsQueryable();
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name" && x.Level == 1);
            var Other = GetRow(x => x.Name == "Other" && x.Level == 1);
            var Something = GetRow(x => x.Name == "Something" && x.Level == 0);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 0);
            var Jun = GetColumn(x => x.Name == "Jun");

            Assert.AreEqual(6, report.RowLabels.Count());
            Assert.AreEqual(9, report.ColumnLabels.Count());
            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(400m, report[Jun, report.TotalRow]);
            Assert.AreEqual(200m, report[Jun, Else]);
        }
        [TestMethod]
        public void SubItemsFromL1()
        {
            report.SingleSource = Items.Skip(9).Take(10).AsQueryable();
            report.FromLevel = 1;
            report.Build();

            var Something = GetRow(x => x.Name == "Something" && x.Level == 0);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 0);

            Assert.AreEqual(3, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(1000m, report[report.TotalColumn, report.TotalRow]);
        }
        [TestMethod]
        public void SubItemsFromL1Cols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Skip(9).Take(10).AsQueryable();
            report.FromLevel = 1;
            report.NumLevels = 2;
            report.Build();

            var Something = GetRow(x => x.Name == "Something" && x.Level == 1);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 1);
            var A = GetRow(x => x.Name == "A" && x.Level == 0);
            var B = GetRow(x => x.Name == "B" && x.Level == 0);
            var Jun = GetColumn(x => x.Name == "Jun");

            Assert.AreEqual(8, report.RowLabels.Count());
            Assert.AreEqual(6, report.ColumnLabels.Count());
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(300m, report[report.TotalColumn, A]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(1000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(300m, report[Jun, report.TotalRow]);
            Assert.AreEqual(100m, report[Jun, B]);
        }
        [TestMethod]
        public void ThreeLevelsDeepAllCols()
        {
            report.WithMonthColumns = true;
            report.SingleSource = Items.Take(20).AsQueryable();
            report.NumLevels = 3;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name" && x.Level == 2);
            var Other = GetRow(x => x.Name == "Other" && x.Level == 2);
            var Something = GetRow(x => x.Name == "Something" && x.Level == 1);
            var Else = GetRow(x => x.Name == "Else" && x.Level == 1);
            var A = GetRow(x => x.Name == "A" && x.Level == 0);
            var B = GetRow(x => x.Name == "B" && x.Level == 0);
            var Jun = GetColumn(x => x.Name == "Jun");

            Assert.AreEqual(11, report.RowLabels.Count());
            Assert.AreEqual(9, report.ColumnLabels.Count());
            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(400m, report[report.TotalColumn, Something]);
            Assert.AreEqual(300m, report[report.TotalColumn, A]);
            Assert.AreEqual(600m, report[report.TotalColumn, Else]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(400m, report[Jun, report.TotalRow]);
            Assert.AreEqual(100m, report[Jun, B]);
            Assert.AreEqual(200m, report[Jun, Else]);

        }
        //[TestMethod]
        public void ThreeLevelsDeepSorted()
        {
            report.SingleSource = Items.Take(20).AsQueryable();
            report.NumLevels = 3;
            report.Build();
            report.WriteToConsole();

            var sortedrows = report.RowLabelsOrdered;
            Console.WriteLine(string.Join(',', sortedrows.Select(x => x.Name)));
        }

        [TestMethod]
        public void ThreeLevelsDeepLeafs()
        {
            report.SingleSource = Items.Take(20).AsQueryable();
            report.LeafRowsOnly = true;
            report.NumLevels = 3;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name:" && x.Level == 0);
            var Other = GetRow(x => x.Name == "Other:" && x.Level == 0);
            var Else = GetRow(x => x.Name == "Other:Else:" && x.Level == 0);

            Assert.AreEqual(8, report.RowLabels.Count());
            Assert.AreEqual(1, report.ColumnLabels.Count());
            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(200m, report[report.TotalColumn, Else]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
        }


        IEnumerable<IGrouping<string, IReportable>> TwoSeriesSource
        {
            get
            {
                // Divide the transactios into two imbalanced partitions, each partition will be a series
                // ToList() needed to execute the index % 3 calculations NOW not later
                int index = 0;
                var seriesone = new ReportSeries() { Key = "One", Items = Items.Take(20).Where(x => index++ % 3 == 0).ToList() };
                index = 0;
                var seriestwo = new ReportSeries() { Key = "Two", Items = Items.Take(20).Where(x => index++ % 3 != 0).ToList() };

                return new List<ReportSeries>() { seriesone, seriestwo };
            }
        }

        [TestMethod]
        public void TwoSeries()
        {
            report.SeriesSource = TwoSeriesSource;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name" );
            var Other = GetRow(x => x.Name == "Other" );
            var One = GetColumn(x => x.Name == "One");
            var Two = GetColumn(x => x.Name == "Two");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(700m, report[One, report.TotalRow]);
            Assert.AreEqual(1300m, report[Two, report.TotalRow]);
        }
        [TestMethod]
        public void TwoSeriesQuerySource()
        {
            // Contort our data into the form it will take in production use
            report.SeriesQuerySource = TwoSeriesSource.Select(x => x.GroupBy(y => x.Key).AsQueryable());
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");
            var One = GetColumn(x => x.Name == "One");
            var Two = GetColumn(x => x.Name == "Two");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(700m, report[One, report.TotalRow]);
            Assert.AreEqual(1300m, report[Two, report.TotalRow]);
        }
        [TestMethod]
        public void TwoSeriesDeep()
        {
            report.SeriesSource = TwoSeriesSource;
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");
            var Else = GetRow(x => x.Name == "Else");
            var One = GetColumn(x => x.Name == "One");
            var Two = GetColumn(x => x.Name == "Two");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(700m, report[One, report.TotalRow]);
            Assert.AreEqual(1300m, report[Two, report.TotalRow]);
            Assert.AreEqual(200m, report[One, Else]);
            Assert.AreEqual(400m, report[Two, Else]);
        }
        [TestMethod]
        public void TwoSeriesDeepCustomPct()
        {
            report.AddCustomColumn(
                new ColumnLabel()
                {
                    Name = "Pct",
                    UniqueID = "Z",
                    DisplayAsPercent = true,
                    Custom = (cols) => cols["ID:Two"] == 0 ? 0 : cols["ID:One"] / cols["ID:Two"]
                }
            );            
            report.SeriesSource = TwoSeriesSource;
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");
            var Else = GetRow(x => x.Name == "Else");
            var One = GetColumn(x => x.Name == "One");
            var Two = GetColumn(x => x.Name == "Two");
            var Pct = GetColumn(x => x.Name == "Pct");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(0.5m, report[Pct, Name]);
            Assert.AreEqual(5m/9m, report[Pct, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(700m, report[One, report.TotalRow]);
            Assert.AreEqual(1300m, report[Two, report.TotalRow]);
            Assert.AreEqual(200m, report[One, Else]);
            Assert.AreEqual(400m, report[Two, Else]);
        }
        [TestMethod]
        public void TwoSeriesDeepCols()
        {
            report.WithMonthColumns = true;
            report.SeriesSource = TwoSeriesSource;
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");
            var Else = GetRow(x => x.Name == "Else");
            var One = GetColumn(x => x.Name == "One");
            var Two = GetColumn(x => x.Name == "Two");
            var JunTwo = GetColumn(x => x.Name == "Jun Two");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);
            Assert.AreEqual(700m, report[One, report.TotalRow]);
            Assert.AreEqual(1300m, report[Two, report.TotalRow]);
            Assert.AreEqual(400m, report[Two, Else]);
            Assert.AreEqual(200m, report[JunTwo, Else]);
        }
        [TestMethod]
        public void ManySeriesDeep()
        {
            // This crazy test creates a series for every single transaction

            report.SeriesSource = Enumerable.Range(0, 20).Select(i => new ReportSeries() { Key = i.ToString("D2"), Items = Items.Skip(i).Take(1) });
            report.NumLevels = 2;
            report.Build();
            report.WriteToConsole();

            var Name = GetRow(x => x.Name == "Name");
            var Other = GetRow(x => x.Name == "Other");

            Assert.AreEqual(600m, report[report.TotalColumn, Name]);
            Assert.AreEqual(1400m, report[report.TotalColumn, Other]);
            Assert.AreEqual(2000m, report[report.TotalColumn, report.TotalRow]);

            for (int i = 0; i < 20; i++)
            {
                var key = i.ToString("D2");
                var Column = GetColumn(x => x.Name == key);
                Assert.AreEqual(100m, report[Column, report.TotalRow]);
            }
        }
    }
}