﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OfxWeb.Asp;
using OfxWeb.Asp.Controllers;
using OfxWeb.Asp.Data;
using OfxWeb.Asp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.AspNetCore.Test;
using Microsoft.AspNetCore.Http;
using System.IO;
using OfficeOpenXml;
using OfxWeb.Asp.Controllers.Reports;
using Common.Test.Helper;

namespace Ofx.Tests
{
    [TestClass]
    public class TransactionControllerTest
    {
        private ControllerTestHelper<Transaction, TransactionsController> helper = null;

        TransactionsController controller => helper.controller;
        ApplicationDbContext context => helper.context;
        List<Transaction> Items => helper.Items;
        DbSet<Transaction> dbset => helper.dbset;

        List<Transaction> TransactionItems = new List<Transaction>()
        {
            new Transaction() { Category = "B", SubCategory = "A", Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m },
            new Transaction() { Category = "A", SubCategory = "A", Payee = "2", Timestamp = new DateTime(DateTime.Now.Year, 01, 04), Amount = 200m },
            new Transaction() { Category = "C", SubCategory = "A", Payee = "5", Timestamp = new DateTime(DateTime.Now.Year, 01, 01), Amount = 300m },
            new Transaction() { Category = "B", SubCategory = "A", Payee = "1", Timestamp = new DateTime(DateTime.Now.Year, 01, 05), Amount = 400m },
            new Transaction() { Category = "B", SubCategory = "B", Payee = "4", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 500m },
            new Transaction() { Category = "B", SubCategory = "B", Payee = "34", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 500m },
            new Transaction() { Category = "B", SubCategory = "B", Payee = "1234", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 500m },
        };

        List<Split> SplitItems = new List<Split>()
        {
            new Split() { Amount = 25m, Category = "A", SubCategory = "B" },
            new Split() { Amount = 75m, Category = "C", SubCategory = "D" }
        };

        List<Payee> PayeeItems = new List<Payee>()
        {
            new Payee() { Category = "Y", SubCategory = "E", Name = "3" },
            new Payee() { Category = "X", SubCategory = "E", Name = "2" },
            new Payee() { Category = "Z", SubCategory = "E", Name = "5" },
            new Payee() { Category = "X", SubCategory = "E", Name = "1" },
            new Payee() { Category = "Y", SubCategory = "F", Name = "4" }
        };

        List<CategoryMap> CategoryMapItems = new List<CategoryMap>()
        {
            new CategoryMap() { Category = "A", Key1 = "X", Key2 = "Y" },
            new CategoryMap() { Category = "C", Key1 = "Z", Key2 = "R" }
        };

