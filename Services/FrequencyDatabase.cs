using SDRSharp.RFWhisperer.Models;

namespace SDRSharp.RFWhisperer.Services
{
    /// <summary>
    /// Lookup table of common frequency allocations to provide Claude with context.
    /// </summary>
    public static class FrequencyDatabase
    {
        private static readonly List<FrequencyBand> Bands = new()
        {
            // AM Broadcast
            new(530_000,      1_710_000,    "AM Broadcast",     "Broadcast",   "AM radio stations (530–1710 kHz)",                  "AM",   "beginner"),
            // Shortwave / HF
            new(1_800_000,    2_000_000,    "160m Amateur",     "Amateur",     "160m amateur radio band",                            "USB/CW","intermediate"),
            new(3_500_000,    4_000_000,    "80m Amateur",      "Amateur",     "80m amateur — regional comms, evening DX",           "USB/LSB","intermediate"),
            new(7_000_000,    7_300_000,    "40m Amateur",      "Amateur",     "40m amateur — reliable DX day and night",            "USB/LSB","intermediate"),
            new(9_400_000,    9_900_000,    "31m Shortwave",    "Broadcast",   "31m international shortwave broadcast band",         "AM",   "beginner"),
            new(14_000_000,   14_350_000,   "20m Amateur",      "Amateur",     "20m amateur — premier HF DX band",                  "USB",  "intermediate"),
            new(21_000_000,   21_450_000,   "15m Amateur",      "Amateur",     "15m amateur — excellent DX during high solar flux",  "USB",  "intermediate"),
            new(28_000_000,   29_700_000,   "10m Amateur",      "Amateur",     "10m amateur — best DX when solar cycle peaks",      "USB/FM","intermediate"),
            // VHF Low
            new(30_000_000,   50_000_000,   "VHF Low",          "Various",     "Public safety, military, paging",                   "NFM",  "intermediate"),
            new(50_000_000,   54_000_000,   "6m Amateur",       "Amateur",     "6m amateur — magic band, sporadic-E DX",            "USB/FM","intermediate"),
            // FM Broadcast
            new(87_500_000,   108_000_000,  "FM Broadcast",     "Broadcast",   "Commercial FM radio (87.5–108 MHz)",                "WFM",  "beginner"),
            // Aviation
            new(108_000_000,  118_000_000,  "VOR/ILS",          "Aviation",    "VHF Omnidirectional Range & Instrument Landing",    "AM",   "intermediate"),
            new(118_000_000,  137_000_000,  "Aviation VHF",     "Aviation",    "Aircraft ATC communications worldwide",              "AM",   "beginner"),
            new(121_500_000,  121_500_000,  "Aviation Emergency","Aviation",   "Emergency guard frequency — 121.5 MHz",             "AM",   "beginner"),
            new(123_450_000,  123_450_000,  "Air-to-Air",       "Aviation",    "Pilot-to-pilot communications",                     "AM",   "intermediate"),
            // NOAA Weather
            new(162_400_000,  162_550_000,  "NOAA Weather",     "Government",  "US NOAA weather broadcasts (162.4–162.55 MHz)",     "WFM",  "beginner"),
            // VHF Marine
            new(156_000_000,  174_000_000,  "Marine VHF",       "Marine",      "Marine communications; Ch16=156.8 MHz distress",   "NFM",  "beginner"),
            new(156_800_000,  156_800_000,  "Marine Distress",  "Marine",      "Channel 16 — international distress/calling",      "NFM",  "beginner"),
            // 2m Amateur
            new(144_000_000,  148_000_000,  "2m Amateur",       "Amateur",     "2m — most popular VHF amateur band, local/regional","FM",   "beginner"),
            new(146_520_000,  146_520_000,  "2m Simplex Call",  "Amateur",     "National 2m FM calling frequency (US)",             "NFM",  "beginner"),
            // APRS
            new(144_390_000,  144_390_000,  "APRS (North Am)",  "Amateur",     "Automatic Packet Reporting System — GPS tracking",  "AFSK", "intermediate"),
            // FRS/GMRS/MURS
            new(151_820_000,  154_600_000,  "MURS",             "Personal",    "Multi-Use Radio Service (US)",                      "NFM",  "beginner"),
            new(462_550_000,  467_725_000,  "FRS/GMRS",         "Personal",    "Family Radio Service / GMRS (US)",                  "NFM",  "beginner"),
            // Public Safety
            new(450_000_000,  470_000_000,  "UHF Public Safety","Government",  "Police, fire, EMS (US P25 digital or FM analog)",   "NFM",  "intermediate"),
            // 70cm Amateur
            new(420_000_000,  450_000_000,  "70cm Amateur",     "Amateur",     "70cm — popular for satellite, ATV, local",          "FM",   "intermediate"),
            new(446_000_000,  446_000_000,  "70cm Simplex Call","Amateur",     "National 70cm FM calling frequency (US)",           "NFM",  "beginner"),
            // ADS-B
            new(1_090_000_000,1_090_000_000,"ADS-B",            "Aviation",    "Aircraft transponders — track planes in real time", "Mode-S","intermediate"),
            // ACARS
            new(129_125_000,  136_900_000,  "ACARS",            "Aviation",    "Aircraft Communications Addressing & Reporting Sys","ACARS","advanced"),
            // ISS/Satellites
            new(145_800_000,  146_000_000,  "ISS Downlink",     "Space",       "International Space Station voice downlink",        "NFM",  "intermediate"),
            new(437_000_000,  438_000_000,  "Amateur Satellites","Space",      "CubeSats, OSCAR satellites, telemetry",             "AFSK/FM","advanced"),
            // 900 MHz ISM / LoRa
            new(902_000_000,  928_000_000,  "ISM 915 MHz",      "ISM",         "Industrial/Scientific/Medical — LoRa, ISM devices", "FSK",  "advanced"),
            // 433 MHz ISM
            new(433_050_000,  434_790_000,  "ISM 433 MHz",      "ISM",         "433 MHz ISM — key fobs, sensors, OOK devices",      "OOK",  "intermediate"),
            // Cellular / LTE (receive only)
            new(700_000_000,  900_000_000,  "Cellular Low",     "Commercial",  "4G/LTE cellular (700/850 MHz bands)",               "LTE",  "advanced"),
            new(1_710_000_000,1_980_000_000,"Cellular Mid",     "Commercial",  "AWS/PCS/UMTS cellular",                             "LTE",  "advanced"),
        };

