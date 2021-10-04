﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YoFi.AspNet.Models;

namespace YoFi.SampleGen
{
    /// <summary>
    /// Defines a single pattern of yearly spending
    /// </summary>
    /// <remarks>
    /// The sample data generator will use this to generate a series of transactions to
    /// match this spending pattern in a year
    /// </remarks>
    public class SampleDataLineItem
    {
        /// <summary>
        /// Comma-separated list of possible transaction payees
        /// </summary>
        public string Payee { get; set; }

        /// <summary>
        /// How frequently this spending happens
        /// </summary>
        public FrequencyEnum DateFrequency { get; set; }

        /// <summary>
        /// How much variability (jitter) is there between the dates of multiple transactions of
        /// the same pattern
        /// </summary>
        public JitterEnum DateJitter { get; set; }


        /// <summary>
        /// Transaction category
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// The target amount to spend yearly in this pattern
        /// </summary>
        public decimal AmountYearly { get; set; }

        /// <summary>
        /// How much variability (jitter) is there between the amounts of multiple transactions of
        /// the same pattern
        /// </summary>
        public JitterEnum AmountJitter { get; set; }

        /// <summary>
        /// User-defined grouping for multiple patterns
        /// </summary>
        /// <remarks>
        /// Generator will combine these all into a single transaction, with the multiple
        /// patterns as splits
        /// </remarks>
        public string Group { get; set; }

        /// <summary>
        /// What is the year we are operating on?
        /// </summary>
        public static int Year { get; set; } = DateTime.Now.Year;

        /// <summary>
        /// How many per week do we mean when the frequency is "manyperweek"?
        /// </summary>
        /// <remarks>
        /// This will be generalized into the pattern definition in the future
        /// See Task #1098: Add a multiplier generally
        /// </remarks>
        public const int HowManyPerWeek = 3;

        /// <summary>
        /// How much jitter exactly is there in a given kind of amount jitter?
        /// </summary>
        /// <remarks>
        /// This is a +/- mutiplier on the transaction. e,g, for Moderate jitter, the
        /// actual amount will be between 60% and 140% of the target amount
        /// </remarks>
        public static Dictionary<JitterEnum, double> AmountJitterValues = new Dictionary<JitterEnum, double>()
        {
            { JitterEnum.None, 0 },
            { JitterEnum.Low, 0.1 },
            { JitterEnum.Moderate, 0.4 },
            { JitterEnum.High, 0.9 }
        };

        /// <summary>
        /// How much jitter exactly is there in a given kind of date jitter?
        /// </summary>
        /// <remarks>
        /// This expressed how large of range relative to the target period in
        /// which all the transactions should appear. e.g. for Low jitter,
        /// a "Monthly" pattern would generate transactions all within the same 7 days.
        /// </remarks>
        public static Dictionary<JitterEnum, double> DateJitterValues = new Dictionary<JitterEnum, double>()
        {
            { JitterEnum.None, 0 },
            { JitterEnum.Low, 0.25 },
            { JitterEnum.Moderate, 0.4 },
            { JitterEnum.High, 1.0 }
        };

        /// <summary>
        /// How many days are there in a given frequency?
        /// </summary>
        /// <remarks>
        /// This is public so the unit tests can access them
        /// </remarks>
        public static Dictionary<FrequencyEnum, TimeSpan> SchemeTimespans = new Dictionary<FrequencyEnum, TimeSpan>()
        {
            { FrequencyEnum.Weekly, TimeSpan.FromDays(7) },
            { FrequencyEnum.ManyPerWeek, TimeSpan.FromDays(7) },
            { FrequencyEnum.Monthly, TimeSpan.FromDays(28) },
            { FrequencyEnum.Quarterly, TimeSpan.FromDays(90) },
            { FrequencyEnum.Yearly, TimeSpan.FromDays(365) },
        };

        /// <summary>
        /// In case you forgot
        /// </summary>
        const int MonthsPerYear = 12;
        const int WeeksPerYear = 52;
        const int DaysPerWeek = 7;
        const int MonthsPerQuarter = 3;

