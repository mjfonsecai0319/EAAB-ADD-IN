using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EAABAddIn.Src.Presentation.Base
{

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute());
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
            {
                System.Diagnostics.Debug.WriteLine("AsyncRelayCommand: CanExecute returned false");
                return;
            }
            
            _isExecuting = true;
            RaiseCanExecuteChanged();
            
            try
            {
                System.Diagnostics.Debug.WriteLine("AsyncRelayCommand: Executing command...");
                await _execute();
                System.Diagnostics.Debug.WriteLine("AsyncRelayCommand: Command completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand: Exception caught: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand: StackTrace: {ex.StackTrace}");
                throw; // Re-lanzar la excepción para que no se oculte
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
