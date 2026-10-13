namespace SvgCreator.Imaging;

/// <summary>An image kept in its encoded form, for embedding unchanged in an SVG.</summary>
internal sealed record EncodedImage(byte[] Data, string MediaType, int Width, int Height)
{
    public string DataUri => $"data:{MediaType};base64,{Convert.ToBase64String(Data)}";
}
