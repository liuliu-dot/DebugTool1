using System;
using System.Drawing;

namespace DebugTool.Models
{
    // === VDC-32 专用模型 ===

    public enum ChannelDropStatus
    {
        OK,
        DROPPED,
        FLAGGED
    }

    public class ChannelData
    {
        public int Channel { get; set; }
        public double Voltage { get; set; }
        public ChannelDropStatus Status { get; set; }
        public ushort RecoveryTime { get; set; }
        public double Threshold { get; set; }

        // 辅助属性：用于UI显示
        public string VoltageText => Status == ChannelDropStatus.OK && Voltage <= 0 ? "N/A" : Voltage.ToString("F3");

        public Color StatusColor
        {
            get
            {
                switch (Status)
                {
                    case ChannelDropStatus.DROPPED: return Color.Red;
                    case ChannelDropStatus.FLAGGED: return Color.Orange;
                    default: return Color.Black; // 正常颜色
                }
            }
        }
    }

    public class IoStatus
    {
        // 输出状态 (true = 低电平/开启)
        public bool Io0OutputLow { get; set; } // PTC
        public bool Io1OutputLow { get; set; } // AC
        public bool Io2OutputLow { get; set; } // PSON
        public bool Io3OutputLow { get; set; } // FAN
        public bool Io4OutputLow { get; set; }
        public bool Io5OutputLow { get; set; }
        public bool Io6OutputLow { get; set; }
        public bool Io7OutputLow { get; set; }

        // 输入/系统状态
        public bool S1Switch { get; set; }
        public bool WaterLeakSelf { get; set; }
        public bool WaterLeakParallel { get; set; }
        public bool JigInPlace { get; set; }
        public bool ContactorSignal { get; set; }
        public bool FanStatus { get; set; }
        public bool AcOnDependsOnJig { get; set; }
    }

    public enum IoCommand : ushort
    {
        PtcOn = 0x0100, PtcOff = 0x0101,
        AcOn = 0x0200, AcOff = 0x0201,
        PsonOn = 0x0400, PsonOff = 0x0401,
        FanOn = 0x0800, FanOff = 0x0801
    }

    public class DataSnapshot
    {
        public DateTime Timestamp { get; set; }
        public ChannelData[] Channels { get; set; }
    }
}