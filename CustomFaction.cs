// CustomFaction.cs
using Exiled.API.Features;
using System;

namespace DDSurrender
{
    public enum CustomFactionType
    {
        DD_Surrendered,
        MTF_ContainmentExpert
    }

    public class CustomFaction
    {
        public Player OriginalRole { get; set; }
        public DateTime SurrenderTime { get; set; }
        public CustomFactionType CurrentFaction { get; set; }
    }
}