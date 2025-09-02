using EAABAddIn.Src.Presentation.ViewModel;
using System.Windows.Controls;

namespace EAABAddIn
{
    public partial class PropertyPage1View : UserControl
    {
        public PropertyPage1View()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is PropertyPage1ViewModel vm)
            {
                vm.Contraseña = ((PasswordBox)sender).Password;
            }
        }
    }
}