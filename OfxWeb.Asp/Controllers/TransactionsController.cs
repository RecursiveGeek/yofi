﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfxWeb.Asp.Data;
using OfxWeb.Asp.Models;
using Microsoft.AspNetCore.Http;
using OfxSharpLib;

namespace OfxWeb.Asp.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const int pagesize = 100;

        public TransactionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Transactions
        public async Task<IActionResult> Index(string sortOrder, int? page)
        {
            // Sort/Filter: https://docs.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page?view=aspnetcore-2.1

            if (string.IsNullOrEmpty(sortOrder))
                sortOrder = "date_desc";

            ViewData["DateSortParm"] = sortOrder == "date_desc" ? "date_asc" : "date_desc";
            ViewData["PayeeSortParm"] = sortOrder == "payee_asc" ? "payee_desc" : "payee_asc";
            ViewData["CategorySortParm"] = sortOrder == "category_asc" ? "category_desc" : "category_asc";
            ViewData["AmountSortParm"] = sortOrder == "category_asc" ? "category_desc" : "category_asc";
            ViewData["BankReferenceSortParm"] = sortOrder == "ref_asc" ? "ref_desc" : "ref_asc";

            if (!page.HasValue)
                page = 1;

            var result = from s in _context.Transactions
                           select s;

            switch (sortOrder)
            {
                case "amount_asc":
                    result = result.OrderBy(s => s.Amount);
                    break;
                case "amount_desc":
                    result = result.OrderByDescending(s => s.Amount);
                    break;
                case "ref_asc":
                    result = result.OrderBy(s => s.BankReference);
                    break;
                case "ref_desc":
                    result = result.OrderByDescending(s => s.BankReference);
                    break;
                case "payee_asc":
                    result = result.OrderBy(s => s.Payee);
                    break;
                case "payee_desc":
                    result = result.OrderByDescending(s => s.Payee);
                    break;
                case "category_asc":
                    result = result.OrderBy(s => s.Category);
                    break;
                case "category_desc":
                    result = result.OrderByDescending(s => s.Category);
                    break;
                case "date_asc":
                    result = result.OrderBy(s => s.Timestamp);
                    break;
                case "date_desc":
                default:
                    result = result.OrderByDescending(s => s.Timestamp);
                    break;
            }

            var count = await result.CountAsync();

            if (count > pagesize)
            {
                result = result.Skip((page.Value - 1) * pagesize).Take(pagesize);

                if (page.Value > 1)
                    ViewData["PreviousPage"] = page.Value - 1;
                if (page * pagesize < count )
                    ViewData["NextPage"] = page.Value + 1;

                ViewData["CurrentSort"] = sortOrder;
            }

            return View(await result.AsNoTracking().ToListAsync());
        }

        // GET: Transactions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transaction = await _context.Transactions
                .SingleOrDefaultAsync(m => m.ID == id);
            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        // GET: Transactions/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Transactions/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Amount,Memo,Payee,Category,SubCategory,BankReference")] Models.Transaction transaction)
        {
            if (ModelState.IsValid)
            {
                _context.Add(transaction);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(transaction);
        }

        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transaction = await _context.Transactions.SingleOrDefaultAsync(m => m.ID == id);
            if (transaction == null)
            {
                return NotFound();
            }
            return View(transaction);
        }

        // POST: Transactions/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Amount,Memo,Payee,Category,SubCategory,BankReference")] Models.Transaction transaction)
        {
            if (id != transaction.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(transaction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TransactionExists(transaction.ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(transaction);
        }

        // GET: Transactions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var transaction = await _context.Transactions
                .SingleOrDefaultAsync(m => m.ID == id);
            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        // POST: Transactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var transaction = await _context.Transactions.SingleOrDefaultAsync(m => m.ID == id);
            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Upload(List<IFormFile> files)
        {
            var incoming = new HashSet<Models.Transaction>(new TransactionBankReferenceComparer());
            try
            {

                // Build the submitted file into a list of transactions

                foreach (var formFile in files)
                {
                    using (var stream = formFile.OpenReadStream())
                    {
                        var parser = new OfxDocumentParser();
                        var Document = parser.Import(stream);

                        await Task.Run(() => 
                        {
                            foreach (var tx in Document.Transactions)
                            {
                                incoming.Add(new Models.Transaction() { Amount = tx.Amount, Payee = tx.Memo, BankReference = tx.ReferenceNumber.Trim(), Timestamp = tx.Date });
                            }
                        });
                    }
                }

                // Query for matching transactions.

                var keys = incoming.Select(x => x.BankReference).ToHashSet();

                var existing = await _context.Transactions.Where(x => keys.Contains(x.BankReference)).ToListAsync();

                // Removed duplicate transactions.

                incoming.ExceptWith(existing);

                // Add resulting transactions

                await _context.AddRangeAsync(incoming);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }

            return View(incoming.OrderByDescending(x=>x.Timestamp));
        }

        private bool TransactionExists(int id)
        {
            return _context.Transactions.Any(e => e.ID == id);
        }
    }

    class TransactionBankReferenceComparer : IEqualityComparer<Models.Transaction>
    {
        public bool Equals(Models.Transaction x, Models.Transaction y)
        {
            return x.BankReference == y.BankReference;
        }

        public int GetHashCode(Models.Transaction obj)
        {
            int result;
            if (!int.TryParse(obj.BankReference,out result))
            {
                result = obj.BankReference.GetHashCode();
            }
            return result;
        }
    }
}
