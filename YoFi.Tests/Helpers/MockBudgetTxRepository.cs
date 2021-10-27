﻿using jcoliz.OfficeOpenXml.Serializer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoFi.AspNet.Core.Repositories;
using YoFi.AspNet.Models;

namespace YoFi.Tests.Helpers
{
    class MockBudgetTxRepository : IRepository<BudgetTx>
    {
        public void AddItems(int numitems) => Items.AddRange(MakeItems(numitems));

        static readonly DateTime defaulttimestamp = new DateTime(2020, 1, 1);

        public static BudgetTx MakeItem(int x) => new BudgetTx() { ID = x, Amount = x, Category = x.ToString(), Timestamp = defaulttimestamp };

        public static IEnumerable<BudgetTx> MakeItems(int numitems) => Enumerable.Range(1, numitems).Select(MakeItem);

        public bool Ok
        {
            get
            {
                if (!_Ok)
                    throw new Exception("Failed");
                return _Ok;
            }
            set
            {
                _Ok = value;
            }
        }
        public bool _Ok = true;

        public List<BudgetTx> Items { get; } = new List<BudgetTx>();

        public IQueryable<BudgetTx> All => Items.AsQueryable();

        public IQueryable<BudgetTx> OrderedQuery => throw new System.NotImplementedException();

        public Task AddAsync(BudgetTx item)
        {
            if (Ok)
                Items.Add(item);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<BudgetTx> items)
        {
            if (Ok)
                Items.AddRange(items);
            return Task.CompletedTask;
        }

        public Stream AsSpreadsheet()
        {
            if (!Ok)
                return null;

            var items = All;

            var stream = new MemoryStream();
            using (var ssw = new SpreadsheetWriter())
            {
                ssw.Open(stream);
                ssw.Serialize(items);
            }

            stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }

        public IQueryable<BudgetTx> ForQuery(string q) => string.IsNullOrEmpty(q) ? All : All.Where(x => x.Category.Contains(q));

        public Task<BudgetTx> GetByIdAsync(int? id) => Ok ? Task.FromResult(All.Single(x => x.ID == id.Value)) : Task.FromResult<BudgetTx>(null);

        public Task RemoveAsync(BudgetTx item)
        {
            if (!Ok)
                throw new Exception("Failed");

            if (item == null)
                throw new ArgumentException("Expected non-null item");

            var index = Items.FindIndex(x => x.ID == item.ID);
            Items.RemoveAt(index);

            return Task.CompletedTask;
        }

        public Task<bool> TestExistsByIdAsync(int id)
        {
            throw new System.NotImplementedException();
        }

        public Task UpdateAsync(BudgetTx item)
        {
            if (!Ok)
                throw new Exception("Failed");

            if (item == null)
                throw new ArgumentException("Expected non-null item");

            var index = Items.FindIndex(x => x.ID == item.ID);
            Items[index] = item;

            return Task.CompletedTask;
        }
    }
}