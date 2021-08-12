using Common.NET.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OfxSharp;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace YoFi.Tests
{
    [TestClass]
    public class OfxParserTest
    {
        public OfxDocument Document = null;

        [TestInitialize]
        public async Task SetUp()
        {
            if (null == Document)
            {
                var filename = "ExportedTransactions.ofx";
                var stream = SampleData.Open(filename);

                Document = await OfxDocumentReader.FromSgmlFileAsync(stream);
            }
        }

        [TestMethod]
        public void Empty()
        {
            Assert.IsNotNull(Document);
        }

        [TestMethod]
        public void TransactionsCount()
        {
            Assert.AreEqual(1000, Document.Statements.SelectMany(x=>x.Transactions).Count());
        }

        [TestMethod]
        public void TransactionSample()
        {
            var actual = Document.Statements.First().Transactions.First();

            Assert.AreEqual(-49.68M, actual.Amount);
            Assert.AreEqual("Ext Credit Card Debit SAFEWAY FUEL 0490       WINNEMUCCA     NV USA", actual.Memo.Trim());
            Assert.AreEqual("476365570", actual.ReferenceNumber.Trim());
            Assert.AreEqual(new DateTime(2018, 6, 2), actual.Date.Value.DateTime);
        }
    }
}
