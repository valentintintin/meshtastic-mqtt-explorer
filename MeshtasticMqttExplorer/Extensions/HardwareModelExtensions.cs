using Meshtastic.Protobufs;

namespace MeshtasticMqttExplorer.Extensions;

public static class HardwareModelExtensions
{
    public static string GetImageUrl(this HardwareModel hardwareModel)
    {
        return hardwareModel switch
        {
            HardwareModel.TloraV2 or HardwareModel.TloraV1 or HardwareModel.TloraV211P6 or HardwareModel.TloraV11P3 or HardwareModel.TloraV211P8 or HardwareModel.TloraT3S3 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/TLORA_V2_1_1P6.png?raw=true",
            HardwareModel.Tbeam or HardwareModel.TbeamV0P7 or HardwareModel.LilygoTbeamS3Core => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/TBEAM.png?raw=true",
            HardwareModel.HeltecV20 or HardwareModel.HeltecV1 or HardwareModel.HeltecV21 or HardwareModel.HeltecV3 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/HELTEC_V2_0.png?raw=true",
            HardwareModel.HeltecWslV3 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/HELTEC_WSL_V3.png?raw=true",
            HardwareModel.TEcho => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/T_ECHO.png?raw=true",
            HardwareModel.Rak4631 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/RAK4631.png?raw=true",
            HardwareModel.NanoG1Explorer => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/NANO_G1_EXPLORER.png?raw=true",
            HardwareModel.NanoG2Ultra => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/NANO_G2_ULTRA.png?raw=true",
            HardwareModel.Rp2040Lora => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/RP2040_LORA.png?raw=true",
            HardwareModel.RpiPico => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/RPI_PICO.png?raw=true",
            HardwareModel.HeltecWirelessTracker or HardwareModel.HeltecWirelessTrackerV10 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/HELTEC_WIRELESS_TRACKER.png?raw=true",
            HardwareModel.HeltecWirelessPaper or HardwareModel.HeltecWirelessPaperV10 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/HELTEC_WIRELESS_PAPER.png?raw=true",
            HardwareModel.TDeck => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/T_DECK.png?raw=true",
            HardwareModel.TWatchS3 => "https://github.com/liamcottle/meshtastic-map/blob/master/src/public/images/devices/T_WATCH_S3.png?raw=true",
            _ => "gray.jpg"
        };
    }
}