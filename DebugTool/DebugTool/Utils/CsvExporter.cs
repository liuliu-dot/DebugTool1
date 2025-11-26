using DebugTool.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DebugTool.Utils
{
    public static class CsvExporter
    {
        /// <summary>
        /// 导出 VDC-32 通道数据
        /// </summary>
        public static void ExportVdc32Data(List<ChannelData> channels)
        {
            if (channels == null || channels.Count == 0)
            {
                MessageBox.Show("当前没有数据可导出！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"VDC32_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("通道,电压(V),状态,恢复时间(ms),阈值(V)");

                    foreach (var ch in channels)
                    {
                        sb.AppendLine($"{ch.Channel},{ch.Voltage:F3},{ch.Status},{ch.RecoveryTime},{ch.Threshold:F2}");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"导出成功！\n路径: {sfd.FileName}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 导出 负载设备 数据
        /// </summary>
        public static void ExportLoadData(DeviceRealTimeData data)
        {
            if (data == null || data.Channels.Count == 0)
            {
                MessageBox.Show("当前没有数据可导出！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"Load_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("通道,电压(V),电流(A),功率(W),状态");

                    foreach (var ch in data.Channels)
                    {
                        double power = ch.RealVoltage * ch.RealCurrent;
                        string status = ch.IsOnline ? "在线" : "离线";
                        sb.AppendLine($"{ch.ChannelIndex},{ch.RealVoltage:F3},{ch.RealCurrent:F3},{power:F2},{status}");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"导出成功！\n路径: {sfd.FileName}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}