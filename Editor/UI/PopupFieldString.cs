namespace TCS.UiToolkitUtils.Editor.UI {
    [UxmlElement] public partial class PopupFieldString : PopupField<string> 
    {
        public new static readonly string ussClassName = "popup-field-string";
        public new static readonly string labelUssClassName = ussClassName + "__label";
        public new static readonly string inputUssClassName = ussClassName + "__input";

        #region Constructors
        public PopupFieldString() : base() { }

        public PopupFieldString(string label) : base(label) { }

        public PopupFieldString(
            List<string> choices,
            string defaultValue,
            Func<string, string> formatSelectedValueCallback = null,
            Func<string, string> formatListItemCallback = null)
            : base(choices, defaultValue, formatSelectedValueCallback, formatListItemCallback)
        {
            // The base constructor handles all the logic.
        }

        public PopupFieldString(
            string label,
            List<string> choices,
            string defaultValue,
            Func<string, string> formatSelectedValueCallback = null,
            Func<string, string> formatListItemCallback = null)
            : base(label, choices, defaultValue, formatSelectedValueCallback, formatListItemCallback)
        {
            // The base constructor handles all the logic.
        }

        public PopupFieldString(
            List<string> choices,
            int defaultIndex,
            Func<string, string> formatSelectedValueCallback = null,
            Func<string, string> formatListItemCallback = null)
            : base(choices, defaultIndex, formatSelectedValueCallback, formatListItemCallback)
        {
            // The base constructor handles all the logic.
        }

        public PopupFieldString(
            string label,
            List<string> choices,
            int defaultIndex,
            Func<string, string> formatSelectedValueCallback = null,
            Func<string, string> formatListItemCallback = null)
            : base(label, choices, defaultIndex, formatSelectedValueCallback, formatListItemCallback)
        {
            // The base constructor handles all the logic.
        }
        #endregion

        #region Initialization
        public void SetElement(
            string s,
            List<string> list,
            string defaultValue,
            Func<string, string> valueCallback = null,
            Func<string, string> itemCallback = null)
        {
            label = s;
            choices = list;
            value = defaultValue;
            formatSelectedValueCallback = valueCallback;
            formatListItemCallback = itemCallback;
        }
        public void SetElement(
            List<string> list,
            string defaultValue,
            Func<string, string> valueCallback = null,
            Func<string, string> itemCallback = null)
        {
            choices = list;
            value = defaultValue;
            formatSelectedValueCallback = valueCallback;
            formatListItemCallback = itemCallback;
        }
        public void SetElement(
            string s,
            List<string> list,
            int defaultIndex,
            Func<string, string> valueCallback = null,
            Func<string, string> itemCallback = null)
        {
            label = s;
            choices = list;
            index = defaultIndex;
            formatSelectedValueCallback = valueCallback;
            formatListItemCallback = itemCallback;
        }
        public void SetElement(
            List<string> list,
            int defaultIndex,
            Func<string, string> valueCallback = null,
            Func<string, string> itemCallback = null)
        {
            choices = list;
            index = defaultIndex;
            formatSelectedValueCallback = valueCallback;
            formatListItemCallback = itemCallback;
        }
        #endregion
    }
}