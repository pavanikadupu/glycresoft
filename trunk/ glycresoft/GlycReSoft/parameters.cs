using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace GlycreSoft
{
    public partial class parameters : Form
    {
        Form1 mainForm;
        public parameters(Form1 mainForm)
        {
            this.mainForm = mainForm; 
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.mainForm.ABUNDANCE_THRESHOLD = Convert.ToDouble(this.textBox1.Text);
            this.mainForm.NUM_SCANS_THRESHOLD = Convert.ToInt16(this.textBox2.Text);
            this.mainForm.MIN_MW_THRESHOLD = Convert.ToDouble(this.textBox3.Text);
            this.mainForm.MAX_MW_THRESHOLD = Convert.ToDouble(this.textBox4.Text);
            //this.mainForm.NUM_ALLOWED = Convert.ToInt32(this.textBox5.Text);
            double modMass = 0.0;

            if (Double.TryParse(this.textBox5.Text, out modMass)) {
                this.mainForm.inputShiftMass = modMass;
                this.mainForm.inputShiftStr = "";
                this.mainForm.paraInput = true;
                this.Close();
            }

            DataTable residueTable = new DataTable();
            periodicTable pTable = new periodicTable();

            string elementRegex = "([A-Z][a-z]*)([0-9]*)";
            string validateRegex = "^(" + elementRegex + ")+$";
            string formula = Convert.ToString(this.textBox5.Text);
            
            //string modStr = "";

            try
            {

                if (!Regex.IsMatch(formula, validateRegex))
                    throw new FormatException("The Mass Shift is not in a correct format, please input again!");
            }
            catch (FormatException excep) {
                this.Close();
            }

            foreach (Match match in Regex.Matches(formula, elementRegex))
            {
                string name = match.Groups[1].Value;

                int count =
                    match.Groups[2].Value != "" ?
                    int.Parse(match.Groups[2].Value) :
                    1;
                //modStr += name + "_" + Convert.ToString(count);
                modMass += pTable.pTable[name] * count;
            }
            //"#0.00000000"
            this.mainForm.inputShiftMass = Math.Round(modMass, 8);
            this.mainForm.inputShiftStr = textBox5.Text;
            this.mainForm.paraInput = true;
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.mainForm.paraInput = false; 
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            advancedParameters adParameters = new advancedParameters(mainForm);
            adParameters.ShowDialog();
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

    }
}
