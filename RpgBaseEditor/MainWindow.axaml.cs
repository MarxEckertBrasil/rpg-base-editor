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
        public List<TabItemModel> TabItems  { get; } = new List<TabItemModel>();
        
        public MainWindowDataContext(MainWindow window)
        {
            _window = window;
        }

        public void MenuNewCampaign()
        {
            TabItems.Add(new TabItemModel("campaign"+TabItems.Count, new CampaignEditor(), this));
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
        public ContentControl Content { get; }
        public TabItemModel(string header, ContentControl content, MainWindowDataContext dataContext)
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