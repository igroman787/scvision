using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace scvision
{
    public partial class Form2 : Form
    {
        private Form1 form1;

        public Form2(Form1 form1_input)
        {
            InitializeComponent();
            form1 = form1_input;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Dictionary<string, int> settings = new Dictionary<string, int>();

            settings.Add("opasity", Convert.ToInt32(numericUpDown1.Value));
            settings.Add("nickname", BoolToInt(true));
            settings.Add("kd", BoolToInt(checkedListBox1.GetItemCheckState(0) == CheckState.Checked));
            settings.Add("kda", BoolToInt(checkedListBox1.GetItemCheckState(1) == CheckState.Checked));
            settings.Add("winrate", BoolToInt(checkedListBox1.GetItemCheckState(2) == CheckState.Checked));
            settings.Add("wl", BoolToInt(checkedListBox1.GetItemCheckState(3) == CheckState.Checked));
            settings.Add("dpg", BoolToInt(checkedListBox1.GetItemCheckState(4) == CheckState.Checked));
            settings.Add("hpg", BoolToInt(checkedListBox1.GetItemCheckState(5) == CheckState.Checked));
            form1.SaveSettings(settings);
            this.Close();
        }

        private int BoolToInt(bool value)
        {
            int result = 0;
            if (value == true)
            {
                result = 1;
            }
            return result;
        }
    }
}
