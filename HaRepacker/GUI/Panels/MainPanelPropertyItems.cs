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
        public void ChangeReadOnlyAttribute<T>(bool bSet, T obj, Expression<Func<T, bool>> boolPropertyReadOnlySelector, Expression<Func<T, object>> setPropertySelector)
        {
            // Get the property name for the setter
            string setPropertyName = GetPropertyName(setPropertySelector);

            // Get the property descriptor
            PropertyDescriptor descriptor = TypeDescriptor.GetProperties(typeof(T))[setPropertyName];

            // Handle ReadOnlyAttribute
            var readOnlyAttribute = descriptor.Attributes[typeof(ReadOnlyAttribute)] as ReadOnlyAttribute;
            if (readOnlyAttribute != null)
            {
                // Create a new ReadOnlyAttribute with the desired value
                var newReadOnlyAttribute = new ReadOnlyAttribute(bSet);

                // Replace the old attribute with the new one
                ReplaceAttribute(descriptor, newReadOnlyAttribute);
            }

            // Handle BrowsableAttribute
            var browsableAttribute = descriptor.Attributes[typeof(BrowsableAttribute)] as BrowsableAttribute;
            if (browsableAttribute != null)
            {
                // Create a new BrowsableAttribute with the inverse of bSet
                var newBrowsableAttribute = new BrowsableAttribute(!bSet);

                // Replace the old attribute with the new one
                ReplaceAttribute(descriptor, newBrowsableAttribute);
            }

            // Get the property info for the boolean property
            PropertyInfo propInfo = ((MemberExpression)boolPropertyReadOnlySelector.Body).Member as PropertyInfo;

            // Update the boolean property
            propInfo.SetValue(obj, bSet);
        }

        private void ReplaceAttribute(PropertyDescriptor descriptor, Attribute newAttribute)
        {
            var attributes = new AttributeCollection(
                descriptor.Attributes.Cast<Attribute>()
                                     .Where(a => a.GetType() != newAttribute.GetType())
                                     .Append(newAttribute)
                                     .ToArray());

            var field = typeof(PropertyDescriptor).GetField("attributeArray", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(descriptor, attributes);
        }

        private string GetPropertyName<T>(Expression<Func<T, object>> expression)
        {
            if (expression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            else if (expression.Body is UnaryExpression unaryExpression &&
                     unaryExpression.Operand is MemberExpression operandExpression)
            {
                return operandExpression.Member.Name;
            }
            throw new ArgumentException("Invalid expression", nameof(expression));
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
