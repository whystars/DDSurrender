// CustomFaction.cs
namespace DDSurrender
{
    public enum CustomFactionType
    {
        DD_Surrendered,
        MTF_ContainmentExpert
    }

    public class CustomFaction
    {
        public CustomFactionType CurrentFaction { get; set; }
    }
}
