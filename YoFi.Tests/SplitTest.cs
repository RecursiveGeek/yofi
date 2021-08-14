﻿using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using YoFi.AspNet.Data;
using YoFi.AspNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YoFi.Tests
{
    [TestClass]
    public class SplitTest
    {
        ApplicationDbContext context;

        [TestInitialize]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ApplicationDbContext")
                .Options;

            context = new ApplicationDbContext(options);
        }

        [TestMethod]
        public void Empty()
        {
            Assert.IsNotNull(context);
        }
        [TestMethod]
        public async Task Includes()
        {
            // Test that we can commit splits with a transaction AND get them back

            var splits = new List<Split>();
            splits.Add(new Split() { Amount = 25m, Category = "A", SubCategory = "B" });
            splits.Add(new Split() { Amount = 75m, Category = "C", SubCategory = "D" });

            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = splits };

            context.Transactions.Add(item);
            context.SaveChanges();

            var actual = await context.Transactions.Include("Splits").ToListAsync();

            Assert.AreEqual(1, actual.Count);
            Assert.AreEqual(2, actual[0].Splits.Count);
            Assert.AreEqual(75m, actual[0].Splits.Where(x => x.Category == "C").Single().Amount);
        }

        [DataTestMethod]
        [DataRow("A:B:C:D:E", "A:B", "C:D:E")]
        [DataRow("A:B:C:D:", "A:B", "C:D")]
        [DataRow("A:B:C:D", "A:B", "C:D")]
        [DataRow("A:B:C:", "A:B", "C")]
        [DataRow("A:B:C", "A:B", "C")]
        [DataRow("A:B:", "A:B", null)]
        [DataRow("A::::B", "A:B", null)]
        [DataRow("A:B", "A:B", null)]
        [DataRow("A", "A", null)]
        public void FixupCategories(string incategory, string expectcategory, string expectsubcategory)
        {
            var split = new Split() { Category = incategory };
            split.FixupCategories();

            Assert.AreEqual(expectcategory, split.Category);
            Assert.AreEqual(expectsubcategory, split.SubCategory);

        }
    }
}