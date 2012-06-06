using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Accord.Statistics.Models.Regression;
using Accord.Statistics.Models.Regression.Linear;
using Accord.MachineLearning;
using System.Text.RegularExpressions;

namespace GlycreSoft
{
    public partial class Form1 : Form
    {
        // All tolerances must be non-zero.
        // MATCH_TOLERANCE:     error tolerance for an observed MW to match a theoretical MW
        // GAG_TOLERANCE:       doubled error tolerance for combining GAGs of similar MW
        // SHIFT_TOLERANCE:     error tolerance for grouping GAGs by a mass shift
        // SEARCH_TOLERANCE:    error tolerance to allow when searching for a GAG of known MW

        // ABUNDANCE_THRESHOLD:     minimum abundance of Decon2LS output to allow
        // NUM_SCANS_THRESHOLD:     minimum # of scans covered by a MW to allow
        // MIN_MW_THRESHOLD:        minimum MW to allow
        // MAX_MW_THRESHOLD:        maximum MW to allow

        public double MATCH_TOLERANCE = 5.0;
        public double GAG_TOLERANCE = 40.0 * 2.0;
        public double SHIFT_TOLERANCE = 5.0;
        public double SEARCH_TOLERANCE = 0.5;

        public double ABUNDANCE_THRESHOLD = 1.0;
        public int NUM_SCANS_THRESHOLD = 1;
        public double MIN_MW_THRESHOLD = 500.0;
        public double MAX_MW_THRESHOLD = 3000.0;
        public double inputShiftMass = 17.02655;
        public String inputShiftStr = "NH3";
        //public int NUM_ALLOWED = 100000;

        public int features = 8;

        private FileStream fsInputLCMSFile;
        private SortedList<GAG, double> sUnknownGAG;
        private List<SortedList<GAG, double>> replicateGAG;

        private SortedList<GAG, List<string>> hypList;
        private DataTable data;
        private DataTable matched_data;
        private DataTable logit_data;
        private bool bSum;
        private String massout = "";
        private int num_matches;
        private static ToolTip toolTip;

        public Form1()
        {
            InitializeComponent();

            //This table is used for displaying the composition of groups
            dataGridView1.DataSource = getTable();

            //This table is used to display advanced rules;
            dataGridView2.DataSource = getRuleTable();
            //DataTable compositionTable = getTable();

            toolTip = new ToolTip();
            toolTip.SetToolTip(this.label11, "Click to see details.");
        }

        //The two function
        private static DataTable getRuleTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("Formula", typeof(String));
            table.Columns.Add("Relationship", typeof(String));
            table.Columns.Add("Constrain", typeof(String));

            //add rows 
            table.Rows.Add("A+B+C", "=", "2n");
            return table;

        }


        private static DataTable getTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("MonoSacc", typeof(string));
            table.Columns.Add("C", typeof(int));
            table.Columns.Add("H", typeof(int));
            table.Columns.Add("N", typeof(int));
            table.Columns.Add("O", typeof(int));
            table.Columns.Add("S", typeof(int));
            table.Columns.Add("P", typeof(int));
            //
            // Here we add DataRows.
            //

