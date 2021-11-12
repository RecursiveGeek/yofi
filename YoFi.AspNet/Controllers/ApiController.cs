﻿using Common.AspNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using YoFi.Core;
using YoFi.Core.Reports;
using YoFi.Core.Repositories;

namespace YoFi.AspNet.Controllers
{
    [Produces("application/json")]
    [Route("api")]
    public class ApiController : Controller
    {
        [HttpGet("{id}", Name = "Get")]
        [ApiBasicAuthorization]
        [ValidateTransactionExists]
        public async Task<IActionResult> Get(int id, [FromServices] ITransactionRepository repository)
        {
            return new OkObjectResult(await repository.GetByIdAsync(id));
        }

        [HttpGet("ReportV2/{id}")]
        [ApiBasicAuthorization]
        public IActionResult ReportV2([Bind("id,year,month,showmonths,level")] ReportParameters parms, [FromServices] IReportEngine reports)
        {
            var json = reports.Build(parms).ToJson();
            return Content(json,"application/json");
        }

        [HttpGet("txi")]
        [ApiBasicAuthorization]
        public async Task<IActionResult> GetTransactions([FromServices] ITransactionRepository repository, string q = null)
        {
            return new OkObjectResult(await repository.ForQuery(q).ToListAsync());
        }

        /// <summary>
        /// Remove all test data from the system
        /// </summary>
        /// <remarks>
        /// Deletes all objects of all types with __TEST__ in their category.
        /// Used by funtional tests to clean themselves up
        /// </remarks>
        /// <returns></returns>
        [HttpPost("ClearTestData/{id}")]
        [ApiBasicAuthorization]
        public async Task<IActionResult> ClearTestData(string id, [FromServices] IDataContext context)
        {
            if (id.Contains("payee"))
                context.RemoveRange(context.Payees.Where(x => x.Category.Contains(TestMarker)));

            if (id.Contains("budgettx"))
                context.RemoveRange(context.BudgetTxs.Where(x => x.Category.Contains(TestMarker)));

            if (id.Contains("trx"))
            {
                context.RemoveRange(context.Transactions.Where(x => x.Category.Contains(TestMarker) || x.Memo.Contains(TestMarker)));
                context.RemoveRange(context.Splits.Where(x => x.Category.Contains(TestMarker)));
            }

            await context.SaveChangesAsync();

            return new OkResult();
        }

        public const string TestMarker = "__test__";

    }
}
