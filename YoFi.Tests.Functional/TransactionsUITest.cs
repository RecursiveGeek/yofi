﻿using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YoFi.Tests.Functional
{
    /// <summary>
    /// Test the Transactions page
    /// </summary>
    [TestClass]
    public class TransactionsUITest : FunctionalUITest
    {
        public const int TotalItemCount = 889;
        public const string MainPageName = "Transactions";

        [TestInitialize]
        public new async Task SetUp()
        {
            base.SetUp();

            // When: Navigating to the main page for this section
            await WhenNavigatingToPage(MainPageName);

            // Then: We are on the main page for this section
            await Page.ThenIsOnPageAsync(MainPageName);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            //
            // Delete all test items
            //

            // Given: Clicking "Transactions" on the navbar
            await Page.ClickAsync("text=Transactions");

            // And: Showing hidden items
            await Page.ClickAsync("#dropdownMenuButtonAction");
            await Page.ClickAsync("text=Show Hidden");

            // If: totalitems > expected TotalItemCount
            var totalitems = await Page.GetTotalItemsAsync();
            if (totalitems > TotalItemCount)
            {
                // When: Asking server to clear this test data
                var api = new ApiKeyTest();
                api.SetUp(TestContext);
                await api.ClearTestData("trx,payee");
            }

            // And: Releaging the page
            await Page.ReloadAsync();

            // Then: Total items are back to normal
            Assert.AreEqual(TotalItemCount, await Page.GetTotalItemsAsync());
        }


        [TestMethod]
        public async Task ClickTransactions()
        {
            // Given: We are logged in and on the transactions page

            // Then: All expected items are here
            Assert.AreEqual(TotalItemCount, await Page.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task IndexQAny12()
        {
            // Given: We are logged in and on the transactions page

            // When: Searching for "Farquat"
            await Page.SearchFor("Farquat");

            // Then: Exactly 12 transactions are found, because we know this about our source data
            Assert.AreEqual(12, await Page.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task IndexQAnyCategory()
        {
            // Given: We are logged in and on the transactions page

            // When: Searching for "Food:Away:Coffee"
            await Page.SearchFor("Food:Away:Coffee");

            // Then: Exactly 156 transactions are found, because we know this about our source data
            Assert.AreEqual(156, await Page.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task IndexClear()
        {
            // Given: We are logged in and on the transactions page, with an active search
            await IndexQAny12();

            // When: Pressing clear
            await Page.ClickAsync("data-test-id=btn-clear");

            // Then: Back to all the items
            Assert.AreEqual(TotalItemCount, await Page.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task DownloadAll()
        {
            // Given: We are logged in and on the transactions page

            // When: Downloading transactions
            await Page.ClickAsync("#dropdownMenuButtonAction");
            await Page.ClickAsync("text=Export");

            var download1 = await Page.RunAndWaitForDownloadAsync(async () =>
            {
                await Page.ClickAsync("[data-test-id=\"btn-dl-save\"]");
            });

            // Then: A spreadsheet containing 889 Transactions was downloaded
            await download1.ThenIsSpreadsheetContainingAsync<IdOnly>(name: "Transaction", count: 889);
        }

        [TestMethod]
        public async Task DownloadQ12()
        {
            // Given: We are logged in and on the transactions page

            // When: Searching for "Farquat"
            var searchword = "Farquat";
            await Page.SearchFor(searchword);

            // And: Downloading transactions
            await Page.ClickAsync("#dropdownMenuButtonAction");
            await Page.ClickAsync("text=Export");

            var download1 = await Page.RunAndWaitForDownloadAsync(async () =>
            {
                await Page.ClickAsync("[data-test-id=\"btn-dl-save\"]");
            });

            // Then: A spreadsheet containing 12 Transactions were downloaded
            var items = await download1.ThenIsSpreadsheetContainingAsync<IdAndPayee>(name: "Transaction", count: 12);

            // And: All items match the search criteria
            Assert.IsTrue(items.All(x => x.Payee.Contains(searchword)));
        }

        private class IdAndPayee
        {
            public int ID { get; set; }
            public string Payee { get; set; }
        }

        /* Not sure how to assert this
        [TestMethod]
        public async Task HelpPopup()
        {
            // Given: We are logged in and on the transactions page
            await ClickTransactions();

            // When: Hitting the help button
            await Page.ClickAsync("data-test-id=btn-help");

            // Then: The help text is findable
            var title = await Page.QuerySelectorAsync("id=searchHelpModal");
            Assert.IsNotNull(title);
            Assert.IsTrue(await title.IsEnabledAsync());
        }

        [TestMethod]
        public async Task HelpPopupClose()
        {
            // Given: We have the help popup active
            await HelpPopup();

            // When: Hitting the close button
            await Page.ClickAsync("text=Close");

            // Then: The help text is not findable
            var title = await Page.QuerySelectorAsync("data-test-id=help-title");
            Assert.IsFalse(await title.IsEnabledAsync());
        }
        */

        private async Task WhenCreatingTransaction(IPage page, Dictionary<string, string> values)
        {
            await page.ClickAsync("#dropdownMenuButtonAction");
            await page.ClickAsync("text=Create New");
            await page.FillFormAsync(values);
            await page.SaveScreenshotToAsync(TestContext);
            await page.ClickAsync("input:has-text(\"Create\")");
        }

        [TestMethod]
        public async Task Create()
        {
            // Given: We are logged in and on the transactions page

            // When: Creating a new item
            var originalitems = await Page.GetTotalItemsAsync();
            await WhenCreatingTransaction(Page,new Dictionary<string, string>()
            {
                { "Category", NextCategory },
                { "Payee", NextName },
                { "Timestamp", "2021-12-31" },
                { "Amount", "100" },
                { "Memo", testmarker },
            });

            // Then: We are on the main page for this section
            await Page.ThenIsOnPageAsync(MainPageName);

            // And: There is one more item
            var itemsnow = await Page.GetTotalItemsAsync();
            Assert.AreEqual(originalitems + 1,itemsnow);

            await Page.SaveScreenshotToAsync(TestContext);
        }

        [DataRow(1)]
        [DataRow(5)]
        [DataRow(10)]
        [DataTestMethod]
        public async Task Read(int count)
        {
            // Given: One item created
            for ( int i=count ; i > 0 ; i-- )
                await Create();

            // When: Searching for the new item
            await Page.SearchFor(testmarker);
            await Page.SaveScreenshotToAsync(TestContext);

            // Then: It's found
            Assert.AreEqual(count, await Page.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task UpdateModal()
        {
            // Given: One item created
            // And: It's the one item in search results
            await Read(1);

            // When: Editing it
            var newcategory = NextCategory;
            var newpayee = NextName;

            await Page.ClickAsync("[aria-label=\"Edit\"]");
            await Page.FillFormAsync(new Dictionary<string, string>()
            {
                { "Category", newcategory },
                { "Payee", newpayee },
            });
            await Page.SaveScreenshotToAsync(TestContext);
            await Page.ClickAsync("text=Save");

            // Then: We are on the main page for this section
            await Page.ThenIsOnPageAsync(MainPageName);

            // And: Searching for the new item...
            await Page.SearchFor(newcategory);
            await Page.SaveScreenshotToAsync(TestContext);

            // Then: It's found
            Assert.AreEqual(1, await Page.GetTotalItemsAsync());

            // TODO: Also check that the amount is correct
        }

        [TestMethod]
        public async Task UpdatePage()
        {
            // Given: One item created
            // And: It's the one item in search results
            await Read(1);

            // When: Editing it
            var newcategory = NextCategory;
            var newpayee = NextName;

            await Page.ClickAsync("[aria-label=\"Edit\"]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.WaitForSelectorAsync("input[name=\"Category\"]");
                await Page.SaveScreenshotToAsync(TestContext);
                await Page.ClickAsync("text=More");
            });

            await NextPage.FillFormAsync(new Dictionary<string, string>()
            {
                { "Category", newcategory },
                { "Payee", newpayee },
                { "Amount", "200" },
            });

            await NextPage.SaveScreenshotToAsync(TestContext);
            await NextPage.ClickAsync("text=Save");

            // Then: We are on the main page for this section
            await NextPage.ThenIsOnPageAsync(MainPageName);

            // And: Searching for the new item...
            await Page.SearchFor(newcategory);
            await Page.SaveScreenshotToAsync(TestContext);

            // Then: It's found
            Assert.AreEqual(1, await Page.GetTotalItemsAsync());

            // TODO: Also check that the amount is correct
        }

        [TestMethod]
        public async Task Delete()
        {
            // Given: One item created
            // And: It's the one item in search results
            await Read(1);

            // When: Deleting first item in list

            await Page.ClickAsync("[aria-label=\"Edit\"]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.ClickAsync("text=More");
                await Page.SaveScreenshotToAsync(TestContext);
            });
            await NextPage.ClickAsync("text=Delete");

            // Then: We land at the delete page
            await NextPage.ThenIsOnPageAsync("Delete Transaction");
            await NextPage.SaveScreenshotToAsync(TestContext);

            // When: Clicking the Delete button to execute the delete
            await NextPage.ClickAsync("input:has-text(\"Delete\")");

            // Then: We land at the transactions index page
            await NextPage.ThenIsOnPageAsync(MainPageName);
            await NextPage.SaveScreenshotToAsync(TestContext);

            // And: Total number of items is back to the standard amount
            Assert.AreEqual(TotalItemCount, await NextPage.GetTotalItemsAsync());
        }

        [TestMethod]
        public async Task CreateReceipt()
        {
            // Given: One item created
            // And: It's the one item in search results
            await Read(1);

            // And: Editing it
            await Page.ClickAsync("[aria-label=Edit]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.WaitForSelectorAsync("input[name=Category]");
                await Page.SaveScreenshotToAsync(TestContext);
                await Page.ClickAsync("text=More");
            });

            // When: Uploading a receipt
            await NextPage.ClickAsync("[aria-label=UploadReceipt]");
            await NextPage.SetInputFilesAsync("[aria-label=UploadReceipt]", new[] { "SampleData/budget-white-60x.png" });
            await NextPage.SaveScreenshotToAsync(TestContext);
            await NextPage.ClickAsync("data-test-id=btn-create-receipt");

            // Then: Delete Receipt button is visible
            var button = await NextPage.QuerySelectorAsync("data-test-id=btn-delete-receipt");
            await NextPage.SaveScreenshotToAsync(TestContext);

            Assert.IsNotNull(button);

            // TODO: Clean up the storage, else this is going to leave a lot of extra crap lying around there
        }

        [TestMethod]
        public async Task DownloadReceiptFromIndex()
        {
            // Given: A transaction created and a receipte uploaded
            await CreateReceipt();

            // And: Back on the main page
            await Page.ClickAsync("text=Transactions");

            // And: Searching for the new item
            await Page.SearchFor(testmarker);

            // When: Clicking on the get-receipt icon
            var download1 = await Page.RunAndWaitForDownloadAsync(async () =>
            {
                await Page.ClickAsync("[aria-label=\"Get Receipt\"]");
            });

            // Then: Image loads successfully
            var image = await download1.DownloadImageAsync();

            Assert.AreEqual(100, image.Width);
            Assert.AreEqual(100, image.Height);
        }

        [TestMethod]
        public async Task DownloadReceiptFromEditPage()
        {
            // Given: A transaction created and a receipte uploaded
            await CreateReceipt();

            // And: Back on the main page
            await Page.ClickAsync("text=Transactions");

            // And: Searching for the new item
            await Page.SearchFor(testmarker);

            // And: On the edit page
            await Page.ClickAsync("[aria-label=\"Edit\"]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.WaitForSelectorAsync("input[name=\"Category\"]");
                await Page.SaveScreenshotToAsync(TestContext);
                await Page.ClickAsync("text=More");
            });

            // When: Clicking on the get-receipt button
            var download1 = await NextPage.RunAndWaitForDownloadAsync(async () =>
            {
                await NextPage.ClickAsync("[data-test-id=btn-get-receipt]");
            });

            // Then: Image loads successfully
            var image = await download1.DownloadImageAsync();

            Assert.AreEqual(100, image.Width);
            Assert.AreEqual(100, image.Height);
        }

        [TestMethod]
        public async Task DeleteReceiptFromEditPage()
        {
            // Given: A transaction created and a receipte uploaded
            await CreateReceipt();

            // And: Back on the main page
            await Page.ClickAsync("text=Transactions");

            // And: Searching for the new item
            await Page.SearchFor(testmarker);

            // And: On the edit page
            await Page.ClickAsync("[aria-label=\"Edit\"]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.WaitForSelectorAsync("input[name=\"Category\"]");
                await Page.SaveScreenshotToAsync(TestContext);
                await Page.ClickAsync("text=More");
            });

            // When: Clicking on the delete-receipt button
            await NextPage.ClickAsync("[data-test-id=btn-delete-receipt]");

            // Then: The upload receipt button is visible again
            var button = await NextPage.QuerySelectorAsync("data-test-id=btn-create-receipt");
            await NextPage.SaveScreenshotToAsync(TestContext);

            Assert.IsNotNull(button);
        }

        [TestMethod]
        public async Task CreateSplit()
        {
            // Given: One item created
            // And: It's the one item in search results
            await Read(1);

            // And: Editing it
            await Page.ClickAsync("[aria-label=Edit]");
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.WaitForSelectorAsync("input[name=Category]");
                await Page.SaveScreenshotToAsync(TestContext);
                await Page.ClickAsync("text=More");
            });

            // When: Adding a single off-balance split

            await NextPage.ClickAsync("data-test-id=btn-add-split");
            await NextPage.FillFormAsync(new Dictionary<string, string>()
            {
                { "Amount", "25" },
            });
            await NextPage.SaveScreenshotToAsync(TestContext);

            await NextPage.ClickAsync("text=Save");

            var add = await NextPage.QuerySelectorAsync("data-test-id=btn-add-split");
            await add.ScrollIntoViewIfNeededAsync();
            await NextPage.SaveScreenshotToAsync(TestContext);

            // Then: The fix split button is visible
            var fix = await NextPage.QuerySelectorAsync("data-test-id=btn-add-split");
            Assert.IsNotNull(fix);
        }

        [TestMethod]
        public async Task BalanceSplits()
        {
            // Given: One item created with an imbalanced split
            await CreateSplit();

            // And: Starting at the top
            await Page.ClickAsync("text=Transactions");

            // And: Searching for this item
            await Page.SearchFor(testmarker);
            await Page.SaveScreenshotToAsync(TestContext);

            // And: On the edit page for it
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.ClickAsync("[data-test-id=edit-splits] i");
            });

            var fix = await NextPage.QuerySelectorAsync("data-test-id=btn-fix-split");
            await fix.ScrollIntoViewIfNeededAsync();
            await NextPage.SaveScreenshotToAsync(TestContext);

            // When: Clicking the "fix split" button
            await fix.ClickAsync();

            // Adding: Adding the remaining split (but not changing the amount)

            await NextPage.FillFormAsync(new Dictionary<string, string>()
            {
                { "Category", NextCategory },
                { "Memo", testmarker },
            });
            await NextPage.SaveScreenshotToAsync(TestContext);

            await NextPage.ClickAsync("text=Save");

            var add = await NextPage.QuerySelectorAsync("data-test-id=btn-add-split");
            await add.ScrollIntoViewIfNeededAsync();
            await NextPage.SaveScreenshotToAsync(TestContext);

            // Then: The fix split button is NOT visible
            fix = await NextPage.QuerySelectorAsync("data-test-id=btn-fix-split");
            Assert.IsNull(fix);
        }


        /// <summary>
        /// User Story 802: [User Can] Specify loan information in payee matching rules, then see that principal and interest are automatically divided upon transaction import
        /// </summary>
        [TestMethod]
        public async Task LoanPayeeMatchImport()
        {
            /*
            Given: A payee with {loan details json} in the category and {name} in the name
            When: Importing an OFX file containing a transaction with payee {name}
            Then: The transaction is imported as a split, with correct categories and amounts for the {loan details json} 
            */

            // Given: A payee with {loan details} in the category and {name} in the name

            // See TransactionRepositoryTest.CalculateLoanSplits for where we are getting this data from. This is
            // payment #53, made on 5/1/2004 for this loan.

            var rule = "__TEST__ Principal [Loan] { \"interest\": \"Interest __TEST__\", \"amount\": 200000, \"rate\": 6, \"term\": 180, \"origination\": \"1/1/2000\" } ";
            var payee = "AA__TEST__ Loan Payment";
            var principal = -891.34m;
            var interest = -796.37m;

            await GivenPayeeInDatabase(name: payee, category: rule);

            // When: Importing an OFX file containing a transaction with payee {name}

            await WhenNavigatingToPage("Import");
            await Page.ClickAsync("[aria-label=\"Upload\"]");
            await Page.SetInputFilesAsync("[aria-label=\"Upload\"]", new[] { "SampleData\\User-Story-802.ofx" });
            await Page.SaveScreenshotToAsync(TestContext, "SetInputFiles");
            await Page.ClickAsync("text=Upload");
            await Page.ClickAsync("button:has-text(\"Import\")");

            await Page.ThenIsOnPageAsync("Transactions");

            await Page.SearchFor($"p={payee}");

            await Page.SaveScreenshotToAsync(TestContext);
            Assert.AreEqual(1, await Page.GetTotalItemsAsync());

            // Then: The transaction is imported as a split
            var element = await Page.QuerySelectorAsync(".display-category");
            var text = await element.TextContentAsync();
            Assert.AreEqual("SPLIT", text.Trim());

            // And: The splits match the categories and amounts as expected from the {loan details} 
            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.ClickAsync("[data-test-id=\"edit-splits\"]");
            });

            var line1_e = await NextPage.QuerySelectorAsync($"data-test-id=line-1 >> data-test-id=split-amount");
            var line1 = await line1_e.TextContentAsync();
            var line2_e = await NextPage.QuerySelectorAsync($"data-test-id=line-2 >> data-test-id=split-amount");
            var line2 = await line2_e.TextContentAsync();
            await line2_e.ScrollIntoViewIfNeededAsync();
            await NextPage.SaveScreenshotToAsync(TestContext);

            Assert.AreEqual(interest, decimal.Parse(line2.Trim()));
            Assert.AreEqual(principal, decimal.Parse(line1.Trim()));
        }

        /// <summary>
        /// User Story 802: [User Can] Specify loan information in payee matching rules, then see that principal and interest are automatically divided 
        /// (When applying payee from transations index)
        /// </summary>
        [TestMethod]
        public async Task LoanPayeeMatchApplyPayee()
        {
            // Given: A transaction which represents a loan payment
            var payee = "AA__TEST__ Loan Payment";
            var principal = -891.34m;
            var interest = -796.37m;

            await WhenCreatingTransaction(Page, new Dictionary<string, string>()
            {
                { "Payee", payee },
                { "Timestamp", "2004-05-01" },
                { "Amount", (principal+interest).ToString() },
                { "Memo", testmarker },
            });

            // And: A payee with {loan details} in the category and {name} in the name

            // See TransactionRepositoryTest.CalculateLoanSplits for where we are getting this data from. This is
            // payment #53, made on 5/1/2004 for this loan.

            var rule = "__TEST__ Principal [Loan] { \"interest\": \"Interest __TEST__\", \"amount\": 200000, \"rate\": 6, \"term\": 180, \"origination\": \"1/1/2000\" } ";

            await GivenPayeeInDatabase(name: payee, category: rule);

            // When: Searching for this item
            await WhenNavigatingToPage("Transactions");
            await Page.SearchFor($"p={payee}");
            await Page.SaveScreenshotToAsync(TestContext);

            // And: Clicking 'Apply Payee' on the first line
            var button = await Page.QuerySelectorAsync($"data-test-id=line-1 >> [aria-label=\"Apply Payee\"]");
            await button.ClickAsync();
            await Page.SaveScreenshotToAsync(TestContext);

            // And: Editing the transaction
            await Page.ReloadAsync();
            await Page.SearchFor($"p={payee}");
            await Page.SaveScreenshotToAsync(TestContext);

            var NextPage = await Page.RunAndWaitForPopupAsync(async () =>
            {
                await Page.ClickAsync("[data-test-id=edit-splits]");
            });

            // Then: Correct amounts are shown in splits
            var line1_e = await NextPage.QuerySelectorAsync($"data-test-id=line-1 >> data-test-id=split-amount");
            var line1 = await line1_e.TextContentAsync();
            var line2_e = await NextPage.QuerySelectorAsync($"data-test-id=line-2 >> data-test-id=split-amount");
            var line2 = await line2_e.TextContentAsync();
            await line2_e.ScrollIntoViewIfNeededAsync();
            await NextPage.SaveScreenshotToAsync(TestContext);

            Assert.AreEqual(interest, decimal.Parse(line2.Trim()));
            Assert.AreEqual(principal, decimal.Parse(line1.Trim()));
        }

        /// <summary>
        /// [User Can] Choose from a prompt of recently-used transactions when typing just a few letters in any category input
        /// </summary>
        [TestMethod]
        public async Task AutoComplete()
        {
            // Given: We are logged in and on the transactions page
            // (Handled in SetUp)

            // When: Typing a few letters into the search box
            await Page.TypeAsync("[data-test-id=\"q\"]", "inc");

            // Then: There is a drop-down list of the expected length showing options
            var element = await Page.WaitForSelectorAsync("div.bootstrap-autocomplete");
            await Page.SaveScreenshotToAsync(TestContext);

            var children = await element.QuerySelectorAllAsync("a");
            Assert.AreEqual(5, children.Count);
        }

        /// <summary>
        /// [User Can] Edit a transaction's category quickly without having to load
        /// a new page
        /// </summary>
        public void EditModalNotMatching()
        {
            // Given: One item added, in search results

            // When: Editing to change the category

            // And: Editing it again

            // Then: The new category is there
        }

        /// <summary>
        /// [User Can] See that a transaction's category is automatically assigned if it
        /// doesn't already have one, when editing a transaction quickly without having to
        /// load a new page.
        /// </summary>
        public void EditModalMatching()
        {
            // Given: One item added, in search results

            // When: Editing to clear the category

            // And: Editing it again

            // Then: A matched category is there automatically
        }

    }
}
