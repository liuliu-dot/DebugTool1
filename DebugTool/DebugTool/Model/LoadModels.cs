using System;
using System.Collections.Generic;

namespace DebugTool.Models
{
    // === 负载设备 (GJDD-750) 数据模型 ===

    public class DeviceRealTimeData
    {
        public List<ChannelRealTimeStatus> Channels { get; set; } = new List<ChannelRealTimeStatus>();
        public InverterStatus Inverter { get; set; } = new InverterStatus();
    }

    public class ChannelRealTimeStatus
    {
        public int ChannelIndex { get; set; }
        public double RealVoltage { get; set; }
        public double RealCurrent { get; set; }
        public double LlcVoltage { get; set; }
        public ushort StatusBits { get; set; }
        public bool IsOnline { get; set; }
    }

    public class InverterStatus
    {
        public byte StatusByte { get; set; }
        public bool IsOverTemp { get; set; }
        public bool IsAdFault { get; set; }
        public int OutputVoltageStatus { get; set; } // 0=欠压, 1=正常, 2=过压
        public bool IsFanFault { get; set; }
        public bool IsTimeout { get; set; }
        public int DcBusVoltageStatus { get; set; } // 0=欠压, 1=正常, 2=过压
        public bool IsChannelOverCurrent { get; set; }

        // ★★★ 补全这两个方法 ★★★
        public bool HasFault()
        {
            return IsOverTemp || IsAdFault || IsFanFault || IsTimeout ||
                   OutputVoltageStatus != 1 || DcBusVoltageStatus != 1;
        }

        public string GetStatusText()
        {
            if (!HasFault()) return "正常运行";
            List<string> errs = new List<string>();
            if (IsOverTemp) errs.Add("过温");
            if (IsAdFault) errs.Add("AD故障");
            if (IsFanFault) errs.Add("风扇");
            if (OutputVoltageStatus != 1) errs.Add("输出电压异常");
            if (DcBusVoltageStatus != 1) errs.Add("母线电压异常");
            return string.Join(", ", errs);
        }
    }

    public enum LoadMode
    {
        CC_Slow = 0,
        CV = 1,
        CP = 2,
        CR = 3,
        CC_Fast = 4
    }

    public class ChannelLoadConfig
    {
        public int ChannelIndex { get; set; }
        public LoadMode Mode { get; set; }
        public double VonVoltage { get; set; }
        public double LoadValue { get; set; }
        public int AdditionalParam { get; set; }

        public ChannelLoadConfig() { }
        public ChannelLoadConfig(int index) { ChannelIndex = index; }
    }
}