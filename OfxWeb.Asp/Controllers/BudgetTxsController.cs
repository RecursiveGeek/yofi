﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfxWeb.Asp.Data;
using OfxWeb.Asp.Models;

namespace OfxWeb.Asp.Controllers
{
    public class BudgetTxsController : Controller, IController<BudgetTx>
    {
        private readonly ApplicationDbContext _context;

        public BudgetTxsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BudgetTxs
        public async Task<IActionResult> Index()
        {
            // Assemble the results
            var result = await _context.BudgetTxs.OrderByDescending(x => x.Timestamp.Year).ThenByDescending(x => x.Timestamp.Month).ThenBy(x => x.Category).ToListAsync();

            if (result.FirstOrDefault() != null)
            {
                var nextmonth = result.First().Timestamp.AddMonths(1);
                ViewData["LastMonth"] = $"Generate {nextmonth:MMMM} Budget";
            }

            // Show the index
            return View(result);
        }

        // GET: BudgetTxs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var budgetTx = await _context.BudgetTxs
                .SingleOrDefaultAsync(m => m.ID == id);
            if (budgetTx == null)
            {
                return NotFound();
            }

            return View(budgetTx);
        }

        // GET: BudgetTxs/Generate
        public async Task<IActionResult> Generate()
        {
            // Grab the whole first group
            var result = await _context.BudgetTxs.GroupBy(x => x.Timestamp).OrderByDescending(x => x.Key).FirstOrDefaultAsync();

            if (null == result)
                return NotFound();

            var timestamp = result.First().Timestamp.AddMonths(1);
            var newmonth = result.Select(x => new BudgetTx(x, timestamp));

            await _context.BudgetTxs.AddRangeAsync(newmonth);
            await _context.SaveChangesAsync();

            return Redirect("/BudgetTxs");
        }

        // GET: BudgetTxs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: BudgetTxs/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,Amount,Timestamp,Category")] BudgetTx budgetTx)
        {
            if (ModelState.IsValid)
            {
                _context.Add(budgetTx);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(budgetTx);
        }

        // GET: BudgetTxs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var budgetTx = await _context.BudgetTxs.SingleOrDefaultAsync(m => m.ID == id);
            if (budgetTx == null)
            {
                return NotFound();
            }
            return View(budgetTx);
        }

        // POST: BudgetTxs/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ID,Amount,Timestamp,Category")] BudgetTx budgetTx)
        {
            if (id != budgetTx.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(budgetTx);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BudgetTxExists(budgetTx.ID))
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
            return View(budgetTx);
        }

        // GET: BudgetTxs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var budgetTx = await _context.BudgetTxs
                .SingleOrDefaultAsync(m => m.ID == id);
            if (budgetTx == null)
            {
                return NotFound();
            }

            return View(budgetTx);
        }

        // POST: BudgetTxs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var budgetTx = await _context.BudgetTxs.SingleOrDefaultAsync(m => m.ID == id);
            _context.BudgetTxs.Remove(budgetTx);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> Upload(List<IFormFile> files)
        {
            var incoming = new HashSet<Models.BudgetTx>(new BudgetTxComparer());
            IEnumerable<BudgetTx> result = Enumerable.Empty<BudgetTx>();
            try
            {
                foreach (var formFile in files)
                {
                    if (formFile.FileName.ToLower().EndsWith(".xlsx"))
                    {
                        using (var stream = formFile.OpenReadStream())
                        {
                            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                            var excel = new ExcelPackage(stream);
                            var worksheet = excel.Workbook.Worksheets.Where(x => x.Name == "BudgetTxs").Single();
                            worksheet.ExtractInto(incoming);
                        }
                    }
                }

                // Remove duplicate transactions.
                result = incoming.Except(_context.BudgetTxs).ToList();

                // Add remaining transactions
                await _context.AddRangeAsync(result);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }

            return View(result.OrderBy(x => x.Timestamp.Year).ThenBy(x=>x.Timestamp.Month).ThenBy(x => x.Category));
        }

        // GET: Transactions/Download
        [ActionName("Download")]
        public async Task<IActionResult> Download()
        {
            const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            try
            {
                var objecttype = "BudgetTxs";
                var transactions = await _context.BudgetTxs.OrderBy(x => x.Timestamp).ThenBy(x=>x.Category).ToListAsync();

                byte[] reportBytes;
                using (var package = new ExcelPackage())
                {
                    package.Workbook.Properties.Title = objecttype;
                    package.Workbook.Properties.Author = "coliz.com";
                    package.Workbook.Properties.Subject = objecttype;
                    package.Workbook.Properties.Keywords = objecttype;

                    var worksheet = package.Workbook.Worksheets.Add(objecttype);
                    int rows, cols;
                    worksheet.PopulateFrom(transactions, out rows, out cols);

                    var tbl = worksheet.Tables.Add(new ExcelAddressBase(fromRow: 1, fromCol: 1, toRow: rows, toColumn: cols), objecttype);
                    tbl.ShowHeader = true;
                    tbl.TableStyle = OfficeOpenXml.Table.TableStyles.Dark9;

                    reportBytes = package.GetAsByteArray();
                }

                return File(reportBytes, XlsxContentType, $"{objecttype}.xlsx");
            }
            catch (Exception)
            {
                return NotFound();
            }
        }


        private bool BudgetTxExists(int id)
        {
            return _context.BudgetTxs.Any(e => e.ID == id);
        }
    }

    class BudgetTxComparer: IEqualityComparer<Models.BudgetTx>
    {
        public bool Equals(Models.BudgetTx x, Models.BudgetTx y) => x.Timestamp.Year == y.Timestamp.Year && x.Timestamp.Month == y.Timestamp.Month && x.Category == y.Category;
        public int GetHashCode(Models.BudgetTx obj) => (obj.Timestamp.Year * 12 + obj.Timestamp.Month ) ^ obj.Category.GetHashCode();
    }
}