        /// <summary>
        /// For a given frequency, how many transactions will be in a year?
        /// </summary>
        /// <remarks>
        /// This is public so the unit tests can access them
        /// </remarks>
        public static Dictionary<FrequencyEnum, int> FrequencyPerYear = new Dictionary<FrequencyEnum, int>()
        {
            { FrequencyEnum.ManyPerWeek, WeeksPerYear * HowManyPerWeek },
            { FrequencyEnum.Weekly, WeeksPerYear },
            { FrequencyEnum.SemiMonthly, 2 * MonthsPerYear },
            { FrequencyEnum.Monthly, MonthsPerYear },
            { FrequencyEnum.Quarterly, MonthsPerYear / MonthsPerQuarter },
            { FrequencyEnum.Yearly, 1 },
        };

        /// <summary>
        /// On what days exactly do the SemiWeekly transactions occur?
        /// </summary>
        private readonly int[] SemiWeeklyDays = new int[] { 1, 15 };

        /// <summary>
        /// Generate transactions for a given pattern (or group of patterns)
        /// </summary>
        /// <remarks>
        /// For a group of patterns, you'll need to pick a "main" pattern which gives the payee
        /// and date parameters. The group patterns will be used for amount and category.
        /// </remarks>
        /// <param name="group">Optional grouping of patterns to be turned into single transactions</param>
        /// <returns>The transactions generated</returns>
        public IEnumerable<Transaction> GetTransactions(IEnumerable<SampleDataLineItem> group = null)
        {
            // Many Per Week overrides the date jitter to high
            if (DateFrequency == FrequencyEnum.ManyPerWeek)
                DateJitter = JitterEnum.High;

            // Check for invalid parameter combinations
            if (DateFrequency == FrequencyEnum.SemiMonthly && DateJitter != JitterEnum.None && DateJitter != JitterEnum.Invalid)
                throw new NotImplementedException("SemiMonthly with date jitter is not implemented");

            // Randomly choose a window. The Window must be entirely within the Scheme Timespan, but chosen at random.
            // The size of the window is given by the Date Jitter.
            if (DateFrequency != FrequencyEnum.SemiMonthly)
            {
                DateWindowLength = (DateJitter == JitterEnum.None) ? TimeSpan.FromDays(1) : SchemeTimespans[DateFrequency] * DateJitterValues[DateJitter];
                DateWindowStarts = TimeSpan.FromDays(random.Next(0, SchemeTimespans[DateFrequency].Days - DateWindowLength.Days));
            }

            Payees = Payee.Split(",").ToList();

            var splits = group ?? new List<SampleDataLineItem> { this };

            if (DateFrequency == FrequencyEnum.Invalid)
                throw new ApplicationException("Invalid date frequency");
            else if (DateFrequency == FrequencyEnum.SemiMonthly)
                return Enumerable.Range(1, MonthsPerYear).SelectMany(month => SemiWeeklyDays.Select(day => GenerateBaseTransaction(splits, new DateTime(Year, month, day))));
            else if (DateFrequency == FrequencyEnum.ManyPerWeek)
                return Enumerable.Range(1, HowManyPerWeek).SelectMany(x => Enumerable.Range(1, WeeksPerYear).Select(w => GenerateTypicalTransaction(w, splits))).OrderBy(x => x.Timestamp);
            else
                return Enumerable.Range(1, FrequencyPerYear[DateFrequency]).Select(x => GenerateTypicalTransaction(x, splits));
        }

        /// <summary>
        /// Our own random number generator
        /// </summary>
        private static Random random = new Random();

        /// <summary>
        /// For transactions generated in this pattern, what is the earliest day they can fall?
        /// </summary>
        private TimeSpan DateWindowStarts;

        /// <summary>
        /// For transactions generated in this pattern, how large of a possible dates is there?
        /// </summary>
        private TimeSpan DateWindowLength;

