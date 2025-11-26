using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DebugTool.Services;

namespace DebugTool.UI.Load.Tabs
{
    /// <summary>
    /// 电源设置 Tab - 800V 电源配置
    /// </summary>
    public class PowerSettingsTab : UserControl
    {
        private DeviceService750_4_60A _service;

        private CheckBox _chk800V;
        private NumericUpDown _numPowerVolt;
        private Button _btnSet;

        // 事件
        public event Action<string> StatusChanged;

        public PowerSettingsTab()
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

            // 电源设置组
            GroupBox grpPower = new GroupBox
            {
                Text = "800V 电源设置",
                Location = new Point(20, 20),
                Size = new Size(400, 200)
            };

            // 启用800V模式复选框
            _chk800V = new CheckBox
            {
                Text = "启用 800V 设备模式",
                Location = new Point(30, 40),
                AutoSize = true
            };
            _chk800V.CheckedChanged += (s, e) =>
            {
                _numPowerVolt.Enabled = _chk800V.Checked;
                _btnSet.Enabled = _chk800V.Checked;
            };

            // 电压标签
            Label lblVoltage = new Label
            {
                Text = "输出电压 (300-800V):",
                Location = new Point(30, 80),
                AutoSize = true
            };

            // 电压输入框
            _numPowerVolt = new NumericUpDown
            {
                Location = new Point(180, 77),
                Width = 80,
                Minimum = 300,
                Maximum = 800,
                DecimalPlaces = 0,
                Value = 400,
                Enabled = false
            };

            // 设置按钮
            _btnSet = CreateButton("设置整机电压", 30, 130, 150, async (s, e) => await SetPowerVoltageAsync());
            _btnSet.Enabled = false;

            grpPower.Controls.AddRange(new Control[]
            {
                _chk800V, lblVoltage, _numPowerVolt, _btnSet
            });

            this.Controls.Add(grpPower);

            // 添加说明文字
            Label lblNote = new Label
            {
                Text = "提示：设置整机电压前请确保设备处于安全状态。\n电压范围：300V - 800V",
                Location = new Point(20, 240),
                Size = new Size(400, 40),
                ForeColor = Color.DimGray
            };
            this.Controls.Add(lblNote);
        }

        private async Task SetPowerVoltageAsync()
        {
            if (_service == null || !_service.IsConnected)
            {
                MessageBox.Show("请先连接设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要将整机电压设置为 {_numPowerVolt.Value}V 吗？\n请确保设备处于安全状态。",
                "确认操作",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                StatusChanged?.Invoke("正在设置电压...");
                await _service.SetOutputVoltageAsync(1, (double)_numPowerVolt.Value);

                StatusChanged?.Invoke($"电压已设置为 {_numPowerVolt.Value}V");
                MessageBox.Show("电压设置成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("电压设置失败");
                MessageBox.Show($"设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
    }
}
