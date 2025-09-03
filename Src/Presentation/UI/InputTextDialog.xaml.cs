using System.Windows;

namespace EAABAddIn.Src.UI;

public partial class InputTextDialog : Window
{
    public string InputText { get; private set; }

    public InputTextDialog()
    {
        InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        InputText = InputBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

