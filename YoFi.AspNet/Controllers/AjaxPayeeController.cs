﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoFi.Core.Repositories;

namespace YoFi.AspNet.Controllers
{
    [Produces("application/json")]
    [Route("ajax/payee")]
    public class AjaxPayeeController: Controller
    {
        #region Fields
        private readonly IPayeeRepository _repository;
        #endregion

        #region Constructor
        public AjaxPayeeController(IPayeeRepository repository) 
        {
            _repository = repository;
        }
        #endregion

        [HttpPost("select/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<ApiResult> Select(int id, bool value)
        {
            try
            {
                var payee = await _repository.GetByIdAsync(id);
                payee.Selected = value;
                await _repository.UpdateAsync(payee);

                return new ApiResult();
            }
            catch (Exception ex)
            {
                return new ApiResult(ex);
            }
        }

    }
}
