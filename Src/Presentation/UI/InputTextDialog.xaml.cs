using System.Windows;
using System.Windows.Controls; // 👈 IMPORTANTE

namespace EAABAddIn.Src.UI;

public partial class InputTextDialog : Window
{
    public string InputText { get; private set; }
    public string SelectedCity { get; private set; }

    public InputTextDialog()
    {
        InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        InputText = InputBox.Text;

        if (CityComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            SelectedCity = selectedItem.Content.ToString();
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
