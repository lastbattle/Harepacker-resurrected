using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Windows.Threading;

namespace HaCreator.GUI.InstanceEditor
{
    /// <summary>
    /// Filters the ListBox selection according to the user's input in the TextBox
    /// It cancels the prior task when the user types a new character before sorting and searching is completed.
    /// </summary>
    public class LoadSearchHelper
    {
        private string _previousSearchText = string.Empty;
        private CancellationTokenSource _existingSearchTaskToken = null;
        private bool _bItemsLoaded = false;
        private readonly List<string> _itemNames;
        private readonly ListBox _listBox;
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Constructor for the search helper
        /// </summary>
        /// <param name="listBox"></param>
        /// <param name="itemNames"></param>
        public LoadSearchHelper(ListBox listBox, List<string> itemNames)
        {
            _listBox = listBox;
            _itemNames = itemNames;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _bItemsLoaded = true;
        }

        /// <summary>
        /// On text change by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void TextChanged(object sender, EventArgs e)
        {
            TextBox searchBox = (TextBox)sender;
            string searchText = searchBox.Text.ToLower();
            if (_previousSearchText == searchText)
                return;
            _previousSearchText = searchText;

            // Cancel the existing task before starting a new one
            if (_existingSearchTaskToken != null)
            {
                _existingSearchTaskToken.Cancel();
                _existingSearchTaskToken = null;
            }

            await SearchItemInternal(searchText);
        }

        private async Task SearchItemInternal(string searchText)
        {
            if (!_bItemsLoaded)
                return;

            _listBox.Items.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                var filteredItems = _itemNames.Cast<object>().ToArray();
                _listBox.Items.AddRange(filteredItems);
                OnListBoxSelectionChanged();
            }
            else
            {
                _existingSearchTaskToken = new CancellationTokenSource();
                var cancellationToken = _existingSearchTaskToken.Token;

                try
                {
                    await Task.Delay(500, cancellationToken); // Delay for 500ms or until cancelled

                    List<string> itemsFiltered = _itemNames
                        .Where(item => item.ToLower().Contains(searchText))
                        .ToList();

                    await _dispatcher.InvokeAsync(() =>
                    {
                        foreach (string item in itemsFiltered)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;
                            _listBox.Items.Add(item);
                        }
                        if (_listBox.Items.Count > 0)
                        {
                            _listBox.SelectedIndex = 0;
                        }
                    }, DispatcherPriority.Normal, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Task was cancelled, do nothing
                }
                finally
                {
                    _existingSearchTaskToken = null;
                }
            }
        }

        private void OnListBoxSelectionChanged()
        {
            // Implement the logic for listBox_itemList_SelectedIndexChanged here
            // or provide a way to set an external event handler
        }
    }
}