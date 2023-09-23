using Godot;

namespace BasicRaytracer.scripts;

public partial class ComputeOutput : TextureRect
{
    
    private bool _bIsInitialised;
    private Vector2I _imageSize;
    
    public void TextureInit(Vector2I imageSize)
    {
        _imageSize = imageSize;
        var image = Image.Create(_imageSize.X, _imageSize.Y, false, Image.Format.Rgbaf);
        var imageTexture = ImageTexture.CreateFromImage(image);
        Texture = imageTexture;
        _bIsInitialised = true;
    }
    
    public void SetData(byte[] data)
    {
        if(!_bIsInitialised) return;
        var image = Image.CreateFromData(_imageSize.X, _imageSize.Y, false, Image.Format.Rgbaf, data);
        ( (ImageTexture) Texture ).Update(image);
    }

}