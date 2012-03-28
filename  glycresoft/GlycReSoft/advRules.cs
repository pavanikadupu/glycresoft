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
    public partial class advRules : Form
    {
        Form1 mainform;
        public advRules(Form1 mainForm)
        {
            this.mainform = mainForm;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
           // this.mainform.dataGridView2.Datasource = getTable();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
