using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Visuals.Media.Imaging;
using Newtonsoft.Json;
using RpgBaseEditor.Tiles;
using static Avalonia.Media.DrawingContext;

namespace RpgBaseEditor
{
    public partial class CampaignEditor : UserControl
    {
        public CampaignEditor()
        {
            DataContext = new CampaignEditorDataContext(this);  
            this.AddHandler(PointerPressedEvent, (DataContext as CampaignEditorDataContext).MouseDownHandler);     
            
            InitializeComponent();
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
            CampaignEditorPanel = new CampaignEditorControl(campaignEditor, TileViewerPanel);
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

        //Avalonia
        public TileViewer(CampaignEditor userControl)
        {
            _userControl = userControl;
            GetTiledMap(string.Empty);

            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }

        public void GetTiledMap(string selectedMap)
        {
            selectedMap = "Adventure/Maps/map.json";
            using StreamReader reader = new StreamReader(selectedMap);
            
            string json = reader.ReadToEnd();
            _tiledMap = JsonConvert.DeserializeObject<TiledMap>(json);

            foreach (var tileset in _tiledMap.tilesets)
            {
                using StreamReader tilesetReader = new StreamReader("Adventure/Maps/"+tileset.source);
                json = tilesetReader.ReadToEnd();
                var tiledTileset = JsonConvert.DeserializeObject<TiledTileset>(json);

                if (tiledTileset != null)
                {
                    tiledTileset.x_tiles = (int)((tiledTileset.imagewidth - tiledTileset.margin*2) / tiledTileset.tilewidth);
                    tiledTileset.y_tiles = (int)((tiledTileset.imageheight - tiledTileset.margin*2) / tiledTileset.tileheight);

                    _tiledMap.TiledTilesets.Add((Firstgid: tileset.firstgid, Tileset: tiledTileset));             
                }
            }
            
            foreach (var tileTileset in _tiledMap.TiledTilesets)
            {
                _tiledMap.TiledMapTextures.Add((Firstgid: tileTileset.Firstgid, Texture: new Bitmap("Adventure/Tilesets/"+tileTileset.Tileset.name+"/"+tileTileset.Tileset.image)));  
            }    
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
            var y_pos = (int)(Math.Floor((decimal)((tile - tileTileset.Firstgid) / tileTileset.Tileset.y_tiles)) * tileTileset.Tileset.tileheight + tileTileset.Tileset.margin);
            
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
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200,200,200)), 1), new Point(SelectedRec.X, SelectedRec.Y), 
                                new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y));

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200,200,200)), 1), new Point(SelectedRec.X, SelectedRec.Y), 
                                new Point(SelectedRec.X, SelectedRec.Y+SelectedRec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200,200,200)), 1), new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y), 
                                new Point(SelectedRec.X+SelectedRec.Width, SelectedRec.Y+SelectedRec.Height)); 

                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(200,200,200)), 1), new Point(SelectedRec.X, SelectedRec.Y+SelectedRec.Height), 
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
        private CampaignEditor _userControl;
        public CampaignEditorControl(CampaignEditor userControl, TileViewer tileViewerPanel)
        {
            _userControl = userControl;
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

        private CampaignEditorControl _campaignEditorControl;
        public CampaignEditorMapManager(CampaignEditorControl campaignEditorControl)
        {
            _campaignEditorControl = campaignEditorControl;
            MapGrid = CreateGrid();
            AddMapButton = CreateButton("Add Map");
            ExportButton = CreateButton("Export .cap");

            this.Children.Add(MapGrid);
            this.Children.Add(AddMapButton);
            this.Children.Add(ExportButton);

            this.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        }

        private Grid CreateGrid()
        {
            var newGrid = new Grid();
            newGrid.Background = Brushes.Gainsboro;
            newGrid.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            newGrid.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            newGrid.ShowGridLines = true;
            newGrid.Width = 200;
            newGrid.Height = 165;

            return newGrid;
        }

        private Button CreateButton(string label)
        {
            var newButton = new Button();
            newButton.Name = label;
            newButton.Background = Brushes.Blue;
            newButton.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            newButton.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
            newButton.Width = 100;
            newButton.Height = 20;

            return newButton;
        }
    }
}