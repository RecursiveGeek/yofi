﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OfxWeb.Asp.Models
{
    public interface ICatSubcat
    {
        string Category { get; set; }
        string SubCategory { get; set; }
    }
}