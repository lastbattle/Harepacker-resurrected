/* Copyright (C) 2018 LastBattle
https://github.com/lastbattle/Harepacker-resurrected
*/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MapleLib.WzLib.WzStructure.Data;

namespace HaSharedLibrary.GUI
{
    public partial class FieldLimitPanel : UserControl
    {
        public event EventHandler<FieldLimitChangedEventArgs> FieldLimitChanged;

        private readonly ObservableCollection<FieldLimitOption> _options = new();
        private bool _initializing;
        private ulong _fieldLimit;

        public FieldLimitPanel()
        {
            InitializeComponent();
            fieldLimitList.ItemsSource = _options;
            Loaded += FieldLimitPanel_Loaded;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public ulong FieldLimit
        {
            get => _fieldLimit;
            set => _fieldLimit = value;
        }

        private void FieldLimitPanel_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateDefaultListView();
        }

        public void PopulateDefaultListView()
        {
            if (_options.Count != 0)
                return;

            _initializing = true;
            int index = 0;
            foreach (FieldLimitType limitType in Enum.GetValues(typeof(FieldLimitType)))
            {
                string fallback = limitType.ToString().Replace("_", " ");
                string label = SharedUiText.Get($"FieldLimit_{limitType}", fallback);
                _options.Add(new FieldLimitOption(index, $"{index} - {label}", Option_CheckedChanged));
                index++;
            }

            for (int i = index; i < index + 30; i++)
                _options.Add(new FieldLimitOption(i, $"{i} - {SharedUiText.Get("FieldLimit_Unknown")}", Option_CheckedChanged));

            _initializing = false;
        }

        public void UpdateFieldLimitCheckboxes(ulong propertyValue)
        {
            PopulateDefaultListView();
            _initializing = true;
            _fieldLimit = propertyValue;

            foreach (FieldLimitOption option in _options)
                option.IsChecked = option.BitIndex < 64 && FieldLimitTypeExtension.Check(option.BitIndex, (long)propertyValue);

            _initializing = false;
            RaiseFieldLimitChanged();
        }

        private void Option_CheckedChanged()
        {
            if (_initializing)
                return;

            ulong value = 0;
            foreach (FieldLimitOption option in _options)
            {
                if (option.IsChecked && option.BitIndex < 64)
                    value |= 1UL << option.BitIndex;
            }

            _fieldLimit = value;
            RaiseFieldLimitChanged();
        }

        private void RaiseFieldLimitChanged()
        {
            FieldLimitChanged?.Invoke(this, new FieldLimitChangedEventArgs(_fieldLimit));
        }

        private sealed class FieldLimitOption : INotifyPropertyChanged
        {
            private readonly Action _changed;
            private bool _isChecked;

            public FieldLimitOption(int bitIndex, string label, Action changed)
            {
                BitIndex = bitIndex;
                Label = label;
                _changed = changed;
            }

            public int BitIndex { get; }
            public string Label { get; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value)
                        return;

                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                    _changed();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
