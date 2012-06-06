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
    public partial class FeatureWeight : Form
    {
        public Boolean checkInput = true;
        public FeatureWeight()
        {
            InitializeComponent();
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            double w;
            checkInput = true;

            if (!Double.TryParse(textBox1.Text, out w)) {
                MessageBox.Show("The weight for avgMonoMW should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox2.Text, out w))
            {
                MessageBox.Show("The weight for NumAdductStates should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox3.Text, out w))
            {
                MessageBox.Show("The weight for NumCharges should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox4.Text, out w))
            {
                MessageBox.Show("The weight for NumScans should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox5.Text, out w))
            {
                MessageBox.Show("The weight for Density should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox6.Text, out w))
            {
                MessageBox.Show("The weight for AvgA:A+2Ratio should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox7.Text, out w))
            {
                MessageBox.Show("The weight for TotalVolume should be a number.");
                checkInput = false;
            }

            if (!Double.TryParse(textBox8.Text, out w))
            {
                MessageBox.Show("The weight for AvgSignalNoise should be a number.");
                checkInput = false;
            }

            

        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