        /// <summary>
        /// Generate an invidifual transaction for MOST freqencies
        /// </summary>
        /// <param name="index">Which # within the frequency are we so far?</param>
        /// <param name="group">Optional grouping of patterns to be turned into single transactions</param>
        /// <returns>The transactions generated</returns>
        private Transaction GenerateTypicalTransaction(int index, IEnumerable<SampleDataLineItem> group) =>
            GenerateBaseTransaction(group,
                DateFrequency switch
                {
                    FrequencyEnum.Monthly => new DateTime(Year, index, 1),
                    FrequencyEnum.Yearly => new DateTime(Year, 1, 1),
                    FrequencyEnum.Quarterly => new DateTime(Year, index * MonthsPerQuarter - 2, 1),
                    FrequencyEnum.ManyPerWeek => new DateTime(Year, 1, 1) + TimeSpan.FromDays(DaysPerWeek * (index - 1)),
                    FrequencyEnum.Weekly => new DateTime(Year, 1, 1) + TimeSpan.FromDays(DaysPerWeek * (index - 1)),
                    _ => throw new NotImplementedException()
                } + JitterizedDate
            );

        /// <summary>
        /// Foundational generator. Actually generates the transaction
        /// </summary>
        /// <remarks>
        /// The thing that actually varies between different frequencies is the generation of the date.
        /// So, you figure that out and tell us.
        /// </remarks>
        /// <param name="timestamp">What exact timestamp to assign to this transaction</param>
        /// <param name="group">Optional grouping of patterns to be turned into single transactions</param>
        /// <returns>The transactions generated</returns>
        private Transaction GenerateBaseTransaction(IEnumerable<SampleDataLineItem> group, DateTime timestamp)
        {
            var generatedsplits = group.Select(s => new Split()
            {
                Category = s.Category,
                Amount = s.JitterizeAmount(s.AmountYearly / FrequencyPerYear[DateFrequency])
            }).ToList();

            return new Transaction()
            {
                Payee = JitterizedPayee,
                Splits = generatedsplits.Count > 1 ? generatedsplits : null,
                Timestamp = timestamp,
                Category = generatedsplits.Count == 1 ? generatedsplits.Single().Category : null,
                Amount = generatedsplits.Sum(x => x.Amount)
            };
        }

        /// <summary>
        /// The indivual payees which can be used in this transaction
        /// </summary>
        /// <remarks>
        /// Turn into a list for so it's easy to use them internally
        /// </remarks>
        private List<string> Payees;

        /// <summary>
        /// Create a varied amount, based on the target amount specified, such that the
        /// jitter values are respected.
        /// </summary>
        /// <param name="amount">Target amount</param>
        /// <returns>Randomized amount within the desired ditter</returns>
        private decimal JitterizeAmount(decimal amount) =>
            (AmountJitter == JitterEnum.None) ? amount :
                (decimal)((double)amount * (1.0 + 2.0 * (random.NextDouble() - 0.5) * AmountJitterValues[AmountJitter]));

        /// <summary>
        /// Create a date modifer to apply to a base date such that the resulting date
        /// will fit within the date jitter parameters
        /// </summary>
        private TimeSpan JitterizedDate => 
            DateWindowStarts + ((DateJitter != JitterEnum.None) ? TimeSpan.FromDays(random.Next(0, DateWindowLength.Days)) : TimeSpan.Zero);

        /// <summary>
        /// Create a payee within the set specified
        /// </summary>
        private string JitterizedPayee => Payees[random.Next(0, Payees.Count)];
    }

    /// <summary>
    /// Defines how frequently a pattern may occur
    /// </summary>
    public enum FrequencyEnum { Invalid = 0, ManyPerWeek, Weekly, SemiMonthly, Monthly, Quarterly, Yearly };

    /// <summary>
    /// Describes the severity of jitter which may be applied to dates or amounts
    /// </summary>
    public enum JitterEnum { Invalid = 0, None, Low, Moderate, High };
}
