﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace YoFi.SampleGen
{
    public class Definition
    {
        public string Category { get; set; }
        public string Payee { get; set; }
        public decimal YearlyAmount { get; set; }
        public SchemeEnum Scheme { get; set; }
        public JitterEnum DateJitter { get; set; }
        public JitterEnum AmountJitter { get; set; }

        public IEnumerable<Transaction> GetTransactions() => Scheme switch
        {
            SchemeEnum.Invalid => throw new ApplicationException("Invalid scheme"),
            SchemeEnum.Yearly => GenerateYearly(),
            SchemeEnum.Monthly => GenerateMonthly(),
            SchemeEnum.SemiMonthly => GenerateSemiMonthly(),
            SchemeEnum.Quarterly => GenerateQuarterly(),
            SchemeEnum.Weekly => GenerateWeekly(),
            SchemeEnum.ManyPerWeek => GenerateManyPerWeek(),
            _ => throw new NotImplementedException()
        };

        public static int Year { get; set; } = DateTime.Now.Year;

        private static Random random = new Random();

        public static Dictionary<JitterEnum, double> AmountJitterValues = new Dictionary<JitterEnum, double>()
        {
            { JitterEnum.None, 0 },
            { JitterEnum.Low, 0.1 },
            { JitterEnum.Moderate, 0.4 },
            { JitterEnum.High, 0.9 }
        };

        public static Dictionary<JitterEnum, double> DateJitterValues = new Dictionary<JitterEnum, double>()
        {
            { JitterEnum.None, 0 },
            { JitterEnum.Low, 0.25 },
            { JitterEnum.Moderate, 0.4 },
            { JitterEnum.High, 1.0 }
        };

        public static Dictionary<SchemeEnum, TimeSpan> SchemeTimespans = new Dictionary<SchemeEnum, TimeSpan>()
        {
            { SchemeEnum.Weekly, TimeSpan.FromDays(7) },
            { SchemeEnum.ManyPerWeek, TimeSpan.FromDays(7) },
            { SchemeEnum.Monthly, TimeSpan.FromDays(28) },
            { SchemeEnum.Quarterly, TimeSpan.FromDays(90) },
            { SchemeEnum.Yearly, TimeSpan.FromDays(365) },
        };

        private TimeSpan DateWindowStarts;
        private TimeSpan DateWindowLength;

        private IEnumerable<Transaction> GenerateYearly()
        {
            SetDateWindow();

            return new List<Transaction>()
            {
                new Transaction() { Amount = JitterizeAmount(YearlyAmount), Category = Category, Payee = Payee, Timestamp = new DateTime(Year,1,1) + JitterizedDate }
            };
        }

        private IEnumerable<Transaction> GenerateMonthly()
        {
            SetDateWindow();

            return Enumerable.Range(1, 12).Select
            (
                month => new Transaction() { Amount = JitterizeAmount(YearlyAmount/12), Category = Category, Payee = Payee, Timestamp = new DateTime(Year, month, 1) + JitterizedDate }
            );
        }

        private IEnumerable<Transaction> GenerateSemiMonthly()
        {
            if (DateJitter != JitterEnum.None && DateJitter != JitterEnum.Invalid)
                throw new NotImplementedException("SemiMonthly with date jitter is not implemented");

            var days = new int[] { 1, 15 };

            return Enumerable.Range(1, 12).SelectMany
            (
                month => 
                days.Select
                ( 
                    day =>                
                    new Transaction() { Amount = JitterizeAmount(YearlyAmount / 24), Category = Category, Payee = Payee, Timestamp = new DateTime(Year, month, day) }
                )
            );
        }

        private IEnumerable<Transaction> GenerateQuarterly()
        {
            SetDateWindow();

            return Enumerable.Range(0, 4).Select
            (
                q => new Transaction() { Amount = JitterizeAmount(YearlyAmount / 4), Category = Category, Payee = Payee, Timestamp = new DateTime(Year, 1+q*3, 1) + JitterizedDate }
            );
        }

        private IEnumerable<Transaction> GenerateWeekly(decimal amount = 0)
        {
            if (0 == amount)
                amount = YearlyAmount / 52;

            SetDateWindow();

            return Enumerable.Range(0, 52).Select
            (
                week => new Transaction() { Amount = JitterizeAmount(amount), Category = Category, Payee = Payee, Timestamp = new DateTime(Year, 1, 1) + TimeSpan.FromDays(7 * week) + JitterizedDate }
            );
        }

        private IEnumerable<Transaction> GenerateManyPerWeek()
        {
            // Many Per Week overrides the date jitter to high
            DateJitter = JitterEnum.High;

            SetDateWindow();

            int numperweek = 3;

            return Enumerable.Repeat(0, numperweek).SelectMany(x => GenerateWeekly(YearlyAmount/52/numperweek)).OrderBy(x=>x.Timestamp);
        }

        private decimal JitterizeAmount(decimal amount)
        {
            if (AmountJitter != JitterEnum.None)
            {
                var amountjittervalue = AmountJitterValues[AmountJitter];
                amount = (decimal)((double)amount * (1.0 + 2.0 * (random.NextDouble() - 0.5) * amountjittervalue));
            }

            return amount;
        }

        private void SetDateWindow()
        {
            // Randomly choose a window. The Window must be entirely within the Scheme Timespan, but chosen at random.
            // The size of the window is given by the Date Jitter.

            DateWindowLength = TimeSpan.FromDays(1);
            if (DateJitter != JitterEnum.None)
            {
                DateWindowLength = SchemeTimespans[Scheme] * DateJitterValues[DateJitter];
            }
            DateWindowStarts = TimeSpan.FromDays(random.Next(0, SchemeTimespans[Scheme].Days - DateWindowLength.Days));
        }

        private TimeSpan JitterizedDate => DateWindowStarts + ((DateJitter != JitterEnum.None) ? TimeSpan.FromDays(random.Next(0, DateWindowLength.Days)) : TimeSpan.Zero);

    }

    public enum SchemeEnum { Invalid = 0, ManyPerWeek, Weekly, SemiMonthly, Monthly, Quarterly, Yearly };
    public enum JitterEnum { Invalid = 0, None, Low, Moderate, High };
}
