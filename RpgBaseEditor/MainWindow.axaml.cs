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

        public void SetSelectedItem(int index)
        {
            ((Content as DockPanel).Children[1] as TabControl).SelectedIndex = index;
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
            var campaignEditor = new CampaignEditor();
            campaignEditor.CampaignName = "campaign"+TabItems.Count;

            TabItems.Add(new TabItemModel(campaignEditor.CampaignName, campaignEditor, this));
        
            _window.UpdateComponent();
            _window.SetSelectedItem(TabItems.Count - 1);
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