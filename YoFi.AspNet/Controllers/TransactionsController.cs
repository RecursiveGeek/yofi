﻿using Ardalis.Filters;
using Common.AspNet;
using Common.DotNet;
using Common.NET.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoFi.AspNet.Boilerplate.Models;
using YoFi.AspNet.Pages.Helpers;
using YoFi.Core;
using YoFi.Core.Importers;
using YoFi.Core.Repositories;
using YoFi.Core.Repositories.Wire;
using YoFi.Core.SampleGen;
using Transaction = YoFi.Core.Models.Transaction;

namespace YoFi.AspNet.Controllers
{
    [Authorize(Policy = "CanRead")]
    public class TransactionsController : Controller, IController<Transaction>
    {
        #region Constructor

        public TransactionsController(ITransactionRepository repository, IAsyncQueryExecution queryExecution, IClock clock)
        {
            _repository = repository;
            _queryExecution = queryExecution;
            _clock = clock;
        }

        #endregion

        #region Action Handlers: Create

        /// <summary>
        /// Create a new split for the specified transaction <paramref name="id"/>
        /// </summary>
        /// <param name="id">ID of the transaction</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanWrite")]
        [ValidateTransactionExists]
        public async Task<IActionResult> CreateSplit(int id)
        {
            var result = await _repository.AddSplitToAsync(id);

            return RedirectToAction("Edit", "Splits", new { id = result });
        }

        /// <summary>
        /// View for create transaction page, which is empty because we're creating a transaction from empty
        /// </summary>
        /// <returns></returns>
        public Task<IActionResult> Create()
        {
            return Task.FromResult(View() as IActionResult);
        }

