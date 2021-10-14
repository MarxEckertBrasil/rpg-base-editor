using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Visuals.Media.Imaging;
using Newtonsoft.Json;
using RpgBaseEditor.Tiles;

namespace RpgBaseEditor
{
    public partial class CampaignEditor : UserControl
    {
        public CampaignEditor()
        {
            DataContext = new CampaignEditorDataContext(this);  
            this.AddHandler(PointerPressedEvent, (DataContext as CampaignEditorDataContext).MouseDownHandler, handledEventsToo: true);     

            UpdateComponent();
        }

        public void UpdateComponent()
        {
            AvaloniaXamlLoader.Load(this);                
        }
    }

    public class CampaignEditorDataContext
    {
        public DockPanel Content {get;}
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

            //Call render
            (_campaignEditor.Parent.Parent.Parent.Parent as MainWindow).UpdateComponent();      
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
            if (_tiledMap.TiledTilesets.Count <= 0)
                return;

            foreach (var layer in _tiledMap.layers)
            {
                int x_pos = 0;
                int y_pos = 0;
                
                if (layer.data != null)
                    foreach (var tile in layer.data)
                    {   
                        var tile_id = tile;
                        tile_id &= ~(FLIPPED_HORIZONTALLY_FLAG |
                                    FLIPPED_VERTICALLY_FLAG   |
                                    FLIPPED_DIAGONALLY_FLAG   );

                        if (tile_id > 0)
                        {        
                            var rotate = false;
                            var posVec = new System.Numerics.Vector2(x_pos*_tiledMap.tilewidth, y_pos*_tiledMap.tileheight);
                            
                            if (tile_id >= 250)
                            {
                                var teste = "new";
                            }
                            var rec = GetTileRecById(tile_id, _tiledMap);
                            
                            if ((tile & FLIPPED_DIAGONALLY_FLAG) > 0)
                            {
                                rotate = true;
                                rec = new Rect(rec.X, rec.Y, rec.Width, -rec.Height);                      
                            }  

                            if ((tile & FLIPPED_HORIZONTALLY_FLAG) > 0)
                            {
                                rec = new Rect(rec.X, rec.Y, -rec.Width, rec.Height);
                            }

                            if ((tile & FLIPPED_VERTICALLY_FLAG) > 0)
                            {
                                rec = new Rect(rec.X, rec.Y, rec.Width, -rec.Height);
                            }
                        
                            var source = _tiledMap.TiledMapTextures.LastOrDefault(x => tile_id >= x.Firstgid).Texture;    
                            var resizedRec = new Rect(posVec.X * IMAGE_SCALE, posVec.Y * IMAGE_SCALE, Math.Abs(rec.Width * IMAGE_SCALE), Math.Abs(rec.Height * IMAGE_SCALE));

                            context.DrawImage(source, rec, resizedRec);
                                                 
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

            _userControl.UpdateComponent();             
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
            return tileTileset.Tileset.tiles.FindLast(x => x.id == ((int)tile_id - tileTileset.Firstgid) && !notSelectable.Contains(x.type))?.type.ToLowerInvariant();
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
        private TileViewer _tileViewerPanel;
        private CampaignEditor _userControl;
        public CampaignEditorControl(CampaignEditor userControl, TileViewer tileViewerPanel)
        {
            _userControl = userControl;
            _tileViewerPanel = tileViewerPanel;

            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        }
    }
}