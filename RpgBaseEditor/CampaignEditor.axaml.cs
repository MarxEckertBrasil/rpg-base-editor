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

        public void LoadCampaign(string path)
        {
            CampaignName = path.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault();
            
           IEnumerable<string> mapPaths = new List<string>();

            using (StreamReader reader = new StreamReader(path + "/Meta/maps.json"))
            {    
                string json = reader.ReadToEnd();
                mapPaths = JsonSerializer.Deserialize<IEnumerable<string>>(json);
            }

            (DataContext as CampaignEditorDataContext).LoadMaps(mapPaths.ToArray());
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
                TileViewerPanel.SelectedTile = TileViewerPanel.ClickAreas.LastOrDefault(x => VerifyMouseInRec(mousePos.Value, x.rec));
            else
                TileViewerPanel.SelectedTile = ("", 0, new Rect());
                
            TileViewerPanel.InvalidateVisual();
            CampaignEditorPanel.InvalidateTileEditVisual();
        }

        private bool VerifyMouseInRec(Point mousePos, Rect rec)
        {
            if ((mousePos.X > rec.X && mousePos.X < (rec.X+rec.Width)) &&
                (mousePos.Y > rec.Y && mousePos.Y < (rec.Y+rec.Height)))
                return true;

            return false;
        }

        public void LoadMaps(string[] paths)
        {
            CampaignEditorPanel.MapManager.LoadMaps(paths);
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
        public (string Type, uint Id, Rect rec) SelectedTile = ("", 0, new Rect());

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
                SelectedTile = ("", 0, new Rect());
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
            
            SelectedTile = ("", 0, new Rect());

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
                
                if (layer.data == null)
                {
                    if (layer.objects != null)
                    {
                        foreach (var obj in layer.objects)
                        {
                            if (!obj.type.ToLowerInvariant().Contains("door"))
                                continue;

                            var resizedRec = new Rect(obj.x * IMAGE_SCALE, obj.y * IMAGE_SCALE, obj.width * IMAGE_SCALE, obj.height * IMAGE_SCALE);
                            ClickAreas.Add(("Door", obj.id, resizedRec));
                        }
                    }

                    continue;
                }

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

            if (SelectedTile.rec.Width > 0 && SelectedTile.rec.Height > 0)
            {
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedTile.rec.X, SelectedTile.rec.Y), 
                                new Point(SelectedTile.rec.X+SelectedTile.rec.Width, SelectedTile.rec.Y));

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedTile.rec.X, SelectedTile.rec.Y), 
                                new Point(SelectedTile.rec.X, SelectedTile.rec.Y+SelectedTile.rec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedTile.rec.X+SelectedTile.rec.Width, SelectedTile.rec.Y), 
                                new Point(SelectedTile.rec.X+SelectedTile.rec.Width, SelectedTile.rec.Y+SelectedTile.rec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255,0,0)), 2), new Point(SelectedTile.rec.X, SelectedTile.rec.Y+SelectedTile.rec.Height), 
                                new Point(SelectedTile.rec.X+SelectedTile.rec.Width, SelectedTile.rec.Y+SelectedTile.rec.Height));
            }

            base.Render(context);
        }

        internal System.Collections.IEnumerable GetDoorIds(string selectedMap)
        {
            using StreamReader reader = new StreamReader("Campaigns/"+selectedMap);
            
            string json = reader.ReadToEnd();
            var tiledMap = JsonSerializer.Deserialize<TiledMap>(json);

            foreach (var tileset in tiledMap.tilesets)
            {
                using StreamReader tilesetReader = new StreamReader(CampaignFolder+tileset.source);
                json = tilesetReader.ReadToEnd();
                var tiledTileset = JsonSerializer.Deserialize<TiledTileset>(json);

                if (tiledTileset != null)
                {
                    tiledTileset.x_tiles = (int)((tiledTileset.imagewidth - tiledTileset.margin*2) / tiledTileset.tilewidth);
                    
                    tiledMap.TiledTilesets.Add((Firstgid: tileset.firstgid, Tileset: tiledTileset, Source: tileset.source));             
                }
            }

            var doorIdList = new List<string>();
            foreach (var layer in tiledMap.layers)
            {
                if (layer.data != null)
                    continue;
                
                if (layer.objects == null || layer.objects.Count == 0)
                    continue;

                foreach (var obj in layer.objects)
                {
                    if (!obj.type.ToLowerInvariant().Contains("door"))
                        continue;
                    
                    doorIdList.Add(obj.id.ToString());
                }
            }

            return doorIdList;
        }

        internal TiledMap GetTiledMapOnly(string mapPath)
        {
            using StreamReader reader = new StreamReader(mapPath);
            
            string json = reader.ReadToEnd();
            var tiledMap = JsonSerializer.Deserialize<TiledMap>(json);

            foreach (var tileset in tiledMap.tilesets)
            {
                using StreamReader tilesetReader = new StreamReader(CampaignFolder+tileset.source);
                json = tilesetReader.ReadToEnd();
                var tiledTileset = JsonSerializer.Deserialize<TiledTileset>(json);

                if (tiledTileset != null)
                {
                    tiledTileset.x_tiles = (int)((tiledTileset.imagewidth - tiledTileset.margin*2) / tiledTileset.tilewidth);
                    
                    tiledMap.TiledTilesets.Add((Firstgid: tileset.firstgid, Tileset: tiledTileset, Source: tileset.source));             
                }
            }
            

            return tiledMap;
        }

        internal void UpdateTiledMap(string mapPath, TiledMap tiledMap)
        {
            var tiledMapJson = JsonSerializer.Serialize(tiledMap, new JsonSerializerOptions() {WriteIndented=true});       
            File.WriteAllText(mapPath, tiledMapJson);
        }

    }

    public class CampaignEditorControl : DockPanel
    {
        public CampaignEditorMapManager MapManager;
        public Panel TileManager;
        
        private TileViewer _tileViewerPanel;
        public CampaignEditor UserControl;
        public CampaignEditorDataContext EditorDataContext;
        public TileEditMapManager TileEditMapManager;
        
        public CampaignEditorControl(CampaignEditor userControl, TileViewer tileViewerPanel, CampaignEditorDataContext campaignEditorDataContext)
        {    
            EditorDataContext = campaignEditorDataContext;
            UserControl = userControl;
            _tileViewerPanel = tileViewerPanel;
            MapManager = new CampaignEditorMapManager(this);
            TileManager = new Panel();

            TileEditMapManager = new TileEditMapManager(_tileViewerPanel, MapManager);

            DockPanel.SetDock(MapManager, Dock.Right);
            DockPanel.SetDock(MapManager, Dock.Top);

            DockPanel.SetDock(TileEditMapManager, Dock.Right);
            DockPanel.SetDock(TileEditMapManager, Dock.Bottom);

            DockPanel.SetDock(TileManager, Dock.Left);

            this.Children.Add(MapManager);
            this.Children.Add(TileEditMapManager);
            this.Children.Add(TileManager);

            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        }

        public void InvalidateTileEditVisual()
        {
            if (!string.IsNullOrEmpty(_tileViewerPanel.SelectedTile.Type))
                TileEditMapManager.TypeLabel.Content = "Tile Type: " + _tileViewerPanel.SelectedTile.Type;

            if (TileEditMapManager.TypeLabel.Content.GetType() == typeof(string) &&
                (TileEditMapManager.TypeLabel.Content as string).ToLowerInvariant().Contains("door"))
            {
                TileEditMapManager.DoorIdLabel.Content = "Door Id: " + _tileViewerPanel.SelectedTile.Id;

                TileEditMapManager.DestLabel.Content = "Destination:";

                var formattedLastMap = _tileViewerPanel.LastMapSelected.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault();
                TileEditMapManager.ListBox.Items = MapManager.MapGrid.Children.Where(name => (name as Panel).Name.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault() 
                                    != formattedLastMap).Select(x => (x as Panel).Name);
            }
            else
            {
                TileEditMapManager.DoorIdLabel.Content = "";
                TileEditMapManager.DestLabel.Content = "";
                TileEditMapManager.ListBox.Items = new List<string>();
                TileEditMapManager.SelectDoorLabel.Content = "";
                TileEditMapManager.DoorIdListBox.Items = new List<string>();
            }

            TileEditMapManager.InvalidateVisual();
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

            ExportButton.Command = new ExportCommand();
            ExportButton.CommandParameter = this;

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

            var moveButton = CreateButton("^");
            moveButton.Command = new MoveMapCommand() {RowPanel = rowPanel};
            moveButton.CommandParameter = this;
            buttonGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;

            rowPanel.Children.Add(moveButton);
            rowPanel.Children.Add(buttonGrid);
            rowPanel.Children.Add(removeButton);
            rowPanel.Name = Path.GetRelativePath(Directory.GetCurrentDirectory()
            ,_campaignEditorControl.UserControl.GetCampaignName()+"/Maps/"+map.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault() 
                                        );
            
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
        
        internal class ExportCommand : ICommand
        {
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                var campManager = (CampaignEditorMapManager)parameter;
                
                var mapList = campManager.MapGrid.Children.OrderBy(x => Grid.GetRow((Panel)x)).Select(x => x.Name);

                var mapListJson = JsonSerializer.Serialize(mapList, new JsonSerializerOptions() {WriteIndented=true});       
                File.WriteAllText("Campaigns/"+campManager._campaignEditorControl.UserControl.GetCampaignName()+"/maps.json", mapListJson);
                campManager._capBuilder.AddAsset("Campaigns/"+campManager._campaignEditorControl.UserControl.GetCampaignName()+"/maps.json", AssetType.META);

                File.Delete("Campaigns/"+campManager._campaignEditorControl.UserControl.GetCampaignName()+"/maps.json");

                campManager._capBuilder.ExportCap("Campaigns/"+campManager._campaignEditorControl.UserControl.GetCampaignName()+".cap", true);
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

        internal class MoveMapCommand : ICommand
        {
            public DockPanel? RowPanel;
            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return true;
            }

            public void Execute(object? parameter)
            {
                if (RowPanel == null)
                    return;
                
                var campManager = (CampaignEditorMapManager)parameter;

                var index = Grid.GetRow(RowPanel);
                
                if (index <= 0)
                    return;

                var aux = (DockPanel)campManager.MapGrid.Children.First(x => Grid.GetRow((DockPanel)x) == 0);

                Grid.SetRow(RowPanel, 0);
                Grid.SetRow(aux, index);
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
                
                (parameter as CampaignEditorMapManager)._campaignEditorControl.TileEditMapManager.TypeLabel.Content = "";
                (parameter as CampaignEditorMapManager)._campaignEditorControl.EditorDataContext.TileViewerPanel.SelectedTile = ("", 0, new Rect());
                (parameter as CampaignEditorMapManager)._campaignEditorControl.InvalidateTileEditVisual();

                (parameter as CampaignEditorMapManager)._campaignEditorControl.EditorDataContext.TileViewerPanel.CampaignFolder = campaignFolder;
                (parameter as CampaignEditorMapManager)._campaignEditorControl.EditorDataContext.TileViewerPanel.GetTiledMap(newMapPath);
            }
        }

        public void LoadMaps(string[] paths)
        {
            _capBuilder = new CapBuilder(false); 
            _capBuilder.ImportPath(Path.GetFullPath("Campaigns/"+_campaignEditorControl.UserControl.GetCampaignName(), 
                                        Directory.GetCurrentDirectory()));

            foreach (var path in paths)
            {
                SetRowOnGrid(path);
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

    public class TileEditMapManager : DockPanel
    {
        private TileViewer? _tileViewerPanel;
        private CampaignEditorMapManager? _campaignEditor;

        public Label TypeLabel;
        public Label DestLabel;
        public Label DoorIdLabel;

        public Label SelectDoorLabel;
        public ListBox DoorIdListBox;

        public ListBox ListBox;

        private string _selectedMap = string.Empty;
        public TileEditMapManager(TileViewer? tileViewerPanel, CampaignEditorMapManager? campaignEditor)
        {
            _tileViewerPanel = tileViewerPanel;
            _campaignEditor = campaignEditor;
            
            TypeLabel = new Label() { Content = ""};
            DestLabel = new Label() { Content = ""};
            DoorIdLabel = new Label() { Content = ""};
            SelectDoorLabel = new Label() {Content = ""};

            DoorIdListBox = new ListBox() {SelectionMode = SelectionMode.Single, MaxHeight = 165};
            ListBox = new ListBox() {SelectionMode = SelectionMode.Single, MaxHeight = 165};
            
            var itemList = new List<string>();

            foreach (var map in _campaignEditor.MapGrid.Children)
            {
                itemList.Add(map.Name);
            }

            DoorIdListBox.SelectionChanged += OnDoorIdSelectionChange;
            ListBox.SelectionChanged += OnSelectionChange;

            ListBox.Items = itemList;
            
            SetDock(TypeLabel, Dock.Top);
            SetDock(DoorIdLabel, Dock.Top);

            SetDock(DestLabel, Dock.Top);
            SetDock(ListBox, Dock.Top);

            SetDock(SelectDoorLabel, Dock.Top);
            SetDock(DoorIdListBox, Dock.Top);

            this.Children.Add(TypeLabel);
            this.Children.Add(new Separator());
            this.Children.Add(DoorIdLabel);
            this.Children.Add(DestLabel);
            this.Children.Add(ListBox);
            this.Children.Add(new Separator());
            this.Children.Add(SelectDoorLabel);
            this.Children.Add(DoorIdListBox);
        }

        private void OnDoorIdSelectionChange(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null &&
                !string.IsNullOrEmpty((sender as ListBox).SelectedItem?.ToString()))
            {
                if (string.IsNullOrEmpty(_selectedMap) || _selectedMap == _tileViewerPanel.LastMapSelected)
                    return;

                var tiledMap = _tileViewerPanel.GetTiledMapOnly(_tileViewerPanel.LastMapSelected);
                
                foreach (var layer in tiledMap.layers)
                {
                    if (layer.data != null)
                        continue;

                    if (layer.objects == null || layer.objects.Count() == 0)
                        continue;
                    
                    foreach (var obj in layer.objects)
                    {
                        if (obj.id != _tileViewerPanel.SelectedTile.Id)
                            continue;

                        obj.type += ":"+_selectedMap.Split(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).LastOrDefault()+":"+(sender as ListBox).SelectedItem.ToString();
                    }
                }

                _tileViewerPanel.UpdateTiledMap(_tileViewerPanel.LastMapSelected, tiledMap);
            }

        }

        private void OnSelectionChange(object? sender, SelectionChangedEventArgs e)
        {
            if (sender != null &&
                !string.IsNullOrEmpty((sender as ListBox).SelectedItem?.ToString()))
            {
                SelectDoorLabel.Content = "Door Id Destination: ";
                DoorIdListBox.Items = _tileViewerPanel.GetDoorIds((sender as ListBox).SelectedItem.ToString());
                _selectedMap = (sender as ListBox).SelectedItem.ToString();
            }
            else
            {
                SelectDoorLabel.Content = "";
                DoorIdListBox.Items = new List<string>(); 
                _selectedMap = string.Empty;
            }
        }
    }
}