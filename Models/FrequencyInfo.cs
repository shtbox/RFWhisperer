namespace SDRSharp.RFWhisperer.Models
{
    public record FrequencyBand(
        long StartHz,
        long EndHz,
        string Name,
        string Service,
        string Description,
        string TypicalModulation,
        string UserLevel  // "beginner", "intermediate", "advanced"
    );

    public record FrequencyBookmark(
        long FrequencyHz,
        string Name,
        string Description,
        string Modulation,
        int BandwidthHz
    );
}
