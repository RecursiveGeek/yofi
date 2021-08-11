﻿using Common.AspNetCore.Test;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OfxWeb.Asp.Controllers;
using OfxWeb.Asp.Data;
using OfxWeb.Asp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ofx.Tests
{
    [TestClass]

    public class BudgetTxControllerTest
    {
        private ControllerTestHelper<BudgetTx, BudgetTxsController> helper = null;

        BudgetTxsController controller => helper.controller;
        ApplicationDbContext context => helper.context;
        List<BudgetTx> Items => helper.Items;
        DbSet<BudgetTx> dbset => helper.dbset;

        List<BudgetTx> BudgetTxItems = new List<BudgetTx>()
        {
            new BudgetTx() { Timestamp = new System.DateTime(2020, 06, 01),  Category = "A", Amount = 100m },
            new BudgetTx() { Timestamp = new System.DateTime(2020, 06, 01),  Category = "B", Amount = 200m },
            new BudgetTx() { Timestamp = new System.DateTime(2020, 05, 01),  Category = "A", Amount = 300m },
            new BudgetTx() { Timestamp = new System.DateTime(2020, 05, 01),  Category = "B", Amount = 400m },
            new BudgetTx() { Timestamp = new System.DateTime(2020, 05, 01),  Category = "C", Amount = 500m },
        };

        IEnumerable<BudgetTx> ItemsLong;

        IEnumerable<BudgetTx> GetItemsLong()
        {
            if (null == ItemsLong)
            {
                int count = 200;
                DateTime origin = new DateTime(2020, 1, 1).AddMonths(-count);
                ItemsLong = Enumerable.Range(1, 200).Select(x => new BudgetTx() { Timestamp = origin.AddMonths(x), Category = x.ToString(), Amount = x * 100 }).ToList();
            }
            return ItemsLong;
        }

        [TestInitialize]
        public void SetUp()
        {
            helper = new ControllerTestHelper<BudgetTx, BudgetTxsController>();
            helper.SetUp();
            helper.controller = new BudgetTxsController(helper.context);
            helper.Items.AddRange(BudgetTxItems.Take(5));

            helper.dbset = helper.context.BudgetTxs;

            // Sample data items will use 'Name' as a unique sort idenfitier
            helper.KeyFor = (x => x.Amount.ToString());
        }

        [TestCleanup]
        public void Cleanup() => helper.Cleanup();
        [TestMethod]
        public void Empty() => helper.Empty();
        [TestMethod]
        public async Task IndexEmpty() => await helper.IndexEmpty();
        [TestMethod]
        public async Task IndexSingle() => await helper.IndexSingle();
        [TestMethod]
        public async Task IndexMany() => await helper.IndexMany();
        [TestMethod]
        public async Task DetailsFound() => await helper.DetailsFound();
        [TestMethod]
        public async Task DetailsNotFound() => await helper.DetailsNotFound();
        [TestMethod]
        public async Task EditFound() => await helper.EditFound();
        [TestMethod]
        public async Task EditNotFound() => await helper.EditNotFound();
        [TestMethod]
        public async Task Create() => await helper.Create();
        [TestMethod]
        public async Task EditObjectValues() => await helper.EditObjectValues();
        [TestMethod]
        public async Task DeleteFound() => await helper.DeleteFound();
        [TestMethod]
        public async Task DeleteConfirmed() => await helper.DeleteConfirmed();
        [TestMethod]
        public async Task Download() => await helper.Download();
        [TestMethod]
        public async Task Upload() => await helper.Upload();
        [TestMethod]
        public async Task UploadWithID() => await helper.UploadWithID();
        [TestMethod]
        public async Task UploadDuplicate() => await helper.UploadDuplicate();
        [TestMethod]
        public async Task UploadAddNewDuplicate()
        {
            // These items are not EXACTLY duplicates, just duplicate enough to trigger the
            // hashset equality constraint on input.

            // Start with a full set of data
            await helper.AddFiveItems();

            // Add some new items, and upload all of it.
            // I think this shows the behaviour described in
            // Product Backlog Item #769: De-dupe BudgetTxs on import
            helper.Items.Add(new BudgetTx() { Timestamp = new System.DateTime(2020, 07, 01), Category = "A", Amount = 600m });
            helper.Items.Add(new BudgetTx() { Timestamp = new System.DateTime(2020, 07, 01), Category = "B", Amount = 700m });
            helper.Items.Add(new BudgetTx() { Timestamp = new System.DateTime(2020, 07, 01), Category = "C", Amount = 800m });

            // Now upload all the items. What should happen here is that only items 1-4 (not 0) get
            // uploaded, because item 0 is already there, so it gets removed as a duplicate.
            var actual = await helper.Upload(8, 3);

            // Let's make sure all three are the new items
            var findinitial = actual.Where(x => x.Timestamp.Month == 7);

            Assert.AreEqual(3, findinitial.Count());
        }
        [TestMethod]
        public async Task UploadMinmallyDuplicate()
        {
            // These items are not EXACTLY duplicates, just duplicate enough to trigger the
            // hashset equality constraint on input.

            // *** This test has ALWAYS failed, we just didn't know it becasue we weren't
            // detaching. But Why did we excpect this to work? Apparantly at one point we
            // thought that BudgetTx wasn't checking for amount sameness.
            //
            // Hmm, that's probably right? If you have same timestamp/amount, you probabbly
            // don't want TWO of the same. 
            //
            // OK so as the fix for #890, I removed 'amount' from the equality test for
            // Budget Txs.

            // Start with a full set of data
            await helper.AddFiveItems();

            // Detach, otherwise the next line will effectively update the DB
            helper.context.Entry(helper.Items[0]).State = EntityState.Detached;
            helper.context.Entry(helper.Items[1]).State = EntityState.Detached;
            helper.context.Entry(helper.Items[2]).State = EntityState.Detached;

            // Make some changes to the amounts
            helper.Items[0].Amount = 1000m;
            helper.Items[1].Amount = 2000m;
            helper.Items[2].Amount = 3000m;

            // Upload these three. They should be rejected.
            var actual = await helper.Upload(3, 0);
        }
        [TestMethod]
        public async Task Bug890()
        {
            // Bug 890: BudgetTxs upload fails to filter duplicates when source data has >2 digits
            // Hah, this is fixed by getting UploadMinmallyDuplicate() test to properly pass.

            // Start with a full set of data
            await helper.AddFiveItems();

            // Detach, otherwise the next line will effectively update the DB
            helper.context.Entry(helper.Items[0]).State = EntityState.Detached;

            // Make small changes to the amounts
            helper.Items[0].Amount += 0.001m;

            // Upload it. It should be rejected.
            var actual = await helper.Upload(1, 0);
        }


        [TestMethod]
        public async Task IndexPage1()
        {
            // Given: A very long set of items 
            var items = GetItemsLong();
            dbset.AddRange(items);
            context.SaveChanges();

            // When: Calling Index page 1
            var result = await controller.Index(p: 1);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as List<BudgetTx>;

            // Then: Only one page's worth of items are returned
            Assert.AreEqual(PayeesController.PageSize, model.Count);

            // And: Page Item values are as expected
            Assert.AreEqual(1, viewresult.ViewData["PageFirstItem"]);
            Assert.AreEqual(PayeesController.PageSize, viewresult.ViewData["PageLastItem"]);
            Assert.AreEqual(items.Count(), viewresult.ViewData["PageTotalItems"]);
        }

        [TestMethod]
        public async Task IndexPage2()
        {
            // Given: A long set of items, which is longer than one page, but not as long as two pages 
            var itemcount = BudgetTxsController.PageSize + PayeesController.PageSize / 2;
            dbset.AddRange(GetItemsLong().Take(itemcount));
            context.SaveChanges();

            // When: Calling Index page 2
            var result = await controller.Index(p: 2);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as List<BudgetTx>;

            // Then: Only items after one page's worth of items are returned
            Assert.AreEqual(BudgetTxsController.PageSize / 2, model.Count);

            // And: Page Item values are as expected
            Assert.AreEqual(1 + BudgetTxsController.PageSize, viewresult.ViewData["PageFirstItem"]);
            Assert.AreEqual(itemcount, viewresult.ViewData["PageLastItem"]);
            Assert.AreEqual(itemcount, viewresult.ViewData["PageTotalItems"]);
        }

        [TestMethod]
        public async Task IndexQAny()
        {
            // Given: A mix of transactions, some with '{word}' in their category, memo, or payee and some without
            var items = BudgetTxItems.Take(5);
            dbset.AddRange(items);
            context.SaveChanges();

            // When: Calling index q={word}
            var word = "A";
            var result = await controller.Index(q: word);
            var actual = result as ViewResult;
            var model = actual.Model as List<BudgetTx>;

            // Then: Only the items with '{word}' in their category are returned
            var expected = items.Where(x=>x.Category.Contains(word)).ToList();
            CollectionAssert.AreEquivalent(expected, model);
        }


        // TODO: Generate next month's TXs
    }
}
