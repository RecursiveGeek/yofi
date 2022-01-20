﻿using Common.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace YoFi.Core.SampleGen
{
    public class SampleDataLoader : ISampleDataLoader
    {
        private readonly IDataContext _context;
        private readonly IClock _clock;
        private readonly string _directory;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Application data context</param>
        /// <param name="directory">Location of sample data file</param>
        public SampleDataLoader(IDataContext context, IClock clock, string directory)
        {
            _context = context;
            _clock = clock;
            _directory = directory;
        }

        public Task<Stream> DownloadSampleDataAsync(string id)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<ISampleDataDownloadOffering>> GetDownloadOfferingsAsync()
        {
            using var stream = Common.NET.Data.SampleData.Open("SampleDataDownloadOfferings.json");

            var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            var result = await JsonSerializer.DeserializeAsync<List<DownloadOffering>>(stream, options);

            return result;
        }

        public Task<IEnumerable<ISampleDataSeedOffering>> GetSeedOfferingsAsync()
        {
            throw new NotImplementedException();
        }

        public Task SeedAsync(string id)
        {
            throw new NotImplementedException();
        }
    }

    internal class DownloadOffering : ISampleDataDownloadOffering
    {
        public string ID { get; set; }

        public string FileType { get; set; } = "xlsx";

        public IEnumerable<string> Description { get; set; }

        public SampleDataDownloadOfferingKind Kind { get; set; }
    }
}