        /// <summary>
        /// Actually create the given <paramref name="transaction"/>
        /// </summary>
        /// <param name="transaction">FUlly-formed transaction to create</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanWrite")]
        [ValidateModel]
        public async Task<IActionResult> Create([Bind("ID,Timestamp,Amount,Memo,Payee,Category,SubCategory,BankReference")] Transaction transaction)
        {
            await _repository.AddAsync(transaction);
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Action Handlers: Read (Index, Details)

        /// <summary>
        /// Fetch list of transactions for display
        /// </summary>
        /// <param name="o">Order of transactions</param>
        /// <param name="p">Page number, where 1 is first page</param>
        /// <param name="q">Query (or filter) specifying which transactions</param>
        /// <param name="v">View modifiers, specifying how the view should look</param>
        /// <returns></returns>
        public async Task<IActionResult> Index(string o = null, int? p = null, string q = null, string v = null)
        {
            try
            {
                var qresult = await _repository.GetByQueryAsync(new WireQueryParameters() { Query = q, Page = p, View = v, Order = o });
                var viewmodel = new TransactionsIndexPresenter(qresult);
                return View(viewmodel);
            }
            catch (ArgumentException)
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Retrieve a single transaction
        /// </summary>
        /// <param name="id">ID of the desired transaction</param>
        /// <returns></returns>
        [ValidateTransactionExists]
        public async Task<IActionResult> Details(int? id)
        {
            return View(await _repository.GetByIdAsync(id));
        }

        #endregion

        #region Action Handlers: Update (Edit)

        [ValidateTransactionExists]
        public async Task<IActionResult> Edit(int? id, [FromServices] IPayeeRepository payeeRepository)
        {
            var transaction = await _repository.GetWithSplitsByIdAsync(id);
            if (string.IsNullOrEmpty(transaction.Category))
            {
                var category = await payeeRepository.GetCategoryMatchingPayeeAsync(transaction.StrippedPayee);
                if (category != null)
                {
                    transaction.Category = category;
                    ViewData["AutoCategory"] = true;
                }
            }

            return View(transaction);
        }

        [ValidateTransactionExists]
        public async Task<IActionResult> EditModal(int? id, [FromServices] IPayeeRepository payeeRepository)
        {
            var transaction = await _repository.GetWithSplitsByIdAsync(id);
            if (string.IsNullOrEmpty(transaction.Category))
            {
                var category = await payeeRepository.GetCategoryMatchingPayeeAsync(transaction.StrippedPayee);
                if (category != null)
                {
                    transaction.Category = category;
                    ViewData["AutoCategory"] = true;
                }
            }

            return PartialView("EditPartial", transaction);

        }

        // I believe this is never used. Instead, API Controller ApplyPayee is used.
#if false
        public async Task<IActionResult> ApplyPayee(int? id)
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

            // Handle payee auto-assignment

            // See if the payee exists
            var payee = await _context.Payees.FirstOrDefaultAsync(x => transaction.Payee.Contains(x.Name));

            if (payee != null)
            {
                transaction.Category = payee.Category;
                transaction.SubCategory = payee.SubCategory;
                _context.Update(transaction);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
#endif
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanWrite")]
        [ValidateModel]
        [ValidateTransactionExists]
        public async Task<IActionResult> Edit(int id, bool? duplicate, [Bind("ID,Timestamp,Amount,Memo,Payee,Category,SubCategory,BankReference")] Transaction transaction)
        {
            if (duplicate == true)
            {
                transaction.ID = 0;
                await _repository.AddAsync(transaction);
            }
            else
            {
                // Bug #846: This Edit function is not allowed to alter the
                // ReceiptUrl. So we must preserve whatever was there.

                // Note that we need an as no tracking query here, or we'll have problems shortly when we query with one
                // object but update another.
                var oresult = await _repository.GetByQueryAsync(new WireQueryParameters() { Query = $"x={id}" } );
                var oldtransaction = oresult.Items.Single();
                var oldreceipturl = oldtransaction.ReceiptUrl;

                transaction.ReceiptUrl = oldreceipturl;

                await _repository.UpdateAsync(transaction);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = "CanWrite")]
        public async Task<IActionResult> BulkEdit(string Category)
        {
            await _repository.BulkEditAsync(Category);

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Action Handlers: Delete

        // GET: Transactions/Delete/5
        [ValidateTransactionExists]
        public async Task<IActionResult> Delete(int? id) => await Details(id);

        // POST: Transactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanWrite")]
        [ValidateTransactionExists]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _repository.RemoveAsync(await _repository.GetByIdAsync(id));

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Action Handlers: Download/Export

        [HttpPost]
        public async Task<IActionResult> Download(bool allyears, string q = null)
        {
            var sessionvars = new SessionVariables(HttpContext);

            Stream stream = await _repository.AsSpreadsheetAsync(sessionvars.Year ?? _clock.Now.Year, allyears,q);

            IActionResult result = File(stream, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileDownloadName: "Transactions.xlsx");

            return result;
        }

        public IActionResult DownloadPartial()
        {
            var sessionvars = new SessionVariables(HttpContext);
            var year = sessionvars.Year ?? _clock.Now.Year;

            return PartialView(year);
        }

        #endregion

        #region Action Handlers: Upload/Import

        /// <summary>
        /// Upload split detail as spreadsheet for this transaction
        /// </summary>
        /// <param name="files">Spreadsheet containing split details</param>
        /// <param name="id">Which transaction</param>
        /// <param name="importer">Importer which will do the work</param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = "CanWrite")]
        [ValidateFilesProvided(multiplefilesok: true)]
        [ValidateTransactionExists]
        public async Task<IActionResult> UpSplits(List<IFormFile> files, int id, [FromServices] SplitImporter importer)
        {
            var transaction = await _repository.GetWithSplitsByIdAsync(id);

            importer.Target = transaction;

            foreach (var file in files.Where(x => Path.GetExtension(x.FileName).ToLowerInvariant() == ".xlsx"))
            {
                using var stream = file.OpenReadStream();
                importer.QueueImportFromXlsx(stream);
            }

            await importer.ProcessImportAsync();

            return RedirectToAction("Edit", new { id = id });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Seed(string id, [FromServices] IDataContext context, [FromServices] IClock clock)
        {
            var result = string.Empty;
            var resultdetails = string.Empty;
            try
            {
                SampleDataPattern.Year = _clock.Now.Year;

                // Load sample data
                var instream = SampleData.Open("FullSampleDataDefinition.xlsx");
                var generator = new SampleDataGenerator();
                generator.LoadDefinitions(instream);

                var ok = false;
                if ("budget" == id)
                {
                    if (!context.BudgetTxs.Any())
                    {
                        generator.GenerateBudget();
                        context.AddRange(generator.BudgetTxs);
                        resultdetails = $"Added {generator.BudgetTxs.Count} budget line items";
                        ok = true;
                    }
                    else
                    {
                        result = "Sorry";
                        resultdetails = $"Cannot add budget line items when the database already has some";
                    }
                }
                else if ("txtoday" == id)
                {
                    DateTime last = DateTime.MinValue;
                    var lastq = context.Transactions.OrderByDescending(x => x.Timestamp).Select(x => x.Timestamp);
                    if (lastq.Any())
                        last = lastq.First();

                    generator.GenerateTransactions(addids: false);
                    var added = generator.Transactions.Where(x => x.Timestamp > last && x.Timestamp <= clock.Now);
                    context.AddRange(added);
                    resultdetails = $"Added {added.Count()} transactions";
                    ok = true;
                }
                else if ("txyear" == id)
                {
                    if (!context.Transactions.Any())
                    {
                        generator.GenerateTransactions(addids: false);
                        context.AddRange(generator.Transactions);
                        resultdetails = $"Added {generator.Transactions.Count()} transactions";
                        ok = true;
                    }
                    else
                    {
                        result = "Sorry";
                        resultdetails = $"Cannot add transactions when the database already has some";
                    }
                }
                else if ("all" == id || "today" == id)
                {
                    if (!context.Transactions.Any() && !context.BudgetTxs.Any() && !context.Payees.Any())
                    {
                        generator.GenerateTransactions(addids: false);
                        generator.GenerateBudget();
                        generator.GeneratePayees();
                        context.AddRange(generator.Payees);
                        context.AddRange(generator.BudgetTxs);

                        IEnumerable<Transaction> txs = generator.Transactions;
                        if ("today" == id)
                            txs = txs.Where(x => x.Timestamp <= clock.Now);
                        context.AddRange(txs);
                        resultdetails = $"Added {txs.Count()} transactions, {generator.BudgetTxs.Count} budget line items, and {generator.Payees.Count} payees";
                        ok = true;
                    }
                    else
                    {
                        result = "Sorry";
                        resultdetails = $"Cannot add full sample data set unlesss the database is completely empty";
                    }
                }
                else if ("payee" == id)
                {
                    if (!context.Payees.Any())
                    {
                        generator.GeneratePayees();
                        context.AddRange(generator.Payees);
                        resultdetails = $"Added {generator.Payees.Count} payees";
                        ok = true;
                    }
                    else
                    {
                        result = "Sorry";
                        resultdetails = $"Cannot add payees when the database already has some";
                    }
                }
                else
                {
                    result = "Sorry";
                    resultdetails = $"The data type {id} was unknown to the system";
                }

                if (ok)
                {
                    result = "Completed";
                    await context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                result = "Sorry";
                resultdetails = $"The operation failed. Please file an issue on GitHub. {ex.GetType().Name}: {ex.Message}";
            }

            return PartialView("Seed",(result,resultdetails));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DatabaseDelete(string id, [FromServices] IDataContext context)
        {
            if ("budget" == id)
            {
                // TODO: Async() ??
                context.RemoveRange(context.BudgetTxs);
                await context.SaveChangesAsync();
            }
            else if ("tx" == id)
            {
                context.RemoveRange(context.TransactionsWithSplits);
                await context.SaveChangesAsync();
            }
            else if ("payee" == id)
            {
                context.RemoveRange(context.Payees);
                await context.SaveChangesAsync();
            }
            else
            {
                // TODO: Return an error
            }

            // TODO: This just redirects back to Admin, so the numbers can be reloaded.
            //
            // It would be better to do this via Ajax, and then we could fill in the new item,
            // and possibly return errors, all without leaving the page.
            return (IActionResult)RedirectToPage("/Admin");
        }

        #endregion

        #region Action Handlers: Receipts

        /// <summary>
        /// Upload receipt from <paramref name="files"/> to transaction #<paramref name="id"/>
        /// </summary>
        /// <param name="files">Files to upload</param>
        /// <param name="id">Transaction ID for this receipt</param>
        [HttpPost]
        [Authorize(Policy = "CanWrite")]
        [ValidateTransactionExists]
        [ValidateFilesProvided(multiplefilesok: false)]
        [ValidateStorageAvailable]
        public async Task<IActionResult> UpReceipt(List<IFormFile> files, int id)
        {
            var transaction = await _repository.GetByIdAsync(id);

            var formFile = files.Single();
            using var stream = formFile.OpenReadStream();
            await _repository.UploadReceiptAsync(transaction, stream, formFile.ContentType);

            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Take <paramref name="action"/> on the receipt for transaction #<paramref name="id"/>
        /// </summary>
        /// <param name="id">Which transaction</param>
        /// <param name="action">get = retrieve the receipt, delete = delete it</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "CanWrite")]
        [ValidateStorageAvailable]
        [ValidateTransactionExists]
        public async Task<IActionResult> ReceiptAction(int id, string action)
        {
            if (action == "delete")
                return await DeleteReceipt(id);
            else if (action == "get")
                return await GetReceipt(id);
            else
                return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Delete the receipt for transaction #<paramref name="id"/>
        /// </summary>
        /// <param name="id">Which transaction</param>
        private async Task<IActionResult> DeleteReceipt(int id)
        {
            var transaction = await _repository.GetByIdAsync(id);
            transaction.ReceiptUrl = null;
            await _repository.UpdateAsync(transaction);

            return RedirectToAction(nameof(Edit), new { id });
        }

        /// <summary>
        /// Get a receipt for transaction #<paramref name="id"/>
        /// </summary>
        /// <param name="id">Which transaction</param>
        [HttpGet]
        [ValidateTransactionExists]
        [ValidateStorageAvailable]
        public async Task<IActionResult> GetReceipt(int id)
        {
            var transaction = await _repository.GetByIdAsync(id);
            var (stream, contenttype, name) = await _repository.GetReceiptAsync(transaction);
            return File(stream, contenttype, name);
        }

        #endregion

        #region Action Handlers: Error

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #endregion

        #region Internals

        private readonly ITransactionRepository _repository;
        private readonly IAsyncQueryExecution _queryExecution;
        private readonly IClock _clock;
        #endregion

        #region IController
        Task<IActionResult> IController<Transaction>.Index() => Index();
        Task<IActionResult> IController<Transaction>.Edit(int id, Transaction item) => Edit(id, false, item);
        Task<IActionResult> IController<Transaction>.Download() => Download(false);
        Task<IActionResult> IController<Transaction>.Upload(List<IFormFile> files) => throw new NotImplementedException();
        Task<IActionResult> IController<Transaction>.Edit(int? id) => throw new NotImplementedException();
        #endregion
    }
}
