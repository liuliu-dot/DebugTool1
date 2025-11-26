using System;
using System.Windows.Forms;

namespace DebugTool
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 确保这里 catch 了异常，这样我们能看到具体的错误信息
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动异常: {ex.Message}\n\n堆栈: {ex.StackTrace}", "错误");
            }
        }
    }
}