        [TestInitialize]
        public void SetUp()
        {
            helper = new ControllerTestHelper<Transaction, TransactionsController>();
            helper.SetUp();
            helper.controller = new TransactionsController(helper.context, null);
            helper.Items.AddRange(TransactionItems.Take(5));
            helper.dbset = helper.context.Transactions;

            // Sample data items will use Payee name as a unique sort idenfitier
            helper.KeyFor = (x => x.Payee);
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
        // TODO: Fix failing tests
        [TestMethod]
        public async Task Download() => await helper.Download();
        [TestMethod]
        public async Task Upload()
        {
            // Can't use the helper's upload bevause Transaction upload does not return the uploaded items.
            var result = await helper.DoUpload(Items);

            // Test the status
            var actual = result as RedirectToActionResult;

            Assert.AreEqual("Import", actual.ActionName);

            // Now check the state of the DB

            Assert.AreEqual(Items.Count, dbset.Count());
        }
        [TestMethod]
        public async Task UploadWithID()
        {
            // Can't use the helper's upload bevause Transaction upload does not return the uploaded items.

            // Start out with one item in the DB. We are picking the ONE item that Upload doesn't upload.
            var expected = Items[4];
            context.Add(expected);
            await context.SaveChangesAsync();

            // One of the new items has an overlapping ID, but will be different in every way. We expect that
            // The end result is that the database will == items
            Items[0].ID = expected.ID;

            // Just upload the first four items. The fifth, we already did above
            // Can't use the helper's upload bevause Transaction upload does not return the uploaded items.
            var result = await helper.DoUpload(Items.Take(4).ToList());
            var actual = result as RedirectToActionResult;

            Assert.AreEqual("Import", actual.ActionName);

            // From here we can just use the Index test, but not add items. There should be the proper "items"
            // all there now.

            var indexmany = await dbset.OrderBy(x => x.Payee).ToListAsync();

            // Sort the original items by Key
            Items.Sort((x, y) => x.Payee.CompareTo(y.Payee));

            // Test that the resulting items are in the same order
            for (int i = 0; i < 5; ++i)
                Assert.AreEqual(Items[i], indexmany[i]);
        }

        [TestMethod]
        public async Task UploadDuplicate()
        {
            // Start out with one item in the DB. We are picking the ONE item that Upload doesn't upload.
            var initial = Items[0];
            context.Add(initial);
            await context.SaveChangesAsync();

            // Need to detach the entity we originally created, to set up the same state the controller would be
            // in with not already haveing a tracked object.
            context.Entry(initial).State = EntityState.Detached;

            Items[0].ID = 0;

            // Now upload all the items. What should happen here is that only items 1-4 (not 0) get
            // uploaded, because item 0 is already there, so it gets removed as a duplicate.
            // Can't use the helper's upload bevause Transaction upload does not return the uploaded items.
            var result = await helper.DoUpload(Items);
            var redirectooactionresult = result as RedirectToActionResult;

            Assert.AreEqual("Import", redirectooactionresult.ActionName);

            // What's expected here? The new item IS uploaded, BUT it's deselected, where the rest are selected, right?
            var indexmany = await dbset.OrderBy(x => x.Payee).ToListAsync();

            Assert.AreEqual(1 + Items.Count(), indexmany.Count);

            // We expect that FIVE items were imported.

            var wasimported = dbset.Where(x => true == x.Imported);

            Assert.AreEqual(5, wasimported.Count());

            // Out of those, we expect that ONE is not selected, because it's a dupe of the initial.

            var actual = wasimported.Where(x => false == x.Selected).Single();

            Assert.AreEqual(initial, actual);
        }

        [TestMethod]
        public async Task UploadMatchPayees()
        {
            context.Payees.AddRange(PayeeItems.Take(5));
            await context.SaveChangesAsync();

            // Strip off the categories, so they'll match on input
            var uploadme = Items.Select(x => { x.Category = null; x.SubCategory = null; return x; }).ToList();

            // Then upload that
            await helper.DoUpload(uploadme);

            // This should have matched ALL the payees

            foreach(var tx in context.Transactions)
            {
                var expectedpayee = context.Payees.Where(x => x.Name == tx.Payee).Single();
                Assert.AreEqual(expectedpayee.Category, tx.Category);
            }
        }

        [TestMethod]
        public async Task Bug883()
        {
            // Bug 883: Apparantly duplicate transactions in import are coalesced to single transaction for input

            var uploadme = new List<Transaction>() { Items[0], Items[0] };

            // Then upload that
            await helper.DoUpload(uploadme);

            Assert.AreEqual(2, context.Transactions.Count());
        }

        [TestMethod]
        public async Task Bug880()
        {
            // Bug 880: Import applies substring matches before regex matches

            var regexpayee = new Payee() { Category = "Y", SubCategory = "E", Name = "/DOG.*123/" };
            var substrpayee = new Payee() { Category = "X", SubCategory = "E", Name = "BIGDOG" };

            context.Payees.Add(regexpayee);
            context.Payees.Add(substrpayee);
            await context.SaveChangesAsync();

            // This transaction will match EITHER of the payees. Preference is to match the regex first
            // because the regex is a more precise specification of what we want.
            await helper.DoUpload(new List<Transaction>() { new Transaction() { Payee = "BIGDOG SAYS 1234", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m } });

            var tx = context.Transactions.Single();

            Assert.AreEqual(regexpayee.Category, tx.Category);
        }

        [TestMethod]
        public async Task Bug839()
        {
            // Bug 839: Imported items are selected automatically :(
            await helper.DoUpload(Items);

            await controller.Import("ok");

            var numimported = await dbset.Where(x => x.Imported == true).CountAsync();
            var numselected = await dbset.Where(x => x.Selected == true).CountAsync();

            Assert.AreEqual(0, numimported);
            Assert.AreEqual(0, numselected);
        }

        [TestMethod]
        public async Task Bug846()
        {
            // Bug 846: Save edited item overwrites uploaded receipt

            // So what is happening here is we are calling Edit(id) to get the item first, which has no receipturl

            await helper.AddFiveItems();
            var expected = Items[3];
            var result = await controller.Edit(expected.ID);
            var viewresult = result as ViewResult;
            var editing = viewresult.Model as Transaction;

            // Detach so our edits won't show up
            context.Entry(editing).State = EntityState.Detached;

            // Then separately we are committing a change to set the receipturl

            var dbversion = dbset.Where(x => x.ID == editing.ID).Single();
            dbversion.ReceiptUrl = "SET";
            context.SaveChanges();

            // Detach so the editing operation can work on this item
            context.Entry(dbversion).State = EntityState.Detached;

            // And THEN we are posting the original item :P
            await controller.Edit(editing.ID, false, editing);

            // What SHOULD happen is that the "blank" recepturl in the updated object does not overwrite
            // the receitpurl we set above.

            var actual = dbset.Where(x => x.ID == editing.ID).Single();

            Assert.AreEqual("SET", actual.ReceiptUrl);
        }

        [TestMethod]
        public async Task CreateSplit()
        {
            await helper.AddFiveItems();
            var item = Items[3];
            var expectedcategory = new String(item.Category);

            var result = await controller.CreateSplit(item.ID);
            var viewresult = result as RedirectToActionResult;
            var routes = viewresult.RouteValues;
            var newid = Convert.ToInt32(routes["id"]);
            var actual = context.Splits.Where(x => x.ID == newid).Single();

            Assert.AreEqual(item.ID, actual.TransactionID);
            Assert.AreEqual(item.Amount, actual.Amount);
            Assert.AreEqual(expectedcategory, actual.Category);
            Assert.IsNull(actual.Transaction.Category);
            Assert.IsNull(actual.Transaction.SubCategory);
        }

        [TestMethod]
        public async Task CreateSecondSplit()
        {
            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(1).ToList() };

            context.Transactions.Add(item);
            context.SaveChanges();

            var result = await controller.CreateSplit(item.ID);
            var viewresult = result as RedirectToActionResult;
            var routes = viewresult.RouteValues;
            var newid = Convert.ToInt32(routes["id"]);
            var actual = context.Splits.Where(x => x.ID == newid).Single();

            Assert.AreEqual(item.ID, actual.TransactionID);
            Assert.AreEqual(75m, actual.Amount);

            // Second split shouldn't have a category/subcat
            Assert.IsNull(actual.Category);
            Assert.IsNull(actual.SubCategory);
        }

