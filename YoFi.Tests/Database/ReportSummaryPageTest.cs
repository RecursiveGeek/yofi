﻿using Common.DotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;
using YoFi.AspNet.Data;
using YoFi.AspNet.Pages;
using YoFi.Core.Reports;
using YoFi.Tests.Helpers;

namespace YoFi.Tests.Database
{
    /// <summary>
    /// Test the "Reports" page
    /// </summary>
    /// <remarks>
    /// Theoretically it should be possible to rewrite this test without the database layer.
    /// The problem lies in QueryBuilder, which has a pretty explicit tie to a relational
    /// database structure, in the interaction between Splits and Transactions.
    /// 
    /// This might be a good case for refactoring the model so that there is always one split
    /// per transaction. Maybe. That would take out some of the variability.
    /// </remarks>
    [TestClass]
    public class ReportSummaryPageTest
    {
        private ApplicationDbContext context;
        private TestClock clock;

        [TestInitialize]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ApplicationDbContext")
                .Options;

            context = new ApplicationDbContext(options);

            // By default it's 2021, which is the year all our sample data is generated for
            clock = new TestClock() { Now = new System.DateTime(2021, 1, 1) };
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Didn't actually solve anything. Keep it around for possible future problem
            //DetachAllEntities();

            // https://stackoverflow.com/questions/33490696/how-can-i-reset-an-ef7-inmemory-provider-between-unit-tests
            context?.Database.EnsureDeleted();
            context = null;
        }

        [TestMethod]
        public async Task ReportsGet()
        {
            // TODO: Need to make a separate test that checks against only having transactions in the previous year

            // Given: A complete sample data set
            await SampleDataStore.LoadSingleAsync();
            context.Transactions.AddRange(SampleDataStore.Single.Transactions);
            context.SaveChanges();

            // When: Getting the "Reports" Page
            var reportspage = new ReportsModel(new ReportBuilder(context,clock),clock);
            reportspage.OnGet(new ReportParameters() { year = 2021, month = 12 });

            // Then: All the totals are as expected
            var totals = reportspage.Reports.SelectMany(x => x).ToDictionary(x => x.Name, x => x.GrandTotal);
            Assert.AreEqual(149000.08m, totals["Income"]);
            Assert.AreEqual(-31872m, totals["Taxes"]);
            Assert.AreEqual(-64619.77m, totals["Expenses"]);
            Assert.AreEqual(-32600.16m, totals["Explicit Savings"]);
        }
        [TestMethod]
        public async Task ReportsGetNewYear()
        {
            // Given: A complete sample data set
            await SampleDataStore.LoadSingleAsync();
            context.Transactions.AddRange(SampleDataStore.Single.Transactions);
            context.SaveChanges();

            // When: Getting the "Reports" Page for the NEXT year, where there is no data
            var reportspage = new ReportsModel(new ReportBuilder(context,clock),clock);
            reportspage.OnGet(new ReportParameters() { year = 2022, month = 12 });

            // Then: All the totals are as expected
            var totals = reportspage.Reports.SelectMany(x => x).ToDictionary(x => x.Name, x => x.GrandTotal);
            Assert.AreEqual(0, totals["Income"]);
            Assert.AreEqual(0, totals["Taxes"]);
            Assert.AreEqual(0, totals["Expenses"]);
            Assert.AreEqual(0, totals["Explicit Savings"]);
        }
    }
}
