using UnityEngine;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // A tile that shows the square of a picture under the cell it is painted in: painted over every
    // cell, the tilemap shows the whole picture, and each cell cleared takes its square away. The
    // picture's bottom-left corner sits at cell (0, 0), pixelsPerCell pixels to a cell.
    public class PictureTile : TileBase
    {
        public Texture2D picture;
        [Min(1)] public int pixelsPerCell = 64;
        public Tile.ColliderType colliderType = Tile.ColliderType.Grid;

        // Cut on first use, so they're never saved: [x + y * columns].
        Sprite[] squares;
        int columns, rows;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = SquareAt(position.x, position.y);
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.None;
            tileData.colliderType = colliderType;
        }

        Sprite SquareAt(int x, int y)
        {
            if (picture == null) return null;
            if (squares == null)
            {
                columns = picture.width / pixelsPerCell;
                rows = picture.height / pixelsPerCell;
                squares = new Sprite[columns * rows];
            }
            if (x < 0 || y < 0 || x >= columns || y >= rows) return null;

            ref var square = ref squares[x + y * columns];
            if (square == null)
            {
                var rect = new Rect(x * pixelsPerCell, y * pixelsPerCell, pixelsPerCell, pixelsPerCell);
                square = Sprite.Create(picture, rect, new Vector2(0.5f, 0.5f), pixelsPerCell, 0, SpriteMeshType.FullRect);
                square.name = $"{name} {x},{y}";
                square.hideFlags = HideFlags.DontSave;
            }
            return square;
        }

        // Cut the picture again, after it or the cell size changed.
        public void ClearSquares() => squares = null;

        void OnValidate() => ClearSquares();
    }
}