        [TestMethod]
        public async Task SplitsShownInEdit()
        {
            // Copied from SplitTest.Includes()
            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(2).ToList() };

            context.Transactions.Add(item);
            context.SaveChanges();

            // Not using this line from SplitTest.Includes(), instead we'll test the controller
            // edit.
            //var actual = await context.Transactions.Include("Splits").ToListAsync();

            // Copied from ControllerTestHelper.EditFound()
            var result = await controller.Edit(item.ID);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as Transaction;

            Assert.AreEqual(2, model.Splits.Count);
            Assert.AreEqual(75m, model.Splits.Where(x => x.Category == "C").Single().Amount);
            Assert.AreEqual(true, viewresult.ViewData["SplitsOK"]);
        }

        [TestMethod]
        public async Task SplitsShownInIndex()
        {
            // Copied from SplitTest.Includes()
            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(2).ToList() };
            context.Transactions.Add(item);
            context.SaveChanges();

            var result = await controller.Index(null,null,null,null,null);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as List<Transaction>;

            Assert.IsTrue(model.Single().HasSplits);
            Assert.IsTrue(model.Single().IsSplitsOK);
        }
        [TestMethod]
        public async Task SplitsShownInIndexSearchCategory()
        {
            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(2).ToList() };
            context.Transactions.Add(item);
            context.SaveChanges();

            var result = await controller.Index(null, null, null, "A", null);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as List<Transaction>;

            Assert.AreEqual(1, model.Count);
            Assert.IsTrue(model.Single().HasSplits);
            Assert.IsTrue(model.Single().IsSplitsOK);
        }

