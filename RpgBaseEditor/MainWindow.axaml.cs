using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Collections.Generic;

namespace RpgBaseEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            UpdateComponent();
            DataContext = new MainWindowDataContext(this);
            
        }

        public void UpdateComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }

    public class MainWindowDataContext
    {
        private MainWindow _window {get;}
        private TabControl _tabControl {get;}
        public List<TabItemModel> TabItems  { get; } = new List<TabItemModel>();
        
        public MainWindowDataContext(MainWindow window)
        {
            _window = window;
            _tabControl = _window.FindControl<TabControl>("TabControl");
        }

        public void MenuNewCampaign()
        {
            var campaignEditor = new CampaignEditor();

            TabItems.Add(new TabItemModel("campaign"+TabItems.Count, campaignEditor, this));
            _window.UpdateComponent();
        }

        public void RemoveTabItem(TabItemModel tabItem)
        {
            TabItems.Remove(tabItem);
            _window.UpdateComponent();
        }

        public void CloseApp()
        {
            _window.Close();
        }
    }

    public class TabItemModel
    {
        private MainWindowDataContext _dataContext { get; }
        public string Header { get; }
        public UserControl Content { get; }
        public TabItemModel(string header, UserControl content, MainWindowDataContext dataContext)
        {
            _dataContext = dataContext;
            Header = header;
            Content = content;
        }

        public void RemoveTab()
        {
            _dataContext.RemoveTabItem(this);
        }
    }
}