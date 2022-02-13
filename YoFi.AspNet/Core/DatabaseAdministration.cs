﻿using Common.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoFi.Core.Models;

namespace YoFi.Core
{
    public class DatabaseAdministration : IDatabaseAdministration
    {
        public const string TestMarker = "__test__";

        private readonly IDataContext _context;
        private readonly IClock _clock;

        public DatabaseAdministration(IDataContext context, IClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task ClearDatabaseAsync(string id)
        {
            if ("budget" == id)
            {
                await _context.ClearAsync<BudgetTx>();
            }
            else if ("tx" == id)
            {
                await _context.ClearAsync<Transaction>();
            }
            else if ("payee" == id)
            {
                await _context.ClearAsync<Payee>();
            }
            else
            {
                // TODO: Return an error
            }
        }

        public async Task ClearTestDataAsync(string id)
        {
            if (id.Contains("payee") || "all" == id)
                _context.RemoveRange(_context.Payees.Where(x => x.Category.Contains(TestMarker)));

            if (id.Contains("budgettx") || "all" == id)
                _context.RemoveRange(_context.BudgetTxs.Where(x => x.Category.Contains(TestMarker)));

            if (id.Contains("trx") || "all" == id)
            {
                _context.RemoveRange(_context.Transactions.Where(x => x.Category.Contains(TestMarker) || x.Memo.Contains(TestMarker) || x.Payee.Contains(TestMarker)));
                _context.RemoveRange(_context.Splits.Where(x => x.Category.Contains(TestMarker)));
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IDatabaseStatus> GetDatabaseStatus()
        {
            var result = new DatabaseStatus()
            {
                NumTransactions = await _context.CountAsync(_context.Transactions),
                NumBudgetTxs = await _context.CountAsync(_context.BudgetTxs),
                NumPayees = await _context.CountAsync(_context.Payees),
            };
            result.IsEmpty = result.NumTransactions == 0 && result.NumBudgetTxs == 0 && result.NumPayees == 0;

            return result;
        }

        public async Task UnhideTransactionsToToday()
        {
            var unhideme = _context.Transactions.Where(x => x.Timestamp <= _clock.Now && x.Hidden == true);
            var any = await _context.AnyAsync(unhideme);
            if (any)
            {
                foreach (var t in unhideme)
                    t.Hidden = false;
                await _context.SaveChangesAsync();
            }
        }
    }

    public class DatabaseStatus : IDatabaseStatus
    {
        public bool IsEmpty { get; set; }

        public int NumTransactions { get; set; }

        public int NumBudgetTxs { get; set; }

        public int NumPayees { get; set; }
    }
}
