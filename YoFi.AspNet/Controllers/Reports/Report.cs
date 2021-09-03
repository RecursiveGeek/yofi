﻿using YoFi.AspNet.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YoFi.AspNet.Controllers.Reports
{
    /// <summary>
    /// Table-based report: Arranges IReportable items into tables
    /// </summary>
    /// <remarks>
    /// General usage: 
    /// 1. Set the Source
    /// 2. Set any needed configuration properties
    /// 3. Build the report
    /// 4. Iterate over rows/columns to display the report
    /// 
    /// Note that there is no app-specific logic of any form in this
    /// class.
    /// </remarks>
    public class Report : IComparer<RowLabel>
    {
        #region Configuration Properties

        /// <summary>
        /// Display name of the report
        /// </summary>
        public string Name { get; set; } = "Report";

        /// <summary>
        /// In-depth description of the report for display
        /// </summary>
        /// <remarks>
        /// In practice, this is used exclusively to describe the timeframe covered by
        /// the report, so could be renamed "timeframe"
        /// </remarks>
        public string Description { get; set; }

        /// <summary>
        /// Where to get items
        /// </summary>
        /// <remarks>
        /// Multiple queries can have the same name, in which case they
        /// are added together as if we concatenated the queries
        /// </remarks>
        public IEnumerable<NamedQuery> Source { get; set; }

        /// <summary>
        /// Whether to include columns for individual months
        /// </summary>
        public bool WithMonthColumns { get; set; } = false;

        /// <summary>
        /// Whether to include a total column
        /// </summary>
        public bool WithTotalColumn { get; set; } = true;

        /// <summary>
        /// How many levels of depth to skip before showing the report
        /// So 0 means start at the top level,
        /// 1 is the 2nd level down
        /// </summary>
        public int SkipLevels { get; set; }

        /// <summary>
        /// How many levels deep to include
        /// </summary>
        public int NumLevels { get; set; } = 1;

        /// <summary>
        /// When displaying the report, how many levels higher to show as
        /// </summary>
        /// <remarks>
        /// Typically the lowest-level of display formatting doesn't look great on its
        /// own, so when NumLevels is 1, it's good to set DisplayLevelAdjustment to 1
        /// and make it look like a heading.
        /// </remarks>
        public int DisplayLevelAdjustment { get; set; }

        /// <summary>
        /// All the possible ways you can sort the report
        /// </summary>
        public enum SortOrders { NameAscending, TotalAscending, TotalDescending };

        /// <summary>
        /// In what order should the report be sorted
        /// </summary>
        public SortOrders SortOrder { get; set; } = SortOrders.TotalDescending;

        /// <summary>
        /// Which column to use for ordering when SortOrder is set to TotalAscending or 
        /// TotalDescending. 
        /// </summary>
        /// <remarks>
        /// By default TotalColumn is used, unless it isn't being shown, in which case the
        /// first column is used
        /// </remarks>
        public ColumnLabel OrderingColumn
        {
            get
            {
                if (_OrderingColumn != null)
                    return _OrderingColumn;
                if (WithTotalColumn)
                    return TotalColumn;

                return ColumnLabels.First();
            }
            set
            {
                _OrderingColumn = value;
            }
        }
        ColumnLabel _OrderingColumn;

        #endregion

        #region Informative Properties

        /// <summary>
        /// Indexer to retrieve report cells
        /// </summary>
        /// <param name="column">Which column</param>
        /// <param name="row">Which row</param>
        /// <returns>Value at that <paramref name="column"/>,<paramref name="row"/> position </returns>
        public decimal this[ColumnLabel column, RowLabel row]
        {
            get
            {
                if (column.IsCalculated)
                    return column.Custom(RowDetails(row));
                else
                    return Table[column, row];
            }
        }

        /// <summary>
        /// All the row labels, in the correct order as specified by SortOrder
        /// </summary>
        public IEnumerable<RowLabel> RowLabelsOrdered => Table.RowLabels.OrderBy(x => x, this);

        /// <summary>
        /// All the row labels, in internal order
        /// </summary>
        public IEnumerable<RowLabel> RowLabels => Table.RowLabels.OrderBy(x => x);

        /// <summary>
        /// All the column labels which should be shown, as modified by configuration properties
        /// </summary>
        public IEnumerable<ColumnLabel> ColumnLabelsFiltered => ColumnLabels.Where(x => WithTotalColumn || !x.IsTotal);

        /// <summary>
        /// All the column labels, in internal order
        /// </summary>
        public IEnumerable<ColumnLabel> ColumnLabels => Table.ColumnLabels.OrderBy(x => x);

        /// <summary>
        /// The row which contains the total for columns
        /// </summary>
        public RowLabel TotalRow { get; } = new RowLabel() { IsTotal = true };

        /// <summary>
        /// The column which contains the total for rows
        /// </summary>
        public ColumnLabel TotalColumn { get; } = new ColumnLabel() { IsTotal = true };

        /// <summary>
        /// The total of the total column
        /// </summary>
        public decimal GrandTotal => this[TotalColumn, TotalRow];

        #endregion

        #region Public Methods

        /// <summary>
        /// Add a custom calculated column 
        /// </summary>
        /// <param name="column"></param>
        public void AddCustomColumn(ColumnLabel column)
        {
            if (column.Custom == null)
                throw new ArgumentOutOfRangeException(nameof(column), "Column must contain a custom function");

            column.IsCalculated = true;
            column.IsTotal = false;
            Table.ColumnLabels.Add(column);
        }

        /// <summary>
        /// Load configuration parameters from supplied <paramref name="definition"/>
        /// </summary>
        /// <param name="definition"></param>
        public void LoadFrom(ReportDefinition definition)
        {
            if (!string.IsNullOrEmpty(definition.Name))
                Name = definition.Name;

            Report.SortOrders order;
            if (!string.IsNullOrEmpty(definition.SortOrder) && Enum.TryParse<Report.SortOrders>(definition.SortOrder, out order))
                SortOrder = order;

            if (definition.WithMonthColumns.HasValue)
                WithMonthColumns = definition.WithMonthColumns.Value;

            if (definition.WithTotalColumn.HasValue)
                WithTotalColumn = definition.WithTotalColumn.Value;

            if (definition.SkipLevels.HasValue)
                SkipLevels = definition.SkipLevels.Value;

            if (definition.NumLevels.HasValue)
                NumLevels = definition.NumLevels.Value;

            if (definition.DisplayLevelAdjustment.HasValue)
                DisplayLevelAdjustment = definition.DisplayLevelAdjustment.Value;
        }

        /// <summary>
        /// Build the report
        /// </summary>
        public void Build()
        {
            if (NumLevels < 1)
                throw new ArgumentOutOfRangeException(nameof(NumLevels), "Must be 1 or greater");

            if (Source == null)
                throw new ArgumentOutOfRangeException(nameof(Source), "Must set a source");

            // Build each query individually
            foreach (var query in Source)
                BuildPhase_Group(query);
        }

        /// <summary>
        /// Render the report to console
        /// </summary>
        /// <param name="sorted">Whether to show rows in sorted order</param>
        public void WriteToConsole(bool sorted = false)
        {
            if (!RowLabels.Any())
                return;

            var maxlevel = RowLabels.Max(x => x.Level);

            // Columns

            var builder = new StringBuilder();
            var name = Name ?? string.Empty;
            var padding = (maxlevel > 0) ? String.Concat(Enumerable.Repeat<char>(' ', maxlevel)) : string.Empty;
            builder.Append($"+ {name,15}{padding} ");

            foreach (var col in ColumnLabelsFiltered)
            {
                name = col.Name;
                if (col.IsTotal)
                    name = "TOTAL";
                builder.Append($"| {name,10} ");
            }

            Console.WriteLine(builder.ToString());

            // Rows

            var rows = sorted ? RowLabelsOrdered : RowLabels;
            foreach (var row in rows)
            {
                builder = new StringBuilder();

                name = row.Name;
                if (row.IsTotal)
                    name = "TOTAL";
                if (name == null)
                    name = "-";

                var padding_before = string.Concat( Enumerable.Repeat<char>('>', row.IsTotal ? 0 : maxlevel - row.Level));
                var padding_after = string.Concat( Enumerable.Repeat<char>(' ', row.IsTotal ? maxlevel : row.Level));

                builder.Append($"{row.Level} {padding_before}{name,-15}{padding_after} ");

                foreach (var col in ColumnLabelsFiltered)
                {
                    var val = this[col, row];
                    var format = col.DisplayAsPercent ? "P0" : "C2";
                    builder.Append($"| {val.ToString(format),10} ");
                }

                builder.Append($" {row}");

                Console.WriteLine(builder.ToString());
            }
        }

        /// <summary>
        /// Serialize the report to JSON
        /// </summary>
        /// <returns>JSON-encoded string for this report</returns>
        public string ToJson()
        {
            string result = string.Empty;

            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream,options:new JsonWriterOptions() { Indented = true }))
                {
                    writer.WriteStartArray();

                    foreach (var line in RowLabelsOrdered)
                    {
                        writer.WriteStartObject();

                        writer.WritePropertyName("Name");
                        writer.WriteStringValue(line.Name ?? "-");
                        writer.WritePropertyName("ID");
                        writer.WriteStringValue(line.UniqueID);
                        writer.WritePropertyName("IsTotal");
                        writer.WriteBooleanValue(line.IsTotal);
                        writer.WritePropertyName("Level");
                        writer.WriteNumberValue(line.Level);

                        foreach (var col in ColumnLabelsFiltered)
                        {
                            var val = this[col, line];
                            var name = col.ToString();
                            if (col.DisplayAsPercent)
                                name += "%";

                            writer.WritePropertyName(name);
                            writer.WriteNumberValue(val);
                        }
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.Flush();

                    var bytes = stream.ToArray();
                    result = Encoding.UTF8.GetString(bytes);
                }
            }

            return result;

        }

        #endregion

        #region Fields
        Table<ColumnLabel, RowLabel, decimal> Table = new Table<ColumnLabel, RowLabel, decimal>();
        #endregion

        #region Internal Methods

        /// <summary>
        /// Group source transactions by name/month and calculate totals, and call remaining
        /// phases.
        /// </summary>
        /// <remarks>
        /// Phases:
        ///  1. Group. Group source transactions by name/month and calculate totals
        ///  2. Place. Place each incoming data point into a report cell.
        ///  3. Propagate. Propagate those values upward into totalling rows.
        ///  4. Prune. Prune out rows that are not really needed.
        /// </remarks>
        /// <param name="source">Source of reportables, with optional series name key</param>

        private void BuildPhase_Group(NamedQuery source)
        {
            IQueryable<IGrouping<object, IReportable>> groups;
            if (WithMonthColumns)
                groups = source.Query.GroupBy(x => new { Name = x.Category, Month = x.Timestamp.Month });
            else
                groups = source.Query.GroupBy(x => new { Name = x.Category });

            //  1. Group. Group source transactions by name/month and calculate totals
            var selected = groups.Select(g => new { Key = g.Key, Total = g.Sum(y => y.Amount) });

            //  2. Place. Place each incoming data point into a report cell.
            BuildPhase_Place(source: selected, oquery: source);
        }

        /// <summary>
        /// Place data into report, and call remaining phases
        /// </summary>
        /// <remarks>
        /// The heart of V3 reports is to push the summing of items into the server side.
        /// This method is the first place where data is actually brought into the client
        /// and worked on.
        /// </remarks>
        /// <param name="source">Report data</param>
        /// <param name="oquery">Original query</param>
        private void BuildPhase_Place(IQueryable<dynamic> source, NamedQuery oquery)
        {
            // If this query has a NAME, then we are also collecting the values in a
            // series column.
            var seriescolumn = string.IsNullOrEmpty(oquery?.Name) ? 
                null : 
                new ColumnLabel() 
                { 
                    Name = oquery.Name, 
                    UniqueID = oquery.Name, 
                    LeafNodesOnly = oquery.LeafRowsOnly 
                };

            // The pattern for collector rows
            var collectorregex = new Regex("(.+?)\\[(.+?)\\]");

            //  2. Place. Place each incoming data point into a report cell.
            foreach (var cell in source)
            {
                string dynamicname = "[Blank]";
                if (!string.IsNullOrEmpty(cell.Key.Name))
                    dynamicname = cell.Key.Name;
                var keys = dynamicname.Split(':').Skip(SkipLevels).ToList();
                if (keys.Any())
                {
                    // Is this a collector row?
                    var match = collectorregex.Match(keys.Last());
                    string collector = null;
                    if (match.Success)
                    {
                        // Replace the last key with only the first part of the match
                        keys[keys.Count()-1] = match.Groups[1].Value;

                        // The second part IS the collector rule
                        collector = match.Groups[2].Value;
                    }

                    // Reconstitute the category from parts
                    var id = string.Join(':', keys) + (!oquery.LeafRowsOnly ? ":" : string.Empty);

                    // Leaf-rows reports use the ID as a name.
                    var name = oquery.LeafRowsOnly ? id : null;

                    // Does this row already exist?
                    var row = RowLabels.Where(x => x.UniqueID == id).SingleOrDefault();

                    // If not, we'll go ahead and create one now
                    if (row == null)
                        row = new RowLabel() { Name = name, UniqueID = id, Collector = collector };

                    // Place the value in the correct month column if applicable
                    ColumnLabel column = null;
                    if (WithMonthColumns)
                    {
                        column = new ColumnLabel() 
                        { 
                            UniqueID = cell.Key.Month.ToString("D2"), 
                            Name = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(cell.Key.Month)
                        };
                        Table[column, row] += cell.Total;
                    }

                    // Place the value in the correct series column, if applicable
                    if (seriescolumn != null)
                        Table[seriescolumn, row] += cell.Total;

                    // Place the value in the total column
                    Table[TotalColumn, row] += cell.Total;

                    // 3. Propagate. Propagate those values upward into totalling rows.
                    // By definition "leaf rows only" means DON'T propagate into totalling rows
                    if (!oquery.LeafRowsOnly)
                        BuildPhase_Propagate(row: row, column: column, seriescolumn: seriescolumn, amount: cell.Total);
                }
            }

            //  4. Prune. Prune out rows that are not really needed.
            // Don't need to prune leaf rows only, because extraneous rows were never propagated in the first place
            if (!oquery.LeafRowsOnly)
                BuildPhase_Prune();
        }

        /// <summary>
        /// Propagate values upward to be collected by all generations of parents
        /// </summary>
        /// <param name="amount">The amount value for this cell</param>
        /// <param name="row">The current row, upward from which we are going to propagate</param>
        /// <param name="column">The month column where this cell exists, or null</param>
        /// <param name="seriescolumn">The series column which is collecting series totals, or null if there isn't one</param>
        private void BuildPhase_Propagate(decimal amount, RowLabel row, ColumnLabel column = null, ColumnLabel seriescolumn = null)
        {
            // Who is our parent? Based on ID. e.g. if we are A:B:C, our parent is A:B
            var split = row.UniqueID.Split(':');
            var parentsplit = split.SkipLast(1);

            // If we DO have a parent...
            if (parentsplit.Any())
            {
                // Find the actual parent ROW, given that ID. Or create if not exists
                var parentid = string.Join(':', parentsplit);
                var parentrow = RowLabels.Where(x => x.UniqueID == parentid).SingleOrDefault();
                if (parentrow == null)
                    parentrow = new RowLabel() { Name = parentsplit.Last(), UniqueID = parentid };
                row.Parent = parentrow;

                // Do the accumlation of values into the parent row
                Table[TotalColumn, parentrow] += amount;
                if (column != null)
                    Table[column, parentrow] += amount;
                if (seriescolumn != null)
                    Table[seriescolumn, parentrow] += amount;

                // Then recursively accumulate upwards to the grandparent
                BuildPhase_Propagate(amount: amount, row: parentrow, column: column, seriescolumn: seriescolumn);

                // Now that the parent has been propagated, we finally know what level WE are, so we can set it here
                row.Level = parentrow.Level - 1;

                // User Story 819: Managed Budget Report
                // There is also a case where a sibling wants to collect our values also. We will deal with that here.
                // A valid peer-collection row will have an ID like A:B:X[^C], which collects all A:B children except
                // A:B:C, into a row named A:B:X.
                // The peer-collection row will be in place before we get here. So if the peer-collector is not
                // found, we are not peer-collecting.
                // Peer collection is (currently) only done for series values.
                if (seriescolumn != null)
                {
                    // Let's find any peer-collecting rows where we share a common category parent
                    var peerstart = parentid;
                    var foundrows = RowLabels.Where(x => x.Collector != null && x.UniqueID.StartsWith(parentid) && ( x.UniqueID.Count(y=>y==':') == 1 + parentid.Count(y=>y==':')));

                    // Consider each peer-collector to see if it collects US
                    foreach(var collectorrow in foundrows)
                    {
                        var rule = collectorrow.Collector;
                        var isnotlist = rule.StartsWith('^');
                        if (isnotlist)
                            rule = rule.Substring(1);
                        var categories = rule.Split(';');

                        // When 'isnotlist' is false, the catgories array contains categories
                        // we DO match. When it's true, the opposite is true.
                        if (categories.Contains(split.Last()) ^ isnotlist && collectorrow.UniqueID != row.UniqueID)
                        {
                            // Found a peer collector who wants us, let's do the collection.
                            Table[seriescolumn, collectorrow] += amount;
                        }
                    }
                }
            }
            // Else we are a top-level row
            else
            {
                // In this case, we accumulate into the report total, for all applicable columns
                Table[TotalColumn, TotalRow] += amount;
                if (column != null)
                    Table[column, TotalRow] += amount;
                if (seriescolumn != null)
                    Table[seriescolumn, TotalRow] += amount;

                // We now know what level we are, so we can set it here.
                row.Level = NumLevels - 1;
            }
        }

        private void BuildPhase_Prune()
        {
            // This gets rid of the case where we might have A:B: sitting under A:B, with no other children.
            // In that case, A:B: is duplicating A:B, and is ergo needless
            // Note that this can only be done after ALL the series are in place

            // Also will handle the case where one (or more) series in the report is "leaf nodes only"
            // while the others were propagated. In this case, we will prune out rows which don't have
            // a value in leaf-nodes-only series
            var leafnodecolumns = ColumnLabelsFiltered.Where(x => x.LeafNodesOnly);

            var pruned = new HashSet<RowLabel>();
            foreach (var row in RowLabels)
            {
                if (!leafnodecolumns.Any())
                    if (string.IsNullOrEmpty(row.UniqueID.Split(':').Last()))
                        if (row.Parent != null)
                            if (Table[TotalColumn, row] == Table[TotalColumn, row.Parent as RowLabel])
                                pruned.Add(row);

                // Also prune rows that are below the numrows cutoff
                if (row.Level < 0)
                    pruned.Add(row);

                // Also prune rows that aren't included a leaf-rows-only series, IF there is at least one
                // such series. Also flatten series if there is a leaf-rows-only series.
                if (leafnodecolumns.Any())
                {
                    if (leafnodecolumns.Sum(x => Table[x, row]) == 0)
                        pruned.Add(row);
                    else
                    {
                        row.Level = 0;
                        row.Parent = null;
                    }
                }
            }

            Table.RowLabels.RemoveWhere(x => pruned.Contains(x));
        }

        /// <summary>
        /// Retrieve all the columns for a certain row in a focused way
        /// </summary>
        /// <param name="rowLabel">Which row</param>
        /// <returns>Dictionary of column identifier strings to the value in that column</returns>
        Dictionary<string, decimal> RowDetails(RowLabel rowLabel) => ColumnLabels.ToDictionary(x => x.ToString(), x => Table[x, rowLabel]);

        /// <summary>
        /// Row comparer
        /// </summary>
        /// <remarks>
        /// This is the row-sorter. It's somewhat of a challenge to sort a tree of values to sort
        /// within each level but keep the tree sructure intact
        /// </remarks>
        /// <param name="first">First row for comparison</param>
        /// <param name="second">Second row for comparison</param>
        /// <returns>Comparsion value. -1 if <paramref name="first"/> sorts before <paramref name="second"/></returns>
        int CompareRows(RowLabel first, RowLabel second)
        {
            int result = 0;
            var n = new RowLabel() { Name = "null" };

            // Turn this on if need to trace the sort logic
            bool debugout = false;
            Debug.WriteLineIf(debugout,$"Compare [{first??n}] vs [{second??n}]");

            // (1) Total rows always come after other rows
            if (first.IsTotal && second.IsTotal)
                goto done;
            if (first.IsTotal)
            {
                result = 1;
                goto done;
            }
            if (second.IsTotal)
            {
                result = -1;
                goto done;
            }

            // (2) If these two share a common parent, we can compare based on SortOrder
            if (first.Parent == second.Parent)
            {
                var secondval = Table[OrderingColumn, second];
                var firstval = Table[OrderingColumn, first];
                switch (SortOrder)
                {
                    case SortOrders.TotalAscending:
                        Debug.WriteLineIf(debugout, $"Checking Totals: {firstval:C2} vs {secondval:C2}...");
                        result = secondval.CompareTo(firstval);
                        break;
                    case SortOrders.TotalDescending:
                        Debug.WriteLineIf(debugout, $"Checking Totals: {firstval:C2} vs {secondval:C2}...");
                        result = firstval.CompareTo(secondval);
                        break;
                    case SortOrders.NameAscending:
                        result = first.Name?.CompareTo(second.Name) ?? -1;
                        break;
                }
                goto done;
            }

            // (3) Parents always come before their own children

            if (first.Parent == second)
            {
                Debug.WriteLineIf(debugout, "Parent relationship");
                result = 1;
                goto done;
            }
            else if (second.Parent == first)
            {
                Debug.WriteLineIf(debugout, "Parent relationship");
                result = -1;
                goto done;
            }

            // (4) Search upward if can't resolve at this level

            // If one is deeper than the other, run up that parent chain
            if (first.Level < second.Level)
            {
                Debug.WriteLineIf(debugout, $"Try {first} Parent vs {second}");
                result = CompareRows(first.Parent, second);
            }
            else if (second.Level < first.Level)
            {
                Debug.WriteLineIf(debugout, $"Try {first} vs {second} Parent");
                result = CompareRows(first, second.Parent);
            }
            else
            {
                // If we're at the SAME level, run both parents upwards
                Debug.WriteLineIf(debugout, $"Try {first} Parent vs {second} Parent");
                result = CompareRows(first.Parent, second.Parent);
            }

#if false
            // This is a bit of defensive coding. I have never been able to give inputs which get
            // the result to zero, so I don't think we really need this.
            if (result == 0)
                Debug.WriteLineIf(debugout, $"??? Unable to resolve");
#endif
        done:

            if (result < 0)
                Debug.WriteLineIf(debugout, $">> {first??n} is before {second??n}");
            else if (result > 0)
                Debug.WriteLineIf(debugout, $">> {first??n} is after {second??n}");
            else
                Debug.WriteLineIf(debugout, $">> {first??n} is same as {second??n}");

            return result;
        }

        /// <summary>
        /// Row comparer for use by IComparer
        /// </summary>
        /// <see cref="Report.CompareRows(RowLabel, RowLabel)"/>
        /// <param name="first">First row for comparison</param>
        /// <param name="second">Second row for comparison</param>
        /// <returns>Comparsion value. -1 if <paramref name="first"/> sorts before <paramref name="second"/></returns>
        int IComparer<RowLabel>.Compare(RowLabel first, RowLabel second) => CompareRows(first, second);

        #endregion
    }


    /// <summary>
    /// Common elements which are shared by both rows and columns
    /// </summary>
    public abstract class BaseLabel: IComparable<BaseLabel>
    {
        /// <summary>
        /// Tag to uniquely identify the column
        /// </summary>
        /// <remarks>
        /// Currently used to sort, but that will change.
        /// </remarks>
        public string UniqueID { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the label
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Final total-displaying row or column
        /// </summary>
        public bool IsTotal { get; set; }

        /// <summary>
        /// True if this row or column should sort AFTER the totals
        /// </summary>
        public bool IsSortingAfterTotal { get; set; }

        /// <summary>
        /// Determins whether this instance and the specified object have the same value
        /// </summary>
        /// <param name="obj">Another object for comparison</param>
        /// <returns>True if this instance is the same as the specified object</returns>
        public override bool Equals(object obj)
        {
            return obj is BaseLabel label &&
                   UniqueID == label.UniqueID &&
                   Name == label.Name &&
                   IsTotal == label.IsTotal;
        }

        /// <summary>
        /// Returns the hash code for this object
        /// </summary>
        /// <returns>A 32-bit integer hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(UniqueID, Name, IsTotal);
        }

        /// <summary>
        /// Converts the value of this instance to its equivalent string representation
        /// </summary>
        /// <returns>String representation</returns>
        public override string ToString()
        {
            if (IsTotal)
                return "TOTAL";
            if (!string.IsNullOrEmpty(UniqueID))
                return $"ID:{UniqueID}";
            else
                return $"Name:{Name}";
        }

        /// <summary>
        /// Default comparison between base labels
        /// </summary>
        /// <param name="other">Another base label for comparison</param>
        /// <returns></returns>
        int IComparable<BaseLabel>.CompareTo(BaseLabel other)
        {
            int result = IsSortingAfterTotal.CompareTo(other.IsSortingAfterTotal);
            if (result == 0)
                result = IsTotal.CompareTo(other.IsTotal);
            if (result == 0) // Empty orders sort at the END
                result = string.IsNullOrEmpty(UniqueID).CompareTo(string.IsNullOrEmpty(other.UniqueID));
            if (result == 0)
                result = UniqueID?.CompareTo(other.UniqueID) ?? -1;
            if (result == 0)
                result = Name?.CompareTo(other.Name) ?? -1;

            return result;
        }
    }

    /// <summary>
    /// Describes a row
    /// </summary>
    public class RowLabel: BaseLabel
    {
        /// <summary>
        /// In a multi-level report, whom are we under? or null for top-level
        /// </summary>
        public RowLabel Parent { get; set; }

        /// <summary>
        /// How many levels ABOVE regular data is this?
        /// </summary>
        /// <remarks>
        /// Regular, lowest data is Level 0. First heading above that is Level 1,
        /// next heading up is level 2, etc.
        /// </remarks>
        public int Level { get; set; }

        /// <summary>
        /// If set, this row collects values from peer-level rows under
        /// the same parent, according to the rule it specifies. This is
        /// a semicolon-separated series of category values, optionally
        /// with a '^' to start. The caret indicates we should collect
        /// from all peers EXCEPT those in the list.
        /// </summary>
        public string Collector { get; set; } = null;
    }

    /// <summary>
    /// Describes a column 
    /// </summary>
    public class ColumnLabel : BaseLabel
    {
        /// <summary>
        /// Whether this is a calculated column, using 'Custom' property
        /// </summary>
        /// <remarks>
        /// Otherwise it's a regular column with data in the table
        /// </remarks>
        public bool IsCalculated { get; set; } = false;

        /// <summary>
        /// Whether this should be rendered as a percentage
        /// </summary>
        /// <remarks>
        /// Otherwise it's a regular dollar figure
        /// </remarks>
        public bool DisplayAsPercent { get; set; } = false;

        /// <summary>
        /// True if this column should not be propagating values up to parent rows
        /// </summary>

        public bool LeafNodesOnly { get; set; } = false;

        /// <summary>
        /// Custom function which will calculate values for this column based
        /// on values in other columns
        /// </summary>
        public Func<Dictionary<string,decimal>, decimal> Custom { get; set; }
    }
}
