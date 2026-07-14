using System.Runtime.Serialization;

namespace CurrentMedia;

public enum ImagePosition
{
    [EnumMember(Value = "none")]
    None,

    [EnumMember(Value = "top-left")]
    TopLeft,

    [EnumMember(Value = "top-right")]
    TopRight,

    [EnumMember(Value = "bottom-left")]
    BottomLeft,

    [EnumMember(Value = "bottom-right")]
    BottomRight,

    [EnumMember(Value = "no-image")]
    NoImage
}

public enum CropMode
{
    [EnumMember(Value = "square")]
    Square,

    [EnumMember(Value = "fit")]
    Fit
}

public enum ActionType
{
    [EnumMember(Value = "toggle")]
    Toggle,

    [EnumMember(Value = "next")]
    Next,

    [EnumMember(Value = "previous")]
    Previous,

    [EnumMember(Value = "forward")]
    Forward,

    [EnumMember(Value = "backward")]
    Backward,

    [EnumMember(Value = "none")]
    None
}

public enum TextDisplayMode
{
    [EnumMember(Value = "both")]
    Both,

    [EnumMember(Value = "title")]
    Title,

    [EnumMember(Value = "artists")]
    Artists,

    [EnumMember(Value = "none")]
    None
}

public enum OverlayDisplayMode
{
    [EnumMember(Value = "none")]
    None,

    [EnumMember(Value = "icon")]
    Icon,

    [EnumMember(Value = "status")]
    Status,

    [EnumMember(Value = "both")]
    Both
}
