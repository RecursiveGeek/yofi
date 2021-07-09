﻿using OfxWeb.Asp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OfxWeb.Asp.Controllers.Reports
{
    /// <summary>
    /// A query of reportable items, with an optional name
    /// </summary>
    public class NamedQuery
    {
        public string Name { get; set; }
        public IQueryable<IReportable> Query { get; set; }
        public bool LeafRowsOnly { get; set; } = false;

        public NamedQuery Labeled(string newname) => new NamedQuery() { Name = newname, Query = Query, LeafRowsOnly = LeafRowsOnly };
        public NamedQuery AsLeafRowsOnly(bool leafrows) => new NamedQuery() { Name = Name, Query = Query, LeafRowsOnly = leafrows };
    }
}
