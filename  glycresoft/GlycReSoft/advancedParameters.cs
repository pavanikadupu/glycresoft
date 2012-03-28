using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GlycreSoft
{
    public partial class advancedParameters : Form
    {
        Form1 mainForm;
        public advancedParameters(Form1 mainForm)
        {
            this.mainForm = mainForm;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.mainForm.MATCH_TOLERANCE = Convert.ToDouble(this.textBox1.Text);
            this.mainForm.GAG_TOLERANCE = Convert.ToDouble(this.textBox2.Text);
            this.mainForm.SHIFT_TOLERANCE = Convert.ToDouble(this.textBox3.Text);
            //this.mainForm.SEARCH_TOLERANCE = Convert.ToDouble(this.textBox4.Text);
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
