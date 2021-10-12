﻿using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoFi.AspNet.Data;
using YoFi.AspNet.Pages;
using YoFi.Tests.Helpers;

namespace YoFi.Tests
{
    [TestClass]
    public class PagesTest
    {
        private ApplicationDbContext context;

        [TestInitialize]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ApplicationDbContext")
                .Options;

            context = new ApplicationDbContext(options);
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
            // Given: A complete sample data set
            await SampleDataStore.LoadSingleAsync();
            context.Transactions.AddRange(SampleDataStore.Single.Transactions);
            context.SaveChanges();

            // When: Getting the "Reports" Page
            var reportspage = new ReportsModel(context);
            reportspage.OnGet(new AspNet.Controllers.Reports.ReportBuilder.Parameters() { year = 2021, month = 12 });

            // Then: All the totals are as expected
            var totals = reportspage.Reports.SelectMany(x => x).ToDictionary(x => x.Name, x => x.GrandTotal);
            Assert.AreEqual(149000.08m, totals["Income"]);
            Assert.AreEqual(-31872m, totals["Taxes"]);
            Assert.AreEqual(-64619.77m, totals["Expenses"]);
            Assert.AreEqual(-32600.16m, totals["Savings"]);
        }
    }
}
