using Element34;
using static Element34.Utilities;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SeleniumTest
{
    internal static class GridSearchAccessories
    {
        private static readonly int _defaultTimeSpan = 1;
        private static Dictionary<string, By> Locators;

        public enum controlType
        {
            checkBox,
            anchor
        }

        public enum gridType
        {
            gyupo9,
            mdbootstrap
        }

        public enum inputType
        {
            name,
            id,
            status,
            title,
            location,
            selectChkBox,
            none
        }

        #region [Public Functions]
        ///<summary>
        ///Grid Search:
        ///   1) Find and select a specific row in a table of search results given search criteria.
        ///   2) This method will automatically advance through paginated results until the end is reached.
        ///</summary>
        ///<param name="locGridContainer">Selenium locator containing the grid</param>
        ///<param name="busyIndicator">Selenium locator of the busy indicator</param>
        ///<param name="criteria">Criteria to find in a table row</param>
        ///<param name="blnAllTrue">all criteria must match if true, any one of criteria can match if false</param>
        public static bool GridSearch(this IWebDriver driver, Dictionary<string,By> Locators, gridType grid, inputType howToSelectRow, List<string> criteria, bool blnAllTrue)
        {
            int iRowFound = 0;
            bool blnKeepSearching = true;
            bool blnNextDisabled, blnPrevDisabled;
            IWebElement btnNext, btnPrevious;
            IWebElement gridContainer;

            //find row
            while (blnKeepSearching)
            {
                // Wait for busy indicator
                driver.PauseOnBusyIndicator(Locators["busySpinnerLocator"], TimeSpan.FromSeconds(_defaultTimeSpan));
                gridContainer = driver.FindElement(Locators["gridContainerLocator"]);

                // No gridContainer; bail!
                if (gridContainer == null)
                    break;

                // Scroll to gridContainer
                driver.ScrollToElement(gridContainer);
                driver.wait_A_Moment(timeDelay / 2);

                // Find table within gridContainer
                var tableRows = gridContainer.FindElements(Locators["tableRowsLocator"]);

                // No results; bail!
                foreach (var row in tableRows)
                {
                    if (row.Text.ToLower().Contains("no records"))
                        return false;
                }

                // Find Next and Previous buttons
                try { btnNext = gridContainer.FindElement(Locators["nextButtonLocator"]); } catch { btnNext = null; }
                try { btnPrevious = gridContainer.FindElement(Locators["previousButtonLocator"]); } catch { btnPrevious = null; }

                // Ascertain state of Next and Previous buttons
                blnNextDisabled = (btnNext == null) ? true : Convert.ToBoolean(btnNext.GetAttribute("disabled"));
                blnPrevDisabled = (btnPrevious == null) ? true : Convert.ToBoolean(btnPrevious.GetAttribute("disabled"));

                // Page Navigation
                if (blnNextDisabled && blnPrevDisabled)  //one page
                {
                    iRowFound = findRow(tableRows, criteria, blnAllTrue);
                    if (iRowFound > 0)
                    {
                        rowSelection(tableRows, grid, howToSelectRow, iRowFound);
                    }

                    blnKeepSearching = false;
                }
                else if (blnPrevDisabled) //first of multi page
                {
                    iRowFound = findRow(tableRows, criteria, blnAllTrue);
                    if (iRowFound > 0)
                    {
                        rowSelection(tableRows, grid, howToSelectRow, iRowFound);
                        break;
                    }

                    if (!blnNextDisabled)
                        btnNext.Click();
                }
                else if (blnNextDisabled) // last page (end of search)
                {
                    iRowFound = findRow(tableRows, criteria, blnAllTrue);
                    if (iRowFound > 0)
                    {
                        rowSelection(tableRows, grid, howToSelectRow, iRowFound);
                    }

                    blnKeepSearching = false;
                }
                else //next pages
                {
                    iRowFound = findRow(tableRows, criteria, blnAllTrue);
                    if (iRowFound > 0)
                    {
                        rowSelection(tableRows, grid, howToSelectRow, iRowFound);
                        break;
                    }

                    if (!blnNextDisabled)
                        btnNext.Click();
                }
            }

            return (iRowFound > 0);
        }
        #endregion

        #region [Private Functions]
        ///<summary>
        ///findRow:
        ///   1) Returns the index of the first row that matches given criteria (0 is returned if not found).
        ///   2) Subtract 1 to use in zero-based array.
        ///   3) Algorithm improved by u/vidaj from Reddit.
        ///</summary>
        ///<param name="tableRows">IEnumerable representation of HTML table</param>
        ///<param name="criteria">Criteria to find in a table row</param>
        ///<param name="blnAllTrue">all criteria must match if true, any one of criteria can match if false</param>
        ///<param name="blnExactMatch">text comparison method (Equals if true, Contains if false)</param>
        private static int findRow(IEnumerable<IWebElement> tableRows, List<string> criteria, bool blnAllTrue = true, bool blnExactMatch = false)
        {
            // Avoid doing a .Trim() on each criteria for each row and column.
            var normalizedCriteria = criteria.Where(c => !string.IsNullOrEmpty(c)).Select(c => c.Trim()).ToArray();
            if (normalizedCriteria.Length == 0)
            {
                throw new ArgumentException("no criteria", nameof(criteria));
            }

            for (int iRow = 0, rowLength = tableRows.Count(); iRow < rowLength; iRow++)
            {
                IWebElement row = tableRows.ElementAt(iRow);
                IEnumerable<IWebElement> rowCells = row.FindElements(By.TagName("td"));

                // This can cause a slowdown for tables with lots of columns where the criteria matches early columns.
                // If that's the case, one can create an array of strings with null-values and initialize each cell on
                // first read if cellContents[cellColumn] == null
                string[] cellContents = rowCells.Select(cell => DecodeAndTrim(cell.Text)).ToArray();

                bool isMatch = false;
                foreach (string criterion in normalizedCriteria)
                {
                    foreach (string cellContent in cellContents)
                    {
                        // string.Contains(string, StringComparison) is not available for .Net Framework.
                        // If you're using .Net Framework, substitute by "cellContent.IndexOf(criterion, StringComparison.OrdinalIgnoreCase) >= 0
                        isMatch = (blnExactMatch && string.Equals(criterion, cellContent, StringComparison.OrdinalIgnoreCase)) ||
                                               cellContent.IndexOf(criterion, StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isMatch)
                        {
                            if (!blnAllTrue) { return iRow + 1; }
                            break;
                        }
                    }

                    if (blnAllTrue && !isMatch)
                    {
                        break;
                    }
                }

                if (isMatch)
                {
                    return iRow + 1;
                }
            }

            return 0;
        }

        ///<summary>
        /// rowSelection:
        ///   1) Implementation of how to select a row based on the gridType.  
        ///   2) Each table implemented has its own column layout and various means on selecting a specific row (e.g., checkbox or anchor). 
        ///   3) This function allows which column and method to select the identified row.
        ///   4) All XPaths start with ".//" and are local to the individual cell.
        ///</summary>
        ///<param name="table">IEnumerable representation of selectable HTML table rows</param>
        ///<param name="grid">The gridType representation of current table</param>
        ///<param name="input">The input name (i.e. inputType) representation of current control (e.g. name, ID, status, category, etc.)</param>
        ///<param name="iRow">The integer of selected row</param>
        private static void rowSelection(IEnumerable<IWebElement> table, gridType grid, inputType input, int iRow)
        {
            IWebElement row = table.ElementAt(iRow - 1);
            switch (grid)
            {
                case gridType.gyupo9:
                    switch (input)
                    {
                        case inputType.name:
                            chooseThis(row, 0, By.XPath(".//a"), controlType.anchor);
                            break;

                        case inputType.none:
                            break;
                    }
                    break;

                case gridType.mdbootstrap:
                    switch (input)
                    {
                        case inputType.name:
                            chooseThis(row, 0, By.XPath(".//a"), controlType.anchor);
                            break;

                        case inputType.none:
                            break;
                    }
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// chooseThis:
        ///   1) Implementation of how to select a column based on the controlType. 
        /// </summary>
        /// <param name="row">IWebElement of the HTML table row.</param>
        /// <param name="iColumn">The integer of the selected column.</param>
        /// <param name="locator">The Selenium locator of the DOM control.</param>
        /// <param name="control">The controlType to specify method of selection.</param>
        private static void chooseThis(IWebElement row, int iColumn, By locator, controlType control)
        {
            var cells = row.FindElements(By.TagName("td"));
            switch (control)
            {
                case controlType.checkBox:
                    Check(cells[iColumn].FindElement(locator), "true");
                    break;

                case controlType.anchor:
                    cells[iColumn].FindElement(locator).Click();
                    break;
            }
        }

        #endregion
    }
}
