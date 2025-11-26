using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DebugTool.Models;
using DebugTool.Services;

namespace DebugTool.UI.Load.Tabs
{
    /// <summary>
    /// 通道配置 Tab - 单通道配置和批量配置功能
    /// </summary>
    public class ChannelConfigTab : UserControl
    {
        private DeviceService750_4_60A _service;

        // 单通道配置控件
        private ComboBox _cmbTargetCh;
        private ComboBox _cmbMode;
        private NumericUpDown _numVon;
        private NumericUpDown _numVal;
        private NumericUpDown _numDelay;

        // 批量配置控件
        private ComboBox _cmbBatchMode;
        private NumericUpDown _numBatchVon;
        private NumericUpDown _numBatchVal;
        private NumericUpDown _numBatchDelay;

        // 事件
        public event Action<string> StatusChanged;

        public ChannelConfigTab()
        {
            InitializeUI();
        }

        public void SetService(DeviceService750_4_60A service)
        {
            _service = service;
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // === 1. 单通道设置组 ===
            GroupBox grpSingle = new GroupBox
            {
                Text = "单通道负载配置",
                Location = new Point(20, 20),
                Size = new Size(450, 280)
            };

            int y = 30, dy = 40;

            // 通道选择
            AddLabel(grpSingle, "通道:", 20, y);
            _cmbTargetCh = new ComboBox
            {
                Location = new Point(80, y - 3),
                Width = 80,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            for (int i = 1; i <= 8; i++) _cmbTargetCh.Items.Add($"CH{i}");
            _cmbTargetCh.SelectedIndex = 0;

            // 模式选择
            AddLabel(grpSingle, "模式:", 180, y);
            _cmbMode = new ComboBox
            {
                Location = new Point(230, y - 3),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbMode.Items.AddRange(new object[] { "CC 慢速", "CV 恒压", "CP 恒功率", "CR 恒阻", "CC 快速" });
            _cmbMode.SelectedIndex = 0;

            y += dy;

            // Von电压
            AddLabel(grpSingle, "Von(V):", 20, y);
            _numVon = CreateNumericUpDown(80, y - 3, 0, 60, 2, 0);

            // 设定值
            AddLabel(grpSingle, "设定值:", 180, y);
            _numVal = CreateNumericUpDown(240, y - 3, 0, 750, 2, 0);

            y += dy;

            // 延时
            AddLabel(grpSingle, "延时(s):", 20, y);
            _numDelay = CreateNumericUpDown(80, y - 3, 0, 60, 1, 0);

            y += dy + 10;

            // 按钮
            Button btnRead = CreateButton("读取配置", 20, y, 100, async (s, e) => await ReadSingleConfigAsync());
            Button btnApply = CreateButton("应用设置", 130, y, 100, async (s, e) => await ApplyConfigAsync(false));
            Button btnSave = CreateButton("保存配置", 240, y, 100, async (s, e) => await ApplyConfigAsync(true));

            grpSingle.Controls.AddRange(new Control[]
            {
                _cmbTargetCh, _cmbMode, _numVon, _numVal, _numDelay,
                btnRead, btnApply, btnSave
            });
            this.Controls.Add(grpSingle);

            // === 2. 批量设置组 ===
            GroupBox grpBatch = new GroupBox
            {
                Text = "批量配置 (所有8通道)",
                Location = new Point(500, 20),
                Size = new Size(450, 280)
            };

            y = 30;

            // 统一模式
            AddLabel(grpBatch, "统一模式:", 20, y);
            _cmbBatchMode = new ComboBox
            {
                Location = new Point(100, y - 3),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbBatchMode.Items.AddRange(new object[] { "CC 慢速", "CV 恒压", "CP 恒功率", "CR 恒阻", "CC 快速" });
            _cmbBatchMode.SelectedIndex = 0;

            y += dy;

            // Von
            AddLabel(grpBatch, "Von:", 20, y);
            _numBatchVon = CreateNumericUpDown(60, y - 3, 0, 60, 2, 0);

            // 值
            AddLabel(grpBatch, "值:", 160, y);
            _numBatchVal = CreateNumericUpDown(190, y - 3, 0, 750, 2, 0);

            y += dy;

            // 延时
            AddLabel(grpBatch, "延时:", 20, y);
            _numBatchDelay = CreateNumericUpDown(60, y - 3, 0, 60, 1, 0);

            y += dy + 10;

            // 按钮
            Button btnBatchApply = CreateButton("批量应用", 20, y, 120, async (s, e) => await ApplyBatchConfigAsync(false));
            Button btnBatchSave = CreateButton("批量保存", 160, y, 120, async (s, e) => await ApplyBatchConfigAsync(true));

            grpBatch.Controls.AddRange(new Control[]
            {
                _cmbBatchMode, _numBatchVon, _numBatchVal, _numBatchDelay,
                btnBatchApply, btnBatchSave
            });
            this.Controls.Add(grpBatch);
        }

        #region 业务逻辑

        private async Task ReadSingleConfigAsync()
        {
            if (_service == null || !_service.IsConnected)
            {
                MessageBox.Show("请先连接设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                StatusChanged?.Invoke("正在读取配置...");
                var configs = await _service.ReadAllChannelsConfigAsync(1);

                int idx = _cmbTargetCh.SelectedIndex + 1;
                var cfg = configs.FirstOrDefault(c => c.ChannelIndex == idx);

                if (cfg != null)
                {
                    _cmbMode.SelectedIndex = (int)cfg.Mode;
                    _numVon.Value = (decimal)cfg.VonVoltage;
                    _numVal.Value = (decimal)cfg.LoadValue;
                    _numDelay.Value = (decimal)(cfg.AdditionalParam / 10.0);
                    MessageBox.Show("读取成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                StatusChanged?.Invoke("配置读取成功");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("配置读取失败");
                MessageBox.Show($"读取失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ApplyConfigAsync(bool saveToEEPROM)
        {
            if (_service == null || !_service.IsConnected)
            {
                MessageBox.Show("请先连接设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var cfg = new ChannelLoadConfig
                {
                    ChannelIndex = _cmbTargetCh.SelectedIndex + 1,
                    Mode = (LoadMode)_cmbMode.SelectedIndex,
                    VonVoltage = (double)_numVon.Value,
                    LoadValue = (double)_numVal.Value,
                    AdditionalParam = (int)(_numDelay.Value * 10)
                };

                await _service.SetSingleChannelConfigAsync(1, cfg, saveToEEPROM);

                string msg = saveToEEPROM ? "配置已保存到EEPROM" : "配置已应用";
                StatusChanged?.Invoke(msg);
                MessageBox.Show(msg, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ApplyBatchConfigAsync(bool saveToEEPROM)
        {
            if (_service == null || !_service.IsConnected)
            {
                MessageBox.Show("请先连接设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var list = new List<ChannelLoadConfig>();
                for (int i = 1; i <= 8; i++)
                {
                    list.Add(new ChannelLoadConfig
                    {
                        ChannelIndex = i,
                        Mode = (LoadMode)_cmbBatchMode.SelectedIndex,
                        VonVoltage = (double)_numBatchVon.Value,
                        LoadValue = (double)_numBatchVal.Value,
                        AdditionalParam = (int)(_numBatchDelay.Value * 10)
                    });
                }

                await _service.SetAllChannelsConfigAsync(1, list, saveToEEPROM);

                string msg = saveToEEPROM ? "批量配置已保存" : "批量配置已应用";
                StatusChanged?.Invoke(msg);
                MessageBox.Show(msg, "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批量操作失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 辅助方法

        private void AddLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true
            });
        }

        private NumericUpDown CreateNumericUpDown(int x, int y, decimal min, decimal max, int decimals, decimal value)
        {
            return new NumericUpDown
            {
                Location = new Point(x, y),
                Width = 70,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Value = value
            };
        }

        private Button CreateButton(string text, int x, int y, int width, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 28),
                UseVisualStyleBackColor = true
            };
            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        #endregion
    }
}
