using HaRepacker.Converter;
using HaRepacker.GUI.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
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

        public MainPanelPropertyItems() {
            _XYVector.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(XYVector));
        }

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
        private bool _bIsWzValueReadOnly = true;
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

        private NotifyPointF _XYVector = new NotifyPointF(0,0);
        [Category(CATEGORY_DISPLAY)]
        [Description("The X,Y values.")]
        [DisplayName("X Y")]
        [TypeConverter(typeof(PointFConverter))]
        [Editor(typeof(PointFEditor), typeof(PointFEditor))]
        [ReadOnly(true)]// This makes the Name property read-only
        public NotifyPointF XYVector {
            get => _XYVector;
            set {
                if (_XYVector != value) {
                    if (_XYVector != null) {
                        _XYVector.PropertyChanged -= (sender, args) => OnPropertyChanged(nameof(XYVector));
                    }
                    _XYVector = value;
                    if (_XYVector != null) {
                        _XYVector.PropertyChanged += (sender, args) => OnPropertyChanged(nameof(XYVector));
                    }
                    OnPropertyChanged(nameof(XYVector));
                }
            }
        }
        private bool _bIsXYPanelReadOnly = true;
        [ReadOnly(true)]// This makes the Name property read-only
        [Browsable(false)]
        public bool IsXYPanelReadOnly {
            get => _bIsXYPanelReadOnly;
            set {
                if (_bIsXYPanelReadOnly != value) {
                    _bIsXYPanelReadOnly = value;
                    OnPropertyChanged(nameof(IsXYPanelReadOnly));
                }
            }
        }


        /// <summary>
        /// Updates the read-only attribute of a parameter and sets a boolean property
        /// </summary>
        /// <param name="bSet">True to set as read-only, false otherwise</param>
        /// <param name="paramName">Name of the parameter to update</param>
        /// <typeparam name="T">The type containing the property</typeparam>
        /// <param name="obj">The object containing the property</param>
        /// <param name="propertySelector">A lambda expression to select the property</param>
        public void ChangeReadOnlyAttribute<T>(bool bSet, T obj, Expression<Func<T, bool>> boolPropertyReadOnlySelector, Expression<Func<T, object>> setPropertySelector) {
            // changing attribute does not update the UI state
            string setPropertyName = GetPropertyName(setPropertySelector);
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(T))[setPropertyName];
            ReadOnlyAttribute attribute = (ReadOnlyAttribute)descriptor.Attributes[typeof(ReadOnlyAttribute)];

            FieldInfo isReadOnlyField = typeof(ReadOnlyAttribute).GetField("isReadOnly", BindingFlags.NonPublic | BindingFlags.Instance);
            //System.Diagnostics.Debug.WriteLine("From: " +attribute.IsReadOnly);
            isReadOnlyField.SetValue(attribute, bSet);
            //System.Diagnostics.Debug.WriteLine("To: " + attribute.IsReadOnly);

            BrowsableAttribute browsableAttribute = (BrowsableAttribute)descriptor.Attributes[typeof(BrowsableAttribute)];
            FieldInfo isBrowsableField = typeof(BrowsableAttribute).GetField("browsable", BindingFlags.NonPublic | BindingFlags.Instance);
            isBrowsableField.SetValue(browsableAttribute, !bSet);

            // Get the property
            PropertyInfo propInfo = ((MemberExpression)boolPropertyReadOnlySelector.Body).Member as PropertyInfo;

            // Update the boolean property
            propInfo.SetValue(obj, bSet);

            // Note: OnPropertyChanged is called within the property setter, so we don't need to call it here
        }

        private string GetPropertyName<T>(Expression<Func<T, object>> propertySelector) {
            if (propertySelector.Body is MemberExpression memberExpression) {
                return memberExpression.Member.Name;
            }
            else if (propertySelector.Body is UnaryExpression unaryExpression) {
                return ((MemberExpression)unaryExpression.Operand).Member.Name;
            }

            throw new ArgumentException("Invalid property selector", nameof(propertySelector));
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