        /// <summary>Find all bands that contain the given frequency.</summary>
        public static IEnumerable<FrequencyBand> LookupBands(long frequencyHz)
        {
            return Bands.Where(b => frequencyHz >= b.StartHz && frequencyHz <= b.EndHz);
        }

        /// <summary>
        /// Returns a human-readable string describing what's likely on/near a frequency.
        /// Used to enrich Claude's context.
        /// </summary>
        public static string GetFrequencyContext(long frequencyHz)
        {
            var matches = LookupBands(frequencyHz).ToList();
            if (matches.Count == 0)
            {
                // Try nearby ±5%
                long margin = (long)(frequencyHz * 0.05);
                matches = Bands
                    .Where(b => b.EndHz >= frequencyHz - margin && b.StartHz <= frequencyHz + margin)
                    .ToList();

                if (matches.Count == 0)
                    return "No known allocation near this frequency.";
            }

            var lines = matches.Select(b =>
                $"- **{b.Name}** ({b.Service}): {b.Description} | typical mode: {b.TypicalModulation}");
            return string.Join("\n", lines);
        }

        /// <summary>Returns suggested SDR# settings for a given band.</summary>
        public static (string modulation, int bandwidthHz)? GetSuggestedSettings(long frequencyHz)
        {
            var band = LookupBands(frequencyHz).FirstOrDefault();
            if (band == null) return null;

            return band.TypicalModulation switch
            {
                "WFM"  => ("WFM",  200_000),
                "AM"   => ("AM",   10_000),
                "USB"  => ("USB",  3_000),
                "LSB"  => ("LSB",  3_000),
                "NFM"  => ("WFM",  12_500),   // SDR# uses WFM for NFM in some versions
                "FM"   => ("WFM",  12_500),
                "CW"   => ("CW",   500),
                "AFSK" => ("USB",  3_000),
                _      => ("WFM",  25_000)
            };
        }
    }
}