#if false
        // TODO: Rewrite for V3 reports
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task SplitsShownInReport(bool usesplits)
        {
            int year = DateTime.Now.Year;
            var ex1 = SplitItems[0];
            var ex2 = SplitItems[1];

            if (usesplits)
            {
                var item = new Transaction() { Payee = "3", Timestamp = new DateTime(year, 01, 03), Amount = 100m, Splits = SplitItems.Take(2).ToList() };

                context.Transactions.Add(item);
            }
            else
            {
                var items = new List<Transaction>();
                items.Add(new Transaction() { Category = ex1.Category, SubCategory = ex1.SubCategory, Payee = "3", Timestamp = new DateTime(year, 01, 03), Amount = ex1.Amount });
                items.Add(new Transaction() { Category = ex2.Category, SubCategory = ex2.SubCategory, Payee = "2", Timestamp = new DateTime(year, 01, 04), Amount = ex2.Amount });
                context.Transactions.AddRange(items);
            }

            context.SaveChanges();

            var result = await controller.Pivot("all", null, null, year, null);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as Table<Label, Label, decimal>;

            var row_AB = model.RowLabels.Where(x => x.Key1 == "A" && x.Key2 == "B").Single();
            var col = model.ColumnLabels.First();
            var actual_AB = model[col, row_AB];

            Assert.AreEqual(ex1.Amount, actual_AB);

            var row_CD = model.RowLabels.Where(x => x.Key1 == "C" && x.Key2 == "D").Single();
            var actual_CD = model[col, row_CD];

            Assert.AreEqual(ex2.Amount, actual_CD);

            // Make sure the total is correct as well, no extra stuff in there.
            var row_total = model.RowLabels.Where(x => x.Value == "TOTAL").Single();
            var actual_total = model[col, row_total];

            Assert.AreEqual(ex1.Amount + ex2.Amount, actual_total);
        }
