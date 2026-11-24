namespace BotArena.Persistence.Models;

public sealed class PlacedVob
{
    public int Id { get; set; }
    public string World { get; set; } = "";
    public string Visual { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float Roll { get; set; }
    public float Scale { get; set; } = 1f;
    public bool Collision { get; set; } = true;
    public string PlacedBy { get; set; } = "";
    public string PlacedUid { get; set; } = "";

    public PlacedVob Copy() => (PlacedVob)MemberwiseClone();
}
