using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace HaCreator.GUI.InstanceEditor
{
    internal static class InstanceEditorDialogSupport
    {
        public static int GetInt(TextBox input, int fallback = 0)
        {
            EnableIntegerInput(input);
            return int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        public static void SetInt(TextBox input, int value)
        {
            EnableIntegerInput(input);
            input.Text = value.ToString(CultureInfo.InvariantCulture);
        }

        public static void EnableIntegerInput(params TextBox[] inputs)
        {
            foreach (TextBox input in inputs)
            {
                input.InputScope = new InputScope();
                input.InputScope.Names.Add(new InputScopeName(InputScopeNameValue.Number));
            }
        }
    }
}
