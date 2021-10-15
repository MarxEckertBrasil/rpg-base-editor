using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LibCap;
using System.Text.Json;
using RpgBaseEditor.Tiles;
using static Avalonia.Media.DrawingContext;

namespace RpgBaseEditor
{
    public partial class CampaignEditor : UserControl
    {
        public string CampaignName = string.Empty;
        public CampaignEditor()
        {
            DataContext = new CampaignEditorDataContext(this);  
            this.AddHandler(PointerPressedEvent, (DataContext as CampaignEditorDataContext).MouseDownHandler);     
            
            InitializeComponent();
        }

        public Window GetParentWindow()
        {
            return (Parent.Parent.Parent.Parent as Window);
        }

        public string GetCampaignName()
        {
            return CampaignName;
        }
        
        public void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);                
        }
    }

    public class CampaignEditorDataContext
    {
        public DockPanel Content { get; }
        public TileViewer TileViewerPanel {get;}
        public CampaignEditorControl CampaignEditorPanel {get;}

        private CampaignEditor _campaignEditor;

        public CampaignEditorDataContext(CampaignEditor campaignEditor)
        {
            _campaignEditor = campaignEditor;
            TileViewerPanel = new TileViewer(campaignEditor);
            CampaignEditorPanel = new CampaignEditorControl(campaignEditor, TileViewerPanel, this);
            Content = new DockPanel();
            
            Content.Children.Add(TileViewerPanel);
            Content.Children.Add(CampaignEditorPanel);
        }

        internal void MouseDownHandler(object? sender, PointerPressedEventArgs e)
        {
            var mousePos = e.GetCurrentPoint((Avalonia.VisualTree.IVisual)sender)?.Position;
            if ( mousePos != null &&
                TileViewerPanel.ClickAreas.Count > 0 &&
                TileViewerPanel.ClickAreas.Any(x => VerifyMouseInRec(mousePos.Value, x.rec)))
                TileViewerPanel.SelectedRec = TileViewerPanel.ClickAreas.FirstOrDefault(x => VerifyMouseInRec(mousePos.Value, x.rec)).rec;
            else
                TileViewerPanel.SelectedRec = new Rect();
                
            TileViewerPanel.InvalidateVisual();
        }

        private bool VerifyMouseInRec(Point mousePos, Rect rec)
        {
            if ((mousePos.X > rec.X && mousePos.X < (rec.X+rec.Width)) &&
                (mousePos.Y > rec.Y && mousePos.Y < (rec.Y+rec.Height)))
                return true;

            return false;
        }
    }

    public class TileViewer : Panel
    {
        const int IMAGE_SCALE = 2;
        const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        const uint FLIPPED_VERTICALLY_FLAG   = 0x40000000;
        const uint FLIPPED_DIAGONALLY_FLAG   = 0x20000000;

        private TiledMap _tiledMap = new TiledMap();
        private List<Bitmap> _textures = new List<Bitmap>();
        private CampaignEditor _userControl;

        public List<(string Type, uint Id, Rect rec)> ClickAreas = new List<(string Type, uint Id, Rect rec)>();
        public Rect SelectedRec = new Rect();

        public string CampaignFolder { get; internal set; }

        public string LastMapSelected;
        //Avalonia
        public TileViewer(CampaignEditor userControl)
        {
            _userControl = userControl;

            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }

        public void GetTiledMap(string selectedMap)
        {
            if (string.IsNullOrEmpty(selectedMap))
            {
                _tiledMap = new TiledMap();
                LastMapSelected = string.Empty;
                SelectedRec = new Rect();
                this.InvalidateVisual();
                return;
            }

            if (LastMapSelected == selectedMap)
                return;
            
            LastMapSelected = selectedMap;
            using StreamReader reader = new StreamReader(selectedMap);
            
            string json = reader.ReadToEnd();
            _tiledMap = JsonSerializer.Deserialize<TiledMap>(json);

            foreach (var tileset in _tiledMap.tilesets)
            {
                using StreamReader tilesetReader = new StreamReader(CampaignFolder+tileset.source);
                json = tilesetReader.ReadToEnd();
                var tiledTileset = JsonSerializer.Deserialize<TiledTileset>(json);

                if (tiledTileset != null)
                {
                    tiledTileset.x_tiles = (int)((tiledTileset.imagewidth - tiledTileset.margin*2) / tiledTileset.tilewidth);
                    
                    _tiledMap.TiledTilesets.Add((Firstgid: tileset.firstgid, Tileset: tiledTileset, Source: tileset.source));             
                }
            }
            
            foreach (var tileTileset in _tiledMap.TiledTilesets)
            {
                var image = tileTileset.Source.Remove(tileTileset.Source.Length - tileTileset.Source.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault().Length);
                _tiledMap.TiledMapTextures.Add((Firstgid: tileTileset.Firstgid, Texture: new Bitmap(CampaignFolder+image+tileTileset.Tileset.image)));  
            }    

            SelectedRec = new Rect();

            this.InvalidateVisual();
        }

        public void DrawTiledMap(DrawingContext context)
        {      
            var pushedState = new PushedState();  
            if (_tiledMap.TiledTilesets.Count <= 0)
                return;

            foreach (var layer in _tiledMap.layers)
            {
                int x_pos = 0;
                int y_pos = 0;
                
                if (layer.data != null)
                    foreach (var tile in layer.data)
                    {   
                        var newTransform = new Matrix(1,0,0,1,0,0);

                        var tile_id = tile;
                        tile_id &= ~(FLIPPED_HORIZONTALLY_FLAG |
                                    FLIPPED_VERTICALLY_FLAG   |
                                    FLIPPED_DIAGONALLY_FLAG   );

                        if (tile_id > 0)
                        {        
                            var posVec = new System.Numerics.Vector2(x_pos*_tiledMap.tilewidth, y_pos*_tiledMap.tileheight);

                            var rec = GetTileRecById(tile_id, _tiledMap);

                            if ((tile & FLIPPED_DIAGONALLY_FLAG) > 0)
                            {
                                newTransform = newTransform * new Matrix(0, 1, 1, 0, 
                                                    (posVec.X - posVec.Y) * IMAGE_SCALE, 
                                                    (posVec.Y - posVec.X) * IMAGE_SCALE);
                            }  

                            if ((tile & FLIPPED_HORIZONTALLY_FLAG) > 0)
                            {
                                newTransform = newTransform * new Matrix(-1, 0, 0, 1, posVec.X * IMAGE_SCALE * 2 + _tiledMap.tilewidth*IMAGE_SCALE, 0);
                            }

                            if ((tile & FLIPPED_VERTICALLY_FLAG) > 0)
                            {
                                newTransform = newTransform * new Matrix(1, 0, 0, -1, 0,
                                                        posVec.Y * IMAGE_SCALE * 2 + _tiledMap.tileheight*IMAGE_SCALE);
                            }                    
                        
                            var source = _tiledMap.TiledMapTextures.LastOrDefault(x => tile_id >= x.Firstgid).Texture;

                            var resizedRec = new Rect(posVec.X * IMAGE_SCALE, posVec.Y * IMAGE_SCALE, rec.Width * IMAGE_SCALE, rec.Height * IMAGE_SCALE);
                           
                            pushedState = context.PushSetTransform(newTransform); 

                            context.DrawImage(source, rec, resizedRec);

                            pushedState.Dispose();

                            var selectableType = IsTileSelectable(tile_id);
                            if (!string.IsNullOrEmpty(selectableType))
                                ClickAreas.Add((selectableType, tile_id, resizedRec));
                        }

                        x_pos++;
                        if (x_pos >= layer.width)
                        {
                            x_pos = 0;
                            y_pos++;
                        }
                    }
            } 
            
            this.InvalidateVisual();             
        }
        
        private Rect GetTileRecById(uint tile, TiledMap tiledMap)
        {
            var tileTileset = tiledMap.TiledTilesets.LastOrDefault(x => tile >= x.Firstgid);

            var x_pos = ((tile - tileTileset.Firstgid) % tileTileset.Tileset.x_tiles) * tileTileset.Tileset.tilewidth + tileTileset.Tileset.margin;
            var y_pos = (int)(Math.Floor((decimal)((tile - tileTileset.Firstgid) / tileTileset.Tileset.x_tiles)) * tileTileset.Tileset.tileheight + tileTileset.Tileset.margin);
            
            var rec = new Rect((int)x_pos, y_pos, tiledMap.tilewidth, tiledMap.tileheight);

            return rec;
        }
        
        private string? IsTileSelectable(uint tile_id)
        {
            var notSelectable = new string[] { "wall" };

            var tileTileset = _tiledMap.TiledTilesets.LastOrDefault(x => tile_id >= x.Firstgid);
            return tileTileset.Tileset.tiles.FindLast(x => (x.id == ((int)tile_id - tileTileset.Firstgid) && !notSelectable.Any(t => t == x.type)))?.type.ToLowerInvariant();
        }

        public override void Render(DrawingContext context)
        {            
            DrawTiledMap(context);             

            if (SelectedRec.Width > 0 && SelectedRec.Height > 0)
            {
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedRec.X, SelectedRec.Y), 
                                new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y));

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedRec.X, SelectedRec.Y), 
                                new Point(SelectedRec.X, SelectedRec.Y+SelectedRec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y), 
                                new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y+SelectedRec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedRec.X, SelectedRec.Y+SelectedRec.Height), 
                                new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y+SelectedRec.Height));
            }

            base.Render(context);
        }
    }

    public class CampaignEditorControl : Panel
    {
        public CampaignEditorMapManager MapManager;
        public Panel TileManager;
        
        private TileViewer _tileViewerPanel;
        public CampaignEditor UserControl;
        public CampaignEditorDataContext EditorDataContext;
        public CampaignEditorControl(CampaignEditor userControl, TileViewer tileViewerPanel, CampaignEditorDataContext campaignEditorDataContext)
        {
            EditorDataContext = campaignEditorDataContext;
            UserControl = userControl;
            _tileViewerPanel = tileViewerPanel;
            MapManager = new CampaignEditorMapManager(this);
            TileManager = new Panel();

            this.Children.Add(MapManager);
            this.Children.Add(TileManager);

            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        }

    }

    public class CampaignEditorMapManager : DockPanel
    {
        public Grid MapGrid;
        public Button AddMapButton;
        public Button ExportButton;
        public ScrollViewer ScrollViewer;

        private CampaignEditorControl _campaignEditorControl;
        private CapBuilder? _capBuilder;
        public CampaignEditorMapManager(CampaignEditorControl campaignEditorControl)
        {
            _campaignEditorControl = campaignEditorControl;
            MapGrid = CreateGrid();
            AddMapButton = CreateButton("Add Map");
            ExportButton = CreateButton("Export .cap");

            AddMapButton.Command = new AddMapCommand();
            AddMapButton.CommandParameter = this;

            ScrollViewer = new ScrollViewer();
            ScrollViewer.MaxHeight = 165;
            ScrollViewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            ScrollViewer.Content = MapGrid;
            SetDock(ScrollViewer, Dock.Top);

            this.Children.Add(ScrollViewer);
            this.Children.Add(AddMapButton);
            this.Children.Add(ExportButton);

        }

        private Grid CreateGrid()
        {
            var newGrid = new Grid();
            newGrid.ShowGridLines = true;
            newGrid.Width = 500;

            var colDef1 = new ColumnDefinition();
            colDef1.Width = new GridLength(1, GridUnitType.Auto);
            newGrid.ColumnDefinitions.Add(colDef1);

            return newGrid;
        }

        private void SetRowOnGrid(string map)
        {
            var rowDef = new RowDefinition();
            rowDef.Height = new GridLength(1, GridUnitType.Auto);
            
            var rowPanel = new DockPanel();
            var removeButton = CreateButton("-");
            removeButton.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            removeButton.Command = new RemoveMapCommand() {MapPath=map, RowPanel=rowPanel, RowDef = rowDef};
            removeButton.CommandParameter = this;

            var buttonGrid = new Button(){Content = map.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault()};
            buttonGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            buttonGrid.CommandParameter = this;
            buttonGrid.Command = new SelectMapCommand() {MapPath=map};


            rowPanel.Children.Add(buttonGrid);
            rowPanel.Children.Add(removeButton);
            rowPanel.Name = map;
            
            MapGrid.RowDefinitions.Add(rowDef);
            Grid.SetRow(rowPanel, MapGrid.RowDefinitions.Count - 1);
            MapGrid.Children.Add(rowPanel);
        }

        private Button CreateButton(string label)
        {
            var newButton = new Button();
            newButton.Content = label;
            newButton.Background = Brushes.Blue;

            return newButton;
        }

        internal class AddMapCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                (parameter as CampaignEditorMapManager).GetPath();
            }
        }
        
        internal class RemoveMapCommand : ICommand
        {
            public Panel? RowPanel;
            public RowDefinition? RowDef;
            public string MapPath = string.Empty;

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                var campEditor = (parameter as CampaignEditorMapManager);

                var capBuilder = campEditor?._capBuilder;
                if (!string.IsNullOrEmpty(MapPath))
                {
                    var asset = capBuilder?.RemoveAsset(MapPath);

                    if (asset == null || !asset.IsOk)
                    {
                        var dialog = new Window();
                        dialog.Content = new Label() {Content = "Error:\n" + asset?.Msg};
                        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        dialog.ExtendClientAreaToDecorationsHint = true;

                        dialog.ShowDialog(campEditor?._campaignEditorControl.UserControl.GetParentWindow());
                    }
                    else
                    {
                        campEditor?.MapGrid.Children.Remove(RowPanel);
                        
                        if (RowDef != null)
                            campEditor?.MapGrid.RowDefinitions.Remove(RowDef);

                        var campaignFolder = "Campaigns/"+campEditor?._campaignEditorControl.UserControl.GetCampaignName()+"/";
                        var newMapPath = Path.GetFullPath(campaignFolder+"Maps/"+MapPath.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault(), 
                                        Directory.GetCurrentDirectory());

                        if (campEditor?._campaignEditorControl.EditorDataContext.TileViewerPanel.LastMapSelected == newMapPath)
                            campEditor?._campaignEditorControl.EditorDataContext.TileViewerPanel.GetTiledMap(string.Empty);
                    }
                }
            }
        }

        internal class SelectMapCommand : ICommand
        {
            public string MapPath { get; set; }

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                var campaignFolder = "Campaigns/"+(parameter as CampaignEditorMapManager)._campaignEditorControl.UserControl.GetCampaignName()+"/";
                var newMapPath = Path.GetFullPath(campaignFolder+"Maps/"+MapPath.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault(), 
                                        Directory.GetCurrentDirectory());

                (parameter as CampaignEditorMapManager)._campaignEditorControl.EditorDataContext.TileViewerPanel.CampaignFolder = campaignFolder;
                (parameter as CampaignEditorMapManager)._campaignEditorControl.EditorDataContext.TileViewerPanel.GetTiledMap(newMapPath);
            }
        }

        private void GetPath()
        {
            OpenFileDialog fileDialog = new OpenFileDialog();

            var fileDialogAsync = fileDialog.ShowAsync(_campaignEditorControl.UserControl.GetParentWindow()); 
            fileDialogAsync.Wait();

            var result = fileDialogAsync.Result;      
            
            if (result.Length > 0)
            {
                if (_capBuilder == null)
                    _capBuilder = new CapBuilder(Path.GetFullPath("Campaigns/"+_campaignEditorControl.UserControl.GetCampaignName(), 
                                        Directory.GetCurrentDirectory()), false); 

                if (_capBuilder.ContainsFile(result[0]))
                    return;

                var asset = _capBuilder.AddAsset(result[0], AssetType.MAP);

                if (!asset.IsOk)
                {
                    var dialog = new Window();
                    dialog.Content = new Label() {Content = "Error:\n" + asset.Msg};
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    dialog.ExtendClientAreaToDecorationsHint = true;

                    dialog.ShowDialog(_campaignEditorControl.UserControl.GetParentWindow());
                }
                else
                {
                    SetRowOnGrid(result[0]);
                }
            }
        }
    }  
}