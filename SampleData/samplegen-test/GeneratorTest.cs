using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace YoFi.SampleGen.Tests
{
    [TestClass]
    public class GeneratorTest
    {
        [TestMethod]
        public void YearlySimple()
        {
            // Given: Yearly Scheme, No Jitter
            var item = new Definition() { Scheme = SchemeEnum.Yearly, YearlyAmount = 1234.56m, AmountJitter = JitterEnum.None, DateJitter = JitterEnum.None, Category = "Category", Payee = "Payee" };

            // When: Generating transactions
            var actual = item.GetTransactions();

            // Then: There is only one transaction (it's yearly)
            Assert.AreEqual(1, actual.Count());

            // And: The amount is exactly what's in the definition
            Assert.AreEqual(item.YearlyAmount, actual.Single().Amount);

            // And: The category and payee match
            Assert.AreEqual(item.Payee, actual.Single().Payee);
            Assert.AreEqual(item.Category, actual.Single().Category);
        }

        [DataRow(JitterEnum.Low)]
        [DataRow(JitterEnum.Moderate)]
        [DataRow(JitterEnum.High)]
        [DataTestMethod]
        public void YearlyAmountJitter(JitterEnum jitter)
        {
            // Given: Yearly Scheme, Amount Jitter as supplied
            var amount = 1234.56m;
            var item = new Definition() { Scheme = SchemeEnum.Yearly, YearlyAmount = amount, AmountJitter = jitter, DateJitter = JitterEnum.None, Category = "Category", Payee = "Payee" };

            // When: Generating transactions x100
            var numtries = 100;
            var actual = Enumerable.Repeat(1, numtries).SelectMany(x => item.GetTransactions());

            // Then: There is only one transaction per time we called
            Assert.AreEqual(numtries, actual.Count());

            // And: The amounts vary
            Assert.IsTrue(actual.Any(x => x.Amount != actual.First().Amount));

            // And: The amounts are within the expected range for the supplied jitter
            var jittervalue = Definition.AmountJitterValues[jitter];
            var min = actual.Min(x => x.Amount);
            var max = actual.Max(x => x.Amount);
            Assert.AreEqual((double)(amount * (1 - jittervalue)), (double)min, (double)amount * (double)jittervalue / 5.0);
            Assert.AreEqual((double)(amount * (1 + jittervalue)), (double)max, (double)amount * (double)jittervalue / 5.0);
        }

        [TestMethod]
        public void MonthlySimple()
        {
            // Given: Monthly Scheme, No Jitter
            var amount = 1200.00m;
            var item = new Definition() { Scheme = SchemeEnum.Monthly, YearlyAmount = amount, AmountJitter = JitterEnum.None, DateJitter = JitterEnum.None, Category = "Category", Payee = "Payee" };

            // When: Generating transactions
            var actual = item.GetTransactions();

            // Then: There are exactly 12 transactions (it's monthly)
            Assert.AreEqual(12, actual.Count());

            // And: They are all on the same day
            Assert.IsTrue(actual.All(x => x.Timestamp.Day == actual.First().Timestamp.Day));

            // And: For each transaction...
            foreach (var result in actual)
            {
                // And: The amounts are exactly 1/12 what's in the definition
                Assert.AreEqual(amount / 12, result.Amount);

                // And: The category and payee match
                Assert.AreEqual(item.Payee, result.Payee);
                Assert.AreEqual(item.Category, result.Category);
            }
        }

    }
}