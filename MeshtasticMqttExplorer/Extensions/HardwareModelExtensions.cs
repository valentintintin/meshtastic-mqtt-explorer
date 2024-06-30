using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Extensions;

public static class HardwareModelExtensions
{
    public static string GetImageUrl(this HardwareModel? hardwareModel)
    {
        return hardwareModel switch
        {
            HardwareModel.TloraV2 or HardwareModel.TloraV1 or HardwareModel.TloraV211P6 or HardwareModel.TloraV11P3 or HardwareModel.TloraV211P8 or HardwareModel.TloraT3S3 => "images/hardwares/TLORA_V2_1_1P6.png",
            HardwareModel.Tbeam or HardwareModel.TbeamV0P7 or HardwareModel.LilygoTbeamS3Core => "images/hardwares/TBEAM.png",
            HardwareModel.HeltecV20 or HardwareModel.HeltecV1 or HardwareModel.HeltecV21 or HardwareModel.HeltecV3 => "images/hardwares/HELTEC_V2_0.png",
            HardwareModel.HeltecWslV3 => "images/hardwares/HELTEC_WSL_V3.png",
            HardwareModel.TEcho => "images/hardwares/T_ECHO.png",
            HardwareModel.Rak4631 => "images/hardwares/RAK4631.png",
            HardwareModel.NanoG1Explorer => "images/hardwares/NANO_G1_EXPLORER.png",
            HardwareModel.NanoG2Ultra => "images/hardwares/NANO_G2_ULTRA.png",
            HardwareModel.Rp2040Lora => "images/hardwares/RP2040_LORA.png",
            HardwareModel.RpiPico => "images/hardwares/RPI_PICO.png",
            HardwareModel.HeltecWirelessTracker or HardwareModel.HeltecWirelessTrackerV10 => "images/hardwares/HELTEC_WIRELESS_TRACKER.png",
            HardwareModel.HeltecWirelessPaper or HardwareModel.HeltecWirelessPaperV10 => "images/hardwares/HELTEC_WIRELESS_PAPER.png",
            HardwareModel.TDeck => "images/hardwares/T_DECK.png",
            HardwareModel.TWatchS3 => "images/hardwares/T_WATCH_S3.png",
            _ => "images/hardwares/gray.jpg"
        };
    }
}