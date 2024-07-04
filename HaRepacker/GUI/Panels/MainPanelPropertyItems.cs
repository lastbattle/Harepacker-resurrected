using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.GUI.Panels {

    /// <summary>
    /// For property items with WZ value that can be edited.
    /// </summary>
    public class MainPanelPropertyItems : INotifyPropertyChanged {

        private const string CATEGORY_DISPLAY = "Property";

        private string _wzFileType = "";
        [ReadOnly(true)] // This makes the Name property read-only
        [Category(CATEGORY_DISPLAY)]
        [Description("The file type currently being selected")]
        [DisplayName("WZ file type")]
        public string WzFileType {
            get => _wzFileType;
            set {
                if (_wzFileType != value) {
                    _wzFileType = value;
                    OnPropertyChanged(nameof(WzFileType));
                }
            }
        }

        private string _wzFileName = "";
        [ReadOnly(false)] // This makes the Name property read-only
        [Category(CATEGORY_DISPLAY)]
        [Description("The name of the file currently being selected")]
        [DisplayName("WZ file name")]
        public string WzFileName {
            get => _wzFileName;
            set {
                if (_wzFileName != value) {
                    _wzFileName = value;
                    OnPropertyChanged(nameof(WzFileName));
                }
            }
        }

        private string _wzFileValue = "";
        [ReadOnly(false)]// This makes the Name property read-only
        [Category(CATEGORY_DISPLAY)]
        [Description("The value of the file currently being selected")]
        [DisplayName("WZ value")]
        public string WzFileValue {
            get => _wzFileValue;
            set {
                if (_wzFileValue != value) {
                    _wzFileValue = value;
                    OnPropertyChanged(nameof(WzFileValue));
                }
            }
        }

        public bool _bIsWzValueReadOnly = true;
        [ReadOnly(true)]// This makes the Name property read-only
        [Browsable(false)]
        public bool IsWzValueReadOnly {
            get => _bIsWzValueReadOnly;
            set {
                if (_bIsWzValueReadOnly != value) {
                    _bIsWzValueReadOnly = value;
                    OnPropertyChanged(nameof(IsWzValueReadOnly));
                }
            }
        }


        /// <summary>
        /// Updates the read-only attribute of a parameter
        /// </summary>
        /// <param name="bSetIsReadOnly"></param>
        /// <param name="paramName"></param>
        public void ChangeReadOnlyAttribute(bool bSet, string paramName) {
            // changing attribute does not update the UI state
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(this)[paramName];
            ReadOnlyAttribute attribute = (ReadOnlyAttribute)descriptor.Attributes[typeof(ReadOnlyAttribute)];

            FieldInfo isReadOnlyField = typeof(ReadOnlyAttribute).GetField("isReadOnly", BindingFlags.NonPublic | BindingFlags.Instance);
            System.Diagnostics.Debug.WriteLine("From: " +attribute.IsReadOnly);
            isReadOnlyField.SetValue(attribute, bSet);
            System.Diagnostics.Debug.WriteLine("To: " + attribute.IsReadOnly);

            BrowsableAttribute browsableAttribute = (BrowsableAttribute)descriptor.Attributes[typeof(BrowsableAttribute)];
            FieldInfo isBrowsableField = typeof(BrowsableAttribute).GetField("browsable", BindingFlags.NonPublic | BindingFlags.Instance);
            isBrowsableField.SetValue(browsableAttribute, !bSet);

            // this is a hack for now, to easily use data binding
            IsWzValueReadOnly = bSet;
        }

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// On property changed
        /// </summary>
        /// <param name="propertyName"></param>
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
