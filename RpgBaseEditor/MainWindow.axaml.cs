using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Collections.Generic;
using System.IO;

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

        public void MenuOpenCampaign()
        {
            OpenFolderDialog folderDialog = new OpenFolderDialog();

            var folderDialogAsync = folderDialog.ShowAsync(_window); 
            folderDialogAsync.Wait();

            var result = folderDialogAsync.Result;      
            
            if (result.Length > 0)
            {
                if (Directory.Exists(result + "/Meta"))
                {
                    if (File.Exists(result + "/Meta/maps.json"))
                    {
                        MenuNewCampaign();
                        (TabItems[TabItems.Count - 1].Content as CampaignEditor).LoadCampaign(result);
                    }
                    else
                    {
                       var dialog = new Window();
                        dialog.Content = new Label() {Content = "Error:\n" + "maps.json doesn't exists"};
                        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        dialog.ExtendClientAreaToDecorationsHint = true;

                        dialog.ShowDialog(_window); 
                    }

                }
                else
                {
                    var dialog = new Window();
                    dialog.Content = new Label() {Content = "Error:\n" + "Meta folder doesn't exists"};
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.ExtendClientAreaToDecorationsHint = true;

                    dialog.ShowDialog(_window);
                }
            }
            
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