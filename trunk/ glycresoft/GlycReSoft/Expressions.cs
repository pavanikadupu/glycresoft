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
    public partial class Expressions : Form
    {
        Form1 mainForm;
        public Expressions(Form1 mainForm)
        {
            this.mainForm = mainForm; 
            InitializeComponent();
        }

        public Expressions()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.mainForm.expr = this.textBox1.Text;
            this.Close();
        }
    }
}
