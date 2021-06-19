﻿using ManiaLabs.Portable.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfxWeb.Asp.Controllers.Helpers;
using OfxWeb.Asp.Data;
using OfxWeb.Asp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OfxWeb.Asp.Controllers
{
    // Note that these methods largely do their own Json serialization. This
    // is currently needed because the front end expectes everything in PascalCase,
    // but the default formatter returns everything in camelCase. In later versions
    // of .NET core this is configurable. But for now, we have to do it ourselves.

    [Produces("application/json")]
    [Route("api/tx")]
    public class ApiController : Controller
    {
        private readonly ApplicationDbContext _context;

        private IPlatformAzureStorage _storage;

        public ApiController(ApplicationDbContext context, IPlatformAzureStorage storage)
        {
            _context = context;
            _storage = storage;
        }

        // GET: api/tx
        [HttpGet]
        public string Get()
        {
            return new ApiResult();
        }

        // GET: api/tx/5
        [HttpGet("{id}", Name = "Get")]
        public async Task<string> Get(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .SingleAsync(m => m.ID == id);

                return new ApiTransactionResult(transaction);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/ApplyPayee/5
        [HttpGet("ApplyPayee/{id}")]
        public async Task<string> ApplyPayee(int id)
        {
            try
            {
                var transaction = await _context.Transactions.SingleAsync(m => m.ID == id);

                // Handle payee auto-assignment

                Payee payee = null;

                // Product Backlog Item 871: Match payee on regex, optionally
                var regexpayees = _context.Payees.Where(x => x.Name.StartsWith("/") && x.Name.EndsWith("/"));
                foreach (var regexpayee in regexpayees)
                {
                    var regex = new Regex(regexpayee.Name.Substring(1, regexpayee.Name.Length - 2));
                    if (regex.Match(transaction.Payee).Success)
                    {
                        payee = regexpayee;
                        break;
                    }
                }

                // See if the payee exists outright
                if (payee == null)
                {
                    payee = await _context.Payees.FirstOrDefaultAsync(x => transaction.Payee.Contains(x.Name));
                }

                if (payee == null)
                    throw new KeyNotFoundException("Payee unknown");

                transaction.Category = payee.Category;
                transaction.SubCategory = payee.SubCategory;
                _context.Update(transaction);
                await _context.SaveChangesAsync();

                return new ApiPayeeResult(payee);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/Hide/5
        [HttpGet("Hide/{id}")]
        public async Task<string> Hide(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .SingleAsync(m => m.ID == id);

                transaction.Hidden = true;
                _context.Update(transaction);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/Show/5
        [HttpGet("Show/{id}")]
        public async Task<string> Show(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .SingleAsync(m => m.ID == id);

                transaction.Hidden = false;
                _context.Update(transaction);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/Select/5
        [HttpGet("Select/{id}")]
        public async Task<string> Select(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .SingleAsync(m => m.ID == id);

                transaction.Selected = true;
                _context.Update(transaction);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/Deselect/5
        [HttpGet("Deselect/{id}")]
        public async Task<string> Deselect(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .SingleAsync(m => m.ID == id);

                transaction.Selected = false;
                _context.Update(transaction);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/SelectPayee/5
        [HttpGet("SelectPayee/{id}")]
        public async Task<string> SelectPayee(int id)
        {
            try
            {
                var payee = await _context.Payees
                    .SingleAsync(m => m.ID == id);

                payee.Selected = true;
                _context.Update(payee);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // GET: api/tx/DeselectPayee/5
        [HttpGet("DeselectPayee/{id}")]
        public async Task<string> DeselectPayee(int id)
        {
            try
            {
                var payee = await _context.Payees
                    .SingleAsync(m => m.ID == id);

                payee.Selected = false;
                _context.Update(payee);
                await _context.SaveChangesAsync();

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // POST: api/tx/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost("Edit/{id}")]
        public async Task<string> Edit(int id, bool? duplicate, [Bind("ID,Timestamp,Amount,Memo,Payee,Category,SubCategory,BankReference,ReceiptUrl")] Models.Transaction transaction)
        {
            try
            {
                if (id != transaction.ID && duplicate != true)
                    throw new Exception("not found");

                if (!ModelState.IsValid)
                    throw new Exception("invalid");

                if (duplicate == true)
                {
                    transaction.ID = 0;
                    _context.Add(transaction);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _context.Update(transaction);
                    await _context.SaveChangesAsync();
                }

                return new ApiTransactionResult(transaction);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // POST: api/tx/UpReceipt/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost("UpReceipt/{id}")]
        public async Task<string> UpReceipt(int id, IFormFile file)
        {
            try
            {
                var transaction = await _context.Transactions.SingleOrDefaultAsync(m => m.ID == id);

                if (null == transaction)
                    throw new ApplicationException($"Unable to find transaction #{id}");

                if (! string.IsNullOrEmpty(transaction.ReceiptUrl))
                    throw new ApplicationException($"This transaction already has a receipt. Delete the current receipt before uploading a new one.");

                //
                // Save the file to blob storage
                //

                _storage.Initialize();

                string contenttype = null;

                using (var stream = file.OpenReadStream())
                {

                    // Upload the file
                    await _storage.UploadToBlob(BlobStoreName, id.ToString(), stream, file.ContentType);

                    // Remember the content type
                    // TODO: This can just be a true/false bool, cuz now we store content type in blob store.
                    contenttype = file.ContentType;
                }

                // Save it in the Transaction

                if (null != contenttype)
                {
                    transaction.ReceiptUrl = contenttype;
                    _context.Update(transaction);
                    await _context.SaveChangesAsync();
                }

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        public async Task<String> UpSplits(int id, IFormFile file)
        {

            try
            {
                var transaction = await _context.Transactions.Include(x=>x.Splits)
                    .SingleAsync(m => m.ID == id);

                var incoming = new HashSet<Models.Split>();
                // Extract submitted file into a list objects

                if (file.FileName.ToLower().EndsWith(".xlsx"))
                {
                    using (var stream = file.OpenReadStream())
                    {
                        var excel = new ExcelPackage(stream);
                        var worksheet = excel.Workbook.Worksheets.Where(x => x.Name == "Splits").Single();
                        worksheet.ExtractInto(incoming);
                    }
                }

                if (incoming.Any())
                {
                    // Why no has AddRange??
                    foreach (var split in incoming)
                    {
                        split.FixupCategories();
                        transaction.Splits.Add(split);
                    }

                    _context.Update(transaction);
                    await _context.SaveChangesAsync();
                }

                return new ApiTransactionResult(transaction);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        // POST: api/tx/EditPayee/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost("EditPayee/{id}")]
        public async Task<string> EditPayee(int id, bool? duplicate, [Bind("ID,Name,Category,SubCategory")] Models.Payee payee)
        {
            try
            {
                if (id != payee.ID && duplicate != true)
                    throw new Exception("not found");

                if (!ModelState.IsValid)
                    throw new Exception("invalid");

                if (duplicate == true)
                {
                    payee.ID = 0;
                    _context.Add(payee);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    _context.Update(payee);
                    await _context.SaveChangesAsync();
                }

                return new ApiPayeeResult(payee);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }


        // POST: api/tx/AddPayee
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        // TODO: Move to a payee api controller and rename to POST api/payee/Create
        [HttpPost("AddPayee")]
        public async Task<string> AddPayee([Bind("Name,Category,SubCategory")] Payee payee)
        {
            try
            {
                if (!ModelState.IsValid)
                    throw new Exception("invalid");

                _context.Add(payee);
                await _context.SaveChangesAsync();
                return new ApiPayeeResult(payee);
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

        private static void CheckApiAuth(IHeaderDictionary Headers)
        {
            if (!Headers.ContainsKey("Authorization"))
                throw new UnauthorizedAccessException();

            var authorization = Headers["Authorization"].Single();
            if (!authorization.StartsWith("Basic "))
                throw new UnauthorizedAccessException();

            var base64 = authorization.Substring(6);
            var credentialBytes = Convert.FromBase64String(base64);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            if ("j+dF48FhiU+Dz83ZQYsoXw==" != password)
                throw new ApplicationException("Invalid password");
        }

        // GET: api/tx/report/2020
        [HttpGet("Report/{topic}")]
        public async Task<ActionResult> Report(string topic, int year, string key, string constraint)
        {
            try
            {
                CheckApiAuth(Request.Headers);

                if (year < 2017 || year > 2050)
                    throw new ApplicationException("Invalid year");

                // The only report we can return via API is 'summary'
                if ("summary" != topic)
                    throw new ApplicationException("Invalid topic");

                // The only constraint that's implemented is 'semimonthly'
                DateTime? beforedate = null;
                if (constraint?.ToLower() == "semimonthly")
                {
                    // Semimonthly constraint means return ONLY transactions before or equal to last payday,
                    // which is day before 1st and 16th.
                    var now = DateTime.Now;
                    int beforeday = (now.Day > 15) ? 16 : 1;
                    beforedate = new DateTime(now.Year, now.Month, beforeday);
                }

                Func<Models.Transaction, bool> inscope_t;
                Func<Models.Split, bool> inscope_s;
                if (beforedate.HasValue)
                {
                    inscope_t = (x => x.Timestamp < beforedate.Value && x.Timestamp.Year == year && x.Hidden != true);
                    inscope_s = (x => x.Transaction.Timestamp < beforedate.Value && x.Transaction.Timestamp.Year == year && x.Transaction.Hidden != true);
                }
                else
                {
                    inscope_t = (x => x.Timestamp.Year == year && x.Hidden != true);
                    inscope_s = (x => x.Transaction.Timestamp.Year == year && x.Transaction.Hidden != true);
                }

                var txs = _context.Transactions.Include(x => x.Splits).Where(inscope_t).Where(x => !x.Splits.Any()).AsQueryable<ISubReportable>();
                var splits = _context.Splits.Include(x => x.Transaction).Where(inscope_s).AsQueryable<ISubReportable>();
                var combined = txs.Concat(splits);
                var groupings = combined.OrderBy(x => x.Timestamp).GroupBy(x => x.Timestamp.Month);
                var builder = new Helpers.ReportBuilder(_context);
                var report = await builder.ThreeLevelReport(groupings, true);
                var result = new ApiSummaryReportResult(report);

                // For the summary report via api, we don't want any Key blanks
                result.Lines.RemoveAll(x => x.Keys == null);

                // User Story 889: Add BudgetTxs to API report

                var budgettxs = _context.BudgetTxs.Where(x => x.Timestamp.Year == year);
                result.AddBudgetTxs(budgettxs);

                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        // GET: api/tx/reportv2/all
        [HttpGet("ReportV2/{id}")]
        public async Task<ActionResult> ReportV2([Bind("id,year,month,showmonths,level")] ReportBuilder.Parameters parms)
        {
            try
            {
                CheckApiAuth(Request.Headers);

                var result = new ReportBuilder(_context).BuildReport(parms);
                var json = result.ToJson();

                return Content(json);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        private string BlobStoreName
        {
            get
            {
                var receiptstore = Environment.GetEnvironmentVariable("RECEIPT_STORE");
                if (string.IsNullOrEmpty(receiptstore))
                    receiptstore = "myfire-undefined";

                return receiptstore;
            }
        }
    }

    public class ApiResult
    {
        public bool Ok { get; set; } = true;

        public Exception Exception { get; set; } = null;

        public ApiResult() { }

        public ApiResult(Exception ex)
        {
            Exception = ex;
            Ok = false;
        }

        public static implicit operator string(ApiResult r) => JsonConvert.SerializeObject(r);
    }

    public class ApiTransactionResult : ApiResult
    {
        public Transaction Transaction { get; set; }

        public ApiTransactionResult(Transaction transaction)
        {
            Transaction = transaction;
        }
    }

    public class ApiPayeeResult : ApiResult
    {
        public Payee Payee { get; set; }

        public ApiPayeeResult(Payee payee)
        {
            Payee = payee;
        }
    }

    public class ApiSummaryReportResult : ApiResult
    {
        public struct Line
        {
            public string Keys;
            public decimal Amount;
            public decimal Budget;
        }

        public List<Line> Lines = new List<Line>();

        public ApiSummaryReportResult(Table<Label, Label, decimal> report)
        {
            foreach (var rowlabel in report.RowLabels)
            {
                var line = new Line();

                var keys = new List<string>();
                if (!string.IsNullOrEmpty(rowlabel.Key1))
                {
                    keys.Add(rowlabel.Key1);
                    if (!string.IsNullOrEmpty(rowlabel.Key2))
                    {
                        keys.Add(rowlabel.Key2);
                        if (!string.IsNullOrEmpty(rowlabel.Key3))
                        {
                            keys.Add(rowlabel.Key3);
                            if (!string.IsNullOrEmpty(rowlabel.Key4))
                            {
                                keys.Add(rowlabel.Key4);
                            }
                        }
                    }
                }

                // Prepare a single string of colon-separated keys, for future expansion where we
                // remove the limit on ## of keys
                if (keys.Any())
                    line.Keys = string.Join(':', keys);

                var column = report.ColumnLabels.Where(x => x.Value == "TOTAL").FirstOrDefault();

                if (null != column)
                {
                    var cell = report[column,rowlabel];
                    line.Amount = cell;
                    Lines.Add(line);
                }
            }
        }

        public void AddBudgetTxs(IEnumerable<BudgetTx> budgettxs)
        {
            // User Story 889: Add BudgetTxs to API report

            var newlines = budgettxs.Select(x => new Line() { Budget = x.Amount, Keys = x.Category });
            Lines.AddRange(newlines);
        }
    }
}
