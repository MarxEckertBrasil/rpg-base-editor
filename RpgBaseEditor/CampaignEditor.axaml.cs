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
using Avalonia.Visuals.Media.Imaging;
using Newtonsoft.Json;
using RpgBaseEditor.Tiles;

namespace RpgBaseEditor
{
    public partial class CampaignEditor : UserControl
    {
        
        public CampaignEditor()
        {
            UpdateComponent();
            DataContext = new CampaignEditorDataContext(this);  
            this.AddHandler(PointerPressedEvent, (DataContext as CampaignEditorDataContext).MouseDownHandler, handledEventsToo: true);     
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

        public CampaignEditorDataContext(CampaignEditor campaignEditor)
        {
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
            {
                var teste = "ok";
            }
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

        //Avalonia
        public TileViewer(CampaignEditor userControl)
        {
            _userControl = userControl;
            GetTiledMap(string.Empty);

            this.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }

        public void GetTiledMap(string selectedMap)
        {
            selectedMap = "Adventure/map.json";
            using StreamReader reader = new StreamReader(selectedMap);
            
            string json = reader.ReadToEnd();
            _tiledMap = JsonConvert.DeserializeObject<TiledMap>(json);

            foreach (var tileset in _tiledMap.tilesets)
            {
                using StreamReader tilesetReader = new StreamReader("Adventure"+tileset.source);
                json = tilesetReader.ReadToEnd();
                var tiledTileset = JsonConvert.DeserializeObject<TiledTileset>(json);

                if (tiledTileset != null)
                {
                    tiledTileset.x_tiles = (int)(tiledTileset.imagewidth / tiledTileset.tilewidth);
                    tiledTileset.y_tiles = (int)(tiledTileset.imageheight / tiledTileset.tileheight);

                    _tiledMap.TiledTilesets.Add((Firstgid: tileset.firstgid, Tileset: tiledTileset));             
                }
            }
            
            foreach (var tileTileset in _tiledMap.TiledTilesets)
            {
                _tiledMap.TiledMapTextures.Add((Firstgid: tileTileset.Firstgid, Texture: new Bitmap("Adventure/"+tileTileset.Tileset.image)));  
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
                        
                        var rec = GetTileRecById(tile_id, _tiledMap);
                        
                        if ((tile & FLIPPED_DIAGONALLY_FLAG) > 0)
                        {
                            rotate = true;
                            rec.Size.WithHeight(-rec.Height);
                        }  

                        if ((tile & FLIPPED_HORIZONTALLY_FLAG) > 0)
                        {
                            rec.Size.WithWidth(-rec.Width);
                        }

                        if ((tile & FLIPPED_VERTICALLY_FLAG) > 0)
                        {
                            rec.Size.WithHeight(-rec.Height);
                        }
                     
                        var source = _tiledMap.TiledMapTextures.LastOrDefault(x => tile_id >= x.Firstgid).Texture;    
                        var resizedRec = new Rect(posVec.X * IMAGE_SCALE, posVec.Y * IMAGE_SCALE, Math.Abs(rec.Width * IMAGE_SCALE), Math.Abs(rec.Height * IMAGE_SCALE));
                        
                        if (rotate)
                            RenderTransform = new RotateTransform(-90f);

                        context.DrawImage(source, rec, resizedRec, new BitmapInterpolationMode());

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
            var tileTileset = tiledMap.TiledTilesets.LastOrDefault(x => tile >= x.Firstgid).Tileset;

            var x_pos = (tile % tileTileset.x_tiles - 1) * tileTileset.tilewidth + tileTileset.margin;
            var y_pos = (int)(Math.Ceiling((decimal)tile / tileTileset.y_tiles) - 1) * tileTileset.tileheight + tileTileset.margin;
            
            var rec = new Rect((int)x_pos, y_pos, tiledMap.tilewidth, tiledMap.tileheight);

            return rec;
        }
        
        private string? IsTileSelectable(uint tile_id)
        {
            var notDrawableTypes = new string[] { "monster", "chest", "player", "door" };

            var tileTileset = _tiledMap.TiledTilesets.LastOrDefault(x => tile_id >= x.Firstgid);
            return tileTileset.Tileset.tiles.FindLast(x => x.id == ((int)tile_id - tileTileset.Firstgid))?.type.ToLowerInvariant();
        }

        public override void Render(DrawingContext context)
        {
            DrawTiledMap(context);
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