            table.Rows.Add("Pentose(Xyl)", 5, 8, 0, 4, 0, 0);
            table.Rows.Add("Deoxyhexose (Fuc)", 6, 10, 0, 4, 0, 0);
            table.Rows.Add(Convert.ToChar(916) + "HexA", 6, 6, 0, 5, 0, 0);
            table.Rows.Add("HexN", 6, 11, 1, 4, 0, 0);
            table.Rows.Add("Hexose", 6, 10, 0, 5, 0, 0);
            table.Rows.Add("HexA", 6, 8, 0, 6, 0, 0);
            table.Rows.Add("HexNAc", 8, 13, 1, 5, 0, 0);
            table.Rows.Add("NueAc", 11, 17, 1, 8, 0, 0);
            table.Rows.Add("NueGc", 11, 17, 1, 9, 0, 0);
            table.Rows.Add("Qui4FM", 7, 11, 1, 4, 0, 0);
            table.Rows.Add("GalNAcAn", 8, 12, 2, 5, 0, 0);
            table.Rows.Add("QuiNAc", 8, 13, 1, 4, 0, 0);
            table.Rows.Add("Kdo", 8, 12, 0, 7, 0, 0);
            table.Rows.Add("Ac", 2, 2, 0, 1, 0, 0);
            table.Rows.Add("Phosphate", 0, 1, 0, 3, 0, 1);
            table.Rows.Add("SO3", 0, 0, 0, 3, 1, 0);
            table.Rows.Add("Water", 0, 2, 0, 1, 0, 0);
            return table;
        }

        //Open the file from Decon2LS
        //variable indicate the mode
        public bool batchOpt;
        public bool paraInput;
        string parameterInfo, fileInfo = "";
        private void lCMSDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            parameters paraForm = new parameters(this);
            paraForm.ShowDialog();

            if (!paraInput)
            {
                return;
            }

            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open LCMS Data File";

            //open multiple files
            fDialog.Multiselect = true;
            fDialog.Filter = "CSV Files|*.csv";
            if (fDialog.ShowDialog() == DialogResult.Cancel)
                return;

            replicateGAG = new List<SortedList<GAG, double>>();
            replicateGAG.Clear();
            fileInfo = "";
            foreach (string FileName in fDialog.FileNames)
            {
                try
                {
                    MessageBox.Show("Processing\n" + FileName);
                    this.fsInputLCMSFile = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                    parameterInfo = string.Format("Minimum Abundance:    {0}\n" +
                        "Minimum Number of Scans:    {1}\n" +
                        "Molecular Weight Lower Boundary:    {2}\n" +
                        "Molecular Weight Upper Boundary:    {3}\n" +
                        "Mass Shift:    {4}\n" +
                        "Mass Shift Compound:    {5}\n", ABUNDANCE_THRESHOLD, NUM_SCANS_THRESHOLD,
                        MIN_MW_THRESHOLD, MAX_MW_THRESHOLD, inputShiftMass.ToString("#0.00000000"), inputShiftStr == "" ? "No shift formula inputed" : inputShiftStr);
                    String[] path = FileName.Split('\\');
                    String name = path[path.Count() - 1];
                    fileInfo += String.Format("Data File ---> ...\\{0}\n", name);
                    richTextBox1.Font = new Font("Times New Roman", 12, FontStyle.Regular);
                    richTextBox1.Text = parameterInfo;
                    richTextBox2.Font = new Font("Times New Roman", 12, FontStyle.Underline);
                    richTextBox2.Text = fileInfo;
                    this.parseLCMSRun();

                    //replicateGAG is used to handle the replicates of files. It's a list of sUnknownGAG
                    replicateGAG.Add(this.sUnknownGAG);
                    this.loadHypothesisToolStripMenuItem.Enabled = true;
                    this.toolStripMenuItem1.Enabled = true;
                    this.toolStripMenuItem2.Enabled = true;
                    this.saveOutputAsToolStripMenuItem.Enabled = true;
                    this.loadHypothesisToolStripMenuItem.Enabled = true;
                }
                catch (Exception)
                {
                    MessageBox.Show("Error opening file", "File Error",
                                     MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        //Open the Hypothetical Compound List
        private void hypListMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open Hypothetical List File";
            fDialog.Filter = "CSV Files|*.csv";
            if (fDialog.ShowDialog() == DialogResult.Cancel)
                return;

            int errorIdx = 0;
            hypList = new SortedList<GAG, List<string>>();
            string[] parts;
            string[] parts2;
            string str;
            string key;
            int position;

            hypList.Clear();

            String[] path = fDialog.FileName.Split('\\');
            String name = path[path.Count() - 1];
            fileInfo += String.Format("Hypothesis File ---> ...\\{0}\n", name);
            richTextBox2.Font = new Font("Times New Roman", 12, FontStyle.Underline);
            richTextBox2.Text = fileInfo;
            try
            {
                FileStream fsHypListFile = new FileStream(fDialog.FileName, FileMode.Open, FileAccess.Read);
                StreamReader rdr = new StreamReader(fsHypListFile);
                //int process = 0;
                while (rdr.Peek() >= 0)
                {
                    //string str = rdr.ReadLine();
                    str = rdr.ReadLine();

                    parts = str.Split('"');
                    parts2 = parts[0].Split(',');
                    GAG compound = new GAG(Convert.ToDouble(parts2[1]), MATCH_TOLERANCE);
                    //string key = parts[1];
                    key = parts[1];
                    int idx = hypList.IndexOfKey(compound);
                    if (idx >= 0)
                    {
                        position = 0;
                        foreach (string item in hypList.ElementAt(idx).Value)
                        {
                            position++;
                            if ((this.getSortString(item)).CompareTo(this.getSortString(key)) > 0)
                                break;
                        }
                        hypList.ElementAt(idx).Value.Insert(position, key);
                    }
                    else
                    {
                        List<string> li = new List<String>();
                        li.Add(key);
                        hypList.Add(compound, li);
                    }
                    errorIdx++;
                }
                MessageBox.Show(Convert.ToString(errorIdx) + " hypothetical compounds have been loaded!\n");
                this.saveOutputAsToolStripMenuItem1.Enabled = true;
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening Hypothesis file", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        //Calculate Variance
        private double Variance(List<double> list)
        {
            double avg = list.Average();
            double var = 0.0;
            foreach (double num in list)
            {
                var += Math.Pow((num - avg), 2);
            }
            return var;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Shift is key-value pair, where key is mass shift weight and value is 0 for do not combine, 1 for combine
        private void calMassShiftTargetted(SortedList<double, int> shifts)
        {
            //make sure all the group_flag is with the default value which is -1
            //foreach (var myTargetGAG in this.replicateGAG) {
            //    for (int i = 0; i < myTargetGAG.Count(); i++) {
            //        GAG gag = myTargetGAG.Keys[i];
            //        gag.group_flag = -1;
            //    }
            //}

            firstTime = false; //indicate not the first time for the input, do not need to calculate the shift again
            foreach (var myTargetGAG in this.replicateGAG)
            {
                SortedList<int, List<double>> groupings = new SortedList<int, List<double>>();
                groupings.Clear();
                int group_counter = 1;
                // Loop through all mass shift targets in order
                for (int s = 0; s < shifts.Count(); s++)
                {
                    bool combine = true;
                    if (shifts.Values[s] == 0)
                        combine = false;

                    // For each MW, add the mass shift and see if there is a compound at the calculated MW
                    //for (int i = 0; i < this.sUnknownGAG.Count(); i++)
                    for (int i = 0; i < myTargetGAG.Count(); i++)
                    {
                        //GAG gag1 = this.sUnknownGAG.Keys[i];
                        GAG gag1 = myTargetGAG.Keys[i];
                        GAG gag2 = new GAG(gag1.MolecularWeight + shifts.Keys[s], SHIFT_TOLERANCE);
                        //int idx = this.sUnknownGAG.IndexOfKey(gag2);
                        int idx = myTargetGAG.IndexOfKey(gag2);
                        // If there is a match, mark them as the same group
                        if (idx >= 0)
                        {
                            //gag2 = (GAG)this.sUnknownGAG.ElementAt(idx).Key;
                            gag2 = (GAG)myTargetGAG.ElementAt(idx).Key;
                            if (gag1.group_flag > 0)
                            {
                                gag2.group_flag = gag1.group_flag;
                                groupings[gag1.group_flag].Add(gag2.MolecularWeight);
                            }
                            else
                            {
                                gag1.group_flag = group_counter;
                                groupings.Add(gag1.group_flag, new List<double>());
                                groupings[gag1.group_flag].Add(gag1.MolecularWeight);
                                groupings[gag1.group_flag].Add(gag2.MolecularWeight);
                                gag2.group_flag = group_counter++;
                            }
                        }
                    }

                    // Modify GAG list, grouping MWs based on group_flag
                    foreach (var group in groupings)
                    {
                        // Initialize representative GAG as first MW
                        GAG rep = new GAG(group.Value.First(), SEARCH_TOLERANCE);
                        //int rIdx = this.sUnknownGAG.IndexOfKey(rep);
                        int rIdx = myTargetGAG.IndexOfKey(rep);
                        //rep = this.sUnknownGAG.Keys[rIdx];
                        rep = myTargetGAG.Keys[rIdx];

                        // Select max abundance compound as representative
                        double max_volume = -1.0;
                        foreach (var mw in group.Value)
                        {
                            GAG srch = new GAG(mw, SEARCH_TOLERANCE);
                            //int sIdx = this.sUnknownGAG.IndexOfKey(srch);
                            int sIdx = myTargetGAG.IndexOfKey(srch);
                            if (sIdx >= 0)
                            {
                                //srch = this.sUnknownGAG.Keys[sIdx];
                                srch = myTargetGAG.Keys[sIdx];
                                //if (this.sUnknownGAG.Values[sIdx] > max_volume)
                                if (myTargetGAG.Values[sIdx] > max_volume)
                                {
                                    //max_volume = this.sUnknownGAG.Values[sIdx];
                                    max_volume = myTargetGAG.Values[sIdx];
                                    rep = srch;
                                }
                            }
                        }

                        if (combine)
                        {
                            KeyValuePair<double, int> mod = new KeyValuePair<double, int>(shifts.Keys[s], group.Value.Count);
                            rep.mModStates.Add(mod);
                        }

                        foreach (var mw in group.Value)
                        {
                            if (mw != rep.MolecularWeight)
                            {
                                GAG modified = new GAG(mw, SEARCH_TOLERANCE);
                                //int mIdx = this.sUnknownGAG.IndexOfKey(modified);
                                int mIdx = myTargetGAG.IndexOfKey(modified);
                                if (mIdx < 0)
                                {
                                    Console.Write("WARNING: " + modified.MolecularWeight + " appears to have been deleted already!\n");
                                    continue;
                                }
                                //modified = this.sUnknownGAG.Keys[mIdx];
                                modified = myTargetGAG.Keys[mIdx];
                                if (combine)
                                {
                                    // Scan numbers (elution times)
                                    rep.mElutionTimes.AddRange(modified.mElutionTimes);

                                    // Charge states
                                    foreach (var charge in modified.mChargeState)
                                    {
                                        if (rep.mChargeState.IndexOf(charge) < 0)
                                            rep.mChargeState.Add(charge);
                                    }

                                    // Volume
                                    rep.mVolume.AddRange(modified.mVolume);
                                    //this.sUnknownGAG[rep] += this.sUnknownGAG[modified];
                                    myTargetGAG[rep] += myTargetGAG[modified];
                                    // Signal to Noise
                                    rep.mSignalNoise.AddRange(modified.mSignalNoise);
                                }
                                //this.sUnknownGAG.RemoveAt(mIdx);
                                myTargetGAG.RemoveAt(mIdx);
                            }
                        }
                        if (combine)
                        {
                            // Recompute density
                            int RangeofElution = rep.mElutionTimes.Max() - rep.mElutionTimes.Min() + 1;
                            rep.mDensity = (double)rep.mElutionTimes.Count / (double)RangeofElution;
                        }
                    }
                }
            }
        }

        //Parse the decon2ls output and contruct the GAG objects, UnknownGAG(key is the GAG, value is the total volumes that comprise the GAG
        private bool parseLCMSRun()
        {
            firstTime = true;
            this.sUnknownGAG = new SortedList<GAG, double>();
            try
            {
                StreamReader rdr = new StreamReader(this.fsInputLCMSFile);
                string hdr = rdr.ReadLine();
                this.sUnknownGAG.Clear();

                SortedList<double, List<string[]>> buffer = new SortedList<double, List<string[]>>();
                while (rdr.Peek() >= 0)
                {
                    string str = rdr.ReadLine();
                    string[] parts = str.Split(',');
                    double volume = -1.0 * Convert.ToDouble(parts[10]);
                    double molwt = Convert.ToDouble(parts[6]);
                    if (volume > -1 * ABUNDANCE_THRESHOLD || molwt < MIN_MW_THRESHOLD || molwt > MAX_MW_THRESHOLD)
                        continue;
                    if (buffer.ContainsKey(volume))
                    {
                        buffer[volume].Add(parts);
                    }
                    else
                    {
                        List<string[]> newlist = new List<string[]>();
                        newlist.Add(parts);
                        buffer.Add(volume, newlist);
                    }
                }
                foreach (var val in buffer)
                {
                    foreach (var line in val.Value)
                    {
                        string[] parts = line;
                        GAG g = new GAG(Convert.ToDouble(parts[6]), GAG_TOLERANCE);
                        //int index = this.alLookUpGAGTable.BinarySearch(g);
                        double volume = Convert.ToDouble(parts[10]);
                        int charge = Convert.ToInt16(parts[1]);
                        double fit = Convert.ToDouble(parts[4]);
                        int elutiontime = Convert.ToInt16(parts[0]);
                        double signalnoise = Convert.ToDouble(parts[9]);
                        double plus2regular = Convert.ToDouble(parts[11]) / volume;
                        double width = Convert.ToDouble(parts[8]);

                        int index_unknown = this.sUnknownGAG.IndexOfKey(g);
                        if (index_unknown < 0)
                        {
                            g.mChargeState.Add(charge);
                            g.mFit.Add(fit);
                            g.mElutionTimes.Add(elutiontime);
                            g.mSignalNoise.Add(signalnoise);
                            g.mPlus2Regular.Add(plus2regular);
                            g.mWidth.Add(width);
                            g.mVolume.Add(volume);
                            g.mMW.Add(Convert.ToDouble(parts[6]));
                            g.mAvgMW.Add(Convert.ToDouble(parts[5]));
                            g.mGroup = 0.0;
                            this.sUnknownGAG.Add(g, volume);
                        }
                        else
                        {
                            this.sUnknownGAG[g] = this.sUnknownGAG[g] + volume;
                            if (((GAG)this.sUnknownGAG.Keys[index_unknown]).mChargeState.IndexOf(charge) < 0)
                                ((GAG)this.sUnknownGAG.Keys[index_unknown]).mChargeState.Add(charge);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mFit.Add(fit);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mElutionTimes.Add(elutiontime);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mSignalNoise.Add(signalnoise);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mPlus2Regular.Add(plus2regular);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mWidth.Add(width);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mVolume.Add(volume);
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mMW.Add(Convert.ToDouble(parts[6]));
                            ((GAG)this.sUnknownGAG.Keys[index_unknown]).mAvgMW.Add(Convert.ToDouble(parts[5]));
                        }
                        continue;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error parsing LCMS Run File", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }
        }

        private void sumModToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.bSum = true;
        }



        //write the files that grouped by time
        private void writeFiles(SaveFileDialog dialog, SortedList<GAG, double> myWriteGAG, int reIdx)
        {
            //MessageBox.Show(dialog.FileName);
            StreamWriter unknowns = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_" + Convert.ToString(reIdx) + "_time"));
            //unknowns.WriteLine("AvgMonoMW\tAvgMassDiffAvgMonoMW\tVarAvgMW\tElution Time\tMinError\tAvgFitError\tMaxError\tMinfwhm\tAvgfwhm\tMaxfwhm\tMinPlus2Ratio\t" +
            //"AvgPlus2Ratio\tMaxPlus2Ratio\tVarPlus2Ratio\tMinSignalNoise\tAvgSignalNoise\tTotalSignalNoise\tMaxSignalNoise\tNumCharges\tNumScans\tMinVolume\tAvgVolume\tTotalVolume\tMaxVolume\t");

            unknowns.WriteLine("avgMonoMW\tNumMod\tNumCharges\tNumScans\tDensity\tAvgPlus2Ratio\tTotalVolume\tCentroidScan");

            List<double> bad_scans = new List<double>();
            foreach (DictionaryEntry de in (IDictionary)myWriteGAG)
            {
                double denom = 0.0;
                double num = 0.0;
                for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                {
                    double mw = ((GAG)de.Key).mMW.ElementAt(i);
                    double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                    num += (mw * vol);
                    denom += vol;
                }
                double avgMonoMW = num / denom;
                ((GAG)de.Key).mMolecularWeight = avgMonoMW;
                int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                if (NumScans < NUM_SCANS_THRESHOLD)
                {
                    bad_scans.Add(((GAG)de.Key).mMolecularWeight);
                }
            }
            foreach (var val in bad_scans)
            {
                GAG del = new GAG((double)val, SEARCH_TOLERANCE);
                //this.sUnknownGAG.Remove(del);
                myWriteGAG.Remove(del);
            }
            foreach (DictionaryEntry de in (IDictionary)myWriteGAG)
            {
                double denom = 0.0;
                double num = 0.0;
                for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                {
                    double mw = ((GAG)de.Key).mMW.ElementAt(i);
                    double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                    num += (mw * vol);
                    denom += vol;
                }
                double avgMonoMW = num / denom;
                denom = 0.0;
                num = 0.0;
                for (int i = 0; i < ((GAG)de.Key).mElutionTimes.Count; i++)
                {
                    double scan = (double)((GAG)de.Key).mElutionTimes.ElementAt(i);
                    double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                    num += (scan * vol);
                    denom += vol;
                }
                double CentroidScan = num / denom;
                int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                ((GAG)de.Key).mMolecularWeight = avgMonoMW;
                double avgavgMW = ((GAG)de.Key).mAvgMW.Average();
                int RangeofElution = ((GAG)de.Key).mElutionTimes.Max() - ((GAG)de.Key).mElutionTimes.Min();
                double AvgError = ((GAG)de.Key).mFit.Average();
                double Avgfwhm = ((GAG)de.Key).mWidth.Average();
                double AvgPlus2Ratio = ((GAG)de.Key).mPlus2Regular.Average();
                double AvgSignalNoise = ((GAG)de.Key).mSignalNoise.Average();
                int NumCharges = ((GAG)de.Key).mChargeState.Count();
                double TotalSignalNoise = AvgSignalNoise * NumScans;
                double TotalVolume = (double)de.Value;
                int NumMod = 0;
                for (int mods = 0; mods < ((GAG)de.Key).mModStates.Count(); mods++)
                {
                    NumMod += ((GAG)de.Key).mModStates.ElementAt(mods).Value;
                }
                if (NumMod == 0) NumMod = 1;
                // calculate and set density
                if (((GAG)de.Key).mDensity <= 0.0)
                {
                    double density = ((double)NumScans) / ((double)(RangeofElution + 1));
                    ((GAG)de.Key).mDensity = density;
                }
                double Density = ((GAG)de.Key).mDensity;

                String output = "";
                //int features = 8;
                for (int i = 0; i < features; i++)
                {
                    output += "{";
                    output += i;
                    output += "}";
                    if (i != features - 1)
                        output += "\t";
                }

                unknowns.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, CentroidScan);
            }

            unknowns.Close();
        }

        //Group by time function
        private void unGroupedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;
            int replicateIdx = 1;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foreach (var myWriteGAG in this.replicateGAG)
                {
                    writeFiles(dialog, myWriteGAG, replicateIdx);
                    replicateIdx++;
                }
            }
        }

        Boolean firstTime = true;

        //matrix used to store the weight of each feature
        public double[] featureWeight;
        // Add the unsupervised learning function here
        private void scoreGAGs_unsupervised(object sender, EventArgs e)
        {

            //get the customized weight of features
            FeatureWeight featureWeightForm = new FeatureWeight();
            DialogResult openForm;
            do
            {
                openForm = featureWeightForm.ShowDialog(this);
                if (openForm == DialogResult.OK)
                {
                    if (featureWeightForm.checkInput)
                    {
                        featureWeight = new double[features];
                        featureWeight[0] = Convert.ToDouble(featureWeightForm.textBox1.Text);
                        featureWeight[1] = Convert.ToDouble(featureWeightForm.textBox2.Text);
                        featureWeight[2] = Convert.ToDouble(featureWeightForm.textBox3.Text);
                        featureWeight[3] = Convert.ToDouble(featureWeightForm.textBox4.Text);
                        featureWeight[4] = Convert.ToDouble(featureWeightForm.textBox5.Text);
                        featureWeight[5] = Convert.ToDouble(featureWeightForm.textBox6.Text);
                        featureWeight[6] = Convert.ToDouble(featureWeightForm.textBox7.Text);
                        featureWeight[7] = Convert.ToDouble(featureWeightForm.textBox7.Text);
                    }
                }

                else if (openForm == DialogResult.Cancel)
                {
                    featureWeight = new double[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };
                }


            } while (featureWeightForm.checkInput == false);

            featureWeightForm.Dispose();

            SortedList<double, int> shifts = new SortedList<double, int>();
            //eventually we need the user to add the shift
            //get the inputShiftMass from the parameter table
            shifts.Add(inputShiftMass, 1);

            //Group by shifts for each data in the replicateGAG

            if (firstTime)
            {
                this.calMassShiftTargetted(shifts);
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;
            int replicatesIdx = 1;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foreach (var myTargetGAG in this.replicateGAG)
                {
                    //initiate data structure for curve fitting

                    this.data = new DataTable();
                    this.matched_data = new DataTable();
                    for (int i = 0; i <= features; i++)
                    {
                        DataColumn col = new DataColumn();
                        this.data.Columns.Add("" + i, typeof(double));
                        DataColumn col2 = new DataColumn();
                        this.matched_data.Columns.Add("" + i, typeof(double));
                    }

                    this.num_matches = 0;
                    this.sUnknownGAG = myTargetGAG;
                    StreamWriter unknowns = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "-" + Convert.ToString(replicatesIdx) + "_grouped"));
                    StreamWriter scored = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "-" + Convert.ToString(replicatesIdx) + "_unsupervised"));
                    replicatesIdx++;
                    unknowns.WriteLine("avgMonoMW\tNumAdductStates\tNumCharges\tNumScans\tDensity\tAvgA:A+2Ratio\tTotalVolume\tAvgSignalNoise\tCentroidScan");
                    foreach (DictionaryEntry de in (IDictionary)this.sUnknownGAG)
                    {
                        double denom = 0.0;
                        double num = 0.0;
                        for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                        {
                            double mw = ((GAG)de.Key).mMW.ElementAt(i);
                            double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                            num += (mw * vol);
                            denom += vol;
                        }
                        double avgMonoMW = num / denom;
                        // set it
                        ((GAG)de.Key).mMolecularWeight = avgMonoMW;

                        denom = 0.0;
                        num = 0.0;
                        for (int i = 0; i < ((GAG)de.Key).mElutionTimes.Count; i++)
                        {
                            double scan = (double)((GAG)de.Key).mElutionTimes.ElementAt(i);
                            double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                            num += (scan * vol);
                            denom += vol;
                        }
                        double CentroidScan = num / denom;
                        // set it
                        ((GAG)de.Key).mCentroidScan = CentroidScan;

                        //double avgavgMW = ((GAG)de.Key).mAvgMW.Average();
                        //double AvgError = ((GAG)de.Key).mFit.Average();
                        //double Avgfwhm = ((GAG)de.Key).mWidth.Average();
                        //double TotalSignalNoise = AvgSignalNoise * NumScans;
                        int RangeofElution = ((GAG)de.Key).mElutionTimes.Max() - ((GAG)de.Key).mElutionTimes.Min();
                        double AvgPlus2Ratio = ((GAG)de.Key).mPlus2Regular.Average();
                        double AvgSignalNoise = ((GAG)de.Key).mSignalNoise.Average();
                        int NumCharges = ((GAG)de.Key).mChargeState.Count();
                        int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                        double TotalVolume = (double)de.Value;
                        int NumMod = 0;
                        for (int mods = 0; mods < ((GAG)de.Key).mModStates.Count(); mods++)
                        {
                            NumMod += ((GAG)de.Key).mModStates.ElementAt(mods).Value;
                        }
                        if (NumMod == 0) NumMod = 1;
                        // calculate density
                        if (((GAG)de.Key).mDensity <= 0.0)
                        {
                            double density = ((double)NumScans) / ((double)(RangeofElution + 1));
                            // set density
                            ((GAG)de.Key).mDensity = density;
                        }
                        double Density = ((GAG)de.Key).mDensity;

                        // Since there is no matching to a hypothesis list like the supervised version, 
                        // fit the curves only on points with AvgPlus2Ratios > 0
                        if (AvgPlus2Ratio > 0.0)
                        {
                            this.num_matches++;

                            // build sub-data matrix for curve fitting
                            DataRow r = this.matched_data.NewRow();
                            r[0] = avgMonoMW;
                            r[1] = NumCharges;
                            r[2] = NumMod;
                            r[3] = NumScans;
                            r[4] = Density;
                            r[5] = TotalVolume;
                            r[6] = AvgSignalNoise;
                            r[7] = AvgPlus2Ratio;
                            r[8] = CentroidScan;
                            this.matched_data.Rows.Add(r);
                        }

                        String output = "";
                        //int features = 8;
                        for (int i = 0; i < features; i++) // 24
                        {
                            output += "{";
                            output += i;
                            output += "}";
                            if (i != features - 1)
                                output += "\t";
                        }
                        unknowns.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, AvgSignalNoise, CentroidScan);

                        // build data matrix for scoring
                        DataRow row = this.data.NewRow();
                        row[0] = avgMonoMW;
                        row[1] = NumCharges;
                        row[2] = NumMod;
                        row[3] = NumScans;
                        row[4] = Density;
                        row[5] = TotalVolume;
                        row[6] = AvgSignalNoise;
                        row[7] = AvgPlus2Ratio;
                        if (AvgPlus2Ratio == 0.0)
                            row[7] = 1000000000; // A ratio of 0 means there is no A+2 peak. Setting this value large makes the error much larger.
                        row[8] = CentroidScan;
                        this.data.Rows.Add(row);
                    }

                    // define column indices
                    int col_mw = 0;
                    int col_p2 = 7;
                    int col_cent = 8;

                    // Create a RANSAC algorithm to fit a simple linear regression for plus2ratio
                    int minSamples = 50;
                    var p2ransac = new RANSAC<SimpleLinearRegression>(minSamples);
                    p2ransac.Probability = 0.80;
                    p2ransac.Threshold = 0.25;
                    p2ransac.MaxEvaluations = 2000;

                    DataTable datatable = this.data;
                    if (this.matched_data.Rows.Count > minSamples)
                    {
                        datatable = this.matched_data;
                    }

                    p2ransac.Fitting = // Define a fitting function
                        delegate(int[] sample)
                        {
                            // Retrieve the training data
                            List<double> inputs = new List<double>();
                            List<double> outputs = new List<double>();
                            for (int i = 0; i < sample.Length; i++)
                            {
                                inputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_mw]));
                                outputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_p2]));
                            }

                            // Build a Simple Linear Regression model
                            var r = new SimpleLinearRegression();
                            r.Regress(inputs.ToArray(), outputs.ToArray());
                            return r;
                        };

                    p2ransac.Degenerate = // Define a check for degenerate samples
                        delegate(int[] sample)
                        {
                            // In this case, we will not be performing such checkings.
                            return false;
                        };

                    p2ransac.Distances = // Define a inlier detector function
                        delegate(SimpleLinearRegression r, double threshold)
                        {
                            List<int> inliers = new List<int>();
                            for (int i = 0; i < datatable.Rows.Count; i++)
                            {
                                // Compute error for each point
                                double input = (double)(datatable.Rows[i].ItemArray[col_mw]);
                                double output = (double)(datatable.Rows[i].ItemArray[col_p2]);
                                double error = r.Compute(input) - output;

                                // If the squared error is below the given threshold,
                                //  the point is considered to be an inlier.
                                if (error * error < threshold)
                                    inliers.Add(i);
                            }
                            return inliers.ToArray();
                        };
                    // Finally, try to fit the regression model using RANSAC
                    int[] idx_p2;
                    SimpleLinearRegression rlr_p2 = p2ransac.Compute(datatable.Rows.Count, out idx_p2);

                    // Create a RANSAC algorithm to fit a simple linear regression for centroid scan
                    var centransac = new RANSAC<SimpleLinearRegression>(minSamples);
                    centransac.Probability = 0.90;
                    centransac.Threshold = 500;
                    centransac.MaxEvaluations = 3000;

                    centransac.Fitting = // Define a fitting function
                        delegate(int[] sample)
                        {
                            // Retrieve the training data
                            List<double> inputs = new List<double>();
                            List<double> outputs = new List<double>();
                            for (int i = 0; i < sample.Length; i++)
                            {
                                inputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_mw]));
                                outputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_cent]));
                            }

                            // Build a Simple Linear Regression model
                            var r = new SimpleLinearRegression();
                            r.Regress(inputs.ToArray(), outputs.ToArray());
                            return r;
                        };

                    centransac.Degenerate = // Define a check for degenerate samples
                        delegate(int[] sample)
                        {
                            // In this case, we will not be performing such checkings.
                            return false;
                        };

                    centransac.Distances = // Define a inlier detector function
                        delegate(SimpleLinearRegression r, double threshold)
                        {
                            List<int> inliers = new List<int>();
                            for (int i = 0; i < datatable.Rows.Count; i++)
                            {
                                // Compute error for each point
                                double input = (double)(datatable.Rows[i].ItemArray[col_mw]);
                                double output = (double)(datatable.Rows[i].ItemArray[col_cent]);
                                double error = r.Compute(input) - output;

                                // If the squared error is below the given threshold,
                                //  the point is considered to be an inlier.
                                if (error * error < threshold)
                                    inliers.Add(i);
                            }
                            return inliers.ToArray();
                        };
                    // Finally, try to fit the regression model using RANSAC
                    int[] idx_cent;
                    SimpleLinearRegression rlr_cent = centransac.Compute(datatable.Rows.Count, out idx_cent);

                    for (int j = 0; j < this.data.Rows.Count; j++)
                    {
                        double p2 = (double)this.data.Rows[j].ItemArray[col_p2];
                        double comp_p2 = rlr_p2.Compute((double)this.data.Rows[j].ItemArray[0]);
                        if (p2 < 5000.0)
                            p2 = p2 * 1.0;
                        double cent = (double)this.data.Rows[j].ItemArray[col_cent];
                        double comp_cent = rlr_cent.Compute((double)this.data.Rows[j].ItemArray[0]);
                        this.data.Rows[j].SetField(col_p2, (double)Math.Abs(comp_p2 - p2));
                        this.data.Rows[j].SetField(col_cent, (double)Math.Abs(comp_cent - cent));
                        //this.data.Rows[j].ItemArray[col_cent] = (double)Math.Abs(comp_cent - cent);
                        double diffp2 = (double)this.data.Rows[j].ItemArray[col_p2];
                        double diffcent = (double)this.data.Rows[j].ItemArray[col_cent];

                        GAG search = new GAG((double)this.data.Rows[j].ItemArray[col_mw], SEARCH_TOLERANCE);
                        int idx = this.sUnknownGAG.IndexOfKey(search);
                        if (idx >= 0)
                        {
                            this.sUnknownGAG.Keys[idx].mPlus2Error = (double)this.data.Rows[j].ItemArray[col_p2];
                            this.sUnknownGAG.Keys[idx].mCentroidScanError = (double)this.data.Rows[j].ItemArray[col_cent];
                        }
                    }
                    this.scoreGAGs(scored, null);
                    unknowns.Close();
                    scored.Close();
                }
            }
        }

        //supervised learning here
        private void scoreGAGs_supervised(object sender, EventArgs e)
        {
            SortedList<double, int> shifts = new SortedList<double, int>();
            //eventually we need the user to add the shift
            //get the inputShiftMass from the parameter table
            shifts.Add(inputShiftMass, 1);

            if (firstTime)
            {
                this.calMassShiftTargetted(shifts);
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;
            int replicatesIdx = 1;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foreach (var myTargetGAG in this.replicateGAG)
                {
                    //initiate data structure for curve fitting

                    this.data = new DataTable();
                    this.matched_data = new DataTable();
                    this.logit_data = new DataTable();
                    for (int i = 0; i < 9; i++)
                    {
                        DataColumn col = new DataColumn();
                        this.data.Columns.Add("" + i, typeof(double));
                        DataColumn col2 = new DataColumn();
                        this.matched_data.Columns.Add("" + i, typeof(double));
                        DataColumn col3 = new DataColumn();
                        this.logit_data.Columns.Add("" + i, typeof(double));
                    }

                    this.num_matches = 0;
                    this.sUnknownGAG = myTargetGAG;
                    StreamWriter unknowns = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "-" + Convert.ToString(replicatesIdx) + "_grouped"));
                    StreamWriter scored = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "-" + Convert.ToString(replicatesIdx) + "_unsupervised_comparison"));
                    StreamWriter learned = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "-" + Convert.ToString(replicatesIdx) + "_supervised"));
                    replicatesIdx++;
                    unknowns.WriteLine("avgMonoMW\tNumAdductStates\tNumCharges\tNumScans\tDensity\tAvgA:A+2Ratio\tTotalVolume\tAvgSignalNoise\tCentroidScan");
                    foreach (DictionaryEntry de in (IDictionary)this.sUnknownGAG)
                    {
                        double denom = 0.0;
                        double num = 0.0;
                        for (int i = 0; i < ((GAG)de.Key).mMW.Count; i++)
                        {
                            double mw = ((GAG)de.Key).mMW.ElementAt(i);
                            double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                            num += (mw * vol);
                            denom += vol;
                        }
                        double avgMonoMW = num / denom;
                        // set it
                        ((GAG)de.Key).mMolecularWeight = avgMonoMW;

                        denom = 0.0;
                        num = 0.0;
                        for (int i = 0; i < ((GAG)de.Key).mElutionTimes.Count; i++)
                        {
                            double scan = (double)((GAG)de.Key).mElutionTimes.ElementAt(i);
                            double vol = ((GAG)de.Key).mVolume.ElementAt(i);
                            num += (scan * vol);
                            denom += vol;
                        }
                        double CentroidScan = num / denom;
                        // set it
                        ((GAG)de.Key).mCentroidScan = CentroidScan;

                        //double avgavgMW = ((GAG)de.Key).mAvgMW.Average();
                        //double AvgError = ((GAG)de.Key).mFit.Average();
                        //double Avgfwhm = ((GAG)de.Key).mWidth.Average();
                        //double TotalSignalNoise = AvgSignalNoise * NumScans;
                        int RangeofElution = ((GAG)de.Key).mElutionTimes.Max() - ((GAG)de.Key).mElutionTimes.Min();
                        double AvgPlus2Ratio = ((GAG)de.Key).mPlus2Regular.Average();
                        double AvgSignalNoise = ((GAG)de.Key).mSignalNoise.Average();
                        int NumCharges = ((GAG)de.Key).mChargeState.Count();
                        int NumScans = ((GAG)de.Key).mElutionTimes.Count();
                        double TotalVolume = (double)de.Value;
                        int NumMod = 0;
                        for (int mods = 0; mods < ((GAG)de.Key).mModStates.Count(); mods++)
                        {
                            NumMod += ((GAG)de.Key).mModStates.ElementAt(mods).Value;
                        }
                        if (NumMod == 0) NumMod = 1;
                        // calculate density
                        if (((GAG)de.Key).mDensity <= 0.0)
                        {
                            double density = ((double)NumScans) / ((double)(RangeofElution + 1));
                            // set density
                            ((GAG)de.Key).mDensity = density;
                        }
                        double Density = ((GAG)de.Key).mDensity;

                        GAG match_gag = new GAG(avgMonoMW, MATCH_TOLERANCE);
                        int idx = this.hypList.IndexOfKey(match_gag);
                        ((GAG)de.Key).match_mw = -1.0;
                        ((GAG)de.Key).match_string = "";
                        if (idx >= 0)
                        {
                            this.num_matches++;
                            KeyValuePair<GAG, List<string>> kvp = this.hypList.ElementAt(idx);
                            double hyp_mw = kvp.Key.MolecularWeight;
                            ((GAG)de.Key).match_mw = hyp_mw;
                            foreach (string str in kvp.Value)
                            {
                                ((GAG)de.Key).match_string += (str + ", ");
                            }
                            ((GAG)de.Key).match_string = ((GAG)de.Key).match_string.TrimEnd(',');

                            // build sub-data matrix for curve fitting
                            DataRow r = this.matched_data.NewRow();
                            r[0] = avgMonoMW;
                            r[1] = NumCharges;
                            r[2] = NumMod;
                            r[3] = NumScans;
                            r[4] = Density;
                            r[5] = TotalVolume;
                            r[6] = AvgSignalNoise;
                            r[7] = AvgPlus2Ratio;
                            r[8] = CentroidScan;
                            this.matched_data.Rows.Add(r);
                        }

                        String output = "";
                        //int features = 8;
                        for (int i = 0; i < features; i++) // 24
                        {
                            output += "{";
                            output += i;
                            output += "}";
                            if (i != features - 1)
                                output += "\t";
                        }
                        unknowns.WriteLine(output, avgMonoMW, NumMod, NumCharges, NumScans, Density, AvgPlus2Ratio, TotalVolume, AvgSignalNoise, CentroidScan);

                        // build data matrix for scoring
                        DataRow row = this.data.NewRow();
                        row[0] = avgMonoMW;
                        row[1] = NumCharges;
                        row[2] = NumMod;
                        row[3] = NumScans;
                        row[4] = Density;
                        row[5] = TotalVolume;
                        row[6] = AvgSignalNoise;
                        row[7] = AvgPlus2Ratio;
                        if (AvgPlus2Ratio == 0.0)
                            row[7] = 1000000000; // A ratio of 0 means there is no A+2 peak. Setting this value large makes the error much larger.
                        row[8] = CentroidScan;
                        this.data.Rows.Add(row);
                    }

                    // define column indices
                    int col_mw = 0;
                    int col_p2 = 7;
                    int col_cent = 8;

                    // Create a RANSAC algorithm to fit a simple linear regression for plus2ratio
                    int minSamples = 50;
                    DataTable datatable = this.data;
                    if (this.matched_data.Rows.Count > minSamples)
                    {
                        datatable = this.matched_data;
                    }
                    else
                    {
                        minSamples = 10;
                        if (this.matched_data.Rows.Count > minSamples)
                        {
                            datatable = this.matched_data;
                        }
                        else
                        {
                            MessageBox.Show("WARNING: Fewer than " + minSamples + " compounds were matched. Trying to perform a regression on this few samples will not work. Regression will be attempted on the full data set, however this has been known to fail in the past. If you experience a failure, try using a hypothesis list that matches more data.");
                            minSamples = Convert.ToInt32(Math.Floor((double)this.data.Rows.Count * 0.5));
                        }
                    }
                    var p2ransac = new RANSAC<SimpleLinearRegression>(minSamples);
                    p2ransac.Probability = 0.80;
                    p2ransac.Threshold = 0.25;
                    p2ransac.MaxEvaluations = 2000;

                    p2ransac.Fitting = // Define a fitting function
                        delegate(int[] sample)
                        {
                            // Retrieve the training data
                            List<double> inputs = new List<double>();
                            List<double> outputs = new List<double>();
                            for (int i = 0; i < sample.Length; i++)
                            {
                                inputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_mw]));
                                outputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_p2]));
                            }

                            // Build a Simple Linear Regression model
                            var r = new SimpleLinearRegression();
                            r.Regress(inputs.ToArray(), outputs.ToArray());
                            return r;
                        };

                    p2ransac.Degenerate = // Define a check for degenerate samples
                        delegate(int[] sample)
                        {
                            // In this case, we will not be performing such checkings.
                            return false;
                        };

                    p2ransac.Distances = // Define a inlier detector function
                        delegate(SimpleLinearRegression r, double threshold)
                        {
                            List<int> inliers = new List<int>();
                            for (int i = 0; i < datatable.Rows.Count; i++)
                            {
                                // Compute error for each point
                                double input = (double)(datatable.Rows[i].ItemArray[col_mw]);
                                double output = (double)(datatable.Rows[i].ItemArray[col_p2]);
                                double error = r.Compute(input) - output;

                                // If the squared error is below the given threshold,
                                //  the point is considered to be an inlier.
                                if (error * error < threshold)
                                    inliers.Add(i);
                            }
                            return inliers.ToArray();
                        };
                    // Finally, try to fit the regression model using RANSAC
                    int[] idx_p2;
                    SimpleLinearRegression rlr_p2 = p2ransac.Compute(datatable.Rows.Count, out idx_p2);

                    // Create a RANSAC algorithm to fit a simple linear regression for centroid scan
                    var centransac = new RANSAC<SimpleLinearRegression>(minSamples);
                    centransac.Probability = 0.90;
                    centransac.Threshold = 500;
                    centransac.MaxEvaluations = 3000;

                    centransac.Fitting = // Define a fitting function
                        delegate(int[] sample)
                        {
                            // Retrieve the training data
                            List<double> inputs = new List<double>();
                            List<double> outputs = new List<double>();
                            for (int i = 0; i < sample.Length; i++)
                            {
                                inputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_mw]));
                                outputs.Add((double)(datatable.Rows[sample[i]].ItemArray[col_cent]));
                            }

                            // Build a Simple Linear Regression model
                            var r = new SimpleLinearRegression();
                            r.Regress(inputs.ToArray(), outputs.ToArray());
                            return r;
                        };

                    centransac.Degenerate = // Define a check for degenerate samples
                        delegate(int[] sample)
                        {
                            // In this case, we will not be performing such checkings.
                            return false;
                        };

                    centransac.Distances = // Define a inlier detector function
                        delegate(SimpleLinearRegression r, double threshold)
                        {
                            List<int> inliers = new List<int>();
                            for (int i = 0; i < datatable.Rows.Count; i++)
                            {
                                // Compute error for each point
                                double input = (double)(datatable.Rows[i].ItemArray[col_mw]);
                                double output = (double)(datatable.Rows[i].ItemArray[col_cent]);
                                double error = r.Compute(input) - output;

                                // If the squared error is below the given threshold,
                                //  the point is considered to be an inlier.
                                if (error * error < threshold)
                                    inliers.Add(i);
                            }
                            return inliers.ToArray();
                        };
                    // Finally, try to fit the regression model using RANSAC
                    int[] idx_cent;
                    SimpleLinearRegression rlr_cent = centransac.Compute(datatable.Rows.Count, out idx_cent);

                    for (int j = 0; j < this.data.Rows.Count; j++)
                    {
                        double p2 = (double)this.data.Rows[j].ItemArray[col_p2];
                        double comp_p2 = rlr_p2.Compute((double)this.data.Rows[j].ItemArray[0]);
                        if (p2 < 5000.0)
                            p2 = p2 * 1.0;
                        double cent = (double)this.data.Rows[j].ItemArray[col_cent];
                        double comp_cent = rlr_cent.Compute((double)this.data.Rows[j].ItemArray[0]);
                        this.data.Rows[j].SetField(col_p2, (double)Math.Abs(comp_p2 - p2));
                        this.data.Rows[j].SetField(col_cent, (double)Math.Abs(comp_cent - cent));
                        //this.data.Rows[j].ItemArray[col_cent] = (double)Math.Abs(comp_cent - cent);
                        double diffp2 = (double)this.data.Rows[j].ItemArray[col_p2];
                        double diffcent = (double)this.data.Rows[j].ItemArray[col_cent];

                        GAG search = new GAG((double)this.data.Rows[j].ItemArray[col_mw], SEARCH_TOLERANCE);
                        int idx = this.sUnknownGAG.IndexOfKey(search);
                        if (idx >= 0)
                        {
                            this.sUnknownGAG.Keys[idx].mPlus2Error = (double)this.data.Rows[j].ItemArray[col_p2];
                            this.sUnknownGAG.Keys[idx].mCentroidScanError = (double)this.data.Rows[j].ItemArray[col_cent];
                        }
                    }
                    // For the unsupervised comparison, need to make sure feature weights are set (to all 1s)
                    featureWeight = new double[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 };
                    this.scoreGAGs(scored, learned);
                    unknowns.Close();
                    scored.Close();
                    learned.Close();
                }
            }
        }

        private void toQuanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.txt)|*.txt";
            dialog.ValidateNames = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {

                StreamWriter Quan = new StreamWriter(dialog.FileName.Insert(dialog.FileName.LastIndexOf('.'), "_unknowns"));

                Quan.WriteLine(massout);
                Quan.Close();
            }
        }

        //this is the function that write the output of scored results and machine learning results
        private bool scoreGAGs(StreamWriter sw, StreamWriter swml)
        {
            bool supervised = true;
            if (swml == null)
                supervised = false;

            Dictionary<double, double> scores = new Dictionary<double, double>();
            Dictionary<double, int> index = new Dictionary<double, int>();

            //List<double> outputs_list = new List<double>();

            int max_score = this.data.Rows.Count + 1;

            int N = this.sUnknownGAG.Keys.Count; //number of positive examples to use
            int M = this.data.Columns.Count - 1;

            double[][] inputs = new double[N][];
            for (int x = 0; x < N; x++)
                inputs[x] = new double[M];

            double[] outputs = new double[N];

            int cidx = 0;
            foreach (var item in this.sUnknownGAG)
            {
                GAG g = (GAG)item.Key;
                double mw = g.mMolecularWeight;
                scores[mw] = 0.0;
                index[mw] = cidx;
                if (supervised)
                {
                    if (g.match_mw > 0.0)
                    {
                        outputs[cidx] = 1.0;
                    }
                    else
                    {
                        outputs[cidx] = 0.0;
                    }
                }
                else
                {
                    outputs[cidx] = 1.0; //this doesn't really matter in unsupervised
                }
                cidx++;
            }

            // Calculate unsupervised scoring relative rankings
            for (int i = 1; i < this.data.Columns.Count; i++)
            {
                DataRow[] sortedrows;
                if (i == 7 || i == 8)
                { //column #s for errors
                    sortedrows = this.data.Select("", i + " ASC");
                }
                else
                {
                    sortedrows = this.data.Select("", i + " DESC");
                }
                int cur_pos = 0;
                int cur_val_pos = 0;
                int cur_score = 0;

                foreach (var row in sortedrows)
                {
                    double cur_val = (double)(sortedrows[cur_val_pos].ItemArray[i]);
                    double val = (double)row[i];
                    if (cur_pos == 0 || val != cur_val)
                    {
                        cur_val_pos = cur_pos;
                        cur_score = cur_pos + 1;
                    }

                    scores[(double)row[0]] += featureWeight[i - 1] * Convert.ToDouble(max_score - cur_score);
                    int idx = -1;
                    if (index.ContainsKey((double)row[0]))
                        idx = index[(double)row[0]];

                    if (idx >= 0)
                    {
                        inputs[idx][i - 1] = val;
                    }
                    cur_pos++;
                }
            }
            List<KeyValuePair<double, double>> scoresList = new List<KeyValuePair<double, double>>(scores);
            scoresList.Sort(
                delegate(KeyValuePair<double, double> firstPair,
                KeyValuePair<double, double> nextPair)
                {
                    return nextPair.Value.CompareTo(firstPair.Value);
                }
            );
            sw.WriteLine("Score\tMW\tCompound Key\tPPM Error\tTheoretical MW\tNumAdductStates\tNumCharges\tNumScans\tDensity\tAvgA:A+2Error\tAvgA:A+2Ratio\tTotal Volume\tAvgSignalNoise\tCentroidScanError\tCentroidScan");

            //after printing the header, traverse scoresList, printing relative information retrieved by looking back at sUnknownGAG
            foreach (var val in scoresList)
            {
                GAG lookback = new GAG(val.Key, SEARCH_TOLERANCE);
                int ugidx = this.sUnknownGAG.IndexOfKey(lookback);
                if (ugidx < 0)
                {
                    //this shouldn't happen, but should be handled anyway
                    MessageBox.Show("ERROR1: Couldn't find GAG with MW: " + val.Key);
                    continue;
                }
                lookback = this.sUnknownGAG.Keys[ugidx];
                lookback.score = val.Value;
                string ppm = "";
                if (supervised)
                {
                    if (lookback.match_mw >= 0.0)
                    {
                        double err = ((lookback.MolecularWeight - lookback.match_mw) / lookback.match_mw) * 1000000;
                        ppm = String.Format("{0:0.000}", Math.Abs(err));
                    }
                }
                else
                {
                    ppm = "NA";
                }
                double CentroidScan = lookback.mCentroidScan;
                double CentroidScanError = lookback.mCentroidScanError;
                int RangeofElution = lookback.mElutionTimes.Max() - lookback.mElutionTimes.Min();
                double AvgPlus2Ratio = lookback.mPlus2Regular.Average();
                double Plus2Error = lookback.mPlus2Error;
                double AvgSignalNoise = lookback.mSignalNoise.Average();
                int NumCharges = lookback.mChargeState.Count();
                int NumScans = lookback.mElutionTimes.Count();
                double TotalVolume = lookback.mVolume.Sum();
                int NumMod = 0;
                for (int mods = 0; mods < lookback.mModStates.Count(); mods++)
                {
                    NumMod += lookback.mModStates.ElementAt(mods).Value;
                }
                if (NumMod == 0) NumMod = 1;
                // calculate and set density

                double Density = lookback.mDensity;

                String output = "";
                int display_cols = 15;
                for (int i = 0; i < display_cols; i++)
                {
                    output += "{";
                    output += i;
                    output += "}";
                    if (i != display_cols - 1)
                        output += "\t";
                }
                string avgMonoMW = String.Format("{0:0.000000}", lookback.mMolecularWeight);
                string Hyp_MW = "";
                if (supervised)
                {
                    if (lookback.match_mw > 0.0)
                        Hyp_MW = String.Format("{0:0.000000}", lookback.match_mw);
                }
                else
                {
                    Hyp_MW = "NA";
                }
                string Match = lookback.match_string;
                double weights_summation = 0.0;
                foreach (var w in featureWeight)
                    weights_summation += w;
                double Score = val.Value / (double)(max_score * weights_summation);
                double avgSignalNoise = lookback.mSignalNoise.Average();
                sw.WriteLine(output, String.Format("{0:0.0000}", Score), avgMonoMW, Match, ppm, Hyp_MW, NumMod, NumCharges, NumScans, Density, String.Format("{0:0.0000}", Plus2Error), AvgPlus2Ratio, TotalVolume, avgSignalNoise, String.Format("{0:0.0000}", CentroidScanError), CentroidScan);
            }

            if (!supervised)
                return true;

            // If here, then it is supervised learning. Do logistic regression.
            swml.WriteLine("Score\tMW\tCompound Key\tPPM Error\tTheoretical MW\tNumAdductStates\tNumCharges\tNumScans\tDensity\tAvgA:A+2Error\tAvgA:A+2Ratio\tTotal Volume\tAvgSignalNoise\tCentroidScanError\tCentroidScan");
            LogisticRegression logit = new LogisticRegression(M);
            logit.Regress(inputs, outputs);
            Dictionary<double, double> regressed = new Dictionary<double, double>();
            foreach (DataRow row in this.data.Rows)
            {
                double[] test = new double[M];
                for (int c = 1; c < this.data.Columns.Count; c++)
                {
                    test[c - 1] = (double)row[c];
                }
                regressed[(double)row[0]] = logit.Compute(test);
            }

            List<KeyValuePair<double, double>> regList = new List<KeyValuePair<double, double>>(regressed);
            regList.Sort(
                delegate(KeyValuePair<double, double> firstPair,
                KeyValuePair<double, double> nextPair)
                {
                    return nextPair.Value.CompareTo(firstPair.Value);
                }
            );

            foreach (var val in regList)
            {
                GAG lookback = new GAG(val.Key, SEARCH_TOLERANCE);
                int ugidx = this.sUnknownGAG.IndexOfKey(lookback);
                if (ugidx < 0)
                {
                    //this shouldn't happen, but should be handled anyway
                    MessageBox.Show("ERROR2: Couldn't find GAG with MW: " + val.Key);
                    continue;
                }
                lookback = this.sUnknownGAG.Keys[ugidx];
                lookback.regressed_score = val.Value;
                string ppm = "";
                if (lookback.match_mw >= 0.0)
                {
                    double err = ((lookback.MolecularWeight - lookback.match_mw) / lookback.match_mw) * 1000000;
                    ppm = String.Format("{0:0.000}", Math.Abs(err));
                }
                double CentroidScan = lookback.mCentroidScan;
                double CentroidScanError = lookback.mCentroidScanError;
                int RangeofElution = lookback.mElutionTimes.Max() - lookback.mElutionTimes.Min();
                double AvgPlus2Ratio = lookback.mPlus2Regular.Average();
                double Plus2Error = lookback.mPlus2Error;
                double AvgSignalNoise = lookback.mSignalNoise.Average();
                int NumCharges = lookback.mChargeState.Count();
                int NumScans = lookback.mElutionTimes.Count();
                double TotalVolume = lookback.mVolume.Sum();
                int NumMod = 0;
                for (int mods = 0; mods < lookback.mModStates.Count(); mods++)
                {
                    NumMod += lookback.mModStates.ElementAt(mods).Value;
                }
                if (NumMod == 0) NumMod = 1;
                // calculate and set density

                double Density = lookback.mDensity;

                String output = "";
                int display_cols = 15; // 24
                for (int i = 0; i < display_cols; i++)
                {
                    output += "{";
                    output += i;
                    output += "}";
                    if (i != display_cols - 1)
                        output += "\t";
                }
                string avgMonoMW = String.Format("{0:0.000000}", lookback.mMolecularWeight);
                string Hyp_MW = "";
                if (lookback.match_mw > 0.0)
                    Hyp_MW = String.Format("{0:0.000000}", lookback.match_mw);
                string Match = lookback.match_string;
                double avgSignalNoise = lookback.mSignalNoise.Average();
                swml.WriteLine(output, String.Format("{0:0.0000}", lookback.regressed_score), avgMonoMW, Match, ppm, Hyp_MW, NumMod, NumCharges, NumScans, Density, String.Format("{0:0.0000}", Plus2Error), AvgPlus2Ratio, TotalVolume, avgSignalNoise, String.Format("{0:0.0000}", CentroidScanError), CentroidScan);
            }
            string coef = "Intercept\tNumCharges\tNumMod\tNumScans\tDensity\tTotalVolume\tAvgSignalNoise\tAvgPlus2Ratio\tCentroidScan";
            string vals = "";
            for (int j = 0; j < logit.Coefficients.Count(); j++)
            {
                vals += logit.Coefficients[j] + "\t";
            }
            MessageBox.Show(coef + "\n" + vals);

            return true;
        }



        /// <summary>
        /// the part below is for compound list generator
        /// </summary>

        private Hashtable compGroup = new Hashtable();
        private Hashtable compMass = new Hashtable();
        private Hashtable modGroup = new Hashtable();
        private Hashtable modMass = new Hashtable();

        //This two variables are used to maintain the right sequence of the input group.
        private String[] compIndList = new String[5];
        private String[] modIndList = new String[1];

        private void groupMassCal(Object g1, Object g2, Object g3, Object g4, Object g5, TextBox m1, TextBox m2)
        {
            DataTable residueTable = new DataTable();
            periodicTable pTable = new periodicTable();
            string[] groups = new string[] { g1.ToString(), g2.ToString(), g3.ToString(), g4.ToString(), g5.ToString() };
            string[] elements = new string[] { "C", "H", "N", "O", "S", "P" };
            int listInd = 0;

            residueTable = (DataTable)dataGridView1.DataSource;
            compMass.Clear();
            foreach (var group in groups)
            {
                DataRow[] selectedRow = residueTable.Select(string.Format("{0} LIKE '{1}'", "MonoSacc", group));
                double mass = 0.0;
                string groupStr = "";

                for (int i = 0; i < elements.Count(); i++)
                {
                    double eMass = pTable.pTable[elements[i]];
                    int eNum = Convert.ToInt16(selectedRow[0].ItemArray[i + 1]);
                    if (i != 0)
                    {
                        groupStr += "_";
                    }
                    groupStr += elements[i] + "_" + Convert.ToString(eNum);
                    mass += eMass * eNum;
                }
                try
                {
                    compMass.Add(groupStr, mass);
                    compIndList[listInd++] = groupStr;
                }
                catch (ArgumentException e) {
                    MessageBox.Show("Duplicate residues are selected, please check your selection!");
                    return;
                }
                //MessageBox.Show(Convert.ToString(mass));
            }

            //parse modification string 
            string elementRegex = "([A-Z][a-z]*)([0-9]*)";
            string validateRegex = "^(" + elementRegex + ")+$";
            string formula = Convert.ToString(m1.Text);
            string formulaSub = Convert.ToString(m2.Text);
            //MessageBox.Show(formula + "\n");
            if (!Regex.IsMatch(formula, validateRegex))
                throw new FormatException("Input string was in an incorrect format.");

            string modStr = "";
            double modMass = 0.0;
            listInd = 0;
            foreach (Match match in Regex.Matches(formula, elementRegex))
            {
                string name = match.Groups[1].Value;

                int count =
                    match.Groups[2].Value != "" ?
                    int.Parse(match.Groups[2].Value) :
                    1;
                modStr += name + "_" + Convert.ToString(count);
                modMass += pTable.pTable[name] * count;
                //MessageBox.Show(Convert.ToString(name) + ":" + Convert.ToString(count) + "\n");
            }
            this.modMass[modStr] = modMass;
            if (formulaSub != "")
            {

                string subStr = "";
                double subMass = 0.0;
                foreach (Match match in Regex.Matches(formulaSub, elementRegex))
                {
                    string name = match.Groups[1].Value;

                    int count =
                        match.Groups[2].Value != "" ?
                        int.Parse(match.Groups[2].Value) :
                        1;
                    subStr += name + "_" + Convert.ToString(count);
                    subMass += pTable.pTable[name] * count;
                    //MessageBox.Show(Convert.ToString(name) + ":" + Convert.ToString(count) + "\n");
                }
                this.modMass[modStr] = modMass - subMass;
            }
            modIndList[listInd++] = modStr;
            MessageBox.Show(String.Format("The modification is: {0}\n" + "Mass of modification is: {1:0.00000000}.\n", modStr, this.modMass[modStr]));
            //parameterInfo += String.Format("Modification:    {0}\n", modStr.Replace("_",""));

            //richTextBox1.Font = new Font("Times New Roman", 12, FontStyle.Regular);
            //richTextBox1.Text = parameterInfo;
            //        compMass
            //        modMass
            //        compIndList
            //        modIndList
        }

        ///new compound generator based directly on the expression 
        ///new generator
        private void compoundListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.csv)|*.csv";
            dialog.ValidateNames = true;
            this.compoundListToolStripMenuItem.Enabled = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StreamWriter listFile = new StreamWriter(dialog.FileName);
                //generate composition list
                List<List<int>> compList = new List<List<int>>();
                List<List<int>> modList = new List<List<int>>();
                int min, max;
                string[] symArray = new string[] { "A", "B", "C", "D", "E" };
                double massWater = 0.0;
                periodicTable massTable = new periodicTable();
                massWater = 2 * massTable.pTable["H"] + massTable.pTable["O"];

                foreach (String compIndGroup in compIndList)
                {
                    if (compList.Count() == 0)
                    {
                        try
                        {
                            min = int.Parse(exprs[0]);
                            max = int.Parse(exprs[1]);
                        }
                        catch (Exception) { 
                            MessageBox.Show(String.Format("There is a format error for the boundaries of {0}, please input the right boundary!", symArray[0]));
                            listFile.Close();
                            return;
                        }
                        if (min > max) { 
                            MessageBox.Show(String.Format("There is an error for the boundaries of {0}, please try again!",symArray[0]));
                            listFile.Close();
                            return;
                        }

                        for (int i = min; i <= max; i++)
                        {
                            List<int> temp = new List<int>();
                            temp.Add(i);
                            compList.Add(temp);
                        }
                    }

                    else
                    {
                        List<List<int>> tempList = new List<List<int>>();
                        for (int j = 0; j < compList.Count(); j++)
                        {
                            //here parse the expression for the j-th

                            //get the expression index
                            int position = compList[j].Count();
                            int parseStartInd = 2 * position;
                            string exprLow = exprs[parseStartInd], exprHigh = exprs[parseStartInd + 1];
                            //symVal = new SortedList<string, int>();
                            symVal.Clear();
                            for (int ind = 0; ind < compList[j].Count(); ind++)
                            {
                                string symbol = symArray[ind];
                                int value = compList[j][ind];
                                symVal.Add(symbol, value);
                            }

                            try{
                                min = Convert.ToInt16(mathParse(exprLow));
                                max = Convert.ToInt16(mathParse(exprHigh));
                            }
                            catch (Exception) { 
                                MessageBox.Show(String.Format("There is a format error for the boundaries of {0}, please input the right boundary!", symArray[position]));
                                listFile.Close();
                                return;
                            }
                            if (min < 0) { min = 0; }

                            //MessageBox.Show(Convert.ToString(min) + "\t" + Convert.ToString(max));

                            for (int i = min; i <= max; i++)
                            {
                                List<int> temp = new List<int>();
                                foreach (var n in compList[j])
                                {
                                    temp.Add(n);
                                }
                                temp.Add(i);
                                tempList.Add(temp);
                            }
                            //compList.Clear();
                            //compList = tempList;
                        }

                        int parseCompInd = compList[0].Count();
                        if (tempList.Count == 0) {
                            MessageBox.Show(String.Format("There is an error for the boundaries of {0}, please try again!", symArray[parseCompInd]));
                            listFile.Close();
                            return;
                        }
                        compList = tempList;
                    }

                }

                //generate modification list
                List<List<int>> tempAll = new List<List<int>>();

                //only print out the error msg the first time getting problem parsing it
                Boolean errorAdvRuleShowed = false;
                for (int j = 0; j < compList.Count(); j++)
                {
                    //here parse the expression for the modification

                    //get the expression index
                    string exprLow = exprs[10], exprHigh = exprs[11];
                    //symVal = new SortedList<string, int>();
                    symVal.Clear();
                    for (int ind = 0; ind < compList[j].Count(); ind++)
                    {
                        string symbol = symArray[ind];
                        int value = compList[j][ind];
                        symVal.Add(symbol, value);
                    }

                    try
                    {
                        min = Convert.ToInt16(mathParse(exprLow));
                        max = Convert.ToInt16(mathParse(exprHigh));
                    }catch (Exception) { 
                            MessageBox.Show(String.Format("There is a format error for the boundaries of modification, please input the right boundary!"));
                            listFile.Close();
                            return;
                    }

                    DataTable ruleTable = new DataTable();
                    int checkRule = 1;
                    ruleTable = (DataTable)dataGridView2.DataSource;

                    if (ruleTable.Rows.Count != 0)
                    {
                        foreach (DataRow row in ruleTable.Rows)
                        {
                            String rule, relationship;
                            int constrain;

                            //check whether the rule table is empty
                            if (ruleTable.Rows[0].IsNull(0) || ruleTable.Rows[0].IsNull(1) || ruleTable.Rows[0].IsNull(2))
                            {
                                break;
                            }

                            rule = (String)row.ItemArray[0];
                            //renew symVal
                            symVal.Clear();
                            for (int ind = 0; ind < symArray.Count(); ind++)
                            {
                                symVal.Add(symArray[ind], compList[j][ind]);
                            }
                            try
                            {
                                double num = mathParse(rule);
                                relationship = (String)row.ItemArray[1];

                                if (relationship != "=" && relationship != "<" && relationship != ">" && relationship != ">=" && relationship != "<=") {
                                    MessageBox.Show("The format of the relationship is wrong, generator terminated!");
                                    listFile.Close();
                                    return;
                                }

                                bool res = int.TryParse((String)row.ItemArray[2], out constrain);

                                if (Double.IsNaN(num))
                                {
                                    MessageBox.Show("The format of the other rule is wrong, generator terminated!");
                                    listFile.Close();
                                    return;
                                }

                                if (!res)
                                {
                                    if ((String)row.ItemArray[2] == "2n")
                                    {
                                        if (num % 2 == 0)
                                        {
                                            checkRule *= 1;
                                        }
                                        else
                                        {
                                            checkRule *= 0;
                                        }
                                    }
                                    else if ((String)row.ItemArray[2] == "2n+1" || (String)row.ItemArray[2] == "2n-1")
                                    {
                                        if (num % 2 != 0)
                                        {
                                            checkRule *= 1;
                                        }
                                        else
                                        {
                                            checkRule *= 0;
                                        }
                                    }
                                    else
                                    {
                                        if (!errorAdvRuleShowed)
                                        {
                                            MessageBox.Show("The constrain in other rules can not be recognized, thus be ingnored! \nDouble click on \"Other Rules\" to see help!");
                                            errorAdvRuleShowed = true;
                                        }
                                    }
                                }

                                else
                                {
                                    //will develop later if user input a number here.
                                    if ((String)row.ItemArray[1] == "=")
                                    {
                                        checkRule *= num == constrain ? 1 : 0;
                                    }
                                    else if ((String)row.ItemArray[1] == ">=" || (String)row.ItemArray[1] == "<=") {
                                        Boolean check = (String)row.ItemArray[1] == ">=" ? num >= constrain : num <= constrain;
                                        checkRule *= check ? 1 : 0;
                                    }
                                    else if ((String)row.ItemArray[1] == ">" || (String)row.ItemArray[1] == "<")
                                    {
                                        Boolean check = (String)row.ItemArray[1] == ">" ? num > constrain : num < constrain;
                                        checkRule *= check ? 1 : 0;
                                    }
                                    else
                                    {
                                        if (!errorAdvRuleShowed)
                                        {
                                            MessageBox.Show("The relationship in other rules can not be recognized, generator terminated.");
                                            listFile.Close();
                                            return;
                                        }
                                    }

                                }
                            }
                            catch (Exception) {
                                MessageBox.Show("Can not recognize the advanced rules, please double check the format!");
                                listFile.Close();
                                return;
                            }


                        }
                    }

                    if (min < 0) { min = 0; }
                    if (checkRule == 1)
                    {
                        for (int i = min; i <= max; i++)
                        {
                            List<int> temp = new List<int>();
                            foreach (var n in compList[j])
                            {
                                temp.Add(n);
                            }
                            temp.Add(i);
                            temp.Add(0);
                            temp.Add(0);
                            temp.Add(0);
                            tempAll.Add(temp);
                        }
                    }
                }

                int parseModInd = compList[0].Count();
                if (tempAll.Count == 0)
                {
                    MessageBox.Show(String.Format("There is an error for the boundaries of modification, please try again!"));
                    listFile.Close();
                    return;
                }
                compList = tempAll;

                //combine the compList and the modList and calculate the MW

                //C H N O S P
                //DataTable residueTable = getTable();
                DataTable residueTable = (DataTable)dataGridView1.DataSource;

                Char[] elemList = new Char[6] { 'C', 'H', 'N', 'O', 'S', 'P' };
                foreach (var comp in compList)
                {
                    int checkSum = 0;
                    double mass = 0.0;
                    String formulaStr = "";
                    String bracketRep = "";
                    int[] formulaCof = new int[6] { 0, 0, 0, 0, 0, 0 };
                    //assemble fomula
                    for (int i = 0; i < comp.Count() - 4; i++)
                    {
                        String groupStr = compIndList[i];
                        double groupMass = Convert.ToDouble(compMass[groupStr]);
                        int cof = comp[i];

                        if (i == 0)
                        {
                            bracketRep += "[" + Convert.ToString(cof);
                        }

                        else
                        {
                            bracketRep += "," + Convert.ToString(cof);
                        }

                        string[] groupInfo = groupStr.Split('_');
                        for (int ind = 0; ind < formulaCof.Count(); ind++)
                        {
                            formulaCof[ind] += cof * int.Parse(groupInfo[2 * ind + 1]);
                        }
                        //formulaStr += "(" + groupStr + ")" + Convert.ToString(cof);

                        mass += groupMass * cof;
                        checkSum += cof;
                    }

                    for (int ind = 0; ind < formulaCof.Count(); ind++)
                    {
                        if (ind == 1) { formulaCof[ind] = formulaCof[ind] + 2; }
                        if (ind == 3) { formulaCof[ind] = formulaCof[ind] + 1; }
                        formulaStr += elemList[ind] + Convert.ToString(formulaCof[ind]);
                    }

                    double modmass = 0.0;
                    String modStr = "";
                    String bracket2 = "";

                    modStr += "(" + modIndList[0].Replace("_", "") + ")" + Convert.ToString(comp[5]);
                    modmass += Convert.ToDouble(this.modMass[modIndList[0]]) * comp[5];
                    bracket2 = "]-[" + Convert.ToString(comp[5]) + ",0," + "0," + "0" + "]";
                    if (checkSum != 0)
                    {
                        double massAdj = mass + modmass + massWater;
                        //double massAdj = mass + modmass;
                        listFile.WriteLine(formulaStr + modStr + "," + "{0}" + "," + "\"" + bracketRep + bracket2 + "\"", massAdj.ToString("#0.00000000"));
                    }

                }
                listFile.Close();
            }
        }

        //this function is used to generate the compound list        
        private void old_compoundListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Tab Separated Value (*.csv)|*.csv";
            dialog.ValidateNames = true;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                StreamWriter listFile = new StreamWriter(dialog.FileName);
                //generate composition list
                List<List<int>> compList = new List<List<int>>();
                List<List<int>> modList = new List<List<int>>();
                foreach (string compIndGroup in compIndList)
                {
                    List<int> bound = (List<int>)compGroup[compIndGroup];
                    int min, max;
                    min = bound[0];
                    max = bound[1];

                    if (compList.Count() == 0)
                    {
                        for (int i = min; i <= max; i++)
                        {
                            List<int> temp = new List<int>();
                            temp.Add(i);
                            compList.Add(temp);
                        }
                    }

                    else
                    {
                        List<List<int>> tempList = new List<List<int>>();

                        for (int j = 0; j < compList.Count(); j++)
                        {
                            for (int i = min; i <= max; i++)
                            {
                                List<int> temp = new List<int>();
                                foreach (var n in compList[j])
                                {
                                    temp.Add(n);
                                }
                                temp.Add(i);
                                tempList.Add(temp);
                            }
                        }
                        compList = tempList;
                    }

                }
                //MessageBox.Show(Convert.ToString(compList.Count()));
                //generate modification list
                foreach (string modIndGroup in modIndList)
                {
                    List<int> bound = (List<int>)modGroup[modIndGroup];
                    int min, max;
                    min = bound[0];
                    max = bound[1];

                    if (modList.Count() == 0)
                    {
                        for (int i = min; i <= max; i++)
                        {
                            List<int> temp = new List<int>();
                            temp.Add(i);
                            modList.Add(temp);
                        }
                    }

                    else
                    {
                        List<List<int>> tempList = new List<List<int>>();

                        for (int j = 0; j < modList.Count(); j++)
                        {
                            for (int i = min; i <= max; i++)
                            {
                                List<int> temp = new List<int>();
                                foreach (var n in modList[j])
                                {
                                    temp.Add(n);
                                }
                                temp.Add(i);
                                tempList.Add(temp);
                            }
                        }
                        modList = tempList;
                    }

                }
                //MessageBox.Show(Convert.ToString(modList.Count()));

                //now combine the compList and the modList
                //also here to check whether the compound permutation fit the algbra 
                List<string> compKey = new List<string>();
                foreach (var numList in compList)
                {
                    int idx = 0;
                    string formular1 = "";
                    string fstr = "[";
                    double compMW = 0.0;
                    foreach (var num in numList)
                    {
                        try
                        {
                            symVal.Add(compSymList[idx], num);
                        }
                        catch
                        {
                            symVal[compSymList[idx]] = num;
                        }
                        string groupKey = Convert.ToString(compIndList[idx++]);
                        //MessageBox.Show(groupKey + "\n");
                        double mass = Convert.ToDouble(compMass[groupKey]);

                        if (num != 0)
                        {
                            formular1 += "(" + groupKey + ")" + Convert.ToString(num);
                            compMW += mass * Convert.ToInt16(num);
                        }
                        if (idx != 1)
                        {
                            fstr += "," + Convert.ToString(num);
                        }
                        else
                        {
                            fstr += Convert.ToString(num);
                        }
                    }

                    fstr += "]";

                    foreach (var numList1 in modList)
                    {
                        string sstr = "-[";
                        string formular2 = "";
                        idx = 0;
                        double modMW = compMW;
                        foreach (var num1 in numList1)
                        {
                            try
                            {
                                symVal.Add(modSymList[idx], num1);
                            }
                            catch
                            {
                                symVal[modSymList[idx]] = num1;
                            }
                            string groupKey = Convert.ToString(modIndList[idx++]);
                            double mass = Convert.ToDouble(modMass[groupKey]);
                            if (num1 != 0)
                            {
                                formular2 += "(" + groupKey + ")" + Convert.ToString(num1);
                                modMW += mass * Convert.ToInt16(num1);
                            }
                            if (idx != 1)
                            {
                                sstr += "," + Convert.ToString(num1);
                            }
                            else
                            {
                                sstr += Convert.ToString(num1);
                            }
                        }
                        sstr += "]";

                        //first check the algebraic rule
                        bool check = false;
                        foreach (string comp in compSymList)
                        {
                            //List<String> testList = exprList[comp];
                            //MessageBox.Show(testList[0]);
                            if (exprList.ContainsKey(comp))
                            {
                                //get bound
                                List<String> boundList = exprList[comp];
                                double lowerBound = mathParse(boundList[0]);
                                double upperBound = mathParse(boundList[1]);
                                if (lowerBound > upperBound)
                                {
                                    lowerBound = (lowerBound + upperBound) / 2 - (lowerBound - upperBound) / 2;
                                    upperBound = (lowerBound + upperBound) / 2 + (lowerBound - upperBound) / 2;
                                }
                                int val = symVal[comp];
                                if (val <= lowerBound || val >= upperBound)
                                {
                                    check = true;
                                }
                            }
                        }

                        foreach (string mod in modSymList)
                        {
                            if (exprList.ContainsKey(mod))
                            {
                                //get bound
                                List<String> boundList = exprList[mod];
                                double lowerBound = mathParse(boundList[0]);
                                double upperBound = mathParse(boundList[1]);
                                int val = symVal[mod];
                                if (val <= lowerBound || val >= upperBound)
                                {
                                    check = true;
                                }
                            }
                        }

                        if (formular1 + formular2 != "" && check == false)
                        {
                            listFile.WriteLine(formular1 + formular2 + "," + Convert.ToString(modMW) + "," + "\"" + fstr + sstr + "\"");
                        }
                        compKey.Add(fstr + sstr);
                    }
                }
                //MessageBox.Show(Convert.ToString(compKey.Count()));
                listFile.Close();

            }
        }

        private string getSortString(string compound_key)
        {
            string ret_key = "";
            string[] parts = compound_key.Split('-');
            string modStr = parts[1];
            modStr = modStr.TrimStart('[', ']').TrimEnd('[', ']');
            string[] mods = modStr.Split(',');
            foreach (var mod in mods)
            {
                ret_key += mod;
            }

            return ret_key;
        }


        //expression parser here 
        //each group has a list of two rules
        SortedList<String, List<String>> exprList = new SortedList<String, List<String>>();
        List<String> compSymList = new List<String>();
        List<String> modSymList = new List<String>();
        private void algebraicRulesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Expressions exprForm = new Expressions(this);
            //exprForm.ShowDialog();

            //symVal.Add("W", 1);
            //symVal.Add("X", 2);
            //mathParse(expr);
            //symVal.Clear();


            OpenFileDialog fDialog = new OpenFileDialog();
            fDialog.Title = "Open Algebraic Rules";
            fDialog.Filter = "TXT Files|*.txt";

            if (fDialog.ShowDialog() == DialogResult.Cancel)
                return;

            try
            {
                this.fsInputLCMSFile = new FileStream(fDialog.FileName, FileMode.Open, FileAccess.Read);
                //Here parse the control file and get the parameter
                StreamReader rdr = new StreamReader(this.fsInputLCMSFile);
                exprList.Clear();
                string hdr = rdr.ReadLine();
                bool DEF = false;
                bool RULES = false;
                while (rdr.Peek() >= 0)
                {
                    string str = rdr.ReadLine();

                    if (str == "")
                    {
                        continue;
                    }

                    if (str == "Def")
                    {
                        DEF = true;
                        continue;
                    }

                    else if (str == "End of Def")
                    {
                        DEF = false;
                        continue;
                    }

                    else if (str == "Rules")
                    {
                        RULES = true;
                        continue;
                    }

                    else if (str == "End of Rules")
                    {
                        RULES = false;
                        continue;
                    }

                    if (DEF)
                    {
                        string[] parts = str.Split('-');
                        string[] comps = parts[0].TrimStart('\t').TrimStart('[').TrimEnd(']').Split(',');
                        string[] mods = parts[1].TrimStart('\t').TrimStart('[').TrimEnd(']').Split(',');

                        foreach (string comp in comps)
                        {
                            compSymList.Add(comp);
                        }
                        foreach (string mod in mods)
                        {
                            modSymList.Add(mod);
                        }
                    }

                    else if (RULES)
                    {
                        //example rule A = 0 to (W+X+Y)*3
                        string[] parts = str.Split('=');
                        string key = parts[0].TrimStart('\t').TrimEnd(' ');
                        string[] bound = parts[1].Split('~');
                        List<string> exprBound = new List<string>();
                        exprBound.Add(bound[0]);
                        exprBound.Add(bound[1]);
                        exprList.Add(key, exprBound);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening file", "File Error",
                                 MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public string expr;
        private SortedList<String, int> symVal = new SortedList<string, int>();
        private double mathParse(String expr)
        {
            while (expr.Contains("("))
            {
                expr = Regex.Replace(expr, @"\(([^\ (]*?)\)", m => mathParse(m.Groups[1].Value).ToString());
            }
            double r = 0;
            //foreach (Match m in Regex.Matches("+" + expr, @"\D ?-?[\d.]+"))
            foreach (Match m in Regex.Matches("+" + expr, @"[^A-Za-z\d] ?-?[a-zA-Z|\d]+"))
            {
                var o = m.Value[0];
                String v = m.Value.Substring(1).Trim();
                //v.TrimStart(' ').TrimEnd(' ');
                double num;
                bool isNum = double.TryParse(v, out num);

                if (!isNum)
                {
                    try
                    {
                        num = Convert.ToDouble(symVal[v]);
                    }
                    catch
                    {
                        throw new Exception();
                        //return 0;
                    }
                }
                //var v = float.Parse(m.Value.Substring(1));
                //MessageBox.Show(Convert.ToString(v));
                r = o == '+' ? r + num : o == '-' ? r - num : o == '*' ? r * num : r / num;
            }
            //MessageBox.Show(Convert.ToString(r));
            return r;
        }

        //get the group information from the generator tag
        private string[] exprs;
        private void showSelectedButton_Click(object sender, System.EventArgs e)
        {
            Object selectedItem1, selectedItem2, selectedItem3, selectedItem4, selectedItem5;
            selectedItem1 = comboBox1.SelectedItem;
            selectedItem2 = comboBox2.SelectedItem;
            selectedItem3 = comboBox3.SelectedItem;
            selectedItem4 = comboBox4.SelectedItem;
            selectedItem5 = comboBox5.SelectedItem;
            //selectedItem5 = comboBox5.SelectedItem;

            string low1 = Convert.ToString(textBox2.Text), up1 = Convert.ToString(textBox3.Text);
            string low2 = Convert.ToString(textBox4.Text), up2 = Convert.ToString(textBox5.Text);
            string low3 = Convert.ToString(textBox6.Text), up3 = Convert.ToString(textBox7.Text);
            string low4 = Convert.ToString(textBox8.Text), up4 = Convert.ToString(textBox9.Text);
            string low5 = Convert.ToString(textBox10.Text), up5 = Convert.ToString(textBox11.Text);
            string low6 = Convert.ToString(textBox12.Text), up6 = Convert.ToString(textBox13.Text);
            //string low6 = Convert.ToString(textBox12.Text), up6 = Convert.ToString(textBox13.Text);
            exprs = new string[] { low1, up1, low2, up2, low3, up3, low4, up4, low5, up5, low6, up6 };

            if (comboBox1.SelectedIndex == -1)
            {
                MessageBox.Show("group A is not defined!\n");
                return;
            }
            else if (comboBox2.SelectedIndex == -1)
            {
                MessageBox.Show("group B is not defined!\n");
                return;
            }
            else if (comboBox3.SelectedIndex == -1)
            {
                MessageBox.Show("group C is not defined!\n");
                return;
            }
            else if (comboBox4.SelectedIndex == -1)
            {
                MessageBox.Show("group D is not defined!\n");
                return;
            }
            else if (textBox1.Text == "")
            {
                MessageBox.Show("modification is not defined!\n");
                return;
            }
            // MessageBox.Show("Selected Item Text: " + selectedItem.ToString() + "\n" + "Index: " + selectedIndex.ToString());
            else
            {
                groupMassCal(selectedItem1, selectedItem2, selectedItem3, selectedItem4, selectedItem5, textBox1, textBox14);
            }
            this.compoundListToolStripMenuItem.Enabled = true;
            return;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            comboBox4.Items.Clear();
            comboBox5.Items.Clear();
            comboBox1.Text = "";
            comboBox2.Text = "";
            comboBox3.Text = "";
            comboBox4.Text = "";
            comboBox5.Text = "";

            //get the column names from the datagridview
            DataTable residueTable = (DataTable)dataGridView1.DataSource;
            int range = residueTable.Rows.Count;
            for (int i = 0; i < range; i++)
            {
                try
                {
                    String residueName = (String)residueTable.Rows[i][0];
                    comboBox1.Items.Add(residueName);
                    comboBox2.Items.Add(residueName);
                    comboBox3.Items.Add(residueName);
                    comboBox4.Items.Add(residueName);
                    comboBox5.Items.Add(residueName);
                }
                catch (Exception)
                {
                    MessageBox.Show("There is at least one residue without a name!");
                }
            }
            comboBox1.SelectedIndex = -1;
            comboBox2.SelectedIndex = -1;
            comboBox3.SelectedIndex = -1;
            comboBox4.SelectedIndex = -1;
            comboBox5.SelectedIndex = -1;
            return;
        }

        private void setParametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Please set parameters on the Generator page");
            tabControl1.SelectedTab = tabPage2;
        }

        private void contentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
           // System.Diagnostics.Process.Start("Help.chm");
            Help.ShowHelp(this,"Help.chm");
        }

        private void label11_Click(object sender, EventArgs e)
        {
            MessageBox.Show(dataGridView2, "The \"Formula\" is a linear combination of composition, such as A+B+C(no space are allowed in between)\n\nThe \"Relationship\" should be =, > or <\n\nThe \"Constrain\" should be a number or a math linear expression\n\tTo represent all the even numbers type in 2n\n\tTo represent all the odd numbers type in 2n-1 or 2n+1 (no space are allowed in between)\n");
        }
    }
}
