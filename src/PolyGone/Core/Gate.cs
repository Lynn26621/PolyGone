using Microsoft.Xna.Framework;
namespace PolyGone;

public class Gate
{
    public Vector2 Position { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsOpen { get; private set; }

    public Gate(Vector2 position, int width, int height)
    {
        Position = position;
        Width = width;
        Height = height;
        IsOpen = false;
    }

    public void Open()
    {
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public Rectangle GetBounds()
    {
        return new Rectangle((int)Position.X, (int)Position.Y, Width, Height);
    }
}