#endif

        [TestMethod]
        public async Task SplitsDontAddUpInEdit()
        {
            // Copied from SplitTest.Includes()

            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(1).ToList() };

            context.Transactions.Add(item);
            context.SaveChanges();

            // Copied from ControllerTestHelper.EditFound()
            var result = await controller.Edit(item.ID);
            var viewresult = result as ViewResult;
            var model = viewresult.Model as Transaction;

            Assert.AreEqual(false, viewresult.ViewData["SplitsOK"]);
            Assert.IsFalse(model.IsSplitsOK);
        }

        [TestMethod]
        public async Task UploadSplitsForTransaction()
        {
            // Don't add the splits here, we'll upload them
            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m };

            context.Transactions.Add(item);
            context.SaveChanges();

            // Make an HTML Form file containg an excel spreadsheet containing those splits
            var file = ControllerTestHelper<Split, SplitsController>.PrepareUpload(SplitItems.Take(2).ToList());

            // Upload that
            var result = await controller.UpSplits(new List<IFormFile>() { file }, item.ID);
            var redir = result as RedirectToActionResult;

            Assert.IsNotNull(redir);
            Assert.IsTrue(item.HasSplits);
            Assert.IsTrue(item.IsSplitsOK);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task SplitsShownDownload(bool mapped)
        {
            if (mapped)
                context.CategoryMaps.AddRange(CategoryMapItems.Take(2)); 

            var item = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = SplitItems.Take(2).ToList() };

            context.Transactions.Add(item);
            context.SaveChanges();

            var result = await controller.Download(false,mapped);
            var fcresult = result as FileContentResult;
            var data = fcresult.FileContents;
            var incoming = helper.ExtractFromExcel<Split>(data);

            Assert.AreEqual(2, incoming.Count);
            Assert.AreEqual(item.ID, incoming.First().TransactionID);
            Assert.AreEqual(item.ID, incoming.Last().TransactionID);

            if (mapped)
            {
                Assert.AreEqual("X:Y:B", incoming.First().Category);
                Assert.AreEqual("Z:R:D", incoming.Last().Category);
            }

        }


        [TestMethod]
        public async Task UploadSplitsWithTransactions()
        {
            // This is the correlary to SplitsShownDownload(). The question is, now that we've
            // DOWNLOADED transactions and splits, can we UPLOAD them and get the splits?

            // Here's the test data set. Note that "Transaction ID" in this case is used just
            // as a matching ID for the current spreadsheet. It should be discarded.
            var transactions = new List<Transaction>();
            var splits = new List<Split>();
            splits.Add(new Split() { Amount = 25m, Category = "A", SubCategory = "B", TransactionID = 1000 });
            splits.Add(new Split() { Amount = 75m, Category = "C", SubCategory = "D", TransactionID = 1000 });
            splits.Add(new Split() { Amount = 175m, Category = "X", SubCategory = "Y", TransactionID = 12000 }); // Not going to be matched!

            var item = new Transaction() { ID = 1000, Payee = "3", Category = "RemoveMe", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m, Splits = splits };
            transactions.Add(item);

            // Build a spreadsheet with the chosen number of items
            byte[] reportBytes;
            var sheetname = "Transactions";
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add(sheetname);
                worksheet.PopulateFrom(transactions, out _, out _);
                worksheet = package.Workbook.Worksheets.Add("Splits");
                worksheet.PopulateFrom(splits, out _, out _);
                reportBytes = package.GetAsByteArray();
            }

            // Create a formfile with it
            // Note that we are not disposing the stream. User of the file will do so later.
            var stream = new MemoryStream(reportBytes);
            IFormFile file = new FormFile(stream, 0, reportBytes.Length, sheetname, $"{sheetname}.xlsx");

            // Upload it!
            var result = await controller.Upload(new List<IFormFile>() { file },null);
            Assert.IsTrue(result is RedirectToActionResult);

            // Did the transaction and splits find each other?
            var actual = context.Transactions.Include(x=>x.Splits).Single();
            Assert.AreEqual(2,actual.Splits.Count);

            // The transaction should NOT have a category anymore, even if it had one to begin with
            Assert.IsNull(actual.Category);
        }
        [TestMethod]
        public async Task DownloadMapped()
        {
            context.CategoryMaps.AddRange(CategoryMapItems.Take(1));
            context.Transactions.AddRange(TransactionItems.Skip(1).Take(1));
            context.SaveChanges();

            var result = await controller.Download(false, true);
            var fcresult = result as FileContentResult;
            var data = fcresult.FileContents;
            var incoming = helper.ExtractFromExcel<Transaction>(data);

            Assert.AreEqual(1, incoming.Count);
            Assert.AreEqual("X:Y:A", incoming.Single().Category);
        }

        [TestMethod]
        public async Task NullTransactionsOKinMappedDownload()
        {
            var item = new Transaction() { Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m };
            context.Transactions.Add(item);
            context.SaveChanges();

            var result = await controller.Download(false, true);
            var fcresult = result as FileContentResult;
            var data = fcresult.FileContents;
            var incoming = helper.ExtractFromExcel<Transaction>(data);

            Assert.AreEqual(1, incoming.Count);
            Assert.AreEqual(null, incoming.Single().Category);
        }

        [TestMethod]
        public async Task<FileContentResult> DownloadAllYears()
        {
            var item_new = new Transaction() { Payee = "3", Timestamp = new DateTime(DateTime.Now.Year, 01, 03), Amount = 100m };
            var item_old = new Transaction() { Payee = "4", Timestamp = new DateTime(DateTime.Now.Year - 5, 01, 03), Amount = 200m };

            context.Transactions.Add(item_old);
            context.Transactions.Add(item_new);
            context.SaveChanges();

            var result = await controller.Download(true, false);
            var fcresult = result as FileContentResult;
            var data = fcresult.FileContents;

            var incoming = helper.ExtractFromExcel<Transaction>(data);

            Assert.AreEqual(2, incoming.Count);

            return fcresult;
        }

        [TestMethod]
        public async Task Bug895()
        {
            // Bug 895: Transaction download appears corrupt if no splits

            // So if there are no SPLITS there should be do splits tab.
            // For convenience we'll use a different test and just grab those results.

            var fcresult = await DownloadAllYears();
            var data = fcresult.FileContents;

            using (var stream = new MemoryStream(data))
            {
                var excel = new ExcelPackage(stream);
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                // Only 1 worksheet, no "splits" worksheet
                Assert.AreEqual(1, excel.Workbook.Worksheets.Count);
                Assert.IsFalse(excel.Workbook.Worksheets.Where(x => x.Name == "Splits").Any());
            }
        }

        [TestMethod]
        public async Task EditObjectValuesDuplicate()
        {
            var initial = Items[3];
            context.Add(initial);
            await context.SaveChangesAsync();
            var id = initial.ID;

            // Need to detach the entity we originally created, to set up the same state the controller would be
            // in with not already haveing a tracked object.
            context.Entry(initial).State = EntityState.Detached;

            var updated = Items[1];
            updated.ID = id;
            var result = await controller.Edit(id, duplicate:true, transaction:updated);
            var actual = result as RedirectToActionResult;

            Assert.AreEqual("Index", actual.ActionName);

            // The net effect of the duplicate flag is that we now have TWO of these objects,
            // so 2 total.
            Assert.AreEqual(2, context.Transactions.Count());
        }

        [DataRow("1/1/2017",1000)]
        [DataRow("6/1/2018", 4)]
        [DataTestMethod]
        public async Task OfxUpload(string date, int expected)
        {
            var filename = "ExportedTransactions.ofx";
            var stream = SampleData.Open(filename);
            var length = stream.Length;
            IFormFile file = new FormFile(stream, 0, length, filename, filename);
            var result = await controller.Upload(new List<IFormFile>() { file },date);

            // Test the status
            var rdresult = result as RedirectToActionResult;

            Assert.AreEqual("Import", rdresult.ActionName);
            Assert.AreEqual(expected, dbset.Count());
        }

        [TestMethod]
        public async Task ImportCancel()
        {
            // Given: As set of items, some with imported flag, some with not
            var expected = 1; // How many should remain?
            foreach(var item in Items.Skip(expected))
                item.Imported = true;
            await helper.AddFiveItems();

            // When: Calling import with "cancel" command
            var result = await controller.Import("cancel");

            // Then: Only items without imported flag remain
            Assert.AreEqual(expected, dbset.Count());
        }

        [TestMethod]
        public async Task ImportOk()
        {
            // Given: As set of items, some with imported & selected flags, some with not
            var notimported = 1; // How many should remain?
            foreach (var item in Items.Skip(notimported))
                item.Imported = item.Selected = true;
            await helper.AddFiveItems();

            // When: Calling import with "ok" command
            var result = await controller.Import("ok");

            // Then: All items remain, none have imported flag
            Assert.AreEqual(5, dbset.Count());
            Assert.AreEqual(0, dbset.Where(x => x.Imported == true).Count());
        }

        [TestMethod]
        public async Task ImportOkSelected()
        {
            // Given: As set of items, all with imported some selected, some not
            foreach (var item in Items)
                item.Imported = true;

            var imported = 2; // How many should remain?
            foreach (var item in Items.Take(imported))
                item.Selected = true;

            await helper.AddFiveItems();

            // When: Calling import with "ok" command
            var result = await controller.Import("ok");

            // Then: Only selected items remain
            Assert.AreEqual(imported, dbset.Count());
        }

        public async Task IndexSortOrder()
        {
            // Given: A set of items

            // When: Calling Index with a defined sort order

            // Then: The items are returned sorted in that order
        }

        [TestMethod]
        public async Task IndexPayeeSearch()
        {
            // Given: A set of items with various payees
            context.Transactions.AddRange(TransactionItems.Take(7));
            context.SaveChanges();

            // When: Calling Index with payee search term
            var result = await controller.Index(null, null, "4", null, null);
            var actual = result as ViewResult;
            var model = actual.Model as List<Transaction>;

            // Then: Only the items with a matching payee are returned
            Assert.AreEqual(3, model.Count);
        }

        public async Task IndexCategorySearch()
        {
            // Given: A set of items with various categories

            // When: Calling Index with category search term

            // Then: Only the items with a matching category are returned
        }

        public async Task IndexPage1()
        {
            // Given: A very long set of items 

            // When: Calling Index page 1

            // Then: Only one page's worth of items are returned
        }

        public async Task IndexPage2()
        {
            // Given: A very long set of items 

            // When: Calling Index page 2

            // Then: Only items after one page's worth of items are returned
        }

        //
        // Long list of TODO tests!!
        //
        // TODO: Bulk Edit
        // TODO: UpReceipt
        // TODO: DeleteReceipt
        // TODO: GetReceipt
    }
}
