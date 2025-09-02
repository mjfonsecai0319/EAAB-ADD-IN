using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace EAABAddIn
{
    internal class PropertyPage1ViewModel : Page
    {
        protected override Task CommitAsync() => Task.FromResult(0);
        protected override Task InitializeAsync() => Task.FromResult(true);
        protected override void Uninitialize() { }

        public string DataUIContent
        {
            get => Data[0] as string;
            set => SetProperty(ref Data[0], value);
        }

        public ObservableCollection<string> MotoresBD { get; set; } =
            new ObservableCollection<string> { "Oracle", "PostgreSQL" };

        private string _motorSeleccionado;
        public string MotorSeleccionado
        {
            get => _motorSeleccionado;
            set
            {
                SetProperty(ref _motorSeleccionado, value);
                IsModified = true;
            }
        }

        private string _usuario;
        public string Usuario
        {
            get => _usuario;
            set
            {
                SetProperty(ref _usuario, value);
                IsModified = true;
            }
        }

        private string _contraseña;
        public string Contraseña
        {
            get => _contraseña;
            set
            {
                SetProperty(ref _contraseña, value);
                IsModified = true;
            }
        }

        private string _host;
        public string Host
        {
            get => _host;
            set
            {
                SetProperty(ref _host, value);
                IsModified = true;
            }
        }

        private string _puerto;
        public string Puerto
        {
            get => _puerto;
            set
            {
                SetProperty(ref _puerto, value);
                IsModified = true;
            }
        }
    }

    internal class PropertyPage1_ShowButton : Button
    {
        protected override void OnClick()
        {
            object[] data = new object[] { "Page UI content" };
            if (!PropertySheet.IsVisible)
                PropertySheet.ShowDialog("EAABAddIn_PropertySheet1", "EAABAddIn_PropertyPage1", data);
        }
    }
}
