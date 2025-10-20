using EAABAddIn.Src.Presentation.Base;

namespace EAABAddIn.Src.Presentation.Base
{
    internal abstract class BusyViewModelBase : PanelViewModelBase
    {
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    NotifyPropertyChanged(nameof(IsBusy));
                }
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    NotifyPropertyChanged(nameof(StatusMessage));
                }
            }
        }
    }
}
