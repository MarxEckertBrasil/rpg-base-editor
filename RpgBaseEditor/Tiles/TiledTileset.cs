using System.Collections.Generic;

namespace RpgBaseEditor.Tiles
{
    public class TiledTileset
    {
        public int columns { get; set; }
        public string image { get; set; }
        public int imageheight { get; set; }
        public int imagewidth { get; set; }
        public int margin { get; set; }
        public string name { get; set; }
        public int spacing { get; set; }
        public int tilecount { get; set; }
        public string tiledversion { get; set; }
        public int tileheight { get; set; }
        public List<Tile> tiles { get; set; }
        public int tilewidth { get; set; }
        public string type { get; set; }
        public string version { get; set; }

        public int x_tiles { get; set; }
    }
    
    public class Object
    {
        public int height { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public int rotation { get; set; }
        public string type { get; set; }
        public bool visible { get; set; }
        public int width { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }

    public class Objectgroup
    {
        public string draworder { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public List<Object> objects { get; set; }
        public int opacity { get; set; }
        public string type { get; set; }
        public bool visible { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }

    public class Tile
    {
        public int id { get; set; }
        public Objectgroup objectgroup { get; set; }
        public string type { get; set; }
    }
}