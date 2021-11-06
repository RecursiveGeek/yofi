﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YoFi.Core.Models
{
    /// <summary>
    /// Any model item in our system needs to fulfill these things
    /// </summary>
    public interface IModelItem<T>: IID
    {
        /// <summary>
        /// Generate a query to place the <paramref name="original"/> items in the correct
        /// default order for this kind of item
        /// </summary>
        /// <param name="original"></param>
        IQueryable<T> InDefaultOrder(IQueryable<T> original);

        /// <summary>
        /// Get a hash code base on the uniqueness defined in ImportEquals
        /// </summary>
        /// <returns>The hash code</returns>
        int GetImportHashCode();

        /// <summary>
        /// Whether this object is equal to <paramref name="other"/> for the purposes
        /// of import. That is, whether the other object is a duplicate during import
        /// </summary>
        /// <param name="other"></param>
        /// <returns>'true' if is an import duplicate of <paramref name="other"/></returns>
        bool ImportEquals(T other);
    }
}
