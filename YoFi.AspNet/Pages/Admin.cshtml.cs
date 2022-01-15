using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using YoFi.Core;

namespace YoFi.AspNet.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminModel : PageModel
    {
        public bool HasSomeData { get; set; }
        public bool HasAllData { get; set; }
        public int NumTransactions { get; set; }
        public int NumBudgetTxs { get; set; }
        public int NumPayees { get; set; }
        public PageConfig Config { get; private set; }

        private readonly IDataContext _context;


        public class PageConfig
        {
            public const string Section = "Admin";

            public bool NoDelete { get; set; }
        }

        public AdminModel(IDataContext context, IOptions<PageConfig> config)
        {
            _context = context;
            Config = config.Value;
        }

        public async Task OnGetAsync()
        {
            // TODO: CountAsync()
            NumTransactions = _context.Transactions.Count();
            NumBudgetTxs = _context.BudgetTxs.Count();
            NumPayees = _context.Payees.Count();

            HasSomeData = NumTransactions > 0 || NumBudgetTxs > 0 || NumPayees > 0;
            HasAllData = NumTransactions > 0 && NumBudgetTxs > 0 && NumPayees > 0;
        }
    }
}
