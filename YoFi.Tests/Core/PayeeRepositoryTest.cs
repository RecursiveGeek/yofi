﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using YoFi.AspNet.Controllers;
using YoFi.Core.Models;
using YoFi.Core.Importers;
using YoFi.Core.Repositories;
using YoFi.Tests.Helpers;
using System.Threading.Tasks;
using System.Linq;
using YoFi.Tests.Common;

namespace YoFi.Tests.Core
{
    [TestClass]
    public class PayeeRepositoryTest : BaseRepositoryTest<Payee>
    {
        protected override List<Payee> Items => new List<Payee>()
        {
                    new Payee() { ID = 1, Category = "B", Name = "3" },
                    new Payee() { ID = 2, Category = "A", Name = "2" },
                    new Payee() { ID = 3, Category = "C", Name = "5" },
                    new Payee() { ID = 4, Category = "A", Name = "1" },
                    new Payee() { ID = 5, Category = "B", Name = "4" },

                    new Payee() { Category = "ABCD", Name = "5" },
                    new Payee() { Category = "X", Name = "6" }
        };

        protected override int CompareKeys(Payee x, Payee y) => x.Name.CompareTo(y.Name);

        PayeeRepository payeeRepository => base.repository as PayeeRepository;

        [TestInitialize]
        public void SetUp()
        {
            context = new MockDataContext();
            repository = new PayeeRepository(context);
        }

        [TestMethod]
        public async Task BulkEdit()
        {
            // Given: Five items in the data set
            var all = Items.Take(5);

            // And: A subset of the items are selected
            var subset = all.Take(2);
            foreach (var item in subset)
                item.Selected = true;
            context.AddRange(all);

            // When: Applying bulk edit to the repository using a new category
            var newcategory = "New Category";
            await payeeRepository.BulkEdit(newcategory);

            // Then: The selected items have the new category
            Assert.IsTrue(subset.All(x => x.Category == newcategory));

            // And: The other items are unchanged
            Assert.IsTrue(all.Except(subset).All(x => x.Category != newcategory));

            // And: All items are unselected
            Assert.IsTrue(all.All(x => x.Selected != true));
        }

        [TestMethod]
        public async Task BulkEditNoChange()
        {
            // Given: Five items in the data set
            var all = Items.Take(5);

            // And: Taking a snapshot of that data for later comparison
            var expected = await DeepCopy.MakeDuplicateOf(all);

            // And: A subset of the items are selected
            var subset = all.Take(2);
            foreach (var item in subset)
                item.Selected = true;
            context.AddRange(all);

            // When: Applying bulk edit to the repository using null
            await payeeRepository.BulkEdit(null);

            // And: All items are unchanged
            Assert.IsTrue(expected.SequenceEqual(repository.All));

            // And: All items are unselected
            Assert.IsTrue(all.All(x => x.Selected != true));
        }

        [TestMethod]
        public async Task BulkDelete()
        {
            // Given: Five items in the data set
            var numtotalitems = 5;
            var all = Items.Take(numtotalitems);

            // And: A subset of the items are selected
            var numdeleteditems = 2;
            var subset = all.Take(numdeleteditems);
            foreach (var item in subset)
                item.Selected = true;
            context.AddRange(all);

            // When: Bulk deleting the selected items
            await payeeRepository.BulkDelete();

            // Then: The number of items is total minus deleted
            Assert.AreEqual(numtotalitems-numdeleteditems,repository.All.Count());

            // And: All repository items are unselected
            Assert.IsTrue(repository.All.All(x => x.Selected != true));
        }

        [TestMethod]
        public async Task NewFromTransaction()
        {
            // Given: A transaction in the DB
            const string payee = "Payee";
            const string category = "Category";
            var transaction = new Transaction() { ID = 1, Payee = payee, Category = category };
            await context.AddAsync(transaction);

            // When: Creating a new payee from that transaction
            var actual = await payeeRepository.NewFromTransaction(transaction.ID);

            // Then: The resulting payee matches category & payee name
            Assert.AreEqual(payee, actual.Name);
            Assert.AreEqual(category, actual.Category);
        }
    